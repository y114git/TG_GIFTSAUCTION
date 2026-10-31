# Vision

G3MStudio is a desktop editor for compiled GameMaker data files (`data.win`, `game.ios`, `game.droid`, `game.unx`). It replaces UndertaleModTool and the tools around it.

## Goals

1. Feature parity with the latest UndertaleModTool, verified item by item against [research/utmt-parity.md](research/utmt-parity.md). Nothing is dropped because it is rare or hard.
2. A better daily workflow than UndertaleModTool: a data file opens like a folder in VS Code or a project in GameMaker, with search, quick open, a command palette, tabs, docking, autosave, recovery and Git.
3. Purpose-built editors for every resource type (sprites, rooms, code, sounds, fonts, sequences and the rest) instead of generic property grids.
4. Part of the G3M family: 90% G3M's visual style (10% from GameMaker, VS Code and Zed), every border exactly 2 px, the same languages as G3M.
5. Fast and light: quick startup, low memory, no UI freezes on large games.
6. Cross-platform on every OS and architecture G3M supports.
7. Safe: no silent data loss, clear difference between saved, backed up and committed.

## Foundations

- UI: Avalonia (same major version as G3MTool GUI, currently 12.x).
- Core: G3MLib for reading, writing, decompiling, compiling, patching and diffing, consumed only as a NuGet package (version 1.1.0, see ADR-0003).
- Runtime: .NET 10, self-contained single-file builds, like G3MTool.

## First release

The first public version is 1.0.0-beta. It must be complete and polished: full UTMT parity, every P0 and P1 requirement, the performance budget met. The scope is in [roadmap.md](roadmap.md). G3MLib 1.1.0, released first, is the final library version G3MStudio is built on.

## Out of scope (for now)

- Building or running GameMaker source projects (`.yyp`). G3MStudio edits compiled games. Export to and import from a project folder is in scope.
- An extension marketplace. C# scripting covers automation.
