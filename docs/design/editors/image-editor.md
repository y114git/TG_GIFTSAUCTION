# Image editor

Status: proposed. A pixel editor for sprite frames, tile set images and single texture page items (F-RES-9). It keeps the core of GameMaker's Image Editor: the tools, frames strip and frame operations. Layers are left out because data files store flattened bitmaps. Shared rules: [../ux-standards.md](../ux-standards.md). The sprite properties are in [sprites.md](sprites.md).

## 1. Purpose and entry points

Fix or draw pixels without leaving the program. For heavy work the "Edit in External Program" loop in the sprite editor stays the better path; this editor covers quick edits, frame operations and effects.

Opens as its own tab from: a sprite frame (double-click), "Edit Image" in the sprite or tile set editor, a texture page item ("Edit Item"), the context menu "Open in Image Editor", and Go To `img:`. In the Canvas preset it opens as a card like any other editor, not as a separate top-level tab (this differs from GameMaker, deliberately).

## 2. Layout

```
+--------------------------------------------------------------------------------------+
| spr_player (Image)   * unapplied     Tool: Pencil   Size [3 ] [----o--]  [x] Smooth   |
+------------+-----------------------------------------------------------+-------------+
| Frames     |                                                           | Colours     |
| [1][2][3*] |                                                           | [P][S] swap |
| [+] [onion]|            canvas with checkerboard and pixel grid        | palette     |
| [<<][>][>>]|                                                           | recent      |
|            |                                                           | Brushes     |
|            |                                                           | Toolbox     |
+------------+-----------------------------------------------------------+-------------+
| x 12  y 40   sel 16x16   Frame 3/8   Click to paint (Shift: straight line, Ctrl: pick)|
+--------------------------------------------------------------------------------------+
```

Left: frame strip (96 px wide, vertical, 56 px thumbnails) with add, onion skin, play. Centre: canvas. Right: Colours (primary and secondary swatches with a mouse-button diagram, swap with X, palette grid, recent colours row, palette menu to import or copy colours between sprites), Brushes (10 presets: square and round, 1 to 16 px; a pasted selection adds a custom brush), Toolbox (icon buttons, 2 columns, 32 px). Top: the tool options bar for the active tool.

## 3. Tools

All tools are commands; the key is active when the canvas has focus. Left button paints with the primary colour, right button with the secondary.

| Tool | Key | Options | Behaviour |
| --- | --- | --- | --- |
| Pencil | D | Size 1 to 256, Smooth | Shift draws a straight line from the last point; Ctrl picks a colour |
| Eraser | E | Size, Smooth | Sets alpha to 0 |
| Fill | F | Alpha tolerance 0 to 255, contiguous or global | Click fills similar pixels |
| Line | L | Size | Press, drag, release |
| Rectangle | R | Outline, Filled, Both | Alt draws from the centre; Shift keeps a square |
| Ellipse | C | Outline, Filled, Both | Alt draws from the centre; Shift keeps a circle |
| Polygon | Y | Outline, Filled | Click points; Enter closes; Escape cancels |
| Colour Picker | I | Sample: pixel, 3 x 3 average | Click sets primary; right-click sets secondary |
| Rectangle Select | S | | Drag; Escape cancels |
| Magic Wand | W | Tolerance, Colour only | Selects similar pixels |
| Move | V | | Moves the selection or the whole frame; pixels moved off the canvas are cropped when the tool changes |
| Colour Remove, Colour Replace | In the Effects menu | Tolerance, limited to selection | |

Brush size: [ and ] change the size by 1 (Shift: 5). The pointer shows the brush outline. Eight transform commands apply to the selection or brush: Rotate 90 either way, Flip Horizontal, Flip Vertical, Mirror (toolbar menu "Selection").

## 4. Frame operations

The Image menu (editor menu while this tab has focus).

