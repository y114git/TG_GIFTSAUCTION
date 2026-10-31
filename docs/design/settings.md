# Settings

Status: proposed. The Settings editor, the settings files and scopes, every UTMT setting mapped to a G3MStudio setting, and the settings that only G3MStudio has. Requirements: F-UI-6, F-UI-5 (shortcut editor link), F-L10N-2. Shared rules: [ux-standards.md](ux-standards.md). Compiler and decompiler settings (UTMT-GMLS-01 to 08) are a separate page owned by [editors/code.md](editors/code.md) section 8.

## 1. Purpose and entry points

Settings are stored as JSON, searchable, scoped to the user or to one workspace, and every setting has a default, a type and a description in one schema. The settings UI and the command palette are generated from that schema (architecture "Main parts").

Opens from File > Settings (Ctrl+, / Cmd+,), the status bar and menu items that name a setting ("Settings: Autosave" in the palette), the first run flow, and from messages that say "Change this in Settings > ..." (ux-standards 6). `app.openSettingsJson` opens the user file in the code editor.

## 2. Layout

```
+--------------------------------------------------------------------------------------+
| Settings    [ Search settings...                         ]   User | Workspace         |
+--------------------+-----------------------------------------------------------------+
| Appearance         |  Appearance                                                     |
| Language           |                                                                 |
| Files              |  Theme                                                          |
| Workspace          |  appearance.theme                                               |
| Explorer           |  [ G3M Default  v ]  [Edit Theme...]                            |
| Editors            |  One customizable theme. Edit any token in the theme editor.    |
| Run                |                                                                 |
| Scripts            |  * Transparency grid                         [Reset to Default] |
| Projects           |    appearance.transparencyGrid                                  |
| Paths              |    [Grid.Dark] [Grid.Light]                                     |
| File Associations  |    Colors of the checkerboard behind images.                    |
| Updates            |                                                                 |
| Confirmations      |                                                                 |
| Plugins            |                                                                 |
| Advanced           |                                                                 |
+--------------------+-----------------------------------------------------------------+
```

Left: the page list (200 px) with a count of modified settings per page. Right: settings of the page in groups. A tab bar at the top right switches the scope being edited (3.2). Search filters every page at once; matches are highlighted and the page list shows match counts.

Each setting is one row:

1. Title (sentence case), the key in monospace below it (copy button on hover), the control, a description of one or two sentences, an optional "Learn more" link.
2. A modified marker (the dot of ux-standards 2 rule 1) and "Reset to Default" when the value differs from the default of the scope.
3. A scope badge "Workspace" when the value in the other scope differs ("Also set in Workspace: ...").
4. Invalid values keep the previous value and show the message below the control (ux-standards 8 rule 1); the file is never written with an invalid value.

Controls follow the type: check box (boolean), combo (enum), number field with unit (number), text with Browse (path), list grid (list), color field (color), key chooser (shortcut). Changes apply at once, with no Apply button; settings that need a restart say so on the row ("Restart G3MStudio to apply") and show a "Restart" button.

## 3. Storage and scopes (UTMT-SET-01)

### 3.1 Files

| File | Where | Content |
| --- | --- | --- |
| `settings.json` | User settings folder (architecture "Files on disk") | User scope |
| `keybindings.json` | Same folder | Keyboard shortcuts (the shortcut editor, F-UI-5) |
| Workspace settings | Data file mode: app data, keyed by the file identity (path, size, MD5). Project mode: `.g3mstudio/settings.json` in the project folder (ignored by Git by default) | Workspace scope |
| `onboarding.json` | User settings folder | First run state |

Rules: UTF-8 without BOM, LF, 2 space indent, keys in schema order, final newline. Comments are allowed on read (a line starting `//`) and are kept on write. Unknown keys are kept and reported as "Unknown setting" in Problems with a link to the file, never deleted. The file is written atomically (temp file, replace). A file that does not parse is moved to `settings.json.invalid` and the defaults load, with an error notification "Could not read settings.json. The file was kept as settings.json.invalid." and "Open File". Settings are loaded when the window shows (UTMT loads at window show) and reloaded when the file changes on disk.

### 3.2 Scopes

Order of precedence, lowest to highest: Default (built in), User, Workspace.

| Scope | Meaning | Which settings |
| --- | --- | --- |
| User | All workspaces on this machine | Every setting |
| Workspace | One data file or project | Settings marked W or UW in the tables below |
| Machine | A user setting that depends on the PC (paths, runtimes, window placement) | Marked M; never exported with "Export Settings" |

