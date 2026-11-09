# Objects and events

Status: proposed. The editor for OBJT resources: properties, physics, the event list and the code of each event, parent and children. Code text is edited in the [code editor](code.md). Shared rules: [../ux-standards.md](../ux-standards.md).

## 1. Purpose and entry points

An object is the blueprint for room instances. This editor edits what the data file stores: sprite, mask, flags, depth, parent, physics and the table of events with their code. Variable definitions are not stored as typed rows in compiled files (they compile into pre-creation code), so the editor shows them as code, not as a table (editors.md 2.10).

Opens from Explorer > Resources > Objects, Go To `obj:`, a room instance's "Open Object", a code entry's owner link, and References results. Double-click opens the editor; the Inspector alone covers simple changes without opening it.

## 2. Layout

```
+-----------------------------------------------------------------------------------+
| obj_player        Index 12       Parent: obj_actor  [Open Parent]  [Children (3)]  |
+--------------------------+--------------------------------------------------------+
| Properties               | Events (12)                          [Add Event v]       |
| Sprite   [spr_player] x  | > Create                                                |
| Mask     [Same as sprite]|   Step                                                  |
| Visible  [x]  Solid [ ]  |   Alarm 0                                               |
| Persistent [ ]  Depth [0]|   Collision with obj_wall                               |
| Parent   [obj_actor]     |   Key Press: Space                                      |
| > Physics                |   Draw GUI                                              |
|                          +--------------------------------------------------------+
| (thumbnail 96x96)        | (code editor for the selected event: Create)            |
+--------------------------+--------------------------------------------------------+
```

Left column 300 px: properties, collapsible Physics group, sprite thumbnail. Right: event list (top, 40% height) and the code editor of the selected event (bottom). The splitter orientation switches to side by side when the tab is wider than 1100 px. Inherited events from parents appear in the list with a parent icon and the label "from {parent}", in `Text` colour (not dimmed), so contrast stays above 4.5:1.

## 3. Properties (UTMT-OBJ-01)

| Field | Control | Rules |
| --- | --- | --- |
| Name | Name field in the header | Resource name rules |
| Sprite | Reference field (Sprite), with "None" | Thumbnail, Open, Choose, Clear. "Edit Sprite" opens the sprite editor |
| Collision mask (texture mask) | Reference field (Sprite) with first option "Same as sprite" | |
| Visible | Checkbox | Invisible instances skip drawing but still collide |
| Solid | Checkbox | |
| Persistent | Checkbox | |
| Depth | Number, -16000 to 16000 (integer) | Lower draws on top |
| Parent | Reference field (Object) with "None" | Picking a parent that would create a cycle is refused: "'{parent}' already inherits from '{name}'." |

## 4. Physics (UTMT-OBJ-02, UTMT-OBJ-03)

Collapsed group "Physics", enabled by the checkbox "Uses physics". When off, its fields are disabled but keep their values.

| Field | Control | Range |
| --- | --- | --- |
| Is sensor | Checkbox | |
| Collision shape | Combo: Circle, Box, Custom | Custom shows the vertex list and canvas |
| Density | Number, float 0 and up | 0 makes the object static |
| Restitution | Number, float 0 to 1 | |
| Collision group | Number, integer | |
| Linear damping, angular damping | Number, float 0 and up | |
| Friction | Number, float 0 and up | |
| Start awake | Checkbox | |
| Kinematic | Checkbox | |
| Vertices (Custom shape) | List grid X and Y, 3 to 32 rows, with a small canvas where points can be dragged | The count stays in sync with the list; below 3: "A custom shape needs at least 3 points." Above 32: "A custom shape can have at most 32 points." Drag alternative: edit the numbers or use Add Point |

## 5. Events (UTMT-OBJ-04, UTMT-OBJ-05, UTMT-OBJ-06)

The event list shows one row per event: icon, type and subtype in words ("Collision with obj_wall", "Key Press: Space", "Alarm 3"), a code indicator (lines count), and a dirty dot if its code is unapplied. Sorted in GameMaker's order: Create, Destroy, Clean Up, Step (Begin, Normal, End), Alarm 0 to 11, Draw, Mouse, Key Down, Key Pressed, Key Up, Gesture, Collision, Other, Asynchronous, Pre-create, Trigger. The list can also be sorted alphabetically.

### 5.1 Add Event menu

`objects.addEvent` (Insert) opens a menu with these groups. A search box at the top filters the whole tree.