| Command | Behaviour |
| --- | --- |
| Add Frame, Insert Frame, Delete Frames | Frame strip actions; keys in the table below |
| Cut, Copy, Paste Frame or Selection | Image data to the clipboard; paste creates a floating selection |
| Select All, Cancel Selection, Invert Selection | Ctrl+A, Escape, Ctrl+I |
| Import Images... | Dialog: resize, crop or pad, anchor 3 x 3; adds frames |
| Convert Sheet to Frames... | Frame count, frame width and height, offsets, separation (the `_stripN` style) |
| Export to PNG... | One frame or all; multiple frames export as `{name}_strip{N}.png` |
| Resize All Frames... | Scale or canvas, anchor, pixels or percent, interpolation |
| Crop All Frames to Selection | Uses the rectangle selection |
| Auto Trim All Frames | Removes the transparent border shared by all frames |
| Reverse Frames | |
| Mirror, Flip, Rotate 90 All Frames | Current frame or all frames |
| Blur..., Greyscale... | Effects with preview and amount, limited to the selection or the frame |

## 5. Session and apply

The editor works on a copy of the frames. Strokes and frame operations go to the editor's own history (limit 200). The workspace model changes when: the tab loses focus, the tab closes, the user chooses "Apply" (Ctrl+Enter in this editor), or the data file is saved. The apply is one workspace undo step "Edit image '{name}'" and repacks the affected texture pages in the background; the tab title shows "(Image)" and an unapplied dot until then. If the repack fails the model keeps its old images and Problems gets an entry with a Retry action.

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
| Pencil | `image.pencil` | D | D |
| Eraser | `image.eraser` | E | E |
| Fill | `image.fill` | F | F |
| Line | `image.line` | L | L |
| Rectangle | `image.rectangle` | R | R |
| Ellipse | `image.ellipse` | C | C |
| Polygon | `image.polygon` | Y | Y |
| Colour Picker | `image.picker` | I | I |
| Rectangle Select | `image.select` | S | S |
| Magic Wand | `image.wand` | W | W |
| Move | `image.move` | V | V |
| Swap Primary and Secondary Colour | `image.swapColours` | X | X |
| Smaller Brush | `image.brushSmaller` | [ | [ |
| Larger Brush | `image.brushLarger` | ] | ] |
| Invert Selection | `image.invertSelection` | Ctrl+I | Cmd+I |
| Add Frame | `image.addFrame` | Insert | Insert |
| Delete Frames | `image.deleteFrames` | Delete | Delete |
| Previous Frame | `image.previousFrame` | PageUp | PageUp |
| Next Frame | `image.nextFrame` | PageDown | PageDown |
| Onion Skin | `image.onionSkin` | O | O |
| Resize All Frames... | `image.resizeAll` | None | None |
| Crop All Frames to Selection | `image.cropToSelection` | None | None |
| Auto Trim All Frames | `image.autoTrim` | None | None |
| Rotate All Frames 90 Degrees | `image.rotate90` | None | None |
| Flip or Mirror Frames | `image.flipFrames` | None | None |
| Import Images... | `image.importImages` | None | None |
| Convert Sheet to Frames... | `image.convertToFrames` | None | None |
| Export to PNG... | `image.exportPng` | None | None |
| Blur... | `image.blur` | None | None |
| Greyscale... | `image.greyscale` | None | None |

Frame keys: PageUp and PageDown change frame; Home and End are used by the canvas for first and last when the strip has focus.

## 7. Validation and messages

| Case | Message |
| --- | --- |
| Image would exceed 32767 pixels | "The result is {w} x {h}. The limit is 32767 in each direction." |
| Paste larger than the canvas | Dialog "Expand the canvas to fit the pasted image?" with Expand, Crop, Cancel |
| Repack failed | "Could not place the new image on a texture page: {reason}. Your edits are kept in this tab. Try again or change the texture group." |
| Last frame deleted | "A sprite needs at least one frame." |

## 8. Undo

Editor history: per stroke, per fill, per frame operation; coalescing for continuous strokes. Ctrl+Z inside the canvas uses it. After Apply, the workspace step can be undone from outside the tab; the editor history is cleared on Apply and says so in the status bar: "Applied. Earlier strokes can be undone from the workspace history."

## 9. Empty and loading states

- Loading frames: "Loading frames..." with a progress bar for sheets over 4096 px.
- Zero frames: "This image has no frames. Import an image to start."
- Large canvas (over 8192 px in a direction): a notice "Large images edit slowly. Consider editing in an external program." with a link to the external-edit action.

## 10. Parity coverage

- No UTMT parity item: the image editor is new in G3MStudio (F-RES-9). Its frame operations also back UTMT-SPR-02 (frame list) and UTMT-BG-03 (tile import) through the shared commands.
- UTMT-TEX-02: texture page item "Edit Item" opens here (section 1).
