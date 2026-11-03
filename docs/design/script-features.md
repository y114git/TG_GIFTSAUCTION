# Script features

Status: proposed. The built-in tools that replace the 110 UndertaleModTool scripts (owner decision Q42: G3MStudio ships no bundled scripts). Thirteen features for batch work across the whole data file, a list of plugin candidates for game specific patches, and the additions the Fonts editor takes from the old font script. Requirements: F-PAR-3, F-MOD-4, F-RES-6. Analysis of every script and its fate: [../research/utmt-scripts-analysis.md](../research/utmt-scripts-analysis.md). The script runner for user scripts stays as in [scripting.md](scripting.md). Shared rules: [ux-standards.md](ux-standards.md). Menus and keys: [shell.md](shell.md). Resource editors keep their own import and export commands ([editors/sprites.md](editors/sprites.md) and the others); the features here work on many resources at once, or on the file as a whole.

## 1. Purpose and entry points

Modders used scripts for jobs that touch hundreds of resources: dump everything, import a folder, search all code, repack texture pages, repair a file. G3MStudio does these jobs in the app, with a selection, a preview, progress, cancel and undo, and works the same on every platform. Nothing here is tied to one game. Each feature is small and has one job; a job that has options is one dialog, not several commands.

Entry points, the same for every feature:

1. Tools menu (shell 2): the command with its dialog.
2. Explorer context menu on a type root, a folder, or a multi-selection: the same command with the selection preset (shell 6: "Export All {Type}...", "Import All {Type}...").
3. Command palette: `>Export Resources`, `>Find and Replace in Code` and so on.
4. Editor toolbar: the editors that already have a per resource command (sprites, sounds, fonts, strings, texture pages, rooms) keep it. The Import and Export menus of those editors open the same dialog with the one resource preselected.
5. Problems panel and Find usages results: the repair and remove commands appear as "Fix" actions on findings.

G3MTool runs the same jobs without a window for scripts and automation; G3MStudio has no command line (Q24). A user can still write a script for anything not covered; scripts are the escape hatch, not the shipped feature set.

## 2. Layout

All batch features share one dialog frame (ux-standards 11): left a selection tree, right the options, bottom the plan and the buttons.

```
+--------------------------------------------------------------------------------------+
| Export Resources                                                                     |
+----------------------------------+---------------------------------------------------+
| Select                           | Options                                           |
|  [search / pattern ......] [.*]  |  Folder   [ export\game           ] [Browse]      |
|  [x] Sprites (412)               |  Layout   ( ) Flat  (x) Folder per asset  ( ) ... |
|    [x] spr_player                |  Sprites  Format [PNG frames v]  [x] Padding      |
|    [x] spr_enemy                 |  Sounds   Format [Original v]   [x] External too  |
|  [ ] Sounds (88)                 |  [x] Write settings.json beside the files         |
|  [x] Code (3,100)                |  If a file exists  [Ask v]                        |
|  [ ] Strings                     |                                                   |
+----------------------------------+---------------------------------------------------+
| Plan: 412 sprites, 3,100 code entries -> 5,230 files, about 180 MB, 3 name clashes   |
|                                  [Show Plan]            [Cancel]  [Export]           |
+--------------------------------------------------------------------------------------+
```

The tree is the same selection tree as the Explorer with checkboxes; Ctrl+A selects all, the pattern box accepts a wildcard or a regular expression (toggle `.*`), and "Selected in Explorer" fills it from the Explorer selection. The plan line is always visible and updates as options change. "Show Plan" opens the full list (resource, action, target file, warning) in a table that can be copied.

## 3. Shared behaviour

