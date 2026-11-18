# Rooms

Status: proposed. The editor for ROOM resources and for the UI layers stored in the UILR chunk: room properties, views, physics, every layer type, instances, tiles, assets, effects, UI layers with flex panels, creation order, tile painting, and a room preview you can edit directly. Shared rules: [../ux-standards.md](../ux-standards.md). Code of creation scripts is edited in the [code editor](code.md).

## 1. Purpose and entry points

Place and change what is in a level. The editor renders the room as the game would (layers in depth order) and edits the same data: instances, tiles, sprites, backgrounds, paths, sequences, text, particle systems, effects and UI nodes.

Opens from Explorer > Rooms (double-click), Explorer > Quick Access > Room Order, Go To `room:`, References results and a code entry's owner link. Rooms open as their own tab (or a card). A room with more than 5,000 elements opens with a notice and renders layers progressively.

Differences by file version: GMS1 files show Backgrounds, Views and Tiles in the object tree; GMS2 files show Layers. The editor shows only what the file version has (UTMT-ROOM-03).

## 2. Layout

```
+----------------------------------------------------------------------------------------------+
| room_start    Index 3   1366 x 768   Speed 60   Layers 8   Instances 214        [Play Room >] |
+--------------------+-------------------------------------------------------+-----------------+
| Layers             |  rulers + canvas (room at zoom)                       | Inspector       |
| [+] [folder] [del] |  +--------------------------------------------+       | (room, layer,   |
| (eye)(lock) Inst.. |  |                                            |       |  or element)    |
| (eye)(lock) Tiles  |  |        instances, tiles, sprites           |       |                 |
| (eye)(lock) Back.. |  |                                            |       |                 |
|  UI Folder         |  +--------------------------------------------+       |                 |
|    Splash          |  grid [G] snap [Shift+G] views [V] [-] 100% [+]       |                 |
+--------------------+-------------------------------------------------------+                 |
| Elements: Instances layer  [search...]                                      |                 |
| inst_10AC2371 : obj_player        (x)    214 elements, 1 selected           |                 |
+----------------------------------------------------------------------------------------------+
| x 1819, y 2198  snapped 1824, 2192  LMB: select  Alt+drag: paint copies  Space+drag: pan       |
+----------------------------------------------------------------------------------------------+
```

Regions: layers panel (left, 240 px), canvas with rulers and a floating toolbar (centre), Inspector (right dock, switches to the selected room, layer or element), element list (bottom, 140 px, collapsible), status hint line. In the Studio preset the layers panel is part of the editor, not the left dock, so the Explorer stays visible. In the Code preset the layers panel docks to the left dock as a tab.

## 3. Canvas

