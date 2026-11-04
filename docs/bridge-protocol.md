# Bridge protocol

This is the private protocol between the mod and the MCP server. It may change between versions, so
other programs should use MCP instead.

## Discovery

On launch, the mod writes `~/.rimworld-mcp/bridge.json` and removes it on exit. Set
`RIMWORLD_MCP_DISCOVERY_FILE` on the MCP server to read it from another location, for example when
Steam runs in a Flatpak with its own home directory.

```json
{
  "format_version": 1,
  "port": 41234,
  "token": "64 hexadecimal characters",
  "bridge_version": "0.1.0",
  "process_id": 12345
}
```

The port and token change on every launch. A crash can leave the file behind, and in that case the
connection to the port fails.

## Requests

```http
GET /v1/overview?map=0 HTTP/1.1
Host: 127.0.0.1:41234
Authorization: Bearer <token>
```

- Endpoints that act on the player's request use `POST`, and all others use `GET`.
- `Host` must be exactly `127.0.0.1:<port>`.
- Requests with an `Origin` header are rejected.

## Responses

Success is status 200 with a JSON body. Keys use `snake_case`, and fields that do not apply may be
missing.

Failure looks like this:

```json
{ "error": { "code": "no_game_loaded", "message": "No game is loaded. The player is at the main menu." } }
```

| Status | Codes |
| --- | --- |
| 400 | `bad_request` |
| 401 | `unauthorized` |
| 403 | `forbidden_host`, `forbidden_origin` |
| 404 | `not_found`, `unknown_endpoint` |
| 405 | `method_not_allowed` |
| 409 | `no_game_loaded`, `no_map`, `no_world`, `not_selecting_site`, `not_configuring_ideo`, `ideology_not_active`, `not_choosing_colonists`, `not_on_new_game_screen`, `wrong_ideo_screen` |
| 429 | `too_many_requests` |
| 500 | `internal_error` |
| 503 | `game_busy`, `game_not_responding` |

## Endpoints

| Path | Parameters | MCP tool |
| --- | --- | --- |
| `/v1/status` | | `get_game_status` |
| `/v1/mods` | | `list_mods` |
| `/v1/overview` | `map` | `get_colony_overview` |
| `/v1/alerts` | | `get_alerts` |
| `/v1/focus` | | `get_player_focus` |
| `/v1/pawns` | `group`, `limit`, `map` | `list_pawns` |
| `/v1/pawn` | `id` | `get_pawn` |
| `POST /v1/pawn/work` | `pawn`, `priorities`, `manual_priorities` | `set_work_priorities` |
| `POST /v1/pawn/schedule` | `pawn`, `hours` | `set_schedule` |
| `/v1/threats` | `map` | `get_threats` |
| `/v1/resources` | `map` | `get_resources` |
| `/v1/base` | `sections`, `map` | `get_base` |
| `/v1/research` | `limit` | `get_research` |
| `/v1/events` | `limit` | `get_recent_events` |
| `/v1/world` | | `get_world` |
| `/v1/map/view` | `x`, `z`, `width`, `height`, `map` | `view_map` |
| `/v1/map/cell` | `x`, `z`, `map` | `inspect_cell` |
| `/v1/defs/search` | `q`, `type`, `limit` | `search_defs` |
| `/v1/defs/get` | `def`, `type` | `get_def` |
| `/v1/new-game` | | `get_new_game_setup` |
| `POST /v1/new-game/configure` | See `docs/tools.md` | `configure_new_game` |
| `/v1/sites/world` | | `get_world_settings` |
| `/v1/sites/search` | See `docs/tools.md` | `search_sites` |
| `/v1/sites/get` | `tile` | `get_site` |
| `POST /v1/sites/show` | `tile` | `show_site` |
| `POST /v1/sites/options` | `map_size`, `season` | `set_start_options` |
| `/v1/ideo/options` | `section`, `issue` | `get_ideo_options` |
| `/v1/ideo/setup` | | `get_ideo_setup` |
| `POST /v1/ideo/configure` | See `docs/tools.md` | `configure_ideo` |
| `/v1/ideo/icon-sheet` | `names`, `columns` | `view_ideo_icons` |
| `/v1/game-settings` | | `get_game_settings` |
| `POST /v1/game-settings/set` | `values` | `set_game_settings` |
| `/v1/mods/installed` | `q` | `get_installed_mods` |
| `POST /v1/mods/configure` | `enable`, `disable`, `order`, `sort` | `configure_mods` |
| `/v1/mods/settings` | `mod` | `get_mod_settings` |
| `POST /v1/mods/settings/set` | `mod`, `values` | `set_mod_settings` |
| `/v1/starting-pawns` | | `get_starting_pawns` |
| `POST /v1/starting-pawns/find` | See `docs/tools.md` | `find_starting_pawn` |
| `POST /v1/starting-pawns/swap` | `first`, `second` | `swap_starting_pawns` |
| `POST /v1/starting-pawns/edit` | See `docs/tools.md` | `edit_starting_pawn` |
