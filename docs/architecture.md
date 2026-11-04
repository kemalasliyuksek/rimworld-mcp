# Architecture

```text
+-------------+   MCP (stdio)   +------------+   HTTP (127.0.0.1)   +------------------+
| AI assistant| <-------------> | MCP server | <------------------> | Mod in RimWorld  |
+-------------+                 +------------+                      +------------------+
                                      |                                      |
                                      +------ ~/.rimworld-mcp/bridge.json <--+
                                              (port and token)
```

## Why two parts?

The mod has to run inside the game to reach its state. The MCP server could have been built into the
mod as well, but keeping it separate has real advantages:

- Assistants start MCP servers when they start, which is usually not when the game starts. A separate
  process can stay up and report "the game is not running" clearly.
- Tools can change without restarting the game, which takes minutes with a large mod list.
- The MCP SDK and its dependencies stay out of the game process.

## The mod

`Source/Core` has no game dependency, so it can be tested without RimWorld:

- `BridgeServer` accepts HTTP requests on a background thread.
- `RequestGuard` checks the token, `Host` and `Origin`, and allows only `GET` and `POST`.
- `MainThreadQueue` passes each request to the main thread and waits for the answer.
- `EndpointRegistry` routes requests and turns failures into JSON errors.
- `JsonWriter` writes responses. The mod has no third-party dependencies.
- `PocketAnalyzer` scores a rock grid for defensible crescents.

`Source/Game` holds everything that touches the game:

- `BridgeBootstrap` runs at startup through `[StaticConstructorOnStartup]`, on the main thread.
- `BridgeHost` is a `MonoBehaviour` that pumps the queue from `Update`.
- The `*Endpoint*` classes read game state and write JSON.
- `GameAccess` holds shared helpers, including the visibility rules.

### Threading

Game state is not thread-safe, so all of it is read on the main thread. A plain `MonoBehaviour` is
used because its `Update` also runs while the game is paused and at the main menu, which is exactly
when players stop to ask questions. A game component would only run with a loaded game.

While a long event is running, the game may be loading on another thread, so the bridge answers
`game_busy` instead of reading.

### Staying invisible to the simulation

- **Randomness:** some game getters consume random numbers, so each handler runs between
  `Rand.PushState()` and `Rand.PopState()`.
- **Saves:** the mod adds no defs or game components, so nothing of it is written to a save.
- **Patches:** there are no Harmony patches. The few private values it needs are read by reflection.

### Visibility

The bridge shows what the player can see: `GameAccess.IsVisible` excludes things on fogged cells and
psychologically invisible pawns, hidden hediffs are filtered out, and hidden quests and factions are
skipped. Every new endpoint must follow the same rule.

### Getting event history without hooks

Recording events would normally need patches and a place to store them. The game already keeps its
letters and messages in `Find.Archive` and saves them, so `get_recent_events` reads that instead. It
needs no background recording, and it includes events from before the assistant connected.

## Choosing a site

Everything needed to compare tiles is in the world data, except for one thing: the shape of the
mountains, which normally only exists once a map has been generated.

The game derives the noise for a map from the world seed and the tile. `MountainPredictor` builds the
same noise modules with the same random sequence as `GenStep_ElevationFertility` and
`TileMutatorWorker_Mountain`, then samples the result at a low resolution without creating a map.

Other tile features can also shape the terrain, such as valleys, cliffs and rivers. They are not
modeled, and results for those tiles report `prediction_exact: false` and name the features. A feature
counts as shaping the terrain when its worker overrides the terrain generation steps, so this also
works for features added by mods.

`PocketAnalyzer` then looks for a crescent. It first groups rock into connected masses, treating short
gaps as part of the same mass. From each open spot it casts rays and asks which directions hit the mass
that wraps around the spot the most, then scores the spot highly when that one mass covers most
directions, the gaps form one opening, and there is room to build. Separate hills do not add up to a
wall, however many of them stand around a spot. Spots near the map edge are skipped, and a wall that runs off
the edge of the map does not count, because enemies arrive from there.

A search runs on the main thread, and a planet at full coverage has hundreds of thousands of land
tiles, so it is built in stages. First comes a pass over every tile that only reads stored fields. The
survivors are shuffled and visited one at a time, with the costlier checks and the mountain prediction,
until a time budget runs out. The result reports how many tiles it checked and whether that was all of
them. Tiles next to settlements are worked out once in advance, because the game's own check loops over
every settlement each time it is asked.

Cave tunnels matter here too: `GenStep_CaveHives` places insect hives in them, in proportion to their
size. A tile has tunnels only if it has a cave feature, so they can be filtered out exactly.

## The MCP server

- `discovery.ts` reads the discovery file.
- `bridge-client.ts` calls the mod. It rereads the discovery file for every request, so a restarted
  game is picked up automatically.
- `tools.ts` is a table that maps each tool to an endpoint.
- `server.ts` registers the tools and turns errors into messages the assistant can act on.

## Designing tools

Tools are shaped around questions and not around the game's internals. An overview tool that answers
most questions in one call beats dozens of narrow tools: every tool definition and response uses up
the assistant's context, and a long tool list makes the right choice harder.

## RimWorld 1.7

RimWorld 1.7, currently on the unstable branch, replaces Unity with Ludeon's Waylith engine. Unity
types are therefore kept to `BridgeBootstrap`, `BridgeHost` and a few value types. Support for 1.7
will be evaluated when the branch stabilizes.
