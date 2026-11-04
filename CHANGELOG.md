# Changelog

All notable changes to this project are documented here. The format is based on
[Keep a Changelog](https://keepachangelog.com), and the project follows
[Semantic Versioning](https://semver.org).

## [Unreleased]

### Added

- RimWorld 1.6 bridge mod with a local, token-protected HTTP API that cannot change the game's code or files.
- MCP server with 51 tools covering the colony, the player's focus, pawns, threats, resources, the
  base, research, events, the world, the map, game data and mods.
- Scenario, storyteller, difficulty and world settings tools on the first new game screens, on the
  player's request.
- RimWorld settings, the mod list and mods' settings can be read, and changed on the player's request.
- Site search before a game starts, with filters for climate, terrain, water and caves, ranking by
  predicted mountain shape to find defensible crescents, and a tool that shows a site in the game.
- Ideoligion help on the new game screens: every preset, meme and precept in the game, and a tool that
  sets the ideoligion up on the player's request.
- Colonist help on the new game screen: read every colonist on offer, generate candidates to match
  criteria, swap them in, and edit their attributes on the player's request, including removing all
  of a colonist's relations.
- Pawn relations now follow the game's social tab: implied relations such as siblings are included, and
  relatives the player has not met are left out.
- Visible items outside storage in `get_resources`, and counted in the overview's days of food, since
  a new colony's supplies are not in storage yet. Forbidden items are marked, and forbidden food is
  reported separately because nobody eats it until the player allows it.
- Work priorities and schedules can be changed during play, on the player's request, and `get_pawn`
  reports whether manual priorities are on.
- Orders on the player's request: drafting, moving, and anything in a colonist's right-click menu or on
  the buttons of a pawn or building, including what mods add.
- Switches in the mod's settings for actions, cheats and hidden information.
- The Architect menu on the player's request, for building, mining, zones and what mods add, and plan
  marks for proposing a layout before anything is built.
- `get_pawn` lists how a pawn and the others on the map regard each other.

### Fixed

- Buttons that act on the selected pawn no longer act on what the player has selected.
- The stored weapons list no longer includes things like wood.

### Changed

- `get_def` leaves out fields at their default value, which were most of its output.
- `get_base` rejects unknown section names instead of ignoring them.

### Fixed

- Fertile, marshy and other tiles whose features only repaint the ground no longer report
  `prediction_exact: false`.
