# Particle systems

Status: proposed. The editor for PSYS (system) and PSEM (emitter) resources, with a live preview. Shared rules: [../ux-standards.md](../ux-standards.md).

## 1. Purpose and entry points

Edit the emitters of a particle system and their particle properties. Opens from Explorer > Particle Systems (emitters appear as children), Go To `ps:`, a room's particle system element, and References results.

## 2. Layout

```
+-------------------------------------------------------------------------------------+
| ps_fire   Index 0   Origin 0,0   3 emitters             [Copy GML] [Restart Preview]  |
+------------------------------------------------+------------------------------------+
| Preview (paused)  zoom, grid, canvas frame     | Emitters                           |
|       . * .                                    | (eye)(lock) em_flame   [x]         |
|      * . *   region outlines                   | (eye)(lock) em_spark               |
|                                                | [Add Emitter]                      |
|  [ > Play ]  Draw order [Old to new v]         | Inspector: em_flame                |
+------------------------------------------------+------------------------------------+
```

The Inspector shows the selected emitter. The preview starts paused (ux-standards section 5).

## 3. System fields (UTMT-AST-15)

| Field | Control | Rules |
| --- | --- | --- |
| Name | Name field | |
| Origin X, Y | Number pair | |
| Draw order | Combo: Old to new, New to old | |
| Emitters | List with visibility (preview only), lock (editor only), add, delete, drag reorder (Alt+Up and Alt+Down) | Emitters are PSEM resources linked to the system |

## 4. Emitter fields (UTMT-AST-16)

Grouped as in GameMaker.

| Group | Field | Control |
| --- | --- | --- |
| Emitter | Name | Name field |
| | Enabled | Checkbox |
| | Mode | Combo: Stream, Burst |
| | Particle count | Number (negative values mean a chance per step, with a hint) |
| | Relative | Checkbox |
| | Delay min, max, unit | Two numbers and Combo (Seconds, Frames) |
| | Interval min, max, unit | Same |
| | Distribution | Combo: Linear, Gaussian, Inverse Gaussian |
| | Shape | Combo: Rectangle, Ellipse, Diamond, Line |
| | Region | X, Y, width, height |
| Particle | Rotation | Number |
| | Sprite, frame, animate, stretch, random | Reference field (Sprite), number, three checkboxes |
| | Texture | Combo: sprite or a built-in shape (Pixel, Disk, Square, Line, Star, Circle, Ring, Sphere, Flare, Spark, Explosion, Cloud, Smoke, Snow) |
| | Colour: start, middle, end | Three colour fields, plus Additive blend checkbox |
| | Lifetime (life) | Min and max numbers |
| | Scale | X and Y numbers |
| | Size | Min, max, increase, wiggle |
| | Speed | Min, max, increase, wiggle |
| | Gravity | Amount and direction |
| | Direction | Min, max, increase, wiggle |
| | Orientation | Min, max, increase, wiggle, relative checkbox |
| | Spawn on death, spawn on update | Two Reference fields (emitter) with counts |
| Library | Library preset | Select Particles, link, save as preset |

Rules: min must not exceed max ("The minimum must not be above the maximum."). Colours accept alpha. Unknown newer fields are preserved untouched.

## 5. Preview and presets

The preview simulates emitters so the user sees changes live; it is a G3MStudio simulation, not the game's, and a note says "Preview is approximate." Play, pause, restart, zoom, grid, canvas frame size (default 1366 x 768) and an optional reference image. Region outlines show per emitter in its colour (not the only cue: the selected emitter has a handle square).

Presets: "Save as Preset..." stores the emitter in the sidecar project library; linked emitters share particle settings (emitter settings are saved only at creation); editing a linked preset updates linked emitters; "Unlink Preset" breaks the link. Built-in presets cannot be changed. Presets are an authoring aid and are not stored in compiled files; the link exists only in the sidecar.

"Copy GML to Clipboard" writes the equivalent `part_system` and `part_emitter` code for the system.

## 6. Actions and shortcuts

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
| Add Emitter | `particles.addEmitter` | Insert | Insert |
| Delete Emitter | `particles.deleteEmitter` | Delete | Delete |
| Copy GML to Clipboard | `particles.copyGml` | None | None |
| Save as Preset... | `particles.savePreset` | None | None |
| Unlink Preset | `particles.unlinkPreset` | None | None |
| Restart Preview | `particles.restart` | Ctrl+Shift+Space | Cmd+Shift+Space |

## 7. Validation and messages

| Case | Message |
| --- | --- |
| Min above max | "The minimum must not be above the maximum." |
| Interval below 0 | "Interval cannot be negative." |
| Spawn loop (emitter spawns itself) | Warning "This emitter spawns itself and may never stop." |
| Delete a system used by rooms | Reference scan dialog |

## 8. Undo

Undoable: field commits, and emitter add, delete, duplicate, reorder and preset apply. Grouping: each is one step; a field typed over several key presses is one step at commit.

## 9. States

Empty:
- No emitters: "This system has no emitters. Choose Add Emitter."
- No particle systems in the game: "This game has no particle systems."

Loading:
- Preview texture decoding: "Loading preview..." while the emitter list and fields stay editable.

Error:
- System unreadable: "The particle system '{name}' could not be read: {error}. The emitters that were read are shown."
- Preview failed: "The preview could not be drawn: {error}. The fields are still editable."

## 10. Parity coverage

- UTMT-AST-15: section 3.
- UTMT-AST-16: section 4.
- UTMT-ROOM-10 (particle system instances): room editor.
