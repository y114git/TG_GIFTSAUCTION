# Scripts, global init and game end

Status: proposed. The editor for SCPT resources (GML scripts), with a link to the lists of code entries that run at game start and game end. Code text itself is edited in the [code editor](code.md). Shared rules: [../ux-standards.md](../ux-standards.md). C# scripts that automate G3MStudio are a different thing and live in [../scripting.md](../scripting.md). The global init and game end lists are in [global-scripts.md](global-scripts.md).

## 1. Purpose and entry points

A script resource links a name to a code entry. In GameMaker 2.3 and later, one script entry holds many functions, and the data file also has a "global init" list that runs scripts at start. This editor makes the link visible and editable and opens the code.

Opens from Explorer > Resources > Scripts, Go To `scr:`, and the Object editor event list. Double-clicking a script opens it as a split editor: the properties strip above and the code editor below.

## 2. Layout

```
+---------------------------------------------------------------------------+
| scr_init                                          Index 31   [Open Code]   |
| Name        [ scr_init                        ]                            |
| Code entry  [ gml_GlobalScript_scr_init   ] [Choose...] [Clear] [Open]     |
| Constructor [ ] Is a constructor                                           |
| Global init [ ] Runs at game start      [Move in order...]                 |
+---------------------------------------------------------------------------+
| (code editor for gml_GlobalScript_scr_init)                                |
+---------------------------------------------------------------------------+
```

## 3. Fields (UTMT-CODE-14)

| Field | Control | Rules |
| --- | --- | --- |
| Name | Name field | Resource name rules; renaming updates the linked code entry name when it follows the default pattern (`gml_GlobalScript_{name}` in 2.3 and later, `gml_Script_{name}` before) and asks "Rename the code entry to match?" when it does not |
| Code entry | Reference field (type Code) | A script without code is allowed but shown with a warning icon; "Create Code Entry" button appears when empty |
| Is a constructor | Checkbox | Shown for 2.3 and later |
| Runs at game start | Checkbox | Adds or removes the script's code entry from the global init list (3.1) |
| Functions in this script | Read-only list | The GMS 2.3 functions defined in the entry, each with an Open action that jumps to the declaration |

Creating a script (Explorer > Scripts > New, type defaults from UTMT-TREE-04): the dialog asks for a name, then creates the resource and its code entry (`gml_GlobalScript_{name}` plus a global init entry in 2.3 and later, otherwise `gml_Script_{name}`) with a code locals entry, and opens the code editor with the template `function {name}() {\n}` for 2.3 files or an empty body for older files.

The Runs at game start checkbox adds or removes the code entry in the global init list; the list, its order and the game end list are edited in [global-scripts.md](global-scripts.md).

## 4. Actions and shortcuts

Script-specific actions (no default shortcuts): Open Code (`resource.open` on the code entry) and Create Code Entry. Constructors cannot be converted after creation, because calls in other code depend on the form. Delete shows the reference scan dialog (ux-standards section 7) because calls in other code refer to the function by name.

## 5. Validation and messages

| Case | Message |
| --- | --- |
| Code entry already used by another script | "'{code}' is already the code of '{script}'." |
| Script without code at save | Warning in Problems: "'{script}' has no code." |

## 6. Undo

Undoable: field commits, add, remove and reorder. Grouping: each is one step; a name change together with its code entry rename is one step.

## 7. States

Empty:
- No scripts: "This game has no scripts. Scripts in GameMaker 2.3 and later are functions inside code entries; use Resource > New Resource > Script to add one."

Loading:
- None. The script table is in memory once the file is open.

Error:
- Table unreadable: "The script table could not be read: {error}."
- Code entry missing: "The code entry '{code}' of '{script}' no longer exists." with Fix to choose another.

## 8. Parity coverage

- UTMT-CODE-14: sections 2 and 3.
- UTMT-TREE-04 (script defaults on add): section 3.
- UTMT-APP-02 (Scripts node): section 1.
