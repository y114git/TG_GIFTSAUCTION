# UndertaleModTool: feature parity checklist for G3MStudio

Research date: 2026-10-25.

## Versions researched

| Item | Value | Date | Source |
|---|---|---|---|
| Latest stable release | 0.9.2.0 (tag `0.9.2.0`, tag commit `03c7048438347799e0717b6e57a9358d000472e0`, a merge into branch `stable-0.9`) | 2026-08-23 16:27 UTC | https://github.com/UnderminersTeam/UndertaleModTool/releases/tag/0.9.2.0 |
| Default branch head (`master`) | `f43e12c445c37d50dc6244caa12ccab232983f3f` "Bump version to 0.9.2.0" | 2026-08-23 16:01 UTC | https://github.com/UnderminersTeam/UndertaleModTool/commit/f43e12c445c37d50dc6244caa12ccab232983f3f |
| Nightly pre-release | tag `nightly`, points at the same `master` head | 2026-08-23 16:05 UTC | https://github.com/UnderminersTeam/UndertaleModTool/releases/tag/nightly |

The `master` head and the release have the same content. GitHub reports `master` as 8 commits behind the tag, and those 8 are the merge commits on `stable-0.9`. Nothing newer than the release exists on `master`, so the bleeding edge equals 0.9.2.0.

Source read: a shallow clone of `master` at `f43e12c`. Nothing from it was built or run. Paths below are relative to the repository root.

Other sources:
- 0.9.0.0 release notes (project system added, profile mode removed): https://github.com/UnderminersTeam/UndertaleModTool/releases/tag/0.9.0.0
- 0.9.1.0 release notes (CLI project build, CLI sprite dump, shader project support): https://github.com/UnderminersTeam/UndertaleModTool/releases/tag/0.9.1.0
- Bundled script descriptions: https://github.com/UnderminersTeam/UndertaleModTool/blob/master/SCRIPTS.md
- Issue tracker (340 open issues and PRs at research time): https://github.com/UnderminersTeam/UndertaleModTool/issues

Status key: planned, in progress, implemented, verified, not applicable (with a reason). Update the Status column as work lands.

Priority key (work order; the 1.0.0-beta release needs every item, see requirement F-PAR-1): P0 first, P1 next, P2 last. Priorities are first guesses for planning, not decisions.

## 1. Main window shell (APP)