A setting marked U cannot be set at workspace scope (the Workspace tab hides it). The Workspace tab is empty when no workspace is open ("Open a data file to set workspace settings.").

Import and export: `app.exportSettings` writes a JSON of the user scope without machine settings; `app.importSettings` merges one in with a preview of what changes; `app.importUtmtSettings` (4.1) reads UndertaleModTool's settings.

## 4. UTMT settings mapped to G3MStudio

### 4.1 Mapping table

| UTMT setting | Parity | G3MStudio setting | Default | Scope | Where in the UI |
| --- | --- | --- | --- | --- | --- |
| Settings JSON in `%AppData%\UndertaleModTool` | UTMT-SET-01 | `settings.json` in the user settings folder (3.1) | n/a | U | Open Settings (JSON) |
| GMS 1.4 folder | UTMT-SET-02 | `paths.gameMakerStudio1` | Windows: `%AppData%\GameMaker-Studio` when it exists; empty elsewhere | M | Paths |
| GMS2 runtimes folder | UTMT-SET-02 | `paths.gameMakerRuntimes` | Windows: `%ProgramData%\GameMakerStudio2\Cache\runtimes` when it exists; other platforms: none | M | Paths |
| Enable asset order swapping | UTMT-SET-03 | `explorer.reorderMode`: `off`, `swap`, `move` | `off` | U | Explorer |
| Automatically associate GameMaker data files | UTMT-SET-04 | Per-extension registration state, with `fileAssociations.keepRegistered` | Off until chosen in first run | M | File Associations |
| Show "Run game under GMS debugger" | UTMT-SET-05 | `run.showGameMakerDebugger` | `false` | U | Run |
| Remember window placement | UTMT-SET-06 | `window.rememberPlacement` and `window.restore` | `true`, `all` | M | Workspace |
| Warn about saving before closing | UTMT-SET-07 | `files.closeUnsaved`: `ask` or `keep` | `ask` | U | Files and Confirmations |
| Warn about temp running | UTMT-SET-08 | `confirm.launchTemporary`: `ask`, `yes`, `no` | `ask` | U | Confirmations |
| Show null entries in tree | UTMT-SET-09 | `explorer.showNull` | `false` | UW | Explorer |
| Recompile all GML on project save | UTMT-SET-10 | `projects.recompileOnSave` | `false` | W | Projects |
| Dark mode | UTMT-SET-11 | Not a setting. G3MStudio has one customizable theme (design.md rule 6); the theme editor (`appearance.theme`) covers it | `G3M Default` | U | Appearance |
| Transparency grid colors | UTMT-SET-12 | `appearance.transparencyGrid` (two colors) | `Grid.Dark`, `Grid.Light` | U | Appearance |
| Global room grid override | UTMT-SET-13 | `rooms.gridOverride` (enabled, width, height, thickness) | off | UW | Editors |
| Open app data folder | UTMT-SET-14 | Command `tools.openAppData` and the button "Open Settings Folder" on Advanced | n/a | n/a | Tools menu, Advanced |
| Update app button (debug builds) | UTMT-SET-15 | `updates.channel`, `updates.checkOnStart`; the Updates page always has "Check Now" | `stable`, `true` | U | Updates |
| GML settings button | UTMT-SET-16 | The page of editors/code.md section 8, reached by `tools.gmlSettings` and a button on Editors | n/a | UW | Editors |

Notes on the rows where the behaviour changes:

1. UTMT-SET-06: UTMT's default is off. G3MStudio's default is on, because the window state is also the multi-window restore (shell 13.5). Turning `window.rememberPlacement` off opens windows centered at the default size.
2. UTMT-SET-07: with the journal (F-WS-4) closing never loses edits. `ask` (default) shows the prompt of shell 14.2 item 8 when changes are not in the data file: Sync changes (Save in Direct mode), Do it later or Cancel (Q35). `keep` closes without the prompt and keeps the changes in the journal. UTMT's dialog is Save, Don't Save, Cancel; Do it later replaces Don't Save because nothing is thrown away. The keymap preset "UTMT" does not change this setting; a button on the Confirmations page, "Use UndertaleModTool defaults", sets the confirmations and these two to UTMT's values in one step.
3. UTMT-SET-11: UTMT's dark mode toggle maps to the theme editor. G3MStudio has a single customizable theme, no light theme and no dark and light split (design.md rule 6, N-UI-1). A user who wants a lighter look edits the tokens; the contrast table of ux-standards 2 guides the choice.
4. UTMT-SET-15: UTMT shows the button only in debug builds and never checks by itself. G3MStudio checks at start by default (F-UPD-1) and always shows the Updates page.
5. UTMT-SET-04: the first run flow (onboarding step 4) is where a user chooses; skipping it touches nothing.