1. Origin at 0,0 top left. Rulers 20 px with draggable guides (snap distance 8 px, setting `room.guideSnap`). Zoom 6.25% to 3200% (Ctrl+wheel, Ctrl+=, Ctrl+-, Ctrl+0 fit, Ctrl+Shift+0 actual size). Pan with Space+drag, middle drag, or two-finger drag.
2. Grid (UTMT-ROOM-20): G toggles; cell size and thickness come from the room's grid fields (3.1) unless the global override is set (UTMT-SET-13 in settings: width, height, thickness). Snap: Shift+G. Dragging snaps to the grid. Hold Ctrl while dragging to halve the grid step and Shift to double it (UTMT-ROOM-12). Smart guides align edges and centres to other elements (setting).
3. Rendering (UTMT-ROOM-02, UTMT-ROOM-25): backgrounds, layers in depth order, tiles, object sprites, sprite instances, particle system bounds (dashed outline), path layers (the path drawn as a polyline), text items, sequence elements as boxes with the sequence name, effect layers shown as a labelled band. A cache of rendered sprites keeps panning smooth; 60 fps panning is the target with 5,000 elements. Effect previews render only when "Effect Preview" is on (default off, `rooms.disableEffects` toggles) and strobing effects show a still frame until enabled.
4. Selection: click selects the topmost element on the selected layer; hold P to select from any layer once ("Select From Any Layer"). Shift+click adds, Ctrl+click toggles, marquee drag selects, Escape clears. Selected elements show 8 handles for scale and a rotation handle; Shift keeps aspect.
5. Hover shows a tooltip "inst_10AC2371 : obj_player" and the cursor position in the status hint (world and snapped).
6. Alt+drag paints copies of the selected instance, tile, sprite or particle instance along the grid (UTMT-ROOM-13). A ghost preview shows the next copy. Ctrl while placing disables snapping.
7. Drag from the Explorer (UTMT-ROOM-14, UTMT-TREE-09): an object creates an instance, a sprite creates a sprite element on an asset layer, a tile set creates a tile layer, a background fills an empty room background or creates a tile, a font creates a text item, a sequence or particle system creates an element on an asset layer. Drops snap to the grid. Dropping on an unsuitable layer asks "Create a new {type} layer?" with Create and Cancel (an automatic response can be stored).
8. Copy and paste (UTMT-ROOM-15): Ctrl+C copies the selection; Ctrl+V pastes at the mouse position with snapping; Ctrl+Shift+V pastes without snapping. Pasted particle system instances get a random name and a new id. Pasted instances get new ids.
9. Flip: X and Y mirror the selected instance and correct the position by the sprite origin (UTMT-ROOM-18).
10. Nudge: arrow keys move by 1 pixel or the grid step when snap is on; Shift multiplies by 8. Drag-free alternative for every move (WCAG 2.5.7): the Inspector position fields and these keys.
11. Save as PNG (UTMT-ROOM-21): Room > "Save Room as PNG..." renders the room at 100% with the selected layers visible.
12. Scroll position and zoom are restored per tab (UTMT-ROOM-19, UTMT-TAB-08).

## 4. Layers panel

Layer list in depth order (top is closest to the viewer). Each row: eye (visibility), lock, type icon, name; folders (GMS2 layer groups and the UI Folder) expand. Create Layer menu (+): Instances, Tile Map, Path, Assets, Filter/Effect, Background, UI. Folder button, delete button. Slow double-click or F2 renames (names must be unique within the room: "'{name}' is already a layer name."). Drag to reorder; Alt+Up and Alt+Down also move; Shift range and Ctrl multi-select. Eye and lock toggles exist per row and "all" in the header. Lock is a G3MStudio editing aid and is never written to the data file.

Depth range is -16000 to 16000. Layer properties shown in the Inspector when a layer is selected (UTMT-ROOM-08): Name, ID (read-only), Type (read-only), Depth (with lock toggle to keep relative depth), X offset and Y offset, horizontal and vertical speed, Visible.

### 4.1 Layer types

| Layer | Inspector fields (besides the common ones) | Element list and tools |
| --- | --- | --- |
| Instances | None | Element list of instances (3 columns: name, object, position) with a checkbox to export or not. Click to place objects selected in the Explorer |
| Tile map | Tile set (Reference field), tile width and height, "Edit Tiles", "Export Tile Data as CSV...", "Import Tile Data From CSV...", "Fit Tile Layer to Room Size" (UTMT-ROOM-22, UTMT-ROOM-23) | Tile painting mode, section 7 |
| Path | Path (Reference field), smooth or straight, closed, precision | The path is drawn in the room; edit points with the path tools |
| Assets | None | Element list of sprites, sequences, text items, particle systems, plus legacy tiles on older files. Per-element export checkbox |
| Filter/Effect | Effect type (list of 41 types with search: Blend, Extended Blend, Gaussian Blur, Glow, Recursive Blur, Windblown Particles, Blocks, Boxes, Clouds, Colour Balance, Colourise, Contrast, Distort, Dots, Edge Detect, Fractal Noise, Gradient, Greyscale, Hard Drop Shadow, Heat Haze, Hue, Large Blur, Linear Blur, LUT Colour, Mask, Old Film, Outline, Panorama, Parallax, Pixelate, Posterise, RGB Noise, Ripples, Screen Shake, Stripes, Tint, Twirl Distort, Twist Blur, Underwater, Vignette, White Noise, Zoom Blur) and the parameters of that effect | Applies to every layer below it (FEDS data). Parameters appear as generic fields from the effect definition (number, colour, sprite, checkbox) |
| Background | Colour (used when no sprite), sprite, tile horizontally, tile vertically, stretch, first frame, animation speed and speed type, X/Y offset, horizontal and vertical speed | Single element |
| UI | See section 8 | UI tree |

