import { mkdtemp, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { afterEach, beforeEach, describe, expect, it } from "vitest";
import { BridgeNotFoundError, readDiscoveryFile } from "../src/discovery.js";

const valid = {
  format_version: 1,
  port: 5000,
  token: "abc",
  bridge_version: "0.1.0",
  process_id: 42,
};

describe("readDiscoveryFile", () => {
  let directory: string;
  let path: string;

  beforeEach(async () => {
    directory = await mkdtemp(join(tmpdir(), "rimworld-mcp-"));
    path = join(directory, "bridge.json");
  });

  afterEach(async () => {
    await rm(directory, { recursive: true, force: true });
  });

  it("reads a valid file", async () => {
    await writeFile(path, JSON.stringify(valid));
    await expect(readDiscoveryFile(path)).resolves.toEqual(valid);
  });

  it("explains that the game is not running when the file is missing", async () => {
    await expect(readDiscoveryFile(path)).rejects.toThrow(/not running/);
  });

  it.each([
    ["broken JSON", "{not json"],
    ["a missing token", JSON.stringify({ ...valid, token: undefined })],
    ["an empty token", JSON.stringify({ ...valid, token: "" })],
    ["an impossible port", JSON.stringify({ ...valid, port: 70000 })],
    ["a non-object", "[]"],
  ])("rejects %s", async (_name, content) => {
    await writeFile(path, content);
    await expect(readDiscoveryFile(path)).rejects.toBeInstanceOf(BridgeNotFoundError);
  });

  it("asks for an update when the formats differ", async () => {
    await writeFile(path, JSON.stringify({ ...valid, format_version: 2 }));
    await expect(readDiscoveryFile(path)).rejects.toThrow(/do not match/);
  });
});