### 4.2 Import from UndertaleModTool

`app.importUtmtSettings` (File > Settings > "Import UndertaleModTool Settings..." button on Advanced) reads `%AppData%\UndertaleModTool\settings.json` or a chosen file. A preview table lists every UTMT setting found, the G3MStudio setting it maps to, the new value and a check box; "Import" applies the checked rows to the user scope. Settings with no equivalent (profile mode, the already removed ones, the update button) are listed as "Not used" and never imported. The GML settings (UTMT-GMLS-01 to 08) import into the compiler and decompiler page. The file is only read.

## 5. G3MStudio settings

Every setting below is in the schema. Settings defined in an editor spec (for example the code editor options, the sound mixer, the room editor) are listed on their page by the schema; this table is the shell level set.

### 5.1 Appearance

| Key | Type and default | Scope | Notes |
| --- | --- | --- | --- |
| `appearance.theme` | `G3M Default` (string) | U | Name of an installed theme (Color Theme..., Ctrl+K Ctrl+T); "Edit Theme..." opens the theme editor |
| `appearance.backgroundImage` | `true` | U | The G3M background behind the side docks |
| `appearance.transparencyGrid` | two colors, default `Grid.Dark` and `Grid.Light` | U | Checkerboard behind images (tokens in ux-standards 2) |
| `ui.zoom` | `100%` (50 to 300) | U | Also Ctrl+= and Ctrl+- |
| `ui.density` | `standard` or `compact` | U | ux-standards 1 |
| `ui.reduceMotion` | `system`, `on`, `off` | U | ux-standards 5 |
| `ui.breadcrumbs` | `true` | U | Breadcrumb row above editors |

### 5.2 Language

| Key | Type and default | Scope | Notes |
| --- | --- | --- | --- |
| `ui.language` | OS language if supported, else `en` | U | Applies live |
| `ui.formatCulture` | `system` or a culture name | U | Numbers, dates, sort |

### 5.3 Files

| Key | Type and default | Scope | Notes |
| --- | --- | --- | --- |
| `files.autosave` | `off`, `afterDelay`, `onFocusLoss` | UW | Writes the data file (per data file, `off` by default). The journal is always on and is not a setting (F-WS-4, F-WS-13) |
| `files.autosaveDelay` | 2 s (1 to 600) | U | |
| `files.backupCount` | 3 (1 to 3) | UW | Rotating backups of the data file in the user data folder (F-WS-5, shell 14.2) |
| `files.history.maxSnapshots` | none (0 = no limit) | UW | Snapshot count in project mode (F-WS-6) |
| `files.history.maxSizeMb` | none (0 = no limit) | UW | Oldest snapshots are dropped first when set; `files.history.maxAgeDays` (none) works the same way |
| `files.autoReload` | `false` | U | Reload without asking when no unsaved edits |
| `files.closeUnsaved` | `keep` | U | See 4.1 |
| `files.confirmOverwriteExport` | `true` | U | Confirmation before export over an existing file |

### 5.4 Workspace and window

| Key | Type and default | Scope | Notes |
| --- | --- | --- | --- |
| `window.openMode` | `ask`, `new`, `this` (default `ask`) | M | shell 13.2 |
| `window.restore` | `all`, `last`, `none` | M | shell 13.5 |
| `window.rememberPlacement` | `true` | M | |
| `tabs.showModified` | `true` | U | shell 9.1 |
| `explorer.quickAccess` | `true` | U | |
| `explorer.sort` | `index` | UW | Per type |
| `explorer.showNull` | `false` | UW | |
| `explorer.reorderMode` | `off` | U | `swap` swaps two positions of one type as UTMT does; `move` inserts. Both ask once per session about shifted indexes |
| `inspector.openLimit` | 10 | U | |

### 5.5 Run, scripts and projects