Effect layer data is stored in the model but UTMT only lists the name (UTMT-NOED-09). G3MStudio edits type and parameters.

### 4.2 Elements

Instances (UTMT-ROOM-06): Instance ID (read-only, copy), Object (Reference field; changing it keeps the position), Position, Scale, Colour (colour field with alpha), Rotation, Frame index, Image speed, Creation code (opens the code editor; "Add Creation Code" when empty), Pre-create code. Variable overrides appear as code in creation code (no typed table in compiled files).

Tile (legacy GMS1 and old GMS2, UTMT-ROOM-07): Tile set or background, source region (x, y, w, h), position, scale, colour, depth, instance ID.

Sprite element (UTMT-ROOM-09): Sprite, position, scale, colour, rotation, frame index, animation speed and type.

Particle system element (UTMT-ROOM-10): Particle system, position, scale, colour, rotation.

Sequence element (UTMT-ROOM-26): Sequence, position, scale, colour, rotation, speed, start frame. It is listed and movable and has a property section in this editor (an improvement over UTMT, which had none).

Text item (UTMT-NOED-08): String, font, colour, rotation, scale, frame origin and offset, frame size, wrapping, alignment. Nine-slice room items use the nine-slice fields of the sprite and appear as an element with its slice settings in the Inspector.

Backgrounds and views for GMS1 (UTMT-ROOM-04, UTMT-ROOM-05): Backgrounds list (up to 8): Enabled, Foreground, Definition (Reference field), Position, Tiling X and Y, Stretch, Speed; "Delete" in the context menu. Views list (up to 8): Enabled, view position and size, port position and size, border, follow object, speed; "Disable view" in the context menu.

Adding items (UTMT-ROOM-11): the canvas context menu and the element list's "Add" menu offer New Instance, New Tile, New Instances Layer, New Tile Layer, New Background Layer, New Assets Layer, New Sprite Element, New Legacy Tile, as the file version allows.

Reorder (UTMT-ROOM-17): the Delete key removes the selected element; the plus and minus keys, Alt+Up and Alt+Down, or the up and down buttons in the element list move it in draw order. Undo restores order.

## 5. Room properties (Inspector when nothing is selected)

UTMT-ROOM-01, UTMT-ROOM-04, UTMT-ROOM-05:

| Field | Control | Rules |
| --- | --- | --- |
| Name | Name field | |
| Caption | Text | GMS1 window caption |
| Width, height | Size editor, integers 1 to 32767 | Changing them offers "Resize layers to match?" (tile layers) |
| Speed | Number, 1 to 1000 | Room speed in frames per second |
| Persistent | Checkbox | |
| Background colour, Draw background colour | Colour field and checkbox | |
| Clear display buffer | Checkbox | |
| Creation code | Reference field (Code) with "Add", "Open" | Opens the code editor |
| Instance creation order | Button | Opens the list (section 6) |
| Flags | Flags box (UTMT-CTRL-04) | GMS2 flags, version-specific |
| Enable views | Checkbox | |
| Views 0 to 7 | Each: Visible; camera X, Y, width, height; viewport X, Y, width, height; object following (Reference field) with horizontal and vertical border and speed (-1 means instant) | The canvas draws the view rectangles when "Show Views" (V) is on; drag handles on the canvas change the same values |
| Clear viewport background | Checkbox | |
| Enable physics | Checkbox | |
| Gravity X, Y | Number, float | |
| Pixels to metres | Number, float > 0 | |
| World: top, left, right, bottom | Four numbers | Old physics world box |
| Grid width, height, thickness | Three numbers | Used for the grid overlay (UTMT-ROOM-20) |

