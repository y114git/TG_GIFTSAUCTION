# Timelines

Status: proposed. The editor for TMLN resources: a list of moments (game steps), each with code. Shared rules: [../ux-standards.md](../ux-standards.md). Code is edited in the [code editor](code.md). Sequences have a similar idea in [sequences.md](sequences.md).

## 1. Purpose and entry points

Edit which code runs at which step of a timeline. Opens from Explorer > Timelines, Go To `tl:`, an object's code that calls the timeline, and References results.

## 2. Layout

```
+---------------------------------------------------------------------+
| tl_intro   Index 2   5 moments                                       |
+------------------------------+--------------------------------------+
| Name [tl_intro]              | (code editor for the selected moment)|
| Moments                      |  // step 60                          |
| Step   Code entry            |  show_message("Hello");              |
| 0      gml_Timeline_tl_intro_0|                                     |
| 60   > gml_Timeline_tl_intro_60                                     |
| 120    gml_Timeline_tl_intro_120                                    |
| [Add Moment] [Merge] [Remove]|                                      |
+------------------------------+--------------------------------------+
```

Left: name and moment list. Right: the code editor of the selected moment.

## 3. Fields (UTMT-AST-03)

| Field | Control | Rules |
| --- | --- | --- |
| Name | Name field | |
| Moments | List grid: Step (integer 0 and up), Code entry (Reference field, Code) | Steps must be unique: "Step {n} already has a moment." The list sorts by step on commit |
| Add Moment | Appends a moment at last step + 1 (Insert), prompts for the step | |
| Edit step | Slow double-click or F2 on the Step cell | Changing a step re-sorts the list |
| Duplicate | Ctrl+D appends copies of the selected moments after the last step | One step each |
| Merge | Combines the selected moments' code into the first one: code of later moments appended in order; asks "Merge {n} moments into step {s}? The other moments are removed." | Undoable |
| Remove | Delete removes selected moments; the code entries are kept and become unreferenced | |
| Cut, Copy, Paste | Moments between timelines | |
| Open code | Enter | Opens the entry in a full tab |

A moment without a code entry shows a warning icon and a "Create Code Entry" button. The step column header explains: "Steps count game frames after the timeline starts."

## 4. Actions and shortcuts

| Command | Id | Windows and Linux | macOS |
| --- | --- | --- | --- |
| Add Moment | `timelines.addMoment` | Insert | Insert |
| Delete Moment | `timelines.deleteMoment` | Delete | Delete |
| Merge Moments | `timelines.mergeMoments` | None | None |
| Open Moment Code | `timelines.openCode` | Enter | Enter |

## 5. Validation and messages

| Case | Message |
| --- | --- |
| Duplicate step | "Step {n} already has a moment." |
| Negative step | "A step cannot be negative." |
| Code entry used by another moment | "'{code}' is already used by step {s} of '{timeline}'." |
| Delete timeline in use | Reference scan dialog |

## 6. Undo

Undoable: moment add, edit, duplicate, merge, remove, paste and code apply. Grouping: each action is one step; code apply is one step that includes the code entry.

## 7. States

Empty:
- No moments: "This timeline has no moments. Choose Add Moment to create one."
- No timelines in the game: Explorer group shows 0.

Loading:
- None. Timelines are in memory once the file is open; a moment's code opens in the code editor with its own loading state.

Error:
- Timeline unreadable: "The timeline '{name}' could not be read: {error}."
- Moment code missing: "The code of step {n} could not be found." with Fix to create an empty entry.

## 8. Parity coverage

- UTMT-AST-03: sections 2 and 3.