| Key | Type and default | Scope | Notes |
| --- | --- | --- | --- |
| `run.tempLocation` | `gameFolder` or `appData` | U | [run-game.md](run-game.md) section 3 |
| `run.wineCommand` | empty (path) | M | Wine or Proton command for Windows games on other systems |
| `run.showGameMakerDebugger` | `false` | U | |
| `run.defaultProfile` | `Default` | UW | |
| `run.crashRetentionDays` | 30 | U | |
| `run.writeGameLog` | `true` | U | |
| `confirm.launchTemporary` | `ask` | U | |
| `scripts.checkpoint` | `always`, `ask`, `never` | U | [scripting.md](scripting.md) section 3.4 |
| `scripts.showFinished` | `true` | U | Section 3.6 |
| `scripts.trustedPaths` | list | M | Managed by the trust prompt |
| `projects.recompileOnSave` | `false` | W | UTMT-SET-10 |
| `projects.defaultMode` | `delta` or `full` | U | New project dialog default |
| `projects.errorOnWarnings` | `true` | W | Mirrors the project option |
| `workspace.defaultMode` | `direct` or `project` | U | Mode for data files opened for the first time. With `direct` the one-time offer to enable a project is shown (ADR-0010) |
| `projects.autoSync` | `true` | UW | Sync project to data file on save (F-WS-12). The Sync button is always present |
| `projects.location` | user data folder | U | Where new projects are created; any folder allowed |
| Project list | table | U | Each project with path, size and state; Archive, Delete, Re-enable (F-WS-16) |

### 5.6 Paths

| Key | Type and default | Scope | Notes |
| --- | --- | --- | --- |
| `paths.gameMakerStudio1` | see 4.1 | M | Folder with the GameMaker 1.4 runner and debugger |
| `paths.gameMakerRuntimes` | see 4.1 | M | Folder with runtime versions; a "Detect" button fills it from the usual places |
| `paths.xdelta` | empty = bundled | M | Passed to xdelta as its program path |
| `paths.externalImageEditor` | empty = system default | M | "Edit in External Program" for sprites and images |
| `paths.externalAudioEditor` | empty = system default | M | Sounds |
| `paths.externalTextEditor` | empty | M | "Open in External Editor" for code |
| `paths.git` | empty = `git` on PATH | M | ADR-0004 |
| `paths.cache` | app data | M | `.g3mcache` and decode caches; "Clear Caches..." in Tools |

Path fields show "Not found" in `Warning` when the path does not exist, and "Detect" when a standard location can be found.

### 5.7 File associations

A page with the table of onboarding step 4 (data files, `.g3mpatch` and `.g3mproj`, scripts, links, the shell verbs of UTMT-RUN-06) and, per row, the current owner, a check box and "Apply". `fileAssociations.keepRegistered` (default off) re-registers on start when another program took a type over, with a notification. Restoring UndertaleModTool's registration is offered when G3MStudio took it over ("Restore UndertaleModTool").

### 5.8 Updates

| Key | Type and default | Scope | Notes |
| --- | --- | --- | --- |
| `updates.channel` | `stable`, `prerelease` | U | F-UPD-3: include pre-releases; the highest version wins |
| `updates.checkOnStart` | `true` | U | Off switch (F-UPD-3) |
| `updates.autoDownload` | `true` | U | Download in the background; "Restart to Update" applies it (F-UPD-4) |

Buttons: Check Now, What's New, Revert to Previous Version. The page states the program folder and whether it is writable. See [platforms-formats.md](platforms-formats.md) section 4.

### 5.9 Confirmations

A table of every confirmation (ux-standards 7): name, current response (Ask, Always Yes, Always No) and a "Reset" button. Keys are `confirm.<id>`. "Reset All Confirmations" restores Ask for all. The page lists the ones defined by the specs: delete with references, discard Git changes, replace project-wide, launch with temporary copy, launch and keep changes, restore, quit with running game, run first-time script. Confirmations that never have an automatic answer (data loss outside the data file) are not listed.

### 5.10 Plugins

