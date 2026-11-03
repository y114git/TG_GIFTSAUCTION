# Parity coverage

Status: generated from the "Parity coverage" sections of the design specs. Every ID in [../research/utmt-parity.md](../research/utmt-parity.md) and [../research/modding-suite.md](../research/modding-suite.md) is mapped to the spec section that covers it. Where UTMT or a G3M tool did something in a way G3MStudio replaces on purpose, the row says "Replaced" and names the replacement.

Totals: 393 IDs (272 UTMT, 58 TOOL, 26 MCE, 37 G3M). IDs without a home: 0. G3MStudio has no command line (questions.md Q24): command line only features are covered by G3MTool CLI/GUI (not duplicated).

## UndertaleModTool parity (UTMT-*)

| ID | Feature | Spec | Where |
| --- | --- | --- | --- |
| UTMT-APP-01 | Menu bar | [shell.md](shell.md) | section 2 (menus) |
| UTMT-APP-02 | Resource tree | [editors/scripts.md](editors/scripts.md)<br>[editors/strings.md](editors/strings.md)<br>[shell.md](shell.md) | section 1<br>list view in section 2<br>section 5.2 (tree content) and the editor specs for each node |
| UTMT-APP-03 | Tree name filter | [shell.md](shell.md) | section 5.4 (name filter) |
| UTMT-APP-04 | Back and forward buttons | [shell.md](shell.md) | section 9 item 8 and the breadcrumb bar |
| UTMT-APP-05 | C# command box | [scripting.md](scripting.md) | section 4 |
| UTMT-APP-06 | Asset ID label | [shell.md](shell.md) | section 4 (Asset ID item) |
| UTMT-APP-07 | Welcome and description view | [onboarding.md](onboarding.md)<br>[shell.md](shell.md) | listed in the page header<br>section 15 (empty states) and the start page |
| UTMT-APP-08 | Modal loader dialog | [shell.md](shell.md)<br>[ux-standards.md](ux-standards.md) | section 14.1 and ux-standards section 9 (progress replaces the modal loader)<br>sections 9 and 10 (progress and notifications replace the modal loader) |
| UTMT-APP-09 | Dark mode | [ux-standards.md](ux-standards.md) | sections 1 and 2 (one themeable default theme with contrast rules; replaced, no light and dark toggle) |
| UTMT-APP-10 | Window placement memory | [shell.md](shell.md) | section 1 (window placement) |
| UTMT-APP-11 | Help menu | [shell.md](shell.md) | section 14.5 |
| UTMT-APP-12 | Clickable results window | [editors/code.md](editors/code.md)<br>[scripting.md](scripting.md) | section 9<br>section 7.4 |
| UTMT-APP-13 | Text input dialogs | [scripting.md](scripting.md)<br>[shell.md](shell.md)<br>[ux-standards.md](ux-standards.md) | section 7.2<br>ux-standards section 11 (text input and message dialogs)<br>section 11 (text input and message dialogs) |
| UTMT-APP-14 | Crash logging | [shell.md](shell.md) | section 14.5 |
| UTMT-APP-15 | Message boxes | [scripting.md](scripting.md)<br>[shell.md](shell.md)<br>[ux-standards.md](ux-standards.md) | section 7.1<br>ux-standards section 11 (text input and message dialogs)<br>section 11 (text input and message dialogs) |
| UTMT-FILE-01 | New data file | [shell.md](shell.md) | section 2 (File menu) and section 14.1 |
| UTMT-FILE-02 | Open data file | [shell.md](shell.md) | section 2 (File menu) and section 14.1 |
| UTMT-FILE-03 | Save data file | [shell.md](shell.md) | section 14.2 (save) |
| UTMT-FILE-04 | Load warnings | [shell.md](shell.md) | section 14.1 (load warnings) |
| UTMT-FILE-05 | Unsupported bytecode handling | [editors/code.md](editors/code.md)<br>[platforms-formats.md](platforms-formats.md) | section 1<br>section 7.3 |
| UTMT-FILE-06 | YYC detection | [editors/code.md](editors/code.md)<br>[platforms-formats.md](platforms-formats.md) | section 1<br>section 7.5 |
| UTMT-FILE-07 | Debugger flag warnings | [editors/general-info.md](editors/general-info.md)<br>[run-game.md](run-game.md) | section 5 (debugger flag warning)<br>section 3, 4 |
| UTMT-FILE-08 | Unknown texture format warning | [editors/sprites.md](editors/sprites.md) | section 9 |
| UTMT-FILE-09 | Close and unsaved-changes prompt | [shell.md](shell.md) | section 14.2 and settings.md section 4.1 |
| UTMT-FILE-10 | Generate offset map | [platforms-formats.md](platforms-formats.md) | section 9.2 |
| UTMT-FILE-11 | Audio group child instances | [editors/sounds.md](editors/sounds.md)<br>[platforms-formats.md](platforms-formats.md)<br>[shell.md](shell.md) | section 6.2<br>section 6<br>sounds.md section 6.2 (linked files in the Explorer) |
| UTMT-FILE-12 | Command-line arguments | [shell.md](shell.md) | section 13, section 13.1 and section 14.1 |
| UTMT-FILE-13 | Drag and drop onto window | [shell.md](shell.md) | section 13 and section 14.1 |
| UTMT-FILE-14 | Path confinement | [editors/included-files.md](editors/included-files.md)<br>[projects.md](projects.md)<br>[run-game.md](run-game.md)<br>[scripting.md](scripting.md) | section 4 (paths stay inside the expected folder)<br>section 7, 9.1<br>section 3<br>section 3.2, 8 |
| UTMT-TREE-01 | Open asset | [shell.md](shell.md) | sections 5.5 and 5.6 |
| UTMT-TREE-02 | Open in new tab | [shell.md](shell.md) | sections 5.5 and 5.6 |
| UTMT-TREE-03 | Add asset | [shell.md](shell.md) | section 5.6 (New Resource) and each editor's creation notes |
| UTMT-TREE-04 | Type-specific defaults on add | [editors/extensions.md](editors/extensions.md)<br>[editors/general-info.md](editors/general-info.md)<br>[editors/global-scripts.md](editors/global-scripts.md)<br>[editors/rooms.md](editors/rooms.md)<br>[editors/scripts.md](editors/scripts.md)<br>[editors/shaders.md](editors/shaders.md)<br>[shell.md](shell.md) | folder, class, version, product ID): section 3<br>Explorer defaults on add are in the shell; the general-info side effects (room order entry on adding a room) are in section 3.5<br>section 3<br>GMS2, 2.3 or 2024.13 flags and optional room-order entry): New Resource dialog for rooms asks for the room order insertion<br>section 3<br>section 3<br>section 5.6 (New Resource) and each editor's creation notes |
| UTMT-TREE-05 | Item context menu | [shell.md](shell.md) | sections 5.5 and 5.6 |
| UTMT-TREE-06 | Delete asset | [shell.md](shell.md) | section 5.6 (Delete with reference scan) and ux-standards section 7 |
| UTMT-TREE-07 | Copy name | [shell.md](shell.md) | sections 5.5 and 5.6 |
| UTMT-TREE-08 | Reorder by drag | [shell.md](shell.md) | section 5.4 |
| UTMT-TREE-09 | Drag asset as reference | [editors/rooms.md](editors/rooms.md)<br>[shell.md](shell.md) | section 3, item 7<br>section 5.5 item 4 |
| UTMT-TREE-10 | Version-aware labels | [editors/backgrounds-tilesets.md](editors/backgrounds-tilesets.md)<br>[shell.md](shell.md) | section 1<br>section 5.2 |
| UTMT-TREE-11 | Null entries | [shell.md](shell.md) | section 5.4 |
| UTMT-TAB-01 | Tab strip | [shell.md](shell.md) | section 9 |
| UTMT-TAB-02 | Close tab | [shell.md](shell.md) | section 9 |
| UTMT-TAB-03 | Close other and all tabs | [shell.md](shell.md) | section 9 |
| UTMT-TAB-04 | Restore closed tab | [shell.md](shell.md) | section 9 |
| UTMT-TAB-05 | Switch tabs | [shell.md](shell.md) | section 9 |
| UTMT-TAB-06 | Reorder tabs by drag | [shell.md](shell.md) | section 9 |
| UTMT-TAB-07 | Tab overflow scrolling | [shell.md](shell.md) | section 9 |
| UTMT-TAB-08 | Per-tab state restore | [editors/backgrounds-tilesets.md](editors/backgrounds-tilesets.md)<br>[shell.md](shell.md) | section 4.1<br>section 9 |
| UTMT-RUN-01 | Temp run | [run-game.md](run-game.md) | section 3 |
| UTMT-RUN-02 | Run with other runner | [run-game.md](run-game.md) | section 4 |
| UTMT-RUN-03 | Runtime picker | [run-game.md](run-game.md) | section 4 |
| UTMT-RUN-04 | Run under GMS debugger | [run-game.md](run-game.md) | section 6 |
| UTMT-RUN-05 | Debug data generation | [run-game.md](run-game.md) | section 6 |
| UTMT-RUN-06 | Shell verbs | [onboarding.md](onboarding.md)<br>[run-game.md](run-game.md)<br>[shell.md](shell.md) | listed in the page header<br>UTMT-RUN-06 (shell verbs) is in section 7<br>section 13.1 (launch forms) |
| UTMT-SET-01 | Settings storage | [settings.md](settings.md) | section 3 |
| UTMT-SET-02 | GameMaker paths | [settings.md](settings.md) | section 4.1, 5.6 |
| UTMT-SET-03 | Asset order swapping | [settings.md](settings.md) | section 4.1, 5.4 |
| UTMT-SET-04 | File association toggle | [onboarding.md](onboarding.md)<br>[settings.md](settings.md) | listed in the page header<br>section 4.1, 5.7 |
| UTMT-SET-05 | Debugger menu toggle | [run-game.md](run-game.md)<br>[settings.md](settings.md) | section 6<br>section 4.1, 5.5 |
| UTMT-SET-06 | Remember window placement | [settings.md](settings.md) | section 4.1, 5.4 |
| UTMT-SET-07 | Warn on close | [settings.md](settings.md)<br>[shell.md](shell.md) | section 4.1, 5.3<br>settings.md section 4.1 (hot exit replaces the prompt) |
| UTMT-SET-08 | Warn about temp running | [run-game.md](run-game.md)<br>[settings.md](settings.md) | section 3<br>section 4.1, 5.5, 5.9 |
| UTMT-SET-09 | Show null entries in tree | [settings.md](settings.md) | section 4.1, 5.4 |
| UTMT-SET-10 | Recompile all GML on project save | [projects.md](projects.md)<br>[settings.md](settings.md) | section 6<br>section 4.1, 5.5 |
| UTMT-SET-11 | Dark mode toggle | [settings.md](settings.md)<br>[ux-standards.md](ux-standards.md) | section 4.1, 5.1<br>section 2 (replaced by the single theme; see settings.md) |
| UTMT-SET-12 | Transparency grid colors | [editors/sprites.md](editors/sprites.md)<br>[settings.md](settings.md) | section 4.2<br>section 4.1, 5.1 |
| UTMT-SET-13 | Global room grid override | [editors/rooms.md](editors/rooms.md)<br>[settings.md](settings.md) | section 3, item 2<br>section 4.1 |
| UTMT-SET-14 | Open app data folder | [settings.md](settings.md) | section 4.1, 5.11, 6 |
| UTMT-SET-15 | Update app button | [settings.md](settings.md) | section 4.1, 5.8 |
| UTMT-SET-16 | GML settings button | [editors/code.md](editors/code.md)<br>[settings.md](settings.md) | section 8<br>section 4.1 |
| UTMT-GMLS-01 | Indentation style | [editors/code.md](editors/code.md) | section 8 |
| UTMT-GMLS-02 | Formatting toggles | [editors/code.md](editors/code.md) | section 8 |
| UTMT-GMLS-03 | Empty line rules | [editors/code.md](editors/code.md) | section 8 |
| UTMT-GMLS-04 | Cleanup passes | [editors/code.md](editors/code.md) | section 8 |
| UTMT-GMLS-05 | Enum generation | [editors/code.md](editors/code.md) | section 8 |
| UTMT-GMLS-06 | Unknown argument naming | [editors/code.md](editors/code.md) | section 8 |
| UTMT-GMLS-07 | Allow leftover VM stack data | [editors/code.md](editors/code.md) | section 8 |
| UTMT-GMLS-08 | Named instance ID prefix | [editors/code.md](editors/code.md) | section 8 |
| UTMT-SRCH-01 | Search in code | [editors/code.md](editors/code.md) | section 9 |
| UTMT-SRCH-02 | Search results actions | [editors/code.md](editors/code.md) | section 9 |
| UTMT-SRCH-03 | Find all references | [editors/code.md](editors/code.md) | section 10 |
| UTMT-SRCH-04 | Find unreferenced assets | [editors/code.md](editors/code.md) | section 10 |
| UTMT-SRCH-05 | Reference coverage | [editors/code.md](editors/code.md)<br>[editors/sequences.md](editors/sequences.md) | section 10<br>tracks and keyframes appear in References |
| UTMT-SRCH-06 | Show children objects | [editors/code.md](editors/code.md)<br>[editors/objects.md](editors/objects.md) | section 10<br>section 5.4 |
| UTMT-SRCH-07 | Tile references | [editors/backgrounds-tilesets.md](editors/backgrounds-tilesets.md)<br>[editors/code.md](editors/code.md) | section 7<br>section 10 |
| UTMT-PROJ-01 | New project | [projects.md](projects.md) | section 3.3 |
| UTMT-PROJ-02 | Open project | [projects.md](projects.md) | section 3.4, 10.1 |
| UTMT-PROJ-03 | Save project | [editors/git-panel.md](editors/git-panel.md)<br>[projects.md](projects.md) | the Git panel tracks the result; see projects.md<br>section 3.5, 10.2 |
| UTMT-PROJ-04 | View unexported assets | [projects.md](projects.md) | section 4.2 |
| UTMT-PROJ-05 | Close project | [projects.md](projects.md) | section 3.6 |
| UTMT-PROJ-06 | Automatic change tracking | [editors/data-diff.md](editors/data-diff.md)<br>[projects.md](projects.md) | section 6 markers; "Marked for export" in projects.md<br>section 4.1 |
| UTMT-PROJ-07 | Serializable asset types | [editors/animation-curves.md](editors/animation-curves.md)<br>[editors/sequences.md](editors/sequences.md)<br>[editors/shaders.md](editors/shaders.md)<br>[projects.md](projects.md) | one JSON file per curve<br>serialization is one JSON file per sequence<br>shaders export as `.vsh` and `.fsh` in project folders<br>section 5 |
| UTMT-PROJ-08 | GML source retention | [projects.md](projects.md) | section 6 |
| UTMT-PROJ-09 | Project options file | [editors/included-files.md](editors/included-files.md)<br>[projects.md](projects.md) | the project options list the same files, see projects.md<br>section 7 |
| UTMT-PROJ-10 | BPS patches | [projects.md](projects.md) | section 8 |
| UTMT-PROJ-11 | Project scripts | [projects.md](projects.md) | section 9.1 |
| UTMT-PROJ-12 | Game file backup | [editors/git-panel.md](editors/git-panel.md)<br>[projects.md](projects.md)<br>[run-game.md](run-game.md) | data-file protection is in run-game.md section 5.3; project-level changes are protected here by history<br>section 9.2<br>section 5.3 |
| UTMT-PROJ-13 | Streamed sound copy | [editors/sounds.md](editors/sounds.md)<br>[projects.md](projects.md) | section 6.3<br>section 9.2 |
| UTMT-CODE-01 | Code entry properties | [editors/code.md](editors/code.md) | section 3.4 |
| UTMT-CODE-02 | Decompiled view | [editors/code.md](editors/code.md) | sections 3.1 and 6 |
| UTMT-CODE-03 | GML compile | [editors/code.md](editors/code.md) | sections 3.1 and 6 |
| UTMT-CODE-04 | Disassembly view | [editors/code.md](editors/code.md) | section 3.2 |
| UTMT-CODE-05 | Assemble | [editors/code.md](editors/code.md) | section 3.2 |
| UTMT-CODE-06 | Syntax highlighting | [editors/code.md](editors/code.md) | section 4 |
| UTMT-CODE-07 | Semantic coloring | [editors/code.md](editors/code.md) | section 4 |
| UTMT-CODE-08 | Go to definition | [editors/code.md](editors/code.md) | section 4 |
| UTMT-CODE-09 | Number resolution menu | [editors/code.md](editors/code.md) | section 4 |
| UTMT-CODE-10 | Find and replace | [editors/code.md](editors/code.md) | section 4 |
| UTMT-CODE-11 | Zoom | [editors/code.md](editors/code.md) | section 4 |
| UTMT-CODE-12 | Text comments for Undertale and Deltarune | [editors/code.md](editors/code.md) | section 4 |
| UTMT-CODE-13 | Open at line | [editors/code.md](editors/code.md) | section 4 |
| UTMT-CODE-14 | Script asset editor | [editors/scripts.md](editors/scripts.md) | sections 2 and 3 |
| UTMT-CODE-15 | Modern GML syntax | [editors/code.md](editors/code.md) | section 4 |
| UTMT-CODE-16 | Game-specific definitions | [editors/code.md](editors/code.md) | section 4 |
| UTMT-CODE-17 | Code import groups | [editors/code.md](editors/code.md) | section 7 |
| UTMT-SPR-01 | Sprite properties | [editors/sprites.md](editors/sprites.md) | sections 3.1 and 4.3 |
| UTMT-SPR-02 | Frame list | [editors/sprites.md](editors/sprites.md) | section 4.1 |
| UTMT-SPR-03 | Collision masks | [editors/sprites.md](editors/sprites.md) | section 3.2 |
| UTMT-SPR-04 | GMS2 special type | [editors/sprites.md](editors/sprites.md) | sections 3.1 and 3.4 |
| UTMT-SPR-05 | Export all frames | [editors/sprites.md](editors/sprites.md) | sections 3.4 and 5 |
| UTMT-BG-01 | Background properties | [editors/backgrounds-tilesets.md](editors/backgrounds-tilesets.md) | section 3.1 |
| UTMT-BG-02 | GMS2 tile set properties | [editors/backgrounds-tilesets.md](editors/backgrounds-tilesets.md) | section 3.2 |
| UTMT-BG-03 | Tile ID table | [editors/backgrounds-tilesets.md](editors/backgrounds-tilesets.md) | section 4.2 |
| UTMT-BG-04 | Pick tile on texture | [editors/backgrounds-tilesets.md](editors/backgrounds-tilesets.md) | section 4.1 |
| UTMT-BG-05 | Tile separation display | [editors/backgrounds-tilesets.md](editors/backgrounds-tilesets.md) | section 3.2 |
| UTMT-TEX-01 | Texture page item editor | [editors/texture-pages.md](editors/texture-pages.md) | section 3 |
| UTMT-TEX-02 | Page item import and export | [editors/image-editor.md](editors/image-editor.md)<br>[editors/texture-pages.md](editors/texture-pages.md) | texture page item "Edit Item" opens here (section 1)<br>section 3 |
| UTMT-TEX-03 | Embedded texture editor | [editors/texture-pages.md](editors/texture-pages.md) | section 4 |
| UTMT-TEX-04 | Embedded texture import and export | [editors/texture-pages.md](editors/texture-pages.md) | section 4 |
| UTMT-TEX-05 | Page item hit test | [editors/texture-pages.md](editors/texture-pages.md) | section 2 |
| UTMT-TEX-06 | Embedded images | [editors/embedded-images.md](editors/embedded-images.md)<br>[editors/texture-pages.md](editors/texture-pages.md) | section 3<br>section 7 |
| UTMT-TEX-07 | Texture group info | [editors/texture-pages.md](editors/texture-pages.md) | section 5 |
| UTMT-TEX-08 | Texture formats | [editors/texture-pages.md](editors/texture-pages.md) | section 4 |
| UTMT-TEX-09 | Image cache | [editors/texture-pages.md](editors/texture-pages.md) | section 4 |
| UTMT-TEX-10 | Texture packing helpers | [editors/texture-pages.md](editors/texture-pages.md) | section 6 (the packer is the shared service behind implicit and explicit repacking) |
| UTMT-FNT-01 | Font properties | [editors/fonts.md](editors/fonts.md) | section 3 |
| UTMT-FNT-02 | Glyph table | [editors/fonts.md](editors/fonts.md) | section 4 |
| UTMT-FNT-03 | Glyph rectangle editor | [editors/fonts.md](editors/fonts.md) | section 4, item 2 |
| UTMT-FNT-04 | Kerning editor | [editors/fonts.md](editors/fonts.md) | section 4, item 4 |
| UTMT-FNT-05 | Glyph maintenance | [editors/fonts.md](editors/fonts.md) | section 4, item 3 |
| UTMT-SND-01 | Sound properties | [editors/sounds.md](editors/sounds.md) | section 3.1 |
| UTMT-SND-02 | Sound playback | [editors/sounds.md](editors/sounds.md) | section 4 |
| UTMT-SND-03 | Audio file reference control | [editors/sounds.md](editors/sounds.md) | section 3.2 |
| UTMT-SND-04 | Embedded audio editor | [editors/sounds.md](editors/sounds.md) | section 7 |
| UTMT-SND-05 | Audio group editor | [editors/sounds.md](editors/sounds.md) | section 6.1 |
| UTMT-SND-06 | Audio group files | [editors/sounds.md](editors/sounds.md) | section 6.2 |
| UTMT-OBJ-01 | Object properties | [editors/objects.md](editors/objects.md) | section 3 |
| UTMT-OBJ-02 | Physics properties | [editors/objects.md](editors/objects.md) | section 4 |
| UTMT-OBJ-03 | Physics vertices | [editors/objects.md](editors/objects.md) | section 4 |
| UTMT-OBJ-04 | Event list | [editors/objects.md](editors/objects.md) | section 5 |
| UTMT-OBJ-05 | Event actions | [editors/objects.md](editors/objects.md) | section 5 |
| UTMT-OBJ-06 | Event subtype pickers | [editors/objects.md](editors/objects.md) | section 5 |
| UTMT-ROOM-01 | Room properties | [editors/rooms.md](editors/rooms.md) | section 5 |
| UTMT-ROOM-02 | Room canvas render | [editors/rooms.md](editors/rooms.md) | section 3 (items 3) |
| UTMT-ROOM-03 | Room object tree | [editors/rooms.md](editors/rooms.md) | section 1 and the layers panel (section 4) |
| UTMT-ROOM-04 | GMS1 backgrounds | [editors/rooms.md](editors/rooms.md) | sections 4.2 and 5 |
| UTMT-ROOM-05 | Views | [editors/rooms.md](editors/rooms.md) | sections 4.2 and 5 |
| UTMT-ROOM-06 | Instance properties | [editors/rooms.md](editors/rooms.md) | section 4.2 |
| UTMT-ROOM-07 | Tile properties | [editors/rooms.md](editors/rooms.md) | section 4.2 |
| UTMT-ROOM-08 | Layer properties | [editors/rooms.md](editors/rooms.md) | section 4.2 |
| UTMT-ROOM-09 | Sprite instances | [editors/rooms.md](editors/rooms.md) | section 4.2 |
| UTMT-ROOM-10 | Particle system instances | [editors/particle-systems.md](editors/particle-systems.md)<br>[editors/rooms.md](editors/rooms.md) | room editor<br>section 4.2 |
| UTMT-ROOM-11 | Add items | [editors/rooms.md](editors/rooms.md) | section 4.2 |
| UTMT-ROOM-12 | Select and drag | [editors/rooms.md](editors/rooms.md) | section 3 and section 11 |
| UTMT-ROOM-13 | Paint copies | [editors/rooms.md](editors/rooms.md) | section 3 and section 11 |
| UTMT-ROOM-14 | Drop from resource tree | [editors/rooms.md](editors/rooms.md) | section 3 and section 11 |
| UTMT-ROOM-15 | Copy and paste | [editors/rooms.md](editors/rooms.md) | section 3 and section 11 |
| UTMT-ROOM-16 | Undo move | [editors/rooms.md](editors/rooms.md) | section 3 and section 11 |
| UTMT-ROOM-17 | Delete and reorder | [editors/rooms.md](editors/rooms.md) | section 4.2 |
| UTMT-ROOM-18 | Flip instance | [editors/rooms.md](editors/rooms.md) | section 3 and section 11 |
| UTMT-ROOM-19 | Zoom and scroll | [editors/rooms.md](editors/rooms.md) | section 3 and section 11 |
| UTMT-ROOM-20 | Grid overlay | [editors/rooms.md](editors/rooms.md) | section 3 and section 11 |
| UTMT-ROOM-21 | Export room as PNG | [editors/rooms.md](editors/rooms.md) | section 3 and section 11 |
| UTMT-ROOM-22 | Tile data CSV | [editors/rooms.md](editors/rooms.md) | section 4.1 |
| UTMT-ROOM-23 | Auto size tile layer | [editors/rooms.md](editors/rooms.md) | section 4.1 |
| UTMT-ROOM-24 | Instance creation order | [editors/rooms.md](editors/rooms.md) | section 6 |
| UTMT-ROOM-25 | Path layers | [editors/paths.md](editors/paths.md)<br>[editors/rooms.md](editors/rooms.md) | the room editor opens this editor for path layers<br>section 3 (items 3) |
| UTMT-ROOM-26 | Sequence instances | [editors/rooms.md](editors/rooms.md) | section 4.2 |
| UTMT-TILE-01 | Tile palette | [editors/rooms.md](editors/rooms.md) | section 7 |
| UTMT-TILE-02 | Paint and line | [editors/rooms.md](editors/rooms.md) | section 7 |
| UTMT-TILE-03 | Fill | [editors/rooms.md](editors/rooms.md) | section 7 |
| UTMT-TILE-04 | Pick | [editors/rooms.md](editors/rooms.md) | section 7 |
| UTMT-TILE-05 | Brush transforms | [editors/rooms.md](editors/rooms.md) | section 7 |
| UTMT-TILE-06 | View toggles | [editors/rooms.md](editors/rooms.md) | section 7 |
| UTMT-TILE-07 | Undo and redo | [editors/rooms.md](editors/rooms.md) | section 7 |
| UTMT-TILE-08 | Apply and cancel | [editors/rooms.md](editors/rooms.md) | section 7 |
| UTMT-AST-01 | Shader editor | [editors/shaders.md](editors/shaders.md) | sections 2 and 3 |
| UTMT-AST-02 | Path editor | [editors/paths.md](editors/paths.md) | sections 2 to 4 |
| UTMT-AST-03 | Timeline editor | [editors/timelines.md](editors/timelines.md) | sections 2 and 3 |
| UTMT-AST-04 | Extension editor | [editors/extensions.md](editors/extensions.md) | sections 2 and 3 |
| UTMT-AST-05 | Extension file editor | [editors/extensions.md](editors/extensions.md) | section 4 |
| UTMT-AST-06 | Extension function editor | [editors/extensions.md](editors/extensions.md) | section 5 |
| UTMT-AST-07 | String editor | [editors/strings.md](editors/strings.md) | sections 2 and 3 |
| UTMT-AST-08 | Shared string edits | [editors/languages.md](editors/languages.md)<br>[editors/strings.md](editors/strings.md) | section 2<br>section 4 |
| UTMT-AST-09 | Variable editor | [editors/variables-functions.md](editors/variables-functions.md) | section 3 |
| UTMT-AST-10 | Variables chunk header | [editors/variables-functions.md](editors/variables-functions.md) | section 3.1 |
| UTMT-AST-11 | Function editor | [editors/variables-functions.md](editors/variables-functions.md) | section 4 |
| UTMT-AST-12 | Code locals editor | [editors/variables-functions.md](editors/variables-functions.md) | section 5 |
| UTMT-AST-13 | Global init scripts | [editors/global-scripts.md](editors/global-scripts.md) | section 3 |
| UTMT-AST-14 | Game End scripts | [editors/global-scripts.md](editors/global-scripts.md) | section 4 |
| UTMT-AST-15 | Particle system editor | [editors/particle-systems.md](editors/particle-systems.md) | section 3 |
| UTMT-AST-16 | Particle emitter editor | [editors/particle-systems.md](editors/particle-systems.md) | section 4 |
| UTMT-GEN-01 | General info fields | [editors/general-info.md](editors/general-info.md) | sections 3.1 and 3.3 |
| UTMT-GEN-02 | Info flags | [editors/general-info.md](editors/general-info.md) | sections 3.1 and 3.3 |
| UTMT-GEN-03 | Active targets and function classifications | [editors/general-info.md](editors/general-info.md) | sections 3.1 and 3.3 |
| UTMT-GEN-04 | Room order | [editors/general-info.md](editors/general-info.md) | section 3.5 |
| UTMT-GEN-05 | Debugger checkbox guard | [editors/general-info.md](editors/general-info.md) | sections 3.1 and 3.3 |
| UTMT-GEN-06 | GMS2 extras | [editors/general-info.md](editors/general-info.md) | sections 3.1 and 3.3 |
| UTMT-GEN-07 | Options (OPTN) | [editors/general-info.md](editors/general-info.md) | sections 3.2 and 3.4 |
| UTMT-GEN-08 | Language (LANG) | [editors/general-info.md](editors/general-info.md)<br>[editors/languages.md](editors/languages.md) | section 3.6<br>section 3.1 (read-only fields kept) and 3.2, 3.3 (entries, which UTMT lacks) |
| UTMT-NOED-01 | Sequences (SEQN) | [editors/sequences.md](editors/sequences.md) | sections 3 to 6 (tracks, keyframes, moments, broadcast messages) |
| UTMT-NOED-02 | Animation curves (ACRV) | [editors/animation-curves.md](editors/animation-curves.md) | sections 2 to 4 (channels and points) |
| UTMT-NOED-03 | Tags (TAGS) | [editors/tags-feature-flags.md](editors/tags-feature-flags.md) | section 2 |
| UTMT-NOED-04 | Feature flags (FEAT) | [editors/tags-feature-flags.md](editors/tags-feature-flags.md) | section 3 |
| UTMT-NOED-05 | Filter effects (FEDS) | [editors/filter-effects.md](editors/filter-effects.md) | section 2, 3, 4 |
| UTMT-NOED-06 | UI layers (UILR) | [editors/rooms.md](editors/rooms.md) | section 8 |
| UTMT-NOED-07 | Data files (DAFL) | [editors/tags-feature-flags.md](editors/tags-feature-flags.md) | section 4 |
| UTMT-NOED-08 | Room text items and nine-slices | [editors/rooms.md](editors/rooms.md) | section 4.2 (text items and nine-slice room items) |
| UTMT-NOED-09 | Effect layer properties | [editors/filter-effects.md](editors/filter-effects.md)<br>[editors/rooms.md](editors/rooms.md) | section 6, and rooms.md 4.2<br>section 4.1 (effect layers) |
| UTMT-CTRL-01 | Asset reference field | [editors/backgrounds-tilesets.md](editors/backgrounds-tilesets.md)<br>[editors/embedded-images.md](editors/embedded-images.md)<br>[editors/global-scripts.md](editors/global-scripts.md)<br>[editors/objects.md](editors/objects.md)<br>[shell.md](shell.md)<br>[ux-standards.md](ux-standards.md) | section 3, 4<br>section 2, 3<br>section 2, 3<br>section 3<br>ux-standards section 11<br>section 11 (shared controls) |
| UTMT-CTRL-02 | String reference field | [editors/embedded-images.md](editors/embedded-images.md)<br>[editors/filter-effects.md](editors/filter-effects.md)<br>[editors/languages.md](editors/languages.md)<br>[editors/strings.md](editors/strings.md)<br>[editors/tags-feature-flags.md](editors/tags-feature-flags.md)<br>[editors/variables-functions.md](editors/variables-functions.md)<br>[shell.md](shell.md)<br>[ux-standards.md](ux-standards.md) | section 2, 3<br>section 3, 4<br>section 2, 4<br>section 4 and the String reference field in ux-standards section 11<br>section 2, 3<br>section 3, 4, 5<br>ux-standards section 11<br>section 11 (shared controls) |
| UTMT-CTRL-03 | Texture page item display | [editors/backgrounds-tilesets.md](editors/backgrounds-tilesets.md)<br>[editors/embedded-images.md](editors/embedded-images.md)<br>[editors/sprites.md](editors/sprites.md)<br>[editors/texture-pages.md](editors/texture-pages.md)<br>[shell.md](shell.md)<br>[ux-standards.md](ux-standards.md) | section 3, 4<br>section 2, 3<br>section 4.2 (texture page item display on a checkerboard)<br>section 3<br>ux-standards section 11<br>section 11 (shared controls) |
| UTMT-CTRL-04 | Flags box | [shell.md](shell.md)<br>[ux-standards.md](ux-standards.md) | ux-standards section 11<br>section 11 (shared controls) |
| UTMT-CTRL-05 | Color picker | [editors/filter-effects.md](editors/filter-effects.md)<br>[shell.md](shell.md)<br>[ux-standards.md](ux-standards.md) | section 3, 4<br>ux-standards section 11<br>section 11 (shared controls) |
| UTMT-CTRL-06 | Editable list grids | [editors/backgrounds-tilesets.md](editors/backgrounds-tilesets.md)<br>[editors/global-scripts.md](editors/global-scripts.md)<br>[editors/tags-feature-flags.md](editors/tags-feature-flags.md)<br>[editors/variables-functions.md](editors/variables-functions.md)<br>[shell.md](shell.md)<br>[ux-standards.md](ux-standards.md) | section 3, 4<br>section 2, 3<br>section 2, 3<br>section 5<br>ux-standards section 11<br>section 11 (shared controls) |
| UTMT-SCRIPT-01 | Scripts menu | [scripting.md](scripting.md) | section 2, 3.1 |
| UTMT-SCRIPT-02 | Run other script | [scripting.md](scripting.md) | section 3.1 |
| UTMT-SCRIPT-03 | C# script engine | [scripting.md](scripting.md) | section 3.2 |
| UTMT-SCRIPT-04 | Script error reporting | [scripting.md](scripting.md) | section 3.3 |
| UTMT-SCRIPT-05 | Nested scripts and linting | [scripting.md](scripting.md) | section 3.5 |
| UTMT-SCRIPT-06 | Finished message | [scripting.md](scripting.md) | section 3.6 |
| UTMT-SCRIPT-07 | Script helpers | [scripting.md](scripting.md) | section 3.7 |
| UTMT-API-01 | State accessors | [scripting.md](scripting.md) | section 5.2 |
| UTMT-API-02 | Data helpers | [scripting.md](scripting.md) | section 5.2 |
| UTMT-API-03 | Message dialogs | [scripting.md](scripting.md) | section 5.2, 7.1 |
| UTMT-API-04 | Input and output dialogs | [scripting.md](scripting.md) | section 5.2, 7.2, 7.3 |
| UTMT-API-05 | Clickable search output | [scripting.md](scripting.md) | section 5.2, 7.4 |
| UTMT-API-06 | Progress API | [scripting.md](scripting.md) | section 5.2, 7.5 |
| UTMT-API-07 | File prompts | [scripting.md](scripting.md) | section 5.2, 7.6 |
| UTMT-API-08 | Code text | [editors/code.md](editors/code.md)<br>[scripting.md](scripting.md) | section 7 (text of code by name is available to scripts through the same decompile service)<br>UTMT-API-08 (code text) is covered in  section 7. UTMT-PLAT-04 (scripts shipped beside the program) is in  section 5.  |
| UTMT-API-09 | Navigation | [scripting.md](scripting.md) | section 5.2 |
| UTMT-API-10 | Removed members | [scripting.md](scripting.md) | section 5.2, 5.3 |
| UTMT-CLI-01 | `new` | G3MTool CLI/GUI (not duplicated) | covered by G3MTool: blank data file: File > New > New Data File in the GUI ([shell.md](shell.md) section 2); scripted use is G3MTool |
| UTMT-CLI-02 | `load` | G3MTool CLI/GUI (not duplicated) | covered by G3MTool: `execute` runs scripts; in the window use Scripts > Run Script... and the Script Console ([scripting.md](scripting.md) section 4) |
| UTMT-CLI-03 | `load -i` interactive menu | G3MTool CLI/GUI (not duplicated) | covered by G3MTool: interactive terminal menu; G3MTool CLI only |
| UTMT-CLI-04 | `info` | G3MTool CLI/GUI (not duplicated) | covered by G3MTool: `info`; in the window use Mod > Inspect File... ([editors/patches.md](editors/patches.md) section 4.5) |
| UTMT-CLI-05 | `dump` | G3MTool CLI/GUI (not duplicated) | covered by G3MTool: scripted dump is G3MTool; in the window use Tools > Export All... |
| UTMT-CLI-06 | `replace` | G3MTool CLI/GUI (not duplicated) | covered by G3MTool: scripted replace is G3MTool; in the window use the code and texture import commands |
| UTMT-CLI-07 | `project build` | G3MTool CLI/GUI (not duplicated) | covered by G3MTool: headless project build is G3MTool; in the window use Project > Build ([projects.md](projects.md)) |
| UTMT-CLI-08 | Headless script interface | G3MTool CLI/GUI (not duplicated) | covered by G3MTool: headless script interface is G3MTool `execute` |
| UTMT-PLAT-01 | File associations | [onboarding.md](onboarding.md)<br>[platforms-formats.md](platforms-formats.md) | listed in the page header<br>UTMT-PLAT-01 (file associations) is in  section 7. UTMT-FMT-04 (GMS1 and GMS2 split), UTMT-FMT-07 (audio formats) and U |
| UTMT-PLAT-02 | GUI platform | [platforms-formats.md](platforms-formats.md) | section 3 |
| UTMT-PLAT-03 | In-app updater | [platforms-formats.md](platforms-formats.md) | section 4 |
| UTMT-PLAT-04 | Scripts shipped beside the exe | [platforms-formats.md](platforms-formats.md)<br>[scripting.md](scripting.md) | section 5<br>UTMT-API-08 (code text) is covered in  section 7. UTMT-PLAT-04 (scripts shipped beside the program) is in  section 5.  |
| UTMT-PLAT-05 | Multiple instances | [platforms-formats.md](platforms-formats.md)<br>[shell.md](shell.md) | section 6<br>section 13 (several workspaces in several windows, one process) |
| UTMT-KEY-01 | Global shortcuts | [editors/keyboard-shortcuts.md](editors/keyboard-shortcuts.md)<br>[shell.md](shell.md) | section 5 (UTMT preset) and the default map in shell.md section 16<br>section 16 |
| UTMT-KEY-02 | Tab shortcuts | [editors/keyboard-shortcuts.md](editors/keyboard-shortcuts.md)<br>[shell.md](shell.md) | section 5 (UTMT preset) and the default map in shell.md section 16<br>section 16 |
| UTMT-KEY-03 | Tree shortcuts | [editors/keyboard-shortcuts.md](editors/keyboard-shortcuts.md)<br>[shell.md](shell.md) | section 5 (UTMT preset) and the default map in shell.md section 16<br>section 16 |
| UTMT-KEY-04 | Code editor shortcuts | [editors/code.md](editors/code.md)<br>[editors/keyboard-shortcuts.md](editors/keyboard-shortcuts.md) | section 5<br>section 5 (UTMT preset) and the default map in shell.md section 16 |
| UTMT-KEY-05 | Room editor shortcuts | [editors/keyboard-shortcuts.md](editors/keyboard-shortcuts.md)<br>[editors/rooms.md](editors/rooms.md) | section 5 (UTMT preset) and the default map in shell.md section 16<br>section 9 and the keyboard map |
| UTMT-KEY-06 | Tile editor shortcuts | [editors/keyboard-shortcuts.md](editors/keyboard-shortcuts.md)<br>[editors/rooms.md](editors/rooms.md) | section 5 (UTMT preset) and the default map in shell.md section 16<br>section 9 and the keyboard map |
| UTMT-FMT-01 | Data file containers | [platforms-formats.md](platforms-formats.md) | section 7.1 |
| UTMT-FMT-02 | Chunk coverage | [editors/chunk-viewer.md](editors/chunk-viewer.md)<br>[platforms-formats.md](platforms-formats.md) | sections 3 and 4 (unknown chunk passthrough and chunk bytes)<br>section 7.2 |
| UTMT-FMT-03 | Bytecode versions | [platforms-formats.md](platforms-formats.md) | section 7.3 |
| UTMT-FMT-04 | GMS1 and GMS2 split | [platforms-formats.md](platforms-formats.md) | UTMT-PLAT-01 (file associations) is in  section 7. UTMT-FMT-04 (GMS1 and GMS2 split), UTMT-FMT-07 (audio formats) and U |
| UTMT-FMT-05 | Version detection | [platforms-formats.md](platforms-formats.md) | section 7.4 |
| UTMT-FMT-06 | YYC games | [platforms-formats.md](platforms-formats.md) | section 7.5 |
| UTMT-FMT-07 | Audio formats | [editors/sounds.md](editors/sounds.md)<br>[platforms-formats.md](platforms-formats.md) | sections 5 and 6.3 (audio formats)<br>UTMT-PLAT-01 (file associations) is in  section 7. UTMT-FMT-04 (GMS1 and GMS2 split), UTMT-FMT-07 (audio formats) and U |
| UTMT-FMT-08 | Texture formats | [editors/texture-pages.md](editors/texture-pages.md)<br>[platforms-formats.md](platforms-formats.md) | sections 4 and 11 (texture formats)<br>UTMT-PLAT-01 (file associations) is in  section 7. UTMT-FMT-04 (GMS1 and GMS2 split), UTMT-FMT-07 (audio formats) and U |
| UTMT-FMT-09 | Debug data format | [platforms-formats.md](platforms-formats.md) | section 9.1 |
| UTMT-FMT-10 | Write integrity checks | [platforms-formats.md](platforms-formats.md) | section 8 |
| UTMT-FMT-11 | Pointer offset map | [editors/chunk-viewer.md](editors/chunk-viewer.md)<br>[platforms-formats.md](platforms-formats.md) | section 2 (offsets for every structured row)<br>section 9.2 |

