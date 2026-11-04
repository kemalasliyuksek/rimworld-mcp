import { createServer, type IncomingMessage, type Server, type ServerResponse } from "node:http";
import { mkdtemp, rm, writeFile } from "node:fs/promises";
import type { AddressInfo } from "node:net";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { afterEach, beforeEach, describe, expect, it } from "vitest";
import { BridgeClient, BridgeRequestError } from "../src/bridge-client.js";
import { BridgeNotFoundError } from "../src/discovery.js";

describe("BridgeClient", () => {
  let directory: string;
  let discoveryPath: string;
  let server: Server;
  let lastRequest: IncomingMessage | undefined;
  let respond: (response: ServerResponse) => void;

  async function writeDiscovery(port: number, token = "secret"): Promise<void> {
    await writeFile(
      discoveryPath,
      JSON.stringify({ format_version: 1, port, token, bridge_version: "0.1.0", process_id: 1 }),
    );
  }

  beforeEach(async () => {
    directory = await mkdtemp(join(tmpdir(), "rimworld-mcp-"));
    discoveryPath = join(directory, "bridge.json");
    respond = (response) => response.end("{}");
    server = createServer((request, response) => {
      lastRequest = request;
      respond(response);
    });
    await new Promise<void>((resolve) => server.listen(0, "127.0.0.1", resolve));
    await writeDiscovery((server.address() as AddressInfo).port);
  });

  afterEach(async () => {
    await new Promise((resolve) => server.close(resolve));
    await rm(directory, { recursive: true, force: true });
  });

  it("sends the token and encodes parameters", async () => {
    respond = (response) => response.end('{"ok":true}');
    const body = await new BridgeClient(discoveryPath).get("/v1/defs/search", {
      q: "çelik duvar",
      limit: 5,
      type: undefined,
    });

    expect(body).toBe('{"ok":true}');
    expect(lastRequest?.headers.authorization).toBe("Bearer secret");
    const url = new URL(lastRequest?.url ?? "", "http://localhost");
    expect(url.pathname).toBe("/v1/defs/search");
    expect(url.searchParams.get("q")).toBe("çelik duvar");
    expect(url.searchParams.get("limit")).toBe("5");
    expect(url.searchParams.has("type")).toBe(false);
  });

  it("turns bridge errors into coded errors", async () => {
    respond = (response) => {
      response.statusCode = 409;
      response.end('{"error":{"code":"no_game_loaded","message":"At the main menu."}}');
    };
    const error = await new BridgeClient(discoveryPath).get("/v1/overview").catch((e: unknown) => e);

    expect(error).toBeInstanceOf(BridgeRequestError);
    expect((error as BridgeRequestError).code).toBe("no_game_loaded");
    expect((error as BridgeRequestError).message).toBe("At the main menu.");
  });

  it("survives an error response that is not JSON", async () => {
    respond = (response) => {
      response.statusCode = 500;
      response.end("oops");
    };
    await expect(new BridgeClient(discoveryPath).get("/v1/overview")).rejects.toThrow(/HTTP status 500/);
  });

  it("reports a stale discovery file as a closed game", async () => {
    const closed = createServer();
    await new Promise<void>((resolve) => closed.listen(0, "127.0.0.1", resolve));
    const deadPort = (closed.address() as AddressInfo).port;
    await new Promise((resolve) => closed.close(resolve));
    await writeDiscovery(deadPort);

    await expect(new BridgeClient(discoveryPath).get("/v1/overview")).rejects.toBeInstanceOf(BridgeNotFoundError);
  });

  it("picks up a restarted game without being recreated", async () => {
    const client = new BridgeClient(discoveryPath);
    await client.get("/v1/status");

    await writeDiscovery((server.address() as AddressInfo).port, "new-token");
    await client.get("/v1/status");

    expect(lastRequest?.headers.authorization).toBe("Bearer new-token");
  });
});
