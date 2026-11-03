# GameMaker LTS 2026: hands-on findings

IDE v2026.0.0.16, runtime v2026.0.0.23, Windows 11, project "TEST" (one room). Explored on 2026-10-25. Per-editor detail from the manual is in [editors.md](editors.md). Default hotkeys: [gamemaker-hotkeys.md](gamemaker-hotkeys.md) (514 commands, 34 contexts). IDE terms in 10 languages: [gamemaker-terminology.md](gamemaker-terminology.md). Screenshots: [screenshots/](screenshots/) (local only, not for the public repo).

## Shell

![Main window](screenshots/01-main-window.jpg)

- Menu bar: File, Edit, Build, Windows, Tools, Marketplace, Layouts, Help, Source Control. Editors add their own menus while focused (the image editor adds Image, View, Effects). Right side: IDE and runtime versions, account, detach button.
- Toolbar: start page, new, open, save, create executable, import, debug, run, stop, clean, game options, help, zoom out/reset/in/fit. Right: target bar "Windows | GMS2 VM | Default | Default" (platform, output, device, configuration) and target settings.
- Three docks (left Inspector, right Asset Browser, bottom output panes) around a Workspace canvas. Every dock is a tab strip with a "+" to add a pane. Edge chevrons collapse a dock; F12 collapses all, Shift+F12 forces them open.
- Bottom panes: Output, Search Results, Source Control, Breakpoints, Compile Errors, Feather Messages, Prefab Library.
- Workspaces: several workspace tabs ("Workspace 1", "+"). Editors open as floating windows on a pannable, zoomable canvas inside a workspace, with chain links between related editors. Some editors (image editor) open as their own top-level tab instead.
- Inspector: shows the selected asset's main properties with search, match counter, lock (keep showing this asset) and menu. Most simple edits never need the full editor.
- Asset Browser: search, create (+), filter, menu. "Quick Access" group: Recent, Favourites, Room Order, Saved Searches, Tags, Game Options, Prefabs. Footer: item count, selection count, zoom slider. The first room has a home icon.
- Create Asset popup: 15 types (Animation Curves, Extensions, Fonts, Notes, Objects, Particle Systems, Paths, Rooms, Scripts, Sequences, Shaders, Sounds, Sprites, Tile Sets, Timelines), a count spinner to create several at once, grouped/grid/list views. Each type also has a direct shortcut (Right Alt + letter).
- Feather Messages: toggle counts for Errors, Warnings, Suggestions; search; sortable Code, Message, Resource, Line, Column.

### Menus

| Menu | Items |
| --- | --- |
| File | New Project Ctrl+N, Open Project Ctrl+O, Import Project, Recent Projects, Save Project Ctrl+S, Save Project As Ctrl+Shift+S, Export Project (YYZ, As Template), New IDE, Preferences Ctrl+Shift+P, Exit |
| Edit | Undo Ctrl+Z, Redo Ctrl+Y, Search & Replace Ctrl+Shift+F |
| Build | Run F5, Debug F6, Re-Run, Clean Ctrl+F7, Clean Code, Clean Graphics, Clean Audio, Create Executable Ctrl+F8, Create Executable and Launch |
| Windows | Asset Browser, Output, Search Results, Source Control Output, Breakpoints, Compile Errors, Feather Messages, Inspector, Recent Windows, IDE Environment Variables, Prefab Library, Project Health, Project Tool Output |
| Tools | Texture Groups, Audio Groups, Sound Mixer, Generate Project Images, Create Local Package, Import Local Package, Package Manager, Project Tool |
| Layouts | Save Layout, Load Layout, Reset Layout, Show All Windows, Close All Windows |
| Help | Open Manual F1, Welcome Tab, Knowledge Base, Contact Us, Upload a Bug/Ticket Sample, Report a Bug, Open Log In Explorer, GameMaker.io, Community, Release Notes, Required SDKs, Open Project In Explorer, Refresh System Fonts, Licences, Check for Updates, About |
| Source Control | Create Project Repository, Clone Repository, Commit Changes, Push Changes, Pull Changes, View History, Show Conflicts |

Screenshots 02 to 11.

## Sprite editor

![Sprite editor](screenshots/13-sprite-editor.jpg)

