# GameMaker coverage checklist

Every window, menu item, pane and editor of GameMaker LTS 2026 (IDE v2026.0.0.16), with how it is documented. "shot N" refers to screenshots/NN-*.jpg and the matching section in README.md. "manual" means editors.md (from the official manual). Update this list whenever an item is captured.

Status: captured, manual, todo. Hands-on pass complete on 2026-10-25: no todo items left; manual items are covered in editors.md and would need native dialogs, a full build, or input the IDE ignores from automation.

## Shell

| Item | Status |
| --- | --- |
| Start page, set up wizard, recent projects | captured, shot 32 |
| Main window, docks, workspace canvas | captured, shot 1 |
| Toolbar and target bar | captured, shot 1 |
| Target bar dropdowns and target settings | manual (selector did not open from synthetic input; not relevant to data files) |
| Inspector panel and its menu | captured, shots 13, 33, 58; menu manual |
| Asset Browser, Quick Access, create popup | captured, shots 1, 12 |
| Asset Browser menu and filter menu | manual (did not open from synthetic input) |
| Asset Browser context menu (right click) | manual (synthetic right click does not open it) |
| Go To (Ctrl+T) | captured, shots 47, 48 |
| Workspace tabs, chained windows, canvas pan | captured, shots 33, 45 |
| Layouts: reset | captured, shot 120; save and load dialogs manual |
| Notifications | manual |

## Menus

| Menu | Status |
| --- | --- |
| File | captured, shots 2, 3 |
| File > New Project (template chooser) | captured, shots 93 to 96 |
| File > Import Project | manual (native file dialog) |
| File > Export Project (YYZ, As Template) | manual (native file dialog) |
| Edit | captured, shot 4 |
| Build | captured, shot 5 |
| Build > Create Executable | manual (starts a full build); headless equivalent documented in ../gamemaker-format.md |
| Windows | captured, shot 6 |
| Windows > Recent Windows | captured, shot 109 |
| Windows > IDE Environment Variables | captured, shot 110 |
| Windows > Project Health | captured, shot 111 |
| Windows > Project Tool Output | captured as the Project Tool log, shot 96 |
| Tools | captured, shot 7 |
| Tools > Texture Groups | captured, shots 79, 80 |
| Tools > Audio Groups | captured, shot 81 |
| Tools > Sound Mixer | captured, shot 91 |
| Tools > Generate Project Images | captured, shot 116 |
| Tools > Create Local Package | captured, shot 117 |
| Tools > Import Local Package | manual (native file dialog) |
| Tools > Package Manager | captured, shot 92 |
| Tools > Project Tool | captured as its log window, shot 96 |
| Marketplace | captured, shot 8 (opens a web page) |
| Layouts | captured, shot 9 |
| Help | captured, shot 10 |
| Help > About, Licences | captured, shots 118, 119 |
| Source Control | captured, shot 11 |
| Source Control > Clone Repository | captured, shot 90 |
| Source Control with a repository (commit, history, conflicts) | manual; G3MStudio follows VS Code and Zed here instead (vscode-zed.md) |

## Bottom panes

| Pane | Status |
| --- | --- |
| Output | captured, shot 84 area |
| Search Results | captured, shot 84 |
| Source Control output | captured, shot 115 |
| Breakpoints | captured, shot 113 |
| Compile Errors | captured, shot 114 |
| Feather Messages | captured, shot 45 |
| Prefab Library | captured, shot 112 |

## Resource editors

| Editor | Status |
| --- | --- |
| Sprite editor (texture settings, collision mask, nine slice) | captured, shots 13 to 16 |
| Sprite origin preset menu, broadcast messages, Spine sprites | manual; Spine noted in pass 3 |
| Image editor (menus, toolbox, colours) | captured, shots 17 to 20 |
| Image editor layers panel, onion skin settings | manual |
| Object editor and every event menu | captured, shots 33 to 43 |
| Object Parent, Physics, Variable Definitions windows | captured, shots 121 to 123 |
| Code editor (Feather, autocomplete) | captured, shots 45, 46 |
| Code editor context menu | manual |
| Script editor | captured, shot 57 |
| Room editor (menu, layers, settings, viewports, physics, effects) | captured, shots 49 to 56, 89 |
| Room: Instance Creation Order, Layer View | captured, shot 108 (Layer View toggles the layer panel) |
| Room: instance list and instance properties | captured, shots 98 to 101 |
| Room: tile layer painting tools | manual (template has no tile layers; brush builder captured, shot 66) |
| Room: asset, effect and UI layers, flex panels | captured, shots 102 to 107; path layer manual |
| Sound editor | captured, shot 59 |
| Font editor and options | captured, shots 60, 61 |
| Path editor | captured, shot 63 |
| Tile set editor, resource picker, brush builder | captured, shots 64 to 66 |
| Tile set: tile animation, auto tiling | manual |
| Timeline editor | captured, shot 67 |
| Sequence editor | captured, shot 68 |
| Animation curve editor | captured, shot 69 |
| Particle system editor | captured, shots 70 to 72 |
| Extension editor | captured, shot 73 |
| Extension: add file, function definitions | manual |
| Note editor | captured, shot 74 |
| Shader editor | captured, shot 75 |
| Included Files | captured, shot 124 |

## Settings and dialogs

| Item | Status |
| --- | --- |
| Preferences (all pages listed, General subpages, Redefine Keys) | captured, shots 21 to 31 |
| Preferences: Text Editors, Asset Browser, Room Editor, Sprite Editor, Languages | captured, shots 128 to 132; Code Editor 2 manual |
| Game Options: General, Windows (General, Graphics) | captured, shots 76 to 78 |
| Game Options: Template Info, Windows Images and Installer | captured, shots 125 to 127; other platforms manual |
| Search & Replace | captured, shots 82 to 84 |
| Scripting preference dialog | captured, shot 44 |
| Close workspace confirmation | captured, shot 62 |

## Running and debugging

| Item | Status |
| --- | --- |
| Debugger layout, Windows menu, Instances pane, pause | captured, shots 85 to 88 |
| Debugger Others and Graphics tabs, Profile, Surfaces & Textures, Buffers | manual (tabs did not switch from synthetic input) |
