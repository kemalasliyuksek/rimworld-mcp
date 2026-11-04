# RimWorld MCP

Talk to your colony. Let an AI assistant see it, understand it and, when you ask, run it.

RimWorld MCP connects a running game of RimWorld to AI assistants through the
[Model Context Protocol](https://modelcontextprotocol.io). Ask "what should I do next?" while you play,
and the assistant looks at your actual colony before answering, with no screenshots. Say "draft everyone
and hold the east wall", "lay out a bedroom block and show me first" or "you play for a while", and it
can do that too.

> **Status: early development.** Developed and tested on Linux with RimWorld 1.6. Windows and macOS are
> expected to work but have not been tested yet.

## See everything you can see

| Area | Examples |
| --- | --- |
| **Colony** | Date, weather, mood, days of food, alerts, letters |
| **Your focus** | What you have selected, where the camera is, the dialog in front of you and its choices |
| **Pawns** | Skills, passions, traits, every mood thought, injuries, gear, work priorities, schedule, relations |
| **Threats** | Enemies by faction and weapon, your fighters, armor, turrets |
| **Base** | Rooms and their stats, power grids, crops, work benches and bills, beds, stockpiles |
| **Map** | A text rendering of any area, and details of any cell |
| **World** | Factions, quests, caravans, ideoligion, research, event history |
| **Game data** | Every item, building, recipe and more, from the base game, DLCs and your active mods, with patches applied |

## Do anything you ask

Everything here happens only when you ask. It runs in the background through the game's own code, so
your mouse, keyboard, selection and windows stay yours, and you can do something else meanwhile.

| Area | Examples |
| --- | --- |
| **Orders** | Draft, move, attack, and anything in a colonist's right-click menu or on a button, including what mods add |
| **Building** | Everything on the Architect menu: build, mine, cut, make zones. Layouts can be drawn as plan marks for you to approve first |
| **Running the colony** | Work priorities, schedules, research, bills, crops, stockpiles, allowed areas, outfit, drug and food policies |
| **Events** | Answer dialogs and letters, trade, send caravans |
| **Abilities** | Psycasts and any other ability, from the game, DLCs or mods |
| **Playing for you** | Set the game speed and keep going until you say stop. It costs tokens and cannot react in real time, so it pauses to think |
| **Cheats** | Spawn items and pawns, fire events, finish research, build instantly, edit anyone: name, skills, traits, needs, thoughts, health |
| **Hidden information** | What is under the fog, only when you ask about it. It never mentions it otherwise |
| **Game options** | RimWorld's settings, the mod list and mods' settings |

Three switches in the mod's settings turn off actions, cheats and hidden information. Assistants cannot
change them.

See [docs/tools.md](docs/tools.md) for all the tools.

## Stop losing hours to the landing screen

Tell the assistant what you want before you start, whatever it is: *"somewhere with caves and a
river"*, or *"a mountain curved around open ground that is easy to defend, with crops all year and no
caves."* It searches the whole world, explains its favorite and moves your camera straight to it. You
confirm by pressing Next.

The mountain shapes come from the same noise the game uses to generate the map, so there is usually no
need to reroll seeds. On the one map compared so far, the predicted rock matched at 99.6% of 3025
sampled points.

## Skip the setup maze

- **Ideoligion:** the assistant asks how you want to play, explains the options your game really offers,
  including those from mods, and can set the whole thing up.
- **Colonists:** say what you need, such as *"keep Tsu, replace the others with a doctor and a good shot"*.
  It generates candidates with the game's own generator until it finds a match, and can edit them if you
  ask.
- **The rest:** scenario, storyteller, difficulty, world settings and factions.

You review the result on screen and press Next yourself.

## Works with your mods, safe for your save

Most tools use the game's own menus, buttons and data, so what mods add comes along with no code for
them. The mod needs no other mods.

It adds no defs, no game components and no Harmony patches, and it never changes the files of the game
or of other mods. You can add it to or remove it from an ongoing game. What it creates on request is
ordinary game content, so it stays in your save like anything else you built.

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

Planned work is tracked in [docs/roadmap.md](docs/roadmap.md).

## Contributing

Contributions are welcome. See [CONTRIBUTING.md](CONTRIBUTING.md).

## License

[MIT](LICENSE) © Kemal Aslıyüksek

Portions of the materials used to create this mod are trademarks and/or copyrighted works of Ludeon
Studios Inc. All rights reserved by Ludeon. This mod is not official and is not endorsed by Ludeon.