## G3MTool CLI and GUI (TOOL-*)

| ID | Feature | Spec | Where |
| --- | --- | --- | --- |
| TOOL-01 | Interactive prompt | [editors/patches.md](editors/patches.md) | section 10 |
| TOOL-02 | Verbose output | [editors/patches.md](editors/patches.md) | section 10 |
| TOOL-03 | Log file | [editors/patches.md](editors/patches.md) | section 10 |
| TOOL-04 | JSON output | [editors/patches.md](editors/patches.md) | section 10 |
| TOOL-05 | Version | [editors/patches.md](editors/patches.md) | section 10 |
| TOOL-06 | Custom xdelta binary | [editors/patches.md](editors/patches.md) | section 10 |
| TOOL-07 | patch create | [editors/patches.md](editors/patches.md) | section 4.1 |
| TOOL-08 | Create xdelta instead | [editors/patches.md](editors/patches.md) | section 4.1 |
| TOOL-09 | Embedded xdelta fallback | [editors/patches.md](editors/patches.md) | section 4.1 |
| TOOL-10 | Analysis cache | [editors/patches.md](editors/patches.md) | section 4.1 |
| TOOL-11 | patch apply | [editors/patches.md](editors/patches.md) | section 4.2 |
| TOOL-12 | Apply fallback order | [editors/patches.md](editors/patches.md) | section 4.2 |
| TOOL-13 | patch validate | [editors/patches.md](editors/patches.md) | section 4.3 |
| TOOL-14 | patch merge | [editors/patches.md](editors/patches.md) | section 4.4 |
| TOOL-15 | Merge outputs | [editors/patches.md](editors/patches.md) | section 4.4 |
| TOOL-16 | Code merge | [editors/patches.md](editors/patches.md) | section 4.4 |
| TOOL-17 | Property merge | [editors/patches.md](editors/patches.md) | section 4.4 |
| TOOL-18 | Merge report | [editors/patches.md](editors/patches.md) | section 4.4 |
| TOOL-19 | Sequential low-memory merge | [editors/patches.md](editors/patches.md) | section 4.4 |
| TOOL-20 | patch batch apply | [editors/patches.md](editors/patches.md) | section 5 |
| TOOL-21 | patch batch create | [editors/patches.md](editors/patches.md) | section 5 |
| TOOL-22 | patch batch merge | [editors/patches.md](editors/patches.md) | section 5 |
| TOOL-23 | Batch deduplication | [editors/patches.md](editors/patches.md) | section 5 |
| TOOL-24 | diff | [editors/data-diff.md](editors/data-diff.md)<br>[editors/patches.md](editors/patches.md) | section 4<br>section 4.5 and data-diff.md |
| TOOL-25 | Full diff | [editors/data-diff.md](editors/data-diff.md)<br>[editors/patches.md](editors/patches.md) | section 4<br>section 4.5 and data-diff.md |
| TOOL-26 | diff JSON | [editors/data-diff.md](editors/data-diff.md)<br>[editors/patches.md](editors/patches.md) | section 4<br>section 4.5 and data-diff.md |
| TOOL-27 | info | [editors/patches.md](editors/patches.md) | section 4.5 and data-diff.md |
| TOOL-28 | Verbose info | [editors/patches.md](editors/patches.md) | section 4.5 and data-diff.md |
| TOOL-29 | info JSON and cache | [editors/patches.md](editors/patches.md) | section 4.5 and data-diff.md |
| TOOL-30 | xpatch create | [editors/patches.md](editors/patches.md) | section 4.6 |
| TOOL-31 | xpatch apply | [editors/patches.md](editors/patches.md) | section 4.6 |
| TOOL-32 | Run external program | [editors/patches.md](editors/patches.md) | section 4.7 |
| TOOL-33 | Run CSX script | [editors/patches.md](editors/patches.md) | section 4.7 |
| TOOL-34 | Run xdelta | [editors/patches.md](editors/patches.md) | section 4.6 |
| TOOL-35 | CSX host API | [scripting.md](scripting.md) | section 5.2 |
| TOOL-36 | Bundled scripts (G3MTool only; none in G3MStudio) | [scripting.md](scripting.md) | section 6 |
| TOOL-37 | Data-file formats | [editors/patches.md](editors/patches.md) | sections 4 and 9 |
| TOOL-38 | Console streams | [editors/patches.md](editors/patches.md) | section 9 |
| TOOL-39 | Exit codes | [editors/patches.md](editors/patches.md) | section 9 |
| TOOL-40 | Validated data writes | [editors/patches.md](editors/patches.md) | sections 4 and 9 |
| TOOL-41 | Game-specific data | [editors/patches.md](editors/patches.md) | section 9 |
| TOOL-42 | Operation selector | [editors/patches.md](editors/patches.md) | sections 2 and 3 |
| TOOL-43 | Path inputs | [editors/patches.md](editors/patches.md) | sections 2 and 3 |
| TOOL-44 | Batch mode | [editors/patches.md](editors/patches.md) | section 5 |
| TOOL-45 | XDelta options | [editors/patches.md](editors/patches.md) | section 4.6 |
| TOOL-46 | Continue after errors | [editors/patches.md](editors/patches.md) | section 5 |
| TOOL-47 | Merge options | [editors/patches.md](editors/patches.md) | section 4.4 |
| TOOL-48 | Detailed report | [editors/patches.md](editors/patches.md) | section 4.5 and data-diff.md |
| TOOL-49 | Cache folder | [editors/patches.md](editors/patches.md) | sections 2 and 3 |
| TOOL-50 | Default output extension | [editors/patches.md](editors/patches.md) | sections 2 and 3 |
| TOOL-51 | Apply through g3mpatch | [editors/patches.md](editors/patches.md) | section 4.2 |
| TOOL-52 | Inspect | [editors/patches.md](editors/patches.md) | section 4.5 and data-diff.md |
| TOOL-53 | Activity panel | [editors/patches.md](editors/patches.md)<br>[shell.md](shell.md) | section 6<br>section 7.6 (Jobs) and patches.md section 6 |
| TOOL-54 | Interactive scripts | [editors/patches.md](editors/patches.md)<br>[scripting.md](scripting.md) | section 4.7<br>section 7 |
| TOOL-55 | Job lifecycle | [editors/patches.md](editors/patches.md)<br>[shell.md](shell.md) | section 6<br>section 7.6 (Jobs) and patches.md section 6 |
| TOOL-56 | Form reset | [editors/patches.md](editors/patches.md) | sections 2 and 3 |
| TOOL-57 | Safe output replace | [editors/patches.md](editors/patches.md) | sections 4 and 9 |
| TOOL-58 | Community links | [shell.md](shell.md) | section 14.5 |

