# Fonts

Status: proposed. The editor for FONT resources: properties, glyph table, glyph rectangles, kerning, regeneration from a font file, and a preview. Shared rules: [../ux-standards.md](../ux-standards.md). Images of the glyph texture live in [texture-pages.md](texture-pages.md).

## 1. Purpose and entry points

A compiled font is a bitmap on a texture page plus a table that says where each glyph is and how to place it. This editor edits the table and the properties, and can rebuild the bitmap from a font file (F-RES-8). Opens from Explorer > Fonts, Go To `fnt:`, a text item or code reference, References results.

## 2. Layout

```
+--------------------------------------------------------------------------------------+
| fnt_main   Index 3   Arial 12 pt   224 glyphs     [Regenerate] [Import Font File...] |
+---------------------------+----------------------------------------------------------+
| Properties                |  Preview: [Sample v] [Game Preview]    zoom [-] 200% [+]   |
|  Name  [fnt_main]         |  The quick brown fox jumps over...                        |
|  Display name [Arial]     |                                                           |
|  Size (em) [12]  B [ ] I [ ]| Texture view: glyph boxes on the page                    |
|  Charset [0]  AA [x]      |  (selected glyph outlined; shift green, offset blue)      |
|  Range  [32] to [127]     +----------------------------------------------------------+
|  Scale X [1] Y [1]        | Glyphs  Ranges  Kerning                                    |
|  Ascender [..] Offset [..]| Char  Rect (x,y,w,h)   Shift  Offset                       |
|  SDF spread [..] Line [..]| 'A'   120,44,12,14     13     0                            |
|  Texture [page item]      | [Add Glyph] [Sort] [Update Range]                          |
+---------------------------+----------------------------------------------------------+
```

Left: properties. Top right: preview (Sample text or Game Preview of the real texture) and a texture view. Bottom right: tabs Glyphs, Ranges and Kerning.

## 3. Properties (UTMT-FNT-01)

| Field | Control | Rules |
| --- | --- | --- |
| Name | Name field | |
| Display name | Text | Name of the typeface shown to the game |
| Font size (em) | Number, float 1 to 1000 | |
| Bold, Italic | Checkboxes | |
| Range start, end | Two number fields (code points, shown as `U+0020` with the character) | 0 to 0x10FFFF; start <= end: "The range must start before it ends." |
| Charset | Number 0 to 255 | |
| Anti-aliasing | Combo: None, 1, 2, 3, 4 levels (or checkbox for older files) | |
| Texture | Reference field (Texture Page Item) | Shown with thumbnail; changing it moves the glyph source |
| Scale X, Y | Number, float > 0 | |
| Ascender, ascender offset | Number, integer | |
| SDF spread | Number, integer 0 to 255 (0 means no SDF) | Shown for versions with SDF |
| Line height | Number, integer | Recomputed by "Update Metrics" |
| Texture group | Combo | |

## 4. Glyph table (UTMT-FNT-02, UTMT-FNT-03, UTMT-FNT-05)

Table columns: Char (character and `U+XXXX`), Source rectangle (x, y, width, height), Shift (advance), Offset. Rows virtualize; type-ahead jumps to a character; search by typing a character into the filter box finds it.

1. Selecting a row selects the glyph box on the texture view and scrolls it into view. Clicking a box on the texture selects its row. Colours: source rectangle in `Select`, shift drawn as a green arrow in `Success`, offset as a blue arrow in `Info`; both also carry labels so colour is not the only cue.
2. Glyph rectangle editor (UTMT-FNT-03): "Edit Glyph Rectangle..." (F2 on a row) opens an editor tab with the page zoomable; drag handles resize the rectangle; arrow keys nudge by 1 pixel (Shift: 8); number fields edit the same values; "Help" explains the rectangle, shift and offset. A canvas drag alternative is the number fields.
3. Maintenance (UTMT-FNT-05): "Add Glyph" (Insert) creates an empty glyph for a character you type; "Sort Glyphs" sorts by code point; "Update Range" sets range start and end from the glyph list. Delete removes glyphs (undoable).
4. Kerning (UTMT-FNT-04): the Kerning tab shows the glyph's kerning list: preceding character and additional shift. A "Back" arrow returns to the glyph list. Add and Delete rows with the list grid.

## 5. Ranges and regeneration

The Ranges tab lists character ranges (first and last code point) with buttons Normal (32 to 127), ASCII (0 to 255), Digits, Letters, From Code (scan strings used in code), From File (scan a text file), Delete. Typing characters into the preview adds or removes glyphs.

"Regenerate" (Ctrl+R) rebuilds the bitmap and the table from a font file: choose a TTF or OTF ("Import Font File..." or the installed font list), size, bold, italic, anti-aliasing, range list, SDF, rounding and hinting (Options dialog: Hinting Normal/Light/None, Interpreter, Rounding, Apply Kerning). A preview shows the result. The change repacks the glyph texture. If the font cannot be found, only metadata stays editable and the page says "The font file '{name}' was not found. Existing glyphs are kept." with "Locate Font File...".

## 6. Preview

Modes: Sample (editable text, default "The quick brown fox jumps over the lazy dog 0123456789") and Game Preview (the real texture at 100%). Zoom 50% to 800%. Missing glyphs render as a box with the code point. Right-to-left layout is not simulated.

## 7. Actions and shortcuts

| Command | Id | Windows and Linux | macOS |
| --- | --- | --- | --- |
| Regenerate Font Texture | `fonts.regenerate` | Ctrl+R | Cmd+R |
| Add Character Range | `fonts.addRange` | None | None |
| Delete Character Range | `fonts.deleteRange` | Delete | Delete |
| Rendering Options... | `fonts.options` | None | None |
| Import Font File... | `fonts.importGlyphs` | None | None |
| Export Font Texture... | `fonts.exportAtlas` | None | None |
| Sort Glyphs | `fonts.sortGlyphs` | None | None |
| Update Range From Glyphs | `fonts.updateRange` | None | None |
| Create Empty Glyph | `fonts.addGlyph` | Insert | Insert |
| Edit Glyph Rectangle... | `fonts.editRect` | F2 | F2 |
| Edit Kerning... | `fonts.kerning` | None | None |

## 8. Validation and messages

| Case | Message |
| --- | --- |
| Glyph rectangle outside the texture | "The glyph rectangle must be inside the {w} x {h} texture." |
| Duplicate character | "'{char}' (U+{code}) already has a glyph." |
| Font file unreadable | "'{file}' is not a TrueType or OpenType font." |
| Regenerate overflow | "The glyphs do not fit in the page. Reduce the size or the range." |
| Delete a font used by text items or code | Reference scan dialog |

## 9. Undo

Undoable: property commits, glyph edits, nudges, add, delete, sort, kerning edits and regeneration. Grouping: a nudge sequence is one step per key press group; every other action is one step.

## 10. States

Empty:
- No glyphs: "This font has no glyphs. Choose Regenerate to build them from a font file."

Loading:
- Texture not loaded: "Loading glyph texture..." in the texture view; the table works meanwhile.

Error:
- Glyph image unreadable: "This font's texture uses a format G3MStudio cannot show. The table is editable."
- Font unreadable: "The font '{name}' could not be read: {error}."

## 11. Parity coverage

- UTMT-FNT-01: section 3.
- UTMT-FNT-02: section 4.
- UTMT-FNT-03: section 4, item 2.
- UTMT-FNT-04: section 4, item 4.
- UTMT-FNT-05: section 4, item 3.
- F-RES-8 (glyph view, metrics, texture preview, import): sections 4 to 6.
