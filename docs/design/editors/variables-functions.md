# Variables, functions and code locals

Status: proposed. The editors for the VARI chunk (variables and its header), the FUNC chunk (functions) and the code locals tables. Code text is in the [code editor](code.md). Shared rules: [../ux-standards.md](../ux-standards.md). Shell: [../shell.md](../shell.md).

## 1. Purpose and entry points

Compiled code refers to variables and functions by table entries, not by name. These three tables let a modder see every name the game uses, where each is used, and fix a table when a hand-made change needs it. Most users never open them; the code editor's Locals tab and Go To definition cover daily work. They are the expert path, and they are safe: names are shared strings (see [strings.md](strings.md)), so renaming goes through the shared string rules.

Opens from Explorer > Data > Variables, Functions and Code Locals (virtualized lists), Go To `var:` and `fn:`, a Ctrl+click in the code editor on a name, and the code editor's Locals tab ("Open Table").

## 2. Layout

```
+------------------------------------------------------------------------------------------+
| Variables (2,418)    Header: instance 1,912  global 301  max locals 64      [search...] [Aa]|
+-----------------------------------------------+------------------------------------------+
| Index  Name           Instance type  ID   Uses |  Variable 214                            |
| 213    hp             Self           17   88   |   Name         [hp           ] (string)  |
| 214 >  global.lives   Global         3    12   |   Instance type [Global v]               |
| 215    image_speed    Builtin        -6   44   |   Variable ID   [3]                      |
|  ...                                          |   Name string ID 4120 [Open String]      |
|                                               |   Used by 12 places  [Show Uses]         |
+-----------------------------------------------+------------------------------------------+
```

The same two-pane layout serves the three tables: a virtualized table on the left (60%), a detail form on the right. The table header has type-ahead, sortable columns and a search box with Match Case; the Kind filter narrows by instance type.

## 3. Variables (UTMT-AST-09)

| Field | Control | Rules |
| --- | --- | --- |
| Index | Read-only | Position in the table |
| Name | String reference field | Edits the shared string; the usual shared-string prompt (ux-standards section 11) applies because a name may be used by a function or object name too |
| Instance type | Combo | The stored instance type: Self, Other, All, Noone, Global, Builtin, Local, Stacktop, Arg, Static, or a numeric object index for object-scoped variables |
| Variable ID | Number | The internal variable ID; "Next Free" picks the next ID of that instance type |
| Name string ID | Index field | Read-only; "Open String" opens the string |
| Uses | Count | Number of references in code; "Show Uses" fills the References panel (reference chains are followed) |

Add a variable (Insert) creates a table entry with a new string; new entries are not referenced by any code until a code edit uses them. Delete is allowed only for unused variables; a used one shows "'{name}' is used {n} times. Remove the uses first." with "Show Uses".

### 3.1 Variables header (UTMT-AST-10)

Above the table: Instance variable count, Global variable count and Max local variable count (header fields of files with bytecode 15 and higher, hidden otherwise). A "Recompute" button counts the entries and sets the fields; a mismatch shows the Problems information "The variable header counts differ from the table." because a wrong count can crash the game.

## 4. Functions (UTMT-AST-11)

| Field | Control | Rules |
| --- | --- | --- |
| Index | Read-only | |
| Name | String reference field | Shared string prompt; names of script functions in GM 2023.11 and later contain `@` forms that are shown as stored |
| Name string ID | Index field | Read-only with Open String |
| Kind | Read-only label | Built-in, Script function or Extension function (derived from the name and the scripts and extensions tables) |
| Uses | Count and Show Uses | |

Add and delete follow the same rules as variables (delete only when unused). A function whose name matches a script or extension function shows a link: "Defined by script 'scr_init'" or "Defined by extension 'ext_steam'" with Open.

## 5. Code locals (UTMT-AST-12)

Each code entry (in files with bytecode 15 and higher and in GMS 2.3 files) has a locals table. The code locals editor lists entries by name (the code entry name, for example `gml_Object_obj_player_Create_0`) with a count. The detail form shows:

| Field | Control |
| --- | --- |
| Name | String reference field (the code entry name) |
| Locals | List grid: Index, Name (string reference field); the last row "Add local..." (UTMT shows "double-click to add"); Alt+Up and Alt+Down move; Insert adds a local |

Local variable indexes are used by instructions, so reorder shows "Instructions that refer to local index {n} will point at a different name." and offers "Reorder and Keep Names" (rewrites the instruction indexes in the code entry's bytecode, only for entries that can be edited) or "Reorder Table Only". The default is the first. For locals tables of files with GMS 2.3 and later, the Functions chunk holds the same list; the editor shows one view of that data. Files from before GM 2024.8 store the tables in the FUNC chunk and later files split them; the editor hides the difference.

The code editor's Locals tab (code.md section 3.3) shows the table of the open entry and is the same data. The entry's Arguments and Locals counts are in the code properties strip.

## 6. Actions and shortcuts

| Command | Id | Windows and Linux | macOS |
| --- | --- | --- | --- |
| Add Local Variable | `symbols.addLocal` | Insert | Insert |
| Show Uses | `symbols.showUses` | None | None |
| Add Variable | `symbols.addVariable` | None | None |
| Add Function | `symbols.addFunction` | None | None |
| Recompute Header Counts | `symbols.recomputeHeader` | None | None |
| Next Free Variable ID | `symbols.nextVariableId` | None | None |
| Export Table... | `symbols.exportTable` | None | None |

Export Table writes CSV (index, name, kind, id, uses). Delete removes an unused entry with a confirm only when the entry is a local variable that code refers to by index.

## 7. Validation and messages

| Case | Message |
| --- | --- |
| Variable name empty or with spaces | "Variable names can use letters, digits and underscores and cannot start with a digit." |
| Duplicate name with the same instance type | "'{name}' is already a {instance type} variable." |
| Duplicate variable ID in an instance type | "ID {id} is already used by '{name}'." |
| Header counts differ | See 3.1 |
| Delete used entry | See 3 |
| Local name duplicate in one entry | "'{name}' is already a local of this entry." |

## 8. Undo

Undoable: field commits, add, delete, reorder and recompute. Grouping: each is one step named after the action; reorder with bytecode rewrite is one step that includes the rewrite.

## 9. States

Empty:
- No variables or functions (unusual): "This data file has no {table}." The groups hide in the Explorer.

Loading:
- Table with 100,000 rows: virtualized; usage counts fill in the background ("Counting uses...") and the column shows a dash until ready.
- YYC games: the tables exist but code is absent; Uses shows "Code is not in the file" for all rows.

Error:
- Table unreadable: "The {table} table could not be read: {error}."
- Usage count failed: "Uses could not be counted: {error}." The column keeps its dashes.

## 10. Parity coverage

| ID | Feature | Where |
| --- | --- | --- |
| UTMT-AST-09 | Variable editor (name, instance type, variable ID, name string ID) | 3 |
| UTMT-AST-10 | Variables chunk header | 3.1 |
| UTMT-AST-11 | Function editor | 4 |
| UTMT-AST-12 | Code locals editor | 5 |
| UTMT-CTRL-02 | String reference field | 3, 4, 5 |
| UTMT-CTRL-06 | List grid | 5 |
