# Decisions

One file per decision. Status: proposed, accepted, revised, superseded. A superseded record stays and links to its replacement.

| ID | Decision | Status |
| --- | --- | --- |
| [0001](0001-stack.md) | Avalonia 12, .NET 10, G3MLib, GPL-3.0-only | accepted |
| [0002](0002-solution-layout.md) | Two projects (Core, App) plus tests; editors register through `IEditorProvider` | proposed |
| [0003](0003-g3mlib-consumption.md) | G3MLib only as NuGet; G3MLib 1.1.0 finished and released first | accepted |
| [0004](0004-git-cli.md) | Git through the system `git` CLI | proposed |
| [0005](0005-updater.md) | Own updater for portable zips; GitHub releases, SHA-256 digest check; Velopack not chosen | revised |
| [0006](0006-localization-format.md) | G3M's JSON language format for UI strings | proposed |
| [0007](0007-git-unit.md) | Git tracks the project folder, not the binary data file | proposed |
| [0008](0008-project-format.md) | Project folders follow GameMaker's .yyp layout; UTMT 0.9 import/export; deterministic full and delta modes | revised |
| [0009](0009-plugins.md) | Plugins: folder with plugin.json and C# sources compiled with Roslyn, full API, full trust asked once | accepted |
| [0010](0010-layout-modes.md) | Layout: docks, tabs, canvas and floating windows mixed freely; Studio, Code and Canvas presets | accepted |
| [0010](0010-workspace-and-sync.md) | Workspace model: project, representation, data file; Project and Direct mode; sync, external change and recovery | accepted direction, details open |