Sections for rooms from older files that lack a field hide it.

## 6. Instance creation order (UTMT-ROOM-24)

"Instance Creation Order..." opens a floating list (or tab) of all instances in creation order: row number, instance name, object. Reorder by drag or Alt+Up and Alt+Down; "Move to Top" and "Move to End" buttons; search box; instances from UI layers listed after, for the first room in the game. The list stays in sync when instances are added or deleted (new instances are appended). A note: "Creation order does not decide event order."

## 7. Tile painting (UTMT-TILE-01 to 08)

Entering "Edit Tiles" (T) on a tile layer opens a tile palette panel and the tile painting mode. Changes apply to the layer only on Apply (Ctrl+Enter); closing the mode asks "Apply tile edits?" with Apply, Discard, Cancel (UTMT-TILE-08).

1. Palette (UTMT-TILE-01): the tile set image with the tile grid; the column count is adjustable and saved per tile set; drag selects a multi-tile brush; Ctrl+click adds tiles to the brush; the empty tile 0 clears.
2. Tools (keys 1 to 7): Pencil, Eraser, Fill, Line, Rectangle, Selection, Auto Tile. Left mouse paints (UTMT-TILE-02); right mouse erases with the pencil. Shift+drag draws a straight line. Ctrl+click flood fills (UTMT-TILE-03); Ctrl+Shift+click replaces the tile id everywhere in the layer.
3. Pick (UTMT-TILE-04): Alt+click or middle click picks the tile under the pointer into the brush.
4. Brush transforms (UTMT-TILE-05): X mirrors horizontally, Y or Z flips vertically, R rotates clockwise, Q rotates counterclockwise.
5. View toggles (UTMT-TILE-06): G grid, B tile a multi-tile brush across the drag, P show the rest of the room behind the layer.
6. Undo and redo (UTMT-TILE-07) inside the mode: Ctrl+Z, Ctrl+Y, Ctrl+Shift+Z. After Apply, the whole session is one workspace undo step.
7. Auto Tile and brush libraries are authoring data that compiled files do not hold. The Auto Tile tool uses a built-in 47-tile and 16-tile template mapping the user assigns once per tile set; the assignment is stored in the sidecar project.
8. Convert Image to Tile Layer (`rooms.convertImage`): choose an image; set cell size, offsets, remove colour, names; creates a deduplicated tile set, a sprite and a tile layer.

## 8. UI layers and flex panels (UTMT-NOED-06)

UI layers are global (shared by all rooms) and live in a "UI Folder". They hold a tree of nodes that the UILR chunk stores.

Tree: the layers panel shows UI layers as roots (Splash, MainMenu, ...) with child nodes. Node kinds: Flex Panel, Sprite, Object instance, Text. Create from the + menu on a selected node; drag to reorder; to move a node to a new parent, hold R while dragging; to clone, hold C while dragging (the status hint reads "LMB + drag to reorder selected panel; + R drag to new parent; + C clone panel"). Keyboard alternatives: Alt+Up and Alt+Down reorder, "Move To Parent..." and "Duplicate" (Ctrl+D) in the context menu.

Flex panel properties:

| Group | Fields |
| --- | --- |
| Size | Width, height (number plus unit toggle: pixels or percent), Min size, Max size, Grow, Shrink, Clip contents |
| Position | Relative or Absolute, Align self (Auto, Start, Centre, End, Stretch, Baseline), Offset left, right, top, bottom |
| Flex | Direction (Row, Row reverse, Column, Column reverse), Align items, Justify content, Layout direction (Inherit, LTR, RTL), Wrap, Align content |
| Spacing | Margin and padding box editors with per-side values and "Preview All Sides" (linked editing), Gap |

