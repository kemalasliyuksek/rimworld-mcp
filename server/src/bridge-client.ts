import { BridgeNotFoundError, readDiscoveryFile } from "./discovery.js";

const REQUEST_TIMEOUT_MS = 15_000;

export type QueryValue = string | number | undefined;

export interface BridgeConnection {
  get(path: string, params?: Record<string, QueryValue>): Promise<string>;
  post(path: string, params?: Record<string, QueryValue>): Promise<string>;
}

/** An error the assistant can explain to the player, such as "no game is loaded". */
export class BridgeRequestError extends Error {
  constructor(
    readonly code: string,
    message: string,
  ) {
    super(message);
  }
}

export class BridgeClient implements BridgeConnection {
  constructor(private readonly discoveryPath: string) {}

  get(path: string, params: Record<string, QueryValue> = {}): Promise<string> {
    return this.send("GET", path, params);
  }

  post(path: string, params: Record<string, QueryValue> = {}): Promise<string> {
    return this.send("POST", path, params);
  }

  private async send(
    method: "GET" | "POST",
    path: string,
    params: Record<string, QueryValue>,
  ): Promise<string> {
    const location = await readDiscoveryFile(this.discoveryPath);

    const url = new URL(path, `http://127.0.0.1:${location.port}`);
    for (const [key, value] of Object.entries(params)) {
      if (value !== undefined) {
        url.searchParams.set(key, String(value));
      }
    }

    let response: Response;
    try {
      response = await fetch(url, {
        method,
        headers: { Authorization: `Bearer ${location.token}` },
        signal: AbortSignal.timeout(REQUEST_TIMEOUT_MS),
      });
    } catch (error) {
      if (error instanceof Error && error.name === "TimeoutError") {
        throw new BridgeRequestError(
          "timeout",
          "The game did not respond in time. It may be frozen or busy.",
        );
      }
      // A file left behind by a crash points at a port nobody is listening on.
      throw new BridgeNotFoundError(
        "Could not reach the game. RimWorld is probably closed, or it crashed and left a stale discovery file.",
      );
    }

    const body = await response.text();
    if (response.ok) {
      return body;
    }
    throw toRequestError(response.status, body);
  }
}

function toRequestError(status: number, body: string): BridgeRequestError {
  try {
    const parsed = JSON.parse(body) as { error?: { code?: unknown; message?: unknown } };
    const { code, message } = parsed.error ?? {};
    if (typeof code === "string" && typeof message === "string") {
      return new BridgeRequestError(code, message);
    }
  } catch {
    // Fall through. The status is all that is known.
  }
  return new BridgeRequestError("http_error", `The game answered with HTTP status ${status}.`);
}
