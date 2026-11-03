# Projects

Status: proposed. A project is an optional layer, enabled per data file (ADR-0010). G3MStudio projects (ADR-0008) and the import and export of UndertaleModTool 0.9 projects. Covers creating, opening, saving and closing a project, the unexported resources list, the project options, GML source files, BPS patches, project scripts, game file backup and the two UTMT conversions. Requirements: F-WS-7, F-WS-10, F-WS-11, F-GIT-1. Shared rules: [ux-standards.md](ux-standards.md). The menu items are in [shell.md](shell.md) section 2 (File and Project items).

## 1. Purpose and entry points

A project is a folder that holds a game's resources as text and side files, so Git can track it and a mod can be rebuilt from it. It is optional: Direct mode edits the data file with no project, and "Enable project" (title bar, Project menu) exports the open data file into a project in the user data folder, or any chosen folder, in the background with progress; edits made meanwhile are queued (ADR-0010). It has two modes (ADR-0008): delta, which holds only the resources that differ from a base data file and is the default for mods, and full, which holds every resource.

Entry points: File > New > New Project Folder..., File > Open Project Folder... (Ctrl+K Ctrl+O), File > Export > Export Project Folder..., File > Import > UTMT Project..., File > Export > Export as UTMT 0.9 Project..., the Project menu, the Start page card "Open Project Folder..." and drag and drop of a `.g3mproj` or a UTMT project JSON onto the window.

## 2. Layout

On disk (native layout, ADR-0008):

```
MyMod/
  MyMod.g3mproj                  project file (JSON)
  sprites/spr_player/spr_player.json
  sprites/spr_player/spr_player_0.png
  sprites/spr_player/spr_player_0.mask.png
  objects/obj_player/obj_player.json
  objects/obj_player/Create_0.gml
  rooms/room_start/room_start.json
  scripts/scr_move/scr_move.json
  scripts/scr_move/scr_move.gml
  code/gml_GlobalScript_scr_move/gml_GlobalScript_scr_move.gml
  strings/strings.json
  options/general.json
  datafiles/...
  .g3mstudio/                    local state, ignored by Git
  build/                         output of Build Data File, ignored by Git
```

In the window the Explorer root shows the project name with a "Project" badge, its mode and the base data file on hover. The status bar adds the "Marked for export" check box (UTMT-PROJ-06, [shell.md](shell.md) section 4) and the Git branch item.

```
+--------------------------------------------------------------------------------------+
| New Project Folder                                                                   |
|  Name        [ MyMod                      ]                                          |
|  Location    [ D:\Mods                    ] [Browse...]                              |
|  Base data   [ ...\Undertale\data.win     ] [Browse...]   2023.8, bytecode 16        |
|  Mode        (*) Delta: only changed resources   ( ) Full: every resource            |
|  [x] Create a Git repository   [x] Add .gitignore                                    |
|  Result: D:\Mods\MyMod\MyMod.g3mproj                                                 |
|                                                          [Cancel]  [Create Project]  |
+--------------------------------------------------------------------------------------+
```

## 3. The G3MStudio project

### 3.1 Project file

`<Name>.g3mproj` at the root is JSON in the order below (ADR-0008 decision 1 and 3).

| Field | Content |
| --- | --- |
| `formatVersion` | Integer. Older versions migrate on load; G3MStudio never writes older ones |
| `name` | Project name |
| `mode` | `delta` or `full` |
| `base` | Base data file identity: relative or absolute `path`, `size`, `md5`, GameMaker version, bytecode version |
| `resources` | List of resources in the project: type, name, path of its folder, state (`added`, `changed`, `deleted`, `unchanged` in full mode) |
| `folders` | Virtual folders of the Explorer (shell 5.3): name, type, members, color |
| `order` | Resource order per type (the index order of the data file; custom Explorer order stays in the sidecar) |
| `groups` | Texture groups and audio groups with members |
| `options` | The project options of section 7 |

Deterministic output: sorted lists, fixed property order, UTF-8 without BOM, LF, a final newline, invariant culture. Saving an unchanged project changes no file (ADR-0008 acceptance).

### 3.2 Layout rules