## G3M-MCE mod editor (MCE-*)

| ID | Feature | Spec | Where |
| --- | --- | --- | --- |
| MCE-01 | Start page | [editors/mod-packaging.md](editors/mod-packaging.md) | sections 1 and 2 |
| MCE-02 | Open G3M ZIP | [editors/mod-packaging.md](editors/mod-packaging.md) | sections 1 and 2 |
| MCE-03 | Open config alone | [editors/mod-packaging.md](editors/mod-packaging.md) | sections 1 and 2 |
| MCE-04 | Open plain ZIP or file | [editors/mod-packaging.md](editors/mod-packaging.md) | sections 1 and 2 |
| MCE-05 | Deltamod import | [editors/mod-packaging.md](editors/mod-packaging.md) | section 2 |
| MCE-06 | Legacy config migration | [editors/mod-packaging.md](editors/mod-packaging.md) | section 2 |
| MCE-07 | Metadata tab | [editors/mod-packaging.md](editors/mod-packaging.md) | section 4 |
| MCE-08 | Tags | [editors/mod-packaging.md](editors/mod-packaging.md) | section 4 |
| MCE-09 | Compatibility tab | [editors/mod-packaging.md](editors/mod-packaging.md) | section 5 |
| MCE-10 | Files tab operations | [editors/mod-packaging.md](editors/mod-packaging.md) | section 6 |
| MCE-11 | Groups | [editors/mod-packaging.md](editors/mod-packaging.md) | section 6 |
| MCE-12 | Reorder | [editors/mod-packaging.md](editors/mod-packaging.md) | section 6 |
| MCE-13 | Bundled files tree | [editors/mod-packaging.md](editors/mod-packaging.md) | section 6 |
| MCE-14 | Drop targets | [editors/mod-packaging.md](editors/mod-packaging.md) | section 6 |
| MCE-15 | Browse bundled ZIP | [editors/mod-packaging.md](editors/mod-packaging.md) | section 6 |
| MCE-16 | Hash calculation | [editors/mod-packaging.md](editors/mod-packaging.md) | section 6 |
| MCE-17 | Placeholders tab | [editors/mod-packaging.md](editors/mod-packaging.md) | section 7 |
| MCE-18 | Help tab | [editors/mod-packaging.md](editors/mod-packaging.md) | section 8 |
| MCE-19 | Config preview | [editors/mod-packaging.md](editors/mod-packaging.md) | section 8 |
| MCE-20 | Validation UI | [editors/mod-packaging.md](editors/mod-packaging.md) | sections 3 and 9 |
| MCE-21 | Package checks | [editors/mod-packaging.md](editors/mod-packaging.md) | sections 3 and 9 |
| MCE-22 | Package limits | [editors/mod-packaging.md](editors/mod-packaging.md) | sections 3 and 9 |
| MCE-23 | Save ZIP | [editors/mod-packaging.md](editors/mod-packaging.md) | section 10 |
| MCE-24 | Export to G3M | [editors/mod-packaging.md](editors/mod-packaging.md) | section 10 |
| MCE-25 | Unsaved changes guard | [editors/mod-packaging.md](editors/mod-packaging.md) | sections 3 and 9 |
| MCE-26 | Localization | [editors/mod-packaging.md](editors/mod-packaging.md) | localization follows ux-standards section 13 (all seven languages) |

