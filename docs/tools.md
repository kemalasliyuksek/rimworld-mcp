# Tools

All tools are read-only, except for those marked as acting on request: `configure_new_game`, `show_site`,
`set_start_options`, `configure_ideo`, `find_starting_pawn`, `swap_starting_pawns`, `edit_starting_pawn`,
`set_game_settings`, `configure_mods`, `set_mod_settings`, `set_work_priorities`, `set_schedule`,
`set_drafted`, `move_pawns`, `give_pawn_order`, `use_gizmo`, `use_designator`, `draw_plan`, `remove_plan`, `set_game_speed`, `set_research`, `add_bill`, `remove_bill`, `set_crop`, `answer_dialog`, `answer_letter`, `set_pawn_policies`, `set_stockpile`, `trade`, `form_caravan`, `send_caravan`, and the cheats below. The mod's settings can turn acting tools, cheats and hidden information off. Those marked with `map` accept an optional map id and default to the map the player
is looking at.

## Situation

| Tool | Purpose |
| --- | --- |
| `get_colony_overview` (`map`) | Date, weather, mood, food, threats, research, alerts, unread letters. The starting point. |
| `get_player_focus` | Selection, camera, open windows, and the text and choices of event dialogs. |
| `get_alerts` | Active alerts with the game's full explanations. |
| `get_recent_events` (`limit`) | History of letters and messages, newest first. |

## Pawns

| Tool | Purpose |
| --- | --- |
| `list_pawns` (`group`, `limit`, `map`) | Compact list. Groups: `colonists`, `prisoners`, `slaves`, `animals`, `mechanoids`, `hostiles`, `visitors`, `wildlife`. |
| `get_pawn` (`id`) | Full details for one pawn, by id or name. |
| `set_work_priorities` (`pawn`, `priorities`, `manual_priorities`) | Changes settings on the game's Work tab. Only when the player asks. |
| `set_schedule` (`pawn`, `hours`) | Changes hours on the game's Schedule tab. Only when the player asks. |
| `set_drafted` (`pawns`, `drafted`, `fire_at_will`) | Drafts or releases colonists. Only when the player asks. |
| `move_pawns` (`pawns`, `x`, `z`) | Drafts colonists and sends them to a cell. Only when the player asks. |
| `list_pawn_orders` (`pawn`, `x`, `z`) | What the right-click menu offers a colonist on a cell, including what mods add. |
| `give_pawn_order` (`pawns`, `x`, `z`, `order`) | Chooses one of those orders. Only when the player asks. |
| `list_gizmos` (`thing`) | The buttons of a selected pawn or building, including those from mods. |
| `use_gizmo` (`thing`, `button`, `x`, `z`, `target`) | Presses one of those buttons. Only when the player asks. |

## Colony

| Tool | Purpose |
| --- | --- |
| `get_threats` (`map`) | Visible enemies, colony fighters and turrets. |
| `get_resources` (`map`) | Stock by category, wealth, stored weapons and apparel, and visible items outside storage. |
| `get_base` (`sections`, `map`) | Rooms, power, zones, production and beds. |
| `get_research` (`limit`) | Current project and available projects. |
| `get_world` | Factions, quests, caravans, maps and ideoligion. |

## Building

| Tool | What it does |
| --- | --- |
| `list_designators` (`category`) | What the Architect menu offers, including what mods add. |
| `use_designator` (`designator`, `cells`, `stuff`, `rotation`) | Applies one to cells: build, mine, cut, make zones and more. Only when the player asks. |
| `list_plans` (`map`) | Plans on the map and the available colors. |
| `draw_plan` (`cells`, `color`, `label`, `map`) | Draws planning marks to propose a layout. Only when the player asks. |
| `remove_plan` (`plan`, `map`) | Removes one plan, or `all`. |

Cells are parts separated by semicolons: a cell (`10,20`), a filled rectangle (`10,20-15,25`), or the edge of one (`outline:10,20-15,25`).

## Running the colony

