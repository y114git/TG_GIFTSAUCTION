# Editors parity audit, 2026-11

Scope: every GameMaker editor and tool except the image editor and the code editor intelligence (other work is in progress there), compared with UndertaleModTool (UTMT) and with what G3MStudio has today. This is a document only; no code was changed.

Sources:

- GameMaker: [gamemaker/README.md](gamemaker/README.md) (hands-on screenshots of IDE 2026.0.0.16), [gamemaker/editors.md](gamemaker/editors.md) (per-editor manual notes taken from manual.gamemaker.io, section numbers below), [gamemaker/coverage.md](gamemaker/coverage.md), [gamemaker-format.md](gamemaker-format.md). The manual notes there already list the exact fields per editor, so no new pages were fetched.
- UTMT: [utmt-parity.md](utmt-parity.md) (272 IDs, UTMT 0.9.2.0).
- Studio specs: [../design/editors/](../design/editors/), [../design/shell.md](../design/shell.md), [../design/parity-coverage.md](../design/parity-coverage.md). The specs all say "proposed", so they do not tell what is built.
- Studio state: read from the code on this branch (`G3MStudio/Editor`, `G3MStudio/Rooms`, `G3MStudio/Panels`, `G3MStudio.Core/Workspace`, `lang_en.json`). Nothing was run; a status marks what the source contains, not how it looks in a window.

Status words: **done** means the source has it and a test or command covers it; **partial** means a smaller version exists; **missing** means no code was found. Effort: S is under a day, M is a few days, L is a week or more. "Data" says whether the data file (game.unx / data.win) can hold the result.

Studio-wide facts used below. Every resource opens in the generic property grid (`PropertyGridTab`), which commits one undoable command per edit. Specialised views sit above it: room canvas, sprite frames, object events, sequence timeline, path, particle preview, font glyphs, shader sources, sound player. Panels exist for Explorer, Inspector, History, Git, Problems, Search, Usages and Output. Layouts, a shortcuts editor, a minimal command palette and quick open exist.

## 1. Room editor (manual 2.11, UTMT-ROOM-01 to 26)