| ID | Feature | Details | UTMT source location | Priority | Status |
|---|---|---|---|---|---|
| UTMT-APP-01 | Menu bar | Top menus File, Scripts, Find, Project, Help. Items disable when no data is loaded or `CanSave` is false. | UndertaleModTool/MainWindow.xaml | P0 | planned |
| UTMT-APP-02 | Resource tree | Root node "Data" with General info, Global init, Game End scripts, Audio groups, Sounds, Sprites, Backgrounds & Tile sets, Paths, Scripts, Shaders, Fonts, Timelines, Game objects, Rooms, Extensions, Texture page items, Code, Variables, Functions, Code locals, Strings, Embedded textures, Embedded audio, Texture group information, Embedded images, Particle systems, Particle system emitters. Nodes hide when the chunk is absent. | UndertaleModTool/MainWindow.xaml, UndertaleModTool/Controls/ResourceListTreeViewItem.xaml | P0 | planned |
| UTMT-APP-03 | Tree name filter | Text box "Filter by name..." above the tree filters every list live. | UndertaleModTool/MainWindow.xaml (SearchBox), UndertaleModTool/Converters/FilteredViewConverter.cs | P0 | planned |
| UTMT-APP-04 | Back and forward buttons | Per-tab navigation history of opened assets. Buttons disable at the ends of the history. | UndertaleModTool/MainWindow.xaml.cs (GoBack, GoForward), UndertaleModTool/Tab.cs | P1 | planned |
| UTMT-APP-05 | C# command box | Text box at the bottom. Enter runs the text as a C# script with `IScriptInterface` as globals and shows the result in the box. Shift+Enter inserts a newline. | UndertaleModTool/MainWindow.xaml.cs (CommandBox_PreviewKeyDown) | P2 | planned |
| UTMT-APP-06 | Asset ID label | Status label at bottom right shows the index of the open asset, starting from 0. | UndertaleModTool/MainWindow.xaml.cs (UpdateObjectLabel) | P0 | planned |
| UTMT-APP-07 | Welcome and description view | Heading plus text shown when nothing is open or after creating a new file. | UndertaleModTool/MainWindow.xaml.cs (DescriptionView) | P2 | planned |
| UTMT-APP-08 | Modal loader dialog | Progress dialog with message, status text and determinate or indeterminate bar during load, save, offset map and scripts. Can block closing. | UndertaleModTool/Windows/LoaderDialog.xaml | P0 | planned |
| UTMT-APP-09 | Dark mode | Custom dark variants of buttons, combo boxes, menus, data grids, tabs and text boxes, plus dark title bars via DWM. | UndertaleModTool/Controls/System/*.cs, UndertaleModTool/MainWindow.xaml.cs (SetDarkMode) | P0 | planned |
| UTMT-APP-10 | Window placement memory | Restores main window position and size on start when enabled. | UndertaleModTool/WindowExtensions.cs, UndertaleModTool/Settings.cs | P2 | planned |
| UTMT-APP-11 | Help menu | "GitHub" opens the repository page. "About" shows the version and edition string. | UndertaleModTool/MainWindow.xaml.cs (MenuItem_GitHub_Click, MenuItem_About_Click) | P2 | planned |
| UTMT-APP-12 | Clickable results window | Separate window that lists code search results as links. Context menu: Open in new tab, Copy, Copy all. Used by scripts through `ClickableSearchOutput`. | UndertaleModTool/Windows/ClickableTextOutput.xaml | P1 | planned |
| UTMT-APP-13 | Text input dialogs | Single-line and multiline input dialogs with custom button captions, used by the Add menu and scripts. | UndertaleModTool/Windows/TextInputDialog.xaml, UndertaleModTool/Windows/TextInput.cs | P0 | planned |
| UTMT-APP-14 | Crash logging | Unhandled exceptions are written to `crash.txt`, `crash2.txt` or `crash3.txt` next to the executable and logged with log4net. | UndertaleModTool/Program.cs | P1 | planned |
| UTMT-APP-15 | Message boxes | Shared question, warning and error dialogs that follow dark mode. | UndertaleModTool/WindowExtensions.cs | P0 | planned |

Count: 15.

## 2. File operations (FILE)

| ID | Feature | Details | UTMT source location | Priority | Status |
|---|---|---|---|---|---|
| UTMT-FILE-01 | New data file | Ctrl+N. Creates an empty `UndertaleData`, asks first if a file is open. | UndertaleModTool/MainWindow.xaml.cs (MakeNewDataFile) | P2 | planned |
| UTMT-FILE-02 | Open data file | Ctrl+O. Filter: `.win`, `.unx`, `.ios`, `.droid`, `audiogroup*.dat`, all files. Loads on a background task with progress. | UndertaleModTool/MainWindow.xaml.cs (DoOpenDialog, LoadFile) | P0 | planned |
| UTMT-FILE-03 | Save data file | Ctrl+S, save dialog. Writes to `<file>temp`, then moves over the target only on success, else deletes the temp file. Commits pending code editor changes first. Compacts the large object heap afterwards. | UndertaleModTool/MainWindow.xaml.cs (DoSaveDialog, SaveFile, SaveCodeChanges) | P0 | planned |
| UTMT-FILE-04 | Load warnings | Shows each serializer warning, then a summary warning that saving may lose data. | UndertaleModTool/MainWindow.xaml.cs (LoadFile) | P0 | planned |
| UTMT-FILE-05 | Unsupported bytecode handling | Bytecode versions below 13 or above 17 load with code disabled and saving refused. | UndertaleModLib/Models/UndertaleGeneralInfo.cs, UndertaleModTool/MainWindow.xaml.cs | P0 | planned |
| UTMT-FILE-06 | YYC detection | Warns that code lives in the executable for YoYo Compiler games. Code search refuses to run. | UndertaleModTool/MainWindow.xaml.cs, UndertaleModTool/Windows/SearchInCodeWindow.xaml.cs | P0 | planned |
| UTMT-FILE-07 | Debugger flag warnings | On load and on save, warns when the GMS debugger flag is set, because the normal runtime hangs. | UndertaleModTool/MainWindow.xaml.cs | P1 | planned |
| UTMT-FILE-08 | Unknown texture format warning | Warns when textures use unknown formats. They round-trip but do not display. | UndertaleModTool/MainWindow.xaml.cs | P1 | planned |
| UTMT-FILE-09 | Close and unsaved-changes prompt | Ctrl+Q closes. With "Warn about saving before closing", asks to save. Closes standalone windows and child instances. | UndertaleModTool/MainWindow.xaml.cs (DataWindow_Closing, CloseOtherWindows) | P0 | planned |
| UTMT-FILE-10 | Generate offset map | File menu. Reads a chosen data file and writes `<file>.offsetmap.txt` listing hex offset and object for each serialized item. | UndertaleModTool/MainWindow.xaml.cs (MenuItem_OffsetMap_Click), UndertaleModLib/UndertaleIO.cs | P2 | planned |
| UTMT-FILE-11 | Audio group child instances | Opening an embedded audio entry stored in an external `audiogroupN.dat` starts a second UTMT process on that file, linked by a named pipe, and focuses the entry there. | UndertaleModTool/MainWindow.xaml.cs (OpenChildFile, ListenChildConnection), UndertaleModTool/Controls/AudioFileReference.xaml.cs | P1 | planned |
| UTMT-FILE-12 | Command-line arguments | `UndertaleModTool <file>` opens a file. A second argument `launch` or `special_launch` runs the game and exits. `deleteTempFolder` cleans up after the updater. | UndertaleModTool/MainWindow.xaml.cs (Window_Loaded) | P1 | planned |
| UTMT-FILE-13 | Drag and drop onto window | Dropping a `.csx` asks to run it as a script. Dropping a data file or `.dat` asks to open it. | UndertaleModTool/MainWindow.xaml.cs (Window_Drop) | P1 | planned |
| UTMT-FILE-14 | Path confinement | Paths derived from data (game exe, child files, script output) are checked to stay inside the expected folder. | UndertaleModLib/Util/Paths.cs | P0 | planned |

Count: 14.

## 3. Resource tree actions (TREE)

| ID | Feature | Details | UTMT source location | Priority | Status |
|---|---|---|---|---|---|
| UTMT-TREE-01 | Open asset | Single selection shows the asset. Double-click or Enter opens it in the current tab. | UndertaleModTool/MainWindow.xaml.cs (MainTree_MouseDoubleClick, MainTree_KeyUp) | P0 | planned |
| UTMT-TREE-02 | Open in new tab | Middle-click or context menu "Open in new tab". | UndertaleModTool/MainWindow.xaml.cs (MainTree_MouseDown) | P0 | planned |
| UTMT-TREE-03 | Add asset | Category context menu "Add". Prompts for a name, validates it as an identifier, offers an auto name on failure. | UndertaleModTool/MainWindow.xaml.cs (MenuItem_Add_Click) | P0 | planned |
| UTMT-TREE-04 | Type-specific defaults on add | Rooms get GMS2, 2.3 or 2024.13 flags and an optional room-order entry. Scripts get a code entry (`gml_GlobalScript_` plus a global init entry in 2.3+, else `gml_Script_`). Code gets a code locals entry. Extensions get folder, class, version and a product ID. Shaders get six empty sources. Page items, embedded audio and textures get synthetic names. | UndertaleModTool/MainWindow.xaml.cs (MenuItem_Add_Click) | P0 | planned |
| UTMT-TREE-05 | Item context menu | Open in new tab, Find all references, Copy name to clipboard, Delete. | UndertaleModTool/Controls/ResourceListTreeViewItem.xaml | P0 | planned |
| UTMT-TREE-06 | Delete asset | Delete key or menu. Warns that IDs of later items shift. Closes affected tabs and prunes tab and closed-tab history. Unmarks the asset in a project. | UndertaleModTool/MainWindow.xaml.cs (DeleteItem) | P0 | planned |
| UTMT-TREE-07 | Copy name | Copies the asset name, or the display title of a string. | UndertaleModTool/MainWindow.xaml.cs (CopyItemName) | P1 | planned |
| UTMT-TREE-08 | Reorder by drag | Dragging an asset onto another of the same type swaps their positions. Off by default. | UndertaleModTool/MainWindow.xaml.cs (TreeView_Drop) | P2 | planned |
| UTMT-TREE-09 | Drag asset as reference | Dragging from the tree into reference fields or the room canvas links or places the asset. | UndertaleModTool/MainWindow.xaml.cs (TreeView_MouseMove), UndertaleModTool/Controls/UndertaleObjectReference.xaml.cs | P0 | planned |
| UTMT-TREE-10 | Version-aware labels | Backgrounds node is labeled "Tile sets" for GMS2 games. | UndertaleModTool/MainWindow.xaml.cs | P2 | planned |
| UTMT-TREE-11 | Null entries | Optional display of null list entries, which GM 2024.11+ games contain. | UndertaleModTool/Settings.cs (ShowNullEntriesInResourceTree) | P1 | planned |

Count: 11.

## 4. Tabs (TAB)

| ID | Feature | Details | UTMT source location | Priority | Status |
|---|---|---|---|---|---|
| UTMT-TAB-01 | Tab strip | One tab per open editor, close button per tab, title from asset name and type. | UndertaleModTool/MainWindow.xaml, UndertaleModTool/Tab.cs (TabTitleConverter) | P0 | planned |
| UTMT-TAB-02 | Close tab | Ctrl+W, close button, middle-click, context menu "Close". | UndertaleModTool/MainWindow.xaml.cs (CloseTab, TabItem_MouseUp) | P0 | planned |
| UTMT-TAB-03 | Close other and all tabs | Context menu "Close other tabs", "Close all tabs" (Ctrl+Shift+W). | UndertaleModTool/MainWindow.xaml.cs | P1 | planned |
| UTMT-TAB-04 | Restore closed tab | Ctrl+Shift+T reopens from closed-tab history. | UndertaleModTool/MainWindow.xaml.cs (Command_RestoreClosedTab) | P1 | planned |
| UTMT-TAB-05 | Switch tabs | Ctrl+Tab and Ctrl+Shift+Tab. | UndertaleModTool/MainWindow.xaml.cs | P0 | planned |
| UTMT-TAB-06 | Reorder tabs by drag | Drag a tab onto another to move it. | UndertaleModTool/MainWindow.xaml.cs (TabItem_PreviewMouseMove, TabItem_Drop) | P1 | planned |
| UTMT-TAB-07 | Tab overflow scrolling | Mouse wheel and left and right buttons scroll the strip. Selecting scrolls the tab into view. | UndertaleModTool/MainWindow.xaml.cs (ScrollTabs, ScrollToTab) | P1 | planned |
| UTMT-TAB-08 | Per-tab state restore | Saves and restores view state when switching: code (caret, scroll, zoom, active sub-tab), room, font, general info, global init, sprite, tile set, texture group, texture page. | UndertaleModTool/Tab.cs (TabContentState subclasses) | P1 | planned |

Count: 8.

## 5. Running the game (RUN)

| ID | Feature | Details | UTMT source location | Priority | Status |
|---|---|---|---|---|---|
| UTMT-RUN-01 | Temp run | F5. Saves to `mod_temprun.temp` beside the data file with Steam AppID 0 and debugger off, then starts `<GeneralInfo.FileName>.exe -game <temp>`. Shows a one-time warning that changes are not saved. In project mode saves to the project output path instead. | UndertaleModTool/MainWindow.xaml.cs (Command_Run) | P0 | planned |
| UTMT-RUN-02 | Run with other runner | Alt+F5. Offers to disable the debugger flag, saves, opens the runtime picker, starts the runner with `-game` and `-debugoutput <file>.gamelog.txt`. | UndertaleModTool/MainWindow.xaml.cs (Command_RunSpecial) | P1 | planned |
| UTMT-RUN-03 | Runtime picker | Lists the game exe, GMS 1.4 `Runner.exe` from the GMS1 path, and every GMS2 runtime in the runtimes cache (prefers `windows\x64\Runner.exe`). Columns Version and Path. | UndertaleModTool/Windows/RuntimePicker.xaml.cs | P1 | planned |
| UTMT-RUN-04 | Run under GMS debugger | Shift+F5, hidden unless enabled in settings. Sets the debugger flag, saves, writes a stub `.gmx`, starts the runner and `GMDebug.exe` against the `.yydebug` file and the debugger port. | UndertaleModTool/MainWindow.xaml.cs (Command_RunDebug) | P2 | planned |
| UTMT-RUN-05 | Debug data generation | Code path writes `.yydebug` with decompiled source or disassembly and breakpoint offsets. The mode dialog exists but the save path currently fixes the mode to "no debug". | UndertaleModTool/MainWindow.xaml.cs (SaveFile), UndertaleModTool/Windows/DebugDataDialog.xaml | P2 | planned |
| UTMT-RUN-06 | Shell verbs | File association adds "Run game normally" (`launch`) and "Run extended options" (`special_launch`, runtime picker) to the Explorer context menu. | UndertaleModTool/FileAssociations.cs | P2 | planned |

Count: 6.

## 6. Settings (SET)

| ID | Feature | Details | UTMT source location | Priority | Status |
|---|---|---|---|---|---|
| UTMT-SET-01 | Settings storage | JSON at `%AppData%\UndertaleModTool\settings.json`, loaded at window show. | UndertaleModTool/Settings.cs | P0 | planned |
| UTMT-SET-02 | GameMaker paths | GMS 1.4 folder (default `%appdata%\GameMaker-Studio`) and GMS2 runtimes folder (default `%ProgramData%\GameMakerStudio2\Cache\runtimes`). | UndertaleModTool/Windows/SettingsWindow.xaml | P1 | planned |
| UTMT-SET-03 | Asset order swapping | Enables tree drag reorder. Default off. | UndertaleModTool/Windows/SettingsWindow.xaml | P2 | planned |
| UTMT-SET-04 | File association toggle | "Automatically associate GameMaker data files". Default on. | UndertaleModTool/Windows/SettingsWindow.xaml | P2 | planned |
| UTMT-SET-05 | Debugger menu toggle | Shows "Run game under GMS debugger". Default off. | UndertaleModTool/Windows/SettingsWindow.xaml | P2 | planned |
| UTMT-SET-06 | Remember window placement | Default off. | UndertaleModTool/Windows/SettingsWindow.xaml | P2 | planned |
| UTMT-SET-07 | Warn on close | Default on. | UndertaleModTool/Windows/SettingsWindow.xaml | P0 | planned |
| UTMT-SET-08 | Warn about temp running | Default on. | UndertaleModTool/Windows/SettingsWindow.xaml | P1 | planned |
| UTMT-SET-09 | Show null entries in tree | Default off. | UndertaleModTool/Windows/SettingsWindow.xaml | P1 | planned |
| UTMT-SET-10 | Recompile all GML on project save | Recompiles every project code source before writing the data file. Default off. | UndertaleModTool/Windows/SettingsWindow.xaml, UndertaleModLib/Project/ProjectContext.cs | P1 | planned |
| UTMT-SET-11 | Dark mode toggle | Default off. | UndertaleModTool/Windows/SettingsWindow.xaml | P0 | planned |
| UTMT-SET-12 | Transparency grid colors | Two ARGB hex colors for the checkerboard behind images. | UndertaleModTool/Windows/SettingsWindow.xaml, UndertaleModTool/Controls/TransparencyGridBrush.xaml | P2 | planned |
| UTMT-SET-13 | Global room grid override | Optional global grid width, height and thickness that override per-room grid values. | UndertaleModTool/Windows/SettingsWindow.xaml | P2 | planned |
| UTMT-SET-14 | Open app data folder | Button opens `%AppData%\UndertaleModTool`. | UndertaleModTool/Windows/SettingsWindow.xaml.cs (AppDataButton_Click) | P2 | planned |
| UTMT-SET-15 | Update app button | Visible only in debug (nightly) builds. See UTMT-PLAT-03. | UndertaleModTool/Windows/SettingsWindow.xaml.cs | P2 | planned |
| UTMT-SET-16 | GML settings button | Opens the compiler and decompiler settings window. | UndertaleModTool/Windows/SettingsWindow.xaml.cs | P0 | planned |

Count: 16.

## 7. GML compiler and decompiler settings (GMLS)

| ID | Feature | Details | UTMT source location | Priority | Status |
|---|---|---|---|---|---|
| UTMT-GMLS-01 | Indentation style | Choice of indent kind for decompiled output. | UndertaleModTool/Windows/GMLSettingsWindow.xaml, UndertaleModTool/Settings.cs | P1 | planned |
| UTMT-GMLS-02 | Formatting toggles | Always use semicolons, CSS hex color literals, print warnings, enum and macro declarations at top, open block braces on same line, remove braces of single-line blocks. | UndertaleModTool/Windows/GMLSettingsWindow.xaml | P1 | planned |
| UTMT-GMLS-03 | Empty line rules | After local declarations, around enums, around branch statements, before and after switch cases, around function declarations, around static initialization. | UndertaleModTool/Windows/GMLSettingsWindow.xaml | P2 | planned |
| UTMT-GMLS-04 | Cleanup passes | Try/catch/finally rewriting, else into continue, default argument values, builtin array variable syntax, scoped local declarations, template strings. | UndertaleModTool/Windows/GMLSettingsWindow.xaml | P1 | planned |
| UTMT-GMLS-05 | Enum generation | "Create enum declarations", unknown enum name and value naming patterns. | UndertaleModTool/Windows/GMLSettingsWindow.xaml | P1 | planned |
| UTMT-GMLS-06 | Unknown argument naming | Pattern for unnamed script arguments. | UndertaleModTool/Settings.cs (UnknownArgumentNamePattern) | P1 | planned |
| UTMT-GMLS-07 | Allow leftover VM stack data | Lets the decompiler continue on unbalanced stacks. | UndertaleModTool/Windows/GMLSettingsWindow.xaml | P2 | planned |
| UTMT-GMLS-08 | Named instance ID prefix | Prefix used for room instance IDs in GML (shared by compiler and decompiler). | UndertaleModTool/Windows/GMLSettingsWindow.xaml, UndertaleModTool/Settings.cs | P1 | planned |

Count: 8.

## 8. Search and references (SRCH)

| ID | Feature | Details | UTMT source location | Priority | Status |
|---|---|---|---|---|---|
| UTMT-SRCH-01 | Search in code | Find menu or Ctrl+Shift+F. Options: case sensitive, regex, multiline regex, search in assembly, filter by code name (with its own case and regex options). Parallel over all code entries. Uses project GML sources when present. Results grid Code, Line, Text. | UndertaleModTool/Windows/SearchInCodeWindow.xaml(.cs) | P0 | planned |
| UTMT-SRCH-02 | Search results actions | Open (jumps to line), Open in new tab, Copy, Copy all, Enter to open. Blocks a second search while one runs. | UndertaleModTool/Windows/SearchInCodeWindow.xaml.cs | P0 | planned |
| UTMT-SRCH-03 | Find all references | From tree, reference fields and some editors. A dialog picks the reference types to scan (Select all, Deselect all). Results tree with name filter, Open in new tab, Copy name, nested Find all references, Export results to `references_of_asset_<name>.txt`. | UndertaleModTool/Windows/FindReferencesTypesDialog/*.cs | P0 | planned |
| UTMT-SRCH-04 | Find unreferenced assets | Find menu. Same type dialog, results grouped by type, export to `unreferenced_assets.txt`. | UndertaleModTool/MainWindow.xaml.cs (MenuItem_FindUnreferencedAssets_Click), UndertaleModTool/Windows/FindReferencesTypesDialog/UndertaleResourceReferenceMethodsMap.cs | P1 | planned |
| UTMT-SRCH-05 | Reference coverage | Searches sprites, backgrounds and tiles, fonts (incl. in texture groups), sounds, audio groups, paths, scripts, code, shaders, timelines, objects (parent, sprite, mask, events), rooms (instances, tiles, layers, sprite, sequence and particle instances, backgrounds, effect properties, creation and pre-create code), variables, functions, code locals, strings, texture page items, embedded textures, embedded images, embedded audio, texture groups, particle systems and emitters, sequences (tracks, keyframes, moments, broadcast messages), options constants. Version-dependent. | UndertaleModTool/Windows/FindReferencesTypesDialog/UndertaleResourceReferenceMap.cs | P1 | planned |
| UTMT-SRCH-06 | Show children objects | Object editor button lists objects whose parent is the current object (added in 0.9.2.0). | UndertaleModTool/Editors/UndertaleGameObjectEditor.xaml.cs (ShowChildrenButton_Click) | P1 | planned |
| UTMT-SRCH-07 | Tile references | Tile set editor context action finds rooms and layers that use a tile ID. | UndertaleModTool/Editors/UndertaleBackgroundEditor.xaml.cs (FindAllTileReferencesItem_Click) | P2 | planned |

Count: 7.

## 9. Project system (PROJ)

Profile mode was removed in 0.9.0.0. The project system replaced it, so profile mode needs no parity.

| ID | Feature | Details | UTMT source location | Priority | Status |
|---|---|---|---|---|---|
| UTMT-PROJ-01 | New project | Project menu. Creates a project JSON for the loaded data file. | UndertaleModTool/MainWindow.xaml.cs (Command_NewProject) | P1 | planned |
| UTMT-PROJ-02 | Open project | Picks a project `.json`, loads the base data file, runs the import steps, applies assets. | UndertaleModTool/MainWindow.xaml.cs (Command_OpenProject), UndertaleModLib/Project/ProjectContextImportSteps.cs | P1 | planned |
| UTMT-PROJ-03 | Save project | Exports marked assets to the project folder and writes the manifest. | UndertaleModTool/MainWindow.xaml.cs (Command_SaveProject), UndertaleModLib/Project/ProjectContextIO.cs | P1 | planned |
| UTMT-PROJ-04 | View unexported assets | Window lists assets marked but not yet exported. Actions: Open, Open in new tab, Unmark for export, Copy, Copy all. | UndertaleModTool/Windows/ProjectAssetsWindow.xaml | P1 | planned |
| UTMT-PROJ-05 | Close project | Unloads the project context. | UndertaleModTool/MainWindow.xaml.cs (Command_CloseProject) | P1 | planned |
| UTMT-PROJ-06 | Automatic change tracking | Editors mark an asset for export when edited. Status bar checkbox "Marked for export" toggles it by hand. | UndertaleModTool/Controls/DataUserControl.cs, UndertaleModTool/MainWindow.xaml.cs (MarkedForExport_CheckBoxChanged) | P1 | planned |
| UTMT-PROJ-07 | Serializable asset types | Animation curves, backgrounds, code (GML text), fonts, game objects, general info options, paths, rooms, scripts, sequences, shaders, sounds, sprites. | UndertaleModLib/Project/SerializableAssets/*.cs | P1 | planned |
| UTMT-PROJ-08 | GML source retention | Code is kept as plain GML files, so comments and formatting survive. Editors and search read the source from the project. | UndertaleModLib/Project/ProjectContextAssets.cs (TryGetCodeSource, UpdateCodeSource) | P1 | planned |
| UTMT-PROJ-09 | Project options file | Name, flags, verify strings, assets data file path, error on warnings, excluded folders, sub-projects, patches, external files, file copies, file deletes, pre-import, pre-asset-import and post-import scripts. | UndertaleModLib/Project/ProjectMainOptions.cs | P1 | planned |
| UTMT-PROJ-10 | BPS patches | Applies BPS patch files to data files during project import. | UndertaleModLib/Project/ProjectContextImportSteps.cs, UndertaleModLib/Util/BPS.cs | P2 | planned |
| UTMT-PROJ-11 | Project scripts | Runs `.csx` scripts at defined import stages with a project-aware script interface. | UndertaleModLib/Project/ProjectContextScripts.cs, UndertaleModLib/Project/ProjectContextScriptInterface.cs | P2 | planned |
| UTMT-PROJ-12 | Game file backup | Backs up and restores game files the project overwrites or deletes. | UndertaleModLib/Project/GameFileBackup.cs | P1 | planned |
| UTMT-PROJ-13 | Streamed sound copy | Copies external streamed sounds to the save directory. | UndertaleModLib/Project/ProjectContext.cs (CopyStreamedSoundToSaveDirectory) | P2 | planned |

Count: 13.

## 10. Code editor (CODE)

| ID | Feature | Details | UTMT source location | Priority | Status |
|---|---|---|---|---|---|
| UTMT-CODE-01 | Code entry properties | Name, locals count, arguments count, bytecode offset, parent entry (for GMS 2.3 child functions). | UndertaleModTool/Editors/UndertaleCodeEditor.xaml | P0 | planned |
| UTMT-CODE-02 | Decompiled view | "Decompiled" tab shows GML from the Underanalyzer decompiler with the current settings. Errors are shown as a comment block. | UndertaleModTool/Editors/UndertaleCodeEditor.xaml.cs (DecompileCode) | P0 | planned |
| UTMT-CODE-03 | GML compile | Compiles on focus loss or Ctrl+K and replaces the bytecode. Compile errors are shown to the user. | UndertaleModTool/Editors/UndertaleCodeEditor.xaml.cs (DecompiledLostFocusBody, Command_Compile) | P0 | planned |
| UTMT-CODE-04 | Disassembly view | "Disassembly" tab shows VM assembly with highlighting and the code locals. | UndertaleModTool/Editors/UndertaleCodeEditor.xaml.cs (DisassembleCode) | P0 | planned |
| UTMT-CODE-05 | Assemble | Assembles edited disassembly on focus loss or Ctrl+K. | UndertaleModTool/Editors/UndertaleCodeEditor.xaml.cs (DisassemblyEditor_LostFocus) | P1 | planned |
| UTMT-CODE-06 | Syntax highlighting | AvalonEdit definitions for GML and VM assembly. | UndertaleModTool/Resources/GML.xshd, UndertaleModTool/Resources/VMASM.xshd | P0 | planned |
| UTMT-CODE-07 | Semantic coloring | Identifiers colored by kind: functions (orange, bold), globals, constants, instance variables, locals. | UndertaleModTool/Editors/UndertaleCodeEditor.xaml.cs (NameGenerator) | P1 | planned |
| UTMT-CODE-08 | Go to definition | Ctrl+click on a script, function, asset or GMS 2.3 sub-function name opens it. Middle-click opens in a new tab. | UndertaleModTool/Editors/UndertaleCodeEditor.xaml.cs (ClickVisualLineText) | P0 | planned |
| UTMT-CODE-09 | Number resolution menu | Right-click a number to list assets with that index and matching constants, then open one. | UndertaleModTool/Editors/UndertaleCodeEditor.xaml.cs (NumberGenerator) | P1 | planned |
| UTMT-CODE-10 | Find and replace | AvalonEdit search panel in both views (Ctrl+F), with a "no matches" hint. | UndertaleModTool/Editors/UndertaleCodeEditor.xaml.cs (SearchPanel) | P0 | planned |
| UTMT-CODE-11 | Zoom | Ctrl+mouse wheel and Ctrl+plus or minus. Font size carries over to the next editor. | UndertaleModTool/Editors/UndertaleCodeEditor.xaml.cs (ZoomChange) | P1 | planned |
| UTMT-CODE-12 | Text comments for Undertale and Deltarune | Adds the resolved English text as a comment after `scr_gettext` and `scr_84_get_lang_string` calls, from `gml_Script_textdata_en` or `lang/lang_en.json`. | UndertaleModTool/Editors/UndertaleCodeEditor.xaml.cs (UpdateGettext, UpdateGettextJSON) | P2 | planned |
| UTMT-CODE-13 | Open at line | Opening from search jumps to the line. Caret and scroll restore per tab. | UndertaleModTool/MainWindow.xaml.cs (OpenCodeEntry), UndertaleModTool/Editors/UndertaleCodeEditor.xaml.cs (RestoreState) | P0 | planned |
| UTMT-CODE-14 | Script asset editor | Name and linked code entry. | UndertaleModTool/Editors/UndertaleScriptEditor.xaml | P0 | planned |
| UTMT-CODE-15 | Modern GML syntax | Template strings `$"{x}"`, nullish compound assignment (2026.1), `variable_get_hash()` folding, built-in function list filtered by GameMaker version. | UndertaleModLib/Compiler/*.cs, Underanalyzer, 0.9.2.0 release notes | P0 | planned |
| UTMT-CODE-16 | Game-specific definitions | JSON definitions for generic GameMaker, Undertale and Deltarune resolve macros, enums and asset types in decompiled code (incl. the AudioGroup macro type). Detection can match by name regex. | UndertaleModLib/GameSpecificData/** | P1 | planned |
| UTMT-CODE-17 | Code import groups | Library API used by scripts to import many GML files at once, create missing assets, resolve cross references and collision event names. | UndertaleModLib/Compiler/CodeImportGroup.cs | P0 | planned |

Count: 17.

## 11. Sprites (SPR)

| ID | Feature | Details | UTMT source location | Priority | Status |
|---|---|---|---|---|---|
| UTMT-SPR-01 | Sprite properties | Name, width, height, margins left, right, top, bottom, transparent, smooth, preload, bounding box mode, separate masks mode, origin X and Y. | UndertaleModTool/Editors/UndertaleSpriteEditor.xaml | P0 | planned |
| UTMT-SPR-02 | Frame list | Texture page item per frame, double-click to add, drag a page item onto a slot, remove, preview. | UndertaleModTool/Editors/UndertaleSpriteEditor.xaml(.cs) | P0 | planned |
| UTMT-SPR-03 | Collision masks | Mask list with remove, "Import mask" and "Export mask" as PNG, "Invalid mask data!" marker. | UndertaleModTool/Editors/UndertaleSpriteEditor.xaml.cs (MaskImport_Click, MaskExport_Click) | P1 | planned |
| UTMT-SPR-04 | GMS2 special type | "Is special type?", version, type (normal, SWF, Spine), playback speed and speed type. | UndertaleModTool/Editors/UndertaleSpriteEditor.xaml | P1 | planned |
| UTMT-SPR-05 | Export all frames | Exports all frames as PNG, or the Spine JSON, atlas and textures for Spine sprites. | UndertaleModTool/Editors/UndertaleSpriteEditor.xaml.cs (ExportAll_Click, ExportAllSpine) | P0 | planned |

Count: 5.

## 12. Backgrounds and tile sets (BG)

| ID | Feature | Details | UTMT source location | Priority | Status |
|---|---|---|---|---|---|
| UTMT-BG-01 | Background properties | Name, transparent, smooth, preload, texture page item. | UndertaleModTool/Editors/UndertaleBackgroundEditor.xaml | P0 | planned |
| UTMT-BG-02 | GMS2 tile set properties | Tile set version, tile width and height, output border X and Y, tile columns, items per tile, tile count, frame time, exported sprite (GM 2023.8+). | UndertaleModTool/Editors/UndertaleBackgroundEditor.xaml | P0 | planned |
| UTMT-BG-03 | Tile ID table | Editable tile ID grid, double-click to add. Selecting a row highlights the tile on the texture and scrolls to it. | UndertaleModTool/Editors/UndertaleBackgroundEditor.xaml.cs (SelectTileRegion, ScrollTileIntoView) | P1 | planned |
| UTMT-BG-04 | Pick tile on texture | Clicking the texture selects the matching tile ID. Right-click offers tile reference search. | UndertaleModTool/Editors/UndertaleBackgroundEditor.xaml.cs (BGTexture_MouseLeftButtonDown) | P2 | planned |
| UTMT-BG-05 | Tile separation display | Shows tile separation X and Y derived from the borders. | UndertaleModTool/Editors/UndertaleBackgroundEditor.xaml | P2 | planned |

Count: 5.

## 13. Textures (TEX)

| ID | Feature | Details | UTMT source location | Priority | Status |
|---|---|---|---|---|---|
| UTMT-TEX-01 | Texture page item editor | Source X, Y, width, height; target X, Y, width, height; bounding width and height; texture page reference; preview of the item and of its place on the page. | UndertaleModTool/Editors/UndertaleTexturePageItemEditor.xaml | P0 | planned |
| UTMT-TEX-02 | Page item import and export | "Import" replaces the item image from PNG, "Export" writes PNG. "Find all references to this page item". | UndertaleModTool/Editors/UndertaleTexturePageItemEditor.xaml.cs | P0 | planned |
| UTMT-TEX-03 | Embedded texture editor | Shows the texture page with size, "Scaled" and "Generated mips" fields, load-failure warning, zoom and scroll, fit to view. | UndertaleModTool/Editors/UndertaleEmbeddedTextureEditor.xaml(.cs) | P0 | planned |
| UTMT-TEX-04 | Embedded texture import and export | Replace the whole page from PNG, export the page to PNG. | UndertaleModTool/Editors/UndertaleEmbeddedTextureEditor.xaml.cs | P0 | planned |
| UTMT-TEX-05 | Page item hit test | Hovering a page highlights the page item under the cursor. Context menu: Open in new tab, Find all references. | UndertaleModTool/Editors/UndertaleEmbeddedTextureEditor.xaml.cs (Grid_MouseMove, FindAllItemReferencesItem_Click) | P1 | planned |
| UTMT-TEX-06 | Embedded images | EMBI entries: name and texture page item. | UndertaleModTool/Editors/UndertaleEmbeddedImageEditor.xaml | P2 | planned |
| UTMT-TEX-07 | Texture group info | Group name and lists of texture pages, sprites, Spine sprites, fonts and tile sets (double-click to add). The editor carries a "TODO" note. | UndertaleModTool/Editors/UndertaleTextureGroupInfoEditor.xaml | P1 | planned |
| UTMT-TEX-08 | Texture formats | Decodes PNG, QOI, BZ2-compressed QOI and raw BGRA. DDS and unknown formats round-trip but may not display. | UndertaleModLib/Util/GMImage.cs, UndertaleModLib/Util/QoiConverter.cs | P0 | planned |
| UTMT-TEX-09 | Image cache | Weak-reference bitmap cache per GMImage to avoid repeated decoding. | UndertaleModTool/MainWindow.xaml.cs (GetBitmapSourceForImage), UndertaleModTool/Converters/UndertaleCachedImageLoader.cs | P0 | planned |
| UTMT-TEX-10 | Texture packing helpers | Library packer and texture worker used by import scripts to place new images on pages. | UndertaleModLib/Util/TextureGroupPacker.cs, UndertaleModLib/Util/TextureWorker.cs | P1 | planned |

Count: 10.

## 14. Fonts (FNT)

| ID | Feature | Details | UTMT source location | Priority | Status |
|---|---|---|---|---|---|
| UTMT-FNT-01 | Font properties | Name, display name, font size (em), bold, italic, range start and end, charset, anti-aliasing, texture, scale X and Y, ascender, ascender offset, SDF spread, line height. | UndertaleModTool/Editors/UndertaleFontEditor/UndertaleFontEditor.xaml | P0 | planned |
| UTMT-FNT-02 | Glyph table | Char, source rectangle, shift (green on texture), offset (blue on texture). Click texture to select a glyph, selection scrolls into view. | UndertaleModTool/Editors/UndertaleFontEditor/UndertaleFontEditor.xaml.cs (Grid_MouseDown, ScrollGlyphIntoView) | P0 | planned |
| UTMT-FNT-03 | Glyph rectangle editor | Separate window to drag and resize a glyph rectangle on the texture with zoom, keyboard nudge and a help button. | UndertaleModTool/Editors/UndertaleFontEditor/EditGlyphRectangleWindow.xaml(.cs) | P1 | planned |
| UTMT-FNT-04 | Kerning editor | Per-glyph kerning list (preceding char, additional shift) with back navigation. | UndertaleModTool/Editors/UndertaleFontEditor/UndertaleFontEditor.xaml.cs (EditKerningButton_Click) | P1 | planned |
| UTMT-FNT-05 | Glyph maintenance | "Create an empty glyph", "Sort glyphs", "Update range" from the glyph list. | UndertaleModTool/Editors/UndertaleFontEditor/UndertaleFontEditor.xaml.cs | P1 | planned |

Count: 5.

## 15. Audio (SND)

| ID | Feature | Details | UTMT source location | Priority | Status |
|---|---|---|---|---|---|
| UTMT-SND-01 | Sound properties | Name, flags, type, file, effects, volume, pitch, audio group, audio file reference, preload (old audio system). | UndertaleModTool/Editors/UndertaleSoundEditor.xaml | P0 | planned |
| UTMT-SND-02 | Sound playback | Play and Stop for WAV and OGG through NAudio, including entries in external audio groups. | UndertaleModTool/Editors/UndertaleSoundEditor.xaml.cs | P0 | planned |
| UTMT-SND-03 | Audio file reference control | Shows the embedded audio entry and its group. "..." opens it (in a child instance for external groups), "X" removes it, drag and drop to change. | UndertaleModTool/Controls/AudioFileReference.xaml(.cs) | P0 | planned |
| UTMT-SND-04 | Embedded audio editor | Import and Export (WAV or OGG by header), Play, Stop. | UndertaleModTool/Editors/UndertaleEmbeddedAudioEditor.xaml.cs | P0 | planned |
| UTMT-SND-05 | Audio group editor | Name and path (GM 2024.14+). | UndertaleModTool/Editors/UndertaleAudioGroupEditor.xaml | P1 | planned |
| UTMT-SND-06 | Audio group files | `audiogroupN.dat` files open as data files on their own. | UndertaleModTool/MainWindow.xaml.cs (DataFileFilter) | P0 | planned |

Count: 6.

## 16. Game objects (OBJ)

| ID | Feature | Details | UTMT source location | Priority | Status |
|---|---|---|---|---|---|
| UTMT-OBJ-01 | Object properties | Name, sprite, visible, solid, depth, persistent, parent, texture mask. | UndertaleModTool/Editors/UndertaleGameObjectEditor.xaml | P0 | planned |
| UTMT-OBJ-02 | Physics properties | Uses physics, is sensor, collision shape (circle, box, custom), density, restitution, group, linear and angular damping, friction, awake, kinematic. | UndertaleModTool/Editors/UndertaleGameObjectEditor.xaml | P1 | planned |
| UTMT-OBJ-03 | Physics vertices | Editable X and Y list, vertex count kept in sync. | UndertaleModTool/Editors/UndertaleGameObjectEditor.xaml.cs (PhysicsVertex_ValueUpdated) | P2 | planned |
| UTMT-OBJ-04 | Event list | Event types Create, Destroy, Alarm, Step, Collision, Keyboard, Mouse, Other, Draw, KeyPress, KeyRelease, Trigger, CleanUp, Gesture, PreCreate, each with subtypes (for example Step: Begin, Normal, End; Draw: Draw, GUI, Resize, Begin, End, GUI Begin, GUI End, Pre, Post). Add and remove events. | UndertaleModTool/Editors/UndertaleGameObjectEditor.xaml, UndertaleModLib/Models/UndertaleGameObject.cs | P0 | planned |
| UTMT-OBJ-05 | Event actions | Each event lists actions with a code entry reference. Setting the code renames or links the entry. Opening the code entry from the event. | UndertaleModTool/Editors/UndertaleGameObjectEditor.xaml.cs (UndertaleObjectReference_ObjectReferenceChanged_ActionCode) | P0 | planned |
| UTMT-OBJ-06 | Event subtype pickers | Combo boxes resolve subtype IDs to key names, mouse buttons, other-event names and collision objects. | UndertaleModTool/Editors/UndertaleGameObjectEditor.xaml.cs, UndertaleModTool/Converters/EventNameConverter.cs | P0 | planned |

Count: 6.

## 17. Rooms (ROOM)

| ID | Feature | Details | UTMT source location | Priority | Status |
|---|---|---|---|---|---|
| UTMT-ROOM-01 | Room properties | Name, caption, size, speed, persistent, background color, draw background color, creation code, flags, world, top, left, right, bottom, gravity X and Y, grid width, height and thickness. | UndertaleModTool/Editors/UndertaleRoomEditor/UndertaleRoomEditor.xaml | P0 | planned |
| UTMT-ROOM-02 | Room canvas render | Renders backgrounds, GMS2 layers in depth order, tiles, object sprites, sprite instances, particle system bounds and path layers. Uses a cached sprite image loader. | UndertaleModTool/Controls/UndertaleRoomRenderer.xaml, UndertaleModTool/Controls/TileLayerImage.cs | P0 | planned |
| UTMT-ROOM-03 | Room object tree | Tree of backgrounds, views, game objects, tiles (GMS1) or layers with their contents (GMS2). Selecting an item selects it on the canvas and shows its properties. | UndertaleModTool/Editors/UndertaleRoomEditor/UndertaleRoomEditor.xaml.cs (TreeView_SelectedItemChanged) | P0 | planned |
| UTMT-ROOM-04 | GMS1 backgrounds | Enabled, foreground, definition, position, tiling X and Y, stretch, speed. Delete from context menu. | UndertaleModTool/Editors/UndertaleRoomEditor/UndertaleRoomEditor.xaml | P1 | planned |
| UTMT-ROOM-05 | Views | Enabled, view position and size, port position and size, border, follow object, speed. "Disable view" from context menu. | UndertaleModTool/Editors/UndertaleRoomEditor/UndertaleRoomEditor.xaml | P1 | planned |
| UTMT-ROOM-06 | Instance properties | Instance ID, object, position, scale, color, rotation, frame index, image speed, creation code, pre-create code. | UndertaleModTool/Editors/UndertaleRoomEditor/UndertaleRoomEditor.xaml | P0 | planned |
| UTMT-ROOM-07 | Tile properties | Background or tile set, source region, position, scale, color, depth, instance ID (GMS1 and legacy GMS2 tiles). | UndertaleModTool/Editors/UndertaleRoomEditor/UndertaleRoomEditor.xaml | P1 | planned |
| UTMT-ROOM-08 | Layer properties | Layer name, ID, type, depth, offset, speed, visible. Per type: instances list; tiles (tile set, size, Edit, Export, Import, Auto size); background (sprite, tiling, stretch, color, first frame, animation speed and type); assets (legacy tiles, sprites, sequences, particle systems); effect (name only). | UndertaleModTool/Editors/UndertaleRoomEditor/UndertaleRoomEditor.xaml | P0 | planned |
| UTMT-ROOM-09 | Sprite instances | Sprite, position, scale, color, rotation, frame index, animation speed and type, on assets layers. | UndertaleModTool/Editors/UndertaleRoomEditor/UndertaleRoomEditor.xaml | P1 | planned |
| UTMT-ROOM-10 | Particle system instances | Particle system, position, scale, color, rotation, on assets layers. Random name and new ID on paste. | UndertaleModTool/Editors/UndertaleRoomEditor/UndertaleRoomEditor.xaml.cs | P2 | planned |
| UTMT-ROOM-11 | Add items | Context menus: New object instance, New tile (GMS1), New Instances, Tiles, Background or Assets layer, new GMS2 instance, New legacy tile, New sprite instance. | UndertaleModTool/Editors/UndertaleRoomEditor/UndertaleRoomEditor.xaml.cs (MenuItem_New*) | P0 | planned |
| UTMT-ROOM-12 | Select and drag | Click selects the topmost item. Drag moves it. Movement snaps to the room grid; Ctrl halves the grid, Shift doubles it. | UndertaleModTool/Editors/UndertaleRoomEditor/UndertaleRoomEditor.xaml.cs (GetGridMouseCoordinates, Rectangle_MouseMove) | P0 | planned |
| UTMT-ROOM-13 | Paint copies | Alt+drag paints copies of the selected instance, tile, sprite or particle instance along the grid. | UndertaleModTool/Editors/UndertaleRoomEditor/UndertaleRoomEditor.xaml.cs (PaintObjects) | P1 | planned |
| UTMT-ROOM-14 | Drop from resource tree | Dropping an object creates an instance, a sprite creates a sprite instance, a background fills an empty room background or creates a tile; positions snap to grid. | UndertaleModTool/Editors/UndertaleRoomEditor/UndertaleRoomEditor.xaml.cs (Canvas_Drop) | P0 | planned |
| UTMT-ROOM-15 | Copy and paste | Ctrl+C, Ctrl+V at the mouse with grid snap, Ctrl+Shift+V without snap. Works for instances, tiles, sprite and particle instances. | UndertaleModTool/Editors/UndertaleRoomEditor/UndertaleRoomEditor.xaml.cs (Command_Copy, Paste) | P0 | planned |
| UTMT-ROOM-16 | Undo move | Ctrl+Z restores the position of the last dragged item. No undo for other edits. | UndertaleModTool/Editors/UndertaleRoomEditor/UndertaleRoomEditor.xaml.cs (Command_Undo) | P0 | planned |
| UTMT-ROOM-17 | Delete and reorder | Delete key removes the item. Minus and plus keys or up and down buttons move it in the list (draw order). | UndertaleModTool/Editors/UndertaleRoomEditor/UndertaleRoomEditor.xaml.cs (RoomObjectsTree_KeyDown, MoveItem) | P0 | planned |
| UTMT-ROOM-18 | Flip instance | X and Y keys mirror the selected instance and correct its position by the sprite origin. | UndertaleModTool/Editors/UndertaleRoomEditor/UndertaleRoomEditor.xaml.cs | P2 | planned |
| UTMT-ROOM-19 | Zoom and scroll | Ctrl+mouse wheel zooms the canvas. Scroll position is kept in tab state. | UndertaleModTool/Editors/UndertaleRoomEditor/UndertaleRoomEditor.xaml.cs (Canvas_MouseWheel) | P0 | planned |
| UTMT-ROOM-20 | Grid overlay | Draws the room grid with per-room or global size and thickness. | UndertaleModTool/Converters/GridConverter.cs, UndertaleModTool/Editors/UndertaleRoomEditor/UndertaleRoomEditor.xaml.cs (SetupRoomWithGrids) | P1 | planned |
| UTMT-ROOM-21 | Export room as PNG | "Save as PNG" renders the canvas to a file. | UndertaleModTool/Editors/UndertaleRoomEditor/UndertaleRoomEditor.xaml.cs (ExportAsPNG_Click) | P1 | planned |
| UTMT-ROOM-22 | Tile data CSV | Export and import a tile layer's tile IDs as CSV. | UndertaleModTool/Editors/UndertaleRoomEditor/UndertaleRoomEditor.xaml.cs (TileDataExport_Click, TileDataImport_Click) | P1 | planned |
| UTMT-ROOM-23 | Auto size tile layer | Sets tile layer size to match the room size. | UndertaleModTool/Editors/UndertaleRoomEditor/UndertaleRoomEditor.xaml.cs (AutoSizeTile_Click) | P2 | planned |
| UTMT-ROOM-24 | Instance creation order | Keeps the GMS2 instance creation order list in sync when adding or deleting instances. | UndertaleModTool/Editors/UndertaleRoomEditor/UndertaleRoomEditor.xaml.cs | P0 | planned |
| UTMT-ROOM-25 | Path layers | Path layers render the path on the canvas. | UndertaleModTool/Editors/UndertaleRoomEditor/UndertaleRoomEditor.xaml (PathDataTemplate) | P2 | planned |
| UTMT-ROOM-26 | Sequence instances | Listed under assets layers and movable, without a property editor. | UndertaleModTool/Editors/UndertaleRoomEditor/UndertaleRoomEditor.xaml | P2 | planned |

Count: 26.

## 18. Tile layer editor (TILE)

Opened by "Edit" on a GMS2 tile layer. A modal window with its own undo history.

| ID | Feature | Details | UTMT source location | Priority | Status |
|---|---|---|---|---|---|
| UTMT-TILE-01 | Tile palette | Shows the tile set as a palette with configurable column count, saved per tile set. Drag selects a multi-tile brush. | UndertaleModTool/Editors/UndertaleRoomEditor/UndertaleTileEditor.xaml.cs (PopulatePalette, Get/SetPaletteColumns) | P1 | planned |
| UTMT-TILE-02 | Paint and line | Left mouse paints the brush. Shift draws a line. | UndertaleModTool/Editors/UndertaleRoomEditor/UndertaleTileEditor.xaml.cs (PaintTile, PaintLine) | P1 | planned |
| UTMT-TILE-03 | Fill | Ctrl+left mouse flood fills; with Shift replaces the tile globally. | UndertaleModTool/Editors/UndertaleRoomEditor/UndertaleTileEditor.xaml.cs (Fill) | P1 | planned |
| UTMT-TILE-04 | Pick | Alt+left mouse or middle mouse picks a tile from the map. Middle mouse also scrolls. | UndertaleModTool/Editors/UndertaleRoomEditor/UndertaleTileEditor.xaml.cs (Pick) | P1 | planned |
| UTMT-TILE-05 | Brush transforms | Mirror X (X), flip Y (Y or Z), rotate clockwise and counterclockwise (Q, R). | UndertaleModTool/Editors/UndertaleRoomEditor/UndertaleTileEditor.xaml | P1 | planned |
| UTMT-TILE-06 | View toggles | Grid (G), brush tiling for multi-tile brushes (B), room preview behind the map (P). | UndertaleModTool/Editors/UndertaleRoomEditor/UndertaleTileEditor.xaml | P1 | planned |
| UTMT-TILE-07 | Undo and redo | Ctrl+Z, Ctrl+Y, Ctrl+Shift+Z. | UndertaleModTool/Editors/UndertaleRoomEditor/UndertaleTileEditor.xaml.cs (RecordUndo, ApplyUndo) | P1 | planned |
| UTMT-TILE-08 | Apply and cancel | Edits apply to the layer only on Apply. Closing asks about unsaved edits. | UndertaleModTool/Editors/UndertaleRoomEditor/UndertaleTileEditor.xaml.cs (ApplyButton_Click, Window_Closing) | P1 | planned |

Count: 8.

## 19. Other asset editors (AST)

| ID | Feature | Details | UTMT source location | Priority | Status |
|---|---|---|---|---|---|
| UTMT-AST-01 | Shader editor | Name, type, GLSL ES vertex and fragment, GLSL vertex and fragment, HLSL9 vertex and fragment sources, vertex attribute list, warning against editing compiled data. | UndertaleModTool/Editors/UndertaleShaderEditor.xaml | P1 | planned |
| UTMT-AST-02 | Path editor | Name, smooth, closed, precision, points (X, Y, speed). Preview does not update live (noted in the UI). | UndertaleModTool/Editors/UndertalePathEditor.xaml | P1 | planned |
| UTMT-AST-03 | Timeline editor | Moments list with step and code entry actions. | UndertaleModTool/Editors/UndertaleTimelineEditor.xaml | P1 | planned |
| UTMT-AST-04 | Extension editor | Name, class name, version, folder, product ID, files list ("Add new extension file"), options (name, value, kind, "Add new extension option"), kind templates. | UndertaleModTool/Editors/UndertaleExtensionEditor.xaml(.cs) | P2 | planned |
| UTMT-AST-05 | Extension file editor | File name, init and cleanup function names, kind, functions list. | UndertaleModTool/Editors/UndertaleExtensionFileEditor.xaml | P2 | planned |
| UTMT-AST-06 | Extension function editor | Name, external name, ID, kind, return type, argument types. | UndertaleModTool/Editors/UndertaleExtensionFunctionEditor.xaml | P2 | planned |
| UTMT-AST-07 | String editor | Edits string content. | UndertaleModTool/Editors/UndertaleStringEditor.xaml | P0 | planned |
| UTMT-AST-08 | Shared string edits | Editing a string field used elsewhere asks: change only this value, change all occurrences, or abort. | UndertaleModTool/Windows/StringUpdateWindow.xaml, UndertaleModTool/Controls/UndertaleStringReference.xaml.cs | P0 | planned |
| UTMT-AST-09 | Variable editor | Name, instance type, variable ID, name string ID. | UndertaleModTool/Editors/UndertaleVariableEditor.xaml | P1 | planned |
| UTMT-AST-10 | Variables chunk header | Instance and global variable counts and maximum local variable count. | UndertaleModTool/Editors/UndertaleVariableChunkEditor.xaml | P2 | planned |
| UTMT-AST-11 | Function editor | Name and name string ID. | UndertaleModTool/Editors/UndertaleFunctionEditor.xaml | P1 | planned |
| UTMT-AST-12 | Code locals editor | Name and local variable list (index, name), double-click to add. | UndertaleModTool/Editors/UndertaleCodeLocalsEditor.xaml | P2 | planned |
| UTMT-AST-13 | Global init scripts | List of code entries run at game start. Add and remove. | UndertaleModTool/Editors/UndertaleGlobalInitEditor.xaml | P1 | planned |
| UTMT-AST-14 | Game End scripts | List of code entries run at game end. Add and remove. | UndertaleModTool/Editors/UndertaleGameEndEditor.xaml | P1 | planned |
| UTMT-AST-15 | Particle system editor | Name, origin, draw order, emitters list. | UndertaleModTool/Editors/UndertaleParticleSystemEditor.xaml | P2 | planned |
| UTMT-AST-16 | Particle emitter editor | Enabled, emit mode, count, relative, delay and interval ranges and units, distribution, shape, region, rotation, sprite, frame, animate, stretch, random, texture, start, middle and end color, additive blend, lifetime, scale, size range, increase and wiggle, speed, gravity, direction, orientation, relative orientation, spawn on death and on update with counts. | UndertaleModTool/Editors/UndertaleParticleSystemEmitterEditor.xaml | P2 | planned |

Count: 16.

## 20. General info, options and language (GEN)

| ID | Feature | Details | UTMT source location | Priority | Status |
|---|---|---|---|---|---|
| UTMT-GEN-01 | General info fields | Disable GMS debugger, bytecode version, file name, config, last object ID, last tile ID, game ID, DirectPlay GUID, name, version, default window size, license MD5 and CRC32, timestamp (local, GMT on hover), display name, Steam AppID, debugger port, FPS, allow statistics, game GUID. | UndertaleModTool/Editors/UndertaleGeneralInfoEditor.xaml | P0 | planned |
| UTMT-GEN-02 | Info flags | Checkbox group for general info flags. | UndertaleModTool/Controls/FlagsBox.xaml | P0 | planned |
| UTMT-GEN-03 | Active targets and function classifications | Bit fields shown as flags. | UndertaleModTool/Editors/UndertaleGeneralInfoEditor.xaml | P2 | planned |
| UTMT-GEN-04 | Room order | Editable room order list, double-click to add, "Sync with room list" rebuilds it from the room asset order. | UndertaleModTool/Editors/UndertaleGeneralInfoEditor.xaml.cs (SyncRoomList_Click) | P0 | planned |
| UTMT-GEN-05 | Debugger checkbox guard | Toggling the debugger flag shows a confirmation. | UndertaleModTool/Editors/UndertaleGeneralInfoEditor.xaml.cs (DebuggerCheckBox_PreviewMouseLeftButtonDown) | P1 | planned |
| UTMT-GEN-06 | GMS2 extras | Shader extension flag and version. | UndertaleModTool/Editors/UndertaleGeneralInfoEditor.xaml | P2 | planned |
| UTMT-GEN-07 | Options (OPTN) | Flags, scale, window color, color depth, resolution, frequency, vertex sync, priority, back, front and load images, load alpha, constants table (name, value). | UndertaleModTool/Editors/UndertaleGeneralInfoEditor.xaml | P1 | planned |
| UTMT-GEN-08 | Language (LANG) | Read-only unknown value, language count and entry count. No editor for entries. | UndertaleModTool/Editors/UndertaleGeneralInfoEditor.xaml | P2 | planned |

Count: 8.

## 21. Chunks without a GUI editor (NOED)

These are read and written by UndertaleModLib but have no editor or tree node in the GUI. G3MStudio can surpass UTMT here.

| ID | Feature | Details | UTMT source location | Priority | Status |
|---|---|---|---|---|---|
| UTMT-NOED-01 | Sequences (SEQN) | Tracks, keyframes, moments, broadcast messages. Only the project system and reference search touch them. | UndertaleModLib/Models/UndertaleSequence.cs | P2 | planned |
| UTMT-NOED-02 | Animation curves (ACRV) | Channels and points. Project system only. | UndertaleModLib/Models/UndertaleAnimationCurve.cs | P2 | planned |
| UTMT-NOED-03 | Tags (TAGS) | Asset tags. | UndertaleModLib/Models/UndertaleTags.cs | P2 | planned |
| UTMT-NOED-04 | Feature flags (FEAT) | Runtime feature flag strings. | UndertaleModLib/Models/UndertaleFeatureFlags.cs | P2 | planned |
| UTMT-NOED-05 | Filter effects (FEDS) | Filter and effect definitions. | UndertaleModLib/Models/UndertaleFilterEffect.cs | P2 | planned |
| UTMT-NOED-06 | UI layers (UILR) | UI root nodes. | UndertaleModLib/Models/UndertaleUINode.cs | P2 | planned |
| UTMT-NOED-07 | Data files (DAFL) | Empty chunk, kept for round-trip. | UndertaleModLib/UndertaleChunks.cs | P2 | planned |
| UTMT-NOED-08 | Room text items and nine-slices | Present in the room model, not editable in the room editor. | UndertaleModLib/Models/UndertaleRoom.cs | P2 | planned |
| UTMT-NOED-09 | Effect layer properties | Effect type and properties in the model; the room editor only lists the layer name. | UndertaleModLib/Models/UndertaleRoom.cs | P2 | planned |

Count: 9.

## 22. Shared controls (CTRL)

| ID | Feature | Details | UTMT source location | Priority | Status |
|---|---|---|---|---|---|
| UTMT-CTRL-01 | Asset reference field | Shows the referenced asset. Drop an asset of the right type to change it, double-click or "..." to open, middle-click for a new tab, "X" to clear. Context menu: Open in new tab, Find all references. | UndertaleModTool/Controls/UndertaleObjectReference.xaml(.cs) | P0 | planned |
| UTMT-CTRL-02 | String reference field | Inline text edit of a shared string with the update prompt, drop a string to relink, "..." opens the string. | UndertaleModTool/Controls/UndertaleStringReference.xaml(.cs) | P0 | planned |
| UTMT-CTRL-03 | Texture page item display | Renders a page item with its bounding box on a transparency grid. | UndertaleModTool/Controls/UndertaleTexturePageItemDisplay.xaml | P0 | planned |
| UTMT-CTRL-04 | Flags box | Expander with one checkbox per flag of an enum. | UndertaleModTool/Controls/FlagsBox.xaml | P0 | planned |
| UTMT-CTRL-05 | Color picker | ARGB color field with preview. | UndertaleModTool/Controls/ColorPicker.xaml | P1 | planned |
| UTMT-CTRL-06 | Editable list grids | Data grids with "Double click to add" rows used by most editors. | UndertaleModTool/Controls/System/DataGridDark.cs | P0 | planned |

Count: 6.

## 23. Scripting system (SCRIPT)

| ID | Feature | Details | UTMT source location | Priority | Status |
|---|---|---|---|---|---|
| UTMT-SCRIPT-01 | Scripts menu | Built from the `Scripts` folder next to the executable. Subfolders become submenus loaded on open. Shows "(No scripts found!)" or a missing-path note. | UndertaleModTool/MainWindow.xaml.cs (MenuItem_RunScript_SubmenuOpened) | P1 | planned |
| UTMT-SCRIPT-02 | Run other script | "Run other script..." opens any `.csx`. | UndertaleModTool/MainWindow.xaml.cs (MenuItem_RunOtherScript_Click) | P1 | planned |
| UTMT-SCRIPT-03 | C# script engine | Roslyn `CSharpScript` with `IScriptInterface` as globals, default imports and references (UndertaleModLib, Underanalyzer, ImageMagick, Newtonsoft.Json and others), compiled once at startup in the background. | UndertaleModTool/MainWindow.xaml.cs (scriptOptions, RunScriptNow) | P1 | planned |
| UTMT-SCRIPT-04 | Script error reporting | Compile errors and runtime exceptions are shown with line info. `ScriptCancelledException` ends a script quietly. The result or error text goes to the command box. | UndertaleModTool/MainWindow.xaml.cs (RunScriptNow), UndertaleModLib/Scripting/IScriptInterface.cs | P1 | planned |
| UTMT-SCRIPT-05 | Nested scripts and linting | `RunUMTScript` runs another script. `LintUMTScript` compiles without running. | UndertaleModTool/ScriptingFunctions.cs | P2 | planned |
| UTMT-SCRIPT-06 | Finished message | Scripts can suppress the "finished" message with `SetFinishedMessage`. | UndertaleModTool/MainWindow.xaml.cs | P2 | planned |
| UTMT-SCRIPT-07 | Script helpers | Library helpers for scripts (file name checks, image helpers). | UndertaleModLib/Util/ScriptingUtil.cs | P2 | planned |

Count: 7.

## 24. Script interface API (API)

`IScriptInterface` is the contract every `.csx` depends on. Compatibility with it decides whether existing community scripts run.

| ID | Feature | Details | UTMT source location | Priority | Status |
|---|---|---|---|---|---|
| UTMT-API-01 | State accessors | `Data`, `Project`, `FilePath`, `ScriptPath`, `ExePath`, `Highlighted`, `Selected`, `CanSave`, `IsAppClosed`, `ScriptExecutionSuccess`, `ScriptErrorMessage`, `ScriptErrorType`, `MainThreadAction`. | UndertaleModLib/Scripting/IScriptInterface.cs | P1 | planned |
| UTMT-API-02 | Data helpers | `EnsureDataLoaded`, `MakeNewDataFile`, `AreFilesIdentical`. | UndertaleModLib/Scripting/IScriptInterface.cs | P1 | planned |
| UTMT-API-03 | Message dialogs | `ScriptMessage`, `ScriptWarning`, `ScriptQuestion`, `ScriptError`, `SetUMTConsoleText`, `ScriptOpenURL`. | UndertaleModLib/Scripting/IScriptInterface.cs | P1 | planned |
| UTMT-API-04 | Input and output dialogs | `ScriptInputDialog`, `SimpleTextInput`, `SimpleTextOutput`, `InitializeScriptDialog`. | UndertaleModLib/Scripting/IScriptInterface.cs | P1 | planned |
| UTMT-API-05 | Clickable search output | Two `ClickableSearchOutput` overloads show code search results with links and a failed list. | UndertaleModLib/Scripting/IScriptInterface.cs | P2 | planned |
| UTMT-API-06 | Progress API | `SetProgressBar`, `UpdateProgressBar`, `UpdateProgressValue`, `UpdateProgressStatus`, `AddProgress`, `IncrementProgress`, parallel variants, `GetProgress`, `SetProgress`, `HideProgressBar`, `StartProgressBarUpdater`, `StopProgressBarUpdater`, `EnableUI`. | UndertaleModLib/Scripting/IScriptInterface.cs | P1 | planned |
| UTMT-API-07 | File prompts | `PromptChooseDirectory`, `PromptLoadFile`, `PromptSaveFile`. | UndertaleModLib/Scripting/IScriptInterface.cs | P1 | planned |
| UTMT-API-08 | Code text | `GetDecompiledText` and `GetDisassemblyText` by name or entry, with optional context and settings. | UndertaleModLib/Scripting/IScriptInterface.cs | P1 | planned |
| UTMT-API-09 | Navigation | `ChangeSelection(object, inNewTab)` opens an asset in the GUI. | UndertaleModLib/Scripting/IScriptInterface.cs | P2 | planned |
| UTMT-API-10 | Removed members | `SyncBinding` and `DisableAllSyncBindings` were removed in 0.9.0.0, so old scripts using them fail. | 0.9.0.0 release notes | P2 | planned |

Count: 10.

## 25. Command-line tool (CLI)

`UndertaleModCli` ships for Windows, Ubuntu and macOS (native arm64 since 0.9.2.0).

| ID | Feature | Details | UTMT source location | Priority | Status |
|---|---|---|---|---|---|
| UTMT-CLI-01 | `new` | Writes a blank data file. Options `-o/--output`, `-f/--overwrite`, `-/--stdout`. | UndertaleModCli/Program.cs | P2 | G3MTool (not duplicated) |
| UTMT-CLI-02 | `load` | Loads a data file, runs scripts `-s/--scripts a.csx b.csx`, then a C# line `-l/--line`, saves to `-o/--output` (`-f/--overwrite`). | UndertaleModCli/Program.cs | P1 | G3MTool (not duplicated) |
| UTMT-CLI-03 | `load -i` interactive menu | Menu: 1 run script, 2 run C# string, 3 save and overwrite, 4 save elsewhere, 5 quick info, 6 quit without saving. | UndertaleModCli/Program.cs | P2 | G3MTool (not duplicated) |
| UTMT-CLI-04 | `info` | Prints basic data file info. `-v/--verbose` adds detail. | UndertaleModCli/Program.cs | P1 | G3MTool (not duplicated) |
| UTMT-CLI-05 | `dump` | `-c/--code <names>` or `UMT_DUMP_ALL`, `-s/--strings`, `-t/--textures`, `--sprites`, output folder `-o`. | UndertaleModCli/Program.cs | P1 | G3MTool (not duplicated) |
| UTMT-CLI-06 | `replace` | `-c/--code name=./file.gml`, `-t/--textures "Texture 0=./file.png"`, `UMT_REPLACE_ALL`, output `-o`. | UndertaleModCli/Program.cs | P1 | G3MTool (not duplicated) |
| UTMT-CLI-07 | `project build` | Builds a project: project JSON argument, `-s/--source` data file, `-d/--destination` data file. | UndertaleModCli/Program.cs | P1 | G3MTool (not duplicated) |
| UTMT-CLI-08 | Headless script interface | CLI implements `IScriptInterface` with console prompts so scripts run without the GUI. | UndertaleModCli/Program.UMTLibInherited.cs | P1 | G3MTool (not duplicated) |

Count: 8.

## 26. Platform and distribution (PLAT)

| ID | Feature | Details | UTMT source location | Priority | Status |
|---|---|---|---|---|---|
| UTMT-PLAT-01 | File associations | Per-user registry entries for `.win`, `.unx`, `.ios`, `.droid`, `.3ds`, `.symbian` with open, launch and special launch verbs. First run asks; a `dna.txt` file beside the exe disables it. | UndertaleModTool/FileAssociations.cs, UndertaleModTool/MainWindow.xaml.cs (Window_Loaded) | P1 | planned |
| UTMT-PLAT-02 | GUI platform | WPF on .NET for 64-bit Windows only. Uses Windows Forms, DWM and registry calls. | UndertaleModTool/UndertaleModTool.csproj | P0 | planned |
| UTMT-PLAT-03 | In-app updater | Checks the latest GitHub Actions build artifact, needs 500 MB free on the system drive, downloads it, hands over to `UndertaleModToolUpdater` which swaps files and restarts the tool, which then deletes the temp folder. Debug builds only. No automatic update check at startup. | UndertaleModTool/MainWindow.xaml.cs (UpdateApp), UndertaleModToolUpdater/Program.cs | P2 | planned |
| UTMT-PLAT-04 | Scripts shipped beside the exe | The `Scripts` folder is copied to the output folder and read at runtime. | UndertaleModTool/UndertaleModTool.csproj | P1 | planned |
| UTMT-PLAT-05 | Multiple instances | Each data file opens in its own process; child instances for audio groups (UTMT-FILE-11). No multi-file workspace. | UndertaleModTool/MainWindow.xaml.cs | P1 | planned |

Count: 5.

## 27. Keyboard shortcuts (KEY)

| ID | Feature | Details | UTMT source location | Priority | Status |
|---|---|---|---|---|---|
| UTMT-KEY-01 | Global shortcuts | Ctrl+N new, Ctrl+O open, Ctrl+S save, Ctrl+Q close, F5 temp run, Shift+F5 debugger run, Alt+F5 other runner, Ctrl+Shift+F search in code. | UndertaleModTool/MainWindow.xaml | P0 | planned |
| UTMT-KEY-02 | Tab shortcuts | Ctrl+W, Ctrl+Shift+W, Ctrl+Shift+T, Ctrl+Tab, Ctrl+Shift+Tab. | UndertaleModTool/MainWindow.xaml | P0 | planned |
| UTMT-KEY-03 | Tree shortcuts | Enter opens, Delete deletes, middle-click opens in new tab. | UndertaleModTool/MainWindow.xaml.cs | P0 | planned |
| UTMT-KEY-04 | Code editor shortcuts | Ctrl+K compile or assemble, Ctrl+F find, Ctrl+wheel and Ctrl+plus or minus zoom, Ctrl+click go to definition. | UndertaleModTool/Editors/UndertaleCodeEditor.xaml(.cs) | P0 | planned |
| UTMT-KEY-05 | Room editor shortcuts | Ctrl+C, Ctrl+V, Ctrl+Shift+V, Ctrl+Z, Delete, X and Y flip, minus and plus reorder, Ctrl and Shift grid modifiers, Alt paint. | UndertaleModTool/Editors/UndertaleRoomEditor/UndertaleRoomEditor.xaml(.cs) | P0 | planned |
| UTMT-KEY-06 | Tile editor shortcuts | X, Y or Z, Q, R, G, B, P, Ctrl+Z, Ctrl+Y, Ctrl+Shift+Z, Ctrl and Alt mouse modifiers. | UndertaleModTool/Editors/UndertaleRoomEditor/UndertaleTileEditor.xaml | P1 | planned |

Count: 6.

Note: global Ctrl+C and Ctrl+P (paste) are bound at window level but only forward to the room editor. Ctrl+P as paste is a legacy binding.

## 28. Data formats and GameMaker versions (FMT)

| ID | Feature | Details | UTMT source location | Priority | Status |
|---|---|---|---|---|---|
| UTMT-FMT-01 | Data file containers | IFF `FORM` files: `data.win` (Windows), `game.unx` (Linux), `game.ios` (iOS and macOS), `game.droid` (Android), `audiogroupN.dat`, plus `.3ds` and `.symbian` by association. | UndertaleModTool/MainWindow.xaml.cs, UndertaleModTool/FileAssociations.cs | P0 | planned |
| UTMT-FMT-02 | Chunk coverage | FORM, GEN8, OPTN, LANG, EXTN, SOND, AGRP, SPRT, BGND, PATH, SCPT, GLOB, GMEN, SHDR, FONT, TMLN, OBJT, ROOM, UILR, DAFL, TPAG, CODE, VARI, FUNC, STRG, TXTR, AUDO, EMBI, TGIN, ACRV, SEQN, TAGS, FEDS, FEAT, PSEM, PSYS. | UndertaleModLib/UndertaleChunks.cs | P0 | planned |
| UTMT-FMT-03 | Bytecode versions | Full support for bytecode 13 to 17. Bytecode 14 and lower use the older code layout. Others load with code disabled. | UndertaleModLib/Models/UndertaleGeneralInfo.cs, UndertaleModLib/UndertaleIO.cs | P0 | planned |
| UTMT-FMT-04 | GMS1 and GMS2 split | Chooses GMS1 or GMS2 layouts (rooms, tile sets, sprites) from the general info version and chunk contents. | UndertaleModLib/UndertaleData.cs (IsGameMaker2) | P0 | planned |
| UTMT-FMT-05 | Version detection | Infers the real GameMaker version from chunk layouts because headers stop at 2.0. Detection points: 2.0.6, 2.2.2.302, 2.3, 2.3.1, 2.3.2, 2.3.7, 2022.1, 2022.2, 2022.3, 2022.5, 2022.6, 2022.8, 2022.9, 2023.1, 2023.2, 2023.4, 2023.6, 2023.8, 2023.11, 2024.2, 2024.4, 2024.6, 2024.8, 2024.11, 2024.13, 2024.14, 2024.14.1, 2024.14.4, 2026.1 (beta). Handles the 2022 LTS branch separately. | UndertaleModLib/UndertaleChunks.cs, UndertaleModLib/Models/*.cs | P0 | planned |
| UTMT-FMT-06 | YYC games | Detected. Assets load and save; code is absent. Support is an open research issue. | UndertaleModLib/UndertaleData.cs (IsYYC) | P0 | planned |
| UTMT-FMT-07 | Audio formats | Embedded WAV and OGG; external streamed OGG files referenced by sounds; external audio group files. | UndertaleModLib/Models/UndertaleSound.cs, UndertaleModLib/Models/UndertaleEmbeddedAudio.cs | P0 | planned |
| UTMT-FMT-08 | Texture formats | PNG, QOI, BZ2 plus QOI (GM 2022.3+ texture compression), DDS, raw BGRA. | UndertaleModLib/Util/GMImage.cs | P0 | planned |
| UTMT-FMT-09 | Debug data format | `.yydebug` writer for GMS1 debugger data. | UndertaleModLib/UndertaleDebugChunks.cs, UndertaleModLib/UndertaleDebugData.cs | P2 | planned |
| UTMT-FMT-10 | Write integrity checks | Writer verifies every referenced object was written (`ThrowIfUnwrittenObjects`) and keeps alignment rules per chunk. | UndertaleModLib/UndertaleIO.cs | P0 | planned |
| UTMT-FMT-11 | Pointer offset map | Offset map generator for format debugging. | UndertaleModLib/UndertaleIO.cs (GenerateOffsetMap) | P2 | planned |

Count: 11.

## Feature count by area

| Area | Count |
|---|---|
| APP | 15 |
| FILE | 14 |
| TREE | 11 |
| TAB | 8 |
| RUN | 6 |
| SET | 16 |
| GMLS | 8 |
| SRCH | 7 |
| PROJ | 13 |
| CODE | 17 |
| SPR | 5 |
| BG | 5 |
| TEX | 10 |
| FNT | 5 |
| SND | 6 |
| OBJ | 6 |
| ROOM | 26 |
| TILE | 8 |
| AST | 16 |
| GEN | 8 |
| NOED | 9 |
| CTRL | 6 |
| SCRIPT | 7 |
| API | 10 |
| CLI | 8 |
| PLAT | 5 |
| KEY | 6 |
| FMT | 11 |
| Total | 272 |

## Bundled script inventory

Source: folder `UndertaleModTool/Scripts` at `f43e12c`, descriptions from SCRIPTS.md and, where SCRIPTS.md is silent, from the script code. Folder names are the categories shown in the Scripts menu. There are no "Builtin", "Community" or "Helper" folders in this version. `UTDR Scripts/Borders` and `UTDR Scripts/TouchControls_data` hold PNG data for the border and touch control scripts. Several scripts use WPF or Windows Forms and run only in the Windows GUI.

| Script | Category | Purpose |
|---|---|---|
| ExportAllAssembly.csx | Resource Exporters | Exports GML assembly of all code entries. |
| ExportAllCode.csx | Resource Exporters | Exports decompiled GML of all code entries. |
| ExportAllEmbeddedTextures.csx | Resource Exporters | Exports all texture pages as PNG. |
| ExportAllFonts.csx | Resource Exporters | Exports font PNGs and glyph data. |
| ExportAllMasks.csx | Resource Exporters | Exports sprite collision masks as black and white PNG. |
| ExportAllRoomsToPNG.csx | Resource Exporters | Renders all rooms to PNG (Windows GUI only). |
| ExportAllShaders.csx | Resource Exporters | Exports shader metadata, sources and compiled code. |
| ExportAllSounds.csx | Resource Exporters | Exports sounds as WAV or OGG. |
| ExportAllSprites.csx | Resource Exporters | Exports sprite frames as PNG, optional padding. |
| ExportAllStrings.csx | Resource Exporters | Exports all strings to a text file, optional multiline strings. |
| ExportAllStringsJSON.csx | Resource Exporters | Exports all strings to JSON, including multiline strings. |
| ExportAllTextures.csx | Resource Exporters | Exports PNGs of page items used by sprites, fonts and backgrounds. |
| ExportAllTexturesGrouped.csx | Resource Exporters | Same as above with one subfolder per asset. |
| ExportAllTilesets.csx | Resource Exporters | Exports tile sets or backgrounds as PNG. |
| ExportSpecificCode.csx | Resource Exporters | Exports code for given object, script or code entry names. |
| ExportSpecificSprites.csx | Resource Exporters | Exports named sprites, optional padding. |
| ExportSpritesAsGIF.csx | Resource Exporters | Exports sprites as animated GIF with padding. |
| ExportTextureGroups.csx | Resource Exporters | Exports page items grouped by texture group plus full pages. |
| ApplyBasicGraphicsMod.csx | Resource Importers | Swaps same-size sprite images in place on their texture page, no packer. |
| ImportAllEmbeddedTextures.csx | Resource Importers | Replaces every texture page from `<index>.png` in a chosen folder (not in SCRIPTS.md). |
| ImportAllStrings.csx | Resource Importers | Imports strings from text, same unmodified game only, no multiline strings. |
| ImportAllStringsJSON.csx | Resource Importers | Imports strings from JSON, same unmodified game only. |
| ImportAllTilesets.csx | Resource Importers | Imports tile set or background images for existing assets. |
| ImportAssembly.csx | Resource Importers | Imports GML assembly. |
| ImportFonts.csx | Resource Importers | Imports fonts from PNGs and `glyphs_<name>.csv` files and packs them onto new atlas pages (not in SCRIPTS.md). |
| ImportGML.csx | Resource Importers | Compiles and imports GML files, creating assets where possible. |
| ImportGMS2FontData.csx | Resource Importers | Imports font data from a GMS2 `.yy` font file. |
| ImportGraphics.csx | Resource Importers | Imports sprites (`name_N.png`) or backgrounds from a folder with packing. |
| ImportGraphicsAdvanced.csx | Resource Importers | Imports PNG or GIF with several naming schemes, then a dialog for offsets and speed (Windows GUI only). |
| ImportMasks.csx | Resource Importers | Imports collision masks from black and white PNG. |
| ImportShaders.csx | Resource Importers | Imports shaders in the export script's format. |
| ImportSingleSound.csx | Resource Importers | Imports one WAV or OGG sound. |
| ImportSounds.csx | Resource Importers | Imports many WAV or OGG sounds. |
| NewTextureRepacker.csx | Resource Importers | Rebuilds all texture pages with a packer, settings in the script. |
| ReduceEmbeddedTexturePages.csx | Resource Importers | Older, simpler texture page rebuild. |
| DeltaHATE.csx | Sample Scripts | Shuffles assets in Deltarune SURVEY_PROGRAM. |
| DeltaMILK.csx | Sample Scripts | Replaces non-background sprites in Deltarune chapter 1 with milk. |
| HeCanBeEverywhere.csx | Sample Scripts | Adds multiple Jevils to Deltarune SURVEY_PROGRAM. |
| MixMod.csx | Sample Scripts | Non-working YouTube music extension demo for Undertale (has MixMod_README.md). |
| RoomOfDetermination.csx | Sample Scripts | Room editing and code modding demo for Undertale. |
| TheWholeWorldRevolving.csx | Sample Scripts | Rotates the screen permanently in Deltarune SURVEY_PROGRAM. |
| 13_To_14.csx | Technical Scripts | Converts bytecode 13 to 14. |
| 14_To_16.csx | Technical Scripts | Converts bytecode 14 to 16. |
| 15_To_16.csx | Technical Scripts | Converts bytecode 15 to 16. |
| 15_to_17_To_16.csx | Technical Scripts | Converts bytecode 15 or 17 games to 16. |
| 16_To_17.csx | Technical Scripts | Converts bytecode 16 to 17. |
| ConvertFrom17to16.csx | Technical Scripts | Converts bytecode 17 to 16. |
| ConvertFrom17to16_for_2.3.csx | Technical Scripts | Converts bytecode 17 to 16 for GMS 2.3 games. |
| AutoBackup.csx | Technical Scripts | Background auto backup of the data file with a GUI. |
| CheckDecompiler.csx | Technical Scripts | Round-trips code to find inaccurate decompilation. |
| CopySound.csx | Technical Scripts | Copies sounds from another game. |
| CopySoundInternal.csx | Technical Scripts | Copies sounds within the same game. |
| CopySpriteBgFont.csx | Technical Scripts | Copies sprites, backgrounds and fonts from another game. |
| CopySpriteBgFontInternal.csx | Technical Scripts | Copies sprites, backgrounds and fonts within the same game. |
| ExecutionOrder.csx | Technical Scripts | Shows object event execution order and `global.interact` use in game. |
| ExportAllCodeSync.csx | Technical Scripts | Exports GML single-threaded. |
| ExportAssetOrder.csx | Technical Scripts | Exports asset names in order (excludes 2.3+ assets). |
| FindNullsInOffsetMap.csx | Technical Scripts | Finds null objects in the offset map. |
| FindUnknownFunctions.csx | Technical Scripts | Finds improperly defined functions that stop a game booting. |
| FindUnusedStrings.csx | Technical Scripts | Finds unused strings (superseded by the GUI search). |
| GameObjectCopy.csx | Technical Scripts | Copies objects from another game. |
| GameObjectCopyInternal.csx | Technical Scripts | Copies objects within the same game. |
| GetAllChunkNames.csx | Technical Scripts | Prints C# code listing the chunk names. |
| GetTempDirectory.csx | Technical Scripts | Prints the base directory for bundled builds. |
| ImportAssetOrder.csx | Technical Scripts | Reorders assets from an exported order file. |
| ImportGraphics_Full_Repack.csx | Technical Scripts | Imports graphics and repacks all textures, slow. |
| LintAllScripts.csx | Technical Scripts | Checks that all bundled scripts compile. |
| MatchRoomOrderToInternalOrder.csx | Technical Scripts | Matches room asset order to the room order list. |
| Profiler.csx | Technical Scripts | Adds a profiler and stack tracer to find freezes. |
| RealignRoomInternalOrder.csx | Technical Scripts | Matches room order list to asset order (superseded by "Sync with room list"). |
| RemoveUnusedSounds.csx | Technical Scripts | Removes unused sounds. |
| RestoreMissingCodeLocals.csx | Technical Scripts | Recreates missing code locals for some versions. |
| SpriteOriginCopy.csx | Technical Scripts | Copies sprite origins from another data file by sprite name (not in SCRIPTS.md). |
| TestExportAllCode.csx | Technical Scripts | Logs decompiler crashes over all code. |
| ToggleAlignment.csx | Technical Scripts | Toggles 4-byte alignment in TPAG. |
| VariableFixer.csx | Technical Scripts | Repairs variable definitions for some versions. |
| Debug.csx | UTDR Scripts | Toggles debug mode in supported Undertale and Deltarune versions. |
| DeltaruneClearShaderData.csx | UTDR Scripts | Clears Deltarune shader data for old hardware. |
| DeltaruneReloadJSON.csx | UTDR Scripts | Adds a hotkey that reloads the language JSON in Deltarune. |
| DeltaruneTTFFonts.csx | UTDR Scripts | Marks Deltarune fonts as externally loaded. |
| DisableDogcheck.csx | UTDR Scripts | Disables dogcheck so any room loads from a save. |
| ShowRoomName.csx | UTDR Scripts | Shows room ID and name in debug mode. |
| TouchControlsEnabler.csx | UTDR Scripts | Adds mobile touch controls. |
| UndertaleBattlegroupSelector.csx | UTDR Scripts | Adds a battlegroup selector hotkey. |
| UndertaleBetterVaporiser.csx | UTDR Scripts | Replaces the vapor effect with on-the-fly generation. |
| UndertaleBorderEnabler.csx | UTDR Scripts | Enables borders on PC Undertale before the Switch version. |
| UndertaleBorderEnablerv1_11.csx | UTDR Scripts | Enables borders on Undertale 1.11. |
| UndertaleChangeHomeBattlegroup.csx | UTDR Scripts | Changes the Home key battlegroup. |
| UndertaleDebugMsg.csx | UTDR Scripts | Shows `global.msg` on screen in debug mode. |
| UndertaleDebugToggler.csx | UTDR Scripts | Toggles debug mode with F1. |
| UndertaleDialogSimulator.csx | UTDR Scripts | Adds an in-game dialogue editor. |
| UndertaleFixAlphysLabCrashAndroid.csx | UTDR Scripts | Fixes an Android crash at Alphys's lab. |
| UndertaleGoToRoom.csx | UTDR Scripts | Adds a go-to-room hotkey. |
| UndertaleRunButton.csx | UTDR Scripts | Removes the debug check from the Backspace speed boost. |
| UndertaleSafeBlaster.csx | UTDR Scripts | Adds a Gaster blaster object not tied to the Sans fight. |
| UndertaleSimplifyBattlegroupScript.csx | UTDR Scripts | Removes duplicate code from `scr_battlegroup`. |
| UndertaleSwitchAndXboxOnPC.csx | UTDR Scripts | Patches Switch and Xbox data to run on PC. |
| UndertaleTTFFonts.csx | UTDR Scripts | Marks Undertale fonts as externally loaded. |
| UndertaleWASD.csx | UTDR Scripts | Adds WASD controls. |
| UndertaleWithJSONs.csx | UTDR Scripts | Moves Undertale 1.05+ text to JSON with F11 language switch and F12 reload. |
| ExternalizeAllOGGs.csx | Utility Scripts | Moves embedded OGG sounds to external files. |
| ExtractEmbeddedDataFile.csx | Utility Scripts | Extracts a data file from an executable or memory dump. |
| FancyRoomSelect.csx | Utility Scripts | Adds an in-game room select and teleport menu. |
| FindAndReplace.csx | Utility Scripts | Find and replace across all GML. |
| FindObjectsInRooms.csx | Utility Scripts | Finds instances of chosen objects in all rooms. |
| FindObjectsWithSprite.csx | Utility Scripts | Finds objects using a sprite name pattern. |
| FontEditor.csx | Utility Scripts | Detailed font editor GUI (Windows GUI only, has FontEditor_README.md). |
| GoToRoom_AutoLocatePersistentObj.csx | Utility Scripts | Go-to-room hotkey that finds a persistent object automatically. |
| MergeImages.csx | Utility Scripts | Writes side-by-side images from two folders. |
| ScaleAllTextures.csx | Utility Scripts | Scales all texture pages. |

Script count: 18 Resource Exporters, 17 Resource Importers, 6 Sample Scripts, 35 Technical Scripts, 24 UTDR Scripts, 10 Utility Scripts. Total 110.

## Weaknesses and opportunities

From the source:
- Windows-only GUI. WPF, Windows Forms, DWM and registry calls; Linux and macOS users get only the CLI. Issue #460 asks for a cross-platform UI framework and #2161 for accurate Linux support. https://github.com/UnderminersTeam/UndertaleModTool/issues/460, https://github.com/UnderminersTeam/UndertaleModTool/issues/2161
- No general undo. Only room object drags and the tile editor have undo. Open since early: #146. https://github.com/UnderminersTeam/UndertaleModTool/issues/146
- Deleting an asset shifts later IDs and breaks code that uses numeric IDs. The tool only warns.
- Load and save are modal and block the UI. Saves write the whole file each time.
- Seven chunk types (sequences, animation curves, tags, feature flags, filter effects, UI layers, data files) have no editor, the language editor is a stub, the texture group editor is marked TODO, effect layers and text items are not editable.
- Path preview does not update live (stated in the UI). Debug data generation is wired but disabled.
- One data file per process. Audio groups open as extra processes over a named pipe.

From issues:
- Room editor UX: better GMS1 room editor (#2068), zoom resets position when switching tabs (#2082), view jumps on deletion (#1953), no arrow-key movement (#1471), grainy room export (#328). https://github.com/UnderminersTeam/UndertaleModTool/issues/2068, https://github.com/UnderminersTeam/UndertaleModTool/issues/2082, https://github.com/UnderminersTeam/UndertaleModTool/issues/1953, https://github.com/UnderminersTeam/UndertaleModTool/issues/1471, https://github.com/UnderminersTeam/UndertaleModTool/issues/328
- Code editor wishes: autocomplete (#470), built-in function docs on hover or middle-click (#177), indentation settings (#2366), word wrap and hover info panels (#2382). https://github.com/UnderminersTeam/UndertaleModTool/issues/470, https://github.com/UnderminersTeam/UndertaleModTool/issues/177, https://github.com/UnderminersTeam/UndertaleModTool/issues/2366, https://github.com/UnderminersTeam/UndertaleModTool/issues/2382
- Asset workflow: import assets from other data files (#2382), duplicate and drag between instances (#2287), font glyph import and export (#222), a real sprite editor (#354), type into reference fields instead of drag and drop (#899). https://github.com/UnderminersTeam/UndertaleModTool/issues/2287, https://github.com/UnderminersTeam/UndertaleModTool/issues/222, https://github.com/UnderminersTeam/UndertaleModTool/issues/354, https://github.com/UnderminersTeam/UndertaleModTool/issues/899
- String editing friction: apply on Enter (#748), remember the "String reference changed" choice (#747). https://github.com/UnderminersTeam/UndertaleModTool/issues/748, https://github.com/UnderminersTeam/UndertaleModTool/issues/747
- YYC support is still research (#104, most reacted open issue). https://github.com/UnderminersTeam/UndertaleModTool/issues/104
- Scripting gaps: generic read-line input for scripts (#2487), autorun a script on launch (#754). https://github.com/UnderminersTeam/UndertaleModTool/issues/2487, https://github.com/UnderminersTeam/UndertaleModTool/issues/754
- Other: UI localization (#181), user documentation (#889), save failures around the temp file (#2120). 26 open issues mention crashes. https://github.com/UnderminersTeam/UndertaleModTool/issues/181, https://github.com/UnderminersTeam/UndertaleModTool/issues/889, https://github.com/UnderminersTeam/UndertaleModTool/issues/2120
