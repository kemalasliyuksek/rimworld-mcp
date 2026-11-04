# Tools

All tools are read-only, except for `show_site`, which moves the camera and the selection on the site
selection screen. Those marked with `map` accept an optional map id and default to the map the player
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

## Choosing a site

These work before a game starts, once a world has been generated.

| Tool | Purpose |
| --- | --- |
| `get_world_settings` | Seed, world settings, the selected tile, and how many tiles have each biome, hilliness and feature. |
| `search_sites` (`biomes`, `hilliness`, `min_growing_days`, `min_coldest_c`, `max_hottest_c`, `min_rainfall`, `max_rainfall`, `max_diseases_per_year`, `max_pollution_pct`, `caves`, `coast`, `lake`, `river`, `road`, `exclude_features`, `require_features`, `shape`, `min_pocket_score`, `min_rock_pct`, `max_rock_pct`, `limit`, `offset`) | Finds tiles that match, optionally ranked by predicted mountain shape. Every condition is optional. |
| `get_site` (`tile`) | Full details of a tile, with a text preview of its predicted mountains. |
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

## Connection

| Tool | Purpose |
| --- | --- |
| `get_game_status` | Versions, language, and whether a game is loaded. |

## Errors

Failures come back as tool errors in the form `code: message`:

| Code | Meaning |
| --- | --- |
| `game_not_connected` | The game is closed, or the mod is not active. |
| `no_game_loaded` | The player is at the main menu or still setting up a game. |
| `no_world` | No world has been generated yet. |
| `not_selecting_site` | `show_site` was called outside the site selection screen. |
| `game_busy` | The game is loading or saving. Try again shortly. |
| `game_not_responding` | The game's main thread did not answer in time, usually because the window is in the background with "Run in background" turned off. |
| `not_found`, `bad_request` | The arguments do not match anything, or are invalid. |