1. Plan first. Every feature that writes shows the plan before the first byte is written: counts, target files, items that will be skipped and why, name clashes. The default button of the plan is Cancel when it contains a warning.
2. Selection by checklist and pattern, never by typed names. Names in a pattern that match nothing are listed as "Not found: {n}" with the first five names.
3. Progress and cancel: one row in Jobs and the status bar (shell 7.6), "{done} of {total}" and the current item. Cancel stops after the current item. For writes into the data file, cancel before the commit leaves the file untouched; the tool works on a staged copy and swaps it in at the end, so a cancelled run never leaves half edited data (the G3MLib staged output pattern). For writes into a folder, cancel keeps the files written so far and the summary says so.
4. Undo: every feature that changes the data file is one undo step named after the command ("Import Resources", "Repack Texture Pages"). A feature that writes only to disk (export, compare) has no undo and says "Written to {folder}. Open Folder".
5. Summary: at the end one notification and one result table (item, result, message) with "Copy" and "Save Log". Failures never stop the run unless the user chose "Stop at first error".
6. Existing files: Ask, Overwrite, Skip, Rename (`name (2).png`). Default Ask, shown once per run for all clashes, not per file. Files are written truncated, never appended.
7. Names: resource names are sanitized for the file system (reserved names, trailing dots, characters the target OS forbids, case clashes on case insensitive systems); the plan shows each changed name. The original name is kept in `settings.json`.
8. Settings sidecar: exports write `settings.json` in the output root. It holds, per resource, the fields the file formats cannot carry (sprite origin, playback speed, bounding box mode, collision mask rectangle and shape, sound volume, pitch and audio group, font metrics and kerning, tileset tile size, border and columns, texture group), plus the data file's GameMaker version and a format number. Import reads it when present and falls back to the file naming rules when it is absent ("with or without settings").
9. Paths: all folders are confined; no write outside the chosen folder (UTMT-FILE-14). Working files go to the workspace temp folder, never into the program folder.
10. Newer data file versions: a feature lists the versions it supports. On an untested version it shows an information bar ("Not tested with GameMaker {version}") and continues; it never blocks (Q26).
11. Large files: images decode once per texture page and are cached for the run; parallel work is bounded by memory, not only by core count.

## 4. Features

Each feature lists what it replaces, its commands, where it appears, behaviour, and the G3MLib primitive it uses. "Gap" means the primitive is missing or too high level and G3MLib needs a small addition (collected in section 7).

### 4.1 SF-01 Export Resources

Replaces 20 scripts (all exporters, `ExportAssetOrder` is SF-11). Command `tools.batchExport` (Tools > Export All...), also on every type root in the Explorer and in the editors' Export menus. Per type formats:

| Type | Formats and options |
| --- | --- |
| Sprites | PNG frames `name_N.png` (padding on or off), PNG strip `name_stripN.png`, animated GIF (speed from the sprite, transparent background option); layout flat, folder per asset, or by texture group |
| Masks | Black and white PNG `name_N.png` in the mask rectangle; rectangle and shape go to `settings.json` |
| Tilesets and backgrounds | PNG; tile size, border and columns in `settings.json` |
| Sounds | WAV or OGG as stored; external files copied; layout flat or by audio group; a missing audio file is a warning, never an empty file; volume, pitch, group, flags in `settings.json` |
| Fonts | PNG atlas plus `glyphs_name.csv` (UTMT layout, so old tools can read it) plus full metrics, ranges and kerning in `settings.json` |
| Shaders | Folder per shader with `Type.txt`, stage sources and `.bin` blobs, `VertexShaderAttributes.txt` (round trips with SF-02) |
| Texture pages | PNG per page `index.png`; with "by texture group" also the items of each group |
| Rooms | PNG per room, drawn by the room renderer (layers, tiles, instances; optional grid and views); one file per room, written truncated |
| Code | GML (decompiled) and assembly (`.asm`); entries that fail go to `Failed/` with the error; children of an entry are not exported twice; the decompile runs in a worker so a hard failure marks one entry, not the app |
| Strings | `strings.json` as `{"strings": [...]}` in table order with the index kept; optional plain text for translators, one string per line with an index column, multi-line strings escaped |

Surface notes: The tree shows only types the file has. "Export Selected..." on an Explorer selection runs the same dialog with the selection set. Cancel and progress per section 3. No undo. G3MLib primitives: sprite frame decode with padding, texture page cache, sound read (embedded, group file, external), decompile and disassemble, collision mask decode; gap: GIF encoder (Magick.NET is a dependency, so an adapter), a room renderer call for the PNG type (the room editor owns it), the settings sidecar writer.

### 4.2 SF-02 Import Resources

Replaces 14 scripts (importers). Command `tools.batchImport` (Tools > Import All...), type roots, editors' Import menus. One dialog reads a folder (or a ZIP, or loose files) and shows a plan table before writing:

| Rule | Behaviour |
| --- | --- |
| Type detection | By folder name (`Sprites`, `Backgrounds`, `Sounds`, `Fonts`, `Shaders`, `Code`, `Masks`, `Strings`), by `settings.json`, or the type chosen in the dialog |
| Sprites and backgrounds | `name_N.png` (0 or 1 based, the base is detected and shown), `name_stripN.png`, animated GIF, single image; sheet cut by frame count and size; missing frames or duplicate sources are errors in the plan; trims transparent borders; rebuilds the collision mask (modes of the sprite editor); origin preset and speed defaults apply to new sprites only, with a checkbox "Also apply to existing" |
| Existing resource | Choice per run: Replace frames, Replace in place (same size image written into the existing page slot, no repack, size mismatch is an error), Skip, Create only |
| Missing resource | Create (default), Skip |
| Masks | Black and white PNG, size must match the mask rectangle |
| Tilesets and backgrounds | By name; size change is shown in the plan |
| Sounds | WAV and OGG; embedded, external, compressed in memory or on load; audio group per sound or from the folder name; one policy dialog for the whole run (SF-10 owns the group files) |
| Fonts | PNG plus `glyphs_name.csv` or `settings.json`, or a GameMaker `.yy` font file with its PNG; packed with the shared packer (SF-08) |
| Shaders | Folder per shader; a partial folder only changes the stages it holds (a missing stage is "unchanged", never "empty") |
| GML | `.gml` files compiled as one group so scripts can call each other; file name is the code entry name; creates the asset the name implies (object event, script, global script) when "Create assets and links" is on; the compiler diagnostics (file, line, column, message) go to Problems and nothing is written when any file fails, unless "Skip failed files" is on |
| Assembly | `.asm` files assembled into existing entries; stop at first error is an option |
| Strings | `strings.json` or text; the count must match, or the plan lists added, changed and unmatched items; changes by index only when the base file is the same (hash check), otherwise by content match with a warning |

Plan first (section 3.1); new texture pages follow the group and page format rules of the target version. Undo: one step, including the repack. G3MLib primitives: image decode to BGRA, trimming packer (SF-08), collision mask builder, sprite and background create or update, sound add and replace, GML compiler group (`CodeImportGroup`), assembler. Gaps: `.yy` font reader (G3MStudio side, System.Text.Json), GIF and strip frame splitter, a public way to re-resolve references after replacement.

### 4.3 SF-03 Search and Replace in Code and Strings

Replaces `FindAndReplace`. Commands `search.replaceInCode` (Ctrl+Shift+H, as in VS Code), `search.findInCode` (Ctrl+Shift+F). Edit menu and the code editor's context menu. Scope: all code, selected resources, one object or script and its events, or the string table. Options: case, whole word, regular expression (with groups in the replacement), multi-line. The result is the shared result list (one group per code entry, line preview, old and new text side by side, a checkbox per hit). "Replace Selected" writes the checked hits as one group compile; a decompile or compile failure marks that entry and leaves it unchanged. Strings scope edits the string table and shows the references count of each changed string. Progress and cancel per section 3; undo is one step "Replace in Code". G3MLib primitives: decompile with settings, search API (gap: search without import, with line context), compile group, string table. Existing code editor search stays for the open file.

### 4.4 SF-04 Rename Resources by Pattern

New (no script; the old copy scripts needed unique names and the rename service exists). Command `tools.batchRename` (Explorer context menu on a multi-selection and on folders, Tools menu). Pattern forms: prefix or suffix add and remove, find and replace (text or regex), numbering `{n:000}`, case change. The preview table shows old name, new name and a status (clash, invalid, unchanged); references in code, rooms, objects and the project are rewritten by the rename service as one step, with the usual "code references in strings are not changed" notice. Undo: one step. G3MLib primitive: rename service (exists on `l2/batch5`).

### 4.5 SF-05 Find Resources

Replaces `FindObjectsInRooms` and `FindObjectsWithSprite`. Command `find.resources` (Find menu, Explorer context menu "Find Usages"). A query dialog with a type of question: "Objects placed in rooms" (names or pattern, shows room, instance id, position and layer), "Objects that use a sprite or mask sprite" (names or pattern, includes parent objects), "Who uses this resource" (any type, from the reverse reference index), "Rooms that contain...". Results go to the Search results tab (shell 7.3); click opens the room editor at the instance or the object editor. Read only; no undo. G3MLib primitive: reverse reference index and find usages (exist on `l2/batch4`); gap: instance listing that includes layers and creation code references.