1. One folder per resource: `<type folder>/<Name>/<Name>.json` plus side files. Binary content stays in side files (frame PNGs, mask PNGs, audio as stored, font textures), never inline.
2. Type folders: `sprites`, `objects`, `rooms`, `scripts`, `sounds`, `fonts`, `paths`, `timelines`, `shaders`, `sequences`, `animcurves`, `particles`, `tilesets`, `extensions`, `notes`, `options`, `datafiles`, plus `code` and `strings` for data-file-only content.
3. Field names follow GameMaker's `.yy` names where the concept is the same.
4. Code is a `.gml` file per code entry or per event (section 6).

### 3.3 New project (UTMT-PROJ-01)

`file.new.projectFolder` opens the dialog of section 2.

1. Base data: the open data file is preselected; without one the field is empty and Create is disabled until a file is chosen. The file is read for identity and version only.
2. Mode: Delta creates the folder with the project file and empty type folders; the first edit that is marked for export writes its resource. Full exports every resource immediately as a job with progress (a 92 MB data file takes about 20 s; Cancel deletes the partial folder).
3. Git: "Create a Git repository" runs `git init` and writes a `.gitignore` with `.g3mstudio/`, `build/` and `*.tmp`.
4. The project opens at once in a new workspace in the current window or a new window per `window.openMode`; the data file stays loaded as the base.
5. Name rules: the folder name must be a valid file name; an existing non-empty folder is refused ("'{folder}' is not empty. Choose an empty folder.").
6. Without a loaded data file the command is still enabled and asks for the base first.

### 3.4 Open project (UTMT-PROJ-02)

`file.openFolder` accepts a folder containing a `.g3mproj`, the `.g3mproj` itself, or a UTMT project JSON (section 10.1). Steps, one job with named steps:

1. Read the project file; migrate the format version if older; show "Project upgraded from format {n}." in a notification with Undo (the old file is kept as `.g3mproj.bak`).
2. Locate the base data file by `base.path`. If it is missing: dialog "The base data file was not found." with "Locate..." and "Cancel"; the located file is checked against `base.size` and `base.md5`. A different file shows "This data file is not the one the project was made from. Open it anyway?" with the differences (size, version) and "Open Anyway" or "Cancel"; with Open Anyway the project opens read-only until the user rebases (Project > Rebase on Data File).
3. Run the import steps of the project options (section 7): pre-import scripts, then patches (section 8), pre-asset-import scripts, then assets, then post-import scripts. Every step writes to the Project channel of Output; a failed step stops the open and rolls back (section 9).
4. Import the resources of the project into the loaded base: each resource folder is read, validated and applied to the model. Resources that fail validation become Problems entries and are skipped when "Error on warnings" is off, or stop the open when it is on (default on).
5. The Explorer fills; resources from the project show their state badge (added, changed).
6. Restore the workspace state (tabs, layout) for this project from `.g3mstudio/`.

Open time target: 20,000 resources to a usable Explorer in under 2 seconds (ADR-0008 acceptance). The full import then continues in the background with a progress bar in the Explorer; resources not yet applied are opened on demand.

### 3.5 Save project (UTMT-PROJ-03)

`file.save` in project mode writes the pending resources to the project and, with auto sync on save (default), syncs the data file (ADR-0010). The project steps are:

1. Pending editor text is applied first (code editor Apply).
2. Delta mode writes every resource marked for export (section 4); full mode writes every changed resource and removes folders of deleted resources.
3. The project file is rewritten last and atomically; a failed write leaves the previous project files intact (resource files are written to temp names and renamed together).
4. A deleted resource in a delta project writes an entry `state: deleted` in `resources` (it has no folder).
5. Notification "Saved project 'MyMod'. {n} resources written." with "Show Changes".

`project.build` ("Build Data File From Project") then writes a data file: `build/{base name}` by default, or a chosen path, through the save pipeline (validation read). The build is also `g3mstudio project build`.

Save As... copies the project to a new folder (all files, new name); the base data file link is kept.

### 3.6 Close project (UTMT-PROJ-05)

`file.close` closes the project workspace. Unsaved edits follow `files.closeUnsaved` ([settings.md](settings.md) section 4.1): with the prompt answered Do it later they stay in the journal and return when the project opens again. Closing unloads the base data file and the project context; the `.g3mstudio/` state keeps tabs and layout. Closing a project never deletes the build folder. Project changes are saved on close; if they are not yet in the data file, the prompt of shell 14.2 item 8 appears.

### 3.7 GameMaker project interop

File > Import > GameMaker Project (.yyp)... and File > Export > Export as GameMaker Project Folder (.yy)... are in ADR-0008 decision 6 and F-WS-11. They use the same serializers as the native layout and are specified with the GameMaker format work (sprites, sounds, scripts and code, objects and rooms first). They are listed here so the Project menu has one place for all conversions.