The canvas shows the main display or a chosen view and draws panel outlines; selecting a node draws its bounds. The element list shows children. The preview resolves flex layout using the same rules as the game; layout bugs (children overflow) show in Problems as warnings.

## 9. Actions and shortcuts

| Command | Id | Windows and Linux | macOS |
| --- | --- | --- | --- |
| Zoom In | `canvas.zoomIn` | Ctrl+= | Cmd+= |
| Zoom Out | `canvas.zoomOut` | Ctrl+- | Cmd+- |
| Fit to View | `canvas.fit` | Ctrl+0 | Cmd+0 |
| Actual Size (100%) | `canvas.actualSize` | Ctrl+Shift+0 | Cmd+Shift+0 |
| Pan (Hold, Then Drag) | `canvas.pan` | Space | Space |
| Show or Hide Grid | `canvas.grid` | G | G |
| Snap to Grid | `canvas.snap` | Shift+G | Shift+G |
| Play or Pause Preview | `canvas.play` | Shift+Space | Shift+Space |
| Nudge Selection | `canvas.nudge` | Left / Right / Up / Down | Left / Right / Up / Down |
| Nudge Selection by 8 | `canvas.nudgeLarge` | Shift+Left / Shift+Right / Shift+Up / Shift+Down | Shift+Left / Shift+Right / Shift+Up / Shift+Down |
| Cancel Current Tool or Selection | `canvas.cancel` | Escape | Escape |
| New Layer | `rooms.newLayer` | Insert | Insert |
| Delete Layer | `rooms.deleteLayer` | Delete | Delete |
| Show or Hide Layer | `rooms.toggleVisible` | H | H |
| Lock or Unlock Layer | `rooms.toggleLock` | K | K |
| Move Forward | `rooms.moveForward` | + / Alt+Up | + / Option+Up |
| Move Backward | `rooms.moveBackward` | - / Alt+Down | - / Option+Down |
| Flip Horizontally | `rooms.flipX` | X | X |
| Flip Vertically | `rooms.flipY` | Y | Y |
| Paste Without Snapping | `rooms.pasteNoSnap` | Ctrl+Shift+V | Cmd+Shift+V |
| Select From Any Layer (Hold) | `rooms.selectAnyLayer` | P | P |
| Show Views | `rooms.showViews` | V | V |
| Halve Grid While Dragging (Hold) | `rooms.gridHalf` | Ctrl | Ctrl |
| Double Grid While Dragging (Hold) | `rooms.gridDouble` | Shift | Shift |
| Paint Copies (Hold, Then Drag) | `rooms.paintCopies` | Alt | Option |
| Instance Creation Order... | `rooms.creationOrder` | None | None |
| Layer List | `rooms.layerView` | None | None |
| Play Animations | `rooms.playAnimations` | None | None |
| Save Room as PNG... | `rooms.exportPng` | None | None |
| Convert Image to Tile Layer... | `rooms.convertImage` | None | None |
| Export Tile Layer as CSV... | `rooms.exportTileCsv` | None | None |
| Import Tile Layer From CSV... | `rooms.importTileCsv` | None | None |
| Fit Tile Layer to Room Size | `rooms.autoSizeTiles` | None | None |
| Disable Effect Preview | `rooms.disableEffects` | None | None |
| Edit Tiles | `rooms.editTiles` | T | T |
| Pencil | `roomtiles.pencil` | 1 | 1 |
| Eraser | `roomtiles.eraser` | 2 | 2 |
| Fill | `roomtiles.fill` | 3 | 3 |
| Line | `roomtiles.line` | 4 | 4 |
| Rectangle | `roomtiles.rectangle` | 5 | 5 |
| Selection | `roomtiles.select` | 6 | 6 |
| Auto Tile | `roomtiles.autoTile` | 7 | 7 |
| Mirror Brush | `roomtiles.mirror` | X | X |
| Flip Brush | `roomtiles.flip` | Y / Z | Y / Z |
| Rotate Brush Clockwise | `roomtiles.rotateCw` | R | R |
| Rotate Brush Counterclockwise | `roomtiles.rotateCcw` | Q | Q |
| Tile Multi-Tile Brush | `roomtiles.brushTiling` | B | B |
| Show Room Preview | `roomtiles.preview` | P | P |
| Replace Tile Everywhere (Shift+Fill) | `roomtiles.globalReplace` | Shift+Click | Shift+Click |
| Pick Tile (Alt+Click or Middle Click) | `roomtiles.pick` | Alt+Click | Option+Click |
| Apply Tile Edits | `roomtiles.apply` | Ctrl+Enter | Cmd+Enter |
| Discard Tile Edits | `roomtiles.discard` | None | None |

