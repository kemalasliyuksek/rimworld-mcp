import { Client } from "@modelcontextprotocol/sdk/client/index.js";
import { InMemoryTransport } from "@modelcontextprotocol/sdk/inMemory.js";
import { beforeEach, describe, expect, it } from "vitest";
import { BridgeRequestError, type BridgeConnection, type QueryValue } from "../src/bridge-client.js";
import { BridgeNotFoundError } from "../src/discovery.js";
import { createServer } from "../src/server.js";
import { tools } from "../src/tools.js";

class FakeBridge implements BridgeConnection {
  calls: { method: string; path: string; params: Record<string, QueryValue> | undefined }[] = [];
  next: () => Promise<string> = () => Promise.resolve('{"ok":true}');

  get(path: string, params?: Record<string, QueryValue>): Promise<string> {
    this.calls.push({ method: "GET", path, params });
    return this.next();
  }

  post(path: string, params?: Record<string, QueryValue>): Promise<string> {
    this.calls.push({ method: "POST", path, params });
    return this.next();
  }
}

describe("MCP server", () => {
  let bridge: FakeBridge;
  let client: Client;

  beforeEach(async () => {
    bridge = new FakeBridge();
    const [clientTransport, serverTransport] = InMemoryTransport.createLinkedPair();
    await createServer(bridge, "0.0.0-test").connect(serverTransport);
    client = new Client({ name: "test", version: "0" });
    await client.connect(clientTransport);
  });

  it("offers every tool, and only the setup tools are not read-only", async () => {
    const { tools: listed } = await client.listTools();

    expect(listed.map((t) => t.name).sort()).toEqual(tools.map((t) => t.name).sort());
    for (const tool of listed) {
      expect(tool.annotations?.readOnlyHint, tool.name).toBe(
        ![
          "configure_new_game",
          "show_site",
          "set_start_options",
          "configure_ideo",
          "find_starting_pawn",
          "swap_starting_pawns",
          "edit_starting_pawn",
          "set_game_settings",
          "configure_mods",
          "set_mod_settings",
          "set_work_priorities",
          "set_schedule",
          "set_drafted",
          "move_pawns",
          "give_pawn_order",
          "use_gizmo",
          "attack_target",
          "use_designator",
          "draw_plan",
          "remove_plan",
          "set_game_speed",
          "set_research",
          "add_bill",
          "remove_bill",
          "set_crop",
          "answer_dialog",
          "answer_letter",
          "spawn_thing",
          "spawn_pawn",
          "trigger_incident",
          "complete_research",
          "edit_pawn",
          "reveal_map",
          "set_pawn_policies",
          "set_stockpile",
          "trade",
          "form_caravan",
          "send_caravan",
          "use_ability",
          "edit_bill",
          "create_policy",
          "edit_policy",
          "delete_policy",
          "set_area",
        ].includes(tool.name),
      );
      expect(tool.annotations?.destructiveHint, tool.name).toBe(false);
      expect(tool.description, tool.name).toBeTruthy();
    }
  });

  it("tells the assistant to look at the game before answering", () => {
    expect(client.getInstructions()).toMatch(/get_colony_overview/);
  });

  it("passes arguments to the matching endpoint and returns its answer", async () => {
    const result = await client.callTool({
      name: "list_pawns",
      arguments: { group: "prisoners", limit: 5 },
    });

    expect(bridge.calls).toEqual([
      { method: "GET", path: "/v1/pawns", params: { group: "prisoners", limit: 5 } },
    ]);
    expect(result.isError).toBeFalsy();
    expect(result.content).toEqual([{ type: "text", text: '{"ok":true}' }]);
  });

  it("sends the setup tools as POST, and every other tool as a GET", async () => {
    await client.callTool({ name: "show_site", arguments: { tile: 42 } });
    await client.callTool({ name: "configure_ideo", arguments: { preset: "Unity_Family" } });
    await client.callTool({ name: "edit_starting_pawn", arguments: { index: 0, skills: "Shooting:12" } });
    await client.callTool({ name: "set_work_priorities", arguments: { pawn: "Ayla", priorities: "Growing:1" } });
    await client.callTool({ name: "set_schedule", arguments: { pawn: "Ayla", hours: "Sleep:22-6" } });
    await client.callTool({ name: "search_sites", arguments: { caves: "exclude", shape: "crescent" } });

    expect(bridge.calls).toEqual([
      { method: "POST", path: "/v1/sites/show", params: { tile: 42 } },
      { method: "POST", path: "/v1/ideo/configure", params: { preset: "Unity_Family" } },
      { method: "POST", path: "/v1/starting-pawns/edit", params: { index: 0, skills: "Shooting:12" } },
      { method: "POST", path: "/v1/pawn/work", params: { pawn: "Ayla", priorities: "Growing:1" } },
      { method: "POST", path: "/v1/pawn/schedule", params: { pawn: "Ayla", hours: "Sleep:22-6" } },
      { method: "GET", path: "/v1/sites/search", params: { caves: "exclude", shape: "crescent" } },
    ]);
  });

  it("passes an icon sheet to the assistant as an image, separate from its text", async () => {
    bridge.next = () => Promise.resolve('{"icons":["A"],"image_png_base64":"aGVsbG8="}');
    const result = await client.callTool({ name: "view_ideo_icons", arguments: {} });

    expect(result.content).toEqual([
      { type: "text", text: '{"icons":["A"]}' },
      { type: "image", data: "aGVsbG8=", mimeType: "image/png" },
    ]);
  });

  it("rejects arguments that fail validation without calling the game", async () => {
    const result = await client.callTool({ name: "list_pawns", arguments: { group: "dragons" } });

    expect(result.isError).toBe(true);
    expect(bridge.calls).toEqual([]);
  });

  it("reports game errors as tool errors with their code", async () => {
    bridge.next = () => Promise.reject(new BridgeRequestError("no_game_loaded", "At the main menu."));
    const result = await client.callTool({ name: "get_colony_overview", arguments: {} });

    expect(result.isError).toBe(true);
    expect(result.content).toEqual([{ type: "text", text: "no_game_loaded: At the main menu." }]);
  });

  it("explains a missing game rather than failing the connection", async () => {
    bridge.next = () => Promise.reject(new BridgeNotFoundError("RimWorld is not running."));
    const result = await client.callTool({ name: "get_colony_overview", arguments: {} });

    expect(result.isError).toBe(true);
    expect(result.content).toEqual([
      { type: "text", text: "game_not_connected: RimWorld is not running." },
    ]);
  });
});
