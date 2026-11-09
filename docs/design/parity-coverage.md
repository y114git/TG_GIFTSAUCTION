# Parity coverage

Status: proposed. Maps every UTMT item in [../research/utmt-parity.md](../research/utmt-parity.md) (272 IDs, UTMT 0.9.2.0) to the design spec and section that covers it. An ID is covered when a spec cites it in a parity table, a parity list or the body text. This table was generated from those citations; update it when a spec changes. Requirement: F-PAR-1. Status column: "covered" means a G3MStudio spec section covers it; "covered by G3MTool" means the feature is a command line feature and is left to the G3MTool CLI and GUI (questions.md Q24: G3MStudio has no command line).

Covered: 272 of 272. Not covered: 0.

## Covered IDs

### Main window shell (APP)

| ID | Feature | Spec and section | Status |
| --- | --- | --- | --- |
| UTMT-APP-01 | Menu bar | [shell.md](shell.md) sections 1.2, 2 | covered |
| UTMT-APP-02 | Resource tree | [scripts.md](editors/scripts.md) section 1; [strings.md](editors/strings.md) list view in section 2 | covered |
| UTMT-APP-03 | Tree name filter | [shell.md](shell.md) section 5.4 | covered |
| UTMT-APP-04 | Back and forward buttons | [shell.md](shell.md) section 9.2 | covered |
| UTMT-APP-05 | C# command box | [shell.md](shell.md) section 7.7 | covered |
| UTMT-APP-06 | Asset ID label | [shell.md](shell.md) section 4 | covered |
| UTMT-APP-07 | Welcome and description view | [onboarding.md](onboarding.md)  | covered |
| UTMT-APP-08 | Modal loader dialog | [shell.md](shell.md) sections 14.1, 14.2 (and ux-standards 9: loading is not modal, save and launch show a blocking progress dialog) | covered |
| UTMT-APP-09 | Dark mode | [shell.md](shell.md) section 1.2 item 7 | covered |
| UTMT-APP-10 | Window placement memory | [shell.md](shell.md) section 1 | covered |
| UTMT-APP-11 | Help menu | [shell.md](shell.md) sections 2 (Help menu and its notes) | covered |
| UTMT-APP-12 | Clickable results window | [code.md](editors/code.md) section 9 | covered |
| UTMT-APP-13 | Text input dialogs | [ux-standards.md](ux-standards.md) section 11 | covered |
| UTMT-APP-14 | Crash logging | [shell.md](shell.md) section 14.5 | covered |
| UTMT-APP-15 | Message boxes | [ux-standards.md](ux-standards.md) section 11 | covered |

### File operations (FILE)

| ID | Feature | Spec and section | Status |
| --- | --- | --- | --- |
| UTMT-FILE-01 | New data file | [shell.md](shell.md) section 14.1 item 6 | covered |
| UTMT-FILE-02 | Open data file | [shell.md](shell.md) section 14.1 | covered |
| UTMT-FILE-03 | Save data file | [shell.md](shell.md) section 14.2 | covered |
| UTMT-FILE-04 | Load warnings | [shell.md](shell.md) section 14.1 | covered |
| UTMT-FILE-05 | Unsupported bytecode handling | [code.md](editors/code.md) section 1; [shell.md](shell.md) section 14.1 | covered |
| UTMT-FILE-06 | YYC detection | [code.md](editors/code.md) section 1; [shell.md](shell.md) section 7.7; section 14.1 | covered |
| UTMT-FILE-07 | Debugger flag warnings | [general-info.md](editors/general-info.md) section 5 (debugger flag warning); [shell.md](shell.md) section 14.1 | covered |
| UTMT-FILE-08 | Unknown texture format warning | [sprites.md](editors/sprites.md) section 9; [texture-pages.md](editors/texture-pages.md) 4.1, 5; [shell.md](shell.md) section 14.1 | covered |
| UTMT-FILE-09 | Close and unsaved-changes prompt | [shell.md](shell.md) section 14.5 | covered |
| UTMT-FILE-10 | Generate offset map | [shell.md](shell.md) section 2 (Tools menu); behaviour in platforms-formats.md section 9.2; [platforms-formats.md](platforms-formats.md) section 9.2 | covered |
| UTMT-FILE-11 | Audio group child instances | [sounds.md](editors/sounds.md) 6.3 (replaced by linked files in one window) | covered |
| UTMT-FILE-12 | Command-line arguments | [shell.md](shell.md) section 13 item 4 and section 13.1 (open-file and launch arguments of the GUI program) | covered |
| UTMT-FILE-13 | Drag and drop onto window | [shell.md](shell.md) section 5.5; Start page | covered |
| UTMT-FILE-14 | Path confinement | [shell.md](shell.md) section 14.1 | covered |

### Resource tree actions (TREE)

