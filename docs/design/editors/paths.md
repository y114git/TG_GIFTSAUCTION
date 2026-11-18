# Paths

Status: proposed. The editor for PATH resources. A path stores a smooth or straight line through points, each with a speed. Shared rules: [../ux-standards.md](../ux-standards.md).

## 1. Purpose and entry points

Edit the points and flags of a path, with the option to see a room behind it. Opens from Explorer > Paths, Go To `path:`, a path layer in a room ("Open Path"), and References results.

## 2. Layout

```
+--------------------------------------------------------------------------------------+
| path_enemy_patrol   Index 4   12 points   length 1,840 px                             |
+--------------------------+-----------------------------------------------------------+
| Name [path_enemy_patrol] |  rulers + grid + canvas (room backdrop optional)           |
| Connection [Smooth v]    |     o----o                                                 |
| Closed     [x]           |    /      \                                                |
| Precision  [4]  (1 to 8) |   o        o--o                                            |
| Show room  [room_cave v] |                                                           |
|                          |   [-] 100% [+] [fit] [grid]    x 120, y 340                |
| Points                   +-----------------------------------------------------------+
|  #  X     Y     Speed %  | hint: Click: add point. Drag: move. T/R/S + drag: translate,|
|  1  100   200   100      |       rotate, scale.                                       |
|  2  ...                  |                                                           |
|  [Add Point] [Move All]  |                                                           |
+--------------------------+-----------------------------------------------------------+
```

Left column 300 px; the point table sits below the properties. Canvas shows the path as a polyline or spline, points as 8 px handles with 24 px hit areas, the start point marked with an arrow, and the closing segment dashed when Closed is on (preview does update live; UTMT noted it did not).

## 3. Properties (UTMT-AST-02)

| Field | Control | Rules |
| --- | --- | --- |
| Name | Name field | |
| Connection kind | Combo: Straight Lines, Smooth Curve | |
| Closed | Checkbox | |
| Precision | Number, integer 1 to 8 (default 4) | Smooth curve segments per point |
| Points | List grid: X (float), Y (float), Speed (float, percent of path speed; 100 is normal) | At least 1 point; "A path needs at least one point." when deleting the last |
| Move all | Button opens a dialog with X and Y offsets | One undo step |
| Show room | Combo of rooms or None | Draws the room as a dimmed backdrop; not saved to the data file |

## 4. Canvas interaction

1. Click on empty space adds a point at the end. Clicking near a segment (detection distance 8 px, setting `path.hitDistance`) inserts a point there.
2. Drag a point to move it. Ctrl+click toggles selection; marquee drag selects several; dragging one moves all selected. Delete removes selected points. Arrow keys nudge by 1 (Shift: 8), which is the non-drag alternative; the point table edits numbers.
3. Tool keys (modes, Escape returns to Select): T translate the selection, R rotate around its centre, S scale around its centre. The status hint explains the active mode.
4. Context menu on the canvas: Delete Points, Pan to Path, Reverse, Flip (horizontal), Mirror (vertical), Close Path, Open Path, Insert Point.
5. Selecting a row in the point table pans to the point. Enter on a row pans to it. Slow double-click edits a cell.
6. Snap uses the editor grid (Shift+G); the grid size is a toolbar option.

## 5. Actions and shortcuts

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
| Translate Tool | `paths.tool.translate` | T | T |
| Rotate Tool | `paths.tool.rotate` | R | R |
| Scale Tool | `paths.tool.scale` | S | S |
| Delete Points | `paths.deletePoints` | Delete | Delete |
| Reverse Path | `paths.reverse` | None | None |
| Flip Path | `paths.flip` | None | None |
| Mirror Path | `paths.mirror` | None | None |
| Show Room Behind Path | `paths.showRoom` | None | None |
| Pan to Point | `paths.panToPoint` | Enter | Enter |

## 6. Validation and messages

| Case | Message |
| --- | --- |
| Precision out of range | "Precision must be between 1 and 8." |
| Speed negative | "Speed cannot be negative." |
| Last point deleted | "A path needs at least one point." |
| Delete a path used by rooms or objects | Reference scan dialog |
| Closed path with one point | Info "A closed path with one point is a point." |

## 7. Undo

Undoable: point drags, nudges, table edits, reverse, flip, mirror, tool transforms and move. Grouping: a drag is one step regardless of length; every other action is one step.

## 8. States

Empty:
- No points: "This path has no points. Click the canvas to add one."

Loading:
- Room backdrop loading: "Rendering room..." in the status line; the path stays editable.

Error:
- Path unreadable: "The path '{name}' could not be read: {error}."
- Backdrop failed: "The room backdrop could not be drawn. The path is still editable."

## 9. Parity coverage

- UTMT-AST-02: sections 2 to 4.
- UTMT-ROOM-25 (path layers render paths): the room editor opens this editor for path layers.
