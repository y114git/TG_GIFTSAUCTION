# GameMaker live audit, 2026-11

Status: done for the editors the sample project holds. Path, tile set and timeline editors could not be opened because the project has none.

Method: GameMaker LTS 2026 (IDE 2026.0.0.16, runtime 2026.0.0.23) with the project "Cover Assault" open at 1938 by 1158 pixels, display scale 125%. Screenshots are 1920 by 1140 pixels, so sizes below are in screenshot pixels (divide by 1.25 for logical pixels). Every tool tooltip, menu and panel was opened by hovering or clicking, nothing was changed on purpose, and nothing was saved, built or run. Pictures are in `gamemaker/shots-2026-11/`.

Side effects to know about:

- The title bar gained a `*` (unsaved) after the first room tab was opened. Opening a room editor seems to mark the project dirty by itself. Nothing was saved.
- Ctrl+Space in a code comment typed a space instead of opening a list. It was undone through the right-click Undo item and the text matches the original again. The completion list was therefore not seen.
- Focus jumped to a File Explorer window twice during the run (not caused by a click). Focus was restored with `focus-window` before the next click, and no Explorer window was touched.
- Panel collapse in the image editor right column was toggled once and toggled back.

## Shell and dock layout

Measured on the image editor screen, 1920 by 1140.

| Part | Position and size |
| --- | --- |
| Title bar | top 30 px, then menu bar (30 px) and main toolbar (35 px) |
| Left dock (Inspector) | x 20 to 380, 360 px wide, about 19% |
| Center | x 388 to 1528, 1140 px wide, about 59%, holds the tab strip (y 104 to 128), editor, status bar (y 868) |
| Right dock (Assets) | x 1540 to 1898, 358 px wide, about 19% |
| Bottom dock | y 890 to 1120, about 230 px high, only under the center and the right of the left dock |
| Chevrons | `«` at the left edge and `»` at the right edge (x 8 and 1908, y 620) fold the whole side dock; a chevron at the bottom center (y 1126) folds the bottom dock |

Both side docks are the same width. The bottom dock does not span the side docks. The left dock is split in two stacked panels (Layers and the Inspector) with its own tab strip and a plus button. The bottom dock tabs are Output, Search Results, Source Control, Breakpoints, Compile Errors, Feather Messages, Prefab Library. Each dock tab has an X and a plus button adds more. The Windows menu lists the same panels plus Recent Windows, IDE Environment Variables, Project Health and Project Tool Output.

Editor windows of objects, fonts, curves, shaders and scripts open as floating windows on one free canvas called Workspace 1 (first tab in the center strip). Rooms, sequences, particle systems and images open as their own center tabs. The workspace window set can be saved: the Layouts menu has Save Layout, Load Layout, Reset Layout, and Show All Windows and Close All Windows (greyed out when nothing is floating).

Top menus, as seen:

| Menu | Items (shortcut) |
| --- | --- |
| File | New Project (Ctrl+N), Open Project (Ctrl+O), Import Project, Recent Projects >, Save Project (Ctrl+S), Save Project As (Ctrl+Shift+S), Export Project >, New IDE, Preferences (Ctrl+Shift+P), Exit (Alt+F4) |
| Edit | Undo (Ctrl+Z), Redo (Ctrl+Y), Search & Replace (Ctrl+Shift+F) |
| Build | Run (F5), Debug (F6), Re-Run, Clean (Ctrl+F7), Clean Code, Clean Graphics, Clean Audio, Create Executable (Ctrl+F8), Create Executable and Launch |
| Windows | Asset Browser, Output, Search Results, Source Control Output, Breakpoints, Compile Errors, Feather Messages, Inspector, Recent Windows, IDE Environment Variables, Prefab Library, Project Health, Project Tool Output |
| Tools | Texture Groups, Audio Groups, Sound Mixer, Generate Project Images, Create Local Package, Import Local Package, Project Tool, Package Manager |
| Marketplace | no dropdown seen |
| Layouts | Save Layout, Load Layout, Reset Layout, Show All Windows, Close All Windows |
| Help | no dropdown seen (acts directly) |
| Source Control | Create Project Repository, Clone Repository, Commit Changes, Push Changes, Pull Changes, View History, Show Conflicts (last five greyed without a repository) |
| Room (only with a room tab) | Layer View (Ctrl+Alt+L), Instance Creation Order (Ctrl+Alt+I), Reset Windows On Current Desktop, Disable Filters Preview, Tile Editing >, Convert Image to Tilemap (Ctrl+Alt+G) |
| Image, View, Effects (only with an image tab) | View: 1:1 (Alt+C), Fit Screen (Alt+F), Toggle Grid (G), Onion Skin Settings. Effects: Blur, Greyscale. The Image menu opened empty in this state |

