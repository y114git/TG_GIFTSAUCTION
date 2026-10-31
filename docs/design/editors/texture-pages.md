# Texture pages and texture groups

Status: proposed. Four related editors: the texture page item (TPAG), the embedded texture (TXTR page), the texture group (TGIN), and the Texture Page Viewer that shows how everything sits on the pages. Also the embedded image entries (EMBI) and repacking. Shared rules: [../ux-standards.md](../ux-standards.md). Sprite, font and tile set editors link here for their images.

## 1. Purpose and entry points

Texture pages are the hardest part of a data file to picture. These editors show every page, which items sit where, which resource owns each item, and let the user replace images and repack.

| Editor | Opens from |
| --- | --- |
| Texture page item | Explorer > Data > Texture Page Items, a sprite frame's "Show Item", References results, a hit on the page viewer |
| Embedded texture | Explorer > Data > Embedded Textures, the page viewer |
| Texture group | Explorer > Data > Texture Groups, Tools > Texture Groups..., a sprite's Texture group field |
| Texture Page Viewer | Tools > Texture Page Viewer, the status bar of an embedded texture, the "Preview" link in the group editor |
| Embedded image (EMBI) | Explorer > Data > Embedded Images |

## 2. Texture Page Viewer layout

```
+--------------------------------------------------------------------------------------+
| Texture Pages    Group [All v]  Page [3 of 12 v]   [Show Item Bounds] [Repack...]    |
+--------------------+-----------------------------------------------------------------+
| Pages (12)         |   (page image 2048 x 2048 on a checkerboard)                    |
| [thumb] 0  2048x.. |   hover highlights the item under the pointer                   |
| [thumb] 1  2048x.. |   item bounds drawn in Info when "Show Item Bounds" is on       |
| [thumb] 2  ...     |                                                                 |
|                    |   [-] 50% [+] [fit]   x 120, y 844   item 2210 spr_player#3     |
| Items on page 3    +-----------------------------------------------------------------+
| 2210 spr_player #3 | Selected item: source 0,0 64x64  target 0,0 64x64  page 3       |
| 2211 spr_player #4 |                                                [Open Item]      |
+--------------------+-----------------------------------------------------------------+
```

1. Left: a page list (thumbnail, index, size, format, group) above an items list for the selected page (index, owner resource and frame, size). Selecting an item selects its box on the canvas and the reverse (UTMT-TEX-05).
2. Canvas: zoom 6.25% to 3200% (Ctrl+wheel, Ctrl+=, Ctrl+-), fit (Ctrl+0), actual size (Ctrl+Shift+0), pan Space+drag. Hovering highlights the item under the pointer with a 2 px `Info` outline and shows its name in the status bar (hit test).
3. Context menu on an item: Open in New Tab, Find Usages (UTMT-TEX-02), Replace From PNG..., Export to PNG..., Copy Index.
4. Page memory figure: "{used} of {total} pixels used ({percent}%)" per page.
5. Keys: PageUp and PageDown change page; B toggles item bounds.

## 3. Texture page item editor (UTMT-TEX-01, UTMT-TEX-02)

| Field | Control | Rules |
| --- | --- | --- |
| Source X, Y, width, height | Four number fields (integer) | Rectangle on the page; must stay inside the page: "The source rectangle must be inside the {w} x {h} page." |
| Target X, Y, width, height | Four number fields | Where the pixels land in the owner's frame |
| Bounding width, height | Two number fields | |
| Texture page | Reference field (Embedded Texture) | Changing it moves the item without moving pixels; the editor offers "Move Pixels Too" |
| Preview | Texture page item display (UTMT-CTRL-03) | The item on a checkerboard with its bounding box; below it "Place on page" shows a mini-map of the page with the item outlined |
| Owner | Read-only link | The sprite, tile set or font that uses it; "Find Usages" lists all |

Buttons: "Import..." replaces the item image from PNG (the PNG is fitted to the source rectangle; if sizes differ a dialog offers "Resize Item" and "Repack"), "Export..." writes PNG, "Find Usages" (UTMT-TEX-02), "Edit Image" opens the image editor.

## 4. Embedded texture editor (UTMT-TEX-03, UTMT-TEX-04)

Shows the whole page: size (read-only), "Scaled" (checkbox), "Generated mips" (number), the texture format (PNG, QOI, BZ2 plus QOI, DDS, raw BGRA; UTMT-TEX-08 display), zoom and scroll, fit to view. A load failure shows a warning band "This page uses a format G3MStudio cannot display. The data is kept unchanged when you save." with the format name. Buttons: "Import Page..." (replace the whole page from PNG; size must match unless the "Allow different size" box is ticked, which warns that item rectangles may no longer fit), "Export Page..." (PNG).

Decoding uses a cache of bitmaps per page (weak references; UTMT-TEX-09) and decodes off the UI thread. DDS and unknown formats round-trip untouched.

## 5. Texture group editor (UTMT-TEX-07)

