# Contributing

Thanks for helping out. Bug reports, tool ideas and pull requests are all welcome.

## Project rules

These keep the project trustworthy and easy to maintain, so changes that break them will not be
accepted:

1. **Show only what the player can see.** New data must respect fog of war, hidden conditions and
   invisibility. Use the helpers in `mod/Source/Game/GameAccess.cs`. The one exception is the
   predicted rock layout in site search, before a game starts.
2. **Never modify the game, and only observe during play.** Do not touch the game's code or files. Read
   endpoints have no gameplay side effects and do not consume the game's random numbers: all endpoints
   run inside `Rand.PushState()`. On the new game screens, tools may set things up, but only when the
   player asks, using `POST`, taking the same steps as the game's own buttons, and always leaving the
   final confirmation to the player. Those are `show_site` and `configure_ideo` so far.
3. **No save footprint.** Do not add defs, game components or anything else that ends up in a save.
4. **No required third-party mods or libraries in the mod.** That includes Harmony. Use reflection
   for the occasional private field.
5. **Never freeze the game.** Endpoints run on the main thread, and a planet can have hundreds of
   thousands of tiles. Anything that scans the world must stop at a time limit and report how far it
   got. Beware of game methods that look cheap, because some loop over every settlement or sample a
   whole year of weather.
6. **Never commit game files.** This includes decompiled code and game assemblies. The RimWorld EULA
   allows decompiling to learn from and refer to the code, but not redistributing it.

## Setup

You need the .NET SDK, Node.js with pnpm, and a copy of RimWorld for building the mod.

```bash
# Build the mod. Set RIMWORLD_DIR if the game is not in the default Steam location.
dotnet build mod/Source -c Release

# Test the bridge core. This needs no copy of the game.
dotnet test --project mod/Tests

# Build and test the MCP server.
cd server
pnpm install
pnpm typecheck
pnpm test
pnpm build
```

`./scripts/decompile.sh` decompiles the game into the git-ignored `.decompiled/` folder. It is the
best way to find out how the game really works.

## Adding a tool

1. Add a handler to the mod under `mod/Source/Game/` and register it in `BridgeHost.BuildEndpoints`.
2. Add an entry to `server/src/tools.ts`.
3. Document it in `docs/tools.md`.

`MountainPredictor` mirrors `GenStep_ElevationFertility` and `TileMutatorWorker_Mountain`, including
the order of their random calls. Check it against the decompiled game whenever RimWorld updates.

Prefer compact, task-oriented results over dumping raw state, because every byte a tool returns
uses up the assistant's context. Wrap data from other mods in `try`/`catch` where it could throw, so
that one broken mod cannot take down a whole response.

Test in a real game with a real mod list before opening a pull request. The automated tests cover
the bridge core and the MCP server, but not the code that reads game state.

## Commits and language

Commits follow [Conventional Commits](https://www.conventionalcommits.org). All code, comments and
documentation are in English.
