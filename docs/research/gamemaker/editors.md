# GameMaker LTS 2026 IDE editor reference

Reference for G3MStudio, an Avalonia editor for compiled GameMaker data files. It describes how the GameMaker IDE (LTS 2026, IDE v2026.0.0.16) presents each editor, so G3MStudio can borrow layouts and interactions where they fit a data file.

Sources: the official LTS manual at `https://manual.gamemaker.io/lts/en/`. Every page cited below exists in the LTS manual. Five key pages (Sprites, Image Editor, Sounds, Tile Sets, The Debugger) were also compared with the monthly manual at `https://manual.gamemaker.io/monthly/en/`; the monthly text was the same or older, so nothing here depends on it. Menu layout, preferences and hotkeys come from hands-on notes on the installed IDE and from [gamemaker-hotkeys.md](gamemaker-hotkeys.md). Terms follow [gamemaker-terminology.md](gamemaker-terminology.md).

The install at `C:\Program Files\GameMaker-LTS2026` ships each editor as its own GUI plugin folder: AnimCurveGUI, AssetBrowserGUI, AudioTextureGroupsGUI, BreakpointPluginsGUI, BuildGUI, CodeEditor2, DebugInfoGUI, DebuggerGUI, EnvironmentVariablesViewerGUI, ExtensionsEditorGUI, FontGUI, GMLVisualGUI, ImageGUI, IncludedFilesEditorGUI, InspectorGUI, LocalPackageGUI, NotesEditorGUI, ObjectEditorGUI, OutputWindowGUI, PathEditorGUI, RoomEditorGUI, SearchAndReplaceGUI, SearchInScriptGUI, SoundEditorGUI, StartScreenGUI, TextEditor, TileSetEditorGUI, TimelineEditorGUI. There are no separate top-level folders for the Sequence, Particle, Shader or Prefab editors; those live inside other plugins or packages (the Prefab Library is installed through the Package Manager).

Each "Relevance to G3MStudio" line says what maps onto data-file editing and what does not. The compiled data file (data.win and its relatives) keeps runtime data: chunks such as GEN8, OPTN, SPRT, BGND, SOND, AGRP, AUDO, TPAG, TXTR, TGIN, FONT, PATH, SHDR, SCPT, CODE, VARI, FUNC, STRG, OBJT, ROOM, TMLN, SEQN, ACRV, TAGS, PSEM, PSYS, FEDS, EXTN, EMBI. It lacks the authoring layer: asset folders, colours, favourites, notes, comments, image layers, configurations other than the built one, and every platform's options except the built target.

Contents

1. IDE shell
2. Resource editors
3. Workflows
4. Top 20 ideas for G3MStudio

---

## 1. IDE shell

### 1.1 Start Page

