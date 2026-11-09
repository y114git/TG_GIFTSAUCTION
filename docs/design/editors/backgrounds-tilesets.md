# Backgrounds and tile sets

Status: proposed. The editor for BGND resources: GMS1 backgrounds and GMS2 tile sets, including the tile id table, tile animation and auto tiles. Painting tiles into rooms is in [rooms.md](rooms.md). Texture page effects are in [texture-pages.md](texture-pages.md). Shared rules: [../ux-standards.md](../ux-standards.md). Shell: [../shell.md](../shell.md).

## 1. Purpose and entry points

A BGND entry is a background image (GMS1) or a tile set (GMS2): one texture page item cut into a grid of tiles with an id table. The editor shows the image with the grid and tile numbers, edits the grid fields, and edits tile animation and auto tile rules where the file stores them. It follows GameMaker's Tile Set editor (properties, preview, animated tiles, auto tiles; [../../research/gamemaker/editors.md](../../research/gamemaker/editors.md) section 2.3). It does better than UTMT by showing the grid and tile numbers on the image, by editing animations visually, and by finding the rooms that use any tile (UTMT-SRCH-07).

Opens from Explorer > Tile Sets (labelled Backgrounds for GMS1 files, UTMT-TREE-10), Go To `bg:`, a tile layer's tile set field ("Edit Tile Set") and Find Usages results.

## 2. Layout

```
+----------------------------------------------------------------------------------------+
| ts_ground   Index 6   16 x 16   Tiles 256 (16 columns)   Animated 12      [Export][Import]|
+---------------------+------------------------------------------------------------------+
| Properties          |  tile image on a checkerboard with the tile grid                 |
|  Name [ts_ground]   |  +-------------------------------------------+   [-] 300% [+][fit]|
|  Transparent [ ]    |  | 0  1  2  3  4  5  ...  tile numbers       |   Grid [x] Numbers|
|  Smooth [x]         |  |    [selected tile 18 highlighted]         |   [x] Animated    |
|  Preload [ ]        |  +-------------------------------------------+                    |
|  Texture [tpag 82]  +------------------------------------------------------------------+
|  v Grid             |  Tile 18   Id 18   Used in 3 rooms [Find Rooms]   Animation: none |
|    Tile 16 x 16     |  Tabs: [Tile IDs] [Animation] [Auto Tiles]                       |
|    Border 2 , 2     |  Index  Id                                                       |
|    Columns 16       |  17     17                                                       |
|    Count 256        |  18  >  18                                                       |
|  v Animation        |  [Add...]                                                        |
+---------------------+------------------------------------------------------------------+
```

Left column 280 px. The image fills the top right; the bottom right has tabs for the tile id table, animations and auto tiles. For GMS1 backgrounds only the Properties and image are shown.

## 3. Properties

### 3.1 Background properties (UTMT-BG-01)

| Field | Control | Rules |
| --- | --- | --- |
| Name | Name field | Resource name rules |
| Transparent | Checkbox | Old transparency flag |
| Smooth | Checkbox | |
| Preload | Checkbox | |
| Texture | Texture page item reference | Opens the item; "Replace Image..." imports a PNG (3.3) |

### 3.2 Tile set properties (UTMT-BG-02, UTMT-BG-05)

Shown for GMS2 files.

| Field | Control | Rules |
| --- | --- | --- |
| Tile set version | Read-only | Stored version of the record |
| Tile width, height | Two number fields | 1 to the image size |
| Output border X, Y | Two number fields | Duplicated edge pixels around each tile, default 2 |
| Tile columns | Number | Columns of the image grid |
| Items per tile | Number | Frames stored per tile; 1 means no animation |
| Tile count | Number | Total tiles; "Recount" sets it from the image and the grid |
| Frame time | Number with unit | Stored microseconds per animation frame; shown with a frames-per-second equivalent |
| Exported sprite | Sprite reference | Files from GM 2023.8; the sprite the tile set was built from |
| Tile separation X, Y | Two read-only-by-default numbers | Files from GM 2024.14.1; derived from the borders (UTMT-BG-05). "Edit" unlocks them with the note "This value is normally derived. Change it only to match another tool." |

Changing the grid fields redraws the grid at once. A grid that does not divide the image shows a warning (section 8). Texture group is a combo; moving it repacks (see texture-pages.md).

### 3.3 Image import and export

Export Image writes the texture page item as PNG. Import Image replaces it from a PNG; if the size differs, a dialog offers "Keep grid, resize image", "Fit grid to new size" or Cancel. The result repacks onto texture pages and reports the page count change.

## 4. Tile image and tile id table

### 4.1 Image view (UTMT-BG-04)

1. Grid overlay in `Info`, tile numbers at 400% and above, tile 0 hatched (the empty tile). A selected tile has the selection outline plus a corner label.
2. Clicking a tile selects it and selects its row in the id table (UTMT-BG-04). Right-click offers Find Rooms Using This Tile (UTMT-SRCH-07), Copy Tile Id and Export Tile as PNG. Arrow keys move the selection by tile, Home and End go to first and last tile.
3. Drag selects a rectangle of tiles for batch actions (copy ids, assign to an animation).
4. Zoom 25% to 3200% with the shared canvas commands; status line shows pixel and tile under the pointer.