| Tool | What it does |
| --- | --- |
| `set_game_speed` (`speed`) | Pauses or sets the speed. Only when the player asks. |
| `set_research` (`project`) | Chooses the research. Only when the player asks. |
| `list_recipes` (`thing`) | What a work bench can make. |
| `add_bill` (`thing`, `recipe`, `repeat`, `count`) | Adds a bill. Only when the player asks. |
| `remove_bill` (`thing`, `index`) | Removes a bill. Only when the player asks. |
| `set_crop` (`x`, `z`, `plant`) | Sets what a growing zone sows. Only when the player asks. |
| `answer_dialog` (`choice`) | Chooses an option in the dialog on screen. Only when the player asks. |
| `list_letters` | Letters with their text and choices. |
| `answer_letter` (`letter`, `choice`) | Chooses an option in a letter. Only when the player asks. |
| `list_policies` (`map`) | Names of areas, outfits, drug and food policies and more. |
| `set_pawn_policies` (`pawn`, `area`, `outfit`, `drug_policy`, `food_policy`, `hostility_response`, `medical_care`) | Sets what is on the Assign tab. Only when the player asks. |
| `set_stockpile` (`x`, `z`, `priority`, `preset`, `allow`, `disallow`) | Changes a stockpile's priority and filter. Only when the player asks. |
| `list_traders` (`map`) | Traders on the map and ships in range. |
| `get_trader_goods` (`trader`, `negotiator`) | What can be bought and sold, and at what price. |
| `trade` (`trader`, `negotiator`, `deal`) | Makes a deal at once. Only when the player asks. |
| `form_caravan` (`pawns`, `items`, `tile`) | Has colonists pack and leave. Only when the player asks. |
| `send_caravan` (`caravan`, `tile`) | Sends a caravan to a world tile. Only when the player asks. |

## Cheats

Only when the player asks for them, and only while the Cheats switch in the mod's settings is on.

| Tool | What it does |
| --- | --- |
| `spawn_thing` (`def`, `x`, `z`, `count`, `stuff`, `quality`, `map`) | Creates items, buildings or plants. |
| `spawn_pawn` (`kind`, `x`, `z`, `faction`, `count`, `map`) | Creates people, animals or enemies. |
| `trigger_incident` (`incident`, `points`, `map`) | Fires an event now. |
| `complete_research` (`project`) | Finishes a project, or `all`. |
| `edit_pawn` (`pawn` and the attributes of `edit_starting_pawn`) | Edits a person during play. |
| `reveal_map` (`map`) | Removes all fog. Also needs the Hidden information switch. |

`use_designator` also takes `instant`, which finishes the work at once and free of cost, and `view_map` and `inspect_cell` take `include_hidden`, only for when the player asks what is hidden.

## Map

| Tool | Purpose |
| --- | --- |
| `view_map` (`x`, `z`, `width`, `height`, `map`) | Text rendering of an area, up to 120 by 120 cells, with a legend. |
| `inspect_cell` (`x`, `z`, `map`) | Details of one cell. |

## Game knowledge

| Tool | Purpose |
| --- | --- |
| `search_defs` (`q`, `type`, `mod`, `limit`) | Search definitions from the game, DLCs and active mods, or list what a mod adds. |
| `get_def` (`def`, `type`) | The data of one definition, with mod patches applied and default values left out. |
| `list_mods` | Active mods in load order. |

## New game screens

| Tool | Purpose |
| --- | --- |
| `get_new_game_setup` | The open scenario, storyteller or world settings screen, its choices and every option. |
| `configure_new_game` (`scenario`, `storyteller`, `difficulty`, `difficulty_values`, `game_mode`, `seed`, `factions`, `planet_coverage_pct`, `rainfall`, `temperature`, `population`, `landmark_density`, `pollution_pct`) | Sets choices on the open screen. Only when the player asks. |

## Choosing a site

These work before a game starts, once a world has been generated.

| Tool | Purpose |
| --- | --- |
| `get_world_settings` | Seed, world settings, the selected tile, and how many tiles have each biome, hilliness and feature. |
| `search_sites` (`biomes`, `hilliness`, `min_growing_days`, `min_coldest_c`, `max_hottest_c`, `min_rainfall`, `max_rainfall`, `max_diseases_per_year`, `max_pollution_pct`, `min_stone_types`, `require_stones`, `caves`, `coast`, `lake`, `river`, `road`, `exclude_features`, `require_features`, `shape`, `min_pocket_score`, `min_rock_pct`, `max_rock_pct`, `limit`, `offset`) | Finds tiles that match, optionally ranked by predicted mountain shape. Every condition is optional. |
| `get_site` (`tile`) | Full details of a tile, including movement difficulty, nearby pollution, grazing, time zone, monthly temperatures and nearby settlements, with a text preview of its predicted mountains. |
| `set_start_options` (`map_size`, `season`) | Sets the map size and starting season. Only when the player asks. |
| `show_site` (`tile`) | Moves the camera to a tile, zooms in and selects it. |