| Feature | GameMaker | UTMT | Studio | Effort | Data |
| --- | --- | --- | --- | --- | --- |
| Canvas, zoom 6.25% to 3200%, pan, fit | yes | zoom, scroll | done (`RoomView`) | | yes |
| Rulers and draggable guides | yes | no | done (`RoomRuler`, guide removal) | | no (editor state) |
| Grid, snap, G and Shift+G keys | yes, Ctrl disables snap | grid, Ctrl halves, Shift doubles | done; the Ctrl and Shift grid modifiers are missing | S | grid size yes |
| Layers panel: eye, lock, rename, depth order | yes, plus folders, duplicate, drag reorder, multi-select | tree only | partial: eye, lock (Studio only), rename, up and down, delete; no drag reorder, no duplicate, no multi-select | S | yes (folders no) |
| Layer kinds | Instances, Tile Maps, Paths, Assets, Effect, Background, UI | all kinds listed | partial: add Instances, Assets, Background, Tiles only | M | yes |
| Layer properties: depth, offsets, speeds | yes | yes | done (`RoomLayersPanel` property list) | | yes |
| Instance placing from the Explorer | drag and drop, Alt paints copies | drop, Alt paints copies | done (select asset, click to place, Alt+drag copies) | | yes |
| Select: click, marquee, Shift and Ctrl add | yes, plus select-from-any-layer (hold P) | click | partial: marquee, Shift, Ctrl, select all; no select-from-any-layer | S | no |
| Move, nudge with arrows, delete | yes | yes | done | | yes |
| Copy and paste with snap | yes | yes, Ctrl+Shift+V without snap | done; paste without snap missing | S | yes |
| Flip X and Y | yes | X and Y keys | done | | yes |
| Rotate and scale on the canvas | corner and edge handles, groups | no | missing (numeric fields in the inspector only) | M | yes |
| Align and distribute | Inspector sprite alignment, smart guides | no | missing | S | yes |
| Instance inspector | position, scale, rotation, flip, blend colour, image speed and index, change object, open object, name | most fields | partial: x, y, scale, rotation, frame, speed, object; no blend colour field, no open object | S | yes |
| Instance creation code | per instance, own inherit toggle | per instance | partial: the field exists in the grid; no button to open it in the code editor | S | yes (code entry) |
| Instance variable overrides | Variables button in the Inspector | no | missing | S | not as variables; see section 13 |
| Creation order list | yes | kept in sync | done (`CreationOrderWindow`) | | yes |
| Room order list | yes | yes | done (`RoomOrderWindow`) | | yes |
| Room settings: size, speed, persistent, colour | yes | yes | done in the property grid | | yes |
| Views (8) with camera, port, follow | yes, Show Views | yes | partial: views overlay on the canvas (V), values in the grid; no dragging of view rectangles | M | yes |
| Room physics | yes | yes | done in the grid | | yes |
| Room creation code | yes | yes | done (opens the code entry) | | yes |
| Tile tools: Pencil, Eraser, Fill, Rectangle, Pick | Pencil, Eraser, Fill, Line, Rectangle, Selection, AutoTile, Flip, Mirror, Rotate | tile CSV only | partial: Paint, Erase, Fill, Rectangle, Pick; tile flip, mirror, rotate keys (X, Y, Z); no Line, no Selection | M | yes |
| Tile selection, cut, copy, paste, brush from selection | yes | no | missing | M | yes |
| Tile brush size and stamp brushes | yes | no | missing | S | yes |
| Auto tile and Brush Builder | yes | no | missing | L | tiles yes; the rules are IDE-only |
| Tile palette with indices | Tile Editing window | no | partial (`TilePalette`: click to choose; keyboard picks) | S | yes |
| Tile CSV import and export, fit tile grid | no | yes | done (`TileCsv`) | | yes |
| Convert image to tile map | yes | no | missing | M | yes (builds a tile set and layer) |
| Asset layer: sprites, sequences, text, particle systems | yes, with element list | sprites, particles, sequences listed | partial: element list and rendering; limited property editing for text and sequences | M | yes |
| Path layers | edit points in room | path drawn | partial: path drawn; no in-room editing | M | yes |
| Effect (filter) layers | 41 types with parameters | no | partial: designed in [filter-effects.md](../design/editors/filter-effects.md); not found in the room panel | M | yes (FEDS) |
| UI layers (flex panels) | yes | no | partial: designed in rooms.md; not found in the room panel | L | yes (UILR) |
| Play Animations preview | yes | no | missing | S | no |
| Export room as PNG | no | yes | done | | no |
| Room inheritance, child rooms | yes | no | not applicable, flattened at build | | no |

UTMT strengths already matched: grid snap, Alt paint copies, undo for every edit (UTMT only undoes one move), tile CSV. UTMT has no tile painting tools or layer editing beyond lists.

## 2. Sprite editor (manual 2.1, UTMT-SPR-01 to 05)

| Feature | GameMaker | UTMT | Studio | Effort | Data |
| --- | --- | --- | --- | --- | --- |
| Frame strip with add, duplicate, remove, reorder | yes | list, drop onto slot, remove | done (`FrameListCommand`, `SpriteFramesView`) | | yes |
| Playback preview and speed | speed with unit | speed and type fields | done (play and pause, speed in grid) | | yes |
| Import frames from files, strips, drop | yes, `_stripN` | page items | partial: import and drop for pictures; no strip splitting, no GIF import | S | yes |
| Export frames, strip, GIF | yes | all frames PNG | done: frames, GIF (`SpriteGif`) | | no |
| Origin fields and lock | fields, preset menu, drag on canvas | fields | partial: fields and Lock origin; no preset menu, no drag | S | yes |
| Collision mask mode and type | Automatic, Full Image, Manual; Rectangle, Rotated, Ellipse, Diamond, Precise, Precise per frame | list, remove, import and export PNG | partial: rebuild from picture (`RecalcMaskCommand`), import and export mask PNG | M | masks and bounds yes; mode and type are not stored, they only decide how the mask is built |
| Mask bounding box handles, tolerance slider | yes | no | missing | M | yes |
| Nine slice | guides, colours, tile mode, preview | no | missing (copied with sprites only) | M | yes (GMS2 sprites) |
| Texture group, tile H and V, separate page, premultiply | yes | no | partial: texture page tools in the grid, per-frame own picture (`FrameOwnPictureCommand`); no group picker dialog | M | group yes, flags no |
| Resize / scale image | dialog with preview | no | partial: painter resize; no dialog with anchor and scale | S | yes |
| Per-frame editing (pixel editor) | image editor | no | out of scope here (separate work) | | |
| Broadcast messages on sprites | yes | no | partial: in the sequence view of the sprite (`SequenceTracksView`) | S | yes |
| Spine, SWF types | Spine collision mesh | type shown, Spine export | partial: type shown; no Spine export | M | yes |