### 4.6 SF-06 Integrity Check and Repair

Replaces the checks and repairs among the technical scripts. Commands `tools.integrity` (check, shows findings in Problems) and `tools.integrityFix` (fix all safe findings, already in shell 2). One check list, each with severity and a repair:

| Check | Source script | Repair |
| --- | --- | --- |
| Decompile and recompile round trip of all code (compare disassembly) | CheckDecompiler | None; findings list the entries and the first difference; the check works on a copy and never changes the open file |
| Functions in the function table that nothing defines or calls | FindUnknownFunctions | Remove, only when no instruction references it; otherwise the finding lists the references |
| Code entries without a code locals entry | RestoreMissingCodeLocals | Rebuild the locals from the variable references of the code (not an empty placeholder) |
| Variable ids and header counters out of step | VariableFixer | Renumber and set the counters; also done automatically on save when variables were added |
| Code that fails to decompile | TestExportAllCode | None; export (SF-01) writes the assembly fallback |
| Unresolved references, missing code, null entries | the integrity service | As in F-MOD-4 |

The check runs on the job service, shows a progress row and writes a report (Copy, Save). Fixes are one undo step and always preceded by a checkpoint. The data file is never saved by the check. G3MLib primitives: decompiler and compiler, disassembler, function and variable tables, code locals; gaps: rebuild code locals from variable references, recompute variable counters on write.

### 4.7 SF-07 Remove Unreferenced Resources

Replaces `FindUnusedStrings` and `RemoveUnusedSounds`. Command `tools.removeUnreferenced` (Tools menu; also "Find Unreferenced Resources" in the Find menu which is the read only first step). Kinds: strings, variables, functions, sounds' embedded audio blobs (and unused audio group files), texture page items that nothing uses, empty texture pages. The dialog always starts with a report (dry run) and the remove button works on the checked rows only. References come from one reference walker over every resource type (animation curves, audio groups, backgrounds, code, extensions, fonts, objects, paths, rooms, scripts, sequences, shaders, sounds, sprites, tags, texture groups, timelines and the 2023 and newer types), so a string used only by a newer type is not reported. Removal renumbers audio ids and string indexes; a checkpoint is taken first. Audio group files are backed up into the app data backups before they are rewritten. Undo: one step. G3MLib primitive: reverse reference index; gap: a string reference visitor covering every model type, identity keyed counters, audio id renumbering.

### 4.8 SF-08 Texture Page Tools

Replaces `ReduceEmbeddedTexturePages`, `NewTextureRepacker`, `ImportGraphics_Full_Repack`, `ImportAllEmbeddedTextures` and `ScaleAllTextures`. Commands `texpage.repack`, `texpage.replacePages`, `texpage.scale` (Texture Page Viewer toolbar and Tools menu, [editors/texture-pages.md](editors/texture-pages.md)). One packer serves import, fonts and repack.

| Command | Behaviour |
| --- | --- |
| Repack (`texpage.repack`) | Scope all pages or one texture group; options page size, padding, power of two, maximum item size, items that stay alone, group by flag; moves items and rewrites only their source rectangles, so every reference (sprites, fonts, tilesets, groups) stays valid; the plan shows pages before and after and the size difference |
| Replace Pages (`texpage.replacePages`) | Replaces whole pages from `index.png`; a size change is an error unless the item rectangles are scaled too (offered) |
| Scale (`texpage.scale`) | Factor 0.25 to 10 and filter (nearest, linear, high quality); remaps item, glyph and tile rectangles with 32 bit math and edge correction; items that would overflow 16 bits make the plan fail; optional: leave room and view sizes unchanged, scale them too |

Shared: the page format follows the target version and the page's current format (PNG, QOI, BZ2 variants); texture groups keep their page lists (rebuilt, never cleared); a checkpoint is taken; undo is one step. G3MLib primitives: `TexturePageRepacker` and `TextureGroupPacker` (exist on `l2/batch6`), page decode and encode; gap: image resize with a chosen filter.

### 4.9 SF-09 Copy From Another Data File