Replaces UTMT's incomplete group editor and doubles as GameMaker's Texture Group Manager (Tools > Texture Groups).

```
+----------------------------------------------------------------------------+
| Group [ Default v ] [Add Group] [Rename] [Delete]                           |
+--------------------------+-------------------------------------------------+
| Members (412)            | Group settings                                   |
| v Sprites (380)          |  Texture pages  [ 5 ] [View Pages]               |
| v Tile Sets (20)         |  Parent group   [None v]                         |
| v Fonts (12)             |  Format         [BZ2 + QOI v]  Type [Regular v]  |
| [Add Member...]          |  Border size    [2]   Mipmaps  [ ]               |
|                          |  Allow scaling  [x]   Auto crop [x]              |
+--------------------------+-------------------------------------------------+
```

1. Members tree: Sprites, Spine sprites, Fonts, Tile Sets with double-click to add (UTMT-TEX-07) replaced by "Add Member..." and drag from the Explorer. Multi-select; "Move to Group..." on a selection; the Inspector shows the group for several resources at once (batch edit, editors.md idea 1).
2. Settings shown when the file stores them (version dependent): Allow Scaling, Automatically Crop, Generate Mipmaps (forces border size 8), Border Size (minimum 0), Parent Texture Group, Texture Group Format (BZ2 + QOI, QOI, PNG, Custom), Texture Group Type (Regular or Dynamic), Dynamic Group Path, Custom Options. Platform "Use Group On" checkboxes are not stored in compiled files and are not shown.
3. Moving assets between groups repacks pages. Before applying, the dialog shows "This changes {n} pages and adds or removes {m} pages. The data file will grow by about {size}." with Apply and Cancel (editors.md 1.12).
4. Deleting a group moves its members to Default and is undoable.
5. Empty group: "This group is empty. Drag resources here or choose Add Member."

## 6. Repacking (F-RES-5)

`texpage.repack` opens a dialog for the chosen groups: page size (256 to 8192, default from the file), padding (border size), algorithm (Best fit, Keep order), options (Allow scaling, Crop), and a dry run that shows the resulting page count, fill percentage per page and size change. "Repack" runs as a job with progress and Cancel; Cancel leaves the data unchanged. Repacking is also run implicitly after image edits (sprites, tile sets, fonts); the implicit run packs only changed items into free space first and falls back to a full group repack with a notification "Repacked group 'Default' because the new image did not fit."

## 7. Embedded images (UTMT-TEX-06)

List grid of EMBI entries: name and texture page item reference (Reference field) with a thumbnail. Add and delete rows. Replace image uses the item editor.

## 8. Actions and shortcuts

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
| Next Texture Page | `texpage.nextPage` | PageDown | PageDown |
| Previous Texture Page | `texpage.previousPage` | PageUp | PageUp |
| Show Item Bounds | `texpage.toggleBounds` | B | B |
| Replace Page From PNG... | `texpage.importPage` | None | None |
| Export Page to PNG... | `texpage.exportPage` | None | None |
| Replace Item From PNG... | `texpage.importItem` | None | None |
| Export Item to PNG... | `texpage.exportItem` | None | None |
| Repack Texture Pages... | `texpage.repack` | None | None |
| Find Usages of Item | `texpage.itemUses` | None | None |

## 9. Validation and messages

| Case | Message |
| --- | --- |
| Source rectangle outside page | Section 3 |
| PNG larger than page | "The image is {w} x {h} but pages are {pw} x {ph}. Use a smaller image or a larger page size." |
| Page import size mismatch | "The new page is {w} x {h}; the old page is {ow} x {oh}. Items may no longer fit. Replace anyway?" |
| Group delete | Confirmation not needed (undoable); notification "Group deleted. {n} resources moved to Default. Undo" |
| Repack cannot fit | "The images do not fit in {n} pages of {size}. Increase the page size or split the group." |
| Unknown format on export | "This page uses format {name}, which cannot be exported as PNG." |

## 10. Undo

Item rectangle commits, imports, group edits, member moves and repacks are single steps. A repack triggered by an image edit is part of that edit's step.

## 11. Empty and loading states

- No texture pages: "This data file has no texture pages."
- Decoding a page: "Decoding page {n}..." with the page thumbnail grey; 4096 x 4096 pages decode in under 300 ms on the reference machine.
- Page not found for an item: "The item points at page {n}, which does not exist." with a Fix to relink.

## 12. Parity coverage

- UTMT-TEX-01, UTMT-TEX-02: section 3.
- UTMT-TEX-03, UTMT-TEX-04: section 4.
- UTMT-TEX-05: section 2.
- UTMT-TEX-06: section 7.
- UTMT-TEX-07: section 5.
- UTMT-TEX-08, UTMT-TEX-09: section 4.
- UTMT-TEX-10: section 6 (the packer is the shared service behind implicit and explicit repacking).
- UTMT-CTRL-03: section 3.
- UTMT-FMT-08: sections 4 and 11 (texture formats).