## 3. Sound editor and audio groups (manual 2.4, UTMT-SND-01 to 06)

| Feature | GameMaker | UTMT | Studio | Effort | Data |
| --- | --- | --- | --- | --- | --- |
| Play, stop | yes, loop, rewind | play | done (`SoundPlayerView`); no loop or rewind toggle | S | no |
| Waveform | in the Inspector | no | done (`WaveformView`) | | no |
| Trim, volume scale, fades | no | no | done (`AudioEditorWindow`, `WavEdit`); this goes beyond GameMaker | | yes (WAV/OGG bytes) |
| Replace audio, export audio, export all | yes | yes | done, WAV, OGG and MP3 import (`ReplaceAudioCommand`, `AudioExport`) | | yes |
| Flags: compressed, streamed, decompress on load | attributes | flags | done in the grid | | yes |
| Sample rate, bit depth, quality, channels | target options | no | missing as encoder inputs | M | build-time only (re-encoded audio is stored, the settings are not) |
| Audio group assignment | picker | group field | partial: group field in the grid; audio group files open as workspaces | M | yes (AGRP, audiogroupN.dat) |
| Audio Groups dialog | yes | audio group editor | missing as a dialog (create, rename, move sounds) | M | yes |
| Sound Mixer | batch volume and buses | no | missing | M | volume yes; buses no |
| OGG conversion mode, export directory | yes | no | not applicable (build step) | | no |

## 4. Object editor (manual 2.10, UTMT-OBJ-01 to 06)

| Feature | GameMaker | UTMT | Studio | Effort | Data |
| --- | --- | --- | --- | --- | --- |
| Properties: sprite, mask, parent, visible, solid, persistent, depth | yes | yes | done (grid with pickers) | | yes |
| Physics properties and vertices | yes | yes | done (`PhysicsVerticesView`) | | yes |
| Events list: add, remove | yes, with Add Event menu by category | yes | done (`ObjectEventsView`: add of any type, remove, code links) | | yes |
| Event duplicate, copy to another object | yes | no | missing | S | yes (code entry copy) |
| Event reorder | drag | no | not applicable, the file orders events by type and subtype | | no |
| Event code inline | code opens as a chained window | opens the entry | partial: link opens a code tab; no inline pane | S | yes |
| Parent and children | parent field, hierarchy | parent field, find children | partial: parent field, Find child objects; no inherited-events view | S | yes |
| Variables (default instance variables) | yes | no | missing | | not stored; they compile into Create code |
| Event picker names (keys, mouse, other, collision) | yes | yes | done | | yes |
| Open sprite or parent from the object | yes | no | partial: Go To and References | S | no |

## 5. Sequence editor and animation curves (manual 2.12 and 2.13)

| Feature | GameMaker | UTMT | Studio | Effort | Data |
| --- | --- | --- | --- | --- | --- |
| Track tree | yes | project only | done (`SequenceTracksView`) | | yes |
| Dope sheet: move keyframes | yes, resize, split, copy | no | partial: drag a key to another frame; length, add and remove through the grid | M | yes |
| Add, split, delete keyframes on the sheet | yes | no | missing | M | yes |
| Playhead, preview of sprites | canvas with gizmos | no | done (`SequencePlayerView`, `SequenceEvaluator`) | | no |
| Canvas gizmos and auto record | position, rotation, scale, origin | no | missing | L | yes (keys) |
| Moments and broadcast messages | yes | no | done (listed; add group track) | | yes |
| Parameter tracks, embedded curves | yes | no | partial: through the grid | M | yes |
| Animation curve editor: channels, points, interpolation, presets | graph with Bezier | no | missing (grid only); designed in [animation-curves.md](../design/editors/animation-curves.md) | M | yes |
| Sequence events (Create, Step, ...) bound to scripts | yes | no | partial: fields in the grid | S | yes |
| Clipping masks, text tracks, sound bus effects | yes | no | partial: tracks listed, no dedicated editing | M | tracks yes, buses no |

