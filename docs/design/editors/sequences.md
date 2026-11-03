# Sequences

Status: proposed. The editor for SEQN resources: playback settings, tracks, keyframes, moments, broadcast messages and the canvas preview. UTMT had no editor for sequences (only project and reference support), so this is an improvement over parity. Shared rules: [../ux-standards.md](../ux-standards.md). Shared curves are in [animation-curves.md](animation-curves.md).

## 1. Purpose and entry points

Show and edit keyframe animation: which assets move, rotate, scale or fade over time, and which messages and functions fire at which frame. Opens from Explorer > Sequences, Go To `seq:`, a room's sequence element, a sprite's Spine/sequence link, and References results. Sprites in GMS2 are sequences internally; this editor opens only SEQN resources.

## 2. Layout

```
+-----------------------------------------------------------------------------------------+
| seq_intro   Index 0   Speed 30 fps   Length 120 frames   [Loop v]   [|<][<][ > ][>][>|]   |
+-----------------------------------------------+-----------------------------------------+
| Canvas  (origin cross, canvas frame 1366x768) | Inspector: sequence or selected track    |
|                                               |  Time units [Frames v]  Speed [30]      |
|     [sprite box at playhead]                  |  Length [120]  Canvas frame [1366][768]  |
+-----------------------------------------------+-----------------------------------------+
| Tracks                  | 0    10    20    30    40 ... playhead v    [Add Track v]     |
| v spr_title (sprite)    | [=========asset key=========]                                  |
|    Position             |  o        o               o      (keyframes)                   |
|    Scale                |  o                        o                                    |
| v snd_swoosh (sound)    |          [=====key====]                                        |
|    Volume               |          o      o                                              |
| Moments / Messages      |  M at 30 : seq_hit        B at 60 : "start_game"                |
+-------------------------+----------------------------------------------------------------+
```

Regions: canvas (top left), Inspector area (top right, uses the right dock Inspector in the Studio preset), track panel (bottom left), dope sheet (bottom right), moments and broadcast rows. The orientation button cycles Split Window (dope sheet in a floating window), Snap Top, Right, Bottom (default) and Left, like GameMaker. Reset restores the default.

## 3. Sequence properties (Inspector, nothing selected)

| Field | Control | Rules |
| --- | --- | --- |
| Name | Name field | |
| Time units | Combo: Frames, Seconds | Display only for the dope sheet |
| Playback speed | Number > 0 and unit Combo (Frames per second, Frames per game frame) | |
| Length | Number, float > 0 | In frames |
| Loop mode | Combo: Once, Loop, Ping-pong | |
| Clip region start, end | Two numbers | Optional playback window |
| Canvas frame | Size editor | The reference rectangle on the canvas; default 1366 x 768 |
| Events | Six rows: Create, Destroy, Clean Up, Begin Step, Step, End Step, Async | Each binds a function name (text) with "Create New..." |
| Origin | Number pair | Sequence origin |

## 4. Tracks and keyframes

Track panel: tree of tracks with Add Track (sprite, object, sound, sequence, particle system, text, group, or parameter track), drag to reorder, group folders, Cut, Copy, Paste, Duplicate, Delete, Group, Rename, mute and lock icons. Dropping an asset from the Explorer on the panel or canvas creates a track. Keyboard alternative: Add Track... picks an asset.

Parameter tracks (what can animate):

| Asset | Parameters |
| --- | --- |
| All | Position, Rotation |
| Sprite, object | Origin, Scale, Image Index, Image Speed, Colour Multiply |
| Sequence | Origin, Scale |
| Particle system | Colour Multiply, Origin, Scale |
| Sound | Volume, Pitch, Falloff, Bus, audio effects |
| Text | Origin, Scale, Colour Multiply, character spacing, frame size, line spacing, paragraph spacing, SDF effects |

Dope sheet: asset keys are bars (drag the right edge to change length), parameter keys are diamonds. Click selects, Ctrl+click toggles, marquee selects, drag moves in time (snap to whole frames, Alt disables snapping); alternatives without dragging: select keys and type a frame in the Inspector "Frame" field, or Ctrl+Left and Ctrl+Right move the playhead to the previous or next key. Asset key menu: Cut, Copy, Paste, Add Key In Place, Split Key In Place, Paste In Place, Delete, Copy to Start, Copy to End, Stretch. Parameter key menu adds Convert to Embedded Curve, Set to External Curve, Convert to Keys, Export Embedded Curve, Open Editor, Stretch.