Menus change with the active tab (Room, or Image, View, Effects). A menu stays open until a click elsewhere.

Main toolbar, left to right (tooltip with shortcut): Close Project, New Project (Ctrl+N), Open Project (Ctrl+O), Save Project (Ctrl+S), Create Executable (Ctrl+F8), Create Executable and Launch, Debug (F6), Run (F5), Stop, Clean (Ctrl+F7), Game Options, Help, Zoom Out (Ctrl+-), Zoom Reset (Ctrl+Return), Zoom In (Ctrl++), Collapse or Expand dock panels. The right end shows the IDE and runtime version, an account icon, and a target block: platform (Windows), VM (GMS2 VM), two config names (Default, Default) and a Targets button. Pencil, size slider and Smooth belong to the image editor and appear only there.

## Image editor

Layout: frame strip on top (onion skin toggle, loop or ping-pong, play or pause, add frame button, frame thumbnails), canvas in the middle with a floating toolbar over its top right (Toggle Grid G, Zoom Out Ctrl+-, Reset Zoom Ctrl+Return, Zoom In Ctrl++, Centre Fit Ctrl+F, Show Split View), status bar (size, frame index, a hint line that changes per tool, for example "Click to paint (Shift for a straight line, Ctrl to pick a colour)"). The right column holds Brushes, Colours, Toolbox and Layers as an accordion: collapsing one gives its space to the others, and the Layers panel is squeezed to one row at this window size. The pencil, Size slider (1) and Smooth checkbox sit in the main toolbar.

Toolbox, 19 tools in rows of 6, 5, 4 and 4 with tooltip shortcuts:

| Row | Tools |
| --- | --- |
| 1 | Paintbrush (D), Erase (E), Colour remove (H), Colour replace (V), Fill (F), Line (L) |
| 2 | Rectangle outline (R), Ellipse outline (C), Polygon outline (P), Arc (A), Text (T) |
| 3 | Colour Picker (O), Rectangle select (S), Paint selection (Q), Magic wand (W) |
| 4 | Rotate brush (U), Flip brush (Y), Mirror brush (X), Pan image (M) |

Brushes: ten presets (five single pixel to square, five round). Colours: a palette of 7 rows by 11, a custom row of empty slots, left and right colour swatches with a mouse picture and a swap arrow. The Colours header has an options menu (Import colours, Copy colours to sprite). No separate shape tools for filled shapes were seen: outline tools only, with fill probably an option.

## Room editor

Layout: a left list panel (search box, list filter drop-down, `0 items` count) that shows the instances or assets of the selected layer with a checkbox per row, then the canvas with pixel rulers, and a status bar with the cursor in room and view coordinates plus a hint ("LMB + Alt to paint with selected object resources"). Canvas toolbar (top right of the canvas): Toggle Grid (G) with drop-down, Toggle UI canvas preview, Show UI Layer node outlines, Zoom Out, Reset Zoom, Zoom In, Centre Fit (Ctrl+F), Show Views, Play Animation, Select From Any Layer (Hold P). A small button at the list panel top changes orientation. The same toolbar was shown for instance, asset and the other layers.

Left dock: layer list with eye and lock per layer, a folder row, add layer (+), Inherit button and delete. Layer rows with an effect show a small wand icon. The Inspector below shows Room Settings (Persistent, Clear Display Buffer, Width, Height, Creation Code, Instance Creation Order buttons, and collapsed Viewports and Cameras and Room Physics). Selecting a layer swaps the Inspector to that layer: Instance layer shows Depth, Filters and Effects with effect type.

Add layer popup (grid of 7): Background, Instance, Tile, Path, Asset, Effects, UI.

Layer right-click menu: Layer Properties (Ctrl+Shift+I), Rename Layer (F2), Delete Layer (Delete), Duplicate Layer, Add Layer >, Add Sub Layer >, Inherit Sub Layers, Inherit Layer Visibility (last two greyed).

No context menu appeared on right-click of an empty canvas spot or of an instance row in the list. The sample rooms hold no tile layer, so tile tools could not be seen.

## Sprite editor

Opens in the Workspace as a floating window titled "Sprite: name". Left column: Name, Image group (size with a resize toggle, Edit Image button, Import button), collapsible Texture Settings, Collision Mask (Mode Automatic, Type Ellipse (Slow), Tolerance slider and spin box, Left, Right, Top, Bottom fields) and Nine Slice. Right: Fps with units drop-down, Origin X and Y with a lock and a presets button (clipped at the window edge), preview canvas with a zoom button, Current Frame field and frame length, play controls with loop, and a frame timeline. The toolbar is cut off at the right edge with no overflow menu.