## 6. Particle system editor (manual 2.14)

| Feature | GameMaker | UTMT | Studio | Effort | Data |
| --- | --- | --- | --- | --- | --- |
| Emitter list: add, delete | yes, plus duplicate, copy, rename | project only | done add; delete and rename through the explorer and grid (`ParticleEmitterCommand`) | S | yes |
| Live preview | yes | no | done (`ParticlePreviewView`, `ParticleSimulation`, approximate) | | no |
| Emitter and particle properties | Inspector | no | done in the grid | | yes |
| Region editing on the canvas | drag | no | missing | M | yes |
| Presets and linked emitters | yes | no | not applicable (preset links are project-level); closest is Copy to Other Data File | | no |
| Copy GML to clipboard | yes | no | missing | S | no (it is code output) |
| Pause and solo emitters, draw order | yes | no | missing | S | no |

## 7. Path editor (manual 2.5, UTMT has none)

| Feature | GameMaker | Studio | Effort | Data |
| --- | --- | --- | --- | --- |
| Add, drag, delete points, speed per point | yes | done (`PathView`, `PathPointsCommand`) | | yes |
| Straight or smooth, closed, precision | yes | done in the grid | | yes |
| Insert a point on a segment | yes | missing | S | yes |
| Multi-select and group drag | yes | missing | S | yes |
| Reverse, flip, mirror, translate / rotate / scale | yes | missing (Move all exists) | S | yes |
| Room backdrop | yes | missing | S | no |

## 8. Tile set editor (manual 2.3, UTMT-BG)

| Feature | GameMaker | UTMT | Studio | Effort | Data |
| --- | --- | --- | --- | --- | --- |
| Tile size, offsets, separation, border | yes | yes | done in the grid | | yes |
| Tile grid with indices over the image | yes | no | partial: palette picker; no index overlay | S | no |
| Animated tiles (frames, speed) | editor with play | no | missing (values in the grid) | M | yes |
| Auto tile (47 and 16 templates) | yes | no | missing | L | rules are IDE-only; only the resulting tiles are stored |
| Brush Builder | yes | no | missing | L | IDE-only library |
| Find rooms using a tile | no | tile references | done | | no |

## 9. Font editor (manual 2.8, UTMT-FNT-01 to 05)

| Feature | GameMaker | UTMT | Studio | Effort | Data |
| --- | --- | --- | --- | --- | --- |
| Glyph table, filter by character or code | ranges list | glyph table | done (`FontGlyphsView`) | | yes |
| Glyph rectangle editor on the texture | no | yes | done (pick on the texture) | | yes |
| Kerning editor | option only | yes | done (`FontKerningCommand`) | | yes |
| Add glyph, add range | typing in preview, range buttons Normal, ASCII, Digits, Letters | add and remove | partial: add by U+code, character or range; no preset buttons | S | yes |
| Regenerate from a TrueType or OpenType file | yes | no | done (`FontReplaceCommand`) | | yes |
| SDF | enable, spread | no | partial (`SdfEncoder`) | M | yes |
| Sample text preview and game preview | yes | no | missing | S | no |
| From Code / From File range scan | yes | no | missing | S | yes (the chosen glyphs) |
| Hinting and interpreter options | yes | no | not applicable, tied to GameMaker's rasteriser | | no |

## 10. Shader, timeline, extension, included files, notes

