import { readFile } from "node:fs/promises";
import { homedir } from "node:os";
import { join } from "node:path";
import { z } from "zod";

const SUPPORTED_FORMAT_VERSION = 1;

const discoverySchema = z.object({
  format_version: z.number().int(),
  port: z.number().int().min(1).max(65535),
  token: z.string().min(1),
  bridge_version: z.string(),
  process_id: z.number().int(),
});

export type BridgeLocation = z.infer<typeof discoverySchema>;

export function defaultDiscoveryPath(): string {
  return (
    process.env["RIMWORLD_MCP_DISCOVERY_FILE"] ??
    join(homedir(), ".rimworld-mcp", "bridge.json")
  );
}

export class BridgeNotFoundError extends Error {}

/**
 * Finds the running game. The mod writes this file on launch with a fresh port and token, so it is
 * read again on every request and a restarted game is picked up without restarting this server.
 */
export async function readDiscoveryFile(path: string): Promise<BridgeLocation> {
  let raw: string;
  try {
    raw = await readFile(path, "utf8");
  } catch (error) {
    if ((error as NodeJS.ErrnoException).code === "ENOENT") {
      throw new BridgeNotFoundError(
        "RimWorld is not running, or the RimWorld MCP mod is not active. Ask the player to start the game with the mod enabled.",
      );
    }
    throw error;
  }

  let parsed: unknown;
  try {
    parsed = JSON.parse(raw);
  } catch {
    throw new BridgeNotFoundError(`The discovery file at ${path} is not valid JSON.`);
  }

  const result = discoverySchema.safeParse(parsed);
  if (!result.success) {
    throw new BridgeNotFoundError(`The discovery file at ${path} has an unexpected format.`);
  }
  if (result.data.format_version !== SUPPORTED_FORMAT_VERSION) {
    throw new BridgeNotFoundError(
      `The RimWorld MCP mod and server versions do not match (discovery format ${result.data.format_version}, expected ${SUPPORTED_FORMAT_VERSION}). Update both.`,
    );
  }
  return result.data;
}
