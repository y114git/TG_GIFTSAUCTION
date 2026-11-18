# Strings

Status: proposed. The editor for the STRG chunk: every shared string in the data file, with where each one is used. Shared rules: [../ux-standards.md](../ux-standards.md). Shell: [../shell.md](../shell.md).

## 1. Purpose

Strings in a data file are stored once and referenced by index from names, code, events and options. This editor lets you read and change one string, see all places that use it, and decide whether a change affects one use or all. It is also the quickest way to translate or reword a game's text.

Opens from Explorer > Data > Strings (list view) or by double-clicking one string; from any String reference field's "..." button; from the `#` mode of Go To; from Find Usages results.

## 2. Layout

```
+------------------------------------------------------------------------------+
| Strings (3,214)                    [search...] [Aa] [Ab|] [.*]   [New String] |
+----------------------------------+-------------------------------------------+
| Index  Text                  Uses |  String 214                               |
| 212    "Press Z to continue"  4   |  +-------------------------------------+  |
| 213    "HP"                   31  |  | * Press Z to continue               |  |
| 214  > "Press Z or ENTER"      2  |  +-------------------------------------+  |
| 215    "obj_player"           1   |  Used by 2 places                         |
|  ...                              |  [Show Uses]                              |
|                                   |  Length 17   Bytes 18 (UTF-8)             |
+-----------------------------------+-------------------------------------------+
```

Left: a virtualized table (Index, Text, Uses, Kind) with sortable headers and type-ahead. Right: the detail pane with a multi-line text box, a uses summary, and character and byte counts. The list takes 60% of the width; the splitter is draggable and remembered.

Kind column values: Name (used as a resource or variable name), Text (used in code or events), Constant, Unused. The Kind filter in the search row narrows the list.

## 3. Fields and controls

| Field | Control | Rules |
| --- | --- | --- |
| Text | Multi-line text box, monospaced when the string contains escapes | Any text; newlines shown as a symbol with a toggle "Show whitespace" (default on) |
| Uses | Read-only count and link | "Show Uses" opens References with every user of the string |
| Length, bytes | Read-only | Characters and UTF-8 bytes |
| Index | Read-only, copy button | |

The list search matches Text with the toggles Match Case, Whole Word, Regular Expression (same component as the Search panel). Enter in the box focuses the first row.

## 4. Shared edit prompt (UTMT-AST-08)

Editing a string that has more than one use asks once on commit:

```
Change this string?
'Press Z to continue' is used in 4 places.
(o) Change all 4 uses
( ) Change only this use (creates a new string)
[Show Uses]  [Apply]  [Cancel]
```

"Change only this use" appears when the edit started from a reference field (the reference knows which use). From the strings list itself, only "Change all uses" exists, because there is no single use. The choice can be remembered per session ("Always change all") and reverses in Settings > Confirmations.

## 5. Actions and shortcuts

| Command | Id | Windows and Linux | macOS |
| --- | --- | --- | --- |
| New String | `strings.new` | Insert | Insert |
| Show Uses | `strings.showUses` | None | None |
| Copy String | `strings.copy` | Ctrl+C | Cmd+C |

Context menu on a row: Copy String, Copy Index, Show Uses, Find in Code, Delete (only when Uses is 0; otherwise disabled with the tooltip "This string is used in {n} places."), Rename Resource (only when Kind is Name; this edits the owning resource name).

Strings that are resource names are edited through the Name field of their editor so renames update references. Editing them here shows the same warning as renaming.

## 6. Validation and messages

| Case | Message |
| --- | --- |
| Name-kind string changed to an invalid identifier | "Resource names can use letters, digits and underscores and cannot start with a digit." |
| String is also another resource's name | "'{text}' is already used by a {type}." |
| Control character not allowed in the chunk | "This text contains a character that cannot be stored (U+0000)." |
| Very long text (over 1 MiB) | Warning: "This string is {size} long. Editing may be slow." |

## 7. Undo

One step per committed edit; "Change all uses" is a single step regardless of how many references exist. New String and Delete are single steps.

## 8. Empty and loading states

- Loading: the table shows rows as the string table materialises; the count in the title updates ("Strings (1,200...)") until finished.
- No strings (new file): "This data file has no strings. Strings are created when you add resources or code."
- No match: "No strings match '{query}'."

## 9. Parity coverage

- UTMT-AST-07: sections 2 and 3.
- UTMT-AST-08: section 4.
- UTMT-CTRL-02: section 4 and the String reference field in ux-standards section 11.
- UTMT-APP-02 (Strings node): list view in section 2.