Source: [Introduction/The_Start_Page.htm](https://manual.gamemaker.io/lts/en/Introduction/The_Start_Page.htm)

Purpose: the landing page before a project is open, and a page that can be reopened later in its own workspace tab.

Layout:

| Area | Contents |
| --- | --- |
| Menu bar | The global menus (File, Edit, Build, Windows, Tools, Marketplace, Layouts, Help, plus Source Control once enabled). Focused editors add their own menus, for example Image, View and Effects for the Image Editor. |
| Project options | New, Open, Import. Import takes a `.yyz` archive or a GameMaker Studio 1.4 `.gmx`/`.gmz` project. |
| Recent projects | Project name and path, tile or list view toggle in the top right. |
| Version and log in | IDE and runtime versions, update notices, account. |
| Useful resources | Tiles linking to tutorials. |

New project flow: pick a project type (Game, Live Wallpaper, Game Strip), then a template. "Blank Game" turns on "Interpolate colours between pixels"; "Blank Pixel Game" turns it off. Sample templates may ask for GML Code or GML Visual. Then name and location, then "Let's Go!". A warning appears when the project path is in cloud storage. First launch also offers a Setup Wizard with a "Don't show me again" box.

Relevance to G3MStudio: a start page with recent data files (path, game name from GEN8, last opened) and Open/Import maps directly. Templates and project types do not apply.

### 1.2 Workspaces, chains, canvas pan and zoom

Sources: [Introduction/Workspaces.htm](https://manual.gamemaker.io/lts/en/Introduction/Workspaces.htm), [IDE_Navigation/IDE_Input.htm](https://manual.gamemaker.io/lts/en/IDE_Navigation/IDE_Input.htm), [Workspace preferences](https://manual.gamemaker.io/lts/en/Setting_Up_And_Version_Information/IDE_Preferences/General/Workspace.htm)

Purpose: a workspace is an infinite canvas holding editor windows. A project can have several workspace tabs ("Workspace 1", plus button to add more, double-click a tab to rename).

Behaviour:

- Editors open as floating panels on the canvas. Related editors are linked by drawn "chains", for example Object Editor to Event list to code editor. Chain padding (column and row, default 20 px), segment count, link colour (default ARGB $99DD00) and whether chains may overlap are preferences.
- A workspace tab can be dragged out into its own OS window, for a second monitor. Both windows still belong to one project.
- Pan: hold middle mouse and drag, or Space plus left drag; trackpad two-finger pan. Zoom: Ctrl+mouse wheel or pinch, toolbar zoom out, reset to 1:1, zoom in. Ctrl+Enter resets zoom.
- Ctrl+Alt+Arrow moves focus to the nearest window in that direction; a preference sets the angular range used to pick the neighbour, and another resets zoom on keyboard navigation.
- Ctrl+Tab opens the Workspace Overview (an Alt+Tab-like switcher across open items in all workspaces); it stays open while Ctrl is held.
- Right-click on empty canvas opens a menu: Assets (create any asset type), Windows (jump to an open window), Go To.
- Toolbar quick buttons: Start Page, New, Open, Save, Create Executable, Create Executable and Launch, Debug, Run, Stop, Clean (clear asset cache), Game Options, Manual, zoom out/reset/in, toggle all docks.
- Code Editor 2 always opens in a new workspace tab instead of on the canvas.

Relevance to G3MStudio: the chained free canvas is the GameMaker signature but costs a lot to build and is disliked by many users. Tabs plus docked panels, a workspace switcher (Ctrl+Tab), and directional focus moves give most of the value. Chains map well to a "breadcrumb" (Object > Event > Code). Nothing here depends on project data.

### 1.3 Docks, floating windows and the Layouts menu

Sources: [Introduction/Workspaces.htm](https://manual.gamemaker.io/lts/en/Introduction/Workspaces.htm), [The_Layouts_Menu.htm](https://manual.gamemaker.io/lts/en/IDE_Navigation/Menus/The_Layouts_Menu.htm), [The_Output_Window.htm](https://manual.gamemaker.io/lts/en/Introduction/The_Output_Window.htm)

Docks: left (Inspector), right (Asset Browser), bottom (Output tabs). Drag a tab to a dock edge to dock it (the drop area highlights), drag it away to float it. Each dock has a collapse chevron on its edge; F12 or the toolbar button hides or shows all docks. Two output tabs can be dragged into one tab to form a split view, and a slow click on a tab renames it. Docked windows are remembered between sessions. Some docks hide while a specific editor (the Room Editor) is active and return afterwards.

Layouts menu:

| Item | Effect |
| --- | --- |
| Load Layout | Load a saved layout `.xml`. |
| Save Layout | Save the current layout as `.xml`, usable in any project. |
| Reset Layout | Restore the default layout. |
| Show All Windows | Zoom the current workspace so every window is visible. Workspaces only. |
| Close All Windows | Close every window in the current workspace. Workspaces only. |

The debugger keeps its own layout and has a reset in its own menu. The Sequence Editor and Code Editor 2 each have per-editor orientation buttons.

Relevance to G3MStudio: Avalonia dock libraries (Dock.Avalonia) cover this. Layout save/load/reset as a file and a "hide all docks" key are cheap wins. Layout is IDE state, unrelated to the data file.

### 1.4 Inspector

Sources: [IDE_Tools/The_Inspector.htm](https://manual.gamemaker.io/lts/en/IDE_Tools/The_Inspector.htm), [Inspector preferences](https://manual.gamemaker.io/lts/en/Setting_Up_And_Version_Information/IDE_Preferences/Inspector_Preferences.htm)

Purpose: a property panel for whatever is selected anywhere in the IDE: assets in the Asset Browser, room layers, room elements, sequence tracks, particle emitters, included files.

Layout (hands-on): header with name, thumbnail or preview, "Open Editor" button and type-specific buttons (Sprite: Import, Import Strip, Edit Image; Object: Add Event). Below are collapsible categories (for a sprite: Properties, Texture Settings, Collision Mask, Nine Slices). The top bar has a search box with match counter and up/down arrows, a lock button and a menu.

Features:

- Multi-select: Ctrl+click several items; the Inspector lists them with an X to drop one. Shared values show; differing values show "-". Editing a field applies it to all. Clicking one item in the list shows that item only, without losing the selection; clicking empty space returns to the group. The item focused in the list gets a red outline in its editor (colour set in Room and Sequence preferences).
- Lock: pins an Inspector to its current element. Several Inspectors can be opened from the Windows menu and locked to different elements. Locking does not make values read-only.
- "Open Editor" opens the asset editor; for a room instance it opens the instance editor; for a path layer it focuses the layer panel; for an included file it runs the default external action.
- Applying a filter/effect to a normal room layer from the Inspector makes it a single-layer effect.
- Asset extras shown only in the Inspector: sound waveform with original size, export size and ratio (copy button); sprite "Import Strip"; SVG "Clip hidden geometry"; font "Select Glyphs"; note "Open On First Load".
- Preferences: show variable display names instead of names, scroll an expanded category into view, preview starts expanded, header pinned to top.

Relevance to G3MStudio: the single most useful pattern to copy. A property grid with multi-select, mixed-value "-", lock and multiple instances suits batch edits on data-file entries (texture group of 200 sprites, audio group of many sounds, instance properties in a room). Everything shown maps to chunk fields except notes and IDE-only options.

### 1.5 Asset Browser

Sources: [Introduction/The_Asset_Browser.htm](https://manual.gamemaker.io/lts/en/Introduction/The_Asset_Browser.htm), [Asset Browser preferences](https://manual.gamemaker.io/lts/en/Setting_Up_And_Version_Information/IDE_Preferences/Asset_Browser_Preferences.htm), [Additional_Information/Project_Format.htm](https://manual.gamemaker.io/lts/en/Additional_Information/Project_Format.htm)

Purpose: the project tree. Create, organise, open, rename, delete assets; reach game options, configurations and included files.

Layout, top to bottom:

| Part | Contents |
| --- | --- |
| Search bar | Filters the tree by name substring. |
| Add Asset (+) | Create Asset window: 15 types in grid or list view, an Import button, and a count field to create N assets at once. New assets go to the bottom of the tree, outside folders, and open in the workspace. |
| Filter | Sort A to Z or Z to A, groups first or mixed, Custom Order (enables drag reorder, disables groups first), filter by asset types ("Any Type" resets), filter by tags. The button changes colour while a filter is active. |
| Extras menu | Room Manager, Config Editor, Game Options, Included Files. |
| Quick Access | Recent (default 10), Favourites, Room Order, Saved Searches, Tags, Game Options (per platform), Prefabs. |
| Asset list | The tree of groups (folders) and assets. |
| Information bar | Total and selected counts, active tag filters (click to remove). |
| Zoom | Slider 50% to 250%; double-click the value to reset to 100%. |

Interactions:

- Groups: "Create Group" in the context menu; names are free text (asset names are alphanumeric plus underscore, not starting with a digit). Drag assets and groups between folders. Duplicate group names trigger a confirmation.
- Colours: a colour strip at the bottom of the context menu, scrollable for more; right-click a swatch for a custom colour. A group colour applies to its children unless the child has its own; a preference can force group colours to win. Palette size 16 to 100.
- Tags: "Edit Tags" opens a tag editor (Enter or comma commits). Multi-select applies tags to many assets. Tagging a folder does not tag its contents. Assets created while a tag filter is active get those tags. Tags are readable from game code.
- Favourites: right-click Favourite; "Clear All" on the Favourites node.
- Saved Searches: right-click "Save Current Filter", then rename, apply (double-click), delete.
- Room Order: drag rooms to reorder; the top room has a home icon and starts the game; clicking the home icon opens the Room Manager; right-click a room to create a child room.
- Rename: slow double-click or context menu. With Feather on, a preference renames references in code.
- Open: double-click, or drag the asset onto the workspace (the editor appears under the cursor). Preferences can make sprite double-click open the Image Editor and object double-click open all event code.
- Keyboard (hotkeys file): Ctrl+D duplicate, Shift+O open in Explorer, Ctrl+] and Ctrl+[ move up or down one, Ctrl+Shift+] and Ctrl+Shift+[ move to top or bottom.

Context menu: Create (with a shortcut for the clicked type), Edit (All), Edit Tags, Rename, Duplicate, Favourite, Add Existing (from another project folder), Delete (cannot be undone; also removes room/UI layer elements made from the asset), Create Group, Expand Children, Expand All, Contract All, Open In New Asset Browser (folders only, a second browser rooted at that folder), Open in Explorer, colour swatches. Type extras:

| Type | Extra items |
| --- | --- |
| Sprite | Assign Texture Group, Create Sprite From Image(s), Open In Image Editor, Open in External Editor (bitmap, Spine or SVG/SWF editor by type) |
| Tile set, font | Assign Texture Group; fonts also Regenerate Font |
| Sound | Assign Audio Group, Open In Sound Mixer, Open in External Editor |
| Object | Open All Event Scripts (new workspace, one tab per event) |
| Room | Create Child Room |
| Script | Convert to GML Visual / GML |
| Shader | Shader Type (GLSL ES, GLSL, HLSL11, PSSL) |

Storage: folder layout and custom order live in the `.yyp` and a `.resource_order` file; the default `.gitignore` ignores `.resource_order`.

Preferences worth noting: show Quick Access, default folders in new projects, warn when opening more than N assets (default 10), max recent items, palette size, colour assets in Quick Access, group colours override child colours, create dialog style, default sort order, folders first, tree lines, expand tree on filter change, and automatic responses for delete, room group delete, opening many assets, GML Visual conversion, room parent by drag, duplicate group name.

Relevance to G3MStudio: the data file has no folders, colours, favourites or saved searches; its natural tree is "chunk type, then entries in index order". Tags do exist (TAGS chunk) and can drive filters. Room Order maps to the GEN8 room order list. G3MStudio can keep folders, colours, favourites and saved filters in its own sidecar project file next to the data file. Custom-order drag must never reorder real chunk indices unless the user asks, because code refers to assets by index.

### 1.6 Recent Windows, Go To, bookmarks

Sources: [IDE_Navigation/Recent_Windows.htm](https://manual.gamemaker.io/lts/en/IDE_Navigation/Recent_Windows.htm), [Go To preferences](https://manual.gamemaker.io/lts/en/Setting_Up_And_Version_Information/IDE_Preferences/General/Go_To.htm), [IDE_Navigation/Bookmarks.htm](https://manual.gamemaker.io/lts/en/IDE_Navigation/Bookmarks.htm)

Recent Windows: a dockable list (default 10) of windows opened earlier. Click to pan to the window; right-click to remove selected entries, move them to top, or clear the list; Ctrl or Shift for multi-select.

Go To (Ctrl+T, or workspace right-click): a filtered list that narrows while typing. It searches assets, Preferences, Redefine Keys entries, Game Options fields and user-defined functions (only `function name()` syntax, not `name = function()`). Each source is a toggle in preferences. Default limit 16 results; result styling can be turned off for speed. Choosing an item opens it or focuses it.

Bookmarks: Ctrl+Shift+1..9 sets a bookmark on a window or a code line, Ctrl+1..9 jumps to it, repeating the set combo removes it. Bookmarks last only for the session.

Relevance to G3MStudio: Go To is a must. A command palette over entry names, strings (STRG), functions (FUNC), variables (VARI), game option fields and settings fits a data file with thousands of entries. Session bookmarks and a recent list are cheap.

### 1.7 Search & Replace and the results window

Sources: [The_Edit_Menu.htm](https://manual.gamemaker.io/lts/en/IDE_Navigation/Menus/The_Edit_Menu.htm), [The_Output_Window.htm](https://manual.gamemaker.io/lts/en/Introduction/The_Output_Window.htm)

Purpose: project-wide text search and replace over scripted assets: room creation code, event code, scripts, shaders. Ctrl+Shift+F.

Fields and options:

| Control | Behaviour |
| --- | --- |
| Search term | Text box with a history drop-down. |
| Replace term | Text box with its own history. |
| Case sensitive | Exact case only. |
| Whole word | Skips partial matches (`game` does not match `game_end`). |
| Ignore comments | Skips matches inside comments. |
| Asset filter | "Everything" by default; a checklist of asset types to include. |
| Find All | Lists every match in the Search Results tab. |
| Find Previous / Find Next | Opens the asset and pans to the match. |
| Replace All / Replace | Replace everywhere, or the current highlighted match. |
| Stop Search | Cancels a running search. |

Escape clears the focused field, or closes the window when it is empty. Results use `object - event - line: text` or `script - line: text`; double-click opens the asset at the line. The same tab shows Feather quick-fix results and Find Usages.

Code Editor 2 adds an in-editor Find (Ctrl+F) and Replace (Ctrl+H) over the open documents with whole word, match case and regex toggles, and a session-wide history.

Relevance to G3MStudio: search maps to decompiled code (CODE chunk), shaders (SHDR source), strings and room creation code. Replace must recompile the touched code entries, so G3MStudio needs a preview of all replacements and one undo step for the batch. Comments are absent after decompilation, so "ignore comments" only matters for code the user wrote in G3MStudio.

### 1.8 Output, Compile Errors, Feather Messages, notifications

Sources: [The_Output_Window.htm](https://manual.gamemaker.io/lts/en/Introduction/The_Output_Window.htm), [Feather_Messages.htm](https://manual.gamemaker.io/lts/en/The_Asset_Editors/Code_Editor_Properties/Feather_Messages.htm), [General preferences](https://manual.gamemaker.io/lts/en/Setting_Up_And_Version_Information/IDE_Preferences/General_Preferences.htm), [Output Windows preferences](https://manual.gamemaker.io/lts/en/Setting_Up_And_Version_Information/IDE_Preferences/General/Output_Windows.htm)

Bottom dock tabs:

| Tab | Contents |
| --- | --- |
| Output | Compiler output and `show_debug_message` text. Verbosity is a Compiling preference. |
| Search Results | Search & Replace, Find Usages, quick-fix results. |
| Source Control | Git plugin output. |
| Breakpoints | Every breakpoint with an enable checkbox; right-click to remove or open its code. |
| Syntax Errors | Legacy checker, format `object - event - line - position: message`; updates after a short typing pause; unused or undeclared variable warnings do not block compiling. |
| Compile Errors | Compiler errors split out of Output, format `object - event - line: message`. |
| Feather Messages | Replaces Syntax Errors when Feather is on. |

Feather Messages (hands-on): toggle buttons with counts for Errors, Warnings and Suggestions, a search box, sortable columns Code, Message, Resource, Line, Column. Double-click jumps to the code. The manual lists 107 rules, GM1000 to GM2064, each with a severity that can be changed per rule or by profile (None, SyntaxErrors, TypeErrors, All).

Each output pane has its own colour, font, style, size and anti-aliasing preference.

Notifications: toast messages for new runtimes and similar events; enable switch and timeout (default 6000 ms) in General preferences.

Relevance to G3MStudio: a bottom "Problems" panel with severity toggles, counts, search and sortable columns fits validation of a data file (bad references, overflowing indices, decompile failures). Build output and breakpoints do not apply unless G3MStudio adds a run-game button.

### 1.9 Project Health

Sources: [The_Windows_Menu.htm](https://manual.gamemaker.io/lts/en/IDE_Navigation/Menus/The_Windows_Menu.htm), [Project_Format.htm](https://manual.gamemaker.io/lts/en/Additional_Information/Project_Format.htm)

Purpose: lists non-fatal project problems found at load, for example a sprite `.yy` whose image file is missing. It covers missing files for bitmap, vector and Spine sprites, sounds and shaders. When loading fails outright, the IDE offers to unlink missing resources, or to reinstall missing packages.

Relevance to G3MStudio: the idea maps well. On open, check texture page references (TPAG to TXTR), sound to AUDO or audiogroup file links, missing external audiogroup files, string indices, and code offsets, then list findings in one window instead of failing the load.

### 1.10 Included Files

Source: [Settings/Included_Files.htm](https://manual.gamemaker.io/lts/en/Settings/Included_Files.htm)

Purpose: non-asset files shipped with the game (JSON, images, DLLs). Stored in the project `datafiles` folder.

Layout: a tree mirroring `datafiles`, with Open In Explorer and Refresh. The window cannot rename or move files; that is done on disk and the tree updates. Selecting a file shows name, size and per-platform export checkboxes with All and None buttons in the Inspector.

Interactions: drag a file from Explorer onto the IDE, confirm, and the window opens with it. Context menu: Expand All, Collapse All, Select All (selects into the Inspector for batch platform edits), Open in Code Editor 2 (JSON, YAML, INI, XML, CSV, Markdown/TXT, C/C++, Objective-C, Java), Open Properties, Open in External Editor. Names become lowercase with underscores at build. A streamed sound asset name can shadow an included file of the same base name.

Relevance to G3MStudio: in a built game these are loose files next to data.win, not inside it. G3MStudio can show them as a read/write folder view beside the data file. Per-platform export flags do not exist after the build.

### 1.11 Configurations

Source: [Settings/Configurations.htm](https://manual.gamemaker.io/lts/en/Settings/Configurations.htm)

Purpose: named build variants. "Default" always exists; other configs are its children or grandchildren. Per-config values: Game Options, extension "Copy to" platforms and proxy files, sprite/tile set/font texture group, Texture Group settings (including parent), Audio Group platform targets, Included File platforms, macro overrides (children inherit from the closest parent with an override). Create with "Add Config" or Create; activate with double-click or "Set Config". The active config is chosen in the target bar.

Relevance to G3MStudio: a data file is the output of one config. Nothing to edit here; G3MStudio may show "built with config X" if the name survives in GEN8 or OPTN.

### 1.12 Texture Groups and Audio Groups dialogs

Sources: [Settings/Texture_Groups.htm](https://manual.gamemaker.io/lts/en/Settings/Texture_Groups.htm), [Settings/Audio_Groups.htm](https://manual.gamemaker.io/lts/en/Settings/Audio_Groups.htm), [Texture_Pages.htm](https://manual.gamemaker.io/lts/en/Settings/Texture_Information/Texture_Pages.htm), [Texture_Compression.htm](https://manual.gamemaker.io/lts/en/Settings/Texture_Information/Texture_Compression.htm), [Dynamic_Textures.htm](https://manual.gamemaker.io/lts/en/Settings/Texture_Information/Dynamic_Textures.htm)

Texture Group Manager (Tools menu): add, rename, delete groups (deleted contents move to Default; Ctrl+Z undoes a delete). Left list shows Sprites, Tile Sets and Fonts in the group ("This group is empty" otherwise); clicking shows the first frame in a preview. Right-click an asset to move it to another group or open it. "Add Asset" button at the bottom.

| Setting | Notes |
| --- | --- |
| Allow Scaling | On by default; off keeps a group unscaled. |
| Automatically Crop | On by default; off keeps transparent borders for UV-sensitive use. |
| Generate Mipmaps | Off by default; forces Border Size to 8. |
| Border Size | Default 2 px of edge duplication; minimum 0. |
| Parent Texture Group | Child items go on the parent's pages when the parent ships on that platform. |
| Texture Group Format | BZ2 + QOI (default), QOI, PNG, Custom. HTML5 supports PNG only. |
| Texture Group Type | Regular or Dynamic (loaded later at runtime). |
| Dynamic Group Path | Folder for dynamic group pages inside the package. |
| Custom Options | Tool and arguments for GPU formats, for example `BCN -u2`. |
| Use Group On | Per-platform export checkboxes (not for Default). |

Texture page size is a per-platform Game Option (256 to 8192, default 2048) with a Preview button that renders the pages.

Audio Group Manager (Tools menu): add, rename, delete groups; drop-down selects the group to edit; list of member sounds; "Add Asset" button; per-platform export checkboxes (not for `audiogroup_default`); Export Directory relative to the bundle (disabled for the default group). Streamed sounds cannot join a group.

Relevance to G3MStudio: maps closely. TGIN lists texture groups with their pages and members; AGRP lists audio groups, and non-default groups ship as `audiogroupN.dat`. A group window with member list, preview, "move to group" and a texture page viewer (the Preview button idea) is high value. Moving assets between groups in a built file means repacking pages or moving audio between files, so it needs a rebuild step and a size report. Platform checkboxes and parents do not apply.

### 1.13 Sound Mixer

Source: [IDE_Tools/Sound_Mixer.htm](https://manual.gamemaker.io/lts/en/IDE_Tools/Sound_Mixer.htm)

Purpose: play several sounds side by side and balance volumes.

Layout: columns. Each column has controls (play all, loop, rewind, column volume), an audio group drop-down that loads the whole group into the column, and the sound list with per-sound play, loop, volume and collapse. A link button joins adjacent columns so their controls act together. A large button at the right adds a column; a button next to the group drop-down removes one.

Input: Asset Browser multi-select then "Open In Sound Mixer", or drag a sound into a column, or the button in the Sound Editor. Volume changes are live.

Relevance to G3MStudio: maps to SOND volume and pitch. A mixer that writes the asset-level volume back to many entries at once is a good batch editor.

### 1.14 Package Manager and Local Packages (brief)

Sources: [IDE_Tools/Package_Manager.htm](https://manual.gamemaker.io/lts/en/IDE_Tools/Package_Manager.htm), [IDE_Tools/Local_Asset_Packages.htm](https://manual.gamemaker.io/lts/en/IDE_Tools/Local_Asset_Packages.htm)

Package Manager: list of packages from a selected source (left), details and version picker with Install (right), search top left, source drop-down top right with an editor for sources (name, URL, scopes, user/password, install subfolder, show hidden). Right-click per package: update notifications and auto-update. Installs IDE features (localisation, Project Tool, Prefab Library) and GMRT packages; dependencies block uninstalling.

Local packages (`.yymps`): Create Local Package dialog with publisher, display name, package ID, version, optional signing, "Include All Options", and two lists (Project, Asset Package) with Add, Add All, Remove, Remove All. Import by dragging the file onto the IDE or Tools > Import Local Package, then pick items with the same two-list pattern, Import, or "Import all resources to a new project". Name clashes open a conflict window: per asset Replace, Keep or Rename (append a number), plus Replace All, Keep All, Rename All.

Relevance to G3MStudio: plugin and package sources do not apply. The import conflict dialog maps directly to importing assets from another data file or a mod pack.

### 1.15 Source Control

Sources: [IDE_Tools/Source_Control.htm](https://manual.gamemaker.io/lts/en/IDE_Tools/Source_Control.htm) and its subpages [Setting_Up_Git_Plugin](https://manual.gamemaker.io/lts/en/IDE_Tools/Source_Control/Setting_Up_Git_Plugin.htm), [Repository_Options](https://manual.gamemaker.io/lts/en/IDE_Tools/Source_Control/Repository_Options.htm), [Standard_Workflow](https://manual.gamemaker.io/lts/en/IDE_Tools/Source_Control/Standard_Workflow.htm), [Reverting_Files](https://manual.gamemaker.io/lts/en/IDE_Tools/Source_Control/Reverting_Files.htm), [Conflicts](https://manual.gamemaker.io/lts/en/IDE_Tools/Source_Control/Conflicts.htm), [External_Merge_Diff_Tools](https://manual.gamemaker.io/lts/en/IDE_Tools/Source_Control/External_Merge_Diff_Tools.htm), [Plugin preferences](https://manual.gamemaker.io/lts/en/Setting_Up_And_Version_Information/IDE_Preferences/Plugin_Preferences.htm)

Setup: point Preferences > Plugins > Source Control (Git) at `git.exe`, then tick "Enable Source Control" in the main Game Options. New and imported projects get default `.gitignore` and `.gitattributes` (both toggles).

Menu: Create Project Repository, Clone Repository (HTTPS URL), Commit Changes, Push Changes, Pull Changes, View History, Show Conflicts.

Features:

- Asset Browser status icons: modified (red), clean (green tick).
- Commit window: Staged Changes, Unstaged Changes, Commit Message; Stage All and Unstage All; prefixes `[...]` staged, `[+]` added, `[->]` renamed, `[-]` removed, `[M]` unstaged change, `[?]` untracked, `[!]` missing. Right-click a file: Revert Path, Delete Path. Double-click opens the diff tool.
- History window: commits pane, message pane, files pane. Right-click a commit: Revert To This Revision; right-click a file: Revert Path.
- Conflicts window: list of conflicted files, buttons Use Theirs, Use Mine, Merge (external tool), Choose Merge Tool.
- External tools: merge and diff tool path plus arguments with macros `${scm_mine}`, `${scm_theirs}`, `${scm_base}`, `${scm_merged}`; defaults filled for KDiff3, Meld, TortoiseMerge, DiffMerge, Notepad++.

Limits: no remote setup from the IDE (use `git remote add`), no branch UI, HTTPS only for clone, credentials in preferences, and after any revert or pull the user must choose Reload (not Save) in the File Watcher dialog.

Relevance to G3MStudio: git on a binary data file gives little. A built-in snapshot history (save points of the data file with a per-entry diff) gives the same safety without git. External diff macros are a good model if G3MStudio exports entries as text.

### 1.16 Debugger

Sources: [IDE_Tools/The_Debugger.htm](https://manual.gamemaker.io/lts/en/IDE_Tools/The_Debugger.htm), [Watches.htm](https://manual.gamemaker.io/lts/en/IDE_Tools/The_Debugger/Watches.htm), [The_Profiler.htm](https://manual.gamemaker.io/lts/en/IDE_Tools/The_Debugger/The_Profiler.htm), [Introduction/Debugging.htm](https://manual.gamemaker.io/lts/en/Introduction/Debugging.htm)

Purpose: run the game in debug mode (F6) and inspect it over a network link. Has its own dockable layout and a Debugger menu to reset it or reopen panes.

| Pane | Contents |
| --- | --- |
| Toolbar | Resume (F5), Pause, Restart, Stop, Step Into (F11), Step Over (F10), Step Out (Shift+F11), real-time update toggle; memory, FPS and a connection light. |
| Graph View | Memory and FPS over time, Ctrl+wheel zoom, hover values, debug events as markers (system and code events coloured differently), event lists that can pop out into a window. |
| Resource View | Tree of code-bearing assets; double-click opens code in Code View. |
| Call Stack | Event and script stack with line numbers, after pause. |
| Code View | Tabs of code; click the gutter or use the context menu to toggle breakpoints. |
| Locals | Event or script name, PC offset, `self`, `other` (expandable). |
| Globals | All globals. |
| Watches | User-added expressions; Delete Watch, Clear All. |
| Instance | Variables of the instance being stepped. |
| All Instances | Every instance in the room; Add Watch from the context menu. |
| Selected Instance | Click an instance in the paused game window to inspect it. |
| Render States | Draw state of the current frame (pause only). |
| Surfaces / Textures | Surface list and texture pages in memory; hover an ID for a pop-up preview (pause only). |
| Buffer | Hex view of a buffer by ID; display type, alignment (1-byte integer for exact byte order) and column layout presets. |
| Profile | On/off, average vs total times, Top Down or Bottom Up, My Code / Engine Code / Combined; columns Name, Time (ms), Calls, Step %; sortable; double-click opens source. |

Values of structs, arrays, instances and data structures expand; many values can be edited by double-click (not reassigning arrays or map keys). Macros and enums are gone at runtime. Breakpoints set in the IDE (F9) persist with the project; ones set in the debugger last one session.

Relevance to G3MStudio: there is no debug connection to a shipped game, so the panes do not apply directly. Two ideas carry over: the Buffer pane's typed hex view (for raw chunk bytes) and the Surfaces / Textures pop-up preview (for texture pages). The PC offset in Locals matches how bytecode in CODE is addressed.

### 1.17 Game Options per platform

Sources: [Settings/Game_Options.htm](https://manual.gamemaker.io/lts/en/Settings/Game_Options.htm) and the platform pages under `Settings/Game_Options/` ([Windows](https://manual.gamemaker.io/lts/en/Settings/Game_Options/Windows.htm), [macOS](https://manual.gamemaker.io/lts/en/Settings/Game_Options/macOS.htm), [Ubuntu](https://manual.gamemaker.io/lts/en/Settings/Game_Options/Ubuntu.htm), [Android](https://manual.gamemaker.io/lts/en/Settings/Game_Options/Android.htm), [iOS](https://manual.gamemaker.io/lts/en/Settings/Game_Options/iOS.htm), [tvOS](https://manual.gamemaker.io/lts/en/Settings/Game_Options/tvOS.htm), [HTML5](https://manual.gamemaker.io/lts/en/Settings/Game_Options/HTML5.htm), [GX.games](https://manual.gamemaker.io/lts/en/Settings/Game_Options/GX_Games.htm), [Reddit](https://manual.gamemaker.io/lts/en/Settings/Game_Options/Reddit_Game_Options.htm), [Amazon Fire (legacy)](https://manual.gamemaker.io/lts/en/Settings/Game_Options/Amazon_Fire.htm))

Main options (General): default scripting language, game frames per second (default 60), default draw colour, colour outside the room, Generate Project Images, project start date and use time (read-only), project GUID with "Create New GUID", Enable Source Control, Author, Accept Spine Licence, Deprecated Behaviours (collision compatibility mode, hide audio errors, legacy JSON parsing, legacy number conversion, legacy `other` behaviour, allow `instance_change`, copy-on-write arrays, legacy primitive drawing), automatically remove unused assets when compiling (on by default, keep tags with `gml_pragma("MarkTagAsUsed", ...)`). Template Info is a sub-menu.

Platform sections:

| Platform | Sections and notable fields |
| --- | --- |
| Windows | General (product info, install folder `%localappdata%` or `%appdata%`, copy exe to output, sleep margin, Enable Steam, disable file system sandbox); Graphics (display cursor, start fullscreen, allow fullscreen switching, interpolate colours, vsync, allow window resize, borderless window, legacy DXGI swap effect, raw mouse input, scaling keep aspect or full, texture page size with Preview); Images (`.ico` icon, splash, use splash); Installer (Finished bitmap 164x314, Header bitmap 150x57, licence file, custom NSIS script). |
| macOS | General; Graphics (menu and dock in fullscreen, cursor, fullscreen, switching, interpolation, vsync, resize, Retina, scaling); Images; Packaging; Social. |
| Ubuntu | General; Graphics (fullscreen, switching, interpolation, cursor, vsync, resize, scaling); Images. |
| Android | General (product info, build tools, target/minimum/compile SDK with presets, CPU architectures Armv7/Arm64/x86_64, orientations, Bluetooth/iCade, lint, install location, sleep margin); Adaptive Icons; Graphics (interpolation, edge to edge, display layout, colour depth, device support, scaling keep aspect/crop/stretch); Icons; Images; Packaging; ProGuard; Google; Android TV; Permissions (storage, phone state, network state, internet, Bluetooth, record audio). |
| iOS | General (product info, minimum version, orientations, device family iPhone/iPad/both, defer home indicator, broadcast networking); Graphics (interpolation, halve iPad 1 textures, scaling); Icons; Images. |
| tvOS | Graphics (interpolation, scaling) plus product and image sections. |
| HTML5 | General (browser title, version, folder, output name, debug to console, cursor, "running outside server" alert, included file as index.html, loading bar extension, prepend JS); Analytics; Graphics (fullscreen switching, interpolation, centre in browser, built-in particles, built-in font, WebGL, scaling); Images; Social. |
| GX.games | Game or mod selection, name, version, register, group, share URLs, cursor, interpolation, transparent background, scaling. |
| Consoles | Not documented publicly; requires a console licence and wiki access. |

Relevance to G3MStudio: the data file keeps the built platform's runtime switches (GEN8 and OPTN flags: fullscreen, interpolation, borderless, cursor, sizeable, vsync-like flags, scaling, window size, game speed, room order, GUID, display name). These deserve a typed options page with the same grouping. Installer, icons, SDKs, permissions and other platforms' options are not in the data file.

---

## 2. Resource editors

### 2.1 Sprite Editor

Sources: [The_Asset_Editors/Sprites.htm](https://manual.gamemaker.io/lts/en/The_Asset_Editors/Sprites.htm), [Nine_Slices.htm](https://manual.gamemaker.io/lts/en/The_Asset_Editors/Sprite_Properties/Nine_Slices.htm), [Sprite_Strips.htm](https://manual.gamemaker.io/lts/en/The_Asset_Editors/Sprite_Properties/Sprite_Strips.htm), [Non-Bitmap_Sprites.htm](https://manual.gamemaker.io/lts/en/Settings/Texture_Information/Non-Bitmap_Sprites.htm), [Sprite Editor preferences](https://manual.gamemaker.io/lts/en/Setting_Up_And_Version_Information/IDE_Preferences/Sprite_Editor_Preferences.htm)

The hands-on notes cover the basic layout (left property column, canvas with zoom toolbar, top bar with FPS and origin, bottom frame timeline). Additional detail from the manual:

Image properties:

- Resize dialog with before/after preview. Scale Image (keep aspect, linear interpolation for smooth art) or Resize Canvas (anchor arrows, pixels or percent, keep aspect). "Apply to Origin" scales a custom unlocked origin with the image. Bitmap only.
- Edit Image opens the Image Editor, or the external editor set for that sprite kind.
- Import accepts PNG, JPG, GIF and others, multi-select files become frames, GIF frame speed is kept for single-frame GIFs, `_stripN` files split automatically. SVG import asks vector or bitmap (default set in preferences).

Texture settings: Tile Horizontally, Tile Vertically, Separate Texture Page (each frame gets its own page; then a dynamic option), Premultiply Alpha, Edge Filtering (removes halos with interpolation), texture Group.

Collision mask:

| Field | Values |
| --- | --- |
| Mode | Automatic (from alpha and tolerance), Full Image, Manual |
| Type | Rectangle, Rectangle With Rotation, Ellipse, Diamond, Precise, Precise (per frame), Spine Collision Mesh (Spine only) |
| Tolerance | Alpha threshold slider |
| Bounding box | Left, Right, Top, Bottom; corner handles on the canvas |

Nine slice: Activate; four guide offsets measured from their own edge, each with colour and alpha; guides on one axis push each other instead of crossing; Highlight Inverted or Overlay with colour; Selected Slice (left, top, right, bottom, centre; or click on the canvas); Tile Mode per slice (Stretch, Repeat, Mirror, Blank Repeat, Hide). The preview pane can be resized and rotated. Not for vector or Spine sprites.

Origin: preset drop-down (top left default; the nine anchor positions plus custom), click on the preview to place it, X/Y fields that accept values outside the image, lock button. New-sprite default origin, mask kind, animation mode, speed (30), size (64x64), vector precision (50) are preferences.

Frame speed: value plus mode "Frames per second" or "Frames per game frame". It sets in-game speed at `image_speed` 1.

Broadcast messages: a button adds a message string at the playhead; it shows on the frame view; right-click edits or deletes it. Received by the Broadcast Message event.

Playback and frames: play, loop or ping-pong (preview only), current frame field (1-based in the editor, 0-based in code). Frame view: drag to reorder, double-click to open the Image Editor, cut/copy/paste, Ctrl multi-select, drag image files in to add frames (vector onto bitmap converts, bitmap onto vector is refused), drag a frame edge to stretch its length in whole frames, Frame Length field for fractions, zoom handles on the timeline. Context menu: Add Frame, Insert Frame, Cut, Copy, Paste, Delete Selected Frames (only delete for vectors).

Vector and Spine: vector sprites add a Precision slider (triangle count) and a wireframe toggle; texture options grey out except group. Spine sprites import `.json` plus `.atlas` plus `.png`, cannot be edited, take collision only from the Spine data, and use the Spine runtime licence.

Relevance to G3MStudio: SPRT entries keep size, origin, bounding box, mask type and per-frame masks, playback speed and mode, frame list (TPAG references), nine-slice data and broadcast messages in newer versions, and Spine or vector payloads. Texture group membership comes from TGIN. Separate texture page, tile flags and premultiply alpha are build inputs that may survive only partly; check per version. Resize and "apply to origin" map well; any image change means repacking texture pages.

### 2.2 Image Editor

Sources: [The_Asset_Editors/Image_Editor.htm](https://manual.gamemaker.io/lts/en/The_Asset_Editors/Image_Editor.htm), [Image Editor preferences](https://manual.gamemaker.io/lts/en/Setting_Up_And_Version_Information/IDE_Preferences/Image_Editor_Preferences.htm)

Purpose: pixel editor for sprite frames. Opens as its own top-level tab and can be torn off.

Layout: frame view (left), frame information bar (speed and mode, frame count, collapsible), canvas controls, draw canvas, canvas values bar (mouse position, selection size, frame), and right-side panels for Brushes, Colours, Toolbox and Layers. Image, View and Effects menus appear in the menu bar. A tool options bar shows the active tool's settings (size, smooth, tolerance, fill style, iterations, font).

Frame view: add frame button, drag to reorder, per-frame delete button, Ctrl multi-select, Ctrl+X/C/V on frames, onion skin toggle with drag handles for frames before and after, loop or ping-pong, play. Keys 1 and 2 step frames. Drawing while the animation plays paints onto whichever frame is showing.

Canvas controls: grid toggle (G), grid snap (Ctrl+G), grid menu (colour, alpha, cell X and Y), zoom out/1:1/in/fit, Split Canvas (horizontal, vertical, single; each half has its own zoom and both edit the same image).

Tools (left and right mouse each carry a colour):

| Tool | Notes |
| --- | --- |
| Pencil (D) | Brush size, smoothing; Shift for a straight line, Ctrl picks a colour; Shift+Z then drag resizes the brush. |
| Eraser | Brush size, smoothing. |
| Fill | Alpha tolerance. |
| Colour Remove | Removes all pixels of the clicked colour (to black at alpha 0), tolerance, limited to selection. |
| Colour Replace | Replaces the clicked colour with the button's colour, tolerance, limited to selection. |
| Line | Two clicks. |
| Rectangle | Outline, filled, or both; hold Alt to draw from the centre. |
| Ellipse | Outline, filled, or both; hold Alt to draw from the centre. |
| Polygon | Click points; switching tool commits; Escape cancels. |
| Spline | Click and drag control points; Iterations (default 10). |
| Arc (A) | Present in LTS 2026 per hands-on notes; not described on the manual page. |
| Text | Creates a text layer; font and formatting in the options bar. |
| Eye Dropper | Samples a colour. |
| Rectangle Selection | Escape cancels. |
| Paint Selection | Paint a selection with any brush. |
| Magic Wand | Tolerance, "Colour only" ignores alpha. |
| Rotate | Rotates the selection or the brush; result becomes a new brush. |
| Mirror, Flip | Mirror or flip the current brush only. |
| Pan | Moves layer contents or the selection; pixels off-canvas are cropped on tool change. |

Brushes: preset square and round brushes. Copying a selection (Ctrl+C) adds it as a brush; Ctrl+B forces "selection to brush"; pasting from the clipboard creates a brush or a layer depending on the "Image Selection Copy/Paste" preference. Delete removes the selected brush. Custom brushes are lost when the project closes.

Colours: palette swatches (click with left or right button to assign), swap arrow, colour picker with ARGB and HSV, Ctrl+click a swatch to edit the palette (saved with the sprite), colour history row, palette menu to import or copy colours between sprites.

Layers: add, delete, folders, drag reorder, visibility, lock. Layer Properties: name, blend mode (normal, additive, subtractive, multiply), opacity (multiplies pixel alpha).

Image menu: Cut/Copy/Paste Frame or Selection, Select All, Cancel Selection, Invert Selection, Add Frame, Insert Frame, Delete Selected Frames, Import Image(s) (dialog to resize, crop or pad and set an anchor), Convert to Frames (frame count, width, height, offsets), Import Strip Image, Export to PNG (multi-frame exports `name_stripN.png`), Resize All Frames (scale or canvas, anchor, pixels or percent, interpolation), Crop All Frames To Selection, Auto Trim All Frames (union of all frames), Reverse Frames, Mirror and Flip (current or all frames), Rotate All Frames 90 degrees clockwise or anticlockwise.

View menu: 1:1 (Alt+C), Fit Screen (Alt+F), Show Grid (G), Onion Skin Settings (frame counts, opacity, colour).

Effects menu: Blur (box or gaussian, intensity, horizontal/vertical/both, current, visible or all layers) and Greyscale (amount, layer scope). Effects plugins can be added.

Relevance to G3MStudio: frames in a data file are flattened bitmaps on texture pages, so layers, text layers and palettes are gone. The frame operations (import strip, convert to frames, export strip PNG, auto trim, resize all, reverse, rotate, mirror/flip) map one to one and are the useful core. A full paint tool is optional; an "open frame in external editor and reimport on save" loop covers most needs.

### 2.3 Tile Set Editor

Sources: [The_Asset_Editors/Tile_Sets.htm](https://manual.gamemaker.io/lts/en/The_Asset_Editors/Tile_Sets.htm), [Auto_Tiles.htm](https://manual.gamemaker.io/lts/en/The_Asset_Editors/Tile_Set_Editors/Auto_Tiles.htm), [Brush_Builder.htm](https://manual.gamemaker.io/lts/en/The_Asset_Editors/Tile_Set_Editors/Brush_Builder.htm), [Animated_Tiles.htm](https://manual.gamemaker.io/lts/en/The_Asset_Editors/Tile_Set_Editors/Animated_Tiles.htm), [Tile Set Editor preferences](https://manual.gamemaker.io/lts/en/Setting_Up_And_Version_Information/IDE_Preferences/Tile_Set_Editor_Preferences.htm)

Purpose: turn one sprite image into a grid of tiles for tile map layers. Tile 0 (top left) is always the empty tile.

Main window: Name; Sprite picker with New Sprite, Edit Sprite and Edit Image buttons (non-bitmap sprites are converted); Properties button; Editors buttons (Brush Builder, Tile Animation, Auto Tile); preview with zoom, 1:1, fit, grid toggle and grid colour.

Properties:

| Field | Notes |
| --- | --- |
| Tile width, height | Default 16 px; rectangles allowed. |
| Tile offset X, Y | Start of the grid in the image. |
| Tile separation X, Y | Gaps between tiles. |
| Output border X, Y | Default 2; duplicated edge pixels around each tile to stop seams when scaled. |
| Texture group | As for sprites. |
| Disable Source Sprite Export | Leaves the source sprite off texture pages (only safe if the sprite is not used elsewhere). |

Auto tiles: an Autotile Editor with "47 tile" and "16 tile" templates. Click a template cell, then click the tile set tile to fill it; assign the empty tile to clear. "Show Template" overlays the template on the result. In rooms, the AutoTile tool paints connecting tiles; an "Open or Closed Edges" button decides whether room edges count as filled.

Brush Builder: tile set on the left, a brush canvas on the right. Drag-select tiles, Ctrl+click to add, Alt+click to remove. Left paints, right erases. Groups of touching tiles become one brush; leave a gap between brushes. Tools: Pencil, Eraser (sets index 0), Selection (Ctrl add, Alt subtract, cut/copy/paste), AutoTile, Flip (X), Mirror (Y), Rotate (Z, 90 degrees clockwise). Brush size repeats the tile. Library sections list animated and auto tiles.

Animated tiles: Add Animation asks for a frame count (power of two: 2, 4, 8, 16...). Click tiles in order to fill frames; play to preview. One FPS field for the whole tile set (0 means as fast as possible). Rules: a tile that starts its own animation uses it; a tile in exactly one animation uses that one from its position; a tile in several animations does not animate.

Tile properties: the LTS manual has no per-tile property or custom data editor.

Relevance to G3MStudio: BGND entries hold tile size, offsets, separation, output border, tile count, columns, the animated-tile frame table and frame time, plus the texture page link. Showing the grid with tile indices and the animation table maps well. Brush and auto-tile libraries are authoring aids; verify per version whether BGND keeps them before offering editors for them.

### 2.4 Sound Editor

Sources: [The_Asset_Editors/Sounds.htm](https://manual.gamemaker.io/lts/en/The_Asset_Editors/Sounds.htm), [Sound_in_Sequences.htm](https://manual.gamemaker.io/lts/en/The_Asset_Editors/Sequence_Properties/Sound_in_Sequences.htm)

Purpose: load WAV, MP3 or OGG into a sound asset and set how it is exported.

| Section | Fields |
| --- | --- |
| Name and file | Name, Open (load file). |
| Volume and playback | Volume slider (asset-level gain, multiplied by instance gain), play (loops), stop, loop, rewind, open in Sound Mixer. |
| OGG conversion | OGG sources only: Automatic (copy as is) or Required (always re-encode with the editor's attributes). |
| Attributes | Uncompressed (WAV), Compressed (OGG), Uncompressed on load (decompressed into memory at load), Compressed and streamed (from disk; cannot join an audio group; on HTML5 treated as unstreamed). |
| Target options | Sample rate (default 44100), bit depth (default 16; disabled for compressed), compression quality (compressed only), channels Mono, Stereo or 3D (3D is mono with a different label). |
| Audio group | Group picker; disabled for streamed sounds; not per config. |
| Export directory | Relative folder in the bundle; the audio group's directory wins. |

Inspector: waveform, Original Size, Export Size, Size Ratio, with a hide button and a copy button; byte counts in tooltips.

Effects: the Sound Editor has none. Audio effects (reverb, delay, EQ and others) attach to audio buses, and in sequences each sound track gets a bus whose effects are parameter tracks.

Relevance to G3MStudio: SOND keeps flags (embedded, compressed, streamed, decompress on load), sample rate where present, volume, pitch, group and the AUDO or external file link. Re-encoding, preview playback, the waveform with size figures and "replace audio file" map directly. Bit depth and quality are inputs to the encoder; G3MStudio would apply them when it re-encodes.

### 2.5 Path Editor

Source: [The_Asset_Editors/Paths.htm](https://manual.gamemaker.io/lts/en/The_Asset_Editors/Paths.htm)

Layout: property column with Name, point list (X, Y, speed percent; fast double-click pans to a point, slow double-click edits a value), connection kind (straight or smooth), Closed checkbox, precision 1 to 8 (default 4); canvas on the right with grid and zoom toolbox.

Interactions: click to add points; clicking near a segment inserts a point (detection distance in preferences); drag to move; Ctrl multi-select and drag groups; Delete removes points; hold T, R or S and drag to translate, rotate or scale the selection. Context menu: Delete Points, Pan To Path, Reverse, Flip, Mirror. The room behind can be shown for reference (hands-on).

Relevance to G3MStudio: PATH entries keep exactly these fields (smooth, closed, precision, points with speed), so a full editor is cheap and complete. Showing a room as backdrop is possible since rooms are in the same file.

### 2.6 Script and code editors

Sources: [Scripts.htm](https://manual.gamemaker.io/lts/en/The_Asset_Editors/Scripts.htm), [The_Text_Editor.htm (Code Editor 2)](https://manual.gamemaker.io/lts/en/The_Asset_Editors/The_Text_Editor.htm), [Editing_Text.htm](https://manual.gamemaker.io/lts/en/The_Asset_Editors/Code_Editor_Properties/Editing_Text.htm), [Feather_Features.htm (Code Completion)](https://manual.gamemaker.io/lts/en/The_Asset_Editors/Code_Editor_Properties/Feather_Features.htm), [Code_Snippets.htm](https://manual.gamemaker.io/lts/en/The_Asset_Editors/Code_Editor_Properties/Code_Snippets.htm), [JSDoc_Script_Comments.htm](https://manual.gamemaker.io/lts/en/The_Asset_Editors/Code_Editor_Properties/JSDoc_Script_Comments.htm), [Code Editor 2 preferences](https://manual.gamemaker.io/lts/en/Setting_Up_And_Version_Information/IDE_Preferences/Text_Editor_2_Preferences.htm), [Feather preferences](https://manual.gamemaker.io/lts/en/Setting_Up_And_Version_Information/IDE_Preferences/Feather_Settings.htm)

Two editors exist. The Legacy Code Editor is the default: tabs per script or event inside a canvas window, a gutter with syntax error marks, breakpoints and bookmarks, Ctrl+F and Ctrl+H search, F4 snippet pop-up, maximise into its own workspace, and extra menu items (Restore to workspace, Go to Object, Add/Open Event) when detached. Code Editor 2 (beta, opt-in in preferences) is documented below. A separate language server (`GameMakerLanguageServer.exe`) backs it.

Code Editor 2 frame:

- Opens in a new workspace tab. Closing saves.
- Tab context menu: Show in Asset Browser, Show in Inspector, Open in Explorer, Copy Full Path.
- Navigation bar: asset drop-down (Alt+1; Alt+Shift+1 creates an asset), document drop-down (events, moments, vertex/fragment; inherited events marked; Alt+2; Alt+Shift+2 adds), declaration drop-down (locals, functions, other variables, constructor members indented; Alt+3), orientation (attach to any side or float), splitter (drag to split, right-click for vertical split).
- Gutter rails: toggles (breakpoints, bookmarks), contextual (quick-fix icon), line numbers, folding.
- Vertical scrollbar: minimap (small, medium, large or bar), hover preview, diagnostics marks, overscroll.
- Horizontal bar: session zoom, OVR indicator, "Ln, Col" (click for Go To Line, Ctrl+G), language switch (GML Code or GML Visual for the focused event).

Documents: all documents of an asset are shown in one scrolling view by default (all events of an object, both shader stages), each with a sticky header that folds the document. Statement headers can stick too. Inherited parent events are greyed; their header menu opens, inherits or overrides. Alt+Up/Down jumps between documents; Alt+M folds the active document; Alt+Shift+M and Alt+Shift+U fold or unfold all.

Editing features:

| Feature | Keys and behaviour |
| --- | --- |
| Line operations | Ctrl+D duplicate, Ctrl+J join, Ctrl+K comment, Ctrl+Shift+K uncomment, Tab/Shift+Tab indent, cut/copy whole line with no selection. |
| Navigation | Ctrl+Left/Right word, Ctrl+Up/Down scroll, Ctrl+Alt+Up/Down region, smart Home, Ctrl+{ matching brace, Ctrl+Minus and Ctrl+Shift+Minus back and forward through cursor history (shared across editors, reopens closed ones). |
| Multicursor | Ctrl+Alt+Left/Right adds cursors at all matches of the selection, Alt+Shift+Up/Down/Left/Right box selection, Alt+click or Alt+drag column select, Escape clears. |
| Selection | Double-click word, triple-click line, gutter click line, Shift+click extend, drag selection to move it, drag an asset from the Asset Browser to replace the selection with its name. |
| Folding | Functions, braced control blocks, switch and case, braces and begin/end, multi-line comments, `#region`/`#endregion`; Ctrl+M fold all regions, Ctrl+U unfold, Ctrl+Enter toggle. |
| Find and replace | Ctrl+F, Ctrl+H, whole word, case, regex, previous/next, replace next, replace all; Ctrl+Shift+F for project search. |
| Context menu | Go To Declaration (F1; manual page for built-ins; `@url` opens a browser), Preview Declaration (Alt+F1), Find Usages (Shift+F1, scope-aware), Refactor Identifier (Ctrl+R), Insert (UUID, MD5, SHA1, SHA256, to/from Base64), Edit (tab, comment, duplicate, join, sort lines), Regions, Convert case (upper, lower, proper, sentence, inverted, random). |
| Code completion | Candidate window after a delay, initials matching (`sdm` gives `show_debug_message`), Enter inserts with parentheses, contextual argument tooltip, hover tooltips with manual text, sprite thumbnail or colour values. |
| Quick fixes | Ctrl+Q or the gutter feather icon: fix, disable rule, change severity, generate JSDoc. |
| Snippets | Offered in completion; Tab cycles placeholders; custom `.tmSnippet` plist files in `%ProgramData%\GameMakerStudio2\User\` can override built-ins; variables such as CLIPBOARD, CURRENT_YEAR, UUID, RANDOM. The legacy editor uses `snippets.txt` and F4. |
| Indentation | Spaces or tabs, tab size, detect from document, auto-indent mode None, Keep, Braces, Full, auto-indent pasted code. |
| Rendering | Current line highlight, symbol and selection match highlight, brace pair highlight, break/continue target highlight, indent guides, whitespace guides, rainbow brackets, squiggles red/yellow/blue, JSDoc tag, URL, TODO/BUG/HACK and escape highlighting, RTL text, ligatures. |
| Undo | Per document, stack limit default 200 (negative means unlimited). |

Theme: font and size (fixed-width fonts shown in bold), ligatures, theme selector ("GameMaker Dark - Default"), new theme from a TextMate file or as a copy, restore defaults, colour groups General (background, foreground, caret, line highlight, selection), Gutter, and Scopes (foreground, background, bold, italic, underline per TextMate scope, add your own, rainbow levels `punctuation.rainbow.1` to `4`).

Feather: rename refactoring of asset names on Asset Browser rename (toggle), strict type mode, message profile None/SyntaxErrors/TypeErrors/All, per-rule severity, identifier blocklist, naming rules per identifier kind (style UpperCamelCase, lowerCamelCase, ALL_UPPER, all_lower, prefix, suffix, keep underscores), room-instance analysis toggles, parse delay, compact tooltips, JSDoc style for quick fixes.

Relevance to G3MStudio: the code shown is decompiled from CODE bytecode (VM builds only; YYC builds have no GML in the file). Comments, JSDoc, regions, macros and enum names do not survive, so Feather JSDoc and region features have little to work on. What maps: a document-per-event view for objects, the navigation bar (asset, event, declaration), go to definition and find usages built from FUNC/VARI and code cross references, rename of locals and asset names (with the caveat that asset references are indices), folding by braces, multicursor, minimap with diagnostics, TextMate themes. AvaloniaEdit supplies most editor primitives.

### 2.7 Shader Editor

Source: [The_Asset_Editors/Shaders.htm](https://manual.gamemaker.io/lts/en/The_Asset_Editors/Shaders.htm)

Purpose: edit a shader asset with two documents, Vertex and Fragment, both required; new shaders start with pass-through code.

Layout: legacy editor shows two tabs; Code Editor 2 shows both stages in one view with document switching in the navigation bar. Shader language is set from the asset context menu.

| Language | Targets |
| --- | --- |
| GLSL ES 1.0 | All targets (HTML5 needs WebGL on) |
| GLSL | macOS, Ubuntu |
| HLSL 11 | Windows, Xbox |
| PSSL | PlayStation 4 and 5 |

Relevance to G3MStudio: SHDR keeps the shader type, GLSL ES and GLSL source text, attribute names and precompiled HLSL bytecode blobs. Text sources map to a two-tab editor; HLSL bytecode can only be replaced with a recompiled blob, which needs the DirectX compiler.

### 2.8 Font Editor

Sources: [The_Asset_Editors/Fonts.htm](https://manual.gamemaker.io/lts/en/The_Asset_Editors/Fonts.htm), [Font Editor preferences](https://manual.gamemaker.io/lts/en/Setting_Up_And_Version_Information/IDE_Preferences/Font_Editor_Preferences.htm)

Purpose: render an installed font into bitmap glyphs on a texture page.

| Section | Fields |
| --- | --- |
| Details | Name, font family (installed fonts), texture group, Copy To Project (bundle the `.ttf`/`.otf`). |
| Rendering options | Enable SDF, SDF spread. |
| Presets | Size in points (default 12), bold, italic, anti-aliasing. |
| Range controls | Ranges as first and last code points; buttons Normal (32 to 127, the default), ASCII (0 to 255), Digits, Letters; From Code (scan project strings); From File (scan a file); Delete; add or remove glyphs by typing into the preview. Inspector "Select Glyphs" picks glyphs visually. |
| Texture controls | Options dialog: Hinting (default Normal); glyph options Disable Hinting, Disable Auto-Hinter, Prefer Auto-Hinter, No Scale; Interpreter (default TrueType v40); Rounding (default floor); Apply Kerning. Regenerate button. |
| Preview mode | Sample (editable text) or Game Preview (the generated texture). |
| Range preview | Each range with its glyphs. |

Fonts are also regenerated from the Asset Browser. A changed font is not rebuilt until regenerated. If the font is missing on the machine, the cached texture keeps working but only name, family and texture group stay editable. Fonts can be dragged into rooms (asset layer text) and sequences (text tracks).

Relevance to G3MStudio: FONT keeps display name, size, bold/italic, ranges, ascender data, SDF flags, the glyph table (x, y, width, height, shift, offset, kerning) and the texture page entry. A glyph table editor with a page view, plus "regenerate from a TTF with these ranges" in G3MStudio itself, maps well. Hinting choices only matter if G3MStudio re-rasterises.

### 2.9 Timeline Editor

Source: [The_Asset_Editors/Timelines.htm](https://manual.gamemaker.io/lts/en/The_Asset_Editors/Timelines.htm)

Purpose: legacy list of moments (game steps after start), each with code. Superseded by sequence moments.

Layout: Name; moment list with step number and description (from a `/// @description` first line); code editor chained per moment.

Interactions: Shift range select, Ctrl single select. Context menu: Add (next step after the last), Edit, Change Moment (also slow double-click), Duplicate (appends copies), Merge (combines selected moments at the first one), Remove, Cut, Copy, Paste, Select All, Convert to GML Visual / Code. Code Editor 2 lists moments in the document drop-down.

Relevance to G3MStudio: TMLN keeps moment steps and their code event links, so this editor maps fully. Descriptions are comments and are lost.

### 2.10 Object Editor

Sources: [The_Asset_Editors/Objects.htm](https://manual.gamemaker.io/lts/en/The_Asset_Editors/Objects.htm), [Object_Events.htm](https://manual.gamemaker.io/lts/en/The_Asset_Editors/Object_Properties/Object_Events.htm), [Other_Events.htm](https://manual.gamemaker.io/lts/en/The_Asset_Editors/Object_Properties/Other_Events.htm), [Draw_Events.htm](https://manual.gamemaker.io/lts/en/The_Asset_Editors/Object_Properties/Draw_Events.htm), [Async_Events.htm](https://manual.gamemaker.io/lts/en/The_Asset_Editors/Object_Properties/Async_Events.htm), [Parent_Objects.htm](https://manual.gamemaker.io/lts/en/The_Asset_Editors/Object_Properties/Parent_Objects.htm), [Object_Variables.htm](https://manual.gamemaker.io/lts/en/The_Asset_Editors/Object_Properties/Object_Variables.htm), [Physics_Objects.htm](https://manual.gamemaker.io/lts/en/The_Asset_Editors/Object_Properties/Physics_Objects.htm), [Event_Order.htm](https://manual.gamemaker.io/lts/en/The_Asset_Editors/Object_Properties/Event_Order.htm)

Layout: property panel chained to an event list, which is chained to code editors. With Code Editor 2, properties move to the Inspector and events become documents.

Properties:

| Field | Notes |
| --- | --- |
| Name | Alphanumeric plus underscore. |
| Sprite | Picker with Edit Sprite, Add Sprite, Edit Image (Open Prefab for prefab sprites). |
| Collision Mask | Optional other sprite used as mask. |
| Visible | On by default; invisible instances skip Draw but still collide. |
| Solid | Moves the instance back before the collision event runs. |
| Persistent | Survives room changes; Create runs once; layer matching by name or a new `_layer_XXX`. |
| Uses Physics | Opens the physics panel. |
| Managed | Rollback multiplayer (beta). |
| Parent | Parent object picker. |
| Variable Definitions | Opens the Object Variables window. |
| Events | Event list with Add Event. |

Event types:

| Category | Events |
| --- | --- |
| Create, Destroy, Clean Up | One each. |
| Step | Begin Step, Step, End Step. |
| Alarm | Alarm 0 to 11. |
| Draw | Draw Begin, Draw, Draw End, Draw GUI Begin, Draw GUI, Draw GUI End, Pre-Draw, Post-Draw, Window Resize. |
| Mouse | Button down/pressed/released per button, enter, leave, wheel, plus Global Mouse variants. |
| Key Down, Key Pressed, Key Up | Sub-menu per key. |
| Gesture | Tap, drag, flick, pinch, rotate, local and global. |
| Collision | One per other object. |
| Other | Outside Room, Intersect Boundary, view outside/boundary per view, Game Start, Game End, Room Start, Room End, Animation End, Animation Update, Animation Event, Path Ended, User Event 0 to 15, Broadcast Message, Wallpaper events. |
| Asynchronous | Audio Playback, Audio Playback Ended, Audio Recording, Cloud, Dialog, HTTP, IAP, Image Loaded, Networking, Push Notifications, Save/Load, Social, Steam, System. |

Event context menu: Add Event, Show Parent Event (read-only), Open Parent Event, Cut/Copy/Paste Events, Duplicate Event (to another event slot), Change Event (move code to another event), Convert To GML Visual / Code, Delete Event; Ctrl multi-select for delete.

Parenting: inherited events show greyed with a parent icon; clicking shows parent code read-only; right-click offers Open, Inherit (inserts `event_inherited()`) or Override. Parent chains can be deep; cycles are blocked. Code that targets a parent includes children.

Variable Definitions: Add creates a row with name (letter first, up to 64 chars), type and value. Types Real and Integer (optional range slider), String, Boolean, Expression, Asset (with type filter), List (options, optional multi-select gives an array), Colour ($RRGGBBAA or picker). Display Name and description per variable. Children show inherited variables greyed, with Override and Delete-override. Definitions and room overrides compile into pre-creation code that runs before Create.

Physics panel: collision shape editor (circle, box, or convex polygon of 3 to 32 points, path-editor-like), Density (0 makes it static), Restitution, Collision Group, Linear Damping, Angular Damping, Friction, Sensor, Start Awake, Kinematic.

Relevance to G3MStudio: OBJT keeps sprite, mask, parent, visible, solid, persistent, depth, physics fields and shape points, and the event table (type, subtype, code link). An event list plus code view maps fully, including Add, Change, Duplicate and Delete Event. Variable Definitions survive only as compiled pre-creation code, so typed variable rows cannot be rebuilt without decompiling that code. Inheritance display can be computed from the parent chain.

### 2.11 Room Editor

Sources: [The_Asset_Editors/Rooms.htm](https://manual.gamemaker.io/lts/en/The_Asset_Editors/Rooms.htm), [Room_Properties.htm](https://manual.gamemaker.io/lts/en/The_Asset_Editors/Room_Properties/Room_Properties.htm), [Layer_Properties.htm](https://manual.gamemaker.io/lts/en/The_Asset_Editors/Room_Properties/Layer_Properties.htm), [Room_Inheritance.htm](https://manual.gamemaker.io/lts/en/The_Asset_Editors/Room_Properties/Room_Inheritance.htm), [Filters_and_Effects.htm](https://manual.gamemaker.io/lts/en/The_Asset_Editors/Room_Properties/Filters_and_Effects.htm), [All_Filter_Effect_Types.htm](https://manual.gamemaker.io/lts/en/The_Asset_Editors/Room_Properties/FX/All_Filter_Effect_Types.htm), [UI_Layers.htm](https://manual.gamemaker.io/lts/en/The_Asset_Editors/Room_Properties/UI_Layers.htm), [Settings/The_Room_Manager.htm](https://manual.gamemaker.io/lts/en/Settings/The_Room_Manager.htm), [Room Editor preferences](https://manual.gamemaker.io/lts/en/Setting_Up_And_Version_Information/IDE_Preferences/Room_Editor_Preferences.htm)

Purpose: place instances, tiles, backgrounds, sprites, sequences, text, particle systems and paths on layers; set room size, views and physics. The editor is its own workspace tab.

Layout: rulers with draggable guides (snap distance in preferences), canvas (origin at 0,0), Layers panel, Inspector (room, layer or element properties), Element List (for instance, asset and UI layers), layer toolbox (tools for the selected layer type), room toolbox, status bar (mouse position, grid cell, tile index and tool state), Room menu.

Room toolbox: grid toggle (G), snap (Ctrl+G), grid menu (colour, alpha, cell size), zoom controls, Show Views, Play Animations (preview sprites, sequences, animated tiles and particles), Select From Any Layer (hold P to use it once).

Layers panel: Create Layer menu with Instances, Tile Maps, Paths, Assets, Filter/Effect, Background, UI. Folder button, delete button, eye per layer and for all, lock per layer and for all, inheritance toggle at the bottom. Slow double-click renames (layer and instance names must not clash). Drag to reorder, Shift range and Ctrl multi-select. Depth range -16000 to 16000. Context menu: properties, rename, delete, duplicate, add layer, add sub layer, inherit sub layers, inherit visibility.

Layer types and properties:

| Layer | Properties and tools |
| --- | --- |
| Background | Colour (when no sprite), sprite, tile horizontally, tile vertically, stretch, X/Y offset, horizontal and vertical speed, animation speed, inheritance. |
| Instances | Element list of instances. Instance editor: name (default `inst_XXXXXXXX`), inherit, change object, open object, Variables (overrides), colour blend, rotation, flip X/Y, scale X/Y, position, image speed, image index, creation code (runs after Create, own inherit toggle). Unchecking an instance in the list stops it from being exported. Inspector also offers sprite override and alignment. |
| Tile Maps | Tile set, X/Y offset, inherit; Tile Editing window with tiles, brushes, animated tiles and auto tiles. Tools: Pencil (right button erases), Eraser, Fill, Line, Rectangle, Selection (Ctrl add, Alt subtract, cut/copy/paste turns into a brush), AutoTile, Flip (X), Mirror (Y), Rotate (Z). |
| Paths | Path asset picker, smooth or straight, closed, precision; edit points in the room; context menu reverse, flip, mirror. |
| Assets | Element list of sprites, sequences, text and particle systems. Sprite: name, blend colour, rotation, scale, flip, animation speed, frame. Sequence: speed, start frame, colour, position. Text: string, colour, rotation, scale, frame origin, origin offset, frame size, wrapping and alignment. Particle system: blend colour, rotation. Export checkbox per element. Ctrl (Cmd on macOS) disables snapping while placing. |
| Filter/Effect | Effect type and its parameters; applies to every layer below. Any layer can also get a single-layer effect in the Inspector. 41 types: blend, extended blend, gaussian blur, glow, recursive blur, windblown particles, blocks, boxes, clouds, colour balance, colourise, contrast, distort, dots, edge detect, fractal noise, gradient, greyscale, hard drop shadow, heat haze, hue, large blur, linear blur, LUT colour, mask, old film, outline, panorama, parallax, pixelate, posterise, RGB noise, ripples, screen shake, stripes, tint, twirl distort, twist blur, underwater, vignette, white noise, zoom blur. |
| UI | Global layers shared by all rooms in a "UI Folder"; flex panels holding sprites, objects and text; main display or per viewport. |

Room properties (Inspector): Persistent, Clear Display Buffer, Width, Height, Creation Code (with inherit toggle), Instance Creation Order button, Enable Viewports, Clear Viewport Background, and 8 views each with Visible, camera X/Y/W/H, viewport X/Y/W/H, object following with horizontal and vertical border and speed (-1 means instant); Room Physics with Enable Physics, gravity X/Y, pixels to metres. Every section has an inheritance toggle.

Instance Creation Order: a drag-reorder list of all instances (and UI layer instances for the first room, with move-all buttons). Creation order does not fix event order.

Inheritance: child rooms (Create Child Room) clone the parent and stay linked; inherit buttons light up per section, layer and element; editing an inherited value breaks inheritance for that section. Room Manager has an Inheritance tab where dragging one room onto another makes it a child, plus a Room Order tab (drag, Shift/Ctrl multi-select, move buttons, Create Group, search, right-click Create Child Room, Edit, Edit Tags).

Room menu: Layer View, Instance Creation Order, Reset Windows On Current Desktop, Disable/Enable Filters Preview, Tile Editing (clear, invert, cut, copy, paste selection), Convert Image To Tile Map (cell size, offsets, sprite width, remove colour, names; builds a deduplicated tile set, sprite and tile layer).

Other interactions: drag objects, sprites, sequences, fonts or particle systems from the Asset Browser onto the canvas; hold Alt with an asset selected to preview and paint copies; Ctrl multi-select; drag edges and corners to scale or rotate single elements or groups; double-click an element to pan and open it. Preferences: default room size, default layers, depth spacing, chain fade, master volume, default text, smart guides, rulers, snap distance, and many automatic responses (layer depth update, locked layer or item, parent room deletion, invalid drags, create a new layer when dropping on an unsuitable one).

Relevance to G3MStudio: ROOM keeps size, speed, persistence, colour, views, physics, creation code link, the layer list with depth and type data, instances (with order, creation code, scale, angle, colour, frame), tiles, background settings, asset-layer sprites and sequences, and effect data (FEDS in newer versions). That supports a real visual room editor: layers panel with visibility and lock (lock is G3MStudio-only), element list with search, instance inspector, creation order list and tile painting with X/Y/Z keys. Room inheritance is flattened at build, so inherit toggles do not apply. "Export this instance" maps to deleting it.

### 2.12 Sequence Editor

Sources: [The_Asset_Editors/Sequences.htm](https://manual.gamemaker.io/lts/en/The_Asset_Editors/Sequences.htm), [The_Sequence_Canvas.htm](https://manual.gamemaker.io/lts/en/The_Asset_Editors/Sequence_Properties/The_Sequence_Canvas.htm), [The_Track_Panel.htm](https://manual.gamemaker.io/lts/en/The_Asset_Editors/Sequence_Properties/The_Track_Panel.htm), [Using_The_Dope_Sheet.htm](https://manual.gamemaker.io/lts/en/The_Asset_Editors/Sequence_Properties/Using_The_Dope_Sheet.htm), [Using_Animation_Curves.htm](https://manual.gamemaker.io/lts/en/The_Asset_Editors/Sequence_Properties/Using_Animation_Curves.htm), [Broadcast_Messages.htm](https://manual.gamemaker.io/lts/en/The_Asset_Editors/Sequence_Properties/Broadcast_Messages.htm), [Clipping_Masks.htm](https://manual.gamemaker.io/lts/en/The_Asset_Editors/Sequence_Properties/Clipping_Masks.htm), [Text_in_Sequences.htm](https://manual.gamemaker.io/lts/en/The_Asset_Editors/Sequence_Properties/Text_in_Sequences.htm), [Sound_in_Sequences.htm](https://manual.gamemaker.io/lts/en/The_Asset_Editors/Sequence_Properties/Sound_in_Sequences.htm)

Purpose: keyframe animation of sprites, objects, sounds, nested sequences, particle systems and text.

Layout: canvas (top by default) with rulers and guides, keyframe controls, canvas toolbox, sequence tools bar, track panel and dope sheet (bottom). An orientation button cycles Split Window (dope sheet in a second OS window), Snap Top, Right, Bottom (default) and Left; Reset restores the layout.

Canvas: drop assets to create tracks; transform gizmo at each asset origin switching between position, rotation, scale and origin modes; bounding box handles; smart guides; Toggle Gizmos; canvas frame with size and an optional reference image (not imported); grid and zoom; Add Text Track tool.

Keyframe controls: Split selected keys, Delete selected keys, Record a new key, Automatically Record Changes (auto-key).

Sequence tools bar: playback speed (frames per second), playhead position, time display h:m:s or frames, Broadcast Message (add at playhead), Add Moment (function with no arguments called at that frame; go-to-script and create-script buttons), Open Curve Editor, playback controls with loop and ping-pong, sequence length. Clip region start and end limit what plays.

Sequence events: Create, Destroy, Clean Up, Begin Step, Step, End Step, Async; each bound to a script function name, with a Create New button.

Track panel: drag to reorder; add track button; group folders; Cut, Copy, Paste, Duplicate Track(s), Delete Track(s), Group, Rename. Parameter tracks per asset type:

| Asset | Parameters |
| --- | --- |
| All | Position, Rotation |
| Sprite, object | Origin, Scale, Image Index, Image Speed, Colour Multiply |
| Sequence | Origin, Scale |
| Particle system | Colour Multiply, Origin, Scale |
| Sound | Volume, Pitch, Falloff, Bus, audio effects |
| Text | Origin, Scale, Colour Multiply, character spacing, frame size, line spacing, paragraph spacing, SDF effects |

Dope sheet: drag keys, drag the right edge of an asset key to change length. Asset key menu: Cut/Copy/Paste, Add Key In Place, Split Key In Place, Paste Key(s) In Place, Delete Key(s), Copy To Start, Copy To End, Stretch Asset Key, Delete Asset Key. Parameter key menu adds Convert To Embedded Anim Curve, Set To External Anim Curve, Convert To Keys, Export Embedded Anim Curve, Open Editor, Stretch Parameter Key, Delete Parameter Key.

Curves: embedded (per track) or shared (an Animation Curve asset). Channels per parameter: Position 2, Rotation 1, Colour Multiply 4 (0 to 255), Image Index 1, Image Speed 1, Origin 2, Scale 2, Pitch 1, Volume 1 (0 to 1). Curve menu: Set Range, Convert To Curve, Embed Curve, Assign Shared Curve, Convert To Keys, Export to Shared Curve.

Broadcast messages and moments: one message per frame; overlapping icons are picked from a list on right-click; a message and a moment may share a frame. Messages also exist on sprites.

Relevance to G3MStudio: SEQN keeps playback settings, length, tracks with parameter tracks and keyframes, moments and broadcast messages, so a dope sheet viewer and key editor map fully. Embedded curves and references to ACRV map too. The canvas preview needs a sprite renderer but no new data.

### 2.13 Animation Curve Editor

Sources: [The_Asset_Editors/Animation_Curves.htm](https://manual.gamemaker.io/lts/en/The_Asset_Editors/Animation_Curves.htm), [Animation_Curve_Library.htm](https://manual.gamemaker.io/lts/en/The_Asset_Editors/Animation_Curve_Properties/Animation_Curve_Library.htm), [Anim Curve Editor preferences](https://manual.gamemaker.io/lts/en/Setting_Up_And_Version_Information/IDE_Preferences/Anim_Curve_Editor_Preferences.htm)

Layout: Name; Curve Library button (curve type and presets, "Overwrite" apply mode); channel list (add, rename, expand to edit points numerically, eye toggle, bin); Channel Range button (visual range only, default -1 to 1, no clamping); channel editor (all channels drawn, only the selected one editable); zoom expand, contract and Center Fit.

Interactions: click near the line to add a point, drag points, marquee or Ctrl multi-select and drag together, right-click empty area for presets on the whole channel (Bezier only), right-click a segment or a selection of segments to apply a preset "Between" points. Interpolation linear, smooth (Catmull-Rom) or Bezier. Hotkeys: Ctrl+D duplicate, Ctrl+A add curve, Ctrl+R set range.

Relevance to G3MStudio: ACRV keeps channels, curve type, iterations and points (with Bezier handles in newer versions). A small graph editor with numeric point table maps fully.

### 2.14 Particle System Editor

Sources: [The_Asset_Editors/Particle_Systems.htm](https://manual.gamemaker.io/lts/en/The_Asset_Editors/Particle_Systems.htm), [Emitter_Properties.htm](https://manual.gamemaker.io/lts/en/The_Asset_Editors/Particle_System_Properties/Emitter_Properties.htm), [Particle Editor preferences](https://manual.gamemaker.io/lts/en/Setting_Up_And_Version_Information/IDE_Preferences/Particle_Editor_Preferences.htm)

Layout: canvas with live preview of all emitters and their regions; top-left Origin, Draw Order (old to new or new to old) and Copy GML to Clipboard; top-right toolbox (grid, zoom, canvas settings: frame on/off, origin, size default 1366x768, background reference image); emitter list (Add, Delete, eye to pause, padlock, dockable to any side). Emitter properties live in the Inspector.

Emitter properties:

| Group | Fields |
| --- | --- |
| Emitter settings | Enabled, Preview, Preview Colour, Mode (Stream or Burst), Particle Count (negative values give a chance per step), Delay and units, Interval and units, Distribution, Shape (rectangle, ellipse, diamond, line), region. |
| Particle settings | Library (preset link), Textures, Colour, Life, Scale, Size, Speed, Gravity, Direction, Orientation, Subparticles (On Death, On Update). |

Emitter context menu: delete, cut, copy, duplicate, rename, unlink. Presets: "Save as Preset" stores an emitter in the project library; linked emitters share particle settings (emitter settings are saved only at creation); editing a linked preset updates every linked emitter; the link button unlinks. Built-in presets cannot be changed.

Relevance to G3MStudio: PSYS and PSEM keep the system and emitter data, so an emitter list plus property grid maps well. "Copy GML to Clipboard" is a good export feature for modders. A live preview needs a particle simulator, which is optional.

### 2.15 Extension Editor

Sources: [The_Asset_Editors/Extensions.htm](https://manual.gamemaker.io/lts/en/The_Asset_Editors/Extensions.htm), [Creating_An_Extension.htm](https://manual.gamemaker.io/lts/en/The_Asset_Editors/Extension_Creation/Creating_An_Extension.htm)

Purpose: wrap GML, DLL/dylib/so, JavaScript, Java or Objective-C code and constants as an asset.

Layout: name, version, target platform checkboxes, file list (each file with its own platform targets, functions and constants, proxy files), extension options panel at the bottom (defined by the extension author), "Uncompress on build" for platform archives. Functions have external name, GML name, calling convention, argument types and return type; constants have name and value. Importing goes through the Marketplace library or dropping a `.yymps`, `.yymp` or `.gmez` package.

Relevance to G3MStudio: EXTN keeps extension names, files, kinds, functions (names, IDs, argument and return types) and constants. A read-mostly tree with function signature editing maps well; native files are next to the game, not inside the data file.

### 2.16 Notes

Source: [The_Asset_Editors/Notes.htm](https://manual.gamemaker.io/lts/en/The_Asset_Editors/Notes.htm)

Purpose: free text inside the project. Imported old projects get a Compatibility Report note. Inspector option "Open On First Load" (on automatically for a note named readme) opens it rendered when someone imports the project. Code Editor 2 shows a Markdown preview pane beside the text (collapsible, "Open Preview" to restore). Extras: `@AssetName` previews sprites, objects, sounds, shaders and fonts; `#RRGGBB` shows a colour; links with `file:///${project_dir}/...` and `project://asset_name` open files or assets. HTML is not supported.

Relevance to G3MStudio: notes are not compiled into the data file. G3MStudio could keep its own Markdown notes in the sidecar project with `project://`-style links to entries, which helps modders document changes.

### 2.17 Prefab Library

Sources: [IDE_Tools/Prefab_Library.htm](https://manual.gamemaker.io/lts/en/IDE_Tools/Prefab_Library.htm), [Prefab Library preferences](https://manual.gamemaker.io/lts/en/Setting_Up_And_Version_Information/IDE_Preferences/General/Prefab_Library_Preferences.htm)

Purpose: use assets from installed source projects ("Collections") by reference; they are pulled in at build time. Installed through the Package Manager ("Prefabs" source). Some collections are needed by the IDE (Filters and Effects).

Layout: folder tree (left) and content view (right, grid or list, path bar); view modes Horizontal, Vertical, Simple, Tree; search; Sort/Filter menu (A-Z, Z-A, used collections, used prefabs, customised prefabs, filters and effects, recent, favourites, authors, asset type, tags); Package Manager button; item count bar.

Use: drag prefab sprites into object, sequence, room, tile set and particle editors; sounds into sequences; tile sets into rooms. Prefab use shows an orange highlight and "Open Prefab". Code references use `::package-version::asset` (version can be partial). Scripts can `#export` enums, macros and functions; projects can override exported macros per config. Customise (keeps the link, modified properties stored in the project) or Duplicate into Project (unlinks). On `.yyz` export, choose whether to embed the collections.

Context menu: Make Favourite, Add/Remove Collection Reference, Customise, Reinstall, Upgrade, Uninstall, Duplicate into Project, Show In Inspector, Change Prefab Collection Versions, Show in Package Manager, Expand/Collapse Children, Expand/Collapse All.

Relevance to G3MStudio: at build time prefab assets become ordinary entries in the data file, so the library itself does not apply. Its browser layout (tree plus content grid, view modes, favourites, filters by type and tag) is a good model for an "asset gallery" view of sprites and sounds.

---

## 3. Workflows

Sources: [Introduction/The_Asset_Browser.htm](https://manual.gamemaker.io/lts/en/Introduction/The_Asset_Browser.htm), [The_File_Menu.htm](https://manual.gamemaker.io/lts/en/IDE_Navigation/Menus/The_File_Menu.htm), [General preferences](https://manual.gamemaker.io/lts/en/Setting_Up_And_Version_Information/IDE_Preferences/General_Preferences.htm), [IDE_Tools/The_File_Watcher.htm](https://manual.gamemaker.io/lts/en/IDE_Tools/The_File_Watcher.htm), [File Watcher preferences](https://manual.gamemaker.io/lts/en/Setting_Up_And_Version_Information/IDE_Preferences/General/File_Watcher.htm), [Confirm Dialogs preferences](https://manual.gamemaker.io/lts/en/Setting_Up_And_Version_Information/IDE_Preferences/General/Confirm_Dialogs.htm), [Paths preferences](https://manual.gamemaker.io/lts/en/Setting_Up_And_Version_Information/IDE_Preferences/General/Paths.htm), [Sprite_Strips.htm](https://manual.gamemaker.io/lts/en/The_Asset_Editors/Sprite_Properties/Sprite_Strips.htm), [Feather_Features.htm](https://manual.gamemaker.io/lts/en/The_Asset_Editors/Code_Editor_Properties/Feather_Features.htm)

### 3.1 Importing images, strips and audio

| Route | Result |
| --- | --- |
| Sprite Editor Import | One or many files become frames; PNG, JPG, GIF, SVG/SWF, Spine JSON. Confirms before overwriting existing frames (preference). |
| Asset Browser "Create Sprite From Image(s)" | New sprite with one frame per file. |
| Strip files named `name_stripN.png` | Split into N frames horizontally; `_stripN` is dropped from the name; frame width is image width divided by N. Works by Import, by drag onto the IDE, and at runtime with `sprite_add`. |
| Image Editor Import Strip Image or Convert to Frames | Dialog with frame count, frame width and height, offsets. |
| Inspector "Import Strip" | Opens Convert to Frames for a chosen sheet, bitmap or SVG. |
| Image Editor Import Image(s) | Dialog to resize the canvas, crop or pad, and pick an anchor. |
| Sound Editor Open | WAV, MP3, OGG. |
| Room "Convert Image To Tile Map" | Builds sprite, tile set and tile layer from a picture, without duplicate tiles. |

Export: Image Editor "Export to PNG" writes one PNG, or `name_stripN.png` for several frames.

### 3.2 Dragging files into the IDE

Image and sound files dropped on the workspace create new assets and open their editors (preference "Open new assets for edit on dragging files into IDE"). Images dropped on a sprite's frame view add frames. Other files dropped on the IDE are offered as Included Files. `.yymps`, `.yymp` and `.gmez` packages dropped on the IDE start the package import. Assets dragged from the Asset Browser onto the workspace open their editor at the drop point; onto a room or sequence canvas they create instances, elements or tracks; into Code Editor 2 they insert their name.

### 3.3 Renaming with reference updates

Rename by slow double-click, the context menu Rename, or the Name field in an editor. Asset names must be alphanumeric plus underscore and not start with a digit; folder names are free. With Feather enabled and "Refactor references to asset name when asset is renamed in Asset Browser" on, every code mention of the asset is updated. Code Editor 2 can rename any identifier and all references with Ctrl+R (legacy editor: Ctrl+Shift+R).

### 3.4 Duplicating

Ctrl+D or context menu Duplicate copies selected assets. Events, timeline moments, sequence tracks, keys, emitters, layers, frames and code lines each have their own duplicate commands in their editors.

### 3.5 Deleting with references

Asset Browser Delete removes the asset and cannot be undone. It also removes room and UI layer elements created from that asset (placed instances, sprites, text). Other references (for example a sprite name in code) are left to break and show up as errors. A confirmation appears unless its automatic response is set to OK. Deleting a room group lists the rooms that will go with it. Deleting a texture group moves its assets to Default and can be undone.

### 3.6 Undo and redo scope

Ctrl+Z and Ctrl+Y work per editor with multiple levels. Code Editor 2 keeps one stack per document (default limit 200). Asset deletion is not undoable; texture group deletion is. GML Visual to code conversion and some timeline actions warn that they cannot be undone. There is no global, cross-editor history.

### 3.7 Saving, autosave and backups

The IDE saves changes to disk automatically as you edit; the Save button and Ctrl+S force a save. Closing a Code Editor 2 window saves its documents. There is no backup or versioning system beyond exporting a `.yyz` ("useful for saving backups") and source control. Cloud storage paths trigger a warning because syncing can corrupt saves.

### 3.8 File watcher and reload

The File Watcher tracks project files for external changes (an image edited in another program, a git pull). On a change it shows "Project Directory Modified" listing the changes, with Reload (disk wins) or Save (memory wins). "Automatically reload changed files" skips the dialog. Settings: enable, tolerance after an IDE save (default 2000 ms, 1 to 10000) to ignore its own writes, action delay after file system changes (default 1000 ms). External editors per file type (bitmap, Spine, SVG/SWF, sound, included files) are set in Paths preferences, and "Open in External Editor" uses them.

### 3.9 Confirmation dialogs

Nearly every warning has an "Automatic response" preference (Show Message, Yes or OK, No) in a Confirm Dialogs page per editor. "Don't ask again" is therefore always reversible from preferences.

Relevance to G3MStudio: import by strip naming, frame sheet slicing, drag-and-drop import, duplicate and delete all map to data-file entries. Delete needs a reference scan first, because entries are referenced by index and removing one shifts others; offering "delete and list what still references it" beats GameMaker's silent breakage. Rename with reference updates is easy for asset names (code uses indices, so only strings and debug names change). G3MStudio should add what GameMaker lacks: one global undo history across editors, explicit save with automatic backup of the original data file, and a file watcher for exported entries edited externally.

---

## 4. Top 20 ideas for G3MStudio

Ranked by value for editing compiled data files against build cost.

| Rank | Idea | Source editor | Why |
| --- | --- | --- | --- |
| 1 | Property grid with multi-select, "-" for mixed values, apply to all, lock and multiple instances | Inspector | Batch edits (texture group, audio group, flags) across hundreds of entries are the main data-file chore. |
| 2 | Go To palette (Ctrl+T) over entries, strings, functions, variables, options and settings, with per-source toggles | Go To window | Data files have thousands of unnamed-feeling entries; jumping by name beats tree browsing. |
| 3 | Find Usages and a "used by" panel for every entry | Code Editor 2 Find Usages | Index-based references are invisible in a data file; showing them prevents broken deletes and reorders. |
| 4 | Project-wide search and replace over decompiled code with type filter, whole word, case, preview and one-step undo | Search & Replace | Most mods change code; batch replace needs safety that GameMaker does not give. |
| 5 | Load-time health report listing broken references and missing external files without refusing to open | Project Health | Modded or damaged data files are common; a list beats a crash. |
| 6 | Problems panel with severity toggles, counts, search, sortable Code/Message/Resource/Line columns | Feather Messages | Gives decompile failures and validation results one familiar home. |
| 7 | Texture page viewer plus texture group window with member list, preview and "move to group" | Texture Groups, Game Options Preview, debugger Textures pane | TPAG/TXTR layout is the hardest part of a data file to understand without seeing it. |
| 8 | Sprite frame timeline with drag reorder, stretch, broadcast messages, origin presets, click-to-set origin and mask handles | Sprite Editor | SPRT fields map one to one; this is the most used asset editor. |
| 9 | Frame tools: import strip by `_stripN`, convert sheet to frames, export strip PNG, auto trim, resize all, reverse, rotate, flip | Image Editor | Covers real sprite edits without building a paint program. |
| 10 | Edit externally and reload on save, with a reload/keep dialog and an auto-reload option | File Watcher, External Editors | Lets users keep Aseprite or Audacity for pixels and audio. |
| 11 | Room editor with layers panel (eye, lock), element list with search, instance inspector, creation order list, select from any layer (P) | Room Editor | ROOM data is rich and currently hard to edit in UndertaleModTool. |
| 12 | Object view with event list (add, change, duplicate, delete event) and all events in one scroll view with sticky headers | Object Editor, Code Editor 2 | Matches how GameMaker users think about objects and shows OBJT plus CODE together. |
| 13 | Code editor with navigation bar (asset, event, declaration), folding, multicursor, minimap with diagnostics, TextMate themes | Code Editor 2 | Familiar to GameMaker users; AvaloniaEdit covers most of it. |
| 14 | Asset import conflict dialog with Replace, Keep, Rename per entry and all-at-once buttons | Local Asset Packages | Needed when importing entries from another data file or mod pack. |
| 15 | Sidecar project file for folders, colours, favourites, tags filters and saved searches | Asset Browser | The data file has no organisation layer; users still want one, and it must not touch the file. |
| 16 | Automatic-response preferences for every confirmation | Confirm Dialogs | "Don't ask again" stays reversible, which reduces support questions. |
| 17 | Sound panel with waveform, original and export size, replace file, re-encode, plus a mixer for batch volume | Sound Editor, Inspector, Sound Mixer | SOND/AUDO edits are frequent in mods; size figures show the cost of a change. |
| 18 | Tile set view with grid and tile indices, animated tile table, and room tile painting with X/Y/Z flip, mirror, rotate | Tile Set Editor, Room Editor | BGND and tile layers map directly; the keys are already muscle memory for GameMaker users. |
| 19 | Dope sheet and track panel viewer for sequences, with key editing and curve view | Sequence Editor, Animation Curve Editor | SEQN and ACRV are opaque in current tools; even a read-only timeline helps. |
| 20 | Searchable hotkey table with record-by-keypress, chords, import and export | Redefine Keys | GameMaker's 514 defaults can be offered as a preset so users keep their habits. |

Ideas left out on purpose: the chained free-form workspace canvas (high cost, divisive), GameMaker-style source control on the data file itself (git on a binary file gives little; G3MStudio puts Git on the project folder instead, see decisions/0007-git-unit.md, and uses snapshots for the data file), live debugger panes (no debug link to a shipped game), Feather JSDoc and naming rules (comments do not survive compilation), configurations and per-platform options (the data file holds one build).