| Group | Subtypes |
| --- | --- |
| Create | (none) |
| Destroy | (none) |
| Clean Up | (none) |
| Step | Step, Begin Step, End Step |
| Alarm | Alarm 0 to Alarm 11 |
| Draw | Draw, Draw GUI, Draw Begin, Draw End, Draw GUI Begin, Draw GUI End, Pre-Draw, Post-Draw, Window Resize |
| Mouse | Left, Right, Middle: Down, Pressed, Released; No Mouse Input; Mouse Enter; Mouse Leave; Wheel Up; Wheel Down; Global submenu with the same buttons |
| Key Down, Key Pressed, Key Up | Arrows, Control, Alt, Shift, Space, Enter, Keypad, Digits, Letters, Function Keys, Others, No Key, Any Key (key names resolved from key codes) |
| Gesture | Tap, Double Tap, Drag Start, Dragging, Drag End, Flick, Pinch Start, Pinch In, Pinch Out, Pinch End, Rotate Start, Rotating, Rotate End, Global submenu |
| Collision | One entry per object in the game, searchable |
| Other | Outside Room, Intersect Boundary, View events, Game Start, Game End, Room Start, Room End, Animation End, Animation Update, Animation Event, Path Ended, User Event 0 to 15, Broadcast Message, Wallpaper events |
| Asynchronous | Audio Playback, Audio Playback Ended, Audio Recording, Cloud, Dialog, HTTP, In-App Purchase, Image Loaded, Networking, Push Notification, Save and Load, Social, Steam, System |
| Trigger, Pre-create | Only shown when the file's version has them |

Subtype pickers (UTMT-OBJ-06) resolve ids to names: key codes to key names, mouse buttons, other-event names and collision object names. An event that already exists is disabled in the menu with the tooltip "This object already has this event."

### 5.2 Event rows and code

Selecting a row loads its code into the code editor below. Each event owns one action with a code entry (UTMT-OBJ-05). Setting or changing the code links an entry; if none exists the first edit creates `gml_Object_{object}_{event}_{n}` and links it. The row's "Open Code" action (Enter) opens the entry in a full code tab. Renaming events: a `/// @description Init` comment is not stored in compiled files, so there are no event descriptions to show.

Event actions in the row's context menu: Open Code, Change Event Type... (F2, moves the code to another event slot; fails with "This object already has a '{event}' event." if taken), Duplicate Event (Ctrl+D, to another event slot via a picker), Cut, Copy, Paste (between objects too), Delete Event (Delete; the code entry is kept and shown as unreferenced, with an Undo link in the notification), Compare With Parent Event, Show Parent Event (read-only), Open Parent Event.

Multi-select with Ctrl and Shift for Delete and Copy.

### 5.3 Inheritance

Events of the parent chain appear greyed with the label "from {parent}". Right-click: Open (opens the parent's event), Inherit (inserts `event_inherited();` at the top of a new event's code), Override (creates an empty event). Parent chains may be deep; the editor stops at 32 levels and shows "Parent chain is longer than 32." The inheritance view is computed from the parent chain and is never written.

### 5.4 Children (UTMT-SRCH-06)

"Children (n)" lists objects whose parent is this object (References panel). Added in the UTMT 0.9.2.0 release; also on the Inspector.

## 6. Actions and shortcuts

| Command | Id | Windows and Linux | macOS |
| --- | --- | --- | --- |
| Add Event... | `objects.addEvent` | Insert | Insert |
| Delete Event | `objects.deleteEvent` | Delete | Delete |
| Change Event Type... | `objects.changeEvent` | F2 | F2 |
| Open Event Code | `objects.openEventCode` | Enter | Enter |
| Show Child Objects | `objects.showChildren` | None | None |
| Open Parent Object | `objects.openParent` | None | None |
| Open All Events in Tabs | `objects.openAllEvents` | None | None |
| Add Collision Shape Point | `objects.addVertex` | None | None |
| Compare Event With Parent | `objects.compareWithParent` | None | None |

Other shortcuts: F12 on a name in event code goes to its definition (code editor).

## 7. Validation and messages

| Case | Message |
| --- | --- |
| Parent cycle | "'{parent}' already inherits from '{name}'. Choose another parent." |
| Duplicate event | "This object already has a '{event}' event." |
| Sprite index missing | "The sprite no longer exists. Choose another sprite." with Fix |
| Custom shape under 3 or over 32 points | Section 4 |
| Delete object with instances in rooms | Reference scan dialog: "'{name}' has {n} instances in {m} rooms. Delete the object and remove its instances, or cancel." Buttons: "Delete Object and Instances", "Delete Object Only (Leaves Broken Instances)", "Cancel" |

## 8. Undo

Each property commit, event add, change, duplicate, delete, paste, and code apply is one step. Deleting an object with its instances is one step.

## 9. Empty and loading states

- No events: "This object has no events. Press Insert or choose Add Event to create one."
- No objects in the game: "This game has no objects."
- Loading a large object (hundreds of events): the list virtualizes; code loads when a row is selected.

## 10. Parity coverage

- UTMT-OBJ-01: section 3.
- UTMT-OBJ-02, UTMT-OBJ-03: section 4.
- UTMT-OBJ-04, UTMT-OBJ-05, UTMT-OBJ-06: section 5.
- UTMT-SRCH-06: section 5.4.
- UTMT-CTRL-01 (reference fields for sprite, mask, parent): section 3.
