# RimWorld MCP

Give your AI assistant eyes on your colony.

RimWorld MCP connects a running game of RimWorld to AI assistants through the
[Model Context Protocol](https://modelcontextprotocol.io). Ask "what should I do next?" while you
play, and the assistant looks at your actual colony before answering, with no screenshots and no
describing your situation first.

> **Status: early development.** Version 0.1 observes the game and is developed on Linux with RimWorld 1.6.
> Windows and macOS are expected to work but have not been tested yet.

## What it can see

| Area | Examples |
| --- | --- |
| **Colony** | Date, weather, mood, days of food, alerts, unread letters |
| **Your focus** | What you have selected, where the camera is, the event dialog in front of you and its choices |
| **Pawns** | Skills, passions, traits, every mood thought, injuries, gear, work priorities, schedule, relations |
| **Threats** | Enemies by faction and weapon, your fighters, turrets |
| **Base** | Rooms and their stats, power grids, crops, work benches and bills, beds, stockpiles |
| **Map** | A text rendering of any area, and details of any cell |
| **World** | Factions, quests, caravans, ideoligion, research, event history |
| **Game data** | Every item, building, recipe and more, from the base game, DLCs and your active mods, with patches applied |

See [docs/tools.md](docs/tools.md) for the full list.

## Stop losing hours to the landing screen

Tell the assistant what you want before you start, whatever it is: *"somewhere with caves and a
river"*, or *"a mountain curved around open ground that is easy to defend, with crops all year and no
caves."* It searches the whole world, explains its favorite and moves your camera straight to it. You
confirm by pressing Next.

The mountain shapes come from the same noise the game uses to generate the map, so there is usually
no need to reroll seeds at all.

## Fair play by design

During play, the bridge reports only what you can see yourself. Unexplored areas, invisible enemies
and undiscovered diseases stay hidden, so the assistant is a knowledgeable friend looking over your
shoulder and not a cheat.

There is one deliberate exception, before a game begins: site search predicts the rock layout of a
tile, as map preview mods do. It reveals nothing else about the map, such as ores or ruins.

Version 0.1 also cannot change your game. The only thing it can do is move the camera and the
selection on the site selection screen, and you still confirm the choice yourself.

## Safe for your save

The mod adds no defs, no game components and no Harmony patches, and it needs no other mods. It
leaves nothing in your save files, and you can add it to or remove it from an ongoing game.

## Installation

You need RimWorld 1.6, the [.NET SDK](https://dotnet.microsoft.com/download) and
[Node.js](https://nodejs.org) 22 or later with [pnpm](https://pnpm.io).

```bash
git clone https://github.com/kemalasliyuksek/rimworld-mcp.git
cd rimworld-mcp
./scripts/install.sh
```

The script builds both parts and copies the mod into your game's `Mods` folder. If RimWorld is not
in the default Steam location, set `RIMWORLD_DIR` first.

Then:

1. Start RimWorld, enable **RimWorld MCP** in the mod list and restart the game when asked.
   Turn on **Run in background** in the game's options too. Otherwise the game stops when you switch
   to your assistant, and it cannot answer. With it on, the game keeps running while you are away, so
   pause before you switch.
2. Register the server with your assistant. For [Claude Code](https://claude.com/claude-code):

   ```bash
   claude mcp add --scope user rimworld -- node "$PWD/server/dist/index.js"
   ```

3. Load your game and ask something like *"How is my colony doing?"*

Other MCP clients work too: run `node server/dist/index.js` as a stdio server.

## How it works

```text
AI assistant  <-- MCP over stdio -->  MCP server  <-- HTTP on 127.0.0.1 -->  Mod in RimWorld
```

The mod runs inside the game and answers requests on the game's main thread. The MCP server is a
small separate process that translates between MCP and the mod. Read
[docs/architecture.md](docs/architecture.md) for the details and
[SECURITY.md](SECURITY.md) for the security model.

## Roadmap

Planned work is tracked in [docs/roadmap.md](docs/roadmap.md). The next big step is optional,
player-approved actions.

## Contributing

Contributions are welcome. See [CONTRIBUTING.md](CONTRIBUTING.md).

## License

[MIT](LICENSE) © Kemal Aslıyüksek

Portions of the materials used to create this mod are trademarks and/or copyrighted works of Ludeon
Studios Inc. All rights reserved by Ludeon. This mod is not official and is not endorsed by Ludeon.
