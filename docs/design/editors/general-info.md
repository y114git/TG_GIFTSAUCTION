# General info and game options

Status: proposed. One editor with pages for the GEN8 chunk (general info), the OPTN chunk (game options), the room order, the language table and read-only data-file facts. It mirrors the grouping of GameMaker's Game Options window so people who know GameMaker find things. Shared rules: [../ux-standards.md](../ux-standards.md). Shell behaviour: [../shell.md](../shell.md).

## 1. Purpose and entry points

Shows and edits the settings that identify a game and tell the runner how to start it. Opens from Explorer > Game > General Info and Game Options (both open this editor on the matching page), from the status bar "GameMaker 2023.8, Bytecode 17, Windows" item, and from the palette (`>General Info`, `>Game Options: Fullscreen`).

Resource kind: singleton, not in the resource list; index shown as "General Info". Read-only fields are labelled with a lock icon and the reason in a tooltip.

## 2. Layout

```
+--------------------------------------------------------------------------------------+
| General Info                                          [Reset to Original] [Copy JSON]|
| [search fields...]                                                                   |
+--------------+-----------------------------------------------------------------------+
| General      |  Name              [ UNDERTALE                    ]                      |
| Window and   |  Display name      [ UNDERTALE                    ]                      |
|  Graphics    |  File name         [ UNDERTALE                    ]                      |
| Runtime      |  Config            [ Default                      ]                      |
| Flags        |  Game speed        [ 30 ] frames per second                              |
| Constants    |  Version           [ 1 ].[ 0 ].[ 0 ].[ 0 ]                               |
| Room Order   |  Default window    [ 640 ] x [ 480 ]                                     |
| Language     |  Game GUID         [ 7f1e...                ] [New GUID]                 |
| Data File    |  Steam App ID     [ 391540 ]                                             |
|              |  ...                                                                     |
+--------------+-----------------------------------------------------------------------+
```

Left: page list (a vertical tab list, 160 px). Right: a form with labels 160 px wide above 640 px (labels move above fields below 640 px), fields left-aligned, 8 px row gap, 16 px group gap, group headings in `Strong`. The search box filters fields across all pages and shows the match count; Enter jumps to the next match and switches page.

## 3. Pages and fields