### 3.8 Enable, disable and external changes (ADR-0010)

1. Enable project: `project.enable` is always visible in Direct mode and offered once at the first open of a data file; `workspace.defaultMode` changes the default for new files. The project records the data file identity (size, MD5 and path) and the hash of each resource at the last sync.
2. Disable project: `project.disable` asks to keep the project (archived, can be re-enabled) or delete it to free disk space. The data file is never touched.
3. Data file changed outside: offer a resource-level three-way merge into the project (base = last synced resource hashes, theirs = data file, ours = project), overwrite the data file with the project, or keep both as a new workspace.
4. Data file missing or corrupt: rebuild it from the project to a chosen path (about 18 to 24 s on large games). Project folder lost: re-export from the data file. Project folder edited outside (text editor, Git pull): a file watcher reloads the changed resources, with a prompt if they were also edited in the app.
5. Snapshots: built-in, named or timed, content-addressed, compare and restore per resource; limits default to none. They work without Git, and Git works on the project folder when `git` is installed.

## 4. Change tracking and unexported resources

### 4.1 Marking (UTMT-PROJ-06)

In delta mode an edit marks the resource for export automatically (editors call the mark when a command changes it). The status bar check box "Marked for export" shows and toggles the mark of the open resource (`project.markForExport`). A resource that was edited and then restored to its original value stays marked until the user unmarks it or runs Project > Remove Unchanged Marks, which compares with the base and drops identical ones. Full mode marks nothing: every resource exports.

### 4.2 View unexported resources (UTMT-PROJ-04)

`project.viewUnexported` opens a list in the bottom panel (a result list, ux-standards 11) of resources marked but not yet written to the project folder.

| Part | Behaviour |
| --- | --- |
| Columns | Type icon, name, type, reason (edited, added, deleted, marked by hand), last edit time |
| Header | "{n} resources marked but not saved." with Save Project and Export Selected |
| Actions | Open, Open in New Tab, Unmark for Export, Mark All Changed, Copy, Copy All |
| Keys | Enter opens, Delete unmarks, Ctrl+A selects all |
| Live | The list updates as marks change; the count appears as a badge on the Project menu item and in the status bar item "Unsaved project resources" |
| Empty | "Everything is saved to the project." |

Unmarking a resource that was changed warns once: "'{name}' has edits that will not be written to the project. Unmark anyway?" with "Unmark" and "Cancel".

## 5. Serializable resource types (UTMT-PROJ-07)

The native project stores every resource type of the data file (ADR-0008). The 13 types UTMT 0.9 can store are listed with their UTMT layout, which section 10 uses.

| Resource | Native folder | Side files | UTMT 0.9 folder and form |
| --- | --- | --- | --- |
| Sprite | `sprites` | `<name>_<i>.png`, `<name>_<i>.mask.png` | `sprites/<name>/<name>.json` with frame and mask PNGs |
| Tile set or background | `tilesets` | `.png` | `backgrounds/<name>/<name>.json` with `.png` |
| Game object | `objects` | `.gml` per event | `objects/<name>.json` |
| Room | `rooms` | `.gml` for creation code | `rooms/<name>.json` |
| Script | `scripts` | `.gml` | `scripts/<name>.json` |
| Code entry | `code` | `.gml` | `code/<name>.gml` |
| Sound | `sounds` | audio as stored | `sounds/<name>/<name>.json` with `.wav` or `.ogg` |
| Font | `fonts` | texture `.png` | `fonts/<name>/<name>.json` with `.png` |
| Path | `paths` | none | `paths/<name>.json` |
| Shader | `shaders` | one `.vsh`, `.fsh` per source | `shaders/<name>/<name>.json` with `.txt` per source |
| Sequence | `sequences` | none | `sequences/<name>.json` |
| Animation curve | `animcurves` | none | `animcurves/<name>.json` |
| Particle system | `particles` | none | `particlesystems/<name>.json` |
| General info and options | `options` | none | `general-info-options.json` beside the project file |
| Timeline | `timelines` | one `.gml` per moment: `moment_<step>.gml` | Not stored in UTMT projects |
| Extension | `extensions` | `extensions/<Name>/<Name>.json` plus the extension's files (DLLs, `.gml`, `.js`) in the same folder | Not stored in UTMT projects |
| Texture groups, audio groups | `.g3mproj` | none, recorded in `groups` of the project file (section 3.1) | Not stored in UTMT projects |
| Tags | resource JSON | none, a `tags` list in each resource's `<Name>.json` | Not stored in UTMT projects |
| Feature flags | `options` | `options/feature-flags.json` | Not stored in UTMT projects |
| Filter effects | `options` | `options/filter-effects.json` | Not stored in UTMT projects |
| UI layers | `rooms` | none, part of the layer list in `rooms/<Name>/<Name>.json` | Not stored in UTMT projects |
| Strings | `strings` | `strings/strings.json` | Not stored in UTMT projects |
| Variables, functions | `options` | `options/variables.json`, `options/functions.json` | Not stored in UTMT projects |
| Embedded data, included files | `datafiles` | `datafiles/<relative path>`, one file each | Not stored in UTMT projects |