- Left column: name; size with a resize button; Edit Image; Import; collapsible Texture Settings (tile horizontally and vertically, separate texture page, premultiply alpha, edge filtering, texture group), Collision Mask (mode automatic, full image or manual; type rectangle, rotated rectangle, ellipse, diamond, precise, precise per frame; tolerance; bounds), Nine Slice (enable, four guides with colours, highlight mode, selected slice, tile mode).
- Top bar: playback speed with unit (frames per second or per game frame), origin x and y with lock and preset menu.
- Canvas with a floating zoom toolbar. Nine slice splits it into edit and live preview.
- Bottom: current frame, frame length, transport, loop, and a sequence-style timeline (sprites are sequences internally).

Screenshots 13 to 16.

## Image editor

![Image editor](screenshots/17-image-editor.jpg)

- Opens as a top-level tab and marks the project dirty. Frame strip with add frame, onion skin, loop and play.
- Tool options bar: colour, size slider and spinner, smoothing.
- Right column: Brushes (10 presets), Colours (palette, custom swatches, palette menu), primary and secondary colour with a mouse-button diagram and swap, Toolbox (paint, erase, fill, line, rectangle, ellipse, polygon, arc, text, picker, rectangle select, lasso, magic wand, rotate, flip, mirror, move), Layers with visibility and lock.
- Status bar shows size, frame and a context hint ("Click to paint (Shift for a straight line, Ctrl to pick a colour)"). Tooltips show tool name and shortcut.
- Image menu: selection clipboard, select all, cancel and invert selection, add, insert and delete frames, import images, convert to frames, import strip, export to PNG, resize all frames, crop to selection, auto trim, reverse, mirror, flip, rotate 90. View: 1:1, fit, grid, onion skin settings. Effects: Blur, Greyscale.

Screenshots 17 to 20.

## Preferences

![Preferences](screenshots/22-prefs-general.jpg)

- Non-modal window: the IDE keeps working behind it. Tree of about 30 pages, each with "Restore defaults"; footer Import Preferences, Apply, OK, Cancel.
- Pages: General Settings (Background, Compiling, Confirm Dialogs, Debugging, Display, File Watcher, Go To, Help, Input, Opera GX Environment, Output Windows, Paths, Power Saving, Proxy Settings, Recent Windows, Resources, Workspace), Start Page, Image Editor, Sprite Editor, Object Editor, Script Editor, Font Editor, Room Editor, Spline Editor, Tile Sets Editor, Timelines, Extension Editor, Text Editors, Languages, Feather Settings, Platform Settings, Plugins, Runtime Feeds, Redefine Keys, GMRT, Anim Curve Editor, Asset Browser, Certificate, Code Editor 2 (Beta), Inspector, Package Manager, Particle Editor, Prefab Library, Sequences.
- General: IDE font and fallback font, IDE language, editor frame rate cap (60), skin, default build target, notifications with timeout, transition animations, open assets on creation and on drag-in, automatic reload of changed files, case-insensitive project files, verbose logging, VSync, DPI override.
- Confirm Dialogs: every confirmation has an "automatic response" choice (ask, always yes, always no). "Don't ask again" stays reversible in one place.
- File Watcher: external changes reload after a configurable delay; changes right after an IDE save are ignored for a tolerance window.
- Input: double-click time, scroll speed, pan cursor wrap, wheel zoom needs Ctrl, tab drag distance to undock.
- Output Windows: colour, font, size and anti-aliasing per output pane.
- Paths: temp and cache folders with "delete" buttons, external editors per file type.
- Redefine Keys: searchable Command and Keys table grouped by context, search by pressing the key, filter, import and export. Stored as JSON per context with macOS overrides and a localisation key per command.

Screenshots 21 to 31.

## Install structure worth copying

- Each editor is a separate plugin folder (54 GUI folders such as RoomEditorGUI, SoundEditorGUI). Hotkeys are data, one JSON per editor context. G3MStudio's `IEditorProvider` plus command registry gives the same separation without a plugin loader.
- Localisation packs are plugins: CSV with Name, English, Translation, Restrictions, Comment, and a priority so packs can override each other. 11 IDE languages.
- The code editor uses a separate language server process (GameMakerLanguageServer.exe).

## What to take, what to avoid

Take:
- Inspector that edits the selected resource without opening an editor.
- Quick Access group (recent, favourites, saved searches, tags) at the top of the resource tree.
- Count spinner and per-type shortcuts for fast creation.
- Context hints in the status bar for the active tool.
- Editor-specific menus that appear while the editor has focus.
- Reversible "automatic response" setting for every confirmation.
- Hotkeys as data with per-platform overrides and localisation keys.