Replaces `CopySound`, `CopySpriteBgFont`, `GameObjectCopy`, their Internal forms and `SpriteOriginCopy`. Command `file.import.resources` (File > Import > Resources From Another Data File..., exists) plus `edit.duplicate` for the same file. Dialog: open the donor (read only), pick resources in the selection tree, and one list of dependencies ("also copy: spr_x, snd_y, scr_z", missing items shown in red). Modes: Copy resources (with dependencies, name clash rule Skip, Replace or Rename with a suffix), Copy properties (choose fields: sprite origin, speed, masks and margins, sound volume and pitch, object flags; matched by name; the plan lists every change). Copies reuse the donor's page items and append new pages only when needed; audio group files are created when the target needs one; code of objects is copied as bytecode, not through the decompiler, and the result is checked. Duplicate (`edit.duplicate`) uses the same code with the donor equal to the target and the name from SF-04's pattern (default `name_copy`, made unique). Undo: one step. G3MLib primitives: resource deep clone with reference remapping, second data file reader, page item copy; gap: bytecode level code copy and dependency listing.

### 4.10 SF-10 Audio Tools

Replaces `ExternalizeAllOGGs`, the sound parts of the importers and `RemoveUnusedSounds` (group files). Commands `audio.externalize`, `audio.embed`, `audio.convert` (Sound Mixer toolbar, sound editor, Tools menu) and `tools.audioGroups` (exists). Externalize writes embedded audio as files next to the data file and rewrites the entries; Embed does the reverse; Convert changes format (WAV to OGG and OGG to WAV; an OGG that GameMaker cannot decode is reported). Group assignment moves sounds between audio groups and creates the group file when needed. Options for compressed, uncompressed in memory and on load follow the sound editor's flags. All of it works on selected sounds, shows the plan, backs up every `audiogroup*.dat` it rewrites, and is one undo step. G3MLib primitives: sound flags, embedded audio, audio group file read and write; gap: WAV and OGG encode and decode (adapter over a managed library or the system), audio group file helper that renumbers ids.

### 4.11 SF-11 Asset Order and Room Order

Replaces `ExportAssetOrder`, `ImportAssetOrder`, `MatchRoomOrderToInternalOrder`, `RealignRoomInternalOrder`. Commands `tools.assetOrder` (Tools menu; opens a dialog with Export, Import, Sort by name) and in General Info the room order actions "Sync with asset order" and "Sort asset list by room order" (editors/general-info.md). Export writes a text file (one section per type, names in index order, `(null)` for empty slots, all types including the 2.3 and newer ones); Import reorders to match it, reports missing names, and puts unlisted assets at the end. Because bytecode of old files refers to assets by index, the dialog shows how many code entries and room data would change meaning before it writes, and refuses newer files where indexes are not used. G3MLib primitives: ordered resource lists with typed room order references; gap: report of index dependent references.

### 4.12 SF-12 Compare Folders

Replaces `MergeImages`. Command `tools.compareFolders` (Tools menu, also "Compare With..." on an exported folder). Pairs files of two folders by relative path or by name (both modes), then shows a list with status (same, different, only left, only right) and a viewer: side by side, overlay with difference highlight, swipe slider, for images; a text diff for text files. "Save Side by Side Images..." writes one PNG per pair (A left, B right) into a folder, without losing subfolders. Used to review exports before and after a mod, and texture packs. Read only on the inputs; no undo. G3MLib primitive: none (image work through the shared image library). The data file level comparison stays in [editors/data-diff.md](editors/data-diff.md).

### 4.13 SF-13 Data File Tools

Replaces the bytecode converters, `ToggleAlignment`, `ExtractEmbeddedDataFile` and `GetAllChunkNames`. Commands: `tools.convertBytecode` (exists), `tools.fileSettings`, `file.open.extract`.

| Command | Behaviour |
| --- | --- |
| Convert Bytecode Version (`tools.convertBytecode`) | Only conversions the writer can verify: 15 to 16 (adds LANG and GLOB), 16 to 17 (adds a default texture group chunk), 17 to 16 (removes it). Others are not offered. The dialog states the supported matrix and why (older layouts are not testable, the 2.3 downgrade needs a code translator). Works on a staged copy, offers "Save As" only (never overwrites the original), and the result is read back and compared |
| Data File Settings (`tools.fileSettings`) | A small panel in General Info: 4 byte alignment of texture page items, GameMaker version display and the stored version, chunk list with sizes (replaces the chunk name printout; Copy as list) |
| Extract From Executable (`file.open.extract`, File > Open > Extract Data File...) | Scans an executable or memory dump for the data file header, validates the chunk table and size against the file length, lists every candidate found (offset, size, game name), and saves the chosen one; works on files over 2 GB without cast errors |

