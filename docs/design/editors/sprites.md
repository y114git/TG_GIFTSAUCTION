# Sprites

Status: proposed. The editor for SPRT resources: properties, frames, origin, collision masks, nine slice, broadcast messages, import and export. Pixel editing is in the [image editor](image-editor.md). Texture page effects of image changes are in [texture-pages.md](texture-pages.md). Shared rules: [../ux-standards.md](../ux-standards.md).

## 1. Purpose and entry points

The most used resource editor. It shows a sprite as GameMaker does (a canvas, a frame timeline, a property column) and edits every field the data file stores, including the per-frame texture page items. It also does the import and export work modders do daily: PNG frames, strips, GIF, masks, Spine files.

Opens from Explorer > Sprites, Go To `spr:`, a texture page item's "Used by", an object's sprite field ("Edit Sprite"), and a room instance. In the Canvas preset it opens as a card.

## 2. Layout

```
+--------------------------------------------------------------------------------------+
| spr_player   Index 87   64 x 64   8 frames          [Import v] [Export v] [Edit Image]|
+--------------------+-----------------------------------------------------------------+
| Properties         |  Speed [ 10 ] [frames per second v]   Origin X [32] Y [32] [lock] [v]|
|  Name [spr_player] |                                                                 |
|  Size  64 x 64     |   +-----------------------------------------+   (zoom toolbar)  |
|  v Texture         |   |                                         |    [-] 400% [+] [fit]|
|    Smooth    [x]   |   |         sprite on a checkerboard        |                  |
|    Preload   [ ]   |   |           with origin cross             |                  |
|    Group [Default] |   +-----------------------------------------+                  |
|  v Collision Mask  |                                                                 |
|    Mode  [Auto  v] +-----------------------------------------------------------------+
|    Type [Rectangle]| < |>  Frame 3 of 8      Length 1      [loop v]   [ > ]  [<<][>>] |
|  v Nine Slice      | [1][2][3*][4][5][6][7][8]   (thumbnails, drag to reorder)        |
+--------------------+-----------------------------------------------------------------+
```

Left column 280 px with collapsible groups. Top bar: playback speed with unit, origin fields with lock and preset menu. Canvas fills the rest with a floating zoom toolbar. Bottom: the frame strip (thumbnails 48 px) with transport. With nine slice on, the canvas splits into the edit view and a live preview.

## 3. Properties

### 3.1 Fields (UTMT-SPR-01, UTMT-SPR-04)

| Field | Control | Rules |
| --- | --- | --- |
| Name | Name field | Resource name rules |
| Width, height | Size editor, read-only here | Changed with "Resize Sprite..." (4.3) |
| Margins left, right, top, bottom | Four number fields | Integers 0 to 65535; must stay inside the frame: "Margins must be inside the image." |
| Bounding box mode | Combo: Automatic, Full Image, Manual | Matches mask "Mode" below in GameMaker terms; stored as the sprite's bbox mode |
| Separate masks | Checkbox | One mask per frame; unchecking merges to frame 1 mask after a confirm "Remove {n} masks?" (undoable) |
| Transparent | Checkbox | Old audio-system era flag; shown for GMS1 files |
| Smooth (interpolate) | Checkbox | |
| Preload | Checkbox | |
| Origin X, Y | Number fields (integer; may lie outside the image), lock button, preset menu | Presets: Top Left, Top Centre, Top Right, Middle Left, Centre, Middle Right, Bottom Left, Bottom Centre, Bottom Right, Custom. Click on the canvas in "Set Origin" mode places it. Locked origin follows resizes with "Apply to Origin" |
| Playback speed | Number (float, 0 and up) | Unit combo: Frames per second, Frames per game frame (UTMT-SPR-04 playback speed and speed type) |
| Special type (GMS2) | Read-only row "Type: Normal, SWF or Spine" with version | Normal sprites show a "Texture" group; Spine and SWF sprites show the matching group instead of Nine Slice |
| Texture group | Combo | Moving to a group repacks pages, see texture-pages.md |
| Tile horizontally, vertically; separate texture page; premultiply alpha; edge filtering | Checkboxes | Shown when the file stores them for this version; otherwise the row is hidden |

### 3.2 Collision mask (UTMT-SPR-03)

| Field | Control |
| --- | --- |
| Mode | Combo: Automatic (from alpha and tolerance), Full Image, Manual |
| Type | Combo: Rectangle, Rectangle With Rotation, Ellipse, Diamond, Precise, Precise (per frame) |
| Tolerance | Slider with number, 0 to 255 |
| Bounding box | Left, Right, Top, Bottom number fields and corner handles on the canvas |
| Masks list | List of masks (one per frame when Separate masks is on): index, size, state "Valid" or "Invalid mask data!" (red text with icon, UTMT-SPR-03), Import Mask..., Export Mask... (PNG), Remove |