| Item | GameMaker | UTMT | Studio | Effort | Data |
| --- | --- | --- | --- | --- | --- |
| Shader sources per language, attributes | two tabs, type | two text areas | done (`ShaderSourceView`, `ShaderTemplate`); no compile check | S | yes (HLSL bytecode needs a compiler) |
| Timeline moments: add, remove, code | plus duplicate, merge, cut, copy, paste | project only | partial (`TimelineMomentCommand`, `TimelineActionCommand`) | S | yes |
| Extension editor | files, functions, constants, options | no | partial: grid only; designed in [extensions.md](../design/editors/extensions.md) | M | yes (EXTN) |
| Included files | datafiles folder | no | missing; designed in [included-files.md](../design/editors/included-files.md) | S | no |
| Notes | Markdown-like text asset | no | missing | S | no |
| Game options pages | per platform, big dialog | General Info | partial: General Info and OPTN pages follow GameMaker's grouping ([general-info.md](../design/editors/general-info.md)) | M | OPTN yes, platform build options no |

## 11. Shell tools: Asset Browser, Inspector, panels, layouts, search

| Feature | GameMaker | UTMT | Studio | Effort | Data |
| --- | --- | --- | --- | --- | --- |
| Resource tree with filter by name | yes | yes | done (`ExplorerView`, virtualised, 124k rows tested) | | |
| Multi-select, bulk delete, copy, export, duplicate | yes | no | done (Ctrl and Shift selection) | | yes |
| Folders in the browser | asset folders | none | not applicable, the data file has no folders; closest is the type grouping | | no |
| Thumbnails / grid view with zoom | yes | no | partial: row tooltips with pictures; no grid view | M | no |
| Favourites | yes | no | missing | S | no (store in the project sidecar) |
| Recent | yes (files and windows) | no | partial: recent files and tab history; no recent resources list | S | no |
| Room order group | yes | no | done (`RoomOrderWindow`) | | yes |
| Saved searches | yes | no | missing | S | no (sidecar) |
| Tags (asset tags) | yes | no | partial: TAGS chunk through the grid; no tag filter or editor | S | yes (TAGS) |
| Prefab library | yes | no | not applicable, flattened | | no |
| Create asset popup with count | 15 types | context menu | partial: New resource with templates (`ResourceTemplates`); no count spinner | S | yes |
| Inspector | many fields, lock, search | description view | partial: type, name, usages (`InspectorPanel`); edit fields live in the tab | M | |
| Go To (Ctrl+T) / quick open | yes | tree filter | done (`QuickOpenWindow`, Alt+Enter splits) | | |
| Search & Replace across code | yes | yes | done (`SearchPanel`, `CodeSearch`) | | yes |
| Output pane | yes | no | done (`OutputPanel`) | | |
| Compile errors pane | yes | no | done (`ProblemsPanel`) | | |
| Feather messages | yes | no | out of scope (code editor work) | | |
| Search results pane | yes | results window | done | | |
| Breakpoints pane | yes | no | missing; debug data export exists (`MainWindow.DebugData`), no live debugger | L | .yydebug sidecar, not the data file |
| Layouts: save, load, reset | yes | no | done (`LayoutsWindow`, `StudioDock`) | | |
| Workspaces (free canvas of floating editors) | yes | no | not applicable by design (tabs, splits and cards instead) | | |
| Command palette | no | C# command box | partial: substring filter only (`PaletteWindow`) | S | |
| Source control | Git/SVN/Perforce | no | done for Git on the project folder (`GitPanel`, `GitService`) | | |
| Preferences: themes, hotkeys | yes | dark mode, few settings | done: themes and colours (`ThemeColors`, hot reload), shortcuts editor | | |
| Project Health | yes | no | partial: Find unreferenced, load warnings | S | |
| Texture Groups dialog | yes | page viewer | partial: texture page viewer and group field; no group dialog | M | yes (TGIN) |
| Sound Mixer | yes | no | missing (see section 3) | M | |
| Package Manager, Marketplace, Generate Project Images | yes | no | not applicable | | no |

## 12. What UTMT offers that GameMaker does not

All 272 UTMT IDs have a spec ([parity-coverage.md](../design/parity-coverage.md)). The ones that matter to this audit and that Studio already has: tile CSV, per-chunk hex view, disassembly, chunk offset map, string table, language table, texture page viewer, apply patch and mod, copy to another data file. The only UTMT editing behaviour Studio does not reproduce in a window is the audio group child instance (replaced by audio group files as workspaces, Q70).

## 13. GameMaker features that cannot exist in a data file

The compiled data file holds flattened results. These GameMaker features only exist in the IDE or project files, so Studio cannot store them. The closest alternative is listed.