Name collisions inside one folder get `_2` to `_11` suffixes and then fail with `G3MS-PROJ-007` (UTMT behaviour). A file whose name equals the resource name omits `DataName` in UTMT form; the native files always name the resource.

## 6. GML source retention (UTMT-PROJ-08)

1. Code is stored as plain `.gml` files. Comments, blank lines and formatting survive, because the file is the source of truth and the model's bytecode is compiled from it.
2. When a project is open the code editor and the search index read the `.gml` text, not the decompiler output. The decompiler is used only for entries that have no source file (original game code never touched), and G3MStudio writes a decompiled source file only when the entry is first edited or exported.
3. Apply in the code editor writes the file (UTF-8, LF) and compiles it into the model. If the compile fails, the file is still written and the entry keeps its previous bytecode; the error is in Problems. A Problems entry "Source does not compile" marks the entry until fixed.
4. A decompiler failure at export writes a comment block with the error instead of failing, and the entry is flagged "Decompile failed" (UTMT behaviour).
5. An external edit of a `.gml` file (watched, shell 14.3) offers Reload or Keep; a reload recompiles it.
6. Revert to Original Code removes the source file and restores the base entry.
7. Setting `projects.recompileOnSave` (UTMT-SET-10, default off) recompiles every code source with `.gml` before project save, in name order, so a build never depends on stale bytecode.
8. A project without sources for an entry (created from a data file) builds with the base bytecode for it.

## 7. Project options (UTMT-PROJ-09)

`project.options` opens the Project Options tab. Fields follow UTMT's main options file names so a converted project reads the same.

| Option | Control | Rule |
| --- | --- | --- |
| Name | Text | Required, valid file name |
| Flags | Flags box | Reserved project flags; unknown bits kept |
| Verify strings | List grid of strings | Strings that must exist in the base; the import fails with `G3MS-PROJ-004` if one is missing, which detects a wrong base data file |
| Assets data file path | Path | Base data file; relative paths are relative to the project folder |
| Error on warnings | Check box (default on) | Import warnings stop the build |
| Excluded folders | List grid of folder names | Folders skipped when reading the project (for example `notes`) |
| Sub-projects | List grid of project paths | Other projects imported before this one, in order |
| Patches | List grid (section 8) | BPS patches for files |
| External files | List grid: source, target | Files copied into the game folder with the build |
| File copies | List grid: source, target | Files copied during import |
| File deletes | List grid: path | Game files removed during import |
| Pre-import scripts | Script list | Section 9 |
| Pre-asset-import scripts | Script list | Section 9 |
| Post-import scripts | Script list | Section 9 |

Paths in these lists are relative to the project folder (sources) or to the game folder (targets) and are checked against escaping their root (UTMT-FILE-14): "'{path}' leaves the project folder." The tab validates on edit and lists errors in Problems.

Stored in `options` of the `.g3mproj`. A UTMT main options JSON maps field by field (section 10).

## 8. BPS patches (UTMT-PROJ-10)

1. A BPS patch is a binary patch format with checksums. The Patches list holds a source file in the game folder (or the data file), the `.bps` file and an optional output name.
2. Order: patches apply at the start of the import, before assets, so the assets apply to the patched file. The list order is the apply order.
3. Before applying, the source's checksum is compared with the patch's recorded source checksum. A mismatch is `G3MS-PROJ-005`: "'{file}' does not match the file this patch was made for." with the expected and found CRC32.
4. The patched output replaces the file in the working copy only; the original is in the backup of section 9.
5. A failed patch rolls the import back.
6. The row editor shows file, patch, state (not applied, applied, failed) and "Apply Now" for testing against the base.
7. Creating a BPS patch is not part of this spec. The patch tools in the Mod menu make `.g3mpatch` and xdelta patches.