## G3M modder features (G3M-*)

| ID | Feature | Spec | Where |
| --- | --- | --- | --- |
| G3M-01 | mod_config.json validation | [editors/mod-packaging.md](editors/mod-packaging.md) | sections 3 and 9 |
| G3M-02 | Legacy config migration | [editors/mod-packaging.md](editors/mod-packaging.md) | section 2 |
| G3M-03 | Deltamod conversion | [editors/mod-packaging.md](editors/mod-packaging.md) | section 2 |
| G3M-04 | PizzaOven and CYOP/AFOM import | [editors/mod-packaging.md](editors/mod-packaging.md) | section 2 |
| G3M-05 | Manual installation | [editors/mod-packaging.md](editors/mod-packaging.md) | section 2 |
| G3M-06 | Import with versions | [editors/mod-packaging.md](editors/mod-packaging.md) | section 2 |
| G3M-07 | Duplicate ID handling | [editors/mod-packaging.md](editors/mod-packaging.md) | section 2 |
| G3M-08 | Unknown game | [editors/mod-packaging.md](editors/mod-packaging.md) | section 2 |
| G3M-09 | Desktop mod editor | [editors/mod-packaging.md](editors/mod-packaging.md) | section 4 |
| G3M-10 | Export mod ZIP | [editors/mod-packaging.md](editors/mod-packaging.md) | section 10 |
| G3M-11 | Mod Versions | [editors/mod-packaging.md](editors/mod-packaging.md) | section 11 |
| G3M-12 | Create Modpack | [editors/mod-packaging.md](editors/mod-packaging.md) | section 12 |
| G3M-13 | Priority and steps | [editors/mod-packaging.md](editors/mod-packaging.md)<br>[run-game.md](run-game.md) | section 12<br>section 5.1 |
| G3M-14 | Relationship checks at launch | [run-game.md](run-game.md) | section 5.1 |
| G3M-15 | Launch modes | [run-game.md](run-game.md) | section 5.2 |
| G3M-16 | Chapter Mode | [run-game.md](run-game.md) | section 5.1 |
| G3M-17 | Runtime target rewrite | [editors/mod-packaging.md](editors/mod-packaging.md)<br>[run-game.md](run-game.md) | section 6, item 7<br>section 5.1 |
| G3M-18 | Backup, restore, rollback | [run-game.md](run-game.md) | section 5.3 |
| G3M-19 | Profiles | [editors/launch-profiles.md](editors/launch-profiles.md) | section 2 |
| G3M-20 | Desktop shortcuts | [editors/launch-profiles.md](editors/launch-profiles.md) | section 2 |
| G3M-21 | Mod Diagnostics | [editors/launch-profiles.md](editors/launch-profiles.md) | section 3 |
| G3M-22 | Analyze Actual Launch Result | [run-game.md](run-game.md) | section 7.3 |
| G3M-23 | Convert DATA Files | [editors/patches.md](editors/patches.md) | section 7 |
| G3M-24 | Patch tool | [editors/patches.md](editors/patches.md) | section 7 |
| G3M-25 | Merge tool | [editors/patches.md](editors/patches.md) | section 7 |
| G3M-26 | Info and Diff tools | [editors/data-diff.md](editors/data-diff.md)<br>[editors/patches.md](editors/patches.md) | section 4 and patches.md section 8<br>section 7 |
| G3M-27 | Tool settings | [editors/patches.md](editors/patches.md) | section 7 |
| G3M-28 | Logs | [editors/launch-profiles.md](editors/launch-profiles.md) | section 4 |
| G3M-29 | Patching warnings | [run-game.md](run-game.md) | section 5.3 |
| G3M-30 | Support Packager | [editors/mod-packaging.md](editors/mod-packaging.md) | section 14 |
| G3M-31 | One-click install | [editors/mod-packaging.md](editors/mod-packaging.md) | section 13 |
| G3M-32 | GameBanana Remote Install | [editors/mod-packaging.md](editors/mod-packaging.md) | section 13 |
| G3M-33 | Game Versions | [editors/launch-profiles.md](editors/launch-profiles.md) | section 5 |
| G3M-34 | DR Save Manager plugin | [editors/plugins-panel.md](editors/plugins-panel.md) | section 7, item 1 |
| G3M-35 | Custom Save Folders plugin | [editors/plugins-panel.md](editors/plugins-panel.md) | section 7, item 2 |
| G3M-36 | Game detection | [editors/launch-profiles.md](editors/launch-profiles.md)<br>[onboarding.md](onboarding.md) | section 6<br>listed in the page header |
| G3M-37 | GameBanana compatibility label | [editors/mod-packaging.md](editors/mod-packaging.md) | section 13 |