| IDE-only feature | Why | Closest Studio alternative |
| --- | --- | --- |
| Room and layer inheritance, child rooms | flattened at build | Copy to Other Data File; duplicate room; none needed on edit |
| Layer folders and sub layers | not in the ROOM chunk | Lock and visibility per layer (Studio only) |
| Asset folders in the Asset Browser | not in the data file | Type grouping in the Explorer, favourites in the project sidecar |
| Prefab library, collections, packages, Marketplace | resolved at build | none needed; copy resources between files |
| Auto tile rules, Brush Builder libraries | rules live in the project YYs | tile stamp brushes from a selection (stored as tiles) |
| Instance variable overrides, object default variables | compiled into Create and creation code | helper that writes a creation code line; edit the code entry |
| Notes assets | not exported | Markdown notes in the project sidecar |
| Included Files | loose files next to the game, DAFL is empty | folder view of loose files ([included-files.md](../design/editors/included-files.md)) |
| Collision mask mode and type | only the finished mask and bounds are stored | build the mask from the same choices, then store bits |
| Texture group settings (format, scaling, per platform) | project settings; the file keeps TGIN pages | texture group dialog on TGIN |
| Sound compression targets (rate, bit depth, quality) | build step input | re-encode on import with chosen values |
| Font hinting and interpreter options | GameMaker's rasteriser setting | regenerate from font file with Studio's own rasteriser |
| Game options per platform, configurations, target bar | project settings | General Info and OPTN pages; launch profiles |
| Build, clean, create executable | build tool | Run Game, debug data, save pipeline |
| Free-form workspace canvas with chained windows | UI design | tabs, splits, cards and saved layouts |
| Feather settings and linting profile | IDE config | Problems panel from the compiler |
| Source Control against the project file set (SVN, Perforce) | works on project files | Git panel on the project folder |
| Debugger with breakpoints against the running game | runtime attach | debug data export and the game log; a debugger is a separate large item |
| Particle presets with linked emitters, saved templates | project-level | copy emitters between systems; copy to other file |
| Room Play Animations, sequence canvas auto record | IDE preview and editing aids | live preview already present for particles and sequences |
| Project Health, IDE Environment Variables | IDE | Find unreferenced, load warnings |
| Spine collision mesh, SVG vector sprites | source assets, not stored | bitmaps only |
| Per-element "export" checkboxes | only affects build | delete the element |
| Tile set Disable Source Sprite Export | build flag | none |

## 14. Prioritised backlog (top 30)

Ranking weighs how often a modder hits the gap, how close GameMaker users expect it, and effort. The first 15 are the recommended next work. "Data" is yes only if the result is saved inside the data file.