Keyframe controls: Add Keyframe at Playhead (Insert), Split, Delete (Delete), Record (button; records the changed canvas value as a key) and Automatically Record Changes (A).

Curves: each parameter channel is an embedded curve or a shared Animation Curve resource. Channels per parameter: Position 2, Rotation 1, Colour Multiply 4 (0 to 255), Image Index 1, Image Speed 1, Origin 2, Scale 2, Pitch 1, Volume 1 (0 to 1). The curve view (button) shows the channels as a graph with the same tools as the animation curve editor. Curve menu: Set Range, Convert to Curve, Embed Curve, Assign Shared Curve, Convert to Keys, Export to Shared Curve.

## 5. Moments and broadcast messages

A row under the dope sheet holds moments (a function name called at that frame, with a go-to-script and a create-script button) and broadcast messages (a string at a frame). One message per frame; overlapping icons open a list on right-click; a message and a moment may share a frame. Add Moment at Playhead (M), Add Broadcast Message at Playhead (B). Edit or delete from the context menu. Messages also exist on sprites.

## 6. Canvas and preview

The canvas shows tracks at the playhead: sprites and objects as images, text as boxes, particle systems as bounds, sounds as speaker icons. Gizmos change between position, rotation, scale and origin modes; handles have a 24 px hit area; numeric fields in the Inspector are the non-drag alternative. Grid, zoom (Ctrl+wheel, Ctrl+=, Ctrl+-, Ctrl+0), pan (Space+drag) as in other canvases. Playback (Shift+Space) starts paused and loops according to the loop mode. Sound tracks play only when "Preview audio" is on (default off).

## 7. Actions and shortcuts

| Command | Id | Windows and Linux | macOS |
| --- | --- | --- | --- |
| Add Keyframe at Playhead | `sequences.addKey` | Insert | Insert |
| Delete Keyframes | `sequences.deleteKeys` | Delete | Delete |
| Next Keyframe | `sequences.nextKey` | Ctrl+Right | Cmd+Right |
| Previous Keyframe | `sequences.previousKey` | Ctrl+Left | Cmd+Left |
| Next Frame | `sequences.nextFrame` | Right | Right |
| Previous Frame | `sequences.previousFrame` | Left | Left |
| Record Changes Automatically | `sequences.autoKey` | A | A |
| Add Moment at Playhead | `sequences.addMoment` | M | M |
| Add Broadcast Message at Playhead | `sequences.addBroadcast` | B | B |
| Add Track... | `sequences.addTrack` | None | None |
| Group Tracks | `sequences.groupTracks` | None | None |
| Split Keyframe | `sequences.splitKey` | None | None |
| Show Curve View | `sequences.curveView` | None | None |
| Convert to Embedded Curve | `sequences.embedCurve` | None | None |
| Assign Shared Curve... | `sequences.assignCurve` | None | None |

Canvas shortcuts for pan, zoom and grid follow the canvas set in the keyboard map.

## 8. Validation and messages

| Case | Message |
| --- | --- |
| Keyframe beyond sequence length | "Keyframe at {f} is after the end of the sequence ({len})." Warning with "Extend Length" fix |
| Two keys at the same frame on one channel | "A channel can have only one key per frame." |
| Missing asset on a track | "The {type} no longer exists." with Fix to relink |
| Shared curve missing | "The animation curve no longer exists." |
| Moment function not found | Warning "No script defines '{name}'." |

## 9. Undo

Undoable: key move, add, delete and split, curve edits, track add, reorder and group, and property commits. Grouping: a drag is one step; record mode adds one step per recorded change batch; every other action is one step.

## 10. States

Empty:
- No tracks: "Drag a sprite, object, sound, sequence or text here to create a track, or choose Add Track."
- Empty dope sheet row: no text, a faint grid.

Loading:
- The track tree appears first; asset previews load on demand.

Error:
- Sequence unreadable: "The sequence '{name}' could not be read: {error}. The tracks that were read are shown."
- Preview failed: "The preview of '{asset}' could not be loaded." The track stays editable.

## 11. Parity coverage

- UTMT-NOED-01: sections 3 to 6 (tracks, keyframes, moments, broadcast messages).
- UTMT-SRCH-05 (sequence references): tracks and keyframes appear in References.
- UTMT-PROJ-07 (sequences in project folders): serialization is one JSON file per sequence.