## 9. Project scripts and game file backup

### 9.1 Project scripts (UTMT-PROJ-11)

Scripts run at three stages, in this order: pre-import (before anything is loaded, with `Data` null), pre-asset-import (the base is loaded and patches are applied, no project assets yet) and post-import (everything applied). They use the engine of [scripting.md](scripting.md) with a project-aware interface:

1. `Project` is set; `Data` is the model under construction; `ScriptPath` is the script file; `FilePath` is the base data file.
2. Messages go to the Project channel of Output. Questions return false and prompts throw: project scripts are non-interactive so a build gives the same result on every machine. A script that prompts fails with `G3MS-PROJ-006`: "'{script}' asked a question, which project scripts cannot do."
3. Scripts run in the order of the list. A script error stops the import (with Error on warnings off, a warning is logged and the import continues).
4. Trust: the first time a project is opened the project's scripts are listed in a trust prompt (scripting.md section 8). Until trusted the project opens in Restricted mode without running them.
5. Script paths are relative to the project folder and checked for escape (UTMT-FILE-14).

### 9.2 Game file backup (UTMT-PROJ-12)

A project import can overwrite or delete game files (the data file, external files, file copies, file deletes, patches). Before any change:

1. The files that will change are copied to `.g3mstudio/backup/{time}/` with a manifest (path, size, SHA-256). The manifest is written first.
2. On failure the files return from the backup and the folder is removed. On success the backup stays for the retention count (`files.backupCount`) and is listed by `run.restoreFiles` ("Restore Game Files...").
3. The same mechanism backs up mod launches ([run-game.md](run-game.md) section 5.3).
4. A crash during import leaves the manifest; the next start offers "Game files from an unfinished project import can be restored." with "Restore" and "Keep".

Streamed sound copy (UTMT-PROJ-13) is part of the same import: external streamed sounds are copied to the save directory as in [editors/sounds.md](editors/sounds.md) section 5.

## 10. UTMT 0.9 projects (UTMT-PROJ-01 to 12 compatibility)

### 10.1 Import (F-WS-10)

`file.import.utmtProject` and the Open dialog accept the project main JSON of a UTMT 0.9 project. UTMT's project files are never modified.

1. Choose the JSON and a new folder for the converted G3MStudio project (default `{name}-g3mstudio` beside it).
2. G3MStudio reads the options, then the asset folders and the loose `.gml` files (every non-hidden subfolder, as UTMT does), converts each asset to its native folder (section 5 table) and writes the `.g3mproj` in delta mode. The base is the `AssetsDataFilePath` of the options.
3. Option mapping: Name, Flags, VerifyStrings, AssetsDataFilePath, ErrorOnWarnings, ExcludeDirectories, SubProjects (kept as references, imported in order), Patches, ExternalFiles, FileCopies, FileDeletes, PreImportScripts, PreAssetImportScripts, PostImportScripts map to the options of section 7 one to one. `general-info-options.json` maps to `options/general.json`.
4. The conversion report is a dialog and a Problems group: "Converted {n} resources. {w} warnings." with items such as unknown fields (kept under `extra` and written back on export) and collisions that were renamed.
5. After the conversion the project opens (3.4). The result builds the same data file as UTMT 0.9 does (ADR-0008 acceptance).
6. Scripts in the project that use UTMT names run through the name translator (scripting.md section 3.2).

### 10.2 Export (F-WS-10)

`file.export.utmtProject`:

1. Dialog: target folder, resources to export (all, marked only, or the Explorer selection), and the same base data file path.
2. Only the 13 types of section 5 are written, in UTMT's layout; main options are written to the project JSON with UTMT's field names. GML is written as `.gml` sources.
3. Resources of other types cannot be stored. The dialog lists them with counts ("These resources have no UTMT form and are left out: 4 timelines, 2 extensions, 18 strings tables.") with "Export Anyway" and "Cancel". A project that depends on them for its build warns that UTMT will build a different file.
4. Extra fields kept from an earlier UTMT import (`extra`) are written back so UTMT-first workflows round trip.
5. The result is checked by reading it back with the same code that opens UTMT projects; a mismatch is `G3MS-PROJ-008`.
6. The manual check "open the export in UTMT 0.9" belongs to the acceptance tests (ADR-0008) and is not automated.

