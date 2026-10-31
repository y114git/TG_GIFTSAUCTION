# Global init and game end

Status: proposed. The editors for the GLOB chunk (code entries that run at game start, called global init scripts) and the GMEN chunk (code entries that run at game end). The script resource editor is in [scripts.md](scripts.md), which links here. Code text is in the [code editor](code.md). Shared rules: [../ux-standards.md](../ux-standards.md). Shell: [../shell.md](../shell.md).

## 1. Purpose and entry points

Two ordered lists of code entries. Global init scripts run once, in list order, before the first room starts. Game end scripts run once when the game closes. GameMaker 2.3 and later fill the first list automatically with every script that has code at the top level; modders add entries by hand to run new startup code. GameMaker's IDE has no editor for either list, so this follows UTMT's two list editors and adds ordering tools, a run-order explanation and a used-by check.

Opens from Explorer > Game > Global Init and Game End, the "Runs at game start" checkbox of a script, Go To `init:` and `end:`, and Find Usages on a code entry (shows "Listed in Global Init").

## 2. Layout

```
+--------------------------------------------------------------------------------+
| Global Init   Entries 14   Runs in this order when the game starts              |
+--------------------------------------------------------------------------------+
| #   Code entry                           Script              Lines      [Open]  |
| 1   gml_GlobalScript_scr_init            scr_init            42         [...]   |
| 2   gml_GlobalScript_scr_macros          scr_macros          8          [...]   |
| 3 > gml_GlobalScript_scr_new_start       (none)              3          [...]   |
| [Add script...]                                                                  |
+--------------------------------------------------------------------------------+
| Preview of the selected entry (read-only code view, Open to edit)                |
+--------------------------------------------------------------------------------+
```

A list grid (ux-standards section 11) with a read-only code preview of the selected entry below it. One editor class serves both lists; the title and hint text differ.

## 3. Global init scripts (UTMT-AST-13)

1. Columns: order number, Code entry (reference field, restricted to code entries), Script (the script resource whose code entry it is, if any), Lines, Open. The reference field offers a picker that lists unlisted code entries first and highlights global script entries (`gml_GlobalScript_*`, or `gml_Script_*` in older files).
2. "Add Script..." appends an entry from the picker; a multi-select picker adds several at once in the order picked. The last row "Add..." does the same.
3. Remove deletes the list entry only; the code entry stays. Reordering: Alt+Up and Alt+Down, drag handle, and "Move To..." (position number). Reordering is one undo step.
4. The hint above the list reads "These scripts run in this order when the game starts, before the first room." Order matters when one script defines values another uses; "Check Order" scans each entry for global variables written by a later entry and reads by an earlier one, and lists the pairs as a Problems information item.
5. The `scripts.md` checkbox "Runs at game start" adds or removes the script's code entry here. New scripts in GMS 2.3 and later get a list entry when created (UTMT-TREE-04).
6. G3MLib reads GLOB and its legacy name GMEN with one class (format coverage, GLOB and GMEN). Both are shown with this editor, and the status line names the chunk found in the file.

## 4. Game end scripts (UTMT-AST-14)

The same editor under Explorer > Game > Game End, hint "These scripts run when the game closes." Same columns, same commands. The Check Order command is not offered (end scripts are rarely order-dependent).

## 5. Actions and shortcuts

| Command | Id | Windows and Linux | macOS |
| --- | --- | --- | --- |
| Add Global Init Script | `symbols.globalInitAdd` | None | None |
| Add Game End Script | `symbols.gameEndAdd` | None | None |
| Move Entry Up | `symbols.entryUp` | Alt+Up | Option+Up |
| Move Entry Down | `symbols.entryDown` | Alt+Down | Option+Down |
| Move Entry To... | `symbols.entryMoveTo` | None | None |
| Remove Entry | `symbols.entryRemove` | Delete | Delete |
| Open Code | `resource.open` | Enter | Enter |
| Check Order | `symbols.checkInitOrder` | None | None |

## 6. Validation and messages

| Case | Message |
| --- | --- |
| Entry does not exist | "The code entry no longer exists. Remove it from the list." with a Fix button |
| Duplicate entry | "'{code}' is already in the list." |
| Entry is not a script entry | Information "'{code}' is not a script. It runs at the top level like any other code." |
| Entry listed in both lists | Information "'{code}' runs at start and at the end." |
| Code entry deleted elsewhere while listed | Removed from the list in the same undo step, with a notification "Removed '{code}' from Global Init. Undo" |

## 7. Undo

Add, remove, reorder (one step per move) and Move To are single steps named after the action.

## 8. Empty and loading states

- Empty Global Init list: "No scripts run at game start. Add a script to run it before the first room."
- Empty Game End list: "No scripts run when the game closes."
- Data file with neither chunk (very old files): the Explorer rows are hidden.
- YYC games: lists show but the preview reads "Code is not in the file."

## 9. Parity coverage

| ID | Feature | Where |
| --- | --- | --- |
| UTMT-AST-13 | Global init scripts | 3 |
| UTMT-AST-14 | Game end scripts | 4 |
| UTMT-TREE-04 | Script defaults on add (global init entry) | 3 |
| UTMT-CTRL-01, UTMT-CTRL-06 | Reference field, list grid | 2, 3 |