## Object editor

Two floating windows: the object window (Name, Sprite slot with edit buttons, Collision Mask, Visible, Solid, Persistent, Uses Physics, and buttons Events, Parent, Physics, Variable Definitions) and an Events window (event list with an icon per event and an Add Event button). Add Event opens a cascading menu: Create, Destroy, Clean Up, Step >, Alarm >, Draw >, Mouse >, Key Down >, Key Pressed >, Key Up >, Gesture >, Collision >, and more below. Step holds Step, Begin Step, End Step. Mouse holds Left, Right, Middle Down, No Mouse Input, then Pressed and Released groups. Double-click on an event opens its code in a third floating window beside the events. The Inspector repeats the same object data in a narrow form.

## Sequence, animation curve, particle system

Sequence: top half preview canvas with a floating toolbar (Add a Text Track, Toggle Grid, zoom set, Toggle Gizmos, Toggle Transform Gizmo, Change orientation). Bottom half timeline with: Split selected keys, Delete selected keys, Record a new key, Automatically record changes, current frame, fps, Add Broadcast Message, Add Moment, Toggle Curve Mode, previous or next frame, play, playback mode, a ruler in frames, and a Track Panel with search, eye and lock, plus Add Track, Duplicate Track, Add New Clip Mask, Add New Group.

Animation curve: floating window with a curve list (name, type "Curves", per-curve eye and delete), a graph with value axis, a fit button and zoom buttons. The Inspector shows a preview, Curve Type (Bezier) and Channels with Open Editor and Add Curve.

Particle system: center tab with an emitter list on the left (eye and lock per emitter, add with Insert, delete) and a preview with a toolbar: Origin X and Y, Draw Order, Copy GML to clipboard, Toggle grid, zoom, Toggle canvas frame, Change orientation, and a Restart animation button at the bottom right. The Inspector shows Emitter Settings (Enabled, Preview, Preview Colour, Mode, Particle Count, Delay with Min and Max and units, Interval).

## Font, shader, code editors, asset browser

Font: floating window with Select Font, Group, Copy to project, Enable SDF, SDF spread, Presets (Style, Size), Anti-aliasing, a texture preview with Sample or Game Preview switch and range text, and buttons Add, Delete, Options, Regenerate. It warns when the font is not installed.

Shader: one floating window with two tabs (.vsh and .fsh), full GML-style code area, a status bar with INS. The Inspector shows read-only VSH and FSH panels.

Code editor (script and event code): file tab with close, line numbers, fold markers, current-line highlight, status bar with the INS mode. There is no breadcrumb bar and no minimap. Hover on a function shows a tooltip with the full signature including types and return, a description, and a per-parameter table. Right-click menu: Cut, Copy, Paste, Select All, Undo, Redo, Comment Selection (Ctrl+K), Uncomment Selection (Ctrl+Shift+K), Tab In, Tab Out, Regions >, Code Snippets >, Go to Object, Open Inherited Event (events only), Single pane, 2 columns, 3 columns, Toggle Breakpoint (F9), Convert to GML Visual. The Ctrl+Space completion list and F12 were not seen (see side effects).

Asset browser: search box, Create Asset (+), Sort and Filter, and a menu with Room Manager, Config Editor, Game Options, Included Files. Quick Access group holds Recent, Favourites, Room Order, Saved Searches, Tags, Game Options, Prefabs (empty groups show a hollow arrow). Folders: Animation Curves, Fonts, Objects (with sub folders such as Enemy, Managers), Particle Systems, Rooms, Scripts, Sequences, Shaders, Sounds, Sprites. Footer shows item count, selection count and a zoom percentage slider. Filter popup: Clear Filters, sort (Custom Order, A-Z, Z-A), Groups First, an Asset Type checklist (Any Types, Animation Curves, Extensions, Fonts, Notes, Objects, Particle Systems, Paths, Rooms, Scripts, Sequences, Shaders, Sounds, Sprites, Tile Sets, Timelines) and a Tags section. The sample project had no tags or saved searches, so those could not be tried.

## Gap table against Studio

Status words as in `editors-parity-2026-11.md`: done, partial, missing. The Studio column repeats what that audit read from source; none of it was run in a window for this pass.

