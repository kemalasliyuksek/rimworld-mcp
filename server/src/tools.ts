import { z } from "zod";

export interface ToolDefinition {
  name: string;
  title: string;
  description: string;
  /** Bridge endpoint. The tool's arguments become its query string. */
  path: string;
  /** Set for a tool that acts on the player's request. Everything else reads with GET. */
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

const pawnEditFields = {
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
  needs: z.string().optional().describe("Needs set to a percentage, using the defs in get_pawn, for example Food:40,Joy:90,Mood:75."),
  restore_needs: z.enum(["true"]).optional().describe("Fills food, rest, joy, mood and every other need."),
  add_thoughts: z.string().optional().describe("ThoughtDef defNames of remembered thoughts, with a stage if it has several, for example AteFineMeal or ObservedLayingCorpse:1."),
  remove_thoughts: z.string().optional().describe("ThoughtDef defNames to remove, using the defs in get_pawn. Thoughts about their current situation come back until that changes."),
  add_conditions: z.string().optional().describe("HediffDef defNames, with a severity if wanted, for example Flu or Flu:0.5. Find names with search_defs."),
  remove_conditions: z.string().optional().describe("HediffDef defNames to remove, using the defs in get_pawn."),
  add_abilities: z.string().optional().describe("AbilityDef defNames to give, including psycasts, such as Stun. Find names with search_defs."),
  remove_abilities: z.string().optional().describe("AbilityDef defNames to take away."),
  mental_state: z.string().optional().describe("MentalStateDef defName to start now, such as Wander_Sad."),
  clear_bad_memories: z.enum(["true"]).optional().describe("Removes memories that lower their mood. Thoughts about their current situation, such as having no table, come back until that is fixed."),
  end_mental_state: z.enum(["true"]).optional().describe("Ends a mental break they are having now."),
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
};

const { possessions: _possessions, ...duringPlayEditFields } = pawnEditFields;

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
    name: "set_drafted",
    title: "Draft or undraft",
    description:
      "Only when the player asks. Drafts or releases colonists and sets whether they fire at will. Failures are reported per colonist.",
    path: "/v1/orders/draft",
    acts: true,
    inputSchema: {
      pawns: z.string().min(1).describe("Comma-separated pawn ids or names."),
      drafted: z.enum(["true", "false"]).optional(),
      fire_at_will: z.enum(["true", "false"]).optional().describe("False holds fire until they are ordered to attack."),
    },
  },
  {
    name: "move_pawns",
    title: "Move colonists",
    description:
      "Only when the player asks. Drafts colonists and sends them to a cell, like the game's go-here order. A group spreads out around it, and the result says where each one is going. To place each one exactly, for example behind cover, call this once per colonist. Read view_map first to choose cells.",
    path: "/v1/orders/move",
    acts: true,
    inputSchema: {
      pawns: z.string().min(1).describe("Comma-separated pawn ids or names."),
      x: z.number().int().min(0),
      z: z.number().int().min(0),
    },
  },
  {
    name: "attack_target",
    title: "Attack",
    description:
      "Only when the player asks. Drafts colonists and orders them to attack a target, with their weapon or in melee. Ranged attackers must already be in range with a clear shot, and a failure says why. Get target ids from list_pawns or inspect_cell.",
    path: "/v1/orders/attack",
    acts: true,
    inputSchema: {
      pawns: z.string().min(1).describe("Comma-separated pawn ids or names."),
      target: z.string().min(1).describe("Thing id or pawn name."),
      mode: z.enum(["any", "melee", "ranged"]).optional().describe("Defaults to any, which uses the pawn's weapon."),
    },
  },
  {
    name: "list_pawn_orders",
    title: "Orders on a cell",
    description:
      "What the game's right-click menu offers a colonist on a cell, including what mods add: attack, tend, build, haul, rescue, prioritize and more. Being drafted changes what is offered. Works for colonists on the map the player is looking at.",
    path: "/v1/orders",
    inputSchema: {
      pawn: z.string().min(1).describe("Pawn id or name."),
      x: z.number().int().min(0),
      z: z.number().int().min(0),
    },
  },
  {
    name: "give_pawn_order",
    title: "Give an order",
    description:
      "Only when the player asks. Chooses an order from list_pawn_orders for each colonist, with no use of the mouse. Failures are reported per colonist. If done is false, the order needed something on screen, such as a target or a further menu, so it did not go through and the player has to give it, and needs_player says why.",
    path: "/v1/orders/give",
    acts: true,
    inputSchema: {
      confirm: z
        .enum(["true"])
        .optional()
        .describe("Answers yes if the game asks to confirm. Without it, needs_confirmation gives the question. Tell the player the question, and pass this only once they agree."),
      pawns: z.string().min(1).describe("Comma-separated pawn ids or names."),
      x: z.number().int().min(0),
      z: z.number().int().min(0),
      order: z.string().min(1).describe("The order's text, exactly as list_pawn_orders gives it. For several colonists, check that each one is offered it, since the text can name the colonist."),
    },
  },
  {
    name: "list_gizmos",
    title: "Buttons of a thing",
    description:
      "The buttons the game shows when a pawn or building is selected, including those from mods. usable false means the button only works by hand. Get thing ids from inspect_cell or list_pawns.",
    path: "/v1/gizmos",
    inputSchema: {
      thing: z.string().min(1).describe("Thing id, or a pawn's name."),
    },
  },
  {
    name: "use_gizmo",
    title: "Press a button",
    description:
      "Only when the player asks. Presses a button from list_gizmos, with no use of the mouse. Buttons of kind target also need a target: x and z, or another thing. If done is false, it needed something on screen, so it did not fully go through and the player has to do it, and needs_player says why.",
    path: "/v1/gizmos/use",
    acts: true,
    inputSchema: {
      confirm: z
        .enum(["true"])
        .optional()
        .describe("Answers yes if the game asks to confirm. Without it, needs_confirmation gives the question. Tell the player the question, and pass this only once they agree."),
      thing: z.string().min(1).describe("Thing id, or a pawn's name."),
      button: z.string().min(1).describe("The button's text, exactly as list_gizmos gives it."),
      x: z.number().int().min(0).optional(),
      z: z.number().int().min(0).optional(),
      target: z.string().min(1).optional().describe("Thing id of the target, instead of x and z."),
    },
  },
  {
    name: "list_designators",
    title: "Architect menu",
    description:
      "What the game's Architect menu offers right now, including what mods add and only what research allows: buildings, floors, zones, and orders such as mining, cutting, hunting and cancelling. Without a category it lists the categories. Each entry's id goes to use_designator.",
    path: "/v1/designators",
    inputSchema: {
      category: z.string().optional().describe("Category defName, such as Orders, Structure or Zone."),
    },
  },
  {
    name: "use_designator",
    title: "Use the Architect menu",
    description:
      "Only when the player asks. Applies one designator to cells on the map the player is looking at: places blueprints that builders will construct, marks rock to mine, marks plants to cut, makes zones, and so on. The game's own checks apply, and rejected cells are listed with the reason. Read view_map first to choose cells. If the player wants to see a layout first, draw it with draw_plan and wait for their approval.",
    path: "/v1/designators/use",
    acts: true,
    inputSchema: {
      confirm: z
        .enum(["true"])
        .optional()
        .describe("Answers yes if the game asks to confirm. Without it, needs_confirmation gives the question. Tell the player the question, and pass this only once they agree."),
      designator: z.string().min(1).describe("An id from list_designators."),
      cells: z
        .string()
        .min(1)
        .describe("Parts separated by semicolons: a cell (10,20), a filled rectangle (10,20-15,25), or the edge of one (outline:10,20-15,25), which suits walls. For a building larger than one cell, each cell is where its center goes."),
      stuff: z.string().optional().describe("Material for a building made from one, as a ThingDef defName such as WoodLog, Steel or BlocksGranite. Defaults to the game's choice."),
      rotation: z.enum(["North", "East", "South", "West"]).optional().describe("For a building. Defaults to North."),
      instant: z.enum(["true"]).optional().describe("A cheat, only when the player asks for it. Finishes it at once and free of cost, as the game's god mode does."),
    },
  },
  {
    name: "list_plans",
    title: "Plans",
    description: "The plans drawn on the map, with their ids and areas, and the colors available. view_map marks planned cells with *.",
    path: "/v1/plans",
    inputSchema: { map: mapParam },
  },
  {
    name: "draw_plan",
    title: "Draw a plan",
    description:
      "Only when the player asks. Draws planning marks on the map for the player to look at, using the game's own plan feature. They cost nothing and nobody acts on them, which makes them the way to propose a layout: draw it, ask the player, and only then build it with use_designator. Use a different color or label for each part so that they are easy to tell apart.",
    path: "/v1/plans/draw",
    acts: true,
    inputSchema: {
      cells: z.string().min(1).describe("Parts separated by semicolons: a cell (10,20), a filled rectangle (10,20-15,25), or the edge of one (outline:10,20-15,25)."),
      color: z.string().optional().describe("A color from list_plans."),
      label: z.string().optional(),
      map: mapParam,
    },
  },
  {
    name: "remove_plan",
    title: "Remove a plan",
    description: "Only when the player asks, or to clean up a proposal they turned down or that has been built.",
    path: "/v1/plans/remove",
    acts: true,
    inputSchema: {
      plan: z.string().min(1).describe("A plan id from list_plans, or all."),
      map: mapParam,
    },
  },
  {
    name: "set_game_speed",
    title: "Game speed",
    description:
      "Only when the player asks, or while playing for them at their request. Pauses the game or sets its speed. While playing for the player, pause before anything risky and to think, since the game does not wait for you.",
    path: "/v1/colony/speed",
    acts: true,
    inputSchema: {
      speed: z.enum(["paused", "normal", "fast", "superfast"]),
    },
  },
  {
    name: "set_research",
    title: "Set research",
    description: "Only when the player asks. Chooses the research project, using a def from get_research, or none to stop.",
    path: "/v1/colony/research",
    acts: true,
    inputSchema: {
      project: z.string().min(1).describe("ResearchProjectDef defName, or none."),
    },
  },
  {
    name: "list_recipes",
    title: "Recipes of a bench",
    description: "What a work bench can make right now, including what mods add. Get bench ids from get_base.",
    path: "/v1/colony/recipes",
    inputSchema: {
      thing: z.string().min(1).describe("Thing id of the bench."),
    },
  },
  {
    name: "add_bill",
    title: "Add a bill",
    description: "Only when the player asks. Adds a bill to a work bench. Its bills are listed by get_base.",
    path: "/v1/colony/bills/add",
    acts: true,
    inputSchema: {
      thing: z.string().min(1).describe("Thing id of the bench."),
      recipe: z.string().min(1).describe("Recipe from list_recipes."),
      repeat: z.enum(["count", "forever", "until"]).optional().describe("Make a number of times, forever, or until there are this many. Defaults to count."),
      count: z.number().int().min(1).max(100000).optional().describe("Defaults to 1."),
    },
  },
  {
    name: "remove_bill",
    title: "Remove a bill",
    description: "Only when the player asks. Removes a bill from a work bench, by its position in the list from get_base, starting at 0.",
    path: "/v1/colony/bills/remove",
    acts: true,
    inputSchema: {
      thing: z.string().min(1).describe("Thing id of the bench."),
      index: z.number().int().min(0),
    },
  },
  {
    name: "set_crop",
    title: "Set a crop",
    description: "Only when the player asks. Sets what a growing zone sows. An invalid plant lists the valid ones.",
    path: "/v1/colony/crop",
    acts: true,
    inputSchema: {
      x: z.number().int().min(0).describe("Any cell in the zone."),
      z: z.number().int().min(0),
      plant: z.string().min(1).describe("Plant ThingDef defName, such as Plant_Rice."),
    },
  },
  {
    name: "answer_dialog",
    title: "Answer a dialog",
    description:
      "Only when the player asks. Chooses an option in the dialog on screen, as listed by get_player_focus. Many of these decide an event for good, so tell the player what you are choosing and why.",
    path: "/v1/dialogs/answer",
    acts: true,
    inputSchema: {
      choice: z.string().min(1).describe("The option's text, exactly as given."),
    },
  },
  {
    name: "list_letters",
    title: "Letters",
    description: "The letters on the right of the screen, with their text and choices.",
    path: "/v1/letters",
    inputSchema: {},
  },
  {
    name: "answer_letter",
    title: "Answer a letter",
    description:
      "Only when the player asks. Chooses an option in a letter from list_letters, such as accepting a quest. If dialog_still_open is true, it led to more choices, which answer_dialog can answer.",
    path: "/v1/letters/answer",
    acts: true,
    inputSchema: {
      letter: z.string().min(1).describe("Letter id."),
      choice: z.string().min(1).describe("The choice's text, exactly as given."),
    },
  },
  {
    name: "list_policies",
    title: "Policies",
    description: "The names set_pawn_policies takes: allowed areas, outfits, drug and food policies, hostility responses and medical care.",
    path: "/v1/policies",
    inputSchema: { map: mapParam },
  },
  {
    name: "set_pawn_policies",
    title: "Set a pawn's policies",
    description: "Only when the player asks. Sets a pawn's allowed area, outfit, drug and food policy, hostility response and medical care, as on the game's Assign tab. Names come from list_policies.",
    path: "/v1/pawn/policies",
    acts: true,
    inputSchema: {
      pawn: z.string().min(1).describe("Pawn id from list_pawns, or the pawn's name."),
      area: z.string().optional().describe("Area name, or none for unrestricted."),
      outfit: z.string().optional(),
      drug_policy: z.string().optional(),
      food_policy: z.string().optional(),
      hostility_response: z.string().optional(),
      medical_care: z.string().optional(),
    },
  },
  {
    name: "set_stockpile",
    title: "Set a stockpile",
    description: "Only when the player asks. Changes a stockpile's priority and what it accepts. allow and disallow run after preset.",
    path: "/v1/colony/stockpile",
    acts: true,
    inputSchema: {
      x: z.number().int().min(0).describe("Any cell in the stockpile."),
      z: z.number().int().min(0),
      priority: z.enum(["Low", "Normal", "Preferred", "Important", "Critical"]).optional(),
      preset: z.enum(["all", "none"]).optional().describe("Start by accepting everything or nothing."),
      allow: z.string().optional().describe("Comma-separated ThingDef or ThingCategoryDef defNames, such as Steel,FoodMeals."),
      disallow: z.string().optional().describe("Comma-separated ThingDef or ThingCategoryDef defNames."),
    },
  },
  {
    name: "list_traders",
    title: "Traders",
    description: "Traders on the map and orbital ships in range, with the ids the trade tools take.",
    path: "/v1/trade/traders",
    inputSchema: { map: mapParam },
  },
  {
    name: "get_trader_goods",
    title: "Trader's goods",
    description: "What can be bought from and sold to a trader, at the prices a given negotiator gets, and how much silver each side has. Prices differ by negotiator, so compare before trading.",
    path: "/v1/trade/goods",
    inputSchema: {
      trader: z.string().min(1).describe("An id from list_traders."),
      negotiator: z.string().min(1).describe("The colonist doing the talking."),
    },
  },
  {
    name: "trade",
    title: "Trade",
    description: "Only when the player asks. Makes a deal with a trader at once, with silver covering the difference. Nobody walks over, unlike in the normal game, and the negotiator must be on the map. An orbital ship normally needs a comms console, and this does not check for one, so make sure the player has one unless they asked to cheat.",
    path: "/v1/trade/deal",
    acts: true,
    inputSchema: {
      trader: z.string().min(1).describe("An id from list_traders."),
      negotiator: z.string().min(1).describe("The colonist doing the talking."),
      deal: z.string().min(1).describe("ThingDef defNames with a count, positive to buy and negative to sell, for example Steel:50,WoodLog:-20."),
    },
  },
  {
    name: "form_caravan",
    title: "Form a caravan",
    description: "Only when the player asks, since it sends colonists away from the colony. The colonists gather what is listed and leave the map, and the caravan appears on the world map. This does not check the weight or food as the game's window does, so choose with care. They can be stopped with the cancel button in list_gizmos.",
    path: "/v1/caravans/form",
    acts: true,
    inputSchema: {
      pawns: z.string().min(1).describe("Comma-separated pawn ids or names."),
      items: z.string().optional().describe("ThingDef defNames with a count, for example MealSurvivalPack:20,Silver:300."),
      tile: z.number().int().min(0).optional().describe("World tile to head for after leaving."),
    },
  },
  {
    name: "send_caravan",
    title: "Send a caravan",
    description: "Only when the player asks. Sends a caravan to a world tile. Sent to a tile with one of the player's maps, such as home, it walks back in. Caravans are listed by get_world, and search_sites and get_site find tiles.",
    path: "/v1/caravans/send",
    acts: true,
    inputSchema: {
      caravan: z.string().min(1).describe("The caravan's label from get_world."),
      tile: z.number().int().min(0),
    },
  },
  {
    name: "list_abilities",
    title: "Abilities",
    description: "A pawn's psycasts and other abilities, from the game, its DLCs and mods, with what is stopping them right now, and their psyfocus and neural heat.",
    path: "/v1/abilities",
    inputSchema: {
      pawn: z.string().min(1).describe("Pawn id or name."),
    },
  },
  {
    name: "use_ability",
    title: "Use an ability",
    description:
      "Only when the player asks. Has a pawn use an ability from list_abilities, as they would with its button. Abilities that need a target take a thing, or x and z, or tile for those that target the world map. If done is false, needs_confirmation gives the game's question, or needs_player says why it could not go through.",
    path: "/v1/abilities/use",
    acts: true,
    inputSchema: {
      pawn: z.string().min(1).describe("Pawn id or name."),
      ability: z.string().min(1).describe("AbilityDef defName."),
      target: z.string().optional().describe("Thing id or pawn name."),
      x: z.number().int().min(0).optional(),
      z: z.number().int().min(0).optional(),
      tile: z.number().int().min(0).optional(),
      confirm: z.enum(["true"]).optional().describe("Answers yes if the game asks to confirm. Pass this only once the player agrees."),
    },
  },
  {
    name: "edit_bill",
    title: "Edit a bill",
    description: "Only when the player asks. Changes a bill on a work bench, by its position in get_base, starting at 0. Only what you pass changes.",
    path: "/v1/colony/bills/edit",
    acts: true,
    inputSchema: {
      thing: z.string().min(1).describe("Thing id of the bench."),
      index: z.number().int().min(0),
      suspended: z.enum(["true", "false"]).optional(),
      repeat: z.enum(["count", "forever", "until"]).optional(),
      count: z.number().int().min(1).max(100000).optional(),
      pause_when_satisfied: z.enum(["true", "false"]).optional().describe("For until: stop at the target and wait."),
      unpause_at: z.number().int().min(0).max(100000).optional().describe("For until with a pause: start again at this many."),
      skill_min: z.number().int().min(0).max(20).optional(),
      skill_max: z.number().int().min(0).max(20).optional(),
      radius: z.number().min(3).max(999).optional().describe("How far to look for ingredients."),
      worker: z.string().optional().describe("Who may do it, by pawn id or name, or any."),
      store: z.string().optional().describe("best, floor, or a stockpile's name."),
      min_quality: z.string().optional().describe("Counted for until, such as Good."),
      max_quality: z.string().optional(),
      allow_ingredients: z.string().optional().describe("Comma-separated ThingDef or ThingCategoryDef defNames."),
      disallow_ingredients: z.string().optional().describe("Comma-separated ThingDef or ThingCategoryDef defNames."),
      move: z.number().int().optional().describe("Moves it up with a negative number, or down with a positive one."),
    },
  },
  {
    name: "create_policy",
    title: "Create a policy",
    description: "Only when the player asks. Creates an outfit, drug or food policy, optionally as a copy of another. Assign it with set_pawn_policies.",
    path: "/v1/policies/create",
    acts: true,
    inputSchema: {
      kind: z.enum(["outfit", "drug", "food"]),
      name: z.string().min(1),
      copy_from: z.string().optional().describe("Name of a policy of the same kind."),
    },
  },
  {
    name: "edit_policy",
    title: "Edit a policy",
    description: "Only when the player asks. Changes an outfit or food policy's filter, or one drug's settings in a drug policy, and can rename it. Only what you pass changes. allow and disallow run after preset.",
    path: "/v1/policies/edit",
    acts: true,
    inputSchema: {
      kind: z.enum(["outfit", "drug", "food"]),
      policy: z.string().min(1).describe("The policy's name."),
      name: z.string().optional().describe("A new name."),
      preset: z.enum(["all", "none"]).optional(),
      allow: z.string().optional().describe("Comma-separated ThingDef or ThingCategoryDef defNames."),
      disallow: z.string().optional().describe("Comma-separated ThingDef or ThingCategoryDef defNames."),
      drug: z.string().optional().describe("Drug ThingDef defName, for the settings below."),
      for_addiction: z.enum(["true", "false"]).optional(),
      for_joy: z.enum(["true", "false"]).optional(),
      scheduled: z.enum(["true", "false"]).optional(),
      days_frequency: z.number().min(0.01).max(1000).optional(),
      only_if_mood_below_pct: z.number().min(0).max(100).optional(),
      only_if_joy_below_pct: z.number().min(0).max(100).optional(),
      take_to_inventory: z.number().int().min(0).max(1000).optional(),
    },
  },
  {
    name: "delete_policy",
    title: "Delete a policy",
    description: "Only when the player asks. Deletes a policy. The game refuses if it is in use or is the last one.",
    path: "/v1/policies/delete",
    acts: true,
    inputSchema: {
      kind: z.enum(["outfit", "drug", "food"]),
      policy: z.string().min(1).describe("The policy's name."),
    },
  },
  {
    name: "set_area",
    title: "Set an allowed area",
    description: "Only when the player asks. Creates an allowed area if none has that name, and changes its cells or name, or deletes it. Assign it with set_pawn_policies.",
    path: "/v1/areas/set",
    acts: true,
    inputSchema: {
      area: z.string().min(1).describe("The area's name."),
      add: z.string().optional().describe("Parts separated by semicolons: a cell (10,20), a filled rectangle (10,20-15,25), or the edge of one (outline:10,20-15,25)."),
      remove: z.string().optional().describe("Cells to take out, in the same form."),
      rename: z.string().optional(),
      delete: z.enum(["true"]).optional(),
      map: mapParam,
    },
  },
  {
    name: "spawn_thing",
    title: "Spawn a thing",
    description:
      "A cheat, only when the player asks for it. Creates items, buildings or plants at a cell, out of nothing. Items are placed around the cell. A building replaces what is there, so check view_map first.",
    path: "/v1/cheats/spawn-thing",
    acts: true,
    inputSchema: {
      def: z.string().min(1).describe("ThingDef defName, such as WoodLog. Find names with search_defs."),
      x: z.number().int().min(0),
      z: z.number().int().min(0),
      count: z.number().int().min(1).max(100000).optional().describe("Defaults to 1."),
      stuff: z.string().optional().describe("Material, if it is made from one."),
      quality: z.string().optional().describe("Awful, Poor, Normal, Good, Excellent, Masterwork or Legendary."),
      map: mapParam,
    },
  },
  {
    name: "spawn_pawn",
    title: "Spawn a pawn",
    description:
      "A cheat, only when the player asks for it. Creates pawns near a cell: people, animals or enemies, in any faction.",
    path: "/v1/cheats/spawn-pawn",
    acts: true,
    inputSchema: {
      kind: z.string().min(1).describe("PawnKindDef defName, such as Colonist or Muffalo. Find names with search_defs."),
      x: z.number().int().min(0),
      z: z.number().int().min(0),
      faction: z.string().optional().describe("player, none, or a faction's name or defName. Defaults to none."),
      count: z.number().int().min(1).max(100).optional().describe("Defaults to 1."),
      map: mapParam,
    },
  },
  {
    name: "trigger_incident",
    title: "Trigger an event",
    description:
      "A cheat, only when the player asks for it. Fires an event right now: a raid, a trader, an infestation, a cold snap and so on, including those from mods. fired false means the game could not fire it now.",
    path: "/v1/cheats/incident",
    acts: true,
    inputSchema: {
      incident: z.string().min(1).describe("IncidentDef defName. Find names with search_defs and type IncidentDef."),
      points: z.number().int().min(1).optional().describe("Strength, such as the size of a raid. Defaults to the game's choice."),
      map: mapParam,
    },
  },
  {
    name: "complete_research",
    title: "Complete research",
    description: "A cheat, only when the player asks for it. Finishes a research project, or all of them.",
    path: "/v1/cheats/research",
    acts: true,
    inputSchema: {
      project: z.string().min(1).describe("ResearchProjectDef defName, or all."),
    },
  },
  {
    name: "edit_pawn",
    title: "Edit a pawn",
    description:
      "A cheat, only when the player asks for it. Directly sets a person's attributes during play and leaves the others alone: name, age, skills, passions, traits, backstory, looks, gear and health. Read the result with get_pawn.",
    path: "/v1/cheats/edit-pawn",
    acts: true,
    inputSchema: {
      pawn: z.string().min(1).describe("Pawn id from list_pawns, or the pawn's name."),
      ...duringPlayEditFields,
    },
  },
  {
    name: "reveal_map",
    title: "Reveal the map",
    description: "A cheat that shows hidden information, only when the player asks for it. Removes all fog from a map for good.",
    path: "/v1/cheats/reveal-map",
    acts: true,
    inputSchema: { map: mapParam },
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
      include_hidden: z
        .enum(["true"])
        .optional()
        .describe("Also shows what is hidden from the player. Use only when the player asks what is hidden there, and never mention it otherwise."),
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
      include_hidden: z
        .enum(["true"])
        .optional()
        .describe("Also shows what is hidden from the player. Use only when the player asks what is hidden there, and never mention it otherwise."),
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
      ...pawnEditFields,
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
