# Tools

All tools are read-only. Those marked with `map` accept an optional map id and default to the map the
player is looking at.

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

## Colony

| Tool | Purpose |
| --- | --- |
| `get_threats` (`map`) | Visible enemies, colony fighters and turrets. |
| `get_resources` (`map`) | Stock by category, wealth, stored weapons and apparel. |
| `get_base` (`sections`, `map`) | Rooms, power, zones, production and beds. |
| `get_research` (`limit`) | Current project and available projects. |
| `get_world` | Factions, quests, caravans, maps and ideoligion. |

## Map

| Tool | Purpose |
| --- | --- |
| `view_map` (`x`, `z`, `width`, `height`, `map`) | Text rendering of an area, up to 120 by 120 cells, with a legend. |
| `inspect_cell` (`x`, `z`, `map`) | Details of one cell. |

## Game knowledge

| Tool | Purpose |
| --- | --- |
| `search_defs` (`q`, `type`, `limit`) | Search definitions from the game, DLCs and active mods. |
| `get_def` (`def`, `type`) | The full data of one definition, with mod patches applied. |
| `list_mods` | Active mods in load order. |

## Connection

| Tool | Purpose |
| --- | --- |
| `get_game_status` | Versions, language, and whether a game is loaded. |

## Errors

Failures come back as tool errors in the form `code: message`:

| Code | Meaning |
| --- | --- |
| `game_not_connected` | The game is closed, or the mod is not active. |
| `no_game_loaded` | The player is at the main menu. |
| `game_busy` | The game is loading or saving. Try again shortly. |
| `game_not_responding` | The game's main thread did not answer in time. |
| `not_found`, `bad_request` | The arguments do not match anything, or are invalid. |