Undo: settings changes are normal undo steps; conversion and extraction write new files. G3MLib primitives: writer per version, `IsTPAG4ByteAligned`, chunk list. Gap: data file locator that scans a byte stream and returns offset and length.

## 5. Plugin candidates from UTMT scripts

Game specific or optional patches. Not built in, not shipped in 1.0.0-beta, listed for the plugin phase (ADR-0009). Most are one code patch each: replace, append, prepend, find and replace, regex find and replace, with a game check and a strict "a patch that matches nothing is an error" rule. Open item OI-053 asks whether the plugin API offers these verbs.

| ID | Plugin | Games | Source scripts | Notes |
| --- | --- | --- | --- | --- |
| P-1 | Debug Hooks | any (needs a persistent object) | GoToRoom_AutoLocatePersistentObj, FancyRoomSelect, UndertaleGoToRoom, ShowRoomName, UndertaleDebugToggler | Go to room by name or id with search, room name overlay, a key that toggles a global debug flag. Needs one asset (a font) next to the data file for the picker |
| P-2 | Event Trace and Profiler | any | ExecutionOrder, Profiler | Wraps every object event; overlay of run order, time per object, call counts, stack trace to a file. Needs instruction level patching with jump fix-up; keep out of the core |
| P-3 | Externalize Fonts | any | DeltaruneTTFFonts, UndertaleTTFFonts | Replaces font resources by runtime font loading from TTF files; the font list comes from the data file (the old scripts hard code it); backup before clearing |
| P-4 | Text to JSON Language Files | Undertale, Deltarune | UndertaleWithJSONs, DeltaruneReloadJSON | Moves game text into JSON per language, language switch and reload keys; the text extractor is game specific |
| P-5 | Undertale Borders | Undertale (per version family) | UndertaleBorderEnabler, UndertaleBorderEnablerv1_11 | Needs one patch set per version, new texture pages and the border PNGs (data folder `Borders`) |
| P-6 | Undertale and Deltarune Patches | Undertale, Deltarune | Debug, DisableDogcheck, UndertaleRunButton, UndertaleWASD, UndertaleSwitchAndXboxOnPC, UndertaleBattlegroupSelector, UndertaleChangeHomeBattlegroup, UndertaleBetterVaporiser, UndertaleDialogSimulator | Independent small patches, each its own toggle |
| P-7 | Touch Controls | Undertale, Deltarune | TouchControlsEnabler | Adds a joystick and buttons; data folder `TouchControls_data` (texture, four GML files) |
| P-8 | Randomizer | Deltarune (old builds) | DeltaHATE | Seeded shuffle of sprites, sounds and text within safe classes; low priority |

What the plugin API needs for this list (written down, not decided): the five patch verbs on a code entry or event, with a result per patch (applied, no match, already applied); `EnsureDefined` for runtime functions; create object, script and room helpers; add a texture page and sprite from images; a declarative target filter (name, display name, required resources, version range) checked before the plugin is offered; a small declared options dialog; backup and revert for a patch set.

## 6. Fonts editor additions (from FontEditor)

The script's glyph editor is Windows only and used `System.Drawing`. The Fonts editor ([editors/fonts.md](editors/fonts.md)) must cover what the script did, on the shared canvas: paint a glyph pixel by pixel (the image editor's tools), change glyph size, shift and offset, duplicate a glyph by character or id, delete a glyph, a glyph list with thumbnails, ZIP export and import with the script's name format (`char=N;shift=S;offset=O.png`, plus a CSV for empty glyphs) so old packs still load, automatic repack of the glyph rectangle on save with a "does not fit" error that offers a bigger rectangle, and range update. Beyond the script: kerning, font properties, undo, multi-select, zoom with a pixel grid, text preview. These are Fonts editor requirements; no extra command in this document.

## 7. G3MLib gaps

Collected from the feature list. Each is a single operation, not a workflow (Q25). None blocks the 1.1.0 release by itself; the owner decides which go into 1.1.0 and which into 1.1.x (open item OI-054).