Searches follow what the player asks for. For example, a sheltered, mild, all-year site with no hives:

```json
{
  "hilliness": "SmallHills,LargeHills",
  "min_growing_days": 60,
  "min_coldest_c": -5,
  "max_hottest_c": 38,
  "caves": "exclude",
  "lake": "exclude",
  "coast": "exclude",
  "shape": "crescent"
}
```

Or a flat site next to water, with caves to explore:

```json
{
  "hilliness": "Flat,SmallHills",
  "caves": "require",
  "river": "require"
}
```

## Ideoligion

| Tool | Purpose |
| --- | --- |
| `get_ideo_options` (`section`, `issue`) | Presets, memes, precept topics, the precepts of a topic, icons, colors, cultures, styles and what a relic, building or apparel desire can be, including those from mods. |
| `view_ideo_icons` (`names`, `columns`) | Shows icons as an image, so they can be chosen by how they look. |
| `get_ideo_setup` | The current screen and the ideoligion being edited, with anything that blocks continuing. |
| `configure_ideo` (`preset`, `memes`, `structure`, `precepts`, `remove_precepts`, `name`, `adjective`, `member_name`, `worship_room`, `icon`, `color`, `culture`, `styles`, `add_things`, `remove_things`, `fluid`) | With a preset or memes, builds a new ideoligion with the game's generator. Without them, edits the current one and keeps everything else. Only when the player asks. The player presses Next. |

## Colonists

| Tool | Purpose |
| --- | --- |
| `get_starting_pawns` | Every colonist on offer, in full, and essential work nobody selected can do. Relations follow the game's social tab, and relatives the player has not met appear only as a count. |
| `find_starting_pawn` (`index`, `gender`, `min_age`, `max_age`, `min_skills`, `passions`, `require_traits`, `forbid_traits`, `must_be_capable_of`) | Generates candidates with the game's generator and keeps the closest match. Only when the player asks. |
| `swap_starting_pawns` (`first`, `second`) | Swaps two colonists, which is how one left behind joins the group. Only when the player asks. |
| `edit_starting_pawn` (`index`, `first_name`, `nickname`, `last_name`, `age`, `childhood`, `adulthood`, `skills`, `passions`, `add_traits`, `remove_traits`, `clear_health`, `clear_relations`, `gender`, `hair`, `beard`, `favorite_color`, `xenotype`, `possessions`, `hair_color`, `skin_color`, `head`, `body`, `clear_apparel`, `wear`, `weapon`, `add_implants`) | Sets attributes directly. Only when the player explicitly asks. |

## Settings and mods

| Tool | Purpose |
| --- | --- |
| `get_game_settings` | The player's RimWorld settings. |
| `set_game_settings` (`values`) | Changes and saves them. Only when the player asks. |
| `get_installed_mods` (`q`) | Every installed mod, the load order and the game's warnings. |
| `configure_mods` (`enable`, `disable`, `order`, `sort`) | Changes the mod list, which applies after a restart. Only when the player asks. |
| `get_mod_settings` (`mod`) | A running mod's simple public settings. |
| `set_mod_settings` (`mod`, `values`) | Changes and saves them. Only when the player asks. |

## Connection

| Tool | Purpose |
| --- | --- |
| `get_game_status` | Versions, language, whether a game is loaded, and which windows are open. |

## Errors

Failures come back as tool errors in the form `code: message`:

| Code | Meaning |
| --- | --- |
| `game_not_connected` | The game is closed, or the mod is not active. |
| `no_game_loaded` | The player is at the main menu or still setting up a game. |
| `no_world` | No world has been generated yet. |
| `not_selecting_site` | `show_site` was called outside the site selection screen. |
| `not_configuring_ideo` | `configure_ideo` was called outside the ideoligion screens. |
| `ideology_not_active` | The game does not have the Ideology expansion. |
| `not_choosing_colonists` | A colonist tool was called outside the colonist screen. |
| `not_on_new_game_screen` | `configure_new_game` was called outside its screens. |
| `wrong_ideo_screen` | The open customization screen is for a fixed ideoligion and a fluid one was asked for, or the reverse. |
| `game_busy` | The game is loading or saving. Try again shortly. |
| `game_not_responding` | The game's main thread did not answer in time, usually because the window is in the background with "Run in background" turned off. |
| `not_found`, `bad_request` | The arguments do not match anything, or are invalid. |