Control names are from [ux-standards.md section 11](../ux-standards.md#11-shared-controls).

### 3.1 General (GEN8)

| Field | Control | Rules | Notes |
| --- | --- | --- | --- |
| Name | Text | 1 to 255 chars, no control characters | Internal game name |
| Display name | Text | Up to 255 chars | Window title and Steam name |
| File name | Text | Up to 255 chars, no path separators | Runner name; used by Launch Game |
| Config | Text | Up to 64 chars | Build configuration name, informational |
| Game speed | Number, 1 to 1000 | Default keeps value | Frames per second; unit label shown |
| Game ID | Number, 0 to 2^32-1 | | |
| Game GUID | Text (GUID) with "New GUID" button | GUID pattern; error "Enter a GUID like 00000000-0000-0000-0000-000000000000." | |
| DirectPlay GUID | Text (GUID) | Same | |
| Version | Four number fields (major, minor, release, build), each 0 to 2^31-1 | | Game version, not GameMaker version |
| Default window size | Size editor W, H, each 1 to 32767 | | |
| Steam App ID | Number, 0 to 2^32-1 | 0 means none | |
| Debugger port | Number, 0 to 65535 | | |
| Allow statistics | Checkbox | | |
| Last object ID, last tile ID | Number, read-only by default | Unlock icon enables editing with a warning "Changing this can break instance IDs." | |
| License MD5, CRC32 | Offset field (hex), read-only | "Recalculate" button writes new values on save when "Update license hash on save" is on | |
| Timestamp | Date and time, read-only | Local time; tooltip shows GMT | |
| Disable GameMaker debugger | Checkbox | Toggling on or off shows a confirmation: "The GameMaker debugger flag changes how the runner starts. With the flag set, the normal runtime can hang. Change it?" with buttons "Change Flag" and "Cancel" (UTMT-GEN-05) | |
| Bytecode version | Combo (read-only display) | A value below 13 or above 17 shows an info bar "Code editing is off for bytecode {n}." | Not editable; changing it is a conversion done in Tools |

Group "GameMaker version": shows the detected version (for example "2023.8"), the detection basis ("Detected from chunk layouts"), and a combo "Override detected version" for files where detection is ambiguous (see platforms-formats.md section 7.4). The override is stored per workspace, not in the data file.

### 3.2 Window and Graphics (OPTN)

Grouped like GameMaker's Windows > Graphics page.

| Field | Control | Rules |
| --- | --- | --- |
| Start fullscreen | Checkbox | |
| Allow fullscreen switching | Checkbox | |
| Interpolate colours between pixels | Checkbox | |
| Synchronize vertically | Checkbox | "Vertex sync" in the file |
| Allow window resize | Checkbox | |
| Borderless window | Checkbox | |
| Display cursor | Checkbox | |
| Scaling | Combo: Keep aspect ratio, Full scale, Fixed scale; plus Number (percent, 1 to 1000) when fixed | |
| Colour depth | Combo of values present in OPTN | |
| Resolution | Combo | |
| Refresh rate (frequency) | Number, 0 to 1000 | 0 means default |
| Priority | Combo | |
| Window colour | Colour field `AARRGGBB` | |
| Load images: back, front | Two Reference-like image slots (embedded image) with "Remove" | Shown when the file has them |
| Load alpha | Number, 0 to 255 | |
| Other OPTN flags | Flags box (UTMT-CTRL-04) | One box for flags without a named control; unknown bits listed as "Unknown bit {n}" |

### 3.3 Runtime

Read-mostly facts and runtime switches: Info flags (Flags box, UTMT-GEN-02), Active targets (Flags box, UTMT-GEN-03), Function classifications (Flags box), Shader extension flag and version (UTMT-GEN-06, shown for GMS2 files).

### 3.4 Constants (OPTN constants table)

List grid with columns Name (text, identifier pattern) and Value (text). Sort by name optional. The last row "Add Constant" inserts a row in edit mode. Duplicate names: "'{name}' is already a constant." Delete with Delete; undoable.

### 3.5 Room Order

List grid of the rooms in start order. Each row: index, room Reference field, "Home" icon on row 1 (the room that starts the game). Alt+Up and Alt+Down move; drag handle also works. Buttons: "Add Room...", "Remove", "Sync With Room List" (rebuilds the order from the room resource order; UTMT-GEN-04, with a confirmation naming the number of changes). Removing the first room changes the start room; the row text says "Start room" next to it.

### 3.6 Language (LANG)

Read-only: unknown value, language count, entry count (UTMT-GEN-08). A note: "Language entries have no editor. They are preserved when you save." The page links to the chunk viewer for a raw table.

### 3.7 Data File

Read-only summary card: path, size, MD5, last modified, container type (FORM), platform (Windows, macOS, Linux, Android, iOS), YYC flag, chunk list with sizes (a table), warnings from load. Buttons: "Show in File Manager", "Copy Path", "Open Data File Health" (platforms-formats.md), "Generate Offset Map...".

## 4. Actions and shortcuts

| Command | Id | Windows and Linux | macOS |
| --- | --- | --- | --- |
| Sync Room Order With Room List | `generalinfo.syncRoomOrder` | None | None |
| Add Constant | `generalinfo.addConstant` | Insert | Insert |
| Copy Field Value | `generalinfo.copyField` | None | None |

Field-level actions in the context menu of any field: Copy Value, Paste Value, Reset to Original (the value in the file as opened), Show in Inspector.

## 5. Validation and messages

| Case | Message |
| --- | --- |
| Name empty | "Enter a name." |
| Value out of range | "Value must be between {min} and {max}." |
| Invalid GUID | "Enter a GUID like 00000000-0000-0000-0000-000000000000." |
| Duplicate constant | "'{name}' is already a constant." |
| Room order references a deleted room | "Room {index} no longer exists. Remove it from the order." with a Fix button |
| Debugger flag set at load | Info bar: "The GameMaker debugger flag is set. The normal runtime can hang with this flag. [Clear Flag] [Ignore]" (UTMT-FILE-07). Also shown before Save and Launch Game. |
| Room order empty | Warning: "The game has no start room." |

Validation runs on commit of each field. Values that fail stay in the field with the invalid style and are not written to the model.

## 6. Undo

Each committed field is one undo step named "Change {field}". Typing in a text field coalesces until commit (Enter, Tab, focus loss). Room Order moves are one step per drag or key press; "Sync With Room List" is one step. Reset to Original is one step.

## 7. Empty and loading states

- Loading: the form shows skeleton rows for 120 ms at most; the model is already in memory so there is usually no wait.
- A data file without OPTN (GMS 1.x variants): the Window and Graphics page shows "This data file has no game options chunk." and the page is disabled.
- A data file without LANG: the Language page shows "This data file has no language table."

## 8. Parity coverage

- UTMT-GEN-01, UTMT-GEN-02, UTMT-GEN-03, UTMT-GEN-05, UTMT-GEN-06: sections 3.1 and 3.3.
- UTMT-GEN-04: section 3.5.
- UTMT-GEN-07: sections 3.2 and 3.4.
- UTMT-GEN-08: section 3.6.
- UTMT-FILE-07: section 5 (debugger flag warning).
- UTMT-TREE-04: Explorer defaults on add are in the shell; the general-info side effects (room order entry on adding a room) are in section 3.5.
