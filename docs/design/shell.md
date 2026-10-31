# Shell

Status: proposed. Covers the window, menus, command palette, status bar, Explorer, Inspector, bottom panels, notifications, tabs, docking, the three layout presets (ADR-0010), canvas workspaces, multi-window use, empty states and the keyboard map. Rules that apply to every screen are in [ux-standards.md](ux-standards.md). Editors are in [editors/](editors/). Requirements covered: F-WS-1 to F-WS-9, F-UI-1 to F-UI-10, F-RES-1, F-RES-12, N-UI-2, N-UI-3.

Parity and suite IDs covered here are listed in [coverage.md](coverage.md). The ones that belong to this page: UTMT-APP-*, UTMT-FILE-*, UTMT-TREE-*, UTMT-TAB-*, UTMT-KEY-01 to 03, UTMT-SET-*, TOOL-53 to TOOL-55 (Jobs panel).

## 1. Window anatomy

Default window: 1440 x 900, centred on the screen that holds the mouse; minimum 960 x 600. The window remembers position, size, maximized state and monitor (UTMT-APP-10).

### 1.1 Studio preset (the default)

```
+--------------------------------------------------------------------------------------------------+
| G3M  File Edit View Go Resource Scripts Mod Run Git Tools Window Help    data.win - G3MStudio  _ [] X|  title bar 32
+--------------------------------------------------------------------------------------------------+
| < >  Undertale > objects > obj_player > Create                          [Run] [Save]  [Layout v]|  breadcrumb bar 28
+----------------+------------------------------------------------------------+--------------------+
| Explorer Search|  obj_player  | spr_player | room_start* | + v               | Inspector      [L] |
| Git            |------------------------------------------------------------| [search...]  0/0   |
| [Filter...] [v]|                                                            | obj_player         |
| v Quick Access |   (active editor: Object editor, Code editor, Room         | Object             |
|    Recent      |    editor, ...)                                            | v Properties       |
|    Favourites  |                                                            |   Sprite  [spr_p]  |
| v data.win   * |                                                            |   Visible   [x]    |
|  v Game        |                                                            |   Solid     [ ]    |
|     General    |                                                            |   Persistent[ ]    |
|     Options    |                                                            |   Depth     [0]    |
|  v Sprites 412 |                                                            |   Parent  [None]   |
|     spr_player |                                                            | > Physics          |
|     spr_enemy  |                                                            | > Events (12)      |
|  > Objects 233 |                                                            |                    |
|  > Rooms 94    |                                                            |                    |
|  > Code 3,120  |                                                            |                    |
|                |------------------------------------------------------------|                    |
|                | Problems 2 | Output | Search | References | Console  [^][x]|                    |
|                |  (selected bottom tab)                                     |                    |
+----------------+------------------------------------------------------------+--------------------+
| data.win*  main  x0 !2   Saving...  |  Ln 12, Col 4  GML  UTF-8  | 2023.8 BC17 Windows  ID 214  [bell] |  status 24
+--------------------------------------------------------------------------------------------------+
```

([bell] stands for the notifications bell icon.)

Regions and default sizes at 100% zoom:

| Region | Default | Min | Max | Collapses to | Holds |
| --- | --- | --- | --- | --- | --- |
| Title bar | 32 high | 32 | 32 | Never | Logo, menu bar, window title, sync state, caption buttons |
| Breadcrumb bar | 28 high | 28 | 28 | Hidden by setting `ui.breadcrumbs` | Back and forward, path of the active editor, Run, Save, Layout menu |
| Left dock | 280 wide | 200 | 50% of window | A 0 px edge handle | Explorer, Search, Git, Plugins (tabs along the top) |
| Right dock | 300 wide | 240 | 40% of window | A 0 px edge handle | Inspector, Outline |
| Bottom panel | 220 high | 120 | 70% of window | A 24 px tab strip | Problems, Output, Search results, References, Changes, History, Jobs, Console |
| Editor area | Rest | 320 wide | - | Never | Tab groups |
| Status bar | 24 high | 24 | 24 | Never | Section 5 |

Region rules:

1. Closing a dock or the panel never closes the editors. A dock remembers its last width and returns to it.
2. Edge handles are 6 px strips with a chevron on hover and a tooltip "Show Left Dock (Ctrl+B)". They are keyboard reachable with F6.
3. When the window is narrower than 1100 px, the right dock collapses automatically and the Inspector opens as a flyout from the right edge on demand. Below 960 px the left dock does the same. The user's manual choice wins after the first manual toggle.
4. Splitters are 2 px lines in `Border.Subtle` with a 6 px grab zone. Double-click resets a region to its default size. Arrow keys move a focused splitter by 16 px (Shift: 64 px).
5. Every region has a title row (24 px, `Bg.Raised`) with the panel tabs, a "..." menu for panel actions, and a close or collapse button. The focused region gets a `Border` outline (2 px); others use `Border.Subtle`.

### 1.2 Title bar and menu bar

1. Windows and Linux: the menu bar sits in the title bar, left of the window title. Alt or F10 focuses it. Left and Right move between menus, Down opens, Escape closes. Accelerator letters are not used (Alt+letter belongs to commands inside the Search panel), so type-ahead picks an item inside an open menu.
2. macOS: the native menu bar replaces the in-window one. The app menu holds About, Settings (Cmd+,), Services, Hide, Quit (Cmd+Q).
3. The window title reads "{active resource} - {workspace name} - G3MStudio". A filled dot before the workspace name marks unsaved changes. In project mode the name is the project name.
4. A menu item that cannot run is disabled and keeps its label; its tooltip says why ("Open a data file first."). Items are never hidden for being unavailable. Items that depend on a setting (the GameMaker debugger launch) are hidden only when the setting is off, and the Settings page says where to turn them on.
5. Dialog commands end with "..." (ux-standards 6). Submenus show a right chevron. Checked items show a check mark and a `checked` state for screen readers.
6. A menu may be torn into a toolbar? No. Toolbars are a fixed set: the breadcrumb bar actions and per-editor toolbars (see each editor). All of them render from the command registry (F-UI-3).
7. The sync state item sits right of the window title (ADR-0010). Direct mode: a button "Enable project" (`project.enable`), always visible. Project mode: "Synced", "Syncing..." or, when auto sync is off, "{n} changes not synced to data file"; clicking it runs Sync (`project.sync`).

## 2. Menus

Every top-level menu, every item, its command id and its default shortcuts. Generated from the command registry, so the same ids appear in the palette and in the shortcut editor. Items with no shortcut say None. "Open Recent" lists the last 10 workspaces, then "Reopen Closed Workspace" and "Clear Recently Opened"; Scripts lists the scripts folder (subfolders become submenus, "(No scripts found)" when empty, with a link to the folder); Window ends with the list of open windows.

### File menu

| Item | Id | Windows and Linux | macOS |
| --- | --- | --- | --- |
| New > New Data File | `file.new.dataFile` | Ctrl+N | Cmd+N |
| New > New Project Folder... | `file.new.projectFolder` | None | None |
| New > New G3M Mod... | `file.new.mod` | None | None |
| New > New Plugin... | `file.new.plugin` | None | None |
| Open Data File... | `file.open` | Ctrl+O | Cmd+O |
| Open Project Folder... | `file.openFolder` | Ctrl+K Ctrl+O | Cmd+K Cmd+O |
| Open Recent | `file.openRecent` | None | None |
| Open in New Window... | `file.openInNewWindow` | None | None |
| Close Workspace | `file.close` | Ctrl+K F | Cmd+K F |
| Save | `file.save` | Ctrl+S | Cmd+S |
| Save As... | `file.saveAs` | Ctrl+Shift+S | Cmd+Shift+S |
| Save a Copy... | `file.saveCopy` | None | None |
| Revert to Saved | `file.revert` | None | None |
| Show Data File in File Manager | `file.revealDataFile` | None | None |
| Import > Images as Sprite... | `file.import.images` | None | None |
| Import > Audio as Sound... | `file.import.audio` | None | None |
| Import > Included File... | `file.import.includedFile` | None | None |
| Import > Resources From Another Data File... | `file.import.resources` | None | None |
| Import > GameMaker Project (.yyp)... | `file.import.yyp` | None | None |
| Import > UTMT Project... | `file.import.utmtProject` | None | None |
| Import > G3M Mod, Deltamod or PizzaOven Package... | `file.import.mod` | None | None |
| Export > Export to G3M... | `file.export.g3m` | None | None |
| Export > Export as G3M Mod... | `file.export.mod` | None | None |
| Export > Export as Patch... | `file.export.patch` | None | None |
| Export > Export Modified Data File... | `file.export.dataFile` | None | None |
| Export > Export Selected Resources... | `file.export.selection` | None | None |
| Export > Export Project Folder... | `file.export.projectFolder` | None | None |
| Export > Export as GameMaker Project Folder (.yy)... | `file.export.yy` | None | None |
| Export > Export as UTMT 0.9 Project... | `file.export.utmtProject` | None | None |
| Project > Build Data File From Project | `project.build` | None | None |
| Project > Enable Project | `project.enable` | None | None |
| Project > Disable Project... | `project.disable` | None | None |
| Project > Sync to Data File | `project.sync` | None | None |
| Project > View Unexported Resources... | `project.viewUnexported` | None | None |
| Project > Mark for Export | `project.markForExport` | None | None |
| Project > Project Options... | `project.options` | None | None |
| Settings | `app.settings` | Ctrl+, | Cmd+, |
| Keyboard Shortcuts | `app.keyboardShortcuts` | Ctrl+K Ctrl+S | Cmd+K Cmd+S |
| Color Theme... | `app.colorTheme` | Ctrl+K Ctrl+T | Cmd+K Cmd+T |
| Quit | `app.quit` | Ctrl+Q | Cmd+Q |
| Close Tab | `tabs.close` | Ctrl+W | Cmd+W |
| Close All Tabs | `tabs.closeAll` | Ctrl+Shift+W | Cmd+Option+W |
| Reopen Closed Tab | `tabs.reopenClosed` | Ctrl+Shift+T | Cmd+Shift+T |
| Project > Remove Unchanged Marks | `project.removeUnchangedMarks` | None | None |
| Project > Rebase on Data File... | `project.rebase` | None | None |
| Project > Mark All Changed | `project.markAllChanged` | None | None |

### Edit menu

| Item | Id | Windows and Linux | macOS |
| --- | --- | --- | --- |
| Undo | `edit.undo` | Ctrl+Z | Cmd+Z |
| Redo | `edit.redo` | Ctrl+Y / Ctrl+Shift+Z | Cmd+Shift+Z |
| Undo History... | `edit.undoHistory` | None | None |
| Cut | `edit.cut` | Ctrl+X | Cmd+X |
| Copy | `edit.copy` | Ctrl+C | Cmd+C |
| Paste | `edit.paste` | Ctrl+V | Cmd+V |
| Delete | `edit.delete` | Delete | Cmd+Backspace / Delete |
| Select All | `edit.selectAll` | Ctrl+A | Cmd+A |
| Duplicate | `edit.duplicate` | Ctrl+D | Cmd+D |
| Rename | `edit.rename` | F2 | F2 / Return |
| Find | `edit.find` | Ctrl+F | Cmd+F |
| Replace | `edit.replace` | Ctrl+H | Cmd+Option+F |
| Find Next | `edit.findNext` | F3 | Cmd+G |
| Find Previous | `edit.findPrevious` | Shift+F3 | Cmd+Shift+G |
| Find in Resources | `edit.findInResources` | Ctrl+Shift+F | Cmd+Shift+F |
| Replace in Resources | `edit.replaceInResources` | Ctrl+Shift+H | Cmd+Shift+H |

### View menu

| Item | Id | Windows and Linux | macOS |
| --- | --- | --- | --- |
| Command Palette... | `view.commandPalette` | Ctrl+Shift+P / F1 | Cmd+Shift+P / F1 |
| Explorer | `view.explorer` | Ctrl+Shift+E | Cmd+Shift+E |
| Search | `view.search` | None | None |
| Git | `view.git` | Ctrl+Shift+G | Control+Shift+G |
| Problems | `view.problems` | Ctrl+Shift+M | Cmd+Shift+M |
| Output | `view.output` | Ctrl+Shift+U | Cmd+Shift+U |
| Inspector | `view.inspector` | Ctrl+Shift+I | Cmd+Shift+I |
| Script Console | `view.console` | Ctrl+` | Control+` |
| References | `view.references` | None | None |
| Changes | `view.changes` | None | None |
| Local History | `view.history` | None | None |
| Jobs | `view.jobs` | None | None |
| Plugins | `view.plugins` | Ctrl+Shift+X | Cmd+Shift+X |
| Docks > Show or Hide Left Dock | `view.toggleLeftDock` | Ctrl+B | Cmd+B |
| Docks > Show or Hide Right Dock | `view.toggleRightDock` | Ctrl+Shift+B | Cmd+Option+B |
| Docks > Show or Hide Bottom Panel | `view.toggleBottomPanel` | Ctrl+J | Cmd+J |
| Docks > Focus Editor (Hide All Docks) | `view.focusEditor` | Ctrl+K Z | Cmd+K Z |
| Full Screen | `view.fullScreen` | F11 | Control+Cmd+F |
| Zoom > Zoom In | `view.zoomIn` | Ctrl+= | Cmd+= |
| Zoom > Zoom Out | `view.zoomOut` | Ctrl+- | Cmd+- |
| Zoom > Reset Zoom | `view.zoomReset` | Ctrl+0 | Cmd+0 |
| Focus Next Region | `view.nextRegion` | F6 | F6 |
| Focus Previous Region | `view.previousRegion` | Shift+F6 | Shift+F6 |
| Focus Menu Bar | `view.menuBar` | F10 | F10 |

### Go menu

| Item | Id | Windows and Linux | macOS |
| --- | --- | --- | --- |
| Go to Resource... | `go.quickOpen` | Ctrl+P | Cmd+P |
| Go to Symbol in Editor... | `go.symbolInEditor` | Ctrl+Shift+O | Cmd+Shift+O |
| Go to Symbol in Data File... | `go.symbolInWorkspace` | Ctrl+T | Cmd+T |
| Go to Line or Index... | `go.line` | Ctrl+G | Control+G |
| Go to Definition | `go.definition` | F12 | F12 |
| Peek Definition | `go.peekDefinition` | Alt+F12 | Option+F12 |
| Find Usages | `go.references` | Shift+F12 | Shift+F12 |
| Back | `go.back` | Alt+Left | Control+- |
| Forward | `go.forward` | Alt+Right | Control+Shift+- |
| Next Problem | `go.nextProblem` | F8 | F8 |
| Previous Problem | `go.previousProblem` | Shift+F8 | Shift+F8 |
| Last Edit Location | `go.lastEdit` | Ctrl+K Ctrl+Q | Cmd+K Cmd+Q |
| Reveal in Explorer | `go.revealInExplorer` | Ctrl+K R | Cmd+K R |
| Bookmarks > Toggle Bookmark | `go.bookmark.toggle` | None | None |
| Bookmarks > Set Bookmark 1 to 9 | `go.bookmark.set` | None | None |
| Bookmarks > Go to Bookmark 1 to 9 | `go.bookmark.goto` | None | None |
| Recently Closed Editors... | `go.recentlyClosed` | None | None |