## Requirement coverage (F-* and N-*)

Every functional and non-functional requirement ID of [../requirements.md](../requirements.md) is mapped to the spec file and section that covers it. IDs without a home: 0 (79 IDs).

| ID | Spec | Where |
| --- | --- | --- |
| F-PAR-1 | [coverage.md](coverage.md), [parity-coverage.md](parity-coverage.md) | the UTMT table above and every spec's Parity coverage section |
| F-PAR-2 | [scripting.md](scripting.md) | sections 3 and 5 (runner and Script API) |
| F-PAR-3 | [scripting.md](scripting.md)<br>[script-features.md](script-features.md) | section 6 (no bundled scripts)<br>sections 4 and 5 (built-in features, plugin candidates) |
| F-WS-1 | [shell.md](shell.md) | section 14.1 (Open) |
| F-WS-2 | [shell.md](shell.md) | section 14.1, item 5 (Restore) |
| F-WS-3 | [shell.md](shell.md) | section 15 (Start page) |
| F-WS-4 | [shell.md](shell.md)<br>[platforms-formats.md](platforms-formats.md) | section 14.2 (Save and hot exit)<br>section 8 |
| F-WS-5 | [shell.md](shell.md)<br>[platforms-formats.md](platforms-formats.md) | section 14.2 (Save)<br>section 8 (writing and integrity) |
| F-WS-6 | [editors/data-diff.md](editors/data-diff.md) | section 5 (Local History) |
| F-WS-7 | [projects.md](projects.md) | sections 3 and 5 |
| F-WS-8 | [shell.md](shell.md) | section 14.3 (External changes) |
| F-WS-9 | [shell.md](shell.md) | section 13 (Multi-window) |
| F-WS-10 | [projects.md](projects.md) | section 10 (UTMT conversions) |
| F-WS-11 | [projects.md](projects.md) | section 3.7 (GameMaker project interop) |
| F-UI-1 | [shell.md](shell.md) | section 10 (Docking) |
| F-UI-2 | [shell.md](shell.md) | section 9 (Tabs and the editor area) |
| F-UI-3 | [shell.md](shell.md) | section 3.1 and 3.2 (Registry, Command palette) |
| F-UI-4 | [shell.md](shell.md) | section 3.3 (Quick open) |
| F-UI-5 | [editors/keyboard-shortcuts.md](editors/keyboard-shortcuts.md)<br>[shell.md](shell.md) | the whole page<br>section 16 (Keyboard map) |
| F-UI-6 | [settings.md](settings.md) | sections 2, 3 and 5 |
| F-UI-7 | [shell.md](shell.md) | section 4 (Status bar) |
| F-UI-8 | [shell.md](shell.md) | sections 7.1, 7.2 and 8 (Problems, Output, Notifications) |
| F-UI-9 | [shell.md](shell.md) | section 11 (Layout presets) |
| F-UI-10 | [shell.md](shell.md) | sections 12 and 13 (Canvas workspace, Multi-window) |
| F-RES-1 | [shell.md](shell.md) | section 5 (Explorer) |
| F-RES-2 | [editors/](editors/) | one spec per resource type; the list is in [shell.md](shell.md) section 5.2 |
| F-RES-3 | [editors/code.md](editors/code.md) | sections 3 to 6 and 10 |
| F-RES-4 | [editors/code.md](editors/code.md)<br>[shell.md](shell.md) | section 9 (Search and replace)<br>sections 7.3 and 7.7 |
| F-RES-5 | [editors/sprites.md](editors/sprites.md)<br>[editors/texture-pages.md](editors/texture-pages.md) | sections 3 to 5<br>sections 2 to 6 |
| F-RES-6 | [editors/rooms.md](editors/rooms.md) | sections 3 to 8 |
| F-RES-7 | [editors/sounds.md](editors/sounds.md) | sections 2 to 6 |
| F-RES-8 | [editors/fonts.md](editors/fonts.md) | sections 2 to 5 |
| F-RES-9 | [editors/image-editor.md](editors/image-editor.md) | sections 3 to 5 |
| F-RES-10 | [editors/sequences.md](editors/sequences.md)<br>[editors/animation-curves.md](editors/animation-curves.md)<br>[editors/particle-systems.md](editors/particle-systems.md)<br>[editors/paths.md](editors/paths.md)<br>[editors/timelines.md](editors/timelines.md) | one editor each; see the page headers |
| F-RES-11 | [shell.md](shell.md)<br>[editors/code.md](editors/code.md) | section 5.6 (Rename)<br>section 10 (Find usages) |
| F-RES-12 | [shell.md](shell.md)<br>[ux-standards.md](ux-standards.md) | section 14.4 (Undo)<br>section 7 |
| F-GIT-1 | [editors/git-panel.md](editors/git-panel.md) | sections 2 and 3 |
| F-GIT-2 | [editors/git-panel.md](editors/git-panel.md)<br>[editors/data-diff.md](editors/data-diff.md) | section 4<br>section 3 |
| F-GIT-3 | [editors/data-diff.md](editors/data-diff.md) | section 2 (Changes panel) |
| F-GIT-4 | [editors/git-panel.md](editors/git-panel.md) | section 5 (Merge conflicts) |
| F-MOD-1 | [editors/patches.md](editors/patches.md) | section 4 (Operations) |
| F-MOD-2 | [run-game.md](run-game.md) | sections 3 and 4 |
| F-MOD-3 | [scripting.md](scripting.md) | sections 3 and 4 (runner, console) |
| F-MOD-4 | [editors/patches.md](editors/patches.md)<br>[scripting.md](scripting.md) | section 5 (Batch mode)<br>section 6 (bundled scripts) |
| F-SUITE-1 | [editors/patches.md](editors/patches.md) | sections 4 and 10 |
| F-SUITE-2 | [editors/mod-packaging.md](editors/mod-packaging.md) | sections 2 to 12 |
| F-SUITE-3 | [run-game.md](run-game.md)<br>[editors/launch-profiles.md](editors/launch-profiles.md) | sections 5 and 7<br>sections 2 to 4 |
| F-SUITE-4 | [editors/mod-packaging.md](editors/mod-packaging.md) | section 10 (Saving and export) |
| F-SUITE-5 | [shell.md](shell.md)<br>[editors/mod-packaging.md](editors/mod-packaging.md) | section 13.1<br>section 13 |
| F-SUITE-6 | [run-game.md](run-game.md)<br>[editors/launch-profiles.md](editors/launch-profiles.md) | section 7<br>section 3 (Mod Diagnostics) |
| F-ONB-1 | [onboarding.md](onboarding.md) | sections 3 to 9 |
| F-PLG-1 | [editors/plugins-panel.md](editors/plugins-panel.md) | sections 2 and 3 (layout, install) |
| F-PLG-2 | [editors/plugins-panel.md](editors/plugins-panel.md) | section 6 (what plugins can add) |
| F-PLG-3 | [editors/plugins-panel.md](editors/plugins-panel.md) | section 6 |
| F-PLG-4 | [editors/plugins-panel.md](editors/plugins-panel.md) | section 4 (Manage) |
| F-PLG-5 | [editors/plugins-panel.md](editors/plugins-panel.md) | section 3 (Install and trust) |
| F-PLG-6 | [editors/plugins-panel.md](editors/plugins-panel.md) | section 5 (Develop) |
| F-PLG-7 | [editors/plugins-panel.md](editors/plugins-panel.md) | section 4, item 5 |
| F-PLG-8 | [editors/plugins-panel.md](editors/plugins-panel.md) | section 2 (Layout; the catalog is P2) |
| F-UPD-1 | [platforms-formats.md](platforms-formats.md) | section 4 (In-app updater) |
| F-UPD-2 | [platforms-formats.md](platforms-formats.md) | section 4 |
| F-UPD-3 | [platforms-formats.md](platforms-formats.md)<br>[settings.md](settings.md) | section 4<br>section 5.8 |
| F-UPD-4 | [platforms-formats.md](platforms-formats.md) | section 4 |
| F-L10N-1 | [ux-standards.md](ux-standards.md) | sections 6 and 13 |
| F-L10N-2 | [settings.md](settings.md)<br>[onboarding.md](onboarding.md) | section 5.2 (Language)<br>section 4 |
| F-L10N-3 | [../l10n/glossary.md](../l10n/glossary.md)<br>[ux-standards.md](ux-standards.md) | one glossary per language<br>section 6, item 7 |
| F-L10N-4 | [../l10n/glossary.md](../l10n/glossary.md) | how the glossary is built (outside design/) |
| F-L10N-5 | [ux-standards.md](ux-standards.md) | section 13 (Localization rules) |
| N-UI-1 | [../design.md](../design.md)<br>[ux-standards.md](ux-standards.md) | rules 1 to 6<br>sections 1 and 2 (tokens) |
| N-UI-2 | [ux-standards.md](ux-standards.md) | sections 6 to 8 (copy, confirmations, errors) |
| N-UI-3 | [shell.md](shell.md)<br>[ux-standards.md](ux-standards.md) | section 3.1 (every command in the palette)<br>section 3 (focus and keyboard) |
| N-PLT-1 | [platforms-formats.md](platforms-formats.md) | section 3 |
| N-PERF-1 | [../architecture.md](../architecture.md) | Performance budget |
| N-PERF-2 | [ux-standards.md](ux-standards.md) | sections 9 and 14 |
| N-SAFE-1 | [platforms-formats.md](platforms-formats.md)<br>[shell.md](shell.md) | section 8<br>section 14.2 |
| N-SAFE-2 | [ux-standards.md](ux-standards.md) | section 7 (Confirmations, undo and destructive actions) |
| N-SIZE-1 | [../roadmap.md](../roadmap.md) | M5 (Beta release); the size is recorded in [../status.md](../status.md) per release |
| N-QA-1 | [../architecture.md](../architecture.md) | Shape (test projects); the plan is in [../roadmap.md](../roadmap.md) |
| N-DOC-1 | [../status.md](../status.md) | updated at the end of every work session |
