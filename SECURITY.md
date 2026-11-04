# Security

## Reporting a vulnerability

Please report vulnerabilities privately through
[GitHub security advisories](https://github.com/kemalasliyuksek/rimworld-mcp/security/advisories/new)
and not in a public issue.

## Security model

The mod runs an HTTP server inside the game. These are the protections around it:

| Threat | Protection |
| --- | --- |
| Other machines on the network | The server binds to `127.0.0.1` only. |
| Other programs and users on the same machine | Every request needs a bearer token. It is random, 256 bits long, and new on each launch. The comparison takes constant time. |
| Web pages you visit, which can send requests to localhost | Requests with an `Origin` header are rejected. |
| DNS rebinding | The `Host` header must be exactly `127.0.0.1:<port>`. |
| Reading the token from disk | `~/.rimworld-mcp/bridge.json` is `0600` and its directory is `0700` on Linux and macOS. On Windows it relies on the privacy of the user profile. |
| Changing the game | Nothing modifies the game's code or files. Endpoints use `GET` and only read, except the `POST` endpoints. `/v1/game-settings/set`, `/v1/mods/configure` and `/v1/mods/settings/set` change the player's settings and mod list, through the game's own config files. `/v1/pawn/work`, `/v1/pawn/schedule`, `/v1/orders/*`, `/v1/gizmos/use`, `/v1/designators/use`, `/v1/plans/*`, `/v1/colony/*`, `/v1/dialogs/*`, `/v1/letters/answer`, `/v1/pawn/policies`, `/v1/trade/deal`, `/v1/caravans/*`, `/v1/abilities/use`, `/v1/policies/*` and `/v1/areas/set` give orders, build and run the colony during play. `/v1/cheats/*` go beyond what the game normally allows, and need the Cheats switch as well. All `POST` endpoints stop working if the player turns off the Actions switch in the mod's settings, and the bridge refuses to change those switches itself. The others refuse to run outside their new game screens: `/v1/new-game/configure` sets choices on the first screens, `/v1/sites/*` handle the site, `/v1/ideo/configure` builds the ideoligion being edited, and `/v1/starting-pawns/*` change the colonists on offer. The player confirms the result of all of them. |
| Flooding the game | The request queue is limited and at most four requests run per frame. |

## What this does not protect against

- **Software running as your user** can read the token file, just as it could read your saves or
  anything else you own. Treat that software as trusted, or do not run it alongside the game.
- **Prompt injection through game content.** Names and descriptions come from mods and other players'
  content, and they reach the assistant, which can also change the game. The server tells assistants to
  treat them as data, but that is a mitigation and not a guarantee. Use your client's approval prompts,
  and turn off the switches in the mod's settings for anything you do not want. Your assistant may have
  other tools too, so review what it does as you normally would.