### Resource menu

| Item | Id | Windows and Linux | macOS |
| --- | --- | --- | --- |
| New Resource... | `resource.new` | Ctrl+Shift+A | Cmd+Shift+A |
| New Virtual Folder | `resource.newFolder` | None | None |
| Open | `resource.open` | Enter | Enter |
| Open in New Tab | `resource.openNewTab` | Ctrl+Enter | Cmd+Enter |
| Copy Name | `resource.copyName` | Ctrl+Shift+C | Cmd+Shift+C |
| Copy Index | `resource.copyIndex` | None | None |
| Add to Favourites | `resource.favourite` | None | None |
| Edit Tags... | `resource.addTag` | None | None |
| Move to Group... | `resource.moveToGroup` | None | None |
| Move to Folder... | `resource.moveToFolder` | None | None |
| Find Unreferenced Resources... | `resource.findUnreferenced` | None | None |
| Move Up in Custom Order | `resource.moveUp` | Ctrl+] | Cmd+] |
| Move Down in Custom Order | `resource.moveDown` | Ctrl+[ | Cmd+[ |
| Move to Top in Custom Order | `resource.moveTop` | Ctrl+Shift+] | Cmd+Shift+] |
| Move to Bottom in Custom Order | `resource.moveBottom` | Ctrl+Shift+[ | Cmd+Shift+[ |
| Show in Inspector | `resource.showInInspector` | None | None |
| Restore Original Resource | `resource.restoreOriginal` | None | None |

### Scripts menu

| Item | Id | Windows and Linux | macOS |
| --- | --- | --- | --- |
| Run Script... | `scripts.run` | Ctrl+Shift+R | Cmd+Shift+R |
| Run Last Script | `scripts.runLast` | None | None |
| Refresh Script List | `scripts.refresh` | None | None |
| Open Scripts Folder | `scripts.openFolder` | None | None |
| Check Script... | `scripts.lint` | None | None |
| New Script File... | `scripts.new` | None | None |
| Cancel Running Script | `scripts.cancel` | None | None |
| Trust Scripts in This Folder | `scripts.trustFolder` | None | None |

### Mod menu

| Item | Id | Windows and Linux | macOS |
| --- | --- | --- | --- |
| Open Mod... | `mod.open` | None | None |
| Save Mod | `mod.save` | None | None |
| Validate Mod | `mod.validate` | None | None |
| Patch > Create Patch... | `mod.createPatch` | None | None |
| Patch > Apply Patch... | `mod.applyPatch` | None | None |
| Patch > Validate Patch... | `mod.validatePatch` | None | None |
| Patch > Merge Patches... | `mod.mergePatches` | None | None |
| Patch > Create xdelta Patch... | `mod.createXdelta` | None | None |
| Patch > Apply xdelta Patch... | `mod.applyXdelta` | None | None |
| Patch > Run xdelta With Arguments... | `mod.runXdelta` | None | None |
| Compare Files... | `mod.compare` | None | None |
| Inspect File... | `mod.inspect` | None | None |
| Batch Operations... | `mod.batch` | None | None |
| Run External Program... | `mod.runProgram` | None | None |
| Check Mod Dependencies... | `mod.dependencies` | None | None |
| Create Modpack... | `mod.createModpack` | None | None |

### Run menu

| Item | Id | Windows and Linux | macOS |
| --- | --- | --- | --- |
| Launch Game | `run.game` | F5 | F5 |
| Launch With Options... | `run.withOptions` | Alt+F5 | Option+F5 |
| Launch With Mods... | `run.withMods` | None | None |
| Launch and Keep Changes... | `run.keepChanges` | None | None |
| Stop Game | `run.stop` | Shift+F5 | Shift+F5 |
| Restart Game | `run.restart` | Ctrl+Shift+F5 | Cmd+Shift+F5 |
| Launch Under GameMaker Debugger | `run.gmsDebug` | None | None |
| Launch Profiles... | `run.profiles` | None | None |
| Show Game Log | `run.openLog` | None | None |
| Crash Reports... | `run.crashReports` | None | None |
| Mod Diagnostics... | `run.diagnostics` | None | None |
| Restore Game Files... | `run.restoreFiles` | None | None |
| Check Mod Conflicts... | `run.checkConflicts` | None | None |

### Git menu

| Item | Id | Windows and Linux | macOS |
| --- | --- | --- | --- |
| Initialize Repository | `git.init` | None | None |
| Clone Repository... | `git.clone` | None | None |
| Commit... | `git.commit` | None | None |
| Push | `git.push` | None | None |
| Pull | `git.pull` | None | None |
| Fetch | `git.fetch` | None | None |
| Switch Branch... | `git.branch.switch` | None | None |
| New Branch... | `git.branch.new` | None | None |
| Stage All Changes | `git.stageAll` | None | None |
| Unstage All Changes | `git.unstageAll` | None | None |
| View History | `git.history` | None | None |
| Show Conflicts | `git.conflicts` | None | None |
| Stash Changes... | `git.stash` | None | None |

### Tools menu

| Item | Id | Windows and Linux | macOS |
| --- | --- | --- | --- |
| Texture Groups... | `tools.textureGroups` | None | None |
| Audio Groups... | `tools.audioGroups` | None | None |
| Sound Mixer | `tools.soundMixer` | None | None |
| Texture Page Viewer | `tools.texturePages` | None | None |
| Check Data File Integrity | `tools.integrity` | None | None |
| Export All... | `tools.batchExport` | None | None |
| Import All... | `tools.batchImport` | None | None |
| Compiler and Decompiler Settings... | `tools.gmlSettings` | None | None |
| Generate Offset Map... | `tools.offsetMap` | None | None |
| Open App Data Folder | `tools.openAppData` | None | None |
| Clear Caches... | `tools.clearCaches` | None | None |
| Startup Performance Report | `tools.startupPerformance` | None | None |
| Game Versions... | `tools.gameVersions` | None | None |
| Fix All Safe Integrity Findings | `tools.integrityFix` | None | None |
| Convert Bytecode Version... | `tools.convertBytecode` | None | None |

### Window menu

| Item | Id | Windows and Linux | macOS |
| --- | --- | --- | --- |
| New Window | `window.new` | Ctrl+Shift+N | Cmd+Shift+N |
| Open Workspace in New Window | `window.workspaceInNewWindow` | None | None |
| Move Editor to New Window | `window.moveEditorToWindow` | None | None |
| Float Panel | `window.floatPanel` | None | None |
| Layout > Layout: Studio | `window.layout.studio` | None | None |
| Layout > Layout: Code | `window.layout.code` | None | None |
| Layout > Layout: Canvas | `window.layout.canvas` | None | None |
| Layout > Save Layout As... | `window.layout.saveAs` | None | None |
| Layout > Load Layout... | `window.layout.load` | None | None |
| Layout > Reset Layout | `window.layout.reset` | None | None |
| Layout > Import Layout File... | `window.layout.import` | None | None |
| Layout > Export Layout File... | `window.layout.export` | None | None |
| Canvas > Fit All Cards | `board.fitAll` | Ctrl+Shift+Home | Cmd+Shift+Home |
| Canvas > Focus Card on the Left | `board.focusLeft` | Ctrl+Alt+Left | Control+Option+Left |
| Canvas > Focus Card on the Right | `board.focusRight` | Ctrl+Alt+Right | Control+Option+Right |
| Canvas > Focus Card Above | `board.focusUp` | Ctrl+Alt+Up | Control+Option+Up |
| Canvas > Focus Card Below | `board.focusDown` | Ctrl+Alt+Down | Control+Option+Down |
| Canvas > Card Overview | `board.overview` | None | None |
| Canvas > New Canvas Tab | `board.new` | None | None |
| Canvas > Link Cards | `board.link` | None | None |
| Canvas > Unlink Cards | `board.unlink` | None | None |

### Help menu

| Item | Id | Windows and Linux | macOS |
| --- | --- | --- | --- |
| Getting Started | `help.gettingStarted` | None | None |
| Interface Tour | `help.tour` | None | None |
| Documentation | `help.docs` | None | None |
| Keyboard Shortcuts Reference | `help.shortcutsReference` | None | None |
| Release Notes | `help.releaseNotes` | None | None |
| Check for Updates... | `help.checkUpdates` | None | None |
| Revert to Previous Version... | `help.revertVersion` | None | None |
| Community | `help.community` | None | None |
| Open Log Folder | `help.openLogFolder` | None | None |
| Report an Issue | `help.reportIssue` | None | None |
| Licences | `help.licences` | None | None |
| About G3MStudio | `help.about` | None | None |
| Create Support Package... | `help.supportPackage` | None | None |

### Editor menus

Each name below appears in the menu bar after Resource while that editor has focus. Items are the commands of the editor, in the order of the table in its spec file.

| Menu | Spec | Item count |
| --- | --- | --- |
| Code | [code.md](editors/code.md) | 31 |
| Object | [objects.md](editors/objects.md) | 9 |
| Sprite | [sprites.md](editors/sprites.md) | 26 |
| Image | [image-editor.md](editors/image-editor.md) | 30 |
| Tile Set | [backgrounds-tilesets.md](editors/backgrounds-tilesets.md) | 4 |
| Texture Page | [texture-pages.md](editors/texture-pages.md) | 9 |
| Room | [rooms.md](editors/rooms.md) | 24 |
| Sound | [sounds.md](editors/sounds.md) | 9 |
| Font | [fonts.md](editors/fonts.md) | 11 |
| Path | [paths.md](editors/paths.md) | 9 |
| Timeline | [timelines.md](editors/timelines.md) | 4 |
| Sequence | [sequences.md](editors/sequences.md) | 15 |
| Curve | [animation-curves.md](editors/animation-curves.md) | 5 |
| Particles | [particle-systems.md](editors/particle-systems.md) | 6 |
| Extension | [extensions.md](editors/extensions.md) | 4 |
| Shader | [shaders.md](editors/shaders.md) | 3 |

Rules for menus:

1. Any command reachable only through a menu is a bug; every id here is in the palette (N-UI-3).
2. Shortcut text in menus comes from the active keymap.
3. Platform labels: "Show Data File in File Manager" reads "Show in Explorer" on Windows, "Reveal in Finder" on macOS and "Open Containing Folder" on Linux.
4. Editor menus (Code, Object, Sprite, Image, Tile Set, Texture Page, Room, Sound, Font, Path, Timeline, Sequence, Curve, Particles, Extension, Shader) are inserted after Resource while that editor has focus (the GameMaker pattern) and disappear when it loses focus to a non-editor panel. Their items are the tables in the editor specs.

## 3. Command registry, command palette and Go To

### 3.1 Registry

Every action is one registry entry: id (`area.verb`), localized title, icon, default shortcuts per platform, an enable predicate on context keys, and a handler. Menus, toolbars, context menus, the palette and the shortcut editor render from it (F-UI-3). Plugins add entries through the same API (F-PLG-2).

Context keys (used in `when` clauses and enable predicates): `workspaceOpen`, `projectMode`, `dirty`, `readOnly`, `explorerFocus`, `editorFocus`, `textInputFocus`, `canvasFocus`, `editorType` (for example `room`), `selectionCount`, `selectionType`, `bytecodeSupported`, `yyc`, `gameRunning`, `jobRunning`, `gitRepo`, `modalOpen`, `focusedPanel` (`explorer`, `search`, `git`, `problems`, `console`, `patches`, `output`), `roomMode` (`instances` or `tiles`), `boardFocus` (focus is on the canvas workspace surface, not inside a card).

Naming: lowercase dotted ids, verbs last (`file.save`, `rooms.deleteLayer`). Ids are stable; a removed id stays reserved.

Ownership rules:

1. Each editor spec owns the command ids of its editor. The id prefix is the editor name (`rooms.*` in rooms.md, `sprites.*` in sprites.md, `code.*` in code.md, `plugins.*` in plugins-panel.md).
2. Every command appears in the command palette, whichever spec owns it. A command with no menu item or shortcut is still in the palette (N-UI-3).
3. This file lists the global commands: the ones in the menus of section 2, the keyboard map of section 16 and the table below. A command an editor spec owns is not repeated here.

Global `settings.*` and `plugins.*` commands (specs: [settings.md](settings.md), [editors/keyboard-shortcuts.md](editors/keyboard-shortcuts.md), [editors/plugins-panel.md](editors/plugins-panel.md)):

| Command | Id | Default shortcut |
| --- | --- | --- |
| Open settings.json | `settings.openJson` | None |
| Reset Setting | `settings.reset` | None |
| Reset All Settings... | `settings.resetAll` | None |
| Focus Settings Search | `settings.focusSearch` | Ctrl+F (Cmd+F) in the Settings tab |
| Switch Scope (User, Workspace) | `settings.switchScope` | None (Ctrl+Tab in the scope bar) |
| Record Key | `settings.recordKey` | Enter in the shortcut table |
| Clear Key Binding | `settings.clearKey` | Delete in the shortcut table |
| Install Plugin... | `plugins.install` | None |
| Reload Plugin | `plugins.reload` | None |
| Disable Plugin | `plugins.disable` | None |
| Open Plugin Folder | `plugins.openFolder` | None |

### 3.2 Command palette and Go To

One input box, five modes chosen by a prefix typed first. The box opens centred at the top of the window, 560 px wide (up to 50% of the window), 8 rows tall by default (16 maximum), and keeps focus in the text field while arrows move through results. Escape closes it and returns focus.

| Prefix | Shortcut | Searches | Result row |
| --- | --- | --- | --- |
| none | Ctrl+P (Cmd+P) | Resources by name across all types; a type prefix and colon narrows it: `spr:`, `obj:`, `room:`, `scr:`, `snd:`, `fnt:`, `path:`, `tl:`, `shd:`, `bg:`, `ext:`, `seq:`, `curve:`, `ps:`, `code:`, `str:` | Type icon, name, type label, folder or group path. Right side: "Open" hint. |
| `>` | Ctrl+Shift+P, F1 | Commands, then Settings entries ("Settings: Autosave"), then game option and general info fields ("Game Options: Fullscreen") | Command title, category, shortcut on the right. Settings rows show the page path. |
| `@` | Ctrl+Shift+O | Symbols in the active editor: events of an object, functions and locals of a code entry, layers of a room, frames of a sprite, tracks of a sequence | Icon, symbol, kind, line or index |
| `#` | Ctrl+T | Functions, variables and strings of the whole data file (FUNC, VARI, STRG), with kind badges | Name, kind badge, use count |
| `:` | Ctrl+G | A line in a code editor, a row in a table editor, or an index in the focused resource list (":214" selects index 214; ":spr 214" targets the sprite list) | One row: "Go to line 214" or "Go to index 214: spr_player" |
| `?` | none | Lists these modes | Mode help |

Behaviour:

1. Fuzzy match on initials and substrings with the matched letters highlighted (GameMaker Go To). Ranking: exact prefix, then initials, then substring, then fuzzy, with a recency boost for items opened in this workspace.
2. The palette opens with the last mode used when invoked from its own shortcut and with the mode's prefix preloaded for the mode shortcuts.
3. Enter runs the item. Ctrl+Enter opens a resource in a new tab; Alt+Enter opens it to the side (a split). Arrow keys preview the selected resource in a preview tab behind the palette when `go.previewOnArrow` is on (default off).
4. Backspace on an empty prefixed box returns to the default mode.
5. Result count limit 50, with "{n} more, keep typing to narrow" as a final non-selectable row. Search runs on a background index and never blocks typing.
6. Empty states: no matches shows "No results for '{text}'." and, in resource mode, "Try '>' for commands or '#' for strings and functions."
7. Screen readers: the list is a listbox with the selected option announced as "{title}, {kind}, {shortcut}".

### 3.3 Quick open (F-UI-4)

Quick open is the default mode of the box in 3.2: `go.quickOpen` (Ctrl+P, Cmd+P) finds a resource by name across all types.

1. Typing filters the full resource list of the open data file as you type; the type prefixes of 3.2 (`spr:`, `obj:`, `room:` and the others) narrow it to one type.
2. Enter opens the resource in the preview tab; Ctrl+Enter opens it in a new tab; Alt+Enter opens it to the side.
3. Names that exist in two types show the type label and folder path so the rows are distinguishable.
4. With no text, the box lists the resources opened most recently in this workspace.
5. Empty result: "No results for '{text}'." with the hint line of 3.2 item 6.

## 4. Status bar

24 px high, `Bg.Raised`, items 12 px, separated by 8 px gaps. Items are buttons (focusable, 24 px hit height). Left side is workspace scope, right side is context of the focused editor (VS Code guideline). Items hide when they have no value; the order never changes.

| Side | Item | Shows | Click |
| --- | --- | --- | --- |
| Left | Workspace | Data file name or project name, a filled dot when unsaved, "Restricted" icon when a plugin or script is blocked | Opens `file.revealDataFile` menu: copy path, show in file manager, close |
| Left | Git branch | Branch name, ahead and behind arrows, changed file count (project mode with Git) | `git.branch.switch` |
| Left | Problems | Error count and warning count with icons | Opens Problems |
| Left | Save state | "Saved 14:02", "Unsaved changes", "Saving data.win...", "Recovered 3 unsaved edits" | Click runs `file.save` when unsaved |
| Left | Jobs | Spinner, newest job text, "+N more" | Opens Jobs |
| Right | Editor hint | Active tool hint, for example "Click to paint (Shift for a straight line, Ctrl to pick a colour)" (GameMaker habit) | None |
| Right | Position | "Ln 12, Col 4" in code; "x 1819, y 2198" in canvases; "Frame 3 of 8" in sprites | `go.line` or a position popup |
| Right | Selection | "3 selected", "14 chars selected" | Opens Inspector |
| Right | Language | "GML", "Disassembly", "GLSL ES" | Opens the language picker where changeable |
| Right | Encoding | "UTF-8" for text resources | Shows details |
| Right | Zoom | "100%" for canvases and code | Opens a zoom list |
| Right | Data file info | "GameMaker 2023.8, Bytecode 17, Windows" (GEN8 and platform) | Opens General Info |
| Right | Asset ID | "ID 214" index of the open resource (UTMT-APP-06) | Copies the index |
| Right | Marked for export | Check box "Marked for export" in project mode (UTMT-PROJ-06) | Toggles |
| Right | Notifications | Bell icon with unread count | Opens the history |

Rules: a chord in progress shows "(Ctrl+K) was pressed. Waiting for the second key..." in the left area for 2 seconds. Tooltips give the full text and the command. Items are never animated except the Jobs spinner (static when Reduce motion is on).

## 5. Explorer

### 5.1 Layout

```
+-------------------------------------+
| Explorer                  [+] [v] [...]|  title row: New Resource, Filter menu, panel menu
| [ Filter resources...      ] [x]      |  filter box 24
| Types: Any   State: Any   (chips)    |  active filter chips, hidden when empty
+-------------------------------------+
| v Quick Access                       |
|     Recent                       10  |
|     Favourites                    3  |
|     Room Order                   94  |
|     Saved Filters                 2  |
|     Tags                         5  |
| v data.win                    *  M   |
|   v Game                             |
|       General Info                   |
|       Game Options                   |
|       Global Init                    |
|       Game End                       |
|   v Sprites                    412   |
|     > Characters                     |  virtual folder
|       spr_logo                       |
|   > Tile Sets                   31   |
|   > Sounds                     208   |
|   ... (the other types)              |
|   v Data                             |
|       Code                   3,120   |
|       Strings               18,402   |
|       ...                            |
|   > Included Files                   |
+-------------------------------------+
| 7,431 resources   2 selected   100% |  footer: counts and row zoom
+-------------------------------------+
```

### 5.2 Tree content

Root is the workspace (data file or project). Under it, in this order:

1. Quick Access (collapsible, hidden by `explorer.quickAccess` = off): Recent (default 10), Favourites, Room Order (the GEN8 room order, drag to reorder, first room marked with a home icon, UTMT-GEN-04 shares the data), Saved Filters (named filters, double-click to apply), Tags (from the TAGS chunk, click to filter).
2. Game: General Info, Game Options, Global Init, Game End (UTMT-AST-13, -14).
3. Resource types, shown only when the chunk exists: Sprites, Tile Sets (labelled Backgrounds for GMS1 files, UTMT-TREE-10), Sounds, Paths, Scripts, Shaders, Fonts, Timelines, Objects, Rooms, Extensions, Sequences, Animation Curves, Particle Systems (emitters appear as children).
4. Data: Code, Strings, Variables, Functions, Code Locals, Texture Page Items, Embedded Textures, Embedded Images, Embedded Audio, Texture Groups, Audio Groups, Tags, Feature Flags, Filter Effects, UI Layers, Language. Chunks without an editor open the read-only structured viewer (editors/general-info.md, section "Chunks without an editor").
5. Included Files: the loose files next to the data file (editors/included-files.md).

Counts are right-aligned in `Text.Muted`. Null entries (GameMaker 2024.11 and later files) are hidden by default; `explorer.showNull` shows them as "(null) {index}" rows in `Text.Muted` (UTMT-TREE-11, UTMT-SET-09).

Row anatomy (24 px): chevron (24 px target), type icon (16), name, badges, count. Badges in this order: Git letter (project mode), problem icon (error or warning), modified dot, favourite star. A modified resource has a 6 px dot in `Warning`; a resource added in this session shows "A" in `Success`; a resource marked for deletion does not exist (deletes are immediate and undoable). Tooltip on a row: "{name} - {type}, index {n}. {state}." Rows show the index in the tooltip only; the Inspector and status bar show it too.

### 5.3 Virtual folders

1. Virtual folders organise the tree and nothing else. They exist only in the sidecar (`.g3mproj` or the workspace state in app data, architecture.md "Files on disk"), so the data file never changes because of a folder.
2. A folder holds resources of one type and lives under that type. Create with `resource.newFolder`, rename with F2, delete with Delete (members return to the type root, no confirmation).
3. Drag a resource onto a folder, or use "Move to Folder..." (the non-drag alternative). Dragging never changes the resource's index. Index order only changes through an explicit "Reorder" action in the type's editor where order matters (room order, audio group members, texture group members), and never silently.
4. "Custom Order" sort (`explorer.sort.custom`) stores a display order in the sidecar and enables the Move Up, Down, Top, Bottom commands.
5. Folder colours: 12 swatches in the context menu. A colour has a name for screen readers ("Folder colour: Teal"). Colour applies to the folder icon only.

### 5.4 Filter, sort and view

Filter box syntax (also available through the Filter menu with check boxes, so no syntax is required):

| Token | Meaning |
| --- | --- |
| text | Name contains text, case-insensitive (UTMT-APP-03) |
| `spr:text` | Only that type (same prefixes as Go to Resource) |
| `is:modified` | Changed, added or deleted since opening |
| `is:problem` | Has errors or warnings |
| `is:unused` | Not referenced by anything (needs the reference index) |
| `is:favourite` | In Favourites |
| `tag:name` | Has the tag |
| `-token` | Negates a token |

While a filter is active the tree auto-expands to the matches, the filter button shows `Select` fill, the footer shows "{matches} of {total}", and the chip row lists each active part with an X. Escape in the filter box clears it. "Save Current Filter..." stores it under Saved Filters.

Sort (per type, setting `explorer.sort`): Index (default, the data file order), Name A to Z, Name Z to A, Custom Order. "Folders first" is on by default. View modes: tree (default) and list (flat, with a Type column). Row zoom 75% to 200% (footer slider).

Large lists: virtualized; 20,000 resources open to a usable tree in under 2 seconds (ADR-0008).

### 5.5 Selection, drag and drop, keyboard

1. Single click selects and shows the resource in the Inspector. Double-click, Enter or `resource.open` opens it in the preview tab. Middle-click, Ctrl+Enter or "Open in New Tab" opens a normal tab (UTMT-TREE-01, -02).
2. Ctrl+click toggles, Shift+click selects a range, Ctrl+A selects all in the focused type. Multi-select shows the group summary in the Inspector (section 6).
3. Arrow keys move, Right expands, Left collapses (or moves to parent), Home and End jump, typing jumps to the next row whose name starts with the typed text, Space toggles the check state where a row has one.
4. Drag out of the Explorer: onto a Reference field (sets it), onto a room canvas (creates an instance or a sprite instance), onto a code editor (inserts the name), onto the canvas workspace (opens a card at the drop point), onto a virtual folder (moves). Alternatives that need no drag: the field's "Choose..." picker, the room editor's "Add Instance..." command, copy name and paste, "Open in New Tab".
5. Drag into the Explorer: image and audio files from the file manager create resources (Images as Sprite, Audio as Sound) with an import dialog; a data file or `.g3mpatch` or `.csx` or `.zip` opens the matching action prompt (UTMT-FILE-13); other files become Included Files.

### 5.6 Context menus

Menu key, Shift+F10 and right-click open the same menu. Common items first, then type items, then Delete last, separated by lines.

Common items (all types): Open, Open in New Tab, Rename (F2), Duplicate (Ctrl+D), Copy Name, Copy Index, Find Usages (Shift+F12), Add to Favourites, Edit Tags..., Move to Folder..., Show in Inspector, Restore Original Resource (when modified), Delete.

| Type | Extra items |
| --- | --- |
| Sprite | Assign Texture Group..., Import Images..., Export Frames..., Export Strip PNG..., Open in Image Editor, Edit in External Program |
| Tile set | Assign Texture Group..., Export Tile Sheet..., Find Rooms Using Tiles |
| Sound | Assign Audio Group..., Play, Open in Sound Mixer, Replace Audio File..., Export Audio File..., Edit in External Program |
| Path | Show Room Behind Path... |
| Script | Open Code, Show Linked Code Entry |
| Shader | Show Source Language... |
| Font | Assign Texture Group..., Regenerate Font Texture, Export Font Texture... |
| Timeline | Open All Moment Code |
| Object | Open All Events in Tabs, Show Child Objects, Open Parent Object, Add Event... |
| Room | Open Instance Creation Order, Set as First Room, Save Room as PNG..., Export Tile Layers as CSV... |
| Extension | Add Extension File |
| Sequence, Animation Curve | Export as JSON... |
| Particle system | Copy GML to Clipboard |
| Code entry | Open Decompiled, Open Disassembly, Export Code to File..., Import Code From File..., Revert to Original Code |
| String | Show Uses, Copy |
| Variable, Function, Code local | Show Uses |
| Texture page item | Replace From PNG..., Export to PNG..., Find Usages of Item |
| Embedded texture | Replace Page From PNG..., Export Page to PNG... |
| Embedded audio | Replace Audio..., Export Audio..., Open in the Audio Group File |
| Texture group, audio group | Add Resources..., Preview |
| Folder (virtual) | New Virtual Folder, Rename, Colour, Delete Folder, Expand All, Collapse All, Open Folder in New Explorer |
| Type root | New {Type}..., Expand All, Collapse All, Sort By, Export All {Type}..., Import All {Type}... (tools.batchImport) |
| Included file | Open, Open With..., Show in File Manager, Replace..., Delete |
| Workspace root | Close Workspace, Show Data File in File Manager, Copy Path, Properties (size, MD5, GEN8 summary) |

Delete: runs a reference scan first (ux-standards 7). The dialog title is "Delete 'obj_enemy'?" and the body "{count} places use it: rooms 'room_a' (4 instances), object 'obj_b' (parent). Deleting shifts the index of {n} later objects." Buttons: "Delete and Leave References Broken", "Delete and Show Uses" (opens References), "Cancel". Without references there is no dialog and a notification offers Undo (UTMT-TREE-06).

New resource (`resource.new`): a popup "New Resource" with the type grid or list (all types the data file supports), a count spinner (1 to 99) to create several at once, and the name field. New items go to the bottom of their type, open in the preview tab, and select in the Explorer. Defaults per type follow UTMT-TREE-04: rooms get flags for the file's GameMaker version and an optional room-order entry; scripts get a code entry (`gml_GlobalScript_` + name, plus a global init entry in 2.3 and later, otherwise `gml_Script_`); code gets a locals entry; extensions get folder, class, version and a product ID; shaders get six empty sources; page items, embedded audio and textures get synthetic names. Names are validated live (Name field, ux-standards 11) and an automatic name is offered on failure.

## 6. Inspector

The right dock's default panel. It edits the selected resource without opening its editor (GameMaker Inspector, 'copy' decision in the research).

```
+-----------------------------------+
| Inspector                  [L] [v]|  lock, menu
| [Search properties...] 0 of 12 ^ v|  search box with match counter
+-----------------------------------+
| [thumb]  obj_player        Object |
| [Open Editor]  [Add Event...]     |
+-----------------------------------+
| v Properties                      |
|   Sprite       [spr_player  ...]  |
|   Mask         [Same as sprite]   |
|   Visible      [x]                |
|   Solid        [ ]                |
|   Persistent   [ ]                |
|   Depth        [ 0        ]  (r)  |
|   Parent       [None       ...]   |
| > Physics                         |
| > Events (12)                     |
+-----------------------------------+
```

Behaviour:

1. Header: 48 px thumbnail (when the type has an image), name (editable on F2 or double-click), type label, an "Open Editor" button and type buttons (sprite: Import, Import Strip, Edit Image; object: Add Event; sound: Play).
2. Body: the shared property grid (ux-standards 11). Categories are collapsible; the open state persists per type. Each row: label (sentence case), control, a changed marker, and a Reset to Original button `(r)` when the value differs from the loaded file. Hover shows the field description and the chunk field name in monospace ("OBJT.Depth") for modders.
3. Search box filters rows by label and description, with a match counter and up and down buttons (Enter and Shift+Enter).
4. Lock: pins this Inspector to its current resource. More Inspectors open as extra panels (View > Inspector > New Inspector); each can be locked to a different resource. Locking does not make values read-only.
5. Multi-select: the header becomes "Selection: 2 Objects, 1 Room, 4 Sprites" with an "Open Editors" button (warns above 10, `inspector.openLimit`) and a list of the selected items, each with an X to drop it; clicking one item shows that item alone without losing the selection; clicking empty list space returns to the group. For items of one type the grid shows shared values, and "-" where they differ (a mixed value is a placeholder, not a value; the control shows an indeterminate state). Editing a field applies it to all selected items as one undo step named "Edit {field} on {n} items".
6. Selected inside an editor (instance, layer, frame, track, emitter, point): the Inspector shows that element and the header names its owner ("inst_10AC2371 in room_start"). The Explorer selection returns when focus goes back to it.
7. Read-only items (chunks without an editor, bytecode-unsupported code) show a lock icon and the reason in the header.
8. Empty state: "Select a resource to see its properties." with the tip "Press Ctrl+P to find one by name."
9. Loading state: skeleton rows while a large resource decodes (over 100 ms).
10. Errors: a field that fails validation shows its message under the field and keeps focus; the value is not applied.

## 7. Bottom panel: Output, Problems, Search, References, Changes, History, Jobs, Console

Tabs along the panel's title row, closeable except Problems and Output. The panel remembers the last tab. Tab labels carry a count when meaningful ("Problems 2"). The panel can be moved to the right edge or into a floating window.

### 7.1 Problems (F-UI-8)

The home for every validation result (decompile failures, compile errors, broken references, missing external audio, load warnings, lint results).

| Part | Behaviour |
| --- | --- |
| Toolbar | Toggle buttons for Errors, Warnings and Info with counts (all on by default), a filter box (code, message, resource), "Current resource only" toggle, Collapse All, Copy All |
| Columns (sortable) | Severity icon, Code (for example `G3MS-REF-004`, `GM1022`), Message, Resource, Location (line, column or field path), Source (Load, Compile, Decompile, Validation, Plugin name) |
| Grouping | By resource by default, flat by choice |
| Row actions | Enter or double-click opens the location. Ctrl+. (when a fix exists) applies the fix. Context menu: Open, Open in New Tab, Apply Fix, Ignore This Code, Copy |
| Fix column | A "Fix" button when a safe automatic fix exists (for example "Unlink missing audio file") |
| Empty | "No problems found in 'data.win'." |

Errors also color the Explorer row (red icon badge) and the tab (red underline plus the icon), so problems are visible without opening the panel. The status bar counts open Problems. F8 and Shift+F8 step to the next and previous problem in the open editor.

### 7.2 Output (F-UI-8)

A read-only log with a channel drop-down: Load, Save, Compile, Decompile, Validation, Search, Export, Patch, Game, Scripts, Git, Updates, Localization, and one per plugin ("Plugin: {name}"). Each line has a timestamp, a level icon and text; `Text.Muted` for debug, normal for info, `Warning` and `Danger` text with icons for warnings and errors. Toolbar: channel picker, filter, Level filter, Clear, Save to File..., Lock Scroll, Open Log File. Channels write to the rolling log file in the log folder (Help > Open Log Folder). Selecting a line of a known form (path, resource name, `file(line)`) turns it into a link.

### 7.3 Search results

The shared result list (ux-standards 11). See section 7.7 for the Search panel (query side) and editors/code.md for code search specifics. Results panel actions: Open, Open in New Tab, Copy, Copy All, Export Results, history of the last 10 queries (UTMT-SRCH-02, -03).

### 7.4 References

Output of Find Usages and Find Unreferenced Resources. A dialog first picks the reference kinds to scan (Select All, Deselect All; kinds listed in UTMT-SRCH-05), default all. The result is a tree: resource, then the places that use the target with their path ("rooms/room_a > Layer 'Instances' > inst_10AC2371"). Header shows "{count} usages of 'spr_player'". Actions: Open, Open in New Tab, Copy Name, nested Find Usages on a result, Export to `references_of_{name}.txt`, filter box. Unreferenced results group by type and export to `unreferenced_assets.txt` (UTMT-SRCH-04). Empty: "Nothing uses 'spr_player'."

### 7.5 Changes

The data file against its original (the state when opened, or the last saved file, chosen in the header drop-down), using G3MLib's diff (F-GIT-3). Details in editors/data-diff.md. Header shows totals ("48 changed, 3 added, 1 deleted"). Rows: type icon, name, change kind, a one-line summary. Enter opens the diff view for that resource.

### 7.6 History and Jobs

History is the Local History list: one row per save (time, size, summary, a star to keep it). Actions: Compare with Current, Restore, Delete, Keep. Restore is one undo step and is added to local history first. The list can also show a single resource's history ("Timeline" of that resource) from its tab menu. Jobs lists running and finished jobs with progress, Cancel, elapsed time, and a link to the job's Output channel (TOOL-53, TOOL-55). The Jobs list keeps the last 50 finished jobs.

### 7.7 Search panel (left dock) and Console

The Search panel (Ctrl+Shift+F) is a left-dock tab with:

1. A query box with toggles Match Case (Alt+C), Whole Word (Alt+W), Regular Expression (Alt+R), Multiline (Alt+M), Search Disassembly (Alt+A); a Replace row (Ctrl+H toggles it).
2. Scope list: Code, Strings, Names, Room creation code, Shaders, Everything. A resource-name filter ("only in code names matching") with its own case and regex toggles (UTMT-SRCH-01).
3. A results tree below, from the shared result list, grouped by resource, count per group, click to preview, double-click to open at the hit. Replace All opens a preview list with a check box per hit and one Apply (one undo step); hits in code are recompiled on Apply and errors appear in Problems.
4. Search runs on the background index; the first search after opening shows "Indexing code... {percent}" and works on what is ready. Stop (Escape) cancels. "Search refused: this game was built with YYC and has no code." when the data file is a YYC game (UTMT-FILE-06).

The Script Console (Ctrl+`) is the C# command box (UTMT-APP-05); see scripting.md.

## 8. Notifications

Rules are in ux-standards 10. Placement: bottom right, above the status bar, 360 px wide. Examples and wording:

| Event | Level | Text | Actions |
| --- | --- | --- | --- |
| Saved | Info | "Saved data.win. The previous file was kept as a backup." | Show Backup |
| Opened with warnings | Warning | "Opened with {n} warnings. Saving may lose data." | Show Problems |
| Restored unsaved changes | Info | "Restored unsaved changes from your last session." | Discard, Keep |
| Update available | Info | "G3MStudio {version} is ready to install." | What's New, Restart to Update |
| Batch edit | Info | "Deleted 12 resources." | Undo |
| Script finished | Info | "{script} finished in {seconds} s." | Show Output |
| Save failed | Error | "Could not save 'data.win'. {reason}" | Save As..., Copy Details |

## 9. Tabs and the editor area

### 9.1 Tab anatomy

28 px high (Standard). Left to right: type icon (16), name, state markers, close button (24 x 24 hit area, 16 glyph). Name truncates with an end ellipsis; tooltip shows the full path ("sprites/spr_player") and the data file.

| State | Look |
| --- | --- |
| Active | `Hover` fill, 2 px `Select` bottom bar, bold name |
| Inactive | `Bg.Raised` fill, regular name |
| Preview | Italic name (opened by single-click or double-click in the Explorer; at most one per group; replaced by the next preview) |
| Pinned | Icon only, 36 px wide, placed first, no close button (a pin overlay) |
| Dirty (unapplied edit in this editor, or edited in this session when `tabs.showModified` is on) | A filled dot replaces the close button until hover or focus |
| Problem | Red underline for errors, amber for warnings (not the only cue: a small badge icon too) |
| Read-only | Lock overlay on the icon |

A preview tab becomes a normal tab when the user edits, double-clicks the tab, or runs `tabs.keepOpen`.

### 9.2 Tab behaviour

1. Opening a resource that has a tab focuses that tab. Open in New Tab always creates another tab of the same resource only if the editor allows two views (code: yes, as a split view of the same document; others: focus the existing tab).
2. Overflow: when tabs do not fit, the strip scrolls with the mouse wheel and shows a chevron button at the right. The chevron opens a searchable list of all open tabs (`tabs.showList`). The active tab scrolls into view (UTMT-TAB-07).
3. Reorder by dragging a tab within the strip (UTMT-TAB-06). Alternative: `tabs.moveLeft` and `tabs.moveRight` in the tab menu. Drag to a group edge splits; drag outside the window floats the editor.
4. Close: Ctrl+W, close button, middle-click (UTMT-TAB-02). Close Other Tabs, Close Tabs to the Right, Close Unmodified Tabs, Close All Tabs (Ctrl+Shift+W) (UTMT-TAB-03). Reopen Closed Tab (Ctrl+Shift+T) restores from a history of the last 20 closed tabs, including scroll and caret (UTMT-TAB-04).
5. Ctrl+Tab opens the recency switcher: a list of open tabs ordered by use that stays open while Ctrl is held; Tab moves down, Shift+Tab up, releasing Ctrl opens the selection (UTMT-TAB-05). Ctrl+PageUp and Ctrl+PageDown move through tabs in strip order.
6. Per-tab view state persists across switching and sessions (UTMT-TAB-08): code (caret, scroll, zoom, sub-view), room (camera, zoom, selected layer, tool), font (selected glyph), general info (scroll, section), global init, sprite (frame, zoom, selection), tile set, texture group, texture page (page, zoom, scroll), sequence (playhead, zoom, selected track).
7. Tab context menu: Close, Close Others, Close to the Right, Close Unmodified, Pin or Unpin, Keep Open, Split Right, Split Down, Move to New Window, Float, Move to Canvas, Show in Explorer, Copy Resource Path, Show in Inspector, Open in Image Editor (sprites).
8. Navigation history (back and forward, UTMT-APP-04): per editor group, cleared when the workspace closes; Alt+Left and Alt+Right; the breadcrumb bar arrows disable at the ends.
9. A breadcrumb row above each editor shows "Data file > Type > Resource > Part" (for example "data.win > Objects > obj_player > Create"); each part is a dropdown listing siblings (the object's events, the room's layers). It is the data-file form of VS Code breadcrumbs.
10. Closing a tab with an unapplied code edit applies it first when it compiles; if it does not, a dialog asks "Apply failed. Keep editing or discard changes?" with the compile error shown.

### 9.3 Editor groups and splits

Up to four groups in a 2 x 2 grid maximum: horizontal and vertical splits, nested one level (Split Right Ctrl+\, Split Down Ctrl+K Ctrl+\). Each group has its own tab strip and navigation history. Focus a group with Ctrl+1 to Ctrl+4. Two groups can show the same code resource (decompiled and disassembly side by side). Closing the last tab of a group removes the group.

## 10. Docking

Rules (ADR-0010, F-UI-1):

1. The four regions of section 1 are fixed. Panels are content that can live in any dock: Explorer, Search, Git, Plugins, Inspector, Outline, Problems, Output, Search Results, References, Changes, History, Jobs, Console, Cards. Editors live only in the editor area, in a canvas workspace, or in a floating window.
2. Move a panel by dragging its tab to a dock edge or another dock; the drop target highlights with a 2 px `Select` outline and a text label ("Dock to Left"). Non-drag alternative: panel menu "Move To" > Left Dock, Right Dock, Bottom Panel, New Window.
3. A dock with no panels disappears. A panel closed with its X can be reopened from View.
4. Floating: a panel or editor can open as an OS window (Window > Float Panel, tab menu "Float Editor in New Window", or drag outside). Floating windows are owned by the workspace window: they minimise, restore and close with it, and they restore on the same monitor with the saved geometry.
5. Drop zones inside the editor area: centre (add as tab), left, right, top, bottom (new group). Maximum four groups; a fifth drop falls back to the centre of the nearest group.
6. Persistence: layout (dock sizes, panel placement, groups, open tabs, canvas cards, floating windows) is stored per workspace and restored exactly. A workspace opened for the first time uses the user's default layout (the preset chosen in onboarding).
7. Layout presets and named layouts: Window > Layout holds Studio, Code, Canvas, Save Layout As..., Load Layout..., Reset Layout. Reset Layout asks nothing if no editors would close; layouts do not close editors (GameMaker's reset closes them; ours keeps them). Layout files are JSON, importable and exportable.
8. Focus Editor (Ctrl+K Z) hides all docks and the panel and restores them on the second press.

## 11. Layout presets (ADR-0010)

All three have the same features; only placement differs. The preset picked in onboarding is the default for new workspaces; switching is `window.layout.*` any time and never closes an editor.

### 11.1 Studio (default)

Section 1.1. Few panels open: Explorer left, Inspector right, Problems and Output at the bottom (collapsed until something is reported), the editor area holds tabs. A Canvas tab ("Canvas 1") can be added from the tab bar's "+" menu for linked editing. Breadcrumb bar and status bar visible. Tour highlights in onboarding use this layout.

### 11.2 Code

VS Code-like and minimal.

```
+--------------------------------------------------------------------------+
| File Edit View ...                              data.win - G3MStudio  _ [] X|
+--+-----------------------+-----------------------------------------------+
|E | EXPLORER              |  obj_player > Create   | room_start | ...      |
|S |  [Filter...]          |-----------------------------------------------+
|G |  > Sprites            |   (editor)                                    |
|P |  > Objects            |                                               |
|  |  ...                  |                                               |
|  |                       |-----------------------------------------------+
|  |                       | Problems | Output | ...                       |
+--+-----------------------+-----------------------------------------------+
| status bar                                                                |
+--------------------------------------------------------------------------+
```

1. A 40 px activity bar on the far left with icon buttons: Explorer, Search, Git, Plugins, Settings (bottom). Each has a tooltip and a badge (changed files, problems).
2. No right dock; the Inspector is a bottom panel tab and a flyout (Ctrl+Shift+I toggles it as a right peek panel). Breadcrumb bar hidden (breadcrumbs sit above each editor). Title bar menu stays.
3. Everything opens in tabs. Panels appear only on demand.

### 11.3 Canvas

GameMaker-like.

```
+--------------------------------------------------------------------------+
| File Edit View ...  [Canvas 1] [Canvas 2] [+]   data.win - G3MStudio _ [] X|
+----------------+----------------------------------------------+---------+
| Explorer       |    .-------------.        .-------------.    |Inspector|
|                |    | obj_player  |------->| Events      |    |         |
|                |    | [card]      |        | Create      |    |         |
|                |    '-------------'        '------+------'    |         |
|                |                                  |            |         |
|                |                          .-------v------.     |         |
|                |                          | Code: Create |     |         |
|                |                          '--------------'     |         |
|                |                            [minimap]          |         |
+----------------+----------------------------------------------+---------+
| Output | Problems                                                         |
+--------------------------------------------------------------------------+
| status bar                                                                |
+--------------------------------------------------------------------------+
```

Docks as in Studio, the editor area is one or more canvas workspaces ("Canvas 1", "Canvas 2", renamable by double-click), and editors open as cards. Code, Room and Image editors can still maximise to a tab ("Maximize Card") and return.

## 12. Canvas workspace

A canvas workspace is a tab whose content is an infinite pannable, zoomable surface holding editor cards (F-UI-10, UTMT-free feature, ADR-0010).

### 12.1 Card

Title row 28 px: type icon, resource name, a link handle, Collapse, Maximize to Tab, Float, Close. Body is the normal editor with its own toolbar. Border 2 px: `Border` when active, `Border.Subtle` otherwise. Resize by dragging an edge (6 px grab zone); minimum 240 x 160; default sizes per editor (code 640 x 420, object 360 x 480, room 720 x 520, sprite 560 x 460).

### 12.2 Links

A link is a curve between two cards in `Text.Accent` (2 px). Links are created automatically when an editor opens another from inside it (object to its event list, event to its code) and manually with `board.link` (select two cards) or by dragging from one card's link handle to another. `board.unlink` removes it. Links make cards move together when the user chooses "Move Linked Cards With This One" in the card menu. Links are stored in the layout, not in the data file.

### 12.3 Navigation

| Action | Mouse | Keyboard |
| --- | --- | --- |
| Pan | Middle-drag, or Space then drag on empty canvas, two-finger scroll | Arrow keys when the canvas background has focus (64 px; Shift: 256 px) |
| Zoom | Ctrl+wheel, pinch | Ctrl+=, Ctrl+-, Ctrl+0 (fit), Ctrl+Shift+0 (100%) |
| Focus the nearest card | - | Ctrl+Alt+Arrow (Windows, Linux), Control+Option+Arrow (macOS) |
| Fit all cards | Toolbar | Ctrl+Shift+Home |
| Card overview | Toolbar | Ctrl+Tab switcher lists cards too |
| Move a card | Drag the title row | Select the card, then arrow keys (8 px, Shift: 64 px) |

Zoom range 10% to 400%. Snap to an 8 px grid unless Ctrl is held. The right-click menu on empty canvas has New Resource, Go To Resource, Arrange Linked Cards, Fit All. Cards also appear in a Cards panel (Window > Cards) listing every card with Focus, Maximize and Close, so every card action is possible without the canvas (WCAG 2.5.7).

### 12.4 Performance

Inactive cards, cards below 50% zoom and cards outside the view render as cached bitmaps with a title and thumbnail; they wake on focus. A canvas with 50 open cards keeps 60 fps while panning (ADR-0010). A minimap (160 x 100, bottom right, toggle) shows card rectangles and the viewport.

### 12.5 Moving between placements

Any editor moves between placements by command or drag: Tab to Canvas (`tabs.toCanvas`), Card to Tab (`tabs.toTabs`), Float (`tabs.float`). State (zoom, selection, caret) carries over.

## 13. Multi-window

1. One G3MStudio process. Each window holds one workspace (F-WS-9). New Window (Ctrl+Shift+N) opens a window on the start page. "Open in New Window..." and Shift+click on a recent entry open a workspace in a new window.
2. Opening a data file that is already open in a window focuses that window. Opening a file while the current window has a workspace asks "Open in this window, or a new window?" with "Remember my choice" (Settings > Workspace > `window.openMode`: ask, new, this).
3. The same data file cannot be edited in two windows. The second window opens it read-only with a notice ("Open Read-only Copy").
4. Opens from outside the window (`G3MStudio <file>`, `g3mstudio://`, section 13.1) go to the running process, then follow rule 2. A cold start with arguments opens the file after the window paints.
5. Window state (position, size, state, monitor, workspace, layout) is saved on every change and restored at launch for every window that was open ("Restore all windows" is on by default; `window.restore` can be `none` or `last`).
6. Floating panels and editors belong to their workspace window. Close the window and they close with their layout saved. Closing the last main window quits on Windows and Linux; on macOS the app stays running with the menu bar until Cmd+Q.
7. Multi-monitor: a floating window remembers its monitor by id and position relative to it. If the monitor is gone it opens on the primary one, fully on-screen.
8. Title bar text and the taskbar entry name the workspace so several windows are distinguishable.

## 13.1 Opening from outside the window (UTMT-FILE-12, UTMT-RUN-06, F-SUITE-5)

G3MStudio has no command line tool (questions.md Q24). The program accepts a few arguments and one link scheme so the system, file managers, shortcuts and other programs can open things in the window. Scripting and automation use G3MTool (CLI and GUI), which is not duplicated here.

| Form | Behaviour |
| --- | --- |
| `G3MStudio` | Starts the window |
| `G3MStudio <file>` | Opens a data file, `.g3mproj`, project JSON, `.g3mpatch`, `.csx` or mod `.zip` with the matching prompt (section 14.1). `<file>::<code name>` puts the cursor in that code entry |
| `G3MStudio <file> launch` | UTMT's `launch` argument: starts the game from that data file and exits without opening a window |
| `G3MStudio <file> special_launch` | UTMT's `special_launch`: opens the runner picker (run-game.md section 4), launches the choice and exits |
| `G3MStudio <file> deleteTempFolder` | Accepted for compatibility and ignored; the updater leaves no temp folder to delete |
| `G3MStudio --cleanup-after-update` | Internal; run by the updater after a restart |

If an instance is running, the open goes to it and the new process exits 0 after the handover. A cold start with a file opens the window first and the file after it paints. The shell verbs "Launch game" and "Launch with options" (UTMT-RUN-06) use the `launch` forms and the `g3mstudio://launch` link below.

The `g3mstudio://` link scheme (F-SUITE-5) mirrors G3M's `g3m://` so tools stay predictable (modding-suite "Notes for g3mstudio://"):

| Form | Action |
| --- | --- |
| `g3mstudio://file:///C:/path/data.win` | Opens a local data file, `.g3mproj`, `.g3mpatch` or mod `.zip` (the file must exist; `netloc` empty or `localhost`; UNC paths rejected; percent-decoded) |
| `g3mstudio://https://host/path.zip` | After a confirmation naming the host and file, downloads a mod or patch and opens the matching prompt |
| `g3mstudio://open?file=...&line=N` | Opens a file and puts the cursor on a line; the query uses percent-encoding |
| `g3mstudio://launch?file=...&profile=...` | Starts a launch after a confirmation |

Rules: registered per user (HKCU class key on Windows, `x-scheme-handler/g3mstudio` desktop entry on Linux, document types on macOS); the raw URL goes to a running instance, otherwise to a new one; commas are reserved for later extra data (use `%2C`); `https//` typos from browsers are repaired; no link starts anything that writes without a confirmation; a link while a game is running is refused with a warning. G3M does not know this scheme, so links meant for G3M keep using `g3m://`; "Export to G3M..." emits `g3m://file:///...` (F-SUITE-4).

## 14. Workspace lifecycle

### 14.1 Open

1. Source: Open dialog, drag and drop on the window, a recent entry, command line, a `g3mstudio://` link, or the start page. The dialog filters `.win .unx .ios .droid`, `audiogroup*.dat`, and all files (UTMT-FILE-02).
2. Checks before reading: file exists and is readable; path confinement for derived paths (UTMT-FILE-14); the file is not already open elsewhere. A read-only location (Steam folder) is allowed and flagged "Read-only location. Use Save As."
3. The window shows the workspace shell immediately with a determinate "Opening data.win... {percent}" bar inside the Explorer area and a Cancel button (ux-standards 9). The Explorer fills as chunks finish. A job log goes to the Load channel.
4. After load: load warnings (UTMT-FILE-04) go to Problems as one group "Load" and one notification. Persistent conditions show an info bar below the tab strip until dismissed, and stay listed in the status bar:

| Condition | Info bar text | Action |
| --- | --- | --- |
| YYC game (UTMT-FILE-06) | "This game was built with the YoYo Compiler. The code lives in the executable, so code search and the code editor are unavailable." | Learn More |
| Unsupported bytecode (UTMT-FILE-05) | "Bytecode version {n} is not supported. Code is read-only and saving is disabled." | Learn More |
| Debugger flag set (UTMT-FILE-07) | "The GameMaker debugger flag is set. The normal runner can hang." | Turn Off Debugger Flag |
| Unknown texture format (UTMT-FILE-08) | "Some textures use a format G3MStudio cannot show. They are kept when you save." | Show Textures |
| Read-only location | "This file is in a read-only folder." | Save As... |
| Detected audio group files | none (shown in Audio Groups) | - |

5. Restore (F-WS-2): last tabs, layout, selection and scroll for this file, keyed by path, size and MD5 (architecture.md). If the file changed since, tabs for resources that no longer exist are dropped silently.
6. Journal (F-WS-4): every edit is appended to the journal in the user data folder at once (flushed at least every second). On the next open of the same file with a journal left by a crash, a notification offers "Restored unsaved changes" with Discard.

### 14.2 Save

1. Save (Ctrl+S) commits pending editor text (code), builds the data file from memory (about 1 s for large games), writes it next to the target, validates by reading it back with G3MLib, compares chunk counts and hashes, then replaces the target atomically and rotates the backups (F-WS-5, UTMT-FILE-03). Sync in project mode uses the same steps. A locked file (game running, Steam updating) is retried, then the program that holds it is named and the edits stay in the journal; Sync retries later. Progress is a job; the UI stays usable except for edits (the workspace lock), and the save state item says "Saving data.win...".
2. If validation fails nothing is replaced: Error "Could not save 'data.win'. The written file did not match the model (chunk FONT)." with Save As..., Copy Details, and a link to the Output channel. The user's file is untouched.
3. Save As... picks a new path and moves the workspace there. Save a Copy... writes without moving. Revert to Saved reloads the file and drops the journal (confirm, lists the count of edits).
4. Backups: 1 to 3 rotating copies of the previous data file, count `files.backupCount` (default 3), kept in the user data folder, not in the game folder. Snapshots in project mode (F-WS-6) have no limit by default.
5. Autosave: the journal is always on. In project mode autosave also writes changed resources into the project within seconds. `files.autosave` (off by default, per data file) additionally writes the data file after a delay or on focus loss; with auto sync on save (default, `projects.autoSync`) a save also syncs.
6. Unsaved edits never get lost: they stay in the journal and return on the next open.
7. Pending state: status bar "Unsaved changes", title dot, Save button in the breadcrumb bar enabled.
8. Close or restart (including restart to update): project changes are saved automatically. If changes are not yet in the data file, a prompt offers Sync changes (Save in Direct mode), Do it later (changes stay in the journal) or Cancel (back to the app as if nothing was closed). `files.closeUnsaved` = `keep` skips the prompt.

### 14.3 External changes (F-WS-8)

A file watcher on the data file and project folder with a 1000 ms action delay, ignoring changes within 2000 ms of our own save (GameMaker values). On a change an info bar appears: "data.win changed on disk." with Reload, Keep My Version, Compare. With unsaved edits, Reload says "Reload and Discard {n} Edits". Setting `files.autoReload` reloads without asking when there are no unsaved edits. In project mode the choices are: bring the external changes into the project (resource-level three-way merge: base = last synced resource hashes, theirs = data file, ours = project; conflicts only where the same resource changed on both sides, resolved per resource), overwrite the data file with the project, or keep both (new workspace). Exported resources edited by an external program (Edit in External Program) are re-imported after a delay with a notification and Undo.

### 14.4 Undo

1. One undo stack per workspace; commands are grouped by editor for display (architecture.md).
2. Ctrl+Z in a focused editor undoes that editor's most recent command first; with no focused editor it undoes the latest command of the workspace. Edit > Undo History... lists the whole stack newest first (time, editor, command text) and undoes or redoes to any point (F-RES-12).
3. Coalescing: typing merges within 1 second, drags and slider scrubs merge into one step per gesture, multi-select edits are one step.
4. Text in the code editor has its own text undo until Apply; Apply is one workspace command ("Apply code to gml_Object_obj_player_Create_0").
5. Save is not an undo step. Undo works across saves until the workspace closes. Hot-exit restore re-creates the stack depth of 0 (undo is not restored).

### 14.5 Crashes, Help and About

1. Crash logging (UTMT-APP-14): an unhandled exception writes `crash.txt` (then `crash2.txt`, `crash3.txt`) into the log folder and the rolling log, then closes the window. The journal keeps unsaved edits. If `privacy.sendCrashReports` is on, a scrubbed report also goes to Sentry (F-PRIV-1); a missing key, a network failure or an exhausted quota is ignored silently. On the next start a notification says "G3MStudio closed unexpectedly last time. [Show Details] [Dismiss]"; Show Details opens the report in Output > Crash with Copy Details and "Create Support Package..." (mod-packaging.md section 14).
2. Help menu (UTMT-APP-11): the items in section 2. "About G3MStudio" opens a tab with the version, build date, commit id, G3MLib version, supported GameMaker versions, licence (GPL-3.0-only), Copy Version Info and Open Log Folder. "Licences" shows a grid of third-party components; each row opens its licence text (the GameMaker About pattern). "Community" lists the Boosty, Telegram, Discord and website links (TOOL-58), each opening in the browser after the standard external link confirmation on first use.

## 15. Empty states

Every empty state says what to do next (GameMaker habit) and offers one action.

| Place | Text | Action |
| --- | --- | --- |
| No workspace (start page) | Section below | - |
| Editor area with workspace but no tabs | "Nothing open. Press Ctrl+P to find a resource, or double-click one in the Explorer." with a watermark logo and the three shortcuts Go to Resource, Command Palette, Find in Resources | Open Resource... |
| Explorer, filter with no matches | "No resources match '{filter}'." | Clear Filter |
| Explorer, type with zero items | "No {type} in this data file." | New {Type}... |
| Inspector, nothing selected | "Select a resource to see its properties." | - |
| Problems | "No problems found in '{name}'." | - |
| Output channel | "Nothing logged yet." | - |
| Search results, before a query | "Type to search code, strings and names." | - |
| Search results, no hits | "No results for '{query}' in {scope}. Try Match Case off or a wider scope." | Search Everything |
| References | "Nothing uses '{name}'." | - |
| Changes | "No changes since opening." | - |
| History | "No snapshots yet. Enable the project to keep snapshots." | Enable Project |
| Jobs | "No running jobs." | - |
| Plugins | "No plugins installed. Install one from a folder, a zip or a GitHub URL." | Install Plugin... |
| Git panel, not a repository | "This project is not a Git repository." | Initialize Repository |
| Git panel, data file mode | "Git tracks the project folder. Enable the project for this data file to use Git." | Enable Project |
| Canvas workspace, no cards | "Drag a resource here, or right-click for New Resource." | New Resource... |
| Script console | "Type C# and press Enter. The globals are Data, FilePath and Selected." | Show Help |

### Start page

Shown only when a window has no workspace; it is a tab-less full-area view (F-WS-3, vscode-zed recommendation 7). Closing the last tab of a workspace does not bring it back.

```
+------------------------------------------------------------------+
|                           G3MStudio                      (Display)|
|  Start                                                           |
|  [Open Data File...]  [Open Project Folder...]  [New Mod...]     |
|                                                                  |
|  Recent                                    [grid] [list]   [Remove]|
|  +-----------------+  +-----------------+                        |
|  | [icon] data.win |  | [icon] game.ios |                        |
|  | Undertale 2023.8|  | Pizza Tower     |                        |
|  | C:\...\data.win |  | /Users/...      |                        |
|  | Opened today    |  | Opened 3 days   |                        |
|  +-----------------+  +-----------------+                        |
|  Pinned (star on a card)                                         |
|  Drop a data file, patch, mod or script here to open it.         |
|  Getting Started  |  Interface Tour  |  Release Notes  |  Docs   |
+------------------------------------------------------------------+
```

Cards show the game name from GEN8, the GameMaker version, the path and last opened time. Remove from list is per card (menu) and "Clear Recently Opened". Pin with a star. A missing file shows "File not found" in `Warning` with Remove. The dropped file types: data files, `.g3mpatch`, `.xdelta`, `.zip` mods, `.csx` (UTMT-FILE-13).

## 16. Keyboard map

Every shortcut, by the context where it works. The registry enforces: within one context a key has one command; a key may repeat across contexts only when the contexts cannot be focused at the same time, or when both bindings carry the `when` clauses of the table in 16.1. The map is generated from the registry and checked by a test (no duplicates, no chord prefix clashes, platform conversions applied).

Rules:

1. Windows and Linux use Ctrl and Alt; macOS uses Cmd and Option. Where macOS differs on purpose (Redo, Find Next, Go to Line, Back, Toggle Right Dock, Full Screen) the table says so.
2. No default combines Ctrl+Alt with a printable character in a text field, because AltGr produces Ctrl+Alt on Windows keyboards (ux-standards 3). Ctrl+Alt+Arrow keys are used only in canvases and the code editor, where AltGr is not involved.
3. Alt+letter shortcuts exist only inside the Search panel text field, where VS Code users expect them.
4. Single-letter and number keys act only when a canvas or list has focus, never when a text field has focus, and never during IME composition.
5. Chords wait 2 seconds for the second key; Escape cancels.
6. Keymap presets (`keybindings.preset`): Studio (this map), VS Code, GameMaker (GameMaker's 514 defaults mapped by command name where a command exists), UTMT (the UTMT-KEY-01 to 06 keys). A preset only rebinds. Users override single keys; overrides are stored in `keybindings.json` and conflicts are shown in the shortcut editor with both commands listed.

Notes on UTMT habits that differ from the default: compile or assemble is F7 instead of Ctrl+K (Ctrl+K starts chords); find unreferenced and project search keep Ctrl+Shift+F; the room "minus and plus" reorder keys are bound as well as Alt+Up and Alt+Down.

### 16.1 Shadowing and `when` contexts

Every chord that appears in more than one overlapping context carries an explicit `when` on each binding, so no two bindings fire in the same context. The global binding's `when` excludes every scoped context; a scoped binding's `when` names its context. Context keys are in section 3.1 and the `when` of each context table is in 16.3. A chord that is not in this table repeats only between different editors or panels, which cannot hold focus at the same time (the duplicate scan in 16.4).

| Key (Windows and Linux) | Global command | Global `when` | Scoped command | Scoped `when` |
| --- | --- | --- | --- | --- |
| Ctrl+F | Find (`edit.find`) | `editorFocus && editorType != 'settings'` | Filter Resources (`explorer.filter`) | `explorerFocus` |
| Ctrl+F | Find (`edit.find`) | as above | Focus Settings Search (`settings.focusSearch`) | `editorType == 'settings'` |
| F2 | Rename (`edit.rename`) | `!(editorType in ['code', 'object', 'font'])` | Rename Symbol (`code.rename`) | `editorType == 'code'` |
| F2 | Rename (`edit.rename`) | as above | Change Event Type... (`objects.changeEvent`) | `editorType == 'object'` |
| F2 | Rename (`edit.rename`) | as above | Edit Glyph Rectangle... (`fonts.editRect`) | `editorType == 'font'` |
| Ctrl+D | Duplicate (`edit.duplicate`) | `!(editorType in ['code', 'filterEffects'])` | Add Next Occurrence (`code.selectNext`) | `editorType == 'code'` |
| Ctrl+D | Duplicate (`edit.duplicate`) | as above | Duplicate Effect Definition (`effects.duplicate`) | `editorType == 'filterEffects'` |
| Ctrl+= | Zoom In (`view.zoomIn`) | `!canvasFocus && editorType != 'code'` | Zoom Editor In (`code.zoomIn`) | `editorType == 'code'` |
| Ctrl+= | Zoom In (`view.zoomIn`) | as above | Zoom In (`canvas.zoomIn`) | `canvasFocus` |
| Ctrl+- | Zoom Out (`view.zoomOut`) | `!canvasFocus && editorType != 'code'` | Zoom Editor Out (`code.zoomOut`) | `editorType == 'code'` |
| Ctrl+- | Zoom Out (`view.zoomOut`) | as above | Zoom Out (`canvas.zoomOut`) | `canvasFocus` |
| Ctrl+0 | Reset Zoom (`view.zoomReset`) | `!canvasFocus && editorType != 'code'` | Reset Editor Zoom (`code.zoomReset`) | `editorType == 'code'` |
| Ctrl+0 | Reset Zoom (`view.zoomReset`) | as above | Fit to View (`canvas.fit`) | `canvasFocus` |
| Ctrl+G | Go to Line or Index... (`go.line`) | `editorType != 'chunk'` | Go to Offset (`chunk.goToOffset`) | `editorType == 'chunk'` |
| Ctrl+C | Copy (`edit.copy`) | `editorType != 'strings' && focusedPanel != 'problems'` | Copy String (`strings.copy`) | `editorType == 'strings'` |
| Ctrl+C | Copy (`edit.copy`) | as above | Copy Problem (`problems.copy`) | `focusedPanel == 'problems'` |
| Ctrl+Shift+F | Find in Resources (`edit.findInResources`) | `focusedPanel != 'search'` | Focus Search Box (`search.focus`) | `focusedPanel == 'search'` |
| F5 | Launch Game (`run.game`) | `focusedPanel != 'git'` | Refresh (`gitpanel.refresh`) | `focusedPanel == 'git'` |
| F3 | Find Next (`edit.findNext`) | `editorType != 'diff'` | Next Change (`diffview.nextChange`) | `editorType == 'diff'` |
| Shift+F3 | Find Previous (`edit.findPrevious`) | `editorType != 'diff'` | Previous Change (`diffview.previousChange`) | `editorType == 'diff'` |
| Alt+Left | Back (`go.back`) | `editorType != 'languages'` | Move Language Left (`languages.moveLeft`) | `editorType == 'languages'` |
| Alt+Right | Forward (`go.forward`) | `editorType != 'languages'` | Move Language Right (`languages.moveRight`) | `editorType == 'languages'` |
| Ctrl+Enter | none |  | Open in New Tab (`resource.openNewTab`) | `explorerFocus && !modalOpen` |
| Ctrl+Enter | none |  | Apply Tile Edits (`roomtiles.apply`) | `editorType == 'room' && roomMode == 'tiles'` |
| Ctrl+Enter | none |  | Commit Staged Changes (`gitpanel.commit`) | `focusedPanel == 'git'` |
| Ctrl+Enter | none |  | Run Operation (`patches.run`) | `focusedPanel == 'patches'` |
| Ctrl+Enter | none |  | Open in New Tab (result in the palette, 3.2) | `modalOpen` |
| Ctrl+Alt+Up | none |  | Add Cursor Above (`code.cursorAbove`) | `editorType == 'code'` |
| Ctrl+Alt+Up | none |  | Focus Card Above (`board.focusUp`) | `boardFocus` |
| Ctrl+Alt+Down | none |  | Add Cursor Below (`code.cursorBelow`) | `editorType == 'code'` |
| Ctrl+Alt+Down | none |  | Focus Card Below (`board.focusDown`) | `boardFocus` |
| Space | none |  | Pan (Hold, Then Drag) (`canvas.pan`) | `canvasFocus && !boardFocus` |
| Space | none |  | Pan Canvas (Hold, Then Drag) (`board.pan`) | `boardFocus` |
| Delete | Delete (`edit.delete`) | `!scopedDelete` | the Delete binding of each context in 16.2 that has one | that context's `when` from 16.3 |

`scopedDelete` is true when the focused context is one of the 19 contexts of 16.2 that bind Delete (rooms in instance and layer mode, sprites, image, objects, fonts, paths, timelines, sequences, animation curves, particle systems, variables and locals, Git panel, mod editor, settings and shortcuts, filter effects, tags, feature flags, embedded images, unexported list). In the Explorer, `edit.delete` runs.

`canvasFocus` is true while an editor of the canvas family has focus, including an editor card on a canvas workspace. `boardFocus` is true only while the board surface itself has focus. The two are never true together.

### 16.2 Tables by context

#### Global (always active)

| Windows and Linux | macOS | Command | Id |
| --- | --- | --- | --- |
| Ctrl+N | Cmd+N | New Data File | `file.new.dataFile` |
| Ctrl+O | Cmd+O | Open Data File... | `file.open` |
| Ctrl+K Ctrl+O | Cmd+K Cmd+O | Open Project Folder... | `file.openFolder` |
| Ctrl+K F | Cmd+K F | Close Workspace | `file.close` |
| Ctrl+S | Cmd+S | Save | `file.save` |
| Ctrl+Shift+S | Cmd+Shift+S | Save As... | `file.saveAs` |
| Ctrl+, | Cmd+, | Settings | `app.settings` |
| Ctrl+K Ctrl+S | Cmd+K Cmd+S | Keyboard Shortcuts | `app.keyboardShortcuts` |
| Ctrl+K Ctrl+T | Cmd+K Cmd+T | Color Theme... | `app.colorTheme` |
| Ctrl+Q | Cmd+Q | Quit | `app.quit` |
| Ctrl+Z | Cmd+Z | Undo | `edit.undo` |
| Ctrl+Y / Ctrl+Shift+Z | Cmd+Shift+Z | Redo | `edit.redo` |
| Ctrl+X | Cmd+X | Cut | `edit.cut` |
| Ctrl+C | Cmd+C | Copy | `edit.copy` |
| Ctrl+V | Cmd+V | Paste | `edit.paste` |
| Delete | Cmd+Backspace / Delete | Delete | `edit.delete` |
| Ctrl+A | Cmd+A | Select All | `edit.selectAll` |
| Ctrl+D | Cmd+D | Duplicate | `edit.duplicate` |
| F2 | F2 / Return | Rename | `edit.rename` |
| Ctrl+F | Cmd+F | Find | `edit.find` |
| Ctrl+H | Cmd+Option+F | Replace | `edit.replace` |
| F3 | Cmd+G | Find Next | `edit.findNext` |
| Shift+F3 | Cmd+Shift+G | Find Previous | `edit.findPrevious` |
| Ctrl+Shift+F | Cmd+Shift+F | Find in Resources | `edit.findInResources` |
| Ctrl+Shift+H | Cmd+Shift+H | Replace in Resources | `edit.replaceInResources` |
| Ctrl+Shift+P / F1 | Cmd+Shift+P / F1 | Command Palette... | `view.commandPalette` |
| Ctrl+Shift+E | Cmd+Shift+E | Explorer | `view.explorer` |
| Ctrl+Shift+G | Control+Shift+G | Git | `view.git` |
| Ctrl+Shift+M | Cmd+Shift+M | Problems | `view.problems` |
| Ctrl+Shift+U | Cmd+Shift+U | Output | `view.output` |
| Ctrl+Shift+I | Cmd+Shift+I | Inspector | `view.inspector` |
| Ctrl+` | Control+` | Script Console | `view.console` |
| Ctrl+Shift+X | Cmd+Shift+X | Plugins | `view.plugins` |
| Ctrl+B | Cmd+B | Show or Hide Left Dock | `view.toggleLeftDock` |
| Ctrl+Shift+B | Cmd+Option+B | Show or Hide Right Dock | `view.toggleRightDock` |
| Ctrl+J | Cmd+J | Show or Hide Bottom Panel | `view.toggleBottomPanel` |
| Ctrl+K Z | Cmd+K Z | Focus Editor (Hide All Docks) | `view.focusEditor` |
| F11 | Control+Cmd+F | Full Screen | `view.fullScreen` |
| Ctrl+= | Cmd+= | Zoom In | `view.zoomIn` |
| Ctrl+- | Cmd+- | Zoom Out | `view.zoomOut` |
| Ctrl+0 | Cmd+0 | Reset Zoom | `view.zoomReset` |
| F6 | F6 | Focus Next Region | `view.nextRegion` |
| Shift+F6 | Shift+F6 | Focus Previous Region | `view.previousRegion` |
| F10 | F10 | Focus Menu Bar | `view.menuBar` |
| Ctrl+P | Cmd+P | Go to Resource... | `go.quickOpen` |
| Ctrl+Shift+O | Cmd+Shift+O | Go to Symbol in Editor... | `go.symbolInEditor` |
| Ctrl+T | Cmd+T | Go to Symbol in Data File... | `go.symbolInWorkspace` |
| Ctrl+G | Control+G | Go to Line or Index... | `go.line` |
| F12 | F12 | Go to Definition | `go.definition` |
| Alt+F12 | Option+F12 | Peek Definition | `go.peekDefinition` |
| Shift+F12 | Shift+F12 | Find Usages | `go.references` |
| Alt+Left | Control+- | Back | `go.back` |
| Alt+Right | Control+Shift+- | Forward | `go.forward` |
| F8 | F8 | Next Problem | `go.nextProblem` |
| Shift+F8 | Shift+F8 | Previous Problem | `go.previousProblem` |
| Ctrl+K Ctrl+Q | Cmd+K Cmd+Q | Last Edit Location | `go.lastEdit` |
| Ctrl+K R | Cmd+K R | Reveal in Explorer | `go.revealInExplorer` |
| Ctrl+Shift+A | Cmd+Shift+A | New Resource... | `resource.new` |
| Ctrl+Shift+R | Cmd+Shift+R | Run Script... | `scripts.run` |
| F5 | F5 | Launch Game | `run.game` |
| Alt+F5 | Option+F5 | Launch With Options... | `run.withOptions` |
| Shift+F5 | Shift+F5 | Stop Game | `run.stop` |
| Ctrl+Shift+F5 | Cmd+Shift+F5 | Restart Game | `run.restart` |
| Ctrl+Shift+N | Cmd+Shift+N | New Window | `window.new` |

#### Tabs and editor groups (always active)

| Windows and Linux | macOS | Command | Id |
| --- | --- | --- | --- |
| Ctrl+W | Cmd+W | Close Tab | `tabs.close` |
| Ctrl+Shift+W | Cmd+Option+W | Close All Tabs | `tabs.closeAll` |
| Ctrl+Shift+T | Cmd+Shift+T | Reopen Closed Tab | `tabs.reopenClosed` |
| Ctrl+Tab | Control+Tab | Next Recent Tab | `tabs.next` |
| Ctrl+Shift+Tab | Control+Shift+Tab | Previous Recent Tab | `tabs.previous` |
| Ctrl+PageDown | Cmd+Option+Right | Next Tab in Group | `tabs.nextInGroup` |
| Ctrl+PageUp | Cmd+Option+Left | Previous Tab in Group | `tabs.previousInGroup` |
| Ctrl+K Shift+Enter | Cmd+K Shift+Enter | Pin Tab | `tabs.pin` |
| Ctrl+K Enter | Cmd+K Enter | Keep Tab Open | `tabs.keepOpen` |
| Ctrl+\ | Cmd+\ | Split Right | `tabs.splitRight` |
| Ctrl+K Ctrl+\ | Cmd+K Cmd+\ | Split Down | `tabs.splitDown` |
| Ctrl+1 to Ctrl+4 | Cmd+1 to Cmd+4 | Focus Editor Group 1 to 4 | `tabs.focusGroup` |
| Ctrl+Shift+PageUp | Cmd+Shift+Option+Left | Move Tab Left | `tabs.moveLeft` |
| Ctrl+Shift+PageDown | Cmd+Shift+Option+Right | Move Tab Right | `tabs.moveRight` |

#### Explorer focused

| Windows and Linux | macOS | Command | Id |
| --- | --- | --- | --- |
| Enter | Enter | Open | `resource.open` |
| Ctrl+Enter | Cmd+Enter | Open in New Tab | `resource.openNewTab` |
| Ctrl+Shift+C | Cmd+Shift+C | Copy Name | `resource.copyName` |
| Ctrl+] | Cmd+] | Move Up in Custom Order | `resource.moveUp` |
| Ctrl+[ | Cmd+[ | Move Down in Custom Order | `resource.moveDown` |
| Ctrl+Shift+] | Cmd+Shift+] | Move to Top in Custom Order | `resource.moveTop` |
| Ctrl+Shift+[ | Cmd+Shift+[ | Move to Bottom in Custom Order | `resource.moveBottom` |
| Ctrl+F | Cmd+F | Filter Resources | `explorer.filter` |

#### Code editor focused

| Windows and Linux | macOS | Command | Id |
| --- | --- | --- | --- |
| F7 | F7 | Apply (Compile or Assemble) | `code.apply` |
| F4 | F4 | Switch Decompiled and Disassembly | `code.toggleView` |
| F2 | F2 | Rename Symbol | `code.rename` |
| Ctrl+. | Cmd+. | Quick Fix | `code.quickFix` |
| Ctrl+Space | Control+Space | Trigger Suggestions | `code.suggest` |
| Ctrl+Shift+Space | Cmd+Shift+Space | Parameter Hints | `code.parameterHints` |
| Ctrl+Shift+[ | Cmd+Option+[ | Fold | `code.fold` |
| Ctrl+Shift+] | Cmd+Option+] | Unfold | `code.unfold` |
| Ctrl+K Ctrl+0 | Cmd+K Cmd+0 | Fold All | `code.foldAll` |
| Ctrl+K Ctrl+J | Cmd+K Cmd+J | Unfold All | `code.unfoldAll` |
| Ctrl+/ | Cmd+/ | Toggle Line Comment | `code.toggleComment` |
| Shift+Alt+A | Shift+Option+A | Toggle Block Comment | `code.blockComment` |
| Shift+Alt+Down | Shift+Option+Down | Duplicate Line | `code.duplicateLine` |
| Alt+Up | Option+Up | Move Line Up | `code.moveLineUp` |
| Alt+Down | Option+Down | Move Line Down | `code.moveLineDown` |
| Ctrl+D | Cmd+D | Add Next Occurrence | `code.selectNext` |
| Ctrl+Shift+L | Cmd+Shift+L | Select All Occurrences | `code.selectAllOccurrences` |
| Ctrl+Alt+Up | Cmd+Option+Up | Add Cursor Above | `code.cursorAbove` |
| Ctrl+Alt+Down | Cmd+Option+Down | Add Cursor Below | `code.cursorBelow` |
| Ctrl+Shift+\ | Cmd+Shift+\ | Go to Matching Brace | `code.matchBrace` |
| Ctrl+= | Cmd+= | Zoom Editor In | `code.zoomIn` |
| Ctrl+- | Cmd+- | Zoom Editor Out | `code.zoomOut` |
| Ctrl+0 | Cmd+0 | Reset Editor Zoom | `code.zoomReset` |

#### Any canvas editor (sprite, image, tile set, texture page, room, path, particle system, animation curve)

| Windows and Linux | macOS | Command | Id |
| --- | --- | --- | --- |
| Ctrl+= | Cmd+= | Zoom In | `canvas.zoomIn` |
| Ctrl+- | Cmd+- | Zoom Out | `canvas.zoomOut` |
| Ctrl+0 | Cmd+0 | Fit to View | `canvas.fit` |
| Ctrl+Shift+0 | Cmd+Shift+0 | Actual Size (100%) | `canvas.actualSize` |
| Space | Space | Pan (Hold, Then Drag) | `canvas.pan` |
| G | G | Show or Hide Grid | `canvas.grid` |
| Shift+G | Shift+G | Snap to Grid | `canvas.snap` |
| Shift+Space | Shift+Space | Play or Pause Preview | `canvas.play` |
| Left / Right / Up / Down | Left / Right / Up / Down | Nudge Selection | `canvas.nudge` |
| Shift+Left / Shift+Right / Shift+Up / Shift+Down | Shift+Left / Shift+Right / Shift+Up / Shift+Down | Nudge Selection by 8 | `canvas.nudgeLarge` |
| Escape | Escape | Cancel Current Tool or Selection | `canvas.cancel` |

#### Room editor, instance and layer mode

| Windows and Linux | macOS | Command | Id |
| --- | --- | --- | --- |
| Insert | Insert | New Layer | `rooms.newLayer` |
| Delete | Delete | Delete Layer | `rooms.deleteLayer` |
| H | H | Show or Hide Layer | `rooms.toggleVisible` |
| K | K | Lock or Unlock Layer | `rooms.toggleLock` |
| + / Alt+Up | + / Option+Up | Move Forward | `rooms.moveForward` |
| - / Alt+Down | - / Option+Down | Move Backward | `rooms.moveBackward` |
| X | X | Flip Horizontally | `rooms.flipX` |
| Y | Y | Flip Vertically | `rooms.flipY` |
| Ctrl+Shift+V | Cmd+Shift+V | Paste Without Snapping | `rooms.pasteNoSnap` |
| P | P | Select From Any Layer (Hold) | `rooms.selectAnyLayer` |
| V | V | Show Views | `rooms.showViews` |
| Ctrl | Ctrl | Halve Grid While Dragging (Hold) | `rooms.gridHalf` |
| Shift | Shift | Double Grid While Dragging (Hold) | `rooms.gridDouble` |
| Alt | Option | Paint Copies (Hold, Then Drag) | `rooms.paintCopies` |
| T | T | Edit Tiles | `rooms.editTiles` |

#### Room editor, tile painting mode

| Windows and Linux | macOS | Command | Id |
| --- | --- | --- | --- |
| 1 | 1 | Pencil | `roomtiles.pencil` |
| 2 | 2 | Eraser | `roomtiles.eraser` |
| 3 | 3 | Fill | `roomtiles.fill` |
| 4 | 4 | Line | `roomtiles.line` |
| 5 | 5 | Rectangle | `roomtiles.rectangle` |
| 6 | 6 | Selection | `roomtiles.select` |
| 7 | 7 | Auto Tile | `roomtiles.autoTile` |
| X | X | Mirror Brush | `roomtiles.mirror` |
| Y / Z | Y / Z | Flip Brush | `roomtiles.flip` |
| R | R | Rotate Brush Clockwise | `roomtiles.rotateCw` |
| Q | Q | Rotate Brush Counterclockwise | `roomtiles.rotateCcw` |
| B | B | Tile Multi-Tile Brush | `roomtiles.brushTiling` |
| P | P | Show Room Preview | `roomtiles.preview` |
| Shift+Click | Shift+Click | Replace Tile Everywhere (Shift+Fill) | `roomtiles.globalReplace` |
| Alt+Click | Option+Click | Pick Tile (Alt+Click or Middle Click) | `roomtiles.pick` |
| Ctrl+Enter | Cmd+Enter | Apply Tile Edits | `roomtiles.apply` |

#### Sprite editor

| Windows and Linux | macOS | Command | Id |
| --- | --- | --- | --- |
| Insert | Insert | Add Frame | `sprites.addFrame` |
| Shift+Insert | Shift+Insert | Insert Frame Before | `sprites.insertFrame` |
| Ctrl+Shift+Left | Cmd+Shift+Left | Move Frame Left | `sprites.moveFrameLeft` |
| Ctrl+Shift+Right | Cmd+Shift+Right | Move Frame Right | `sprites.moveFrameRight` |
| Delete | Delete | Delete Selected Frames | `sprites.deleteFrames` |
| PageUp | PageUp | Previous Frame | `sprites.previousFrame` |
| PageDown | PageDown | Next Frame | `sprites.nextFrame` |
| Home | Home | First Frame | `sprites.firstFrame` |
| End | End | Last Frame | `sprites.lastFrame` |
| M | M | Edit Mask Bounds | `sprites.editMask` |
| O | O | Show Onion Skin | `sprites.onionSkin` |

#### Image editor

| Windows and Linux | macOS | Command | Id |
| --- | --- | --- | --- |
| D | D | Pencil | `image.pencil` |
| E | E | Eraser | `image.eraser` |
| F | F | Fill | `image.fill` |
| L | L | Line | `image.line` |
| R | R | Rectangle | `image.rectangle` |
| C | C | Ellipse | `image.ellipse` |
| Y | Y | Polygon | `image.polygon` |
| I | I | Colour Picker | `image.picker` |
| S | S | Rectangle Select | `image.select` |
| W | W | Magic Wand | `image.wand` |
| V | V | Move | `image.move` |
| X | X | Swap Primary and Secondary Colour | `image.swapColours` |
| [ | [ | Smaller Brush | `image.brushSmaller` |
| ] | ] | Larger Brush | `image.brushLarger` |
| Ctrl+I | Cmd+I | Invert Selection | `image.invertSelection` |
| Insert | Insert | Add Frame | `image.addFrame` |
| Delete | Delete | Delete Frames | `image.deleteFrames` |
| PageUp | PageUp | Previous Frame | `image.previousFrame` |
| PageDown | PageDown | Next Frame | `image.nextFrame` |
| O | O | Onion Skin | `image.onionSkin` |

#### Tile set editor

| Windows and Linux | macOS | Command | Id |
| --- | --- | --- | --- |
| Insert | Insert | Add Tile Animation | `tilesets.addAnimation` |
| N | N | Show Tile Numbers | `tilesets.showNumbers` |

#### Texture page viewer

| Windows and Linux | macOS | Command | Id |
| --- | --- | --- | --- |
| PageDown | PageDown | Next Texture Page | `texpage.nextPage` |
| PageUp | PageUp | Previous Texture Page | `texpage.previousPage` |
| B | B | Show Item Bounds | `texpage.toggleBounds` |

#### Object editor

| Windows and Linux | macOS | Command | Id |
| --- | --- | --- | --- |
| Insert | Insert | Add Event... | `objects.addEvent` |
| Delete | Delete | Delete Event | `objects.deleteEvent` |
| F2 | F2 | Change Event Type... | `objects.changeEvent` |
| Enter | Enter | Open Event Code | `objects.openEventCode` |

#### Sound editor

| Windows and Linux | macOS | Command | Id |
| --- | --- | --- | --- |
| Space | Space | Play or Pause | `sounds.play` |
| Home | Home | Rewind | `sounds.rewind` |
| L | L | Loop | `sounds.loop` |

#### Font editor

| Windows and Linux | macOS | Command | Id |
| --- | --- | --- | --- |
| Ctrl+R | Cmd+R | Regenerate Font Texture | `fonts.regenerate` |
| Delete | Delete | Delete Character Range | `fonts.deleteRange` |
| Insert | Insert | Create Empty Glyph | `fonts.addGlyph` |
| F2 | F2 | Edit Glyph Rectangle... | `fonts.editRect` |

#### Path editor

| Windows and Linux | macOS | Command | Id |
| --- | --- | --- | --- |
| T | T | Translate Tool | `paths.tool.translate` |
| R | R | Rotate Tool | `paths.tool.rotate` |
| S | S | Scale Tool | `paths.tool.scale` |
| Delete | Delete | Delete Points | `paths.deletePoints` |
| Enter | Enter | Pan to Point | `paths.panToPoint` |

#### Timeline editor

| Windows and Linux | macOS | Command | Id |
| --- | --- | --- | --- |
| Insert | Insert | Add Moment | `timelines.addMoment` |
| Delete | Delete | Delete Moment | `timelines.deleteMoment` |
| Enter | Enter | Open Moment Code | `timelines.openCode` |

#### Sequence editor, dope sheet

| Windows and Linux | macOS | Command | Id |
| --- | --- | --- | --- |
| Insert | Insert | Add Keyframe at Playhead | `sequences.addKey` |
| Delete | Delete | Delete Keyframes | `sequences.deleteKeys` |
| Ctrl+Right | Cmd+Right | Next Keyframe | `sequences.nextKey` |
| Ctrl+Left | Cmd+Left | Previous Keyframe | `sequences.previousKey` |
| Right | Right | Next Frame | `sequences.nextFrame` |
| Left | Left | Previous Frame | `sequences.previousFrame` |
| A | A | Record Changes Automatically | `sequences.autoKey` |
| M | M | Add Moment at Playhead | `sequences.addMoment` |
| B | B | Add Broadcast Message at Playhead | `sequences.addBroadcast` |

#### Animation curve editor

| Windows and Linux | macOS | Command | Id |
| --- | --- | --- | --- |
| Insert | Insert | Add Channel | `curves.addCurve` |
| Delete | Delete | Delete Channel | `curves.deleteCurve` |
| Ctrl+R | Cmd+R | Set Visual Range... | `curves.setRange` |

#### Particle system editor

| Windows and Linux | macOS | Command | Id |
| --- | --- | --- | --- |
| Insert | Insert | Add Emitter | `particles.addEmitter` |
| Delete | Delete | Delete Emitter | `particles.deleteEmitter` |
| Ctrl+Shift+Space | Cmd+Shift+Space | Restart Preview | `particles.restart` |

#### Extension editor

| Windows and Linux | macOS | Command | Id |
| --- | --- | --- | --- |
| Insert | Insert | Add Function | `extensions.addFunction` |

#### Shader editor

| Windows and Linux | macOS | Command | Id |
| --- | --- | --- | --- |
| F4 | F4 | Switch Vertex and Fragment | `shaders.switchStage` |

#### General info editor

| Windows and Linux | macOS | Command | Id |
| --- | --- | --- | --- |
| Insert | Insert | Add Constant | `generalinfo.addConstant` |

#### Strings editor

| Windows and Linux | macOS | Command | Id |
| --- | --- | --- | --- |
| Insert | Insert | New String | `strings.new` |
| Ctrl+C | Cmd+C | Copy String | `strings.copy` |

#### Variables, functions and code locals

| Windows and Linux | macOS | Command | Id |
| --- | --- | --- | --- |
| Insert | Insert | Add Local Variable | `symbols.addLocal` |
| Alt+Up | Option+Up | Move Entry Up | `symbols.entryUp` |
| Alt+Down | Option+Down | Move Entry Down | `symbols.entryDown` |
| Delete | Delete | Remove Entry | `symbols.entryRemove` |

#### Search panel

| Windows and Linux | macOS | Command | Id |
| --- | --- | --- | --- |
| Ctrl+Shift+F | Cmd+Shift+F | Focus Search Box | `search.focus` |
| Alt+C | Option+C | Match Case | `search.toggleCase` |
| Alt+W | Option+W | Whole Word | `search.toggleWord` |
| Alt+R | Option+R | Regular Expression | `search.toggleRegex` |
| Alt+M | Option+M | Multiline Regular Expression | `search.toggleMultiline` |
| Alt+A | Option+A | Search Disassembly | `search.toggleAssembly` |
| Ctrl+Alt+Enter | Cmd+Option+Enter | Replace All | `search.replaceAll` |
| Ctrl+Shift+1 | Cmd+Shift+1 | Replace This Result | `search.replaceOne` |
| F4 | F4 | Next Result | `search.next` |
| Shift+F4 | Shift+F4 | Previous Result | `search.previous` |
| Escape | Escape | Stop Search | `search.stop` |
| Alt+Up / Alt+Down | Option+Up / Option+Down | Previous Queries | `search.history` |

#### Problems panel

| Windows and Linux | macOS | Command | Id |
| --- | --- | --- | --- |
| Ctrl+. | Cmd+. | Apply Fix | `problems.fix` |
| Ctrl+C | Cmd+C | Copy Problem | `problems.copy` |
| Enter | Enter | Open Problem Location | `problems.open` |

#### Script console

| Windows and Linux | macOS | Command | Id |
| --- | --- | --- | --- |
| Enter | Enter | Run | `console.run` |
| Shift+Enter | Shift+Enter | Insert New Line | `console.newline` |
| Up / Down | Up / Down | Previous or Next Command | `console.history` |
| Escape | Escape | Cancel Running Script | `console.cancel` |
| Ctrl+L | Cmd+L | Clear Console | `console.clear` |

#### Git panel

| Windows and Linux | macOS | Command | Id |
| --- | --- | --- | --- |
| Ctrl+Enter | Cmd+Enter | Commit Staged Changes | `gitpanel.commit` |
| Space | Space | Stage or Unstage Selected | `gitpanel.toggleStage` |
| Enter | Enter | Open Diff | `gitpanel.openDiff` |
| Delete | Delete | Discard Changes... | `gitpanel.discard` |
| F5 | F5 | Refresh | `gitpanel.refresh` |

#### Patch tools

| Windows and Linux | macOS | Command | Id |
| --- | --- | --- | --- |
| Ctrl+Enter | Cmd+Enter | Run Operation | `patches.run` |
| Escape | Escape | Cancel Operation | `patches.cancel` |

#### Mod editor

| Windows and Linux | macOS | Command | Id |
| --- | --- | --- | --- |
| Insert | Insert | Add Operation | `modpkg.addOperation` |
| Delete | Delete | Delete Operation | `modpkg.deleteOperation` |
| Alt+Up | Option+Up | Move Operation Up | `modpkg.moveUp` |
| Alt+Down | Option+Down | Move Operation Down | `modpkg.moveDown` |

#### Diff views

| Windows and Linux | macOS | Command | Id |
| --- | --- | --- | --- |
| F3 | F3 | Next Change | `diffview.nextChange` |
| Shift+F3 | Shift+F3 | Previous Change | `diffview.previousChange` |

#### Settings and keyboard shortcuts

| Windows and Linux | macOS | Command | Id |
| --- | --- | --- | --- |
| Enter | Enter | Record Key | `settings.recordKey` |
| Delete | Delete | Clear Key Binding | `settings.clearKey` |
| Ctrl+F | Cmd+F | Focus Settings Search | `settings.focusSearch` |

#### Hex view

| Windows and Linux | macOS | Command | Id |
| --- | --- | --- | --- |
| Ctrl+G | Control+G | Go to Offset | `chunk.goToOffset` |
| Insert | Insert | Add Tag | `chunk.addTag` |

#### Filter effects editor

| Windows and Linux | macOS | Command | Id |
| --- | --- | --- | --- |
| Insert | Insert | New Effect Definition | `effects.new` |
| Ctrl+D | Cmd+D | Duplicate Effect Definition | `effects.duplicate` |
| Delete | Delete | Delete Effect Definition | `effects.delete` |

#### Languages editor

| Windows and Linux | macOS | Command | Id |
| --- | --- | --- | --- |
| Insert | Insert | Add Entry | `languages.addEntry` |
| Alt+Left | Option+Left | Move Language Left | `languages.moveLeft` |
| Alt+Right | Option+Right | Move Language Right | `languages.moveRight` |

#### Tags editor

| Windows and Linux | macOS | Command | Id |
| --- | --- | --- | --- |
| Insert | Insert | New Tag | `tags.new` |
| Delete | Delete | Delete Tag | `tags.delete` |

#### Feature flags editor

| Windows and Linux | macOS | Command | Id |
| --- | --- | --- | --- |
| Insert | Insert | Add Feature Flag | `features.add` |
| Delete | Delete | Remove Feature Flag | `features.remove` |

#### Embedded images editor

| Windows and Linux | macOS | Command | Id |
| --- | --- | --- | --- |
| Insert | Insert | Add Embedded Image | `embedded.add` |
| Delete | Delete | Delete Embedded Image | `embedded.delete` |

#### Unexported resources list

| Windows and Linux | macOS | Command | Id |
| --- | --- | --- | --- |
| Delete | Delete | Unmark for Export | `project.unmark` |

#### Canvas workspace (board)

| Windows and Linux | macOS | Command | Id |
| --- | --- | --- | --- |
| Ctrl+Shift+Home | Cmd+Shift+Home | Fit All Cards | `board.fitAll` |
| Ctrl+Alt+Left | Control+Option+Left | Focus Card on the Left | `board.focusLeft` |
| Ctrl+Alt+Right | Control+Option+Right | Focus Card on the Right | `board.focusRight` |
| Ctrl+Alt+Up | Control+Option+Up | Focus Card Above | `board.focusUp` |
| Ctrl+Alt+Down | Control+Option+Down | Focus Card Below | `board.focusDown` |
| Space | Space | Pan Canvas (Hold, Then Drag) | `board.pan` |


### 16.3 `when` of each context

Every binding in a table of 16.2 implicitly carries the `when` of its table, and the keys only reach their handler while it is true. Text fields add `!textInputFocus` to single-letter and number keys (rule 4).

| Context table in 16.2 | `when` |
| --- | --- |
| Global (always active) | `workspaceOpen` or no workspace; bindings that also appear in a scoped context use the global `when` of 16.1 |
| Tabs and editor groups (always active) | `workspaceOpen` |
| Explorer focused | `explorerFocus` |
| Code editor focused | `editorType == 'code'` |
| Any canvas editor | `canvasFocus` |
| Room editor, instance and layer mode | `editorType == 'room' && roomMode == 'instances'` |
| Room editor, tile painting mode | `editorType == 'room' && roomMode == 'tiles'` |
| Sprite editor | `editorType == 'sprite'` |
| Image editor | `editorType == 'image'` |
| Tile set editor | `editorType == 'tileset'` |
| Texture page viewer | `editorType == 'texturePage'` |
| Object editor | `editorType == 'object'` |
| Sound editor | `editorType == 'sound'` |
| Font editor | `editorType == 'font'` |
| Path editor | `editorType == 'path'` |
| Timeline editor | `editorType == 'timeline'` |
| Sequence editor, dope sheet | `editorType == 'sequence'` |
| Animation curve editor | `editorType == 'animationCurve'` |
| Particle system editor | `editorType == 'particleSystem'` |
| Extension editor | `editorType == 'extension'` |
| Shader editor | `editorType == 'shader'` |
| General info editor | `editorType == 'generalInfo'` |
| Strings editor | `editorType == 'strings'` |
| Variables, functions and code locals | `editorType == 'symbols'` |
| Search panel | `focusedPanel == 'search'` |
| Problems panel | `focusedPanel == 'problems'` |
| Script console | `focusedPanel == 'console'` |
| Git panel | `focusedPanel == 'git'` |
| Patch tools | `focusedPanel == 'patches'` |
| Mod editor | `editorType == 'modpkg'` |
| Diff views | `editorType == 'diff'` |
| Settings and keyboard shortcuts | `editorType == 'settings'` |
| Hex view | `editorType == 'chunk'` |
| Filter effects editor | `editorType == 'filterEffects'` |
| Languages editor | `editorType == 'languages'` |
| Tags editor | `editorType == 'tags'` |
| Feature flags editor | `editorType == 'featureFlags'` |
| Embedded images editor | `editorType == 'embedded'` |
| Unexported resources list | `focusedPanel == 'unexported'` |
| Canvas workspace (board) | `boardFocus` |

Contexts other than Global, Tabs and Explorer are focus-exclusive: focus is in one editor or panel at a time. The Room editor tables are exclusive through `roomMode`. A canvas workspace card is `canvasFocus` for the editor inside it and `boardFocus` for the surface.

### 16.4 Duplicate-chord scan

Result of the scan over every table of 16.2, both platform columns (the scan is part of the keymap test described above):

1. No chord repeats inside one context table.
2. No chord is a prefix of a longer chord (`Ctrl+K` is only ever a first key).
3. Chords repeated between two contexts that can be active together were real conflicts: `Ctrl+=`, `Ctrl+-` and `Ctrl+0` in canvas editors against the global zoom, `Ctrl+Alt+Up` and `Ctrl+Alt+Down` and `Space` between a code or canvas editor and the canvas workspace, and `Delete` against the global Delete. All are now in 16.1 with a `when` on each side.
4. Every other repeated chord (for example `Insert`, `Home`, `PageUp`, `PageDown`, single letters, `Enter`, `Escape`, `Ctrl+R`, `Ctrl+Shift+Space`, `Alt+Up`, `Alt+Down`) is bound in contexts that exclude each other by focus.

## 17. Parity coverage

- UTMT-APP-01: section 2 (menus).
- UTMT-APP-02: section 5.2 (tree content) and the editor specs for each node.
- UTMT-APP-03: section 5.4 (name filter).
- UTMT-APP-04: section 9 item 8 and the breadcrumb bar.
- UTMT-APP-06: section 4 (Asset ID item).
- UTMT-APP-07: section 15 (empty states) and the start page.
- UTMT-APP-08: section 14.1 and ux-standards section 9 (progress replaces the modal loader).
- UTMT-APP-13, UTMT-APP-15: ux-standards section 11 (text input and message dialogs).
- UTMT-FILE-01, UTMT-FILE-02: section 2 (File menu) and section 14.1.
- UTMT-APP-10: section 1 (window placement).
- UTMT-FILE-03: section 14.2 (save).
- UTMT-FILE-04: section 14.1 (load warnings).
- UTMT-FILE-09: section 14.2 and settings.md section 4.1.
- UTMT-FILE-11: sounds.md section 6.2 (linked files in the Explorer).
- UTMT-FILE-12, UTMT-FILE-13: section 13, section 13.1 and section 14.1.
- UTMT-RUN-06: section 13.1 (launch forms and link), run-game.md.
- UTMT-TREE-01, UTMT-TREE-02, UTMT-TREE-05, UTMT-TREE-07: sections 5.5 and 5.6.
- UTMT-TREE-03, UTMT-TREE-04: section 5.6 (New Resource) and each editor's creation notes.
- UTMT-TREE-06: section 5.6 (Delete with reference scan) and ux-standards section 7.
- UTMT-TREE-08, UTMT-TREE-11: section 5.4.
- UTMT-TREE-09: section 5.5 item 4.
- UTMT-TREE-10: section 5.2.
- UTMT-TAB-01 to UTMT-TAB-08: section 9.
- UTMT-KEY-01, UTMT-KEY-02, UTMT-KEY-03: section 16.
- UTMT-CTRL-01 to UTMT-CTRL-06: ux-standards section 11.
- UTMT-SET-07: settings.md section 4.1 (the prompt with Do it later replaces UTMT's).
- UTMT-PLAT-05: section 13 (several workspaces in several windows, one process).
- UTMT-APP-11, UTMT-APP-14, TOOL-58: section 14.5.
- TOOL-53, TOOL-55: section 7.6 (Jobs) and patches.md section 6.

## 18. Design decisions taken (2026-10-25)

1. The left tree panel is the "Explorer" (VS Code's term, translated in all six language packs). Added to the glossary; GameMaker's "Asset Browser" is mentioned in the tooltip and onboarding for GameMaker users.
2. Standard rows are 24 px (WCAG 2.5.8). A Compact density setting (22 px rows) ships in the beta and is off by default.
3. Selected rows keep the 2 px `Select` bar on top of the fill; the fill alone does not meet contrast.
4. Canvas links and card positions are layout state, stored with the workspace layout, never in the project file.
5. Closing the last main window quits on Windows and Linux and keeps the app running on macOS, per platform conventions.
6. `Ctrl+Tab` is the global recency switcher. Inside a canvas, `Alt+]` and `Alt+[` cycle cards.
