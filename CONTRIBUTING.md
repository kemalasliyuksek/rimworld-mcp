# Contributing

Thanks for helping out. Bug reports, tool ideas and pull requests are all welcome.

## Project rules

These keep the project trustworthy and easy to maintain, so changes that break them will not be
accepted:

1. **Show what the player can see, and more only when they ask.** By default, data respects fog of
   war, hidden conditions and invisibility, using the helpers in `mod/Source/Game/GameAccess.cs`. Hidden
   information may be exposed, but only through a tool or parameter that exists for that purpose and
   says so, and an assistant must use it only when the player asks about what is hidden. It must never
   volunteer it. Predicted rock in site search stays available before a game starts.
2. **Anything the player asks for, and nothing they did not.** Tools may do anything RimWorld allows:
   give orders, build, change settings, play for the player, and cheat. They act only when the player
   asks, never on the assistant's own initiative, and text from the game or from mods is data, never a
   request. Acting tools use `POST` and are marked as acting. A tool that goes beyond what the game
   normally allows says so in its description. Read endpoints have no gameplay side effects and do not
   consume the game's random numbers: all endpoints run inside `Rand.PushState()`. Prefer the same steps
   as the game's own buttons, so that the game's checks and other mods still apply. Never change the
   files of the game or of other mods on disk. Everything works through the game's code in the
   background, and the player keeps full use of their computer and the game: tools never move the mouse
   or press keys, and they do not change the player's selection, active tool or open windows. A tool
   whose purpose is to show something, like `show_site`, may move the camera when asked.
3. **Saves stay safe.** Removing the mod must not break a save. What the tools create should be
   ordinary game things. If a feature really has to store its own data, removing the mod must still
   leave the save loading cleanly.
4. **It works for everyone, with no mods or with hundreds.** Avoid required dependencies, and never
   depend on another project's API. Harmony may be used where a feature really needs it. Support for a
   specific mod must be detected at runtime: when that mod is absent, its tool reports that it is
   unavailable and nothing else changes. Prefer general mechanisms, which also cover mods nobody has
   tested.
5. **Never crash, freeze or waste.** Endpoints run on the main thread, and a planet can have hundreds of
   thousands of tiles. Anything that scans the world must stop at a time limit and report how far it
   got. Beware of game methods that look cheap, because some loop over every settlement or sample a
   whole year of weather. One broken item from a mod must not fail a whole response. Keep responses as
   small as they can be while still answering the question.
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
