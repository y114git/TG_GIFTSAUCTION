# Embedded images

Status: proposed. The editor for EMBI entries: named images that GameMaker 2 data files keep as texture page items outside sprites and fonts. Texture pages are in [texture-pages.md](texture-pages.md). Shared rules: [../ux-standards.md](../ux-standards.md). Shell: [../shell.md](../shell.md).

## 1. Purpose and entry points

An embedded image entry is a name plus a reference to a texture page item. The runtime uses these entries for images the build adds itself (for example splash or UI images). UTMT lists them with the two fields and nothing else (UTMT-TEX-06, priority P2). G3MStudio keeps that simplicity and adds the image view, replacement and a used-by check, because the entry is only useful if the user can see the picture.

The chunk exists in GMS 2 files (version 1 header). It is hidden for GMS1 files.

Opens from Explorer > Data > Embedded Images, Go To `emb:`, a texture page item's "Used by" list and Find Usages results.

## 2. Layout

```
+---------------------------------------------------------------------------------------+
| Embedded images   6 entries                                          [search...] [Add] |
+---------------------------+-----------------------------------------------------------+
| Index  Name        Item   | emb_splash     Index 2                                    |
| 0      emb_logo    104    |  Name [emb_splash]  (string)                              |
| 1      emb_load    105    |  Texture page item [tpag 106 ...] [Open] [Choose...]      |
| 2    > emb_splash  106    |  Size 640 x 360   Page 4                                  |
|                           |  +-----------------------------------+                    |
|                           |  | image on a checkerboard           |  [-] 100% [+] [fit]|
|                           |  +-----------------------------------+                    |
|                           |  [Export...] [Replace...] [Edit in Image Editor]           |
+---------------------------+-----------------------------------------------------------+
```

Left: list of entries (index, name, item index, thumbnail at 24 px). Right: detail form with the image view (texture page item display, UTMT-CTRL-03).

## 3. Entry fields (UTMT-TEX-06)

| Field | Control | Rules |
| --- | --- | --- |
| Index | Read-only | |
| Name | String reference field | The entry name; unique among embedded images. Uses the shared-string prompt |
| Texture page item | Reference field | Picker of texture page items; the picker highlights unused items. Drag a page item from the texture page viewer onto the field to link |
| Size | Read-only | From the item |
| Texture page | Read-only | Page index with Open |

## 4. Actions

| Command | Behaviour |
| --- | --- |
| Add | New entry: asks for a name, then offers "New image from file..." (creates a texture page item and packs it) or "Use existing item..." |
| Export... | PNG of the image |
| Replace... | Imports a PNG into the linked item (same rules as texture-pages.md section 3.2, including the repack prompt) |
| Edit in Image Editor | Opens [image-editor.md](image-editor.md) on the item |
| Delete | Removes the entry; the texture page item stays (it may be used elsewhere). If no one else uses the item, a notification offers "Remove item too" |
| Find Usages | Lists code that mentions the entry name |

| Command | Id | Windows and Linux | macOS |
| --- | --- | --- | --- |
| Add Embedded Image | `embedded.add` | Insert | Insert |
| Delete Embedded Image | `embedded.delete` | Delete | Delete |
| Export Image... | `embedded.export` | None | None |
| Replace Image... | `embedded.replace` | None | None |
| Edit in Image Editor | `embedded.edit` | None | None |

## 5. Validation and messages

| Case | Message |
| --- | --- |
| Item reference outside the table | Invalid style on the field and Problems warning "Embedded image '{name}' points at a texture page item that does not exist." |
| Duplicate name | "'{name}' is already used by an embedded image." |
| Item shared with a sprite | Information "This item is also used by sprite '{sprite}'. Replacing it changes both." |
| Image decode fails | "This image uses a texture format G3MStudio cannot show. It is kept unchanged when you save." |

## 6. Undo

Field commits, add, delete, link changes, replace and import are single steps named after the action.

## 7. Empty and loading states

- No embedded images: "This data file has no embedded images." (The chunk is often empty.)
- Image decoding: "Loading image..." in the view; the form works at once.

## 8. Parity coverage

| ID | Feature | Where |
| --- | --- | --- |
| UTMT-TEX-06 | Embedded images (EMBI): name and texture page item | 3 |
| UTMT-CTRL-01, UTMT-CTRL-02, UTMT-CTRL-03 | Reference field, string reference field, texture page item display | 2, 3 |