The mask is drawn over the frame in `Info` at 50% opacity when "Show Mask" (M) is on. "Recalculate Collision Mask" rebuilds an automatic mask from alpha and tolerance. Invalid mask data shows an info bar "This sprite's mask data cannot be read. Recalculate it or import a mask. [Recalculate]".

### 3.3 Nine slice

Shown when the sprite has nine-slice data (or the user turns it on).

| Field | Control |
| --- | --- |
| Enable nine slice | Checkbox |
| Guides left, right, top, bottom | Four number fields measured from their own edge; guides on one axis push each other instead of crossing; colour and alpha per guide |
| Highlight | Combo: Overlay, Inverted, Off; highlight colour |
| Selected slice | Combo: Left, Top, Right, Bottom, Centre, plus click on canvas |
| Tile mode (per slice) | Combo: Stretch, Repeat, Mirror, Blank Repeat, Hide |

The preview pane can be resized and rotated 90 degrees. Not offered for Spine sprites.

### 3.4 Spine and special sprites (UTMT-SPR-04, UTMT-SPR-05)

Spine sprites show "Spine Sprite" and read-only skeleton facts (animation names, atlas pages). The frame strip is hidden. "Export Spine Files..." writes the Spine JSON, atlas and textures. Collision uses Spine data and cannot be edited. SWF sprites show the same read-only info.

## 4. Frames and canvas

### 4.1 Frame strip (UTMT-SPR-02)

1. One thumbnail per frame, showing the frame number, its texture page item index and length (fractional lengths shown as "1.5"). Selected frame has the selection style. Click selects, Ctrl+click multi-selects, Shift+click selects a range.
2. Drag a thumbnail to reorder; the keyboard alternative is Ctrl+Shift+Left and Ctrl+Shift+Right on the selected frame.
3. Drag image files from the file manager onto the strip to add frames (inserted at the drop position). Drag a texture page item from the texture page viewer onto a slot to link it. Double-click a thumbnail opens the Image Editor on that frame.
4. Add Frame (Insert) duplicates the last frame's size as an empty frame. Insert Frame (Shift+Insert) inserts before the selection. Delete removes the selected frames after a reference check (frame count cannot drop below 1: "A sprite needs at least one frame.").
5. Cut, Copy and Paste frames use the clipboard (image data plus frame metadata) and work between sprites.
6. Frame length: drag the right edge of a thumbnail to stretch in whole frames, or edit the "Length" field for fractions (0.1 to 100).
7. Transport: first, previous, Play or Pause (Shift+Space), next, last; loop mode Loop, Ping-pong, Once (preview only). The preview starts paused (ux-standards section 5). Current frame field is 1-based in the UI with a tooltip "Code uses 0-based frame numbers."
8. Broadcast messages: the button "Add Broadcast Message" at the playhead adds a message string; messages show as flags on the frame; right-click edits or deletes. Shown for files that store them.

### 4.2 Canvas

Background: checkerboard (colours from `appearance.transparencyGrid`, UTMT-SET-12). Origin drawn as a cross in `Danger` with a circle; mask bounds in `Info`; onion skin (O) shows 1 frame before and after at 30% with settings in the toolbar menu. Zoom 12.5% to 3200%: Ctrl+wheel, Ctrl+=, Ctrl+-; fit Ctrl+0; actual size Ctrl+Shift+0; pan with Space+drag or middle drag. Pixel grid shows at 800% and above. The status bar shows the pixel under the pointer ("x 12, y 40") and the frame.

### 4.3 Resize (UTMT-SPR-01 size)

"Resize Sprite..." dialog: Scale Image (width and height with keep aspect, interpolation Nearest or Linear) or Resize Canvas (anchor 3 x 3 grid, pixels or percent). Preview of before and after. "Apply to origin" scales an unlocked origin. Applying repacks the sprite onto texture pages; the dialog shows the page count change ("Uses 1 more page") before Apply.

## 5. Import and export

