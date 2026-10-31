# Hex view and unknown chunks

Status: proposed. A read-only hex view for any chunk or entry, and the viewer for chunks G3MStudio has no editor for. Chunks that do have small editors are in [tags-feature-flags.md](tags-feature-flags.md) (TAGS, FEAT, DAFL), [filter-effects.md](filter-effects.md) (FEDS), [languages.md](languages.md) (LANG) and [embedded-images.md](embedded-images.md) (EMBI). Chunk and format facts are in [../platforms-formats.md](../platforms-formats.md). Shared rules: [../ux-standards.md](../ux-standards.md).

## 1. Purpose and entry points

Make every byte of the data file visible, even parts G3MStudio does not understand yet, so nothing is hidden or lost. Opens from Explorer > Data > Unknown Chunks, from the chunk list on the Data File page of General Info, and from "Show in Hex View" on any resource or structured row.

## 2. Layout

```
+--------------------------------------------------------------------------------------+
| XYZW   Chunk at 0x00A41C20   size 1,204 bytes   unknown                [Copy Offset]  |
+--------------------------------------------------------------------------------------+
| Structured view (when known)                     | Hex view (monospace)               |
|  field        offset     value                   | 00A41C20  58 59 5A 57 ...  XYZW    |
|                                                  |                                    |
+--------------------------------------------------------------------------------------+
| Selection: 4 bytes at 0x00A41C28   int32 1204   float 1.69e-42   UTF-8 "...."          |
+--------------------------------------------------------------------------------------+
```

Two panes side by side or tabbed. For a known resource the left pane lists the fields with their offsets and selecting a field highlights its bytes (the offsets come from the same table as the offset map). For an unknown chunk the left pane is empty and says "G3MStudio does not know this chunk. It is saved unchanged."

## 3. Unknown chunks

Unknown chunks are kept byte for byte on save (passthrough), listed in Explorer > Data > Unknown Chunks with name (four letters), offset and size, and counted in Problems as an info finding "Unknown chunk XYZW (1,204 bytes) is kept unchanged." They cannot be edited, renamed or deleted from the UI; "Copy Bytes" and "Export Chunk..." are available. A file with only known chunks hides the node unless "Show empty groups" is on.

## 4. Hex view

Monospace (`Code` token), 16 bytes per row (8 or 32 selectable), offset column in hex (copy with `0x`), hex column grouped by 4, ASCII column. The view is read-only, always.

| Control | Behaviour |
| --- | --- |
| Go to Offset (Ctrl+G in this view) | Accepts decimal or `0x` hex; "Offset 0x{value} is beyond the end (0x{end})." for out of range |
| Display type | Byte, int16, int32, float, string, for the selection |
| Alignment | 1, 2 or 4 bytes |
| Copy | As hex, as bytes, offset |
| Find | Hex bytes or text, Enter next, Shift+Enter previous |
| Interpretation row | int8 to int64, float, double and UTF-8 string of the selected bytes |

Chunks over 64 MB page in 1 MB blocks. Keyboard: arrows move by byte or row, Page keys by page, Home and End by row, Shift extends the selection, Ctrl+Home and Ctrl+End jump to the start and end.

## 5. Actions and shortcuts

| Command | Id | Windows and Linux | macOS |
| --- | --- | --- | --- |
| Copy Offset | `chunk.copyOffset` | None | None |
| Go to Offset | `chunk.goToOffset` | Ctrl+G | Control+G |
| Show or Hide Hex View | `chunk.toggleHex` | None | None |
| Add Tag | `chunk.addTag` | Insert | Insert |
| Add Feature Flag | `chunk.addFeatureFlag` | None | None |

## 6. Validation and messages

| Case | Message |
| --- | --- |
| Offset out of range | Section 4 |
| Search not found | "No match for '{text}'." |
| Chunk too large to export at once | "This chunk is {size}. Export it in parts?" |

## 7. Undo

Undoable: nothing. The hex view makes no edits, so there are no steps to group.

## 8. States

Empty:
- No unknown chunks: "This data file has no unknown chunks."

Loading:
- Large chunk loading: hex rows page in with a skeleton row pattern.

Error:
- Bytes unreadable: "The bytes of chunk '{name}' could not be read: {error}. Reload the data file."

## 9. Parity coverage

- UTMT-FMT-02: sections 3 and 4 (unknown chunk passthrough and chunk bytes).
- UTMT-FMT-11: section 2 (offsets for every structured row).