## 10. Validation and messages

| Case | Message |
| --- | --- |
| Duplicate layer or instance name | "'{name}' is already used in this room." |
| Object missing for an instance | "The object no longer exists. Choose another object or delete the instance." with Fix |
| Tile id beyond tile set | "Tile id {id} is beyond the {count} tiles in '{tileset}'." |
| Room too large | "A room can be at most 32767 pixels in each direction." |
| Delete layer with elements | Confirm, undoable: notification "Deleted layer '{name}' and {n} elements. Undo" |
| Instance creation order out of sync | Warning with "Rebuild Order" fix |
| Effect parameter out of range | "Value must be between {min} and {max}." |

## 11. Undo

Undoable: every drag, nudge, paste, delete, reorder, property commit and layer operation. Grouping: a multi-element drag is one step; a tile painting session is one step on Apply. Ctrl+Z in the room canvas undoes this editor's last step first (UTMT-ROOM-16 improved: UTMT only undid the last drag).

## 12. States

Empty:
- Empty room: the canvas shows the room rectangle and a hint "Drag an object from the Explorer to place an instance. Press Insert to add a layer."
- No layers: "This room has no layers. Create one with + in the layers panel."

Loading:
- The layer list appears at once; elements render progressively layer by layer with the status "Rendering layer {n} of {m}...".

Error:
- Missing sprite for an element: a hatched box with the element name and a warning icon.
- Room unreadable: "The room '{name}' could not be read: {error}. The layers that were read are shown."

## 13. Parity coverage

- UTMT-ROOM-01: section 5.
- UTMT-ROOM-02, UTMT-ROOM-25: section 3 (items 3).
- UTMT-ROOM-03: section 1 and the layers panel (section 4).
- UTMT-ROOM-04, UTMT-ROOM-05: sections 4.2 and 5.
- UTMT-ROOM-06 to UTMT-ROOM-10, UTMT-ROOM-26: section 4.2.
- UTMT-ROOM-11: section 4.2.
- UTMT-ROOM-12, UTMT-ROOM-13, UTMT-ROOM-14, UTMT-ROOM-15, UTMT-ROOM-16, UTMT-ROOM-18, UTMT-ROOM-19, UTMT-ROOM-20, UTMT-ROOM-21: section 3 and section 11.
- UTMT-ROOM-17: section 4.2.
- UTMT-ROOM-22, UTMT-ROOM-23: section 4.1.
- UTMT-ROOM-24: section 6.
- UTMT-TILE-01 to UTMT-TILE-08: section 7.
- UTMT-NOED-06: section 8.
- UTMT-NOED-08: section 4.2 (text items and nine-slice room items).
- UTMT-NOED-09: section 4.1 (effect layers).
- UTMT-KEY-05, UTMT-KEY-06: section 9 and the keyboard map.
- UTMT-TREE-09: section 3, item 7.
- UTMT-SET-13: section 3, item 2.
- UTMT-TREE-04 (room defaults on add: GMS2, 2.3 or 2024.13 flags and optional room-order entry): New Resource dialog for rooms asks for the room order insertion.