| Gap | Used by |
| --- | --- |
| Code search without import (matches with line context) | SF-03 |
| Reference visitor for strings, functions, variables and audio over every model type, identity keyed | SF-05, SF-07 |
| Rebuild code locals from variable references; recompute variable counters on write | SF-06 |
| Image resize with a chosen filter | SF-08 |
| GIF encode, WAV and OGG convert | SF-01, SF-10 |
| Audio group file helper that renumbers audio ids | SF-10, SF-07 |
| Bytecode level code copy between two data files; dependency listing | SF-09 |
| Instance listing with layers and creation code references | SF-05 |
| Report of index dependent references for a reorder | SF-11 |
| Data file locator over a byte stream | SF-13 |
| Public re-resolve of cached resource references after list changes | SF-02, SF-09 |
| Model aliases for the four API differences found by the compatibility run (`uint` against `int`, `Instruction.Destination`, `KeyframeChannel.Event`, `WarningHandlerDelegate`) | user scripts (OI-040) |

## 8. Actions and shortcuts

| Command | Id | Windows and Linux | macOS |
| --- | --- | --- | --- |
| Export Resources... | `tools.batchExport` | None | None |
| Import Resources... | `tools.batchImport` | None | None |
| Find and Replace in Code... | `search.replaceInCode` | Ctrl+Shift+H | Cmd+Shift+H |
| Find in Code... | `search.findInCode` | Ctrl+Shift+F | Cmd+Shift+F |
| Rename by Pattern... | `tools.batchRename` | None | None |
| Find Resources... | `find.resources` | None | None |
| Check Data File Integrity | `tools.integrity` | None | None |
| Fix All Safe Integrity Findings | `tools.integrityFix` | None | None |
| Remove Unreferenced Resources... | `tools.removeUnreferenced` | None | None |
| Repack Texture Pages... | `texpage.repack` | None | None |
| Replace Texture Pages... | `texpage.replacePages` | None | None |
| Scale Texture Pages... | `texpage.scale` | None | None |
| Resources From Another Data File... | `file.import.resources` | None | None |
| Duplicate | `edit.duplicate` | Ctrl+D | Cmd+D |
| Externalize Audio... | `audio.externalize` | None | None |
| Embed Audio... | `audio.embed` | None | None |
| Convert Audio... | `audio.convert` | None | None |
| Audio Groups... | `tools.audioGroups` | None | None |
| Asset Order... | `tools.assetOrder` | None | None |
| Compare Folders... | `tools.compareFolders` | None | None |
| Convert Bytecode Version... | `tools.convertBytecode` | None | None |
| Data File Settings | `tools.fileSettings` | None | None |
| Extract Data File From Executable... | `file.open.extract` | None | None |
| Show Plan (in any batch dialog) | `batch.showPlan` | Ctrl+P | Cmd+P |
| Select All (in any batch dialog tree) | `batch.selectAll` | Ctrl+A | Cmd+A |

`edit.duplicate` and `tools.integrityFix` and `tools.convertBytecode` exist in shell.md; the others are new ids to add to its tables. Every command also appears in the command palette under its menu name.

## 9. Validation and messages

| Case | Message |
| --- | --- |
| No data file | "Open a data file first." (commands disabled with the same tooltip) |
| Nothing selected | "Select at least one resource." |
| Name clashes in the plan | "{n} files would have the same name. Choose a layout that keeps names apart, or let G3MStudio rename them." |
| Pattern matches nothing | "No resource matches '{pattern}'." |
| Names not found in a list | "Not found: {n}. First: {names}." |
| Image size differs on in place replace | "'{file}' is {w} x {h}, the frame is {tw} x {th}. Use Replace Frames to change the size." |
| Frames missing in a sequence | "'{sprite}' has frames 0 to 3 and 5. Frame 4 is missing." |
| Compile failure on GML import | "'{file}' line {line}: {message}. Nothing was imported." (with "Skip Failed Files") |
| Sound file missing on export | Warning "No audio data for '{name}'. No file written." |
| Export folder not writable | "Cannot write to '{folder}'. {reason}" |
| Version not tested | "Not tested with GameMaker {version}. Results may differ." |
| Repack would not shrink | "The pages would not get smaller. Nothing changed." |
| Scale overflow | "{n} items would exceed 65,535 pixels at this factor. Choose a smaller factor." |
| Audio group file will be rewritten | "{n} audio group files will be changed. Copies are kept in Backups." |
| Reorder changes code meaning | "{n} code entries and {m} rooms refer to assets by index. Reordering changes what they mean." |
| Conversion not supported | "Converting from bytecode {a} to {b} is not supported. See the supported list." |
| No data file found in executable | "No GameMaker data file was found in '{file}'." |