| Command | Behaviour |
| --- | --- |
| Import Images... (`sprites.importFrames`) | Multi-select PNG, JPG, GIF, BMP. Each file becomes a frame; a GIF becomes its frames and keeps its speed when single. Names ending `_strip{N}` split into N frames (the suffix is dropped from the name). A dialog offers: Replace existing frames, Add after current, Add to end; Resize to sprite size, Crop or pad with anchor. Replacing frames confirms nothing (undoable) but a notification says "Replaced {n} frames. Undo" |
| Import Strip... | Choose a sheet; dialog with frame count, frame width, height, offsets, separation; preview with grid lines |
| Export Frames... | Folder picker; one PNG per frame named `{name}_{index}.png` (UTMT-SPR-05 export all frames) |
| Export Strip PNG... | One PNG `{name}_strip{N}.png` |
| Export GIF... | Animated GIF with the sprite's speed; transparent background option |
| Export Spine Files... | Spine sprites only |
| Import Mask... / Export Mask... | PNG masks, one per mask (UTMT-SPR-03) |
| Edit in External Program | Exports frames to a temp folder, opens the configured editor (Settings > Paths), watches the files and offers "Reimport {n} changed frames" (file watcher pattern) |

Dropping an image file on the editor adds frames; dropping on the Explorer creates a new sprite.

## 6. Actions and shortcuts

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
| Import Images... | `sprites.importFrames` | None | None |
| Import Strip... | `sprites.importStrip` | None | None |
| Export Frames... | `sprites.exportFrames` | None | None |
| Export Strip PNG... | `sprites.exportStrip` | None | None |
| Export GIF... | `sprites.exportGif` | None | None |
| Export Spine Files... | `sprites.exportSpine` | None | None |
| Add Frame | `sprites.addFrame` | Insert | Insert |
| Insert Frame Before | `sprites.insertFrame` | Shift+Insert | Shift+Insert |
| Move Frame Left | `sprites.moveFrameLeft` | Ctrl+Shift+Left | Cmd+Shift+Left |
| Move Frame Right | `sprites.moveFrameRight` | Ctrl+Shift+Right | Cmd+Shift+Right |
| Delete Selected Frames | `sprites.deleteFrames` | Delete | Delete |
| Previous Frame | `sprites.previousFrame` | PageUp | PageUp |
| Next Frame | `sprites.nextFrame` | PageDown | PageDown |
| First Frame | `sprites.firstFrame` | Home | Home |
| Last Frame | `sprites.lastFrame` | End | End |
| Reverse Frames | `sprites.reverseFrames` | None | None |
| Resize Sprite... | `sprites.resize` | None | None |
| Recalculate Collision Mask | `sprites.autoMask` | None | None |
| Import Mask... | `sprites.importMask` | None | None |
| Export Mask... | `sprites.exportMask` | None | None |
| Remove Mask | `sprites.removeMask` | None | None |
| Edit Mask Bounds | `sprites.editMask` | M | M |
| Show Onion Skin | `sprites.onionSkin` | O | O |
| Add Broadcast Message | `sprites.addBroadcast` | None | None |
| Open in Image Editor | `sprites.openInImageEditor` | None | None |
| Edit in External Program | `sprites.openExternal` | None | None |

## 7. Validation and messages

| Case | Message |
| --- | --- |
| Image larger than 32767 px | "The image is {w} x {h}. Sprites can be at most 32767 pixels in each direction." |
| Mixed frame sizes after import | "Frames have different sizes. Resize all to {w} x {h}?" with buttons "Resize All" and "Keep Different Sizes" (the file allows only one size per sprite; the second choice crops to the sprite size) |
| Unsupported image format | "'{file}' is not a supported image. Use PNG, JPG, GIF or BMP." |
| Origin outside 16 times the image size | Warning "The origin is far outside the image." |
| Delete sprite in use | Reference scan dialog listing objects and rooms using it |
| Texture page full | After import: Warning in Problems "Texture group '{group}' now needs {n} pages." (information, not an error) |

## 8. Undo

Each property commit, frame operation, mask import, origin placement drag (one step per drag) and import is one step named after the action. Resize is one step including the repack. Import of many frames is one step.

## 9. Empty and loading states

- Sprite with no frames (malformed): "This sprite has no frames. Import images to add some." with the Import button.
- Frame image still decoding: the thumbnail shows a placeholder and the canvas shows "Loading frame..." in the editor status; large sprites decode on a background job and cache at display size.
- Unknown texture format: "This frame uses a texture format G3MStudio cannot show. It is kept unchanged when you save." (UTMT-FILE-08).
- No sprites in the game: Explorer group shows 0; the editor never opens empty.

## 10. Parity coverage

- UTMT-SPR-01: sections 3.1 and 4.3.
- UTMT-SPR-02: section 4.1.
- UTMT-SPR-03: section 3.2.
- UTMT-SPR-04: sections 3.1 and 3.4.
- UTMT-SPR-05: sections 3.4 and 5.
- UTMT-CTRL-03: section 4.2 (texture page item display on a checkerboard).
- UTMT-SET-12: section 4.2.
- UTMT-FILE-08: section 9.