Avoid:
- Floating editor windows on a pannable canvas as the default. They hide content and need manual arranging; tabs and splits are faster for data files. Offer a canvas view only if users ask.
- Panels that ignore fast clicks (input handled per frame). Avalonia's event-driven input does not have this problem; keep it that way.
- Non-modal Preferences that can lose input state. G3MStudio settings open as a normal editor tab.
- Literal or wrong translations (examples in gamemaker-terminology.md: ru "Браузер актива", ru "Точки разрыва" for breakpoints, pl "Dziecko" for paths, fr "Ombre" for shader).

## Not covered hands-on

Object, room, code, sound, font, path, tile set, sequence, particle, animation curve, shader, timeline, extension and note editors, Game Options, Search & Replace, Source Control and the debugger. Automation stopped when the Preferences window stopped accepting clicks and Escape. These are covered from the manual in [editors.md](editors.md). A second hands-on pass is worth doing with the user present.

## Hands-on pass 2 (2026-10-25, morning)

Input notes for automation: plain clicks open popups and menus; panel buttons sometimes need a slow press (about 150 ms). Fast double-click selects but does not open; use the Inspector's "Open Editor". Right-click context menus did not open from synthetic input. GameMaker draws everything in one SDL window, so popups are not separate windows.

### Start page (32)