### 10.3 Round trips

| Path | Guarantee |
| --- | --- |
| UTMT, import, export to UTMT | The 13 types and all options are equal; extra unknown fields are kept |
| Native, export to UTMT, import | The 13 types are equal; other types are lost and listed at export |
| Native, build, native | Same data file bytes (ADR-0008 acceptance) |

## 11. Actions and shortcuts

| Command | Id | Windows and Linux | macOS |
| --- | --- | --- | --- |
| New Project Folder... | `file.new.projectFolder` | None | None |
| Open Project Folder... | `file.openFolder` | Ctrl+K Ctrl+O | Cmd+K Cmd+O |
| Close Workspace | `file.close` | Ctrl+K F | Cmd+K F |
| Export Project Folder... | `file.export.projectFolder` | None | None |
| Import UTMT Project... | `file.import.utmtProject` | None | None |
| Export as UTMT 0.9 Project... | `file.export.utmtProject` | None | None |
| Build Data File From Project | `project.build` | None | None |
| View Unexported Resources... | `project.viewUnexported` | None | None |
| Mark for Export | `project.markForExport` | None | None |
| Project Options... | `project.options` | None | None |
| Remove Unchanged Marks | `project.removeUnchangedMarks` | None | None |
| Rebase on Data File... | `project.rebase` | None | None |
| Restore Game Files... | `run.restoreFiles` | None | None |
| Unmark for Export | `project.unmark` | Delete (in the unexported list) | Delete |
| Mark All Changed | `project.markAllChanged` | None | None |

## 12. Validation and messages

| Case | Message |
| --- | --- |
| Folder not empty | "'{folder}' is not empty. Choose an empty folder." |
| Base missing | "The base data file was not found." |
| Base different | "This data file is not the one the project was made from. Open it anyway?" |
| Verify string missing | "The base data file does not contain the string '{text}'. Check that the project's base is the right game." (`G3MS-PROJ-004`) |
| BPS mismatch | `G3MS-PROJ-005` text of section 8 |
| Script prompt | `G3MS-PROJ-006` text of section 9.1 |
| Collision | "Too many resources are named '{name}'." (`G3MS-PROJ-007`) |
| UTMT export mismatch | "The exported project did not read back the same. The files are in '{folder}'." (`G3MS-PROJ-008`) |
| Project format newer | "This project was made by a newer G3MStudio (format {n}). Update G3MStudio to open it." |
| Save failure | Error pattern of ux-standards 8 with "Save As..." |
| Path leaves the project | "'{path}' leaves the project folder." |

## 13. Undo

Opening, saving and building are not undoable (saving is not an undo step, shell 14.4). Marking and unmarking for export are undo steps ("Mark '{name}' for Export"). Converting a UTMT project writes a new folder and changes nothing in the original. Closing a project and opening it again restores no undo history.

## 14. Empty and loading states

- Unexported list: "Everything is saved to the project."
- Explorer while a project imports: determinate bar "Importing project... {percent}" with Cancel (ux-standards 9); Cancel rolls back through the backup.
- Project options with no entries: each list shows its "Add..." row (list grid, ux-standards 11).
- Project without Git: the Git panel text of shell section 15.

## 15. Parity coverage

| ID | Feature | Section |
| --- | --- | --- |
| UTMT-PROJ-01 | New project | 3.3 |
| UTMT-PROJ-02 | Open project | 3.4, 10.1 |
| UTMT-PROJ-03 | Save project | 3.5, 10.2 |
| UTMT-PROJ-04 | View unexported assets | 4.2 |
| UTMT-PROJ-05 | Close project | 3.6 |
| UTMT-PROJ-06 | Automatic change tracking | 4.1 |
| UTMT-PROJ-07 | Serializable asset types | 5 |
| UTMT-PROJ-08 | GML source retention | 6 |
| UTMT-PROJ-09 | Project options file | 7 |
| UTMT-PROJ-10 | BPS patches | 8 |
| UTMT-PROJ-11 | Project scripts | 9.1 |
| UTMT-PROJ-12 | Game file backup | 9.2 |
| UTMT-PROJ-13 | Streamed sound copy | 9.2 |
| UTMT-SET-10 | Recompile all GML on project save | 6 |
| UTMT-FILE-14 | Path confinement | 7, 9.1 |