Installed plugins with enable switch, version, permissions, "Settings" (opens the plugin's page), "Reload", "Uninstall", and `plugins.allowUnsigned` (default `false`). Plugins' own settings pages appear as sub pages under their name (ADR-0009).

### 5.11 Advanced

| Item | Notes |
| --- | --- |
| Open Settings Folder | UTMT-SET-14; opens the user settings folder in the file manager |
| Open Settings (JSON) | Opens `settings.json` |
| Export Settings..., Import Settings... | 3.2 |
| Import UndertaleModTool Settings... | 4.2 |
| Reset All Settings... | Confirms and names the pages; keeps keybindings unless asked |
| `diagnostics.logLevel` | `info` (also `debug`); rolling log in the log folder |
| `diagnostics.crashReports` | `true` | U | Writes `crash.txt` on this PC |
| `privacy.sendCrashReports` | `true` | U | Sentry reports with scrubbing (F-PRIV-1, Q29, Q32); set in onboarding step 0. Does nothing in a build without keys, and a failure of the service is never shown |
| `performance.indexCode` | `true`; background code index for search |
| `performance.maxUndoSteps` | 1000 |

There is no usage analytics setting because usage is never sent (onboarding principle 4).

## 6. Actions and shortcuts

| Command | Id | Windows and Linux | macOS |
| --- | --- | --- | --- |
| Settings | `app.settings` | Ctrl+, | Cmd+, |
| Open Settings (JSON) | `app.openSettingsJson` | None | None |
| Keyboard Shortcuts | `app.keyboardShortcuts` | Ctrl+K Ctrl+S | Cmd+K Cmd+S |
| Color Theme... | `app.colorTheme` | Ctrl+K Ctrl+T | Cmd+K Cmd+T |
| Export Settings... | `app.exportSettings` | None | None |
| Import Settings... | `app.importSettings` | None | None |
| Import UndertaleModTool Settings... | `app.importUtmtSettings` | None | None |
| Reset Setting | `settings.reset` | None | None |
| Reset All Settings... | `settings.resetAll` | None | None |
| Open App Data Folder | `tools.openAppData` | None | None |
| Focus Settings Search | `settings.focusSearch` | Ctrl+F | Cmd+F |
| Switch Scope (User, Workspace) | `settings.switchScope` | Ctrl+Tab in the scope bar | Control+Tab |

Inside the Settings tab: Up and Down move between settings, Enter edits, Escape returns to the page list.

## 7. Validation and messages

| Case | Message |
| --- | --- |
| Number out of range | "Value must be between {min} and {max}." |
| Path does not exist | "'{path}' was not found." (Warning, saved anyway) |
| Folder not readable | "Could not read '{path}'." |
| Color not valid | "Use a color like #FF8800 or #80FF8800." |
| Unknown language | "'{code}' is not an installed language. Using English." |
| Workspace scope for a user-only setting | The control is hidden in the Workspace tab; in the JSON file the entry shows a Problems warning "'{key}' only works in the user settings." |
| Settings file changed on disk while open | Info bar "settings.json changed on disk." with Reload |

## 8. Undo

Settings are not part of the workspace undo stack. Each row has Reset to Default, and the page "Reset All Settings..." confirms (ux-standards 7: settings are not undoable). A setting changed by import shows a notification "Imported {n} settings." with "Undo" that restores the previous user file for 60 seconds.

## 9. Empty and loading states

- Search with no match: "No settings match '{text}'." with "Search all text in Settings".
- Workspace tab without a workspace: see 3.2.
- Plugins page, none installed: shell section 15 text.
- Detecting paths: the "Detect" button shows a spinner for up to 3 seconds, then "Nothing found. Choose the folder yourself."

## 10. Decisions

1. Theme: decided by the owner (questions.md Q14, Q23): one default theme, 90% G3M design and style and 10% GameMaker, VS Code and Zed, fully customizable, no light theme and no dark and light split. UTMT-SET-11 is covered by the theme editor (section 5.1).
2. `files.closeUnsaved` default: `ask`, the Q35 prompt with Do it later (changes stay in the journal). Users who never want the prompt set `keep` on the Confirmations page.

## 11. Parity coverage

| ID | Feature | Section |
| --- | --- | --- |
| UTMT-SET-01 | Settings storage | 3 |
| UTMT-SET-02 | GameMaker paths | 4.1, 5.6 |
| UTMT-SET-03 | Asset order swapping | 4.1, 5.4 |
| UTMT-SET-04 | File association toggle | 4.1, 5.7 |
| UTMT-SET-05 | Debugger menu toggle | 4.1, 5.5 |
| UTMT-SET-06 | Remember window placement | 4.1, 5.4 |
| UTMT-SET-07 | Warn on close | 4.1, 5.3 |
| UTMT-SET-08 | Warn about temp running | 4.1, 5.5, 5.9 |
| UTMT-SET-09 | Show null entries in tree | 4.1, 5.4 |
| UTMT-SET-10 | Recompile all GML on project save | 4.1, 5.5 |
| UTMT-SET-11 | Dark mode toggle | 4.1, 5.1 |
| UTMT-SET-12 | Transparency grid colors | 4.1, 5.1 |
| UTMT-SET-13 | Global room grid override | 4.1 |
| UTMT-SET-14 | Open app data folder | 4.1, 5.11, 6 |
| UTMT-SET-15 | Update app button | 4.1, 5.8 |
| UTMT-SET-16 | GML settings button | 4.1 |
| F-UI-6 | Settings UI with search, user and workspace scopes | 2, 3 |

The compiler and decompiler settings behind UTMT-SET-16 are specified in [editors/code.md](editors/code.md) section 8.