| ID | Feature | Spec and section | Status |
| --- | --- | --- | --- |
| UTMT-TREE-01 | Open asset | [shell.md](shell.md) section 5.5 | covered |
| UTMT-TREE-02 | Open in new tab | [shell.md](shell.md) section 5.5 item 1 | covered |
| UTMT-TREE-03 | Add asset | [shell.md](shell.md) section 5.6 (New resource) | covered |
| UTMT-TREE-04 | Type-specific defaults on add | [extensions.md](editors/extensions.md) 3; [general-info.md](editors/general-info.md) Explorer defaults on add are in the shell; the general-info side effects (room order entry on adding a room) are in section 3.5; [global-scripts.md](editors/global-scripts.md) 3; [rooms.md](editors/rooms.md) 3.1 (new rooms get the flags and creation order list of the file's version; the room order row is added in General Info) | covered |
| UTMT-TREE-05 | Item context menu | [shell.md](shell.md) section 5.6 | covered |
| UTMT-TREE-06 | Delete asset | [shell.md](shell.md) section 5.6 | covered |
| UTMT-TREE-07 | Copy name | [shell.md](shell.md) section 5.6 | covered |
| UTMT-TREE-08 | Reorder by drag | [shell.md](shell.md) section 5.3 item 3 | covered |
| UTMT-TREE-09 | Drag asset as reference | [rooms.md](editors/rooms.md) 5.7 | covered |
| UTMT-TREE-10 | Version-aware labels | [backgrounds-tilesets.md](editors/backgrounds-tilesets.md) 1; [shell.md](shell.md) section 5.2 | covered |
| UTMT-TREE-11 | Null entries | [shell.md](shell.md) section 5.2 | covered |

### Tabs (TAB)

| ID | Feature | Spec and section | Status |
| --- | --- | --- | --- |
| UTMT-TAB-01 | Tab strip | [shell.md](shell.md) section 9.1 | covered |
| UTMT-TAB-02 | Close tab | [shell.md](shell.md) section 9.2 | covered |
| UTMT-TAB-03 | Close other and all tabs | [shell.md](shell.md) section 9.2 | covered |
| UTMT-TAB-04 | Restore closed tab | [shell.md](shell.md) section 9.2 | covered |
| UTMT-TAB-05 | Switch tabs | [shell.md](shell.md) section 9.2 | covered |
| UTMT-TAB-06 | Reorder tabs by drag | [shell.md](shell.md) section 9.2 | covered |
| UTMT-TAB-07 | Tab overflow scrolling | [shell.md](shell.md) section 9.2 | covered |
| UTMT-TAB-08 | Per-tab state restore | [backgrounds-tilesets.md](editors/backgrounds-tilesets.md) 4.1; [code.md](editors/code.md) section 4; [fonts.md](editors/fonts.md) 4; [paths.md](editors/paths.md) 2 | covered |

### Running the game (RUN)

| ID | Feature | Spec and section | Status |
| --- | --- | --- | --- |
| UTMT-RUN-01 | Temp run | [run-game.md](run-game.md) section 3 | covered |
| UTMT-RUN-02 | Run with other runner | [run-game.md](run-game.md) section 4 | covered |
| UTMT-RUN-03 | Runtime picker | [run-game.md](run-game.md) section 4 | covered |
| UTMT-RUN-04 | Run under GMS debugger | [run-game.md](run-game.md) section 6 | covered |
| UTMT-RUN-05 | Debug data generation | [run-game.md](run-game.md) section 6 | covered |
| UTMT-RUN-06 | Shell verbs | [onboarding.md](onboarding.md) section 7; [shell.md](shell.md) section 13.1 (launch forms) | covered |

### Settings (SET)

| ID | Feature | Spec and section | Status |
| --- | --- | --- | --- |
| UTMT-SET-01 | Settings storage | [settings.md](settings.md) section 3 | covered |
| UTMT-SET-02 | GameMaker paths | [settings.md](settings.md) sections 4.1, 5.6 | covered |
| UTMT-SET-03 | Asset order swapping | [settings.md](settings.md) sections 4.1, 5.4 | covered |
| UTMT-SET-04 | File association toggle | [onboarding.md](onboarding.md)  | covered |
| UTMT-SET-05 | Debugger menu toggle | [settings.md](settings.md) sections 4.1, 5.5; [run-game.md](run-game.md) section 6 | covered |
| UTMT-SET-06 | Remember window placement | [settings.md](settings.md) sections 4.1, 5.4 | covered |
| UTMT-SET-07 | Warn on close | [settings.md](settings.md) sections 4.1, 5.3 | covered |
| UTMT-SET-08 | Warn about temp running | [settings.md](settings.md) sections 4.1, 5.5, 5.9; [run-game.md](run-game.md) section 3 | covered |
| UTMT-SET-09 | Show null entries in tree | [shell.md](shell.md) section 5.2 | covered |
| UTMT-SET-10 | Recompile all GML on project save | [settings.md](settings.md) sections 4.1, 5.5; [projects.md](projects.md) section 6 | covered |
| UTMT-SET-11 | Dark mode toggle | [settings.md](settings.md) sections 4.1, 5.1 | covered |
| UTMT-SET-12 | Transparency grid colors | [image-editor.md](editors/image-editor.md) 8; [sprites.md](editors/sprites.md) section 4.2 | covered |
| UTMT-SET-13 | Global room grid override | [rooms.md](editors/rooms.md) 5.7 | covered |
| UTMT-SET-14 | Open app data folder | [settings.md](settings.md) sections 4.1, 5.11, 6 | covered |
| UTMT-SET-15 | Update app button | [settings.md](settings.md) sections 4.1, 5.8 | covered |
| UTMT-SET-16 | GML settings button | [code.md](editors/code.md) section 8 | covered |

### GML compiler and decompiler settings (GMLS)

| ID | Feature | Spec and section | Status |
| --- | --- | --- | --- |
| UTMT-GMLS-01 | Indentation style | [code.md](editors/code.md) section 8 | covered |
| UTMT-GMLS-02 | Formatting toggles | [code.md](editors/code.md) section 8 | covered |
| UTMT-GMLS-03 | Empty line rules | [code.md](editors/code.md) section 8 | covered |
| UTMT-GMLS-04 | Cleanup passes | [code.md](editors/code.md) section 8 | covered |
| UTMT-GMLS-05 | Enum generation | [code.md](editors/code.md) section 8 | covered |
| UTMT-GMLS-06 | Unknown argument naming | [code.md](editors/code.md) section 8 | covered |
| UTMT-GMLS-07 | Allow leftover VM stack data | [code.md](editors/code.md) section 8 | covered |
| UTMT-GMLS-08 | Named instance ID prefix | [code.md](editors/code.md) section 8 | covered |

### Search and references (SRCH)

| ID | Feature | Spec and section | Status |
| --- | --- | --- | --- |
| UTMT-SRCH-01 | Search in code | [code.md](editors/code.md) section 9; [shell.md](shell.md) section 7.7 | covered |
| UTMT-SRCH-02 | Search results actions | [code.md](editors/code.md) section 9; [shell.md](shell.md) section 7.3 | covered |
| UTMT-SRCH-03 | Find all references | [code.md](editors/code.md) section 10 | covered |
| UTMT-SRCH-04 | Find unreferenced assets | [code.md](editors/code.md) section 10; [shell.md](shell.md) section 7.4 | covered |
| UTMT-SRCH-05 | Reference coverage | [code.md](editors/code.md) section 10; [shell.md](shell.md) section 7.4 | covered |
| UTMT-SRCH-06 | Show children objects | [code.md](editors/code.md) section 10; [objects.md](editors/objects.md) section 5.4 | covered |
| UTMT-SRCH-07 | Tile references | [backgrounds-tilesets.md](editors/backgrounds-tilesets.md) 7; [code.md](editors/code.md) section 10 | covered |

### Project system (PROJ)

| ID | Feature | Spec and section | Status |
| --- | --- | --- | --- |
| UTMT-PROJ-01 | New project | [projects.md](projects.md) section 3.3 | covered |
| UTMT-PROJ-02 | Open project | [projects.md](projects.md) sections 3.4, 10.1 | covered |
| UTMT-PROJ-03 | Save project | [projects.md](projects.md) sections 3.5, 10.2 | covered |
| UTMT-PROJ-04 | View unexported assets | [projects.md](projects.md) section 4.2 | covered |
| UTMT-PROJ-05 | Close project | [projects.md](projects.md) section 3.6 | covered |
| UTMT-PROJ-06 | Automatic change tracking | [shell.md](shell.md) section 4 | covered |
| UTMT-PROJ-07 | Serializable asset types | [projects.md](projects.md) section 5 | covered |
| UTMT-PROJ-08 | GML source retention | [projects.md](projects.md) section 6 | covered |
| UTMT-PROJ-09 | Project options file | [projects.md](projects.md) section 7 | covered |
| UTMT-PROJ-10 | BPS patches | [projects.md](projects.md) section 8 | covered |
| UTMT-PROJ-11 | Project scripts | [projects.md](projects.md) section 9.1 | covered |
| UTMT-PROJ-12 | Game file backup | [run-game.md](run-game.md) section 5.3; [projects.md](projects.md) section 9.2 | covered |
| UTMT-PROJ-13 | Streamed sound copy | [sounds.md](editors/sounds.md) 5 | covered |

### Code editor (CODE)

| ID | Feature | Spec and section | Status |
| --- | --- | --- | --- |
| UTMT-CODE-01 | Code entry properties | [code.md](editors/code.md) section 3.4 | covered |
| UTMT-CODE-02 | Decompiled view | [code.md](editors/code.md) sections 3.1 and 6 | covered |
| UTMT-CODE-03 | GML compile | [code.md](editors/code.md) sections 3.1 and 6 | covered |
| UTMT-CODE-04 | Disassembly view | [code.md](editors/code.md) section 3.2 | covered |
| UTMT-CODE-05 | Assemble | [code.md](editors/code.md) section 3.2 | covered |
| UTMT-CODE-06 | Syntax highlighting | [code.md](editors/code.md) section 4; [shaders.md](editors/shaders.md) 4, 6 (see ) | covered |
| UTMT-CODE-07 | Semantic coloring | [code.md](editors/code.md) section 4; [shaders.md](editors/shaders.md) 4, 6 (see ) | covered |
| UTMT-CODE-08 | Go to definition | [code.md](editors/code.md) section 4; [shaders.md](editors/shaders.md) 4, 6 (see ) | covered |
| UTMT-CODE-09 | Number resolution menu | [code.md](editors/code.md) section 4; [shaders.md](editors/shaders.md) 4, 6 (see ) | covered |
| UTMT-CODE-10 | Find and replace | [code.md](editors/code.md) section 4; [shaders.md](editors/shaders.md) 4, 6 (see ) | covered |
| UTMT-CODE-11 | Zoom | [code.md](editors/code.md) section 4; [shaders.md](editors/shaders.md) 4, 6 (see ) | covered |
| UTMT-CODE-12 | Text comments for Undertale and Deltarune | [code.md](editors/code.md) section 4; [shaders.md](editors/shaders.md) 4, 6 (see ) | covered |
| UTMT-CODE-13 | Open at line | [code.md](editors/code.md) section 4; [shaders.md](editors/shaders.md) 4, 6 (see ) | covered |
| UTMT-CODE-14 | Script asset editor | [scripts.md](editors/scripts.md) sections 2 and 3; [timelines.md](editors/timelines.md) 1, 2 | covered |
| UTMT-CODE-15 | Modern GML syntax | [code.md](editors/code.md) section 4 | covered |
| UTMT-CODE-16 | Game-specific definitions | [code.md](editors/code.md) section 4 | covered |
| UTMT-CODE-17 | Code import groups | [code.md](editors/code.md) section 7 | covered |

### Sprites (SPR)

| ID | Feature | Spec and section | Status |
| --- | --- | --- | --- |
| UTMT-SPR-01 | Sprite properties | [sprites.md](editors/sprites.md) sections 3.1 and 4.3 | covered |
| UTMT-SPR-02 | Frame list | [image-editor.md](editors/image-editor.md) 1, 5; [sprites.md](editors/sprites.md) section 4.1 | covered |
| UTMT-SPR-03 | Collision masks | [sprites.md](editors/sprites.md) section 3.2 | covered |
| UTMT-SPR-04 | GMS2 special type | [sprites.md](editors/sprites.md) sections 3.1 and 3.4 | covered |
| UTMT-SPR-05 | Export all frames | [image-editor.md](editors/image-editor.md) 5; [sprites.md](editors/sprites.md) sections 3.4 and 5 | covered |

### Backgrounds and tile sets (BG)

| ID | Feature | Spec and section | Status |
| --- | --- | --- | --- |
| UTMT-BG-01 | Background properties | [backgrounds-tilesets.md](editors/backgrounds-tilesets.md) 3.1 | covered |
| UTMT-BG-02 | GMS2 tile set properties | [backgrounds-tilesets.md](editors/backgrounds-tilesets.md) 3.2 | covered |
| UTMT-BG-03 | Tile ID table | [backgrounds-tilesets.md](editors/backgrounds-tilesets.md) 4.2 | covered |
| UTMT-BG-04 | Pick tile on texture | [backgrounds-tilesets.md](editors/backgrounds-tilesets.md) 4.1 | covered |
| UTMT-BG-05 | Tile separation display | [backgrounds-tilesets.md](editors/backgrounds-tilesets.md) 3.2 | covered |

### Textures (TEX)

| ID | Feature | Spec and section | Status |
| --- | --- | --- | --- |
| UTMT-TEX-01 | Texture page item editor | [texture-pages.md](editors/texture-pages.md) 3.1 | covered |
| UTMT-TEX-02 | Page item import and export | [image-editor.md](editors/image-editor.md) 3, 10 (commit writes the item; external edit); [texture-pages.md](editors/texture-pages.md) 3.2 | covered |
| UTMT-TEX-03 | Embedded texture editor | [texture-pages.md](editors/texture-pages.md) 4.1 | covered |
| UTMT-TEX-04 | Embedded texture import and export | [texture-pages.md](editors/texture-pages.md) 4.2 | covered |
| UTMT-TEX-05 | Page item hit test | [texture-pages.md](editors/texture-pages.md) 3.3 | covered |
| UTMT-TEX-06 | Embedded images | [embedded-images.md](editors/embedded-images.md) 3; [texture-pages.md](editors/texture-pages.md) See  | covered |
| UTMT-TEX-07 | Texture group info | [texture-pages.md](editors/texture-pages.md) 6.1 | covered |
| UTMT-TEX-08 | Texture formats | [texture-pages.md](editors/texture-pages.md) 5 | covered |
| UTMT-TEX-09 | Image cache | [rooms.md](editors/rooms.md) section 9; [texture-pages.md](editors/texture-pages.md) 7 | covered |
| UTMT-TEX-10 | Texture packing helpers | [texture-pages.md](editors/texture-pages.md) 6.2 | covered |

### Fonts (FNT)

| ID | Feature | Spec and section | Status |
| --- | --- | --- | --- |
| UTMT-FNT-01 | Font properties | [fonts.md](editors/fonts.md) 3 | covered |
| UTMT-FNT-02 | Glyph table | [fonts.md](editors/fonts.md) 4 | covered |
| UTMT-FNT-03 | Glyph rectangle editor | [fonts.md](editors/fonts.md) 4.2 | covered |
| UTMT-FNT-04 | Kerning editor | [fonts.md](editors/fonts.md) 4.3 | covered |
| UTMT-FNT-05 | Glyph maintenance | [fonts.md](editors/fonts.md) 4.1 | covered |

### Audio (SND)

| ID | Feature | Spec and section | Status |
| --- | --- | --- | --- |
| UTMT-SND-01 | Sound properties | [sounds.md](editors/sounds.md) 3 | covered |
| UTMT-SND-02 | Sound playback | [sounds.md](editors/sounds.md) 4 | covered |
| UTMT-SND-03 | Audio file reference control | [sounds.md](editors/sounds.md) 3.1 | covered |
| UTMT-SND-04 | Embedded audio editor | [sounds.md](editors/sounds.md) 5 | covered |
| UTMT-SND-05 | Audio group editor | [sounds.md](editors/sounds.md) 6.1 | covered |
| UTMT-SND-06 | Audio group files | [sounds.md](editors/sounds.md) 6.3 | covered |

### Game objects (OBJ)

| ID | Feature | Spec and section | Status |
| --- | --- | --- | --- |
| UTMT-OBJ-01 | Object properties | [objects.md](editors/objects.md) section 3 | covered |
| UTMT-OBJ-02 | Physics properties | [objects.md](editors/objects.md) section 4 | covered |
| UTMT-OBJ-03 | Physics vertices | [objects.md](editors/objects.md) section 4 | covered |
| UTMT-OBJ-04 | Event list | [objects.md](editors/objects.md) section 5 | covered |
| UTMT-OBJ-05 | Event actions | [objects.md](editors/objects.md) section 5 | covered |
| UTMT-OBJ-06 | Event subtype pickers | [objects.md](editors/objects.md) section 5 | covered |

### Rooms (ROOM)

| ID | Feature | Spec and section | Status |
| --- | --- | --- | --- |
| UTMT-ROOM-01 | Room properties | [rooms.md](editors/rooms.md) 3.1 | covered |
| UTMT-ROOM-02 | Room canvas render | [rooms.md](editors/rooms.md) 9 | covered |
| UTMT-ROOM-03 | Room object tree | [rooms.md](editors/rooms.md) 2, 4.1, 4.3 | covered |
| UTMT-ROOM-04 | GMS1 backgrounds | [rooms.md](editors/rooms.md) 3.3 | covered |
| UTMT-ROOM-05 | Views | [rooms.md](editors/rooms.md) 3.2 | covered |
| UTMT-ROOM-06 | Instance properties | [rooms.md](editors/rooms.md) 5.1 | covered |
| UTMT-ROOM-07 | Tile properties | [rooms.md](editors/rooms.md) 6.1 | covered |
| UTMT-ROOM-08 | Layer properties | [rooms.md](editors/rooms.md) 4.1, 4.2 | covered |
| UTMT-ROOM-09 | Sprite instances | [rooms.md](editors/rooms.md) 5.2 | covered |
| UTMT-ROOM-10 | Particle system instances | [particle-systems.md](editors/particle-systems.md) rooms.md 5.3; [rooms.md](editors/rooms.md) 5.3 | covered |
| UTMT-ROOM-11 | Add items | [rooms.md](editors/rooms.md) 4.1 (Create Layer), 5.7 (drop and paste), 6.1 | covered |
| UTMT-ROOM-12 | Select and drag | [rooms.md](editors/rooms.md) 5.7 | covered |
| UTMT-ROOM-13 | Paint copies | [rooms.md](editors/rooms.md) 5.7 | covered |
| UTMT-ROOM-14 | Drop from resource tree | [rooms.md](editors/rooms.md) 5.7 | covered |
| UTMT-ROOM-15 | Copy and paste | [rooms.md](editors/rooms.md) 5.7 | covered |
| UTMT-ROOM-16 | Undo move | [rooms.md](editors/rooms.md) 12 | covered |
| UTMT-ROOM-17 | Delete and reorder | [rooms.md](editors/rooms.md) 4.1, 4.3, 5.7 | covered |
| UTMT-ROOM-18 | Flip instance | [rooms.md](editors/rooms.md) 5.7 | covered |
| UTMT-ROOM-19 | Zoom and scroll | [rooms.md](editors/rooms.md) 5.7 | covered |
| UTMT-ROOM-20 | Grid overlay | [rooms.md](editors/rooms.md) 3.1, 5.7 | covered |
| UTMT-ROOM-21 | Export room as PNG | [rooms.md](editors/rooms.md) 9 | covered |
| UTMT-ROOM-22 | Tile data CSV | [rooms.md](editors/rooms.md) 6.3 | covered |
| UTMT-ROOM-23 | Auto size tile layer | [rooms.md](editors/rooms.md) 4.2 | covered |
| UTMT-ROOM-24 | Instance creation order | [rooms.md](editors/rooms.md) 7 | covered |
| UTMT-ROOM-25 | Path layers | [paths.md](editors/paths.md) 4.3 (backdrop), rooms.md 4.2; [rooms.md](editors/rooms.md) 4.2 | covered |
| UTMT-ROOM-26 | Sequence instances | [rooms.md](editors/rooms.md) 5.4; [sequences.md](editors/sequences.md) rooms.md 5.4 | covered |

### Tile layer editor (TILE)

| ID | Feature | Spec and section | Status |
| --- | --- | --- | --- |
| UTMT-TILE-01 | Tile palette | [rooms.md](editors/rooms.md) 6.2 | covered |
| UTMT-TILE-02 | Paint and line | [rooms.md](editors/rooms.md) 6.2 | covered |
| UTMT-TILE-03 | Fill | [rooms.md](editors/rooms.md) 6.2 | covered |
| UTMT-TILE-04 | Pick | [rooms.md](editors/rooms.md) 6.2 | covered |
| UTMT-TILE-05 | Brush transforms | [rooms.md](editors/rooms.md) 6.2 | covered |
| UTMT-TILE-06 | View toggles | [rooms.md](editors/rooms.md) 6.2 | covered |
| UTMT-TILE-07 | Undo and redo | [rooms.md](editors/rooms.md) 6.2 | covered |
| UTMT-TILE-08 | Apply and cancel | [rooms.md](editors/rooms.md) 6.2 | covered |

### Other asset editors (AST)

| ID | Feature | Spec and section | Status |
| --- | --- | --- | --- |
| UTMT-AST-01 | Shader editor | [shaders.md](editors/shaders.md) 3, 4 | covered |
| UTMT-AST-02 | Path editor | [paths.md](editors/paths.md) 3, 4 | covered |
| UTMT-AST-03 | Timeline editor | [timelines.md](editors/timelines.md) 2, 3 | covered |
| UTMT-AST-04 | Extension editor | [extensions.md](editors/extensions.md) 3, 5 | covered |
| UTMT-AST-05 | Extension file editor | [extensions.md](editors/extensions.md) 4.1 | covered |
| UTMT-AST-06 | Extension function editor | [extensions.md](editors/extensions.md) 4.2 | covered |
| UTMT-AST-07 | String editor | [strings.md](editors/strings.md) sections 2 and 3 | covered |
| UTMT-AST-08 | Shared string edits | [languages.md](editors/languages.md) 2; [strings.md](editors/strings.md) section 4; [ux-standards.md](ux-standards.md) section 11 | covered |
| UTMT-AST-09 | Variable editor | [variables-functions.md](editors/variables-functions.md) 3 | covered |
| UTMT-AST-10 | Variables chunk header | [variables-functions.md](editors/variables-functions.md) 3.1 | covered |
| UTMT-AST-11 | Function editor | [variables-functions.md](editors/variables-functions.md) 4 | covered |
| UTMT-AST-12 | Code locals editor | [code.md](editors/code.md) section 3.3; [variables-functions.md](editors/variables-functions.md) 5 | covered |
| UTMT-AST-13 | Global init scripts | [global-scripts.md](editors/global-scripts.md) 3; [scripts.md](editors/scripts.md) section 3.1; [shell.md](shell.md) section 5.2 | covered |
| UTMT-AST-14 | Game End scripts | [global-scripts.md](editors/global-scripts.md) 4; [scripts.md](editors/scripts.md) section 3.2 | covered |
| UTMT-AST-15 | Particle system editor | [particle-systems.md](editors/particle-systems.md) 3 | covered |
| UTMT-AST-16 | Particle emitter editor | [particle-systems.md](editors/particle-systems.md) 4 | covered |

### General info, options and language (GEN)

| ID | Feature | Spec and section | Status |
| --- | --- | --- | --- |
| UTMT-GEN-01 | General info fields | [general-info.md](editors/general-info.md) sections 3.1 and 3.3 | covered |
| UTMT-GEN-02 | Info flags | [general-info.md](editors/general-info.md) sections 3.1 and 3.3 | covered |
| UTMT-GEN-03 | Active targets and function classifications | [general-info.md](editors/general-info.md) sections 3.1 and 3.3 | covered |
| UTMT-GEN-04 | Room order | [general-info.md](editors/general-info.md) section 3.5; [shell.md](shell.md) section 5.2 | covered |
| UTMT-GEN-05 | Debugger checkbox guard | [general-info.md](editors/general-info.md) sections 3.1 and 3.3 | covered |
| UTMT-GEN-06 | GMS2 extras | [general-info.md](editors/general-info.md) sections 3.1 and 3.3 | covered |
| UTMT-GEN-07 | Options (OPTN) | [general-info.md](editors/general-info.md) sections 3.2 and 3.4 | covered |
| UTMT-GEN-08 | Language (LANG) | [general-info.md](editors/general-info.md) section 3.6; [languages.md](editors/languages.md) 3.1 (read-only fields kept) and 3.2, 3.3 (entries, which UTMT lacks) | covered |

### Chunks without a GUI editor (NOED)

| ID | Feature | Spec and section | Status |
| --- | --- | --- | --- |
| UTMT-NOED-01 | Sequences (SEQN) | [sequences.md](editors/sequences.md) 3 to 7 | covered |
| UTMT-NOED-02 | Animation curves (ACRV) | [animation-curves.md](editors/animation-curves.md) 3, 4 | covered |
| UTMT-NOED-03 | Tags (TAGS) | [tags-feature-flags.md](editors/tags-feature-flags.md) 2 | covered |
| UTMT-NOED-04 | Feature flags (FEAT) | [tags-feature-flags.md](editors/tags-feature-flags.md) 3 | covered |
| UTMT-NOED-05 | Filter effects (FEDS) | [filter-effects.md](editors/filter-effects.md) 2, 3, 4 | covered |
| UTMT-NOED-06 | UI layers (UILR) | [rooms.md](editors/rooms.md) 8 | covered |
| UTMT-NOED-07 | Data files (DAFL) | [tags-feature-flags.md](editors/tags-feature-flags.md) 4 | covered |
| UTMT-NOED-08 | Room text items and nine-slices | [rooms.md](editors/rooms.md) 5.5, 5.6 | covered |
| UTMT-NOED-09 | Effect layer properties | [filter-effects.md](editors/filter-effects.md) 6, and rooms.md 4.2; [rooms.md](editors/rooms.md) 4.2 | covered |

### Shared controls (CTRL)

| ID | Feature | Spec and section | Status |
| --- | --- | --- | --- |
| UTMT-CTRL-01 | Asset reference field | [backgrounds-tilesets.md](editors/backgrounds-tilesets.md) 3, 4; [embedded-images.md](editors/embedded-images.md) 2, 3; [fonts.md](editors/fonts.md) 3, 4; [global-scripts.md](editors/global-scripts.md) 2, 3 | covered |
| UTMT-CTRL-02 | String reference field | [embedded-images.md](editors/embedded-images.md) 2, 3; [filter-effects.md](editors/filter-effects.md) 3, 4; [languages.md](editors/languages.md) 2, 4; [strings.md](editors/strings.md) section 4 and the String reference field in ux-standards section 11 | covered |
| UTMT-CTRL-03 | Texture page item display | [backgrounds-tilesets.md](editors/backgrounds-tilesets.md) 3, 4; [embedded-images.md](editors/embedded-images.md) 2, 3; [fonts.md](editors/fonts.md) 2, 4; [image-editor.md](editors/image-editor.md) 8 | covered |
| UTMT-CTRL-04 | Flags box | [general-info.md](editors/general-info.md) section 3.2; [sounds.md](editors/sounds.md) 3, 6.1; [ux-standards.md](ux-standards.md) section 11 | covered |
| UTMT-CTRL-05 | Color picker | [filter-effects.md](editors/filter-effects.md) 3, 4; [image-editor.md](editors/image-editor.md) 7; [particle-systems.md](editors/particle-systems.md) 3, 4; [rooms.md](editors/rooms.md) 3, 5 | covered |
| UTMT-CTRL-06 | Editable list grids | [animation-curves.md](editors/animation-curves.md) 3, 4; [backgrounds-tilesets.md](editors/backgrounds-tilesets.md) 3, 4; [extensions.md](editors/extensions.md) 3, 4, 5; [fonts.md](editors/fonts.md) 3, 4 | covered |

### Scripting system (SCRIPT)

| ID | Feature | Spec and section | Status |
| --- | --- | --- | --- |
| UTMT-SCRIPT-01 | Scripts menu (user and project folders; no scripts are bundled, Q42) | [scripting.md](scripting.md) sections 2, 3.1, 6; bundled script concepts: [script-features.md](script-features.md) | covered |
| UTMT-SCRIPT-02 | Run other script | [scripting.md](scripting.md) section 3.1 | covered |
| UTMT-SCRIPT-03 | C# script engine | [scripting.md](scripting.md) section 3.2 | covered |
| UTMT-SCRIPT-04 | Script error reporting | [scripting.md](scripting.md) section 3.3 | covered |
| UTMT-SCRIPT-05 | Nested scripts and linting | [scripting.md](scripting.md) section 3.5 | covered |
| UTMT-SCRIPT-06 | Finished message | [scripting.md](scripting.md) section 3.6 | covered |
| UTMT-SCRIPT-07 | Script helpers | [scripting.md](scripting.md) section 3.7 | covered |

The 110 bundled scripts (inventory in [../research/utmt-parity.md](../research/utmt-parity.md#bundled-script-inventory)) are not shipped. Each one has an outcome in [../research/utmt-scripts-analysis.md](../research/utmt-scripts-analysis.md): 36 define a built-in feature, 32 are merged into one, 24 are plugin candidates, 18 are dropped with a reason. The features are in [script-features.md](script-features.md) (SF-01 to SF-13, Fonts editor additions, plugin candidates P-1 to P-8). Requirement F-PAR-3 is met by that mapping, not by shipped files.

### Script interface API (API)

| ID | Feature | Spec and section | Status |
| --- | --- | --- | --- |
| UTMT-API-01 | State accessors | [scripting.md](scripting.md) section 5.2 | covered |
| UTMT-API-02 | Data helpers | [scripting.md](scripting.md) section 5.2 | covered |
| UTMT-API-03 | Message dialogs | [scripting.md](scripting.md) sections 5.2, 7.1 | covered |
| UTMT-API-04 | Input and output dialogs | [scripting.md](scripting.md) sections 5.2, 7.2, 7.3 | covered |
| UTMT-API-05 | Clickable search output | [scripting.md](scripting.md) sections 5.2, 7.4 | covered |
| UTMT-API-06 | Progress API | [scripting.md](scripting.md) sections 5.2, 7.5 | covered |
| UTMT-API-07 | File prompts | [scripting.md](scripting.md) sections 5.2, 7.6 | covered |
| UTMT-API-08 | Code text | [code.md](editors/code.md) section 7 (text of code by name is available to scripts through the same decompile service) | covered |
| UTMT-API-09 | Navigation | [scripting.md](scripting.md) section 5.2 | covered |
| UTMT-API-10 | Removed members | [scripting.md](scripting.md) sections 5.2, 5.3 | covered |

### Command-line tool (CLI), left to G3MTool

| ID | Feature | Spec and section | Status |
| --- | --- | --- | --- |
| UTMT-CLI-01 | `new` | G3MTool CLI/GUI (not duplicated): blank data file: File > New > New Data File in the GUI ([shell.md](shell.md) section 2); scripted use is G3MTool | covered by G3MTool |
| UTMT-CLI-02 | `load` | G3MTool CLI/GUI (not duplicated): `execute` runs scripts; in the window use Scripts > Run Script... and the Script Console ([scripting.md](scripting.md) section 4) | covered by G3MTool |
| UTMT-CLI-03 | `load -i` interactive menu | G3MTool CLI/GUI (not duplicated): interactive terminal menu; G3MTool CLI only | covered by G3MTool |
| UTMT-CLI-04 | `info` | G3MTool CLI/GUI (not duplicated): `info`; in the window use Mod > Inspect File... ([editors/patches.md](editors/patches.md) section 4.5) | covered by G3MTool |
| UTMT-CLI-05 | `dump` | G3MTool CLI/GUI (not duplicated): scripted dump is G3MTool; in the window use Tools > Export All... | covered by G3MTool |
| UTMT-CLI-06 | `replace` | G3MTool CLI/GUI (not duplicated): scripted replace is G3MTool; in the window use the code and texture import commands | covered by G3MTool |
| UTMT-CLI-07 | `project build` | G3MTool CLI/GUI (not duplicated): headless project build is G3MTool; in the window use Project > Build ([projects.md](projects.md)) | covered by G3MTool |
| UTMT-CLI-08 | Headless script interface | G3MTool CLI/GUI (not duplicated): headless script interface is G3MTool `execute` | covered by G3MTool |

### Platform and distribution (PLAT)

| ID | Feature | Spec and section | Status |
| --- | --- | --- | --- |
| UTMT-PLAT-01 | File associations | [onboarding.md](onboarding.md)  | covered |
| UTMT-PLAT-02 | GUI platform | [platforms-formats.md](platforms-formats.md) section 3 | covered |
| UTMT-PLAT-03 | In-app updater | [platforms-formats.md](platforms-formats.md) section 4 | covered |
| UTMT-PLAT-04 | Scripts shipped beside the exe | [platforms-formats.md](platforms-formats.md) section 5 | covered |
| UTMT-PLAT-05 | Multiple instances | [platforms-formats.md](platforms-formats.md) section 6 | covered |

### Keyboard shortcuts (KEY)

| ID | Feature | Spec and section | Status |
| --- | --- | --- | --- |
| UTMT-KEY-01 | Global shortcuts | [shell.md](shell.md) ; section 16 | covered |
| UTMT-KEY-02 | Tab shortcuts | [shell.md](shell.md) ; section 16 | covered |
| UTMT-KEY-03 | Tree shortcuts | [shell.md](shell.md) ; section 16 | covered |
| UTMT-KEY-04 | Code editor shortcuts | [code.md](editors/code.md) section 5; [shell.md](shell.md) section 16 | covered |
| UTMT-KEY-05 | Room editor shortcuts | [rooms.md](editors/rooms.md) 10; [shell.md](shell.md) section 16 | covered |
| UTMT-KEY-06 | Tile editor shortcuts | [rooms.md](editors/rooms.md) 10; [shell.md](shell.md) section 16 | covered |

### Data formats and GameMaker versions (FMT)

| ID | Feature | Spec and section | Status |
| --- | --- | --- | --- |
| UTMT-FMT-01 | Data file containers | [platforms-formats.md](platforms-formats.md) section 7.1 | covered |
| UTMT-FMT-02 | Chunk coverage | [platforms-formats.md](platforms-formats.md) section 7.2 | covered |
| UTMT-FMT-03 | Bytecode versions | [platforms-formats.md](platforms-formats.md) section 7.3 | covered |
| UTMT-FMT-04 | GMS1 and GMS2 split | [rooms.md](editors/rooms.md) 1, 2, 3.3 | covered |
| UTMT-FMT-05 | Version detection | [platforms-formats.md](platforms-formats.md) section 7.4 | covered |
| UTMT-FMT-06 | YYC games | [platforms-formats.md](platforms-formats.md) section 7.5 | covered |
| UTMT-FMT-07 | Audio formats | [sounds.md](editors/sounds.md) 3.2, 5 | covered |
| UTMT-FMT-08 | Texture formats | [texture-pages.md](editors/texture-pages.md) 5 | covered |
| UTMT-FMT-09 | Debug data format | [platforms-formats.md](platforms-formats.md) section 9.1 | covered |
| UTMT-FMT-10 | Write integrity checks | [platforms-formats.md](platforms-formats.md) section 8 | covered |
| UTMT-FMT-11 | Pointer offset map | [platforms-formats.md](platforms-formats.md) section 9.2 | covered |

## Not covered

None. Every UTMT ID is cited by at least one design spec.
