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
| Changing the game | No endpoint writes to the game. All endpoints use `GET`, except `POST /v1/sites/show`, which only moves the camera and selection and refuses to run outside the site selection screen. |
| Flooding the game | The request queue is limited and at most four requests run per frame. |

## What this does not protect against

- **Software running as your user** can read the token file, just as it could read your saves or
  anything else you own. Treat that software as trusted, or do not run it alongside the game.
- **Prompt injection through game content.** Names and descriptions come from mods and other players'
  content, and they reach the assistant. The server tells assistants to treat them as data, but that
  is a mitigation and not a guarantee. Having no way to change the game limits what an attack could achieve
  inside it, but your assistant may have other tools, so review what it does as you normally would.
