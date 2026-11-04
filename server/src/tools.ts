import { z } from "zod";

export interface ToolDefinition {
  name: string;
  title: string;
  description: string;
  /** Bridge endpoint. The tool's arguments become its query string. */
  path: string;
  /** Set for the rare tool that changes something. Everything else reads with GET. */
  changesUiState?: true;
  inputSchema: z.ZodRawShape;
}

const mapParam = z
  .number()
  .int()
  .min(0)
  .optional()
  .describe("Map id. Defaults to the map the player is currently looking at.");

const presence = (what: string) =>
  z
    .enum(["any", "require", "exclude"])
    .optional()
    .describe(`Whether the tile must have, or must not have, ${what}. Defaults to any.`);

export const tools: readonly ToolDefinition[] = [
  {
    name: "get_colony_overview",
    title: "Colony overview",
    description:
      "Start here for any question about the player's game. Returns date, season, weather, colonist count and mood, days of food, threats, current research, active alerts and unread letters.",
    path: "/v1/overview",
    inputSchema: { map: mapParam },
  },
  {
    name: "get_player_focus",
    title: "Player focus",
    description:
      'What the player is looking at right now: selected pawns and things, camera area, open windows, and the text and choices of any event dialog on screen. Use it to work out what "this" or "that" means, and whenever the player asks what to do about a decision they are facing.',
    path: "/v1/focus",
    inputSchema: {},
  },
  {
    name: "get_alerts",
    title: "Active alerts",
    description:
      "The alerts on the right side of the screen, with the game's full explanation of each. The overview only has their labels.",
    path: "/v1/alerts",
    inputSchema: {},
  },
  {
    name: "list_pawns",
    title: "List pawns",
    description:
      "Compact list of a group of pawns: position, health, mood, current activity and best skills. Passions are marked * (minor) and ** (major). The colonists group spans all maps and caravans; the other groups cover one map.",
    path: "/v1/pawns",
    inputSchema: {
      group: z
        .enum([
          "colonists",
          "prisoners",
          "slaves",
          "animals",
          "mechanoids",
          "hostiles",
          "visitors",
          "wildlife",
        ])
        .optional()
        .describe("Defaults to colonists."),
      limit: z.number().int().min(1).max(500).optional(),
      map: mapParam,
    },
  },
  {
    name: "get_pawn",
    title: "Pawn details",
    description:
      "Everything the player can see about one pawn: background, traits, skills and passions, needs, each mood thought, health conditions, capacities, gear, work priorities (1 is highest, 0 is off), schedule and relations.",
    path: "/v1/pawn",
    inputSchema: {
      id: z.string().min(1).describe("Pawn id from list_pawns, or the pawn's name."),
    },
  },
  {
    name: "get_threats",
    title: "Threats and defenses",
    description:
      "Visible enemies grouped by faction and kind, with their location and weapons, plus the colony's fighters and turrets. Use it for raids and any defense question.",
    path: "/v1/threats",
    inputSchema: { map: mapParam },
  },
  {
    name: "get_resources",
    title: "Resources",
    description:
      "Stored items by category, silver, wealth breakdown (which drives raid size), and the weapons and apparel in storage.",
    path: "/v1/resources",
    inputSchema: { map: mapParam },
  },
  {
    name: "get_base",
    title: "Base layout",
    description:
      "The colony's infrastructure: rooms with their stats, power grids, stockpile and growing zones with crop progress, work benches with their bills, and beds.",
    path: "/v1/base",
    inputSchema: {
      sections: z
        .string()
        .optional()
        .describe(
          "Comma-separated subset of rooms, power, zones, production, beds. Defaults to all of them.",
        ),
      map: mapParam,
    },
  },
  {
    name: "get_research",
    title: "Research",
    description: "The current project and the projects that can be started now, cheapest first.",
    path: "/v1/research",
    inputSchema: {
      limit: z.number().int().min(1).max(500).optional(),
    },
  },
  {
    name: "get_recent_events",
    title: "Recent events",
    description:
      "The game's history of letters and messages, newest first, including events from before this session. Use it to find out what just happened or why something changed.",
    path: "/v1/events",
    inputSchema: {
      limit: z.number().int().min(1).max(200).optional(),
    },
  },
  {
    name: "get_world",
    title: "World state",
    description:
      "Everything beyond the current map: factions and goodwill, quests, caravans, all open maps, and the colony's ideoligion.",
    path: "/v1/world",
    inputSchema: {},
  },
  {
    name: "view_map",
    title: "View map",
    description:
      "Renders part of the map as text, with a legend, so you can reason about layout and positions without a screenshot. Defaults to the area around the player's camera.",
    path: "/v1/map/view",
    inputSchema: {
      x: z.number().int().optional().describe("West edge of the area."),
      z: z.number().int().optional().describe("South edge of the area."),
      width: z.number().int().min(1).max(120).optional().describe("Defaults to 60."),
      height: z.number().int().min(1).max(120).optional().describe("Defaults to 60."),
      map: mapParam,
    },
  },
  {
    name: "inspect_cell",
    title: "Inspect cell",
    description:
      "Everything visible on one map cell: terrain, fertility, roof, temperature, room, zone and things.",
    path: "/v1/map/cell",
    inputSchema: {
      x: z.number().int().min(0),
      z: z.number().int().min(0),
      map: mapParam,
    },
  },
  {
    name: "search_defs",
    title: "Search game data",
    description:
      "Searches the game's definitions by name, label or description: items, buildings, research, recipes, traits, diseases and more. Covers the base game, DLCs and every active mod, in the player's language. Use it instead of relying on memory, since mods can change anything.",
    path: "/v1/defs/search",
    inputSchema: {
      q: z.string().min(1).describe("Text to search for."),
      type: z.string().optional().describe("Limit to a def type, such as ThingDef or RecipeDef."),
      limit: z.number().int().min(1).max(100).optional(),
    },
  },
  {
    name: "get_def",
    title: "Game data details",
    description:
      "The full data of one definition as the running game sees it, with all mod patches applied: stats, costs, requirements and so on. Find names with search_defs.",
    path: "/v1/defs/get",
    inputSchema: {
      def: z.string().min(1).describe("Exact defName."),
      type: z
        .string()
        .optional()
        .describe("Def type. Only needed when several types share the name."),
    },
  },
  {
    name: "list_mods",
    title: "Active mods",
    description: "The mods running in the player's game, in load order.",
    path: "/v1/mods",
    inputSchema: {},
  },
  {
    name: "get_world_settings",
    title: "World settings",
    description:
      "Works before a game starts, on the world and site selection screens. Returns the seed and world settings, the selected tile, and how many tiles have each biome, hilliness and feature. Use the feature names here with search_sites.",
    path: "/v1/sites/world",
    inputSchema: {},
  },
  {
    name: "search_sites",
    title: "Find places to settle",
    description:
      'Searches the whole world for tiles that match what the player asks for, whatever that is, and works before a game starts. Every condition is optional. With shape "crescent" it predicts each tile\'s mountains from the world seed and ranks tiles by how well rock wraps around open ground with a single way in. min_rock_pct and max_rock_pct filter by how much of the map is predicted to be rock. Cave tunnels contain insect hives, which is worth mentioning when a player asks for caves. A search has a time limit and checks a random sample on large planets, so whole_world_checked may be false. Narrowing by biome, hilliness or features lets it reach more of the matching tiles, and next_offset continues with tiles it has not checked yet. Look at the preview from get_site before recommending a site. Do not suggest generating another world unless this finds nothing good.',
    path: "/v1/sites/search",
    inputSchema: {
      biomes: z.string().optional().describe("Comma-separated biome defNames."),
      hilliness: z
        .string()
        .optional()
        .describe("Comma-separated subset of Flat, SmallHills, LargeHills, Mountainous."),
      min_growing_days: z
        .number()
        .int()
        .min(0)
        .max(60)
        .optional()
        .describe("Outdoor growing days out of 60. 60 means crops grow all year."),
      min_coldest_c: z.number().int().optional().describe("The coldest temperature must be at least this."),
      max_hottest_c: z.number().int().optional().describe("The hottest temperature must be at most this."),
      min_rainfall: z.number().int().min(0).optional().describe("Yearly rainfall in mm."),
      max_rainfall: z.number().int().min(0).optional().describe("Yearly rainfall in mm."),
      max_diseases_per_year: z.number().int().min(0).optional(),
      max_pollution_pct: z.number().int().min(0).max(100).optional(),
      caves: presence("cave tunnels, which contain insect hives"),
      coast: presence("an ocean coast"),
      lake: presence("a lake shore"),
      river: presence("a river"),
      road: presence("a road"),
      exclude_features: z
        .string()
        .optional()
        .describe("Comma-separated feature defNames to avoid, from get_world_settings."),
      require_features: z
        .string()
        .optional()
        .describe("Comma-separated feature defNames that must all be present."),
      shape: z.literal("crescent").optional().describe("Rank by predicted mountain shape."),
      min_rock_pct: z.number().int().min(0).max(100).optional().describe("Predicted share of the map that is rock."),
      max_rock_pct: z.number().int().min(0).max(100).optional().describe("Predicted share of the map that is rock."),
      min_pocket_score: z.number().int().min(0).max(100).optional(),
      limit: z.number().int().min(1).max(20).optional().describe("Defaults to 8."),
      offset: z
        .number()
        .int()
        .min(0)
        .optional()
        .describe("Continue a search from the next_offset of an earlier result, with the same filters."),
    },
  },
  {
    name: "get_site",
    title: "Site details",
    description:
      "Full details of one tile, defaulting to the one selected in the game, with a text preview of its predicted mountains so you can judge the shape yourself. The preview is a prediction and shows no caves, ores or ruins.",
    path: "/v1/sites/get",
    inputSchema: {
      tile: z.number().int().min(0).optional().describe("Tile id from search_sites."),
    },
  },
  {
    name: "show_site",
    title: "Show site in game",
    description:
      "On the site selection screen, moves the player's camera to a tile and selects it, so they can see it without hunting for it on the globe. It also zooms in if the camera is far away. It changes nothing else, and the player still confirms the choice themselves. Call it for the site you recommend.",
    path: "/v1/sites/show",
    changesUiState: true,
    inputSchema: {
      tile: z.number().int().min(0).describe("Tile id from search_sites."),
    },
  },
  {
    name: "get_game_status",
    title: "Connection status",
    description:
      "Checks the connection to the game and reports versions, language, and whether a game is loaded or the player is at the main menu.",
    path: "/v1/status",
    inputSchema: {},
  },
];