## 10. Undo

Section 3.4 applies. One run is one undo step with the command name. Plans, reports and exports to disk are not undone. A cancelled run changes nothing in the data file.

## 11. Empty and loading states

- A type with no resources is absent from the selection tree. When nothing is left: "This data file has nothing to export."
- Folder with no recognizable files on import: "No importable files found in '{folder}'. Names like name_0.png, name_strip4.png or name.gif are recognized." with a link to the naming help.
- Plan still being built: the plan line shows "Preparing..." with a progress ring; the primary button is disabled until it is ready.
- Big selections (tens of thousands of items): the tree virtualizes, the plan table pages by 1,000 rows.

## 12. Parity coverage

| ID | Feature | Section |
| --- | --- | --- |
| UTMT-SCRIPT-01 to UTMT-SCRIPT-07 | The runner stays for user scripts; no bundled scripts | [scripting.md](scripting.md) |
| TOOL-36 | Bundled G3MTool scripts: G3MTool keeps its own; G3MStudio ships none | 1 |
| F-PAR-3 | Every UTMT bundled script has a built-in feature, a plugin candidate, or a written reason | 4, 5; research/utmt-scripts-analysis.md |

Per feature, the scripts it covers (primary first, then merged):

| Feature | Scripts |
| --- | --- |
| SF-01 Export Resources | ExportAllAssembly, ExportAllCode, ExportAllEmbeddedTextures, ExportAllFonts, ExportAllMasks, ExportAllRoomsToPNG, ExportAllShaders, ExportAllSounds, ExportAllSprites, ExportAllStringsJSON, ExportAllTilesets, ExportSpritesAsGIF; merged: ExportAllStrings, ExportAllTextures, ExportAllTexturesGrouped, ExportSpecificCode, ExportSpecificSprites, ExportTextureGroups, ExportAllCodeSync, TestExportAllCode |
| SF-02 Import Resources | ApplyBasicGraphicsMod, ImportAllStringsJSON, ImportAllTilesets, ImportAssembly, ImportFonts, ImportGML, ImportGraphics, ImportGraphicsAdvanced, ImportMasks, ImportShaders, ImportSounds; merged: ImportAllStrings, ImportGMS2FontData, ImportSingleSound |
| SF-03 Search and Replace | FindAndReplace |
| SF-04 Rename by Pattern | no script (new) |
| SF-05 Find Resources | FindObjectsInRooms; merged: FindObjectsWithSprite |
| SF-06 Integrity Check and Repair | CheckDecompiler; merged: FindUnknownFunctions, RestoreMissingCodeLocals, VariableFixer |
| SF-07 Remove Unreferenced Resources | FindUnusedStrings; merged: RemoveUnusedSounds |
| SF-08 Texture Page Tools | ImportAllEmbeddedTextures, ReduceEmbeddedTexturePages, ScaleAllTextures; merged: NewTextureRepacker, ImportGraphics_Full_Repack |
| SF-09 Copy From Another Data File | CopySpriteBgFont; merged: CopySound, CopySoundInternal, CopySpriteBgFontInternal, GameObjectCopy, GameObjectCopyInternal, SpriteOriginCopy |
| SF-10 Audio Tools | ExternalizeAllOGGs |
| SF-11 Asset Order and Room Order | ExportAssetOrder; merged: ImportAssetOrder, MatchRoomOrderToInternalOrder, RealignRoomInternalOrder |
| SF-12 Compare Folders | MergeImages |
| SF-13 Data File Tools | ConvertFrom17to16, ExtractEmbeddedDataFile; merged: 15_To_16, 15_to_17_To_16, 16_To_17, ToggleAlignment |
| Fonts editor | FontEditor (merged) |
| Plugin candidates (24) | section 5 |
| Dropped (18) | research/utmt-scripts-analysis.md, "Decisions grouped" |