### 4.2 Tile id table (UTMT-BG-03)

List grid (ux-standards section 11) of the stored tile ids: index and id. The last row "Add..." appends an id. Selecting a row highlights the tile on the image and scrolls it into view; selecting a tile selects the row. Edit an id in place; "Renumber..." resets ids to 0..N-1. Multi-select to delete. With Items per tile above 1, the table shows one group of frames per tile and the group header names the tile.

## 5. Tile animation and auto tiles

### 5.1 Animation tab

Files with items per tile above 1 store a table of frame ids per tile. The tab lists animations (the first tile id of each) with frame count and a small live preview. Add Animation asks for a frame count (a power of two: 2, 4, 8, 16), then the user clicks tiles in order on the image to fill the frames (the keyboard alternative is typing ids into the row). Play shows the animation at the tile set speed (starts paused). The speed field is shown for the tile set as a whole, matching the stored frame time. Rules shown as text: a tile that starts its own animation uses it; a tile in exactly one animation uses that one from its position; a tile in more than one does not animate (GameMaker behaviour). Delete removes an animation and reverts the tiles to static ids.

Editing animation changes Items per tile for the whole set; the dialog says so before Apply ("All tiles will store {n} frames.") because the table is uniform.

### 5.2 Auto tiles tab

GameMaker stores auto tile templates (47 tile and 16 tile) in the project, not always in the data file. The tab shows the rules only when the data file contains them, and otherwise shows "This data file does not store auto tile rules. G3MStudio keeps them in the workspace sidecar." The sidecar rules let the room tile painter's Auto Tile tool (rooms.md section 6.2) work in files that lack them; they never change the data file. Template editing: pick the 47 or 16 template, click a template cell, click the tile on the image to assign it; assigning tile 0 clears; "Show Template" overlays the template on the image.

### 5.3 Brush library

Brushes built in G3MStudio's tile painter (named groups of tiles) are stored in the sidecar per tile set and appear in the room tile palette. They are authoring aids and do not change the data file.

## 6. Actions and shortcuts

| Command | Id | Windows and Linux | macOS |
| --- | --- | --- | --- |
| Add Tile Animation | `tilesets.addAnimation` | Insert | Insert |
| Find Rooms Using This Tile | `tilesets.findRooms` | None | None |
| Show Tile Numbers | `tilesets.showNumbers` | N | N |
| Recount Tiles | `tilesets.recount` | None | None |
| Renumber Tile IDs... | `tilesets.renumber` | None | None |
| Export Image... | `tilesets.exportImage` | None | None |
| Import Image... | `tilesets.importImage` | None | None |
| Export Tile as PNG... | `tilesets.exportTile` | None | None |
| Edit Auto Tiles | `tilesets.editAutoTiles` | None | None |

Canvas commands (`canvas.*`) apply to the image.

## 7. Find rooms using a tile (UTMT-SRCH-07)

Lists rooms and layers whose tile maps or tile elements use the selected tile id (any flip or rotate variant, with a note). The results open in the References panel with "Open Room at Tile", which opens the room editor, selects the layer and centres the first match.

## 8. Validation and messages

| Case | Message |
| --- | --- |
| Grid does not divide the image | Warning "The image is {w} x {h} and the grid uses {tw} x {th} tiles with borders. {n} pixels are not part of any tile." |
| Tile count differs from the grid | "The tile count is {a} but the grid has {b} tiles." with a Recount fix |
| Tile id outside the image | Table row in the invalid style, "Tile id {id} is outside the tile set." |
| Items per tile mismatch with the id table length | "The id table has {n} entries but {m} are expected." |
| Animation frame count not a power of two | "Use 2, 4, 8, 16 or another power of two." |
| Delete in use | Reference scan dialog listing rooms |
| Backgrounds with GMS2-only fields | Hidden, not disabled |

## 9. Undo

Each field commit, id table edit, animation edit, image import and renumber is one step. Image import includes the repack.

## 10. Empty and loading states

- Tile set without an image: "This tile set has no image. Import a PNG to add one."
- Image decoding: "Loading image..." over the view; the table works.
- Unknown texture format: "This image uses a texture format G3MStudio cannot show. It is kept unchanged when you save."
- No tile sets in the game: the Explorer group shows 0.

## 11. Parity coverage

| ID | Feature | Where |
| --- | --- | --- |
| UTMT-BG-01 | Background properties | 3.1 |
| UTMT-BG-02 | GMS2 tile set properties | 3.2 |
| UTMT-BG-03 | Tile ID table | 4.2 |
| UTMT-BG-04 | Pick tile on texture | 4.1 |
| UTMT-BG-05 | Tile separation display | 3.2 |
| UTMT-SRCH-07 | Tile references | 7 |
| UTMT-TREE-10 | Version-aware labels | 1 |
| UTMT-CTRL-01, UTMT-CTRL-03, UTMT-CTRL-06 | Reference field, texture page item display, list grid | 3, 4 |
| UTMT-TAB-08 | Per-tab state restore (tile set view) | 4.1 |
