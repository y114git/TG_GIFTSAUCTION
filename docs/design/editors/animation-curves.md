# Animation curves

Status: proposed. The editor for ACRV resources: channels, points, interpolation and presets. UTMT had no editor for these (project system only). Shared rules: [../ux-standards.md](../ux-standards.md). Sequences use these curves, see [sequences.md](sequences.md).

## 1. Purpose and entry points

Edit the shape of a curve that code or sequences read. Opens from Explorer > Animation Curves, Go To `curve:`, a sequence parameter key ("Open Editor"), and References results.

## 2. Layout

```
+---------------------------------------------------------------------------------+
| curve_ease   Index 2   Linear   2 channels                                       |
+--------------------------+------------------------------------------------------+
| Name [curve_ease]        |  graph, value range -1 to 1, time 0 to 1               |
| Type [Linear v]          |     |        .--'                                      |
| Channels                 |   --+---..--'---------                                  |
| (eye) ch_x  [rename] [x] |     |                                                  |
| (eye) ch_y               |  [-] [+] [Centre and Fit]  x 0.42  y 0.31               |
| [Add Channel]            +------------------------------------------------------+
| Points of ch_x           | Presets: [Library...]  Range: [-1] to [1]  [Set Range]  |
|  #  X      Value  Handles|                                                        |
|  1  0.00   0.00          |                                                        |
+--------------------------+------------------------------------------------------+
```

Left: name, curve type, channel list, point table for the selected channel. Right: graph with zoom controls, presets and range. All channels draw; only the selected channel is editable (others dimmed with outlines at 50% so shape stays visible).

## 3. Fields

| Field | Control | Rules |
| --- | --- | --- |
| Name | Name field | |
| Curve type | Combo: Linear, Smooth (Catmull-Rom), Bezier | Applies to the selected channel; some files store one type per curve |
| Iterations | Number, integer 1 to 64 | Segments per interval for smooth curves (shown when stored) |
| Channels | List: visibility eye, name (rename with F2), delete | A curve needs at least one channel |
| Points | List grid for the selected channel: X (time), Value, and for Bezier the two handles (X, Y each) | X between 0 and 1 and in ascending order; "Points must be in time order." |
| Visual range | Two numbers, default -1 and 1 | Changes only the view; points are not clamped |

## 4. Graph interaction

1. Click near the line adds a point; drag moves it; marquee or Ctrl+click selects several and dragging moves them together. Delete removes the selected points. Arrow keys nudge a selected point by 0.01 (Shift: 0.1), which is the non-drag alternative. The point table edits the same numbers.
2. Right-click in empty space opens presets for the whole channel (Bezier channels only); right-click a segment or a selection of segments applies a preset between points.
3. Presets library (button): ease in, ease out, ease in-out, bounce, elastic, step, sine; "Overwrite" or "Apply between points" modes.
4. Zoom with Ctrl+wheel, Ctrl+=, Ctrl+-; Centre and Fit (Ctrl+0); pan with Space+drag. Duplicate curve: Ctrl+D (duplicates the selected channel).
5. Hover shows the exact value in the status hint.

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
| Add Channel | `curves.addCurve` | Insert | Insert |
| Delete Channel | `curves.deleteCurve` | Delete | Delete |
| Set Visual Range... | `curves.setRange` | Ctrl+R | Cmd+R |
| Interpolation: Linear, Smooth or Bezier | `curves.interpolation` | None | None |
| Apply Preset... | `curves.applyPreset` | None | None |

## 6. Validation and messages

| Case | Message |
| --- | --- |
| Point out of order | "Points must be in time order." |
| Channel name duplicate | "'{name}' is already a channel name in this curve." |
| Last channel deleted | "A curve needs at least one channel." |
| Delete curve used by sequences or code | Reference scan dialog |
| Range with min above max | "The minimum must be below the maximum." |

## 7. Undo

Undoable: point edits, presets, and channel add, delete, rename and type change. Grouping: a point drag is one step; every other action is one step named after it.

## 8. States

Empty:
- No curves in the game: "This game has no animation curves."
- Channel with no points: "This channel has no points. Click the graph to add one."

Loading:
- None. Curves are in memory once the file is open and the graph draws at once.

Error:
- Curve data unreadable: "The animation curve '{name}' could not be read: {error}. The channels that were read are shown."

## 9. Parity coverage

- UTMT-NOED-02: sections 2 to 4 (channels and points).
- UTMT-PROJ-07 (animation curves in project folders): one JSON file per curve.
