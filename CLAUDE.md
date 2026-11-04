# RimWorld MCP

Lets AI assistants see and, when the player asks, do anything in a running game of RimWorld through
MCP. Open source, MIT, English only.

- `mod/`: the RimWorld mod (C#, net472). `Source/Core` has no game dependency and is unit tested.
  `Source/Game` reads game state and can only be tested in a running game.
- `server/`: the MCP server (TypeScript, stdio). `src/tools.ts` is the tool table.
- `docs/`: architecture, tools, bridge protocol and roadmap.

Read the rules in `CONTRIBUTING.md` before changing the mod. They are hard constraints, above all:
anything RimWorld allows, cheats included, but only when the player asks; hidden information only when
they ask about it; removing the mod must not break a save; and it must run without trouble with no mods
or with many.

Keep Unity-specific code in `BridgeBootstrap`, `BridgeHost` and `IconSheet`.

`MountainPredictor` must stay in step with the game's map generation, down to the order of its random
calls. Recheck it against `.decompiled/game` after any game update.

## Commands

```bash
dotnet build mod/Source -c Release   # needs RimWorld installed
dotnet test --project mod/Tests      # no game needed
cd server && pnpm typecheck && pnpm test && pnpm build
./scripts/install.sh                 # build everything and copy the mod into the game
./scripts/decompile.sh               # game source into .decompiled/ (git-ignored)
```

Inside the Claude Code sandbox, `dotnet test` fails because it cannot open a named pipe. Use
`dotnet run --project mod/Tests` instead.

## Working on game code

Check game APIs against `.decompiled/game` rather than from memory, since they change between
versions. Never commit anything from `.decompiled/`: the EULA forbids redistributing it.

RimWorld 1.7 replaces Unity with the Waylith engine, which is why Unity-specific code is kept apart.
