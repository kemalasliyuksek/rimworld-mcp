import { z } from "zod";

export interface ToolDefinition {
  name: string;
  title: string;
  description: string;
  /** Bridge endpoint. The tool's arguments become its query string. */
  path: string;
  /** Set for the rare tool that acts on the player's request. Everything else reads with GET. */
  acts?: true;
  /** The endpoint returns an image in image_png_base64, sent on to the assistant as an image. */
  returnsImage?: true;
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
      "Everything the player can see about one pawn: background, traits, skills and passions, needs, each mood thought, health conditions, capacities, gear, work priorities (1 is highest, 0 is off, and work they cannot do is marked disabled), whether manual priorities are on, schedule and relations. While manual priorities are off, every active job reads as 3.",
    path: "/v1/pawn",
    inputSchema: {
      id: z.string().min(1).describe("Pawn id from list_pawns, or the pawn's name."),
    },
  },
  {
    name: "set_work_priorities",
    title: "Set work priorities",
    description:
      "Only when the player asks. Changes one colonist's priorities on the game's Work tab and leaves their other work as it is. Read get_pawn first for the work names and for what they cannot do. Passing 1, 2 or 4 turns on manual priorities for the whole colony, which is the checkbox on that tab, so mention it to the player. Colonists finish what they are doing, unless the work they are doing is turned off.",
    path: "/v1/pawn/work",
    acts: true,
    inputSchema: {
      pawn: z.string().min(1).describe("Pawn id from list_pawns, or the pawn's name."),
      priorities: z
        .string()
        .optional()
        .describe("Work type defNames with a priority from 0 to 4, where 1 is highest and 0 is off, for example Doctor:1,Research:2,Mining:0."),
      manual_priorities: z
        .enum(["true", "false"])
        .optional()
        .describe("Turns manual priorities on or off for the whole colony."),
    },
  },
  {
    name: "set_schedule",
    title: "Set schedule",
    description:
      "Only when the player asks. Changes one colonist's hours on the game's Schedule tab and leaves the other hours as they are.",
    path: "/v1/pawn/schedule",
    acts: true,
    inputSchema: {
      pawn: z.string().min(1).describe("Pawn id from list_pawns, or the pawn's name."),
      hours: z
        .string()
        .min(1)
        .describe("Activities with a range of hours, up to but not including the last, for example Sleep:22-6,Joy:20-22,Work:6-20. The activities are Anything, Work, Joy and Sleep, plus Meditate with Royalty."),
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
      "Stored items by category, silver, wealth breakdown (which drives raid size), the weapons and apparel in storage, and the visible items lying outside storage, such as a new colony's supplies.",
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
      q: z.string().min(1).optional().describe("Text to search for."),
      type: z.string().optional().describe("Limit to a def type, such as ThingDef or RecipeDef."),
      mod: z.string().optional().describe("Limit to a mod by name or package id, which also shows what it adds. Pass at least one of q, type or mod."),
      limit: z.number().int().min(1).max(100).optional(),
    },
  },
  {
    name: "get_def",
    title: "Game data details",
    description:
      "The data of one definition as the running game sees it, with all mod patches applied: stats, costs, requirements and so on. Fields left at their default value are omitted. Find names with search_defs.",
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
    name: "get_new_game_setup",
    title: "New game screens",
    description:
      "On the scenario, storyteller or world settings screen: which one is open, what is selected, and every option, including those from mods. Scenarios, storytellers, difficulties, all difficulty values, and the world settings.",
    path: "/v1/new-game",
    inputSchema: {},
  },
  {
    name: "configure_new_game",
    title: "Set up new game screen",
    description:
      "Only when the player asks. Sets choices on the scenario, storyteller or world settings screen, whichever is open, and only the parameters for that screen apply. The player sees the result and presses Next themselves.",
    path: "/v1/new-game/configure",
    acts: true,
    inputSchema: {
      scenario: z.string().optional().describe("Scenario screen: the scenario's name."),
      storyteller: z.string().optional().describe("Storyteller screen: StorytellerDef defName."),
      difficulty: z.string().optional().describe("Storyteller screen: DifficultyDef defName."),
      difficulty_values: z
        .string()
        .optional()
        .describe("Storyteller screen: individual values, which switch to the custom difficulty, for example allowCaveHives:false,threatScale:0.8."),
      game_mode: z.enum(["reload", "commitment"]).optional().describe("Storyteller screen: reload anytime, or commitment (permadeath). The game requires this choice, along with a difficulty, before Next."),
      seed: z.string().optional().describe("World screen."),
      factions: z.string().optional().describe("World screen: the factions in the world and how many of each, replacing the current choice, for example OutlanderCivil:1,Pirate:2. See faction_options."),
      planet_coverage_pct: z.number().int().optional().describe("World screen: 30, 50 or 100."),
      rainfall: z.string().optional().describe("World screen. See rainfall_options."),
      temperature: z.string().optional().describe("World screen. See temperature_options."),
      population: z.string().optional().describe("World screen. See population_options."),
      landmark_density: z.string().optional().describe("World screen. See landmark_density_options."),
      pollution_pct: z.number().int().min(0).max(100).optional().describe("World screen. Needs Biotech."),
    },
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
      min_stone_types: z.number().int().min(0).max(10).optional(),
      require_stones: z.string().optional().describe("Comma-separated stone ThingDef defNames, such as Granite."),
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
    name: "set_start_options",
    title: "Map size and season",
    description:
      "Only when the player asks. On the site selection screen, sets the map size and the starting season from the screen's advanced settings.",
    path: "/v1/sites/options",
    acts: true,
    inputSchema: {
      map_size: z.number().int().optional().describe("One of 200, 225, 250, 275, 300 or 325."),
      season: z.enum(["Undefined", "Spring", "Summer", "Fall", "Winter"]).optional().describe("Undefined is the game's default."),
    },
  },
  {
    name: "show_site",
    title: "Show site in game",
    description:
      "On the site selection screen, moves the player's camera to a tile and selects it, so they can see it without hunting for it on the globe. It also zooms in if the camera is far away. It changes nothing else, and the player still confirms the choice themselves. Call it for the site you recommend.",
    path: "/v1/sites/show",
    acts: true,
    inputSchema: {
      tile: z.number().int().min(0).describe("Tile id from search_sites."),
    },
  },
  {
    name: "get_ideo_options",
    title: "Ideoligion options",
    description:
      'What an ideoligion can be built from, including anything added by mods. The presets and memes sections describe the big choices. For precepts, use the issues section to see the topics, then the precepts section with an issue. Works on the new game screens and during a game.',
    path: "/v1/ideo/options",
    inputSchema: {
      section: z
        .enum(["presets", "memes", "issues", "precepts", "icons", "colors", "cultures", "styles", "precept_things"])
        .optional()
        .describe("Defaults to presets. Icons and colors are filtered to fit the ideoligion being edited."),
      issue: z.string().optional().describe("Issue defName. Required for the precepts section."),
      precept: z.string().optional().describe("PreceptDef defName, for the precept_things section: what a relic, building or apparel desire can be."),
    },
  },
  {
    name: "view_ideo_icons",
    title: "See ideoligion icons",
    description:
      "Shows ideoligion icons as one image, with the order of their names, so you can choose by how they look. Without names it shows those available to the ideoligion being edited, up to 64.",
    path: "/v1/ideo/icon-sheet",
    returnsImage: true,
    inputSchema: {
      names: z.string().optional().describe("Comma-separated IdeoIconDef defNames to show."),
      columns: z.number().int().min(1).max(20).optional().describe("Defaults to 8."),
    },
  },
  {
    name: "get_ideo_setup",
    title: "Current ideoligion setup",
    description:
      "Which new game ideoligion screen the player is on and the ideoligion being edited, with its memes and precepts, and any problems that would stop the player from continuing.",
    path: "/v1/ideo/setup",
    inputSchema: {},
  },
  {
    name: "configure_ideo",
    title: "Set up ideoligion",
    description:
      "Only call this when the player asks you to set up their ideoligion for them. Works on the new game ideoligion screens. With a preset or memes, it builds a new ideoligion with the game's own generator, replacing the one being edited. The generator picks many precepts at random each time, so read the result and fix anything the player would not want. Without a preset or memes, it edits the current ideoligion and keeps everything else, which is the way to make small changes. Each precept you list replaces the current choice on its issue. The result stays on screen for the player, who presses Next themselves, so tell them what you chose and why, and whether it is ready. When setting one up, also choose a name, adjective, member name, ritual room, icon and color that fit it. Look at icons with view_ideo_icons before choosing one.",
    path: "/v1/ideo/configure",
    acts: true,
    inputSchema: {
      preset: z.string().optional().describe("IdeoPresetDef defName. Pass this or memes."),
      memes: z.string().optional().describe("Comma-separated MemeDef defNames. Pass this or preset."),
      structure: z
        .string()
        .optional()
        .describe("Structure meme defName. If there is none, the game picks one at random."),
      precepts: z.string().optional().describe("Comma-separated PreceptDef defNames to apply."),
      name: z.string().optional().describe("Name for the ideoligion. The game generates one if omitted."),
      adjective: z.string().optional().describe("Adjective, as in 'the Steel Horizon way'."),
      member_name: z.string().optional().describe("What a member is called."),
      worship_room: z.string().optional().describe("Name of the ritual room."),
      remove_precepts: z.string().optional().describe("Comma-separated PreceptDef defNames to remove."),
      icon: z.string().optional().describe("IdeoIconDef defName, from the icons section of get_ideo_options."),
      color: z.string().optional().describe("ColorDef defName, from the colors section of get_ideo_options."),
      culture: z.string().optional().describe("CultureDef defName, from the cultures section of get_ideo_options."),
      add_things: z.string().optional().describe("Relics, buildings and apparel desires, as PreceptDef:ThingDef, for example IdeoRelic:Gun_ChainShotgun. See the precept_things section."),
      remove_things: z.string().optional().describe("ThingDef defNames of relics, buildings or apparel desires to remove."),
      fluid: z.enum(["true"]).optional().describe("Build a fluid ideoligion, which evolves during play but starts with only one normal meme of limited impact. Needs a preset or memes, on the preset screen."),
      styles: z.string().optional().describe("Comma-separated StyleCategoryDef defNames, highest priority first. Replaces the current styles."),
    },
  },
  {
    name: "get_starting_pawns",
    title: "Colonist choices",
    description:
      "Works on the new game colonist screen. All the colonists on offer, in full: which are selected, skills and passions, traits, backstories, disabled work and health, plus any essential work that nobody selected can do.",
    path: "/v1/starting-pawns",
    inputSchema: {},
  },
  {
    name: "find_starting_pawn",
    title: "Find a colonist",
    description:
      "Only when the player asks. Replaces one colonist by generating many candidates with the game's own generator and keeping the one closest to the criteria, without editing any values. Prefer this over edit_starting_pawn, because it stays within what the game normally allows. If the criteria are demanding, it returns the closest candidate and lists what is unmet. Criteria left out are ignored.",
    path: "/v1/starting-pawns/find",
    acts: true,
    inputSchema: {
      index: z.number().int().min(0).describe("Colonist index from get_starting_pawns."),
      gender: z.enum(["Male", "Female"]).optional(),
      min_age: z.number().int().min(18).max(120).optional(),
      max_age: z.number().int().min(18).max(120).optional(),
      min_skills: z.string().optional().describe("For example Shooting:8,Medicine:5."),
      passions: z.string().optional().describe("Minimum passions, for example Shooting:Major,Plants:Minor."),
      require_traits: z.string().optional().describe("Comma-separated TraitDef defNames."),
      forbid_traits: z.string().optional().describe("Comma-separated TraitDef defNames."),
      must_be_capable_of: z
        .string()
        .optional()
        .describe("Comma-separated work tags such as Violent or Intellectual, or 'everything'."),
    },
  },
  {
    name: "swap_starting_pawns",
    title: "Swap colonists",
    description:
      "Only when the player asks. Swaps two colonists in the list, which is how one who was left behind joins the selected group: the first starting_count colonists are the ones who go.",
    path: "/v1/starting-pawns/swap",
    acts: true,
    inputSchema: {
      first: z.number().int().min(0),
      second: z.number().int().min(0),
    },
  },
  {
    name: "edit_starting_pawn",
    title: "Edit a colonist",
    description:
      "Only when the player explicitly asks to change specific attributes, since this goes beyond what the game normally allows. Directly sets the attributes you pass and leaves the others alone. Changing a backstory does not recalculate skills, so set those too if they should change. Find trait and backstory names with search_defs. Implants that are not listed by a surgery recipe are rejected, and they replace the part they go on.",
    path: "/v1/starting-pawns/edit",
    acts: true,
    inputSchema: {
      index: z.number().int().min(0).describe("Colonist index from get_starting_pawns."),
      first_name: z.string().optional(),
      nickname: z.string().optional(),
      last_name: z.string().optional(),
      age: z.number().int().min(18).max(120).optional().describe("Biological age. Pawns under 18 cannot be set."),
      childhood: z.string().optional().describe("BackstoryDef defName."),
      adulthood: z.string().optional().describe("BackstoryDef defName. Needed for ages 20 and up."),
      skills: z.string().optional().describe("Levels from 0 to 20, for example Shooting:12,Construction:8."),
      passions: z.string().optional().describe("None, Minor or Major, for example Shooting:Major,Mining:None."),
      add_traits: z.string().optional().describe("TraitDef defNames, with a level if it has several, for example Tough,Beauty:2."),
      remove_traits: z.string().optional().describe("Comma-separated TraitDef defNames."),
      clear_health: z.enum(["true"]).optional().describe("Removes all visible health conditions."),
      clear_relations: z.enum(["true"]).optional().describe("Removes all their family and other relations, including relatives the player has not met yet."),
      gender: z.enum(["Male", "Female"]).optional().describe("Also picks a new head and hair to suit."),
      hair: z.string().optional().describe("HairDef defName."),
      beard: z.string().optional().describe("BeardDef defName."),
      favorite_color: z.string().optional().describe("ColorDef defName."),
      xenotype: z.string().optional().describe("XenotypeDef defName. Needs Biotech. Replaces the pawn's genes."),
      possessions: z.string().optional().describe("Items they bring, replacing the current list, for example MedicineIndustrial:5."),
      hair_color: z.string().optional().describe("Red, green and blue from 0 to 255, such as 120,80,40."),
      skin_color: z.string().optional().describe("Red, green and blue from 0 to 255, such as 230,190,160."),
      head: z.string().optional().describe("HeadTypeDef defName."),
      body: z.string().optional().describe("BodyTypeDef defName."),
      clear_apparel: z.enum(["true"]).optional().describe("Removes everything they wear first."),
      wear: z.string().optional().describe("Apparel ThingDef defNames, with a material if needed, for example Apparel_Parka:Cloth. Replaces anything it conflicts with."),
      weapon: z.string().optional().describe("Weapon ThingDef defName, with a material if needed. Replaces the current one."),
      add_implants: z.string().optional().describe("Comma-separated HediffDef defNames of surgical implants, such as BionicArm."),
    },
  },
  {
    name: "get_game_settings",
    title: "RimWorld settings",
    description: "The player's RimWorld settings: volumes, graphics, autosave, gameplay preferences and the rest.",
    path: "/v1/game-settings",
    inputSchema: {},
  },
  {
    name: "set_game_settings",
    title: "Change RimWorld settings",
    description: "Only when the player asks. Changes and saves RimWorld settings, using the names from get_game_settings.",
    path: "/v1/game-settings/set",
    acts: true,
    inputSchema: {
      values: z.string().describe("For example runInBackground:true,volumeMusic:0.3."),
    },
  },
  {
    name: "get_installed_mods",
    title: "Installed mods",
    description: "Every installed mod, whether active or not, plus the load order and the game's own warnings about it, such as missing dependencies or a wrong order. list_mods only shows the mods running now.",
    path: "/v1/mods/installed",
    inputSchema: {
      q: z.string().optional().describe("Filter by name or package id."),
    },
  },
  {
    name: "configure_mods",
    title: "Change the mod list",
    description: "Only when the player asks. Enables, disables, orders or sorts mods in the game's mod list. It applies after the game restarts, and it can affect existing saves, so say so. It does not change any mod's files.",
    path: "/v1/mods/configure",
    acts: true,
    inputSchema: {
      enable: z.string().optional().describe("Comma-separated package ids."),
      disable: z.string().optional().describe("Comma-separated package ids."),
      order: z.string().optional().describe("The full active list in load order, as comma-separated package ids."),
      sort: z.enum(["true"]).optional().describe("Sort using the game's own auto-sort."),
    },
  },
  {
    name: "get_mod_settings",
    title: "A mod's settings",
    description: "The simple public settings of a running mod. A mod can have more settings than can be read this way.",
    path: "/v1/mods/settings",
    inputSchema: {
      mod: z.string().describe("Package id."),
    },
  },
  {
    name: "set_mod_settings",
    title: "Change a mod's settings",
    description: "Only when the player asks. Changes and saves a running mod's settings, using the names from get_mod_settings.",
    path: "/v1/mods/settings/set",
    acts: true,
    inputSchema: {
      mod: z.string().describe("Package id."),
      values: z.string().describe("For example someOption:true,someAmount:0.5."),
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