| # | Item | Smallest useful implementation | Effort | Data |
| --- | --- | --- | --- | --- |
| 1 | Room: rotate and scale handles | Draw a box with four corner handles around the selection; dragging a corner scales, a handle above the box rotates, one undo step per drag, Shift keeps the ratio | M | yes (instance scale, rotation) |
| 2 | Room: align and distribute | Six align buttons and two distribute buttons in the inspector when two or more instances are selected, using sprite bounds | S | yes |
| 3 | Room: tile selection with copy, cut, paste and flip | A Selection tool that marquees tile cells; Ctrl+C, Ctrl+X, Ctrl+V reuse `RoomTilesCommand`; X, Y, Z transform the pasted block | M | yes |
| 4 | Room: Line tool and brush size | Add `Line` to `RoomTool` (Bresenham over cells) and a 1 to 8 cell brush size box | S | yes |
| 5 | Room: add Path, Effect and UI layers, duplicate layer | Extend the kind list in `RoomLayersPanel` with Path (path picker) and Effect (FEDS type); Duplicate copies the layer with new ids | M | yes |
| 6 | Room: instance creation code button | An "Edit code" button in `InstanceInspector` that opens or creates the instance's code entry in a code tab | S | yes |
| 7 | Sprite: collision mask editor | A mask section with Automatic, Full Image and Manual, a type list for Rectangle, Ellipse, Diamond, Precise, a tolerance box, and four bound fields with handles on the canvas; Apply writes bits through `RecalcMaskCommand` | M | yes (bits and bounds) |
| 8 | Sprite: origin presets and drag | A nine-point preset menu next to the origin fields and a draggable cross on the frame view | S | yes |
| 9 | Sprite: nine slice | Four number fields plus tile mode in a sprite section, drawn as guide lines on the frame view | M | yes (GMS2 sprites) |
| 10 | Animation curve graph | A canvas for one channel with draggable points and a point table, linear and smooth first, Bezier later | M | yes |
| 11 | Sequence: add, split and delete keyframes | Right-click on the dope sheet row: Add key at playhead, Split, Delete, each a `SequenceTrackCommand` | M | yes |
| 12 | Object: duplicate event and copy events to another object | Context menu on an event row: Duplicate (new code entry with copied text) and Copy to object picker | S | yes |
| 13 | Path: insert, multi-select, reverse, flip, mirror | Click near a segment inserts; Ctrl-click multi-select; four buttons under the point list | S | yes |
| 14 | Tile set: index overlay and animated tile table | Toggle that prints tile indices on the grid; a list editor for animation frames and frame time | M | yes |
| 15 | Texture group and audio group dialogs | One dialog per kind: list groups, show members, move selected resources to another group | M | yes (TGIN, AGRP) |
| 16 | Asset tags: editor and filter | Tag chip editor in the Inspector panel and a tag filter in the Explorer filter box | S | yes (TAGS) |
| 17 | Explorer: favourites and recent resources | Star toggle and a Recent group stored in the project sidecar | S | no (sidecar) |
| 18 | Explorer: grid view with thumbnails | A toggle to a wrapped thumbnail list for sprites, tile sets, fonts using `ResourceThumbnails` | M | no |
| 19 | Object: inline event code pane | Show the selected event's code editor control under the event list, same Apply path as the code tab | S | yes |
| 20 | Object: inherited events view | Read-only rows for events of ancestors, greyed, with an Override button that adds the event | S | yes |
| 21 | Room: select from any layer and paste without snap | Hold P for one click ignoring layer locks; Ctrl+Shift+V pastes at the exact pointer | S | no / yes |
| 22 | Room: view rectangles on the canvas | Draw port and camera rectangles and let a drag change X and Y | M | yes |
| 23 | Font: range preset buttons and sample preview | Buttons Normal, ASCII, Digits, Letters that call the existing glyph add; a text box drawn with the font | S | yes |
| 24 | Command palette: fuzzy match and recents | Replace the substring filter with a subsequence score; show the last 5 commands first | S | no |
| 25 | Particle: Copy GML and emitter duplicate | A button that writes `part_*` or `part_system_*` calls for the system to the clipboard; duplicate emitter via `ParticleEmitterCommand` | S | no / yes |
| 26 | Timeline: duplicate, merge, cut, copy, paste moments | Context menu on moment rows using `TimelineMomentCommand` | S | yes |
| 27 | Sound: loop toggle, selection play, Sound Mixer | Loop check box on the player; a grid of all sounds with a volume box each | M | volume yes |
| 28 | Extension editor | Tree of files, functions and constants with name and signature fields, one command per edit | M | yes |
| 29 | Sprite: strip import and resize dialog | Import a picture and split by `_stripN` or by a frame count; resize dialog with anchor | S | yes |
| 30 | Notes and Included Files views | Markdown notes in the project sidecar; a read-only folder view of loose files next to the data file | S | no |

Deferred on purpose: auto tile and Brush Builder (L, rules not stored), canvas gizmos for sequences (L), UI layer authoring beyond listing (L), live debugger and Breakpoints pane (L, needs a runtime attach), workspace canvas (rejected in [editors.md](gamemaker/editors.md) section 4).

## 15. Limits of this audit

- Statuses come from reading source and the English strings of this branch. No window was opened and no editor was run, so "done" means the code exists, not that it reads well on screen.
- The image editor and code editor intelligence are not covered; they belong to other work in progress.
- GameMaker detail was taken from the existing manual notes in [gamemaker/editors.md](gamemaker/editors.md); no additional manual pages were fetched.
- Whether each field above is stored for every game version (GMS1, 2.3, LTS) was not checked against G3MLib; check [gamemaker-format-coverage.md](gamemaker-format-coverage.md) before building an item.
