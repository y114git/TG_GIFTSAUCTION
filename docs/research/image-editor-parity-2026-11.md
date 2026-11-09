# Image editor parity with GameMaker (2026-11)

Date: 2026-11-06. Sources: [gamemaker/editors.md](gamemaker/editors.md) section 2.2, [gamemaker/gamemaker-hotkeys.md](gamemaker/gamemaker-hotkeys.md) (ImageEditorHotkeys), [gamemaker/README.md](gamemaker/README.md) (image editor), the owner's screenshot of the LTS 2026 image editor, and [../design/editors/image-editor.md](../design/editors/image-editor.md). Nothing was read from the running GameMaker window.

Status words: done (works as GameMaker's does), partial (works, with the limit named), missing (not built; the reason or the open question is named). Files are under `G3MStudio/Editor` (UI) and `G3MStudio.Core/Workspace` (pixels and commands).

## How it is built

- One editor view, `ImageEditorView` (with `ImageCanvas`), serves the document tab (kind `image`, key `sprites:NAME`, command Edit > Edit Image...) and the existing painter window (`PixelPainterWindow`, a thin host). There is no second painter.
- Every tool turns into an `ImageOp` text line. `ImageOps.Run` applies it to the picture. The live canvas and the journal replay both call it, so they give the same pixels.
- In the tab, each mouse action is one `ImageEditCommand` in the workspace history: undo, redo, journal replay after a crash and autosave come from the workspace. It stores the operations and the selection they ran under. The picture goes to the data file as `ReplaceFrameCommand` (same size, own picture) or `FrameOwnPictureCommand` (shared picture or new size, owner decisions Q59 and Q60).
- Pixels live in `PixelBuffer` (partial files Brush, Fill, Transform, Adjust). Writes track a dirty rectangle; the canvas uploads only that rectangle to its bitmap.

## Window parts

| GameMaker feature | Studio | Status | Reference |
| --- | --- | --- | --- |
| Options bar: Size slider and spinner | Spinner 1 to 256 and brush presets; no slider | partial | ImageEditorView.Panels.cs `Options` |
| Options bar: Smooth | Check box and size slider; anti-aliased pencil, eraser, line, shapes, polygon, curve (OI-368) | done | ImageEditorView.Panels.cs `Options`, PixelBuffer.Smooth.cs |
| Options bar: tool options (tolerance, fill style, contiguous) | Tolerance, contiguous, outline/filled/both, round or square brush | done | Panels.cs |
| Frame strip: add, duplicate, play | Previous, next, duplicate (Ctrl+D), remove, play; no thumbnails | partial | Panels.cs `BuildFrames` |
| Frame strip: add a blank frame | Only duplicate (FrameListCommand has no blank add) | missing | |
| Frame reordering by drag | Not built | missing | |
| Onion skin on and off | One frame before and one after at 35 percent | partial | ImageEditorView.cs `UpdateOnion` |
| Onion skin settings (counts, opacity, colour) | Not built | missing | |
| Per-frame onion handles | Not built | missing | |
| Big canvas with checkerboard | Done, theme colours | done | ImageCanvas.Render.cs |
| Coordinates, size, frame, selection size in the status line | Position, size, frame, zoom, selection size | done | ImageEditorView.cs `ShowStatus` |
| Hint line for the tool | One hint per tool, translated | done | `image.hint.*` |
| Brushes panel (round and square sizes) | 10 presets | done | Panels.cs `Brushes` |
| Brush from selection (Ctrl+B) and custom brushes | Not built | missing | |
| Colours panel: palette, primary and secondary, swap | 32 swatches, left click primary, right click secondary, swap (X) | done | Panels.cs `Colours` |
| Colours: recent colours row | Last 16 | done | |
| Colours: colour picker with ARGB and HSV | Hex box with optional alpha only | partial | |
| Colours: edit palette, import and export, copy between sprites | Palette is fixed. Needs a place in the sprite to store it | missing | |
| Toolbox with icons, tool tips with key | 15 tools, path icons, tip with key | done | ImageTool.cs |
| Layers panel: add, delete, folders, order | Add, duplicate, delete, move, merge down; no folders. Stacks live beside the data file, the file holds the flat result (OI-370) | partial | ImageEditorView.Layers.cs, LayerStore.cs |
| Layer opacity, visibility, lock, merge, blend modes | All built; blend modes normal, multiply, screen, overlay, add | done | LayerSet.cs |
| Zoom toolbar: zoom in and out, 1:1, fit | Done; wheel zooms to the cursor | done | ImageCanvas.cs |
| Grid toggle (G), grid colour, cell size | Pixel grid at 6x and more, cell grid every 16 pixels; cell size and colour are not editable in the UI | partial | `GridCell` |
| Grid snap (Ctrl+G) | Not built | missing | |
| Split canvas | Not built | missing | |
| Pan with space or middle mouse | Done | done | ImageCanvas.Input.cs |
| Tile and pattern preview | 3 x 3 repeat of the picture around the canvas | done | `TilePreview` |
| Mirror guides and symmetry | Mirror left-right and top-bottom, dashed axes | done | `Symmetry.cs` |
| Reference image behind the canvas | Not built | missing | |

## Tools

| GameMaker tool | Studio | Status | Reference |
| --- | --- | --- | --- |
| Pencil (D) with size and shape | Square or round, size 1 to 256, symmetry; Shift draws a line from the last point, Ctrl picks | done | `PixelBuffer.Stamp`, `ImageCanvas.Input.cs` |
| Pencil: smoothing | Smooth option (anti-aliasing) built; pressure and spacing are not | partial | |
| Eraser (E) | Sets alpha to 0 | done | |
| Fill (F) with alpha tolerance | Tolerance 0 to 255 on all channels, contiguous or global, scanline fill | done | PixelBuffer.Fill.cs |
| Colour Replace (V) | Every pixel near the clicked colour becomes the primary colour | done | `PixelBuffer.Replace` |
| Colour Remove (H) | Every pixel near the clicked colour becomes transparent | done | |
| Replace with alpha (RightAlt) | Not built | missing | |
| Line (L) | Press, drag, release; brush size and shape | done | |
| Rectangle (R, Shift+R filled) | Outline, filled, both; Alt from centre; Shift square | done | `PixelBuffer.Rectangle` |
| Ellipse (C, Shift+C filled) | Same options as the rectangle | done | `PixelBuffer.Ellipse` |
| Polygon (P, Shift+P filled) | Click points, Enter or double click closes, Escape cancels | done | |
| Spline and Arc (A) | One Bezier curve through 3 or 4 control points; the spline iteration count is fixed | partial | `PixelBuffer.Curve` |
| Text tool (T) | Text box and size in the options bar, click to stamp; Avalonia draws the glyphs, the pixels go into the edit (OI-369) | done | TextRaster.cs |
| Eye dropper (O, and I) | Single pixel, left to primary, right to secondary | done | |
| Eye dropper: 3 x 3 average | Not built | missing | |
| Rectangle selection (S) | Drag; Escape clears | done | `PixelSelection.Rectangle` |
| Paint selection (Q) | Not built | missing | |
| Lasso (free select) | Drag outline, even-odd inside plus the outline pixels | done | `PixelSelection.Lasso` |
| Magic wand (W) with tolerance and colour only | Tolerance and contiguous; colour-only exists in code but has no checkbox | partial | `PixelSelection.Wand` |
| Move or pan (M) | Drag moves the selection or the whole picture; arrows nudge 1, Shift 8 | done | `ImageOps.MoveSelected` |
| Rotate tool (Z, U) | Menu: rotate 90 either way, rotate by angle (nearest neighbour) | partial | Transform menu |
| Mirror and Flip tools (X, Y) | Menu: flip horizontally and vertically on the selection or the picture. X swaps the colours, as the design doc has it | partial | |

## Selection, transform, canvas, effects

| GameMaker feature | Studio | Status | Reference |
| --- | --- | --- | --- |
| Select all, none, invert | Ctrl+A, Escape, Ctrl+I | done | |
| Expand and shrink the selection | Menu, pixels from -64 to 64 | done | `PixelSelection.Expanded` |
| Copy, cut, paste | Ctrl+C, X, V; paste lands at the top left as a selection and switches to Move. The clipboard is inside Studio only, not the system clipboard | partial | ImageEditorView.Actions.cs |
| Paste as a new sprite | Not built (needs a create-sprite command with a picture) | missing | |
| Clear (Delete), fill selection, stroke selection | Done; stroke width in pixels | done | |
| Scale | Menu, new width and height, nearest neighbour; on a selection it scales the selected pixels | done | `PixelBuffer.Scaled` |
| Rotate by angle | Done, picture grows to hold the result | done | `PixelBuffer.Rotated` |
| Skew | Horizontal shear by angle | partial | `SkewedHorizontally` |
| Flip and rotate 90 | Done | done | |
| Nudge | Arrow keys | done | |
| Canvas resize with anchor | Width, height and a 3 x 3 anchor as two numbers 0 to 2 | done | `Resized` |
| Crop to selection, trim to content | Done on the current frame | done | |
| Crop, trim, resize, flip, rotate on all frames | Current frame only | missing | |
| Hue, saturation, brightness, contrast | Done | done | `PixelBuffer.Adjust` |
| Invert colours, greyscale, blur | Done; blur is a box blur of radius 1 to 8 | done | |
| Posterize | Done | done | |
| Outline | One pixel outline in the primary colour | done | |
| Dither | Ordered Bayer 4 x 4 to 2 to 16 levels | done | |
| Effects plugins | Not built | missing | |
| Layer blend modes | See layers | done | |
| Import frame, import strip, convert to frames, export PNG | Existing Replace Frame and GIF export only | missing | |
| Resize all frames, reverse frames, auto trim all frames | Not built | missing | |
| Undo and redo depth | Tab: the workspace history, unlimited, one step per mouse action. Window: unlimited local steps | done | `ImageSession.cs` |

## Keys (as built)

Every key is a command (`img.*`) and can be rebound in Settings > Keyboard shortcuts; the table shows the defaults. I and B are no longer extra keys (Q88). GameMaker's file `Hotkeys/ImageEditorHotkeys` gives the tool keys. The task list asked for B, G, I, M as well, so those are accepted too.

| Key | Action |
| --- | --- |
| D (B) | Pencil |
| E | Eraser |
| V | Replace colour |
| H | Remove colour |
| F | Fill |
| L | Line |
| R, Shift+R | Rectangle outline, filled |
| C, Shift+C | Ellipse outline, filled |
| P, Shift+P | Polygon outline, filled |
| A | Curve |
| O, I | Colour picker |
| S | Rectangle select |
| Q | Lasso (GameMaker uses Q for Paint Selection, which is missing) |
| W | Magic wand |
| M | Move |
| X | Swap primary and secondary colour |
| G | Grid |
| + and - | Brush size |
| 1 and 2 | Previous and next frame |
| Alt+C, Alt+F | 1:1 and fit |
| Ctrl+Z, Ctrl+Y | Undo, redo |
| Ctrl+A, Ctrl+I, Escape | Select all, invert selection, clear selection |
| Ctrl+C, Ctrl+X, Ctrl+V | Copy, cut, paste |
| Ctrl+D | Duplicate frame |
| Delete | Clear selection |
| Space (hold) or middle button, drag | Pan |
| Arrows, Shift+arrows | Nudge the selection by 1 or 8 |
| Enter, double click | Close a polygon or draw a curve |

## Speed (Debug build, this machine, 2048 x 2048)

| Operation | Time | Budget |
| --- | --- | --- |
| Fill of a one-colour picture | about 30 ms | 50 ms |
| Line of 2048 pixels, brush 3 | about 1 ms | 50 ms |
| Move a 1800 x 1800 selection | about 45 ms | 50 ms |

The test is `ImageEditCommandTests.BigPicturesStayUnderTheBudgetForFillLineAndSelectionMove`. The canvas uploads only the changed rectangle. Not measured: the time of `workspace.Edit`, which encodes the picture as PNG on the UI thread for every committed stroke (open question Q74).

## Not done and why

- Pen pressure and stroke spacing: need tablet input.
- Frame list work (blank add, reorder, all-frames operations, import strip): needs new frame commands; a follow-up on top of `FrameListCommand`.
- Texture page item editing ("Edit Item", UTMT-TEX-02) and tile set images: `ImageEditCommand` addresses a sprite frame only.
- Palette management, reference image, custom brushes, split canvas.