Header "Welcome To GameMaker", a 4-step Set Up Wizard banner (close, don't show again), New / Open / Import / Tutorials, recent projects with grid/list toggle and full path, news tiles on the right.

### Object editor (33 to 43)

- The Inspector shows the object without an editor: sprite picker with "No Sprite", new sprite and edit sprite buttons, collision mask ("Same As Sprite"), Visible, Solid, Persistent, Uses Physics, event list with Add Event and Open Editor. Empty state text explains where to add events.
- The editor window: Name, Sprite (preview, browse, new, edit), Collision Mask, the four flags, then buttons that open chained windows: Events, Parent, Physics, Variable Definitions. Chained windows are linked by a curve on the canvas.
- Add Event menu (14 groups): Create, Destroy, Clean Up, Step (Step, Begin Step, End Step), Alarm (0 to 11), Draw (Draw, Draw GUI, Draw Begin, Draw End, Draw GUI Begin, Draw GUI End, Pre-Draw, Post-Draw, Window Resize), Mouse (left/right/middle down, pressed, released, No Mouse Input, Mouse Enter, Mouse Leave, wheel up/down, Global submenu), Key Down / Key Pressed / Key Up (arrows, Control, Alt, Shift, Space, Enter, Keypad, Digits, Letters, Function Keys, Others, No Key, Any), Gesture (Tap, Double Tap, Drag Start, Dragging, Drag End, Flick, Pinch Start/In/Out/End, Rotate Start/Rotating/End, Global), Collision (one entry per object), Other (Outside Room, Intersect Boundary, Views, Game Start, Game End, Room Start, Room End, Animation End, Animation Update, Animation Event, Path Ended, User Events, Broadcast Message, Wallpaper Subscription Data), Asynchronous (Audio Playback, Audio Playback Ended, Audio Recording, Cloud, Dialog, HTTP, In-App Purchase, Image Loaded, Networking, Push Notification, Save/Load, Social, Steam, System).
- The first event in a project asks for the scripting language (GML Code or GML Visual), with "don't ask again" per project or forever (44).

### Code editor (45 to 46)

- Each event opens as a tab in a chained code window ("Object1: Create", tab "*Create" while dirty).
- `/// @description Init` renames the event in every list ("Create - Init").
- Feather checks as you type and fills Feather Messages with code (GM1022, GM1020), message, resource ("Object1 : Create"), line and column; the error count updates live.
- Autocomplete opens after typed characters, shows return type (`Id.Instance`) and full signature; Enter accepts and adds parentheses. Fold markers in the gutter, line numbers, current line highlight.

### Go To (47 to 48)

Ctrl+T opens "Search for Assets, Options, Preferences". Results mix assets, every command (with where its shortcut is defined, "Redefine Keys") and single preference settings with their page path ("... > Output"). Fuzzy matching highlights the matched letters. G3MStudio's command palette should search the same three kinds of items.

### Room editor (49 to 56)

- Opens as its own top-level tab and adds a Room menu: Layer View Ctrl+Alt+L, Instance Creation Order Ctrl+Alt+I, Reset Windows On Current Desktop, Disable Filters Preview, Tile Editing, Convert Image to Tilemap Ctrl+Alt+G.
- Left dock switches to "Layers - Room1": layer list with visibility and lock per layer and for all, add layer (+), Inherit, delete. Create Layer popup: Background, Instance, Tile, Path, Asset, Effects, UI.
- Below it, "Inspector: Room1": Persistent, Clear Display Buffer, Width, Height, Creation Code, Instance Creation Order, Viewports and Cameras (Enable Viewports, Clear Viewport Background, Viewport 0 to 7 each with Visible, camera X/Y/width/height, viewport X/Y/width/height, Object Following with borders and speeds), Room Physics (Enable Physics, Gravity X/Y, Pixels To Meters).
- Selecting a layer swaps the inspector to that layer: the background layer shows sprite, colour, depth (with lock) and "Filters & Effects" with an Effect Type list (Blend, Blend Ext, Blocks Background, Boxes, Clouds, Colour Balance, Colour Tint and more).
- Middle: a resource picker with search and type filter for painting. Canvas with rulers, floating toolbar (grid with options, zoom out, reset, in, fit, toggle, play room, layer options). Status bar shows world and snapped coordinates and a hint: "LMB + Alt to paint with selected object resources".
- Dragging an object from the Asset Browser into the room did not register through automation; instance editing is covered from the manual (editors.md).

### Script editor (57)

New script opens a code window filling the workspace (with maximize and close) with the template `function Script1(){ }`. Status bar shows line/total, column and character ("3/3 Col:2 Ch:2") and INS/OVR. The Inspector shows "Script (Read-only)".

### Multi-selection (58)

Ctrl+A in the Asset Browser selects every asset. The Inspector then shows "Selection: 1 Object, 1 Room, 1 Script, 1 Sprite", an "Open Editors" button and a removable list of the selected items. G3MStudio's property panel should support the same multi-edit summary.

### Sound editor (59)

Name with a "..." import button; volume slider with value, play, loop, rewind and a mixer button; position slider with time; Conversion (OGG only, Automatic); Attributes: Uncompressed - Not Streamed, Compressed - Not Streamed, Uncompress on Load - Not Streamed, Compressed - Streamed; Target Options: Channel Format (Mono), Sample Rate (44100), Bit Depth (16 bit), Compression Quality; Audio Group; Export Directory. The Inspector adds original size, export size and size ratio, plus a waveform area ("Empty Asset").

### Font editor (60, 61)

Name, Select Font (system font list), Group (texture group), Copy to project, Enable SDF with SDF spread, presets (Style, Size), Anti-aliasing on/off, character ranges (32...127, default character 9647) with Add, Delete, Options and Regenerate. Preview area switches between Sample and Game Preview. Options opens a chained "Font Options" window: Hinting, Glyph Options, Interpreter (TrueType v40), Rounding, Apply Kerning, each with help.

### Path editor (63)

Name, Connection Kind (Straight Lines or Smooth Curve), Precision, Closed, a move-all button, and a point table (X, Y, Speed). Canvas with rulers, grid and zoom toolbar; clicking adds points. Hints: "Left Mouse Button: Add & Drag Anchor", "T+LMB: Translate, R+LMB: Rotate, S+LMB: Scale".

### Automation notes

Ctrl+W can close the whole workspace (with a "Closing a non-empty workspace" confirmation, 62). Reliable sequence for creating resources: close the editor window with its X, click the empty canvas, then press Alt plus the type letter (S sprite, O object, C script, U sound, F font, P path, B tile set, T timeline, G sequence, J animation curve, K particle system, E extension, N note, A shader, R room).

### Tile set editor (64 to 66)

Name, Sprite (with the shared resource picker: search, tree with None / Create New / assets, preview grid, item count, zoom slider, 65), Tile Set Properties (tile width and height, offset, separation in the Inspector), and three sub-editors: Brush Builder, Tile Animation, Auto Tiling. Brush Builder (66) has Tiles and Brushes canvases, a brush size, tools (pencil, eraser, fill, line, rectangle, select, auto tile, flip, mirror, rotate) and the Auto Tile and Animations libraries with edit buttons.

### Timeline editor (67)

Name, a Moment / Description list, Add and Remove. Each moment holds code like an event.

### Sequence editor (68)

Own tab. Canvas with origin cross, toolbar (text track, grid, zoom, fit, transform tools, selection mode, background). Track Panel ("Click or drag an asset here to create an asset track") and Dope Sheet with playhead, keyframe buttons (previous, add, next, record), moments, broadcast messages, curve view, transport, loop and length. Inspector: Time Units (Frames), Speed (FPS), Length, Canvas Frame.

### Animation curve editor (69)

Curve list with visibility and delete, add curve, graph from -1 to 1 with points and zoom, curve tabs under the graph. Inspector: Curve Type (Linear), Channels with Open Editor and Add Curve.

### Particle system editor (70 to 72)

Own tab with a looping live preview, origin X/Y, Draw Order, emitter list (visibility, lock, add, delete). Inspector per emitter: Enabled, Preview, Preview Colour, Mode (Stream), Particle Count, Delay and Interval (min, max, units), Distribution, Shape with X/Y/W/H, Library presets (Select Particles, link, save), Textures (sprite or built-in shapes: Pixel, Disk, Square, Line, Star, Circle, Ring, Sphere, Flare, Spark, Explosion, Cloud, Smoke, Snow), Frame and more below.

### Extension editor (73)

Name, Version (three numbers), Copies To (Windows, macOS, iOS, Android, HTML5, Ubuntu, PlayStation 4, PlayStation 5, Xbox One & Series X/S, Switch, Switch 2, tvOS, GX.games, Reddit), Resources > Files with a menu, Platform Settings (Android, iOS, tvOS, HTML5), Extension Options.

### Note editor (74)

Plain text file (Note1.txt) in a code window; Inspector shows "Notes (Read-only)" and "Open On First Load".

### Shader editor (75)

Two tabs, Shader1.vsh and Shader1.fsh, filled with a passthrough template; Inspector shows both read-only.

### Game Options (76 to 78)

Toolbar gear opens "Game Options": Main Options (General, Template Info) and Platform Settings (GX.games, Reddit, Windows, macOS, Ubuntu, HTML5, Android, iOS, tvOS). General: default scripting language, game frames per second, default draw colour, colour outside the room, Generate Project Images, project start date and use time, project GUID with "Create New GUID", Enable source control, Author, Spine licence, Deprecated Behaviours, "Automatically remove unused assets when compiling". Windows > General: Display Name, Executable Name, Version (4 numbers), Company, Product, Copyright, Description, save location (%localappdata% or %appdata%), copy exe to output, sleep margin, alternative Steam launcher, disable file system sandbox. Windows > Graphics: display cursor, start fullscreen, allow fullscreen switching, interpolate colours, synchronization, window resize, borderless, legacy DXGI swap effect, raw mouse input, scaling (keep aspect or full scale), Texture page size (2048x2048) with Preview. Also Images and Installer pages. Most of these fields live in the data file's GEN8 and OPTN chunks, so G3MStudio's General Info editor can mirror this grouping.

### Texture Groups and Audio Groups (79 to 81)

Tools > Texture Groups: current group with Add New, tree of member assets by type, page preview, Group Settings (Allow Scaling, Automatically Crop, Generate mipmaps, Border Size, Parent Texture Group, Texture Group Format: BZ2 + QOI, QOI, PNG, Custom; Texture Group Type: Default or Dynamic; Dynamic Group/Page Path, Custom Options) and "Use Group On" per platform. Tools > Audio Groups: current group, member sounds, Add Asset, Add New, per-platform use, Export Directory.

### Search & Replace (82 to 84)

Ctrl+Shift+F opens a floating dialog: Search and Replace with history, Case Sensitive, Whole Word Only, Ignore Comments, scope (Everything or a checked list: Scripts, Objects, Sequences, Rooms, Notes, Timelines, Shaders), Find All, Find Previous, Find Next, Replace All, Replace, Stop Search. Results go to the Search Results pane as plain lines ("Object1-Create at line 2: hp = 10;", "Search Complete (2 results)"). G3MStudio can do better: grouped, clickable results with previews and a replace preview.

### Debugger (85)

F6 builds, starts the runner and switches the IDE to a Debugger layout: a Debugger menu and tab, a toolbar (run, pause, restart, stop, step into, over, out, real-time toggle, close), memory and FPS in the header, a Graph panel (memory and FPS over time), Resource View (assets with code), a source area ("Double-click a resource in the Resource View to view its source code here"), Call Stack, and bottom panes Variables (Locals, Globals, Watch with "Add new watch..."), Instances, Others, Graphics. The first debug run triggered a Windows Security prompt (likely a firewall request for the debugger connection); it was left for the owner.

### Debugger panes (86 to 88)

Debugger > Windows lists every pane: Resource View, Source View, Call Stack, Locals, Globals, Instance, Watch, All Instances, Selected Instance, Surfaces & Textures, Render States, Buffers, Graph, Profile; Debugger > Reset Window Layout. The Instances tab shows Instance, All Instances and Selected Instance tables (Name, Value). Pausing ("Break") enables step into, over and out. Stop is "Close the game". For G3MStudio, the useful parts are a runtime log viewer and a process monitor (memory, FPS) when launching a game, not a GML debugger, which needs the GameMaker runner's debug protocol.

### Room instance layer (89)

Selecting the Instances layer shows Instance Layer, Depth with lock, Filters & Effects. The hint "LMB + Alt to paint with selected object resources" applies once an object is selected in the Asset Browser. Painting did not work through automation (selection in the Asset Browser did not register).

### Source Control (90)

Without a repository, only "Clone Repository" is active: URL and target path. Creating a repository needs "Enable source control" in Game Options first. Not enabled during research to keep the TEST folder clean.

### Sound Mixer (91)

Column volume with play, loop, rewind, "Load Audio Group" list and a column area with an add button. A per-group preview mixer.

### Package Manager (92)

Package source "GM Tools" with a search and a "+" to add sources. Lists packages with installed and available versions: "GameMaker IDE Auto-Updater" (scope `@gm-tools`, "IDE updater for Windows", installed 2024.14.58 bundled, 2026.0.59 available) and "project-tool-win-x64". Install, Uninstall, Update and version choice per package. GameMaker ships its own updater as a versioned package from a registry; G3MStudio's updater stays inside the app (ADR-0005).

### Not captured

Prefab Library pane, debugger Others and Graphics tabs (tab switching did not register), room instance and tile painting. The Package Manager window, like Preferences earlier, stopped taking clicks and Escape; restart GameMaker before further automation. These items are covered from the manual in editors.md.

## Hands-on pass 3 (2026-10-25, midday)

Research project: "Cover Assault" created from GameMaker's full-game template (downloaded on creation), so rooms have real layers and instances. TEST still holds the empty test assets.

### New project flow (93 to 96)

New opens "Select Project Type" (Game, Live Wallpaper, Game Strip), then "Select Template" with a filter (Show ALL): Blank Game, Blank Pixel Game, and sample games (Cover Assault, RPG Tutorial, Action, Scrolling Shmup and more). Each template shows a preview, description, scripting language and project name, then "Let's Go!". The wizard banner tracks steps 1 to 4. Templates download on demand and the Project Tool upgrades their resource versions ("Changing the class revisions of a VERSIONED file", "0 files changed of 365", "ProjectTool Successful").

### Populated room (97 to 107)

- The layer list mixes folders (UI Folder), instance layers, effect layers (Screen_Effects, Vignette with an fx badge), asset layers (+ icon: Stage_Front, Midground, Farground, Sky) and a background layer. Each has visibility and lock.
- Selecting a layer fills the middle pane with its contents: instances ("obj_player_sprite inst_10AC2371" with a visibility checkbox), placed sprites on asset layers ("spr_midground... graphic_..."), or UI panels. Hover shows "inst_10AC2371 : obj_player_sprite".
- Selecting an instance draws its bounds on the canvas and shows the position in the status bar ("inst_10AC2371: (1819, 2198)"). Its Inspector: name, Open Editor, sprite preview, object, Sprite Override, Properties (Align buttons, Position, Scale, Colour, Rotation, Flip, Frame, Image Speed), Creation Code.
- Effect layer Inspector: Depth (locked), Effect Type (Vignette) and the effect's own parameters (Edges 1, Edges 2, Sharpness, Vignette Texture).
- UI Folder holds UI layers (Splash, MainMenu, MainMenu_Extra) whose children are Flex Panels. Flex Panel properties: Size (W/H with % or px), Min Size, Max Size, Grow/Shrink, Clip Contents, Position (Relative), Align Self, Offset (L, R, T, B), Flex Properties (Direction, Align Items, Justify Content, Layout Direction, Wrap, Align Content), Spacing (Margin or Padding box editor with per-side values, Preview All Sides, Gap). Status bar hint: "LMB + drag to reorder selected panel; + R drag to new parent; + C clone panel". Modern data files store this in the UILR chunk, so G3MStudio needs a matching UI layer editor.

### Windows, panes and tools (108 to 117)

- Instance Creation Order (Room menu or Inspector button): a floating list of object and instance id ("obj_input_manager inst_483D2458") in creation order, reordered by drag, with Inherit. G3MStudio must show and edit this order, since it lives in the room data.
- Recent Windows: a popup listing recently closed editor windows to reopen.
- IDE Environment Variables: a two-column table of path macros used by the build (`${accountsURI}`, `${adb_exe_path}`, `${arm64_runner_path}`, `${asset_compiler_cache_directory}`, `${asset_compiler_path}` pointing at GMAssetCompiler.dll, `${base_options_dir}`, `${base_project}`, and more).
- Project Health: a list of broken or suspicious project items; empty for a healthy project.
- Prefab Library pane: search, filter and menu, a Package Manager button, and installed prefab packages ("GameMaker Particle Presets") shown as a grid.
- Empty states: Breakpoints ("There are no breakpoints to display. To place one, press F9 on the desired line in a code editor."), Compile Errors ("There are no compile errors to show!"), Source Control ("Source Control Output will go here."). Each empty state says what to do next; G3MStudio should copy that habit.
- Generate Project Images: source image and icon pickers, Generation Mode (Keep Aspect Ratio), Border Colour, Generate.
- Create Local Package: opens as a tab with Package Details (Publisher Name, Display name, Version, Package ID, Sign Package) and a project tree to add resources to the package (Add, Asset Package list, OK, Cancel). Comparable to G3MStudio's "export selected resources" feature.
- Floating tool windows (IDE Environment Variables, Project Health, Generate Project Images, Package Manager, Preferences) often stop responding to their close button under automation and need an IDE restart.

### Help, layout reset and object sub-windows (118 to 123)

- Help > About: IDE version, current runtime, online status, username, time in IDE, CPU, Staff Credits, copyright. Help > Licences: a grid of third-party components, each opening its licence. G3MStudio's About and licence screen can follow this layout.
- Layouts > Reset Layout asks for confirmation ("Resetting the project layout will close any open resources") and closes every editor and floating window. It is the reliable way to clear stuck tool windows. GameMaker restores all open windows, including floating tool windows, when a project is reopened.
- Object > Variable Definitions: table Name (with help), Default, Type, Options, and Add (Ctrl+Alt+T). These live in the object's data and override per instance in rooms.
- Object > Physics: Density, Restitution, Collision Group, Linear Damping, Angular Damping, Friction, Sensor, Start Awake, Kinematic, Modify Collision Shape.
- Object > Parent: parent object picker and a Children list with add.
- Icon-only buttons in panel headers (Asset Browser menu and filter, build target selector) show their tooltip but did not open from synthetic clicks; their contents are in the manual.

### Included Files, more options and preferences (124 to 132)

- Included Files (Alt+I): a tree rooted at `datafiles` with refresh and "Open In Explorer". Included files ship next to the data file, so G3MStudio's workspace should show them beside the data file.
- Game Options > Template Info: template image (438 x 180) and icon (172 x 172). Windows > Images: icon and splash screen image with "Use splash screen". Windows > Installer: NSIS images (164 x 314, 150 x 57), licence agreement and script paths with Edit buttons.
- Preferences > Text Editors: syntax checking delay (2000 ms), code completion delay (150 ms), open scripts, shaders or object events in a full screen editor, open event and linked scripts as tabs in the same window, pan into view on focus, default and minimum code window size, disable right-to-left ordering.
- Preferences > Asset Browser: Show Quick Access, default groups, maximum assets to open without warning (10), recently edited items (10), colour palette size, asset colours, Create Asset dialogue style (Grid), default sort order (A-Z), sort groups first, double click behaviour for sprites and objects, zoom limits, tree lines.
- Preferences > Room Editor: chain fade distances, default layer depth spacing (100), default room size (1366 x 768), add default layers, show views, default text item text, element list orientation, highlight colours.
- Preferences > Sprite Editor: default origin (Top Left), mask kind (Rectangle), animation mode and speed, default size (64 x 64), SWF precision, apply resize and scale to origin.
- Preferences > Languages: default scripting language for new projects (Always Ask).
- Sprites can be Spine sprites (skeletal animation); the editor then shows "Spine Sprite" instead of nine slice settings.