| Area | GameMaker behavior seen | Studio | Gap |
| --- | --- | --- | --- |
| Dock proportions | left 19%, center 59%, right 19%, bottom 230 px under center only | layouts exist, defaults unknown | partial |
| Dock collapse | one chevron per side and bottom, plus a toolbar button for all | splits and layouts exist, no one-key hide all | partial |
| Floating workspace | free canvas of editor windows | not by design | n/a |
| Menus change with tab | Room and Image menus appear on demand | static menus | missing |
| Main toolbar with shortcuts in tooltips | 16 buttons, all tooltips carry the key | toolbar exists, tooltip keys vary | partial |
| Image editor tools | 19 tools with single-letter keys | image editor gap list in `image-editor-parity-2026-11.md` | partial |
| Image editor panels | Brushes, Colours, Toolbox, Layers accordion | no accordion | partial |
| Frame strip with onion, loop, play | yes | sprite frames exist, no onion | partial |
| Room canvas toolbar | grid, UI preview, views, play, select from any layer (hold P) | grid and zoom done, others missing | partial |
| Room instance list panel | per layer list with checkboxes and search | layers panel only | missing |
| Layer context menu | 7 items with keys (F2, Delete, Ctrl+Shift+I) | rename and delete only | partial |
| Add layer popup | 7 kinds in a grid | 4 kinds in a menu | partial |
| Room menu | Layer View, Creation Order, Convert to Tilemap | Creation Order exists | partial |
| Sprite editor | collapsible Collision Mask and Nine Slice sections | done | done |
| Object editor | Add Event cascade, double-click opens code | no Add Event dialog | partial |
| Sequence timeline | keys, moments, broadcast, clip mask, groups | keyframes done, no moments, broadcast, groups | partial |
| Animation curve | graph, channel list, type | graph done, presets not built | done |
| Particle editor | emitter list with eye and lock, copy GML | preview only | partial |
| Font editor | ranges, SDF, regenerate | glyph view | partial |
| Shader editor | two tabs, read-only in Inspector | sources view | done |
| Code editor hover | signature, text, parameter table | hover exists | check |
| Code editor menu | regions, snippets, 3 pane layouts, breakpoint | partial | partial |
| Code editor breadcrumbs and minimap | none in GameMaker | Studio plan item 18 | not needed |
| Asset browser filters | type checklist, sort, groups first, tags | name filter only | partial |
| Quick Access group | Recent, Favourites, Saved Searches, Tags | missing | missing |
| Hotkey hints in tooltips | everywhere | partial | partial |
| Path, tile set, timeline editors | not in sample project | done and partial per earlier audit | not seen |

## Revised top 20 for Studio

Layout, sizing, navigation and shortcuts first.

1. Default layout 19%, 59%, 19% with a bottom dock only under the center, saved as the Default layout.
2. One chevron per side and for the bottom to fold a dock, plus one toolbar button and key that folds all.
3. Menus that change with the active editor tab (Room, Image, View, Effects) instead of static menus.
4. Tooltips on every toolbar button with the shortcut in brackets, from the same keymap the editor uses.
5. Single-letter tool keys in the image editor (D, E, F, L, R, C, P, A, T, O, S, W and the rest of the 19).
6. Accordion panels in the image editor right column: collapse gives space to the others.
7. Room canvas toolbar: grid, UI preview, views, select from any layer (hold P).
8. Room instance and asset list per layer with search, checkboxes and an orientation toggle.
9. Layer context menu with rename (F2), delete (Delete), duplicate, add layer, add sub layer.
10. Add layer popup as a grid of kinds with icons.
11. Go To palette on Ctrl+T with a fuzzy match.
12. Asset browser Quick Access group: Recent, Favourites, Saved Searches, Tags.
13. Asset browser filter popup: sort A-Z and Z-A, groups first, asset type checklist, tags.
14. Layout save, load, reset and Show All and Close All Windows.
15. Object editor Add Event cascade with sub groups, and double-click on an event opens its code beside it.
16. Code editor right-click menu with comment, regions, snippets, breakpoint, pane layouts, Go to Object.
17. Hover tooltip with a typed signature, description and per-parameter table.
18. Sequence timeline with moments, broadcast messages, groups and clip masks.
19. Particle editor emitter list with eye and lock, plus Copy GML.
20. Font editor range list with Add, Delete, Options and a regenerate action, with a missing-font warning.

## What could not be reached

- Path, tile set and timeline editors: none exist in the sample project.
- Tile layer tools and tile right-click menus: no tile layer in the opened rooms.
- Ctrl+Space completion list, F12 and snippet list: Ctrl+Space typed a space.
- Preferences pages: not opened, because the brief said not to change preferences and the window has stopped taking input before.
- Saved searches, tags and favourites contents: the project has none.
- Image menu contents: the dropdown opened empty.
- Right-click on room instances and empty canvas: no menu appeared.
- Exact key for Create Executable and Launch, Collapse or Expand dock panels and Select From Any Layer: tooltip text was cut at the edge of the screenshot (the last one reads "Hold P").
