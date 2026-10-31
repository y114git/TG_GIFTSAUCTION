# Modding suite: feature inventory for G3MStudio

Research date: 2026-10-25.

## Versions researched

| Item | Version | Source |
|---|---|---|
| G3MTool CLI | 1.3.0 | `G3MTool/README.md`, `G3MTool/G3MToolCLI/CHANGELOG.md` |
| G3MTool GUI | 1.0.0 | `G3MTool/G3MToolGUI/CHANGELOG.md` |
| G3M-MCE | 1.0.0 (package.json), targets G3M 3.4 | `G3M-MCE/package.json`, `G3M-MCE/src/locales/en.json` |
| G3M | 3.4.3 (`APP_VERSION`) | `G3M/src/config/config.py` |
| mod_config.json | 2.0.0 | `G3M/src/utils/mod/config.py` |

Source was read only. Nothing was built or run. Paths are relative to the folder that holds the G3M repositories.

Status key: planned, in progress, implemented, verified, not applicable (with a reason). Update the Status column as work lands.

## 1. G3MTool CLI

Entry point: `G3MTool/G3MToolCLI/Program.cs`. Commands: `G3MTool/G3MToolCLI/Commands/`. Patching, merging, diffing and caching run in G3MLib; the CLI manages batch jobs and scripts.

| ID | Feature | Details | Source path | Status |
|---|---|---|---|---|
| TOOL-01 | Interactive prompt | No arguments starts a `(G3MTool)` prompt with a banner. Accepts `help`, `exit`/`quit`, `clear`/`cls`, double-quoted arguments. Exits with 0 when stdin closes. | `G3MTool/G3MToolCLI/Program.cs` (`RunInteractiveMode`, `ParseCommandLine`) | planned |
| TOOL-02 | Verbose output | `-v`, `--verbose`, recursive on every command. | `G3MTool/G3MToolCLI/Program.cs` | planned |
| TOOL-03 | Log file | `-l`, `--log <path>`. `--log default` writes `logs/{command}_{yyyyMMdd_HHmmss}.log` next to the executable. | `G3MTool/G3MToolCLI/Program.cs` (`ResolveLogPath`), `G3MTool/G3MToolCLI/Services/Logging/FileLogWriter.cs` | planned |
| TOOL-04 | JSON output | `--json`, recursive. Suppresses normal logging and progress. Per-command schemas, see section "Compatibility constraints". | `G3MTool/G3MToolCLI/Commands/PatchCommand.cs`, `DiffCommand.cs`, `InfoCommand.cs`; `G3MWiki/G3MTool/automation.md` | planned |
| TOOL-05 | Version | `--version` or `-V` prints the CLI version. No `version` subcommand. | `G3MTool/G3MToolCLI/Program.cs`, `G3MWiki/G3MTool/commands/version.md` | planned |
| TOOL-06 | Custom xdelta binary | `--xdelta-path <path>`, recursive. Overrides the bundled xdelta for every command. | `G3MTool/G3MToolCLI/Program.cs`, `Services/Execution/EmbeddedXDeltaPathProvider.cs` | planned |
| TOOL-07 | patch create | `patch create <original> <input> [output]`. Input can be `.g3mpatch`, `.xdelta`, `.vcdiff`, `.csx` or a data file. The input is materialized against `original` and the result validated before the patch is written. Default output `patch_{timestamp}.g3mpatch` next to the executable. | `G3MTool/G3MToolCLI/Commands/PatchCommand.cs` L28-133 | planned |
| TOOL-08 | Create xdelta instead | `patch create --xdelta` writes `.xdelta`. Cannot combine with `--xdelta-fallback`. | `G3MTool/G3MToolCLI/Commands/PatchCommand.cs` | planned |
| TOOL-09 | Embedded xdelta fallback | `patch create --xdelta-fallback` stores an exact binary patch under `Xdelta/` inside the `.g3mpatch`. | `G3MTool/G3MToolCLI/Commands/PatchCommand.cs`, `G3MWiki/G3MTool/patch-format.md` | planned |
| TOOL-10 | Analysis cache | `--cache <dir>` on patch create/apply/validate/merge, batch, info and diff. Reads and writes `.g3mcache` files: metadata, resource hashes, duplicate-name counts, order-sensitive names. Ignored when source size or MD5 changed. | `G3MTool/README.md`, G3MLib `G3MCacheService` | planned |
| TOOL-11 | patch apply | `patch apply <data> <patch> [output]`. Patch can be `.g3mpatch`, `.xdelta`, `.vcdiff`, `.csx` or a data file. A `.csx` gets `data` as `ScriptGlobals.Data`; the result is saved and reopened before use. Default output is the data filename next to the executable. `--cache` is accepted but not in the README. | `G3MTool/G3MToolCLI/Commands/PatchCommand.cs` L135-214, `Services/Patching/ScriptPatchInputService.cs` | planned |
| TOOL-12 | Apply fallback order | Default: normal `.g3mpatch` apply, then embedded xdelta if apply fails and the patch has one. With `--xdelta-fallback`: embedded xdelta first, then normal apply. | `G3MTool/README.md` | planned |
| TOOL-13 | patch validate | `patch validate <patch> [-d, --data <data-file>] [--cache <dir>]`. Checks the archive and manifest; with `--data`, checks compatibility with that data file. | `G3MTool/G3MToolCLI/Commands/PatchCommand.cs` L216-274 | planned |
| TOOL-14 | patch merge | `patch merge <original> <p1> <p2> [p3...]`. Inputs low to high priority. Every input is derived independently from the same original, then resources are merged. Inputs can mix `.g3mpatch`, `.xdelta`, data files and `.csx`. | `G3MTool/G3MToolCLI/Commands/PatchCommand.cs` L421-490 | planned |
| TOOL-15 | Merge outputs | `-a, --apply <path>` writes merged data; `-o, --out <path>` keeps the merged `.g3mpatch`. Without either, writes a merged `.g3mpatch` (README: next to the executable; wiki `commands/patch.md` says `merged_<timestamp>.g3mpatch` beside the original, so the docs disagree). | `G3MTool/G3MToolCLI/Commands/PatchCommand.cs`, `G3MWiki/G3MTool/commands/patch.md` | planned |
| TOOL-16 | Code merge | `--code`: 3-way merge of GML code files. | `G3MTool/G3MToolCLI/Commands/PatchCommand.cs` | planned |
| TOOL-17 | Property merge | `--properties`: deep merge of JSON property files. | `G3MTool/G3MToolCLI/Commands/PatchCommand.cs` | planned |
| TOOL-18 | Merge report | `-r, --report <path>` writes a Markdown merge report (conflicts and decisions). | `G3MTool/G3MToolCLI/Commands/PatchCommand.cs` | planned |
| TOOL-19 | Sequential low-memory merge | `--sequential`. Not compatible with `--code` or `--properties`. Missing from the README, present in the wiki. | `G3MTool/G3MToolCLI/Commands/PatchCommand.cs` L432 | planned |
| TOOL-20 | patch batch apply | `patch batch apply <original> <patches...> --out-dir <dir> [--cache] [--continue-on-error] [--xdelta-fallback]`. Output name `{original}_{input}{original-ext}`. | `G3MTool/G3MToolCLI/Commands/PatchCommand.cs` L276-315, `Services/Patching/BatchPatchService.cs` L51 | planned |
| TOOL-21 | patch batch create | `patch batch create <original> <modified...> --out-dir <dir> [--xdelta] [--cache] [--continue-on-error] [--xdelta-fallback]`. Output name `{input}.g3mpatch` or `.xdelta`. | `G3MTool/G3MToolCLI/Commands/PatchCommand.cs` L316-365, `BatchPatchService.cs` L59 | planned |
| TOOL-22 | patch batch merge | `patch batch merge <original> <sets...> [--apply <data-dir>] [--out <patch-dir>] [--cache] [--continue-on-error] [--code] [--properties] [--report]`. Each set is one comma-separated low-to-high list. Output stem `merge_{NNN}_{first five input names}`; existing outputs get `_2`, `_3` suffixes so earlier results survive. Data output defaults to the current directory. | `G3MTool/G3MToolCLI/Commands/PatchCommand.cs` L367-419, `BatchPatchService.cs` L66-90, L268 | planned |
| TOOL-23 | Batch deduplication | Inputs are hashed before work; repeated identical jobs are skipped and the earlier output is copied under the next name. Jobs run sequentially; G3MLib may parallelize analysis inside a job. | `G3MTool/G3MToolCLI/Services/Patching/BatchPatchService.cs` | planned |
| TOOL-24 | diff | `diff <file1> <file2> [output-dir] [--full] [--cache <dir>]`. Data-vs-data, data-vs-patch or patch-vs-patch. Writes a Markdown report into `output-dir` (default `diff/` next to the executable). Default mode: resource-level changes, changed text-file counts, resource counts, asset order/index differences, sprite frame differences, selected reference checks. | `G3MTool/G3MToolCLI/Commands/DiffCommand.cs` | planned |
| TOOL-25 | Full diff | `--full` adds exact text/code/JSON hunks and deeper TPI, reference and asset-order detail. | `G3MTool/G3MToolCLI/Commands/DiffCommand.cs` | planned |
| TOOL-26 | diff JSON | With `--json`: one object with output path, mode, total and per-type counts, text-diff count, warnings. Markdown still written. | `G3MTool/G3MToolCLI/Commands/DiffCommand.cs` L53-102 | planned |
| TOOL-27 | info | `info <target> [-v] [--cache <dir>]`. Data file: resource counts, GeneralInfo, short breakdowns. `.g3mpatch`: manifest summary. | `G3MTool/G3MToolCLI/Commands/InfoCommand.cs` | planned |
| TOOL-28 | Verbose info | `-v`: per-resource listings for data files; resource counts by type from the manifest for patches. Verbose always rereads the data file. | `G3MTool/G3MToolCLI/Commands/InfoCommand.cs` | planned |
| TOOL-29 | info JSON and cache | `--json` prints data metadata (keys such as `File`, `Resources`) or the raw patch manifest, without a `success` wrapper. `--cache` stores the non-verbose info snapshot. | `G3MTool/G3MToolCLI/Commands/InfoCommand.cs` L93, L235, L651 | planned |
| TOOL-30 | xpatch create | `xpatch create <original> <modified> [output]`. Default `{modified-name}.xdelta` next to the executable. | `G3MTool/G3MToolCLI/Commands/XPatchCommand.cs` | planned |
| TOOL-31 | xpatch apply | `xpatch apply <original> <patch> [output]`. Default `{original-stem}_patched{ext}`. | `G3MTool/G3MToolCLI/Commands/XPatchCommand.cs` | planned |
| TOOL-32 | Run external program | `execute <target> [args]`; use `--` before program arguments. Exit code is the program's exit status. | `G3MTool/G3MToolCLI/Commands/ExecuteCommand.cs`, `Services/Execution/ExternalProcessService.cs` | planned |
| TOOL-33 | Run CSX script | `execute <script.csx> [args] -d, --data <file> -o, --output <file> [-i, --input <dir>]`. `--input` becomes the first script argument (`InputDir`). Output is written after success. | `G3MTool/G3MToolCLI/Commands/ExecuteCommand.cs`, `Services/Execution/ExecuteService.cs` | planned |
| TOOL-34 | Run xdelta | `execute xdelta -- <args>`; `--xdelta-path` goes before `--`. | `G3MTool/G3MToolCLI/Commands/ExecuteCommand.cs` | planned |
| TOOL-35 | CSX host API | Roslyn scripting with default imports (System namespaces, `G3MLib.DataFile`, `.Models`, `.Util`, `.Decompiler`, `.Compiler`, `.Scripting`, `ImageMagick`). Globals: `Data`, `FilePath`, `DataFilePath`, `ScriptPath`, `ExePath`, `InputDir`, `OutputDir`, `MainThreadAction`, `Verbose`. Helpers: `EnsureDataLoaded`, `ScriptError`, `ScriptMessage`, `ScriptWarning`, `RunUMTScript`, `GetDisassemblyText`, `ScriptQuestion`, `ScriptInputDialog`, `SimpleTextInput`, `PromptLoadFile`, `PromptSaveFile`, `PromptChooseDirectory`, progress-bar helpers, UMT stubs (`ChangeSelection`, `SyncBinding`). `#load` resolves from the script directory; escapes are rejected. CLI prompts return defaults or null. | `G3MTool/G3MToolCLI/Models/Scripting/ScriptGlobals.cs`, `Services/Execution/ExecuteService.cs`, `G3MWiki/G3MTool/csx-scripting.md` | planned |
| TOOL-36 | Bundled scripts | 36 scripts: Export and Import for AssetOrder, AudioGroups, Backgrounds, CodeEntries, EmbeddedTextures, Extensions, Fonts, GameObjects, GeneralInfo, Paths, Rooms, Shaders, Sounds, Sprites, TextureGroupInfo, TexturePageItems, Tilesets, Timelines. | `G3MTool/G3MToolCLI/Assets/scripts/` | planned |
| TOOL-37 | Data-file formats | `.win`, `.ios`, `.droid`, `.unx`. | `G3MTool/README.md` | planned |
| TOOL-38 | Console streams | Progress on stdout as `\r{operation}: {N}%` (monotonic per operation, newline at completion). Info on stdout, `[Warning] ` lines on stdout, `Error: ` lines on stderr. | `G3MTool/G3MToolCLI/Services/Logging/LogService.cs` L70-138 | planned |
| TOOL-39 | Exit codes | 0 on success; 1 on argument, input or tool failure (`Environment.ExitCode = 1`); `execute` returns the child's code. A merge with conflicts still exits 0. | `G3MTool/G3MToolCLI/Commands/*.cs`, `G3MWiki/G3MTool/automation.md` | planned |
| TOOL-40 | Validated data writes | Data outputs are validated before they replace a file; previous batch outputs are preserved. | `G3MTool/G3MToolCLI/CHANGELOG.md` | planned |
| TOOL-41 | Game-specific data | `GameSpecificResolver.BaseDirectory` is the executable directory, so `GameSpecificData/` must sit next to the binary. `ModdingMetadata.ToolName`/`ToolVersion` stamp the manifest `tool` field. | `G3MTool/G3MToolCLI/Program.cs` L27-29 | planned |

## 2. G3MTool GUI

Entry: `G3MTool/G3MToolGUI/MainWindow.axaml(.cs)`. Operations: `G3MTool/G3MToolGUI/Services/ModdingOperationService.cs`. The GUI calls G3MLib directly, not the CLI.

| ID | Feature | Details | Source path | Status |
|---|---|---|---|---|
| TOOL-42 | Operation selector | 11 operations: Create patch, Apply patch, Validate patch, Merge patches, Compare files, Inspect file, Create XDelta, Apply XDelta, Run XDelta arguments, Run script, Run program. Labels, options and run-button text adapt to the operation. | `G3MTool/G3MToolGUI/MainWindow.axaml.cs` `ConfigureOperation`, `Services/ModdingOperationService.cs` enum `ModdingOperation` | planned |
| TOOL-43 | Path inputs | Browse for original, primary, additional inputs, output and cache folder. Add files appends to a list. Multi-value fields take one item per line. | `G3MTool/G3MToolGUI/MainWindow.axaml.cs` `BrowseOriginal`, `BrowsePrimary`, `BrowseAdditional`, `BrowseOutput`, `BrowseCache` | planned |
| TOOL-44 | Batch mode | Turns Create, Apply or Merge into independent jobs on one original. Create and Apply use an output directory; Merge takes one comma-separated low-to-high set per line, a data output directory and an optional patch directory. | `G3MTool/G3MToolGUI/Services/ModdingOperationService.cs` `BatchCreateAsync`, `BatchApplyAsync`, `BatchMergeAsync` | planned |
| TOOL-45 | XDelta options | Use XDelta fallback (embed on create, try binary on apply). Create XDelta output in batch create; not combinable with fallback. | `G3MTool/G3MToolGUI/Services/ModdingOperationService.cs` | planned |
| TOOL-46 | Continue after errors | Batch jobs keep running after a failure. | `G3MTool/G3MToolGUI/Services/ModdingOperationService.cs` | planned |
| TOOL-47 | Merge options | Merge GML code, Merge JSON properties, Low-memory merge (sequential, single merge only, excludes the two others). Unchecked options resolve overlaps by priority. | `G3MTool/G3MToolGUI/Services/ModdingOperationService.cs` `ModdingOperationRequest` | planned |
| TOOL-48 | Detailed report | Full diff for Compare, Markdown report for Merge. | `G3MTool/G3MToolGUI/Services/ModdingOperationService.cs` `FullReport` | planned |
| TOOL-49 | Cache folder | Optional `.g3mcache` directory for all supporting operations. | `G3MTool/G3MToolGUI/Services/ModdingOperationService.cs` `CacheOptions` | planned |
| TOOL-50 | Default output extension | Save dialog suggests `md` for Compare, `xdelta` for Create XDelta, `win` for Apply, Apply XDelta and Run script. | `G3MTool/G3MToolGUI/MainWindow.axaml.cs` L172-174 | planned |
| TOOL-51 | Apply through g3mpatch | Apply converts any supported input to a temporary `.g3mpatch` with `PatchService.EnsureG3MPatchAsync`, then applies it. | `G3MTool/G3MToolGUI/Services/ModdingOperationService.cs` L131 | planned |
| TOOL-52 | Inspect | Shows data-file or patch info in the Activity log and writes the info cache. | `G3MTool/G3MToolGUI/Services/ModdingOperationService.cs` L220-235 | planned |
| TOOL-53 | Activity panel | Live progress bar and operation label, selectable colour-coded log, Expand, Save to file, Clear. | `G3MTool/G3MToolGUI/MainWindow.axaml.cs` `AddLog`, `RenderLog`, `UpdateProgress`, `ToggleLogs`, `SaveLog`, `ClearLog` | planned |
| TOOL-54 | Interactive scripts | `ScriptInteraction`: Question, Input (title, label, default, multiline), OpenFile, SaveFile, ChooseDirectory. Cancel returns no selection. Script work runs off the UI thread. | `G3MTool/G3MToolGUI/Services/ScriptInteraction.cs`, `Services/ScriptExecutionService.cs` | planned |
| TOOL-55 | Job lifecycle | Run is disabled during a job; closing the window waits for the job. No Cancel button. | `G3MTool/G3MToolGUI/MainWindow.axaml.cs` `RunOperation`, `G3MWiki/G3MTool/gui.md` | planned |
| TOOL-56 | Form reset | Clear resets fields per operation; does not delete outputs. | `G3MTool/G3MToolGUI/MainWindow.axaml.cs` `ClearForm`, `ResetOperationFields` | planned |
| TOOL-57 | Safe output replace | Validated data-file writes with recovery when replacing an output fails. | `G3MTool/G3MToolGUI/CHANGELOG.md` | planned |
| TOOL-58 | Community links | Boosty, Telegram, Discord and website buttons. | `G3MTool/G3MToolGUI/MainWindow.axaml.cs` `OpenSocialLink` | planned |

## 3. G3M-MCE (web mod creator and editor)

Main editor: `G3M-MCE/src/components/ModEditor/ModEditor.jsx`. Packaging: `G3M-MCE/src/utils/zipHandler.js`. Schema: `G3M-MCE/src/data/modConfig.js`.

| ID | Feature | Details | Source path | Status |
|---|---|---|---|---|
| MCE-01 | Start page | Create Mod, Edit Mod, Download G3M link. Drop an archive, config or file to open it. | `G3M-MCE/src/pages/Home.jsx`, `EditMod.jsx`, `CreateMod.jsx` | planned |
| MCE-02 | Open G3M ZIP | Finds the single `mod_config.json` whose folder contains every file (mod root); rejects ambiguous roots and files outside the root; ignores `__MACOSX/`; migrates the config. | `G3M-MCE/src/utils/zipHandler.js` `importZipArchive` | planned |
| MCE-03 | Open config alone | `mod_config.json` opens without bundled files; referenced files must be added before export. | `G3M-MCE/src/utils/zipHandler.js` `importConfigFile` | planned |
| MCE-04 | Open plain ZIP or file | A ZIP without manifest or `modding.xml`, or any single file, starts an empty config with the contents bundled. | `G3M-MCE/src/utils/zipHandler.js` (`format: 'payload'`) | planned |
| MCE-05 | Deltamod import | Needs one root `meta.json`, `meta.toml` or `_deltamodInfo.json` plus `modding.xml` (each at most 4 MiB, no DOCTYPE/ENTITY, 1 to 5000 `<patch>` entries). Maps game IDs (`toby.deltarune` to `deltarune`, `toby.deltarune.demo`/`.demolts` to `deltarunedemo`, `toby.undertale`, `fans.utyellow`, `other.pizzatower`, `other.frickbears3`). Patch types `xdelta`, `g3mpatch`, `csx` become `patch` (an `xdelta` entry with a `.win` source becomes `overwrite`); `copy`, `override` become `overwrite`. ID from `packageID` or `local_{name}_{uuid8}`. Root docs become `info`; `icon.*`/`_icon.*` becomes the icon; `deltaruneTargetVersion`/`undertaleTargetVersion` becomes `game_version`; tags normalized. Chapter prefixes are stripped for non-DELTARUNE games. | `G3M-MCE/src/utils/modConverter.js` | planned |
| MCE-06 | Legacy config migration | Config version empty, null or `1.0.0` with object-shaped `files`: merges `metadata`, maps section keys (`-1` deltarune, `-10` deltarunedemo, `-20` undertale, `-30` undertaleyellow, `-40` pizzatower, `-50` sugaryspire, `-60` frickbears3, `0`-`5` to `deltarune_N`), turns `data_file_path`/`data_file_url` into a `patch` on `{root}/data.win`, turns `extra_files` into copy/extract operations by target mode (`install`/`game_folder`, `data`/`game_data_folder`, `dependency`/`none`, `custom`). Pizza Tower `towers` and FRICKBEARS3 `addons` go to `${game_data_path}`. Resolves sources under `chapter_N`, `chapterN_windows`, `chapterN_mac` or game folders. | `G3M-MCE/src/utils/migrationService.js` | planned |
| MCE-07 | Metadata tab | Game (7 built-ins: deltarune, deltarunedemo, undertale, undertaleyellow, pizzatower, sugaryspire, frickbears3) or Custom game ID, Mod name, Mod ID, Overall mod version, Authors (one per line), Description, Homepage, Icon (bundled path or image URL, preview), Tags, Game Version. | `G3M-MCE/src/components/ModEditor/ModEditor.jsx`, `src/locales/en.json` | planned |
| MCE-08 | Tags | Allowed: `textedit`, `customization`, `gameplay`, `other`, `CYOP/AFOM` (pizzatower only), `FB-Addon` (frickbears3 only). Imported labels map by keyword to the closest tag, unknown to `other`, deduplicated. | `G3M-MCE/src/data/modConfig.js`, `src/utils/tags.js` | planned |
| MCE-09 | Compatibility tab | Dependencies and Conflicts lists: Mod ID plus Order (No order, before, after, before-step, after-step, before-priority, after-priority). | `G3M-MCE/src/components/ModEditor/ModEditor.jsx`, `src/data/modConfig.js` `normalizeRelation` | planned |
| MCE-10 | Files tab operations | Add operation; Source, Target, Type (8 types), Include source hash, Include target hash. Info has no target. | `G3M-MCE/src/components/ModEditor/ModEditor.jsx`, `src/data/operations.js` | planned |
| MCE-11 | Groups | Add group, name it, nest groups, remove with confirmation. Names `source` and `type` are reserved. | `G3M-MCE/src/data/operations.js` `groupPaths`, `ModEditor.jsx` | planned |
| MCE-12 | Reorder | Drag operations to reorder or drop into groups; arrow buttons do the same. Dragging over a tab opens it. | `G3M-MCE/src/data/operations.js` `moveOperation` | planned |
| MCE-13 | Bundled files tree | Lists files and folders, search, Add files, Add folder (keeps structure), remove with confirmation, click to set the selected operation's source or create one. Files without operations stay in the ZIP. Duplicate paths are refused. | `G3M-MCE/src/components/ModEditor/PackageTree.jsx`, `src/utils/fileDrop.js` | planned |
| MCE-14 | Drop targets | Drop on Source (sets path), Icon (image or URL), placeholder path, hash fields (hash without bundling). Drag a bundled file onto a field using MIME type `application/g3m-package-path`. | `G3M-MCE/src/utils/fileDrop.js` | planned |
| MCE-15 | Browse bundled ZIP | Choose a file or folder inside a bundled ZIP as source (`${mod_path}/x.zip/member`). | `G3M-MCE/src/utils/zipHandler.js` `listPackageZipMembers` | planned |
| MCE-16 | Hash calculation | Source hash for a bundled file, folder or ZIP member, same algorithm as G3M (see section "G3M mod package format"). Target hash from a local original file that is not bundled. Hashes for 7z, RAR, TAR and LZMA members are left to G3M. | `G3M-MCE/src/utils/zipHandler.js` `hashPackagePath` | planned |
| MCE-17 | Placeholders tab | Custom placeholder name and path; unique names; built-in names reserved. | `G3M-MCE/src/components/ModEditor/ModEditor.jsx` | planned |
| MCE-18 | Help tab | Help for metadata, placeholders, custom placeholders, operations, processing order, compatibility and hashes. | `G3M-MCE/src/locales/en.json` `mod_editor_help_*` | planned |
| MCE-19 | Config preview | View the generated `mod_config.json`. | `G3M-MCE/src/components/ModEditor/ModEditor.jsx` (`configPreview`) | planned |
| MCE-20 | Validation UI | Fields are highlighted after edit or save attempt; error links open the tab and field, including inside collapsed groups. | `G3M-MCE/src/data/modConfig.js` `validateModConfig`, `ModEditor.jsx` | planned |
| MCE-21 | Package checks | Every source must be bundled (files, folders, ZIP members); declared source hashes are recomputed. Root `meta.json`, `meta.toml`, `_deltamodInfo.json` and a bundled `mod_config.json` are rejected. | `G3M-MCE/src/utils/zipHandler.js` `validatePackage` | planned |
| MCE-22 | Package limits | `MAX_PACKAGE_ENTRIES = 10000`, `MAX_PACKAGE_BYTES = 1 GiB`, 512 MiB per file. Rejects encrypted and split ZIPs, symlinks, ratio above 1000:1 on import, duplicate paths, case conflicts, a file used as a folder. Path rules: NFC, at most 1024 chars, no backslash, colon, control/format chars, leading slash, empty/`.`/`..` segments, trailing space or dot, `<>"|?*`, Windows reserved names. | `G3M-MCE/src/utils/zipHandler.js` L12-13, `safePackagePath`, `checkPaths`, `inspectZip` | planned |
| MCE-23 | Save ZIP | Writes canonical `mod_config.json` (2-space indent, trailing newline) at the root, all files and directories (including empty ones), DEFLATE level 6; entries over 1000:1 are re-stored uncompressed. Filename `{name}-{version}.zip`, sanitized (version at most 64 bytes, total about 180 bytes). | `G3M-MCE/src/utils/zipHandler.js` `exportModArchive`, `archiveFilename` | planned |
| MCE-24 | Export to G3M | After Save ZIP, a dialog asks for the saved file's full path and opens `g3m://file:///...` built by `buildG3MLink`. | `G3M-MCE/src/utils/g3mProtocol.js`, `ModEditor.jsx` | planned |
| MCE-25 | Unsaved changes guard | Asks before navigation or reload when edits are unsaved. | `G3M-MCE/src/components/ModEditor/ModEditor.jsx`, `src/navigation.jsx` | planned |
| MCE-26 | Localization | en, es, ja, ko, ru, zh_cn, zh_tw with a language selector. | `G3M-MCE/src/locales/`, `src/components/LanguageSelector/` | planned |

## 4. G3M (modder-facing parts)

| ID | Feature | Details | Source path | Status |
|---|---|---|---|---|
| G3M-01 | mod_config.json validation | Format 2.0.0, field rules and limits listed in section "G3M mod package format". | `G3M/src/utils/mod/config.py`, `G3MWiki/G3M/mods/mod-config.md` | planned |
| G3M-02 | Legacy config migration | 1.x configs are converted to 2.0.0 on import, including chapter ID migration. | `G3M/src/utils/mod/legacy_config_migration.py`, `G3M/src/services/migration_service.py` | planned |
| G3M-03 | Deltamod conversion | JSON/TOML metadata plus `Modding.XML`. Game map lacks `other.frickbears3`, which MCE has. | `G3M/src/adapters/deltamod_adapter.py` | planned |
| G3M-04 | PizzaOven and CYOP/AFOM import | Converts PizzaOven packages. For `data.win` it tries `patch create` to `data.g3mpatch`, then `xpatch create` to `data.win.xdelta`, then a full `data.win`; each patch is accepted only if reapplying it reproduces the modified bytes exactly. | `G3M/src/services/pizza_oven_conversion_service.py` L1005-1060, `G3M/src/services/pizza_tower_afom_service.py`, `G3M/src/utils/patching/patch_verification_utils.py`, `G3MWiki/G3M/mods/po-conversion.md` | planned |
| G3M-05 | Manual installation | Builds a mod from unconfigured files. Actions: Copy/Replace, Apply patch, INFO/README, Extract, Skip. Auto-matches `.g3mpatch` by `g3mpatch.json` `original.md5`/`size`/`modified.md5` and `.xdelta` by its Adler-32 source checksum against game files. Save and configure opens the editor. | `G3M/src/ui/dialogs/manual_install/detection.py`, `dialog.py`, `workers.py`, `G3MWiki/G3M/mods/importing.md` | planned |
| G3M-06 | Import with versions | Packages with `mod_versions/` ask which versions to install and which is active; only versions whose config is current or migratable are offered. | `G3M/src/utils/mod/version_package.py`, `version_utils.py` | planned |
| G3M-07 | Duplicate ID handling | Merge saves the incoming package as a version; Replace installs it in place. | `G3MWiki/G3M/mods/importing.md` | planned |
| G3M-08 | Unknown game | Install manually or Add game (custom game dialog prefilled with the `game` ID and GameBanana game ID). Custom IDs look like `custom_*`. | `G3M/src/ui/dialogs/unknown_game_dialog.py`, `G3MWiki/G3M/games/custom-games.md` | planned |
| G3M-09 | Desktop mod editor | Metadata, Compatibility, Files, Placeholders; authors comma-separated; hash controls for folders and all archive types. | `G3M/src/ui/dialogs/mod_editor/dialog.py`, `G3MWiki/G3M/mods/mod-editor.md` | planned |
| G3M-10 | Export mod ZIP | Exports config and bundled files; asks which saved versions travel along (setting: ask, active only, all). Drag-out export from the Library. | `G3M/src/utils/mod/version_package.py` `preset_export`, `G3MWiki/G3M/mods/exporting.md` | planned |
| G3M-11 | Mod Versions | Save current, import from file, download from GameBanana, Switch, Delete. Archives in the mod's `mod_versions/` folder. | `G3M/src/utils/mod/version_utils.py`, `G3MWiki/G3M/mods/mod-versions.md` | planned |
| G3M-12 | Create Modpack | Applies the selection and steps to a temporary game copy and bundles the result as one mod; optional XDELTA instead of full data files. | `G3M/src/workers/modpack_create_worker.py`, `G3MWiki/G3M/mods/creating-modpacks.md` | planned |
| G3M-13 | Priority and steps | Mods in one step share the same input data and are merged with `patch merge` (higher row wins); steps run sequentially on the previous step's result. | `G3M/src/services/mod_operation_executor.py` `merge_operations`, `_execute_merge`, `G3MWiki/G3M/mods/modpacks.md` | planned |
| G3M-14 | Relationship checks at launch | Activate installed dependencies, install missing GameBanana ones (`gb_mod_N`, `gb_wip_N`), apply a recommended order. | `G3M/src/utils/mod/relations.py`, `G3M/src/services/launch_service.py`, `G3MWiki/G3M/games/launch-modes.md` | planned |
| G3M-15 | Launch modes | `launch` (apply, start, restore), `launch_keep_changes`, `launch_patching_only`; the latter two need confirmation. | `G3M/src/models/launch_modes.py`, `G3M/src/services/game_runner.py` | planned |
| G3M-16 | Chapter Mode | DELTARUNE sections have their own selections and steps; direct chapter launch; not combinable with Steam launch. | `G3M/src/models/game_modes.py`, `G3MWiki/G3M/games/launch-modes.md` | planned |
| G3M-17 | Runtime target rewrite | Targets `data.win`/`game.unx`/`game.ios` become the runtime's name (Windows and Wine `data.win`, Linux `game.unx`, macOS `game.ios`); `chapterN_windows` becomes `chapterN_mac` on macOS. Sources keep their names. | `G3M/src/utils/mod/operation_plan.py` `_rewrite_target_for_runtime`, `_rewrite_target_archive_path` | planned |
| G3M-18 | Backup, restore, rollback | Backs up affected files, restores after exit, rolls back failed applications, Force restore for externally changed files. | `G3M/src/services/launch_transaction.py`, `mod_operation_executor.py`, `G3MWiki/G3M/advanced/patching-process.md` | planned |
| G3M-19 | Profiles | Per-profile mods, selection, steps, Chapter Mode; create, duplicate, rename, delete, export/import ZIP. Stored in `profiles/<name>/` with `<name>.json` and `mods_data.json`. | `G3M/src/services/profile_service.py`, `G3MWiki/G3M/features/profiles.md` | planned |
| G3M-20 | Desktop shortcuts | Save a selection and its steps as a shortcut; checks G3MTool availability. | `G3M/src/controllers/shortcut_controller.py`, `G3MWiki/G3M/features/shortcuts.md` | planned |
| G3M-21 | Mod Diagnostics | Overview, File Tree, DATA Impact, Preview/Compare, Problems; mod and resource-type filters; file and data overlap detection; relation findings. | `G3M/src/services/mod_diagnostics_service.py`, `G3M/src/ui/dialogs/mod_diagnostics_dialog.py`, `G3MWiki/G3M/features/diagnostics.md` | planned |
| G3M-22 | Analyze Actual Launch Result | Preflight runs the real operations and steps on temporary copies. Views: Actual Steps, Actual Resources, Actual Files (sizes, hashes). Export Exact Report writes HTML plus JSON. | `G3M/src/services/diagnostics/preflight_service.py` (`PreflightReport`, `PreflightStepResult`, `PreflightResourceChange`, `PreflightFileChange`) | planned |
| G3M-23 | Convert DATA Files | Converts patch/data operations of selected mods to `g3mpatch`, `xdelta`, `data.win`, `game.ios`, `game.unx` or `game.win`, saved as a new mod version. | `G3M/src/ui/dialogs/modding_tools_dialog.py`, `G3MWiki/G3M/features/modding-tools.md` | planned |
| G3M-24 | Patch tool | Modes g3mpatch, xdelta, csx; actions Create, Apply, Convert; batch create/apply; continue on error; embed xdelta fallback. CSX apply runs `execute <script> --data <orig> --output <out>`. | `G3M/src/ui/dialogs/modding_tools_dialog.py` L260-330, L940-1000 | planned |
| G3M-25 | Merge tool | List is highest priority first (reverse of the CLI); code merge, properties merge, report `{base}_merge_report.md`; batch sets. | `G3M/src/ui/dialogs/modding_tools_dialog.py` L1820-2075 | planned |
| G3M-26 | Info and Diff tools | Info shows CLI text output (Verbose option). Diff writes to a temp folder, opens the first `.md` in a Markdown viewer with export; Full report option. | `G3M/src/ui/dialogs/modding_tools_dialog.py` L2230-2320, `G3M/src/ui/dialogs/g3mtool_diff_viewer.py` | planned |
| G3M-27 | Tool settings | Custom G3MTool path (`custom_g3mtool_path`), custom xdelta path (`custom_xdelta_path`), Clear G3MTool cache (`<data>/cache/G3MTool`), code and properties merge switches (`merge_code`, `merge_properties`). | `G3M/src/adapters/g3mtool_adapter.py`, `G3M/src/services/settings_service.py`, `G3M/src/utils/path_utils.py` L133 | planned |
| G3M-28 | Logs | `logs/g3m.log`, `logs/patching.log`, `logs/conflicts.log`, `logs/shortcut.log`, archives in `logs/g3m/` and `logs/patching/`; viewer with history and export. | `G3M/src/services/log_viewer_service.py`, `G3MWiki/G3M/advanced/logging.md` | planned |
| G3M-29 | Patching warnings | `xdelta_apply_failed`, `g3mpatch_apply_failed`, `merge_failed` (critical), `patching_warning`, `steam_launch_with_mods`. | `G3M/src/services/warning_service.py` | planned |
| G3M-30 | Support Packager | Collects diagnostics with redaction; reads `g3mpatch.json` from every `.g3mpatch` (up to 8 MiB). | `G3M/src/services/support_package_service.py` L124-460 | planned |
| G3M-31 | One-click install | `g3m://` and `deltahub://` links, see section "URI schemes". | `G3M/src/app/protocol_handler.py`, `G3M/src/app/startup.py` | planned |
| G3M-32 | GameBanana Remote Install | `g3m://gb_pair/<member>/<key>` pairs; queue polled about once a minute; GameBanana files only. | `G3M/src/app/protocol_handler.py`, `G3M/src/services/remote_install_service.py` | planned |
| G3M-33 | Game Versions | `GameVersionsManager`: create (with or without a profile applied to a temp copy), apply (optional full replacement), import/export ZIP with `game_version_data.json`, delete, cancel. Archives in `game_versions/`. | `G3M/src/services/game_versions/manager.py`, `store.py`, `G3M/src/workers/game_version_archive_worker.py` | planned |
| G3M-34 | DR Save Manager plugin | Chapter 1 to 5 tabs with three main slots, named collections (copy from main, overwrite main, rename, delete), show, edit, erase, import, export; save editor with simple mode. Hooks `before_mod_apply`, `mod_apply_cancelled`, `after_restore_after_exit`, `after_mod_apply_committed` and others. | `G3M/catalog/plugins/deltarune_save_manager/` (`plugin_config.json`, `save_manager.py`, `save_editor.py`), `G3MWiki/G3M/features/deltarune-save-manager.md` | planned |
| G3M-35 | Custom Save Folders plugin | Separate save-folder name per game, profile or mod for modded sessions. | `G3M/catalog/plugins/custom_saves_folders/`, `G3MWiki/G3M/features/custom-save-folders.md` | planned |
| G3M-36 | Game detection | Recognizes data files `data.win`, `game.ios`, `game.win`, `game.unx`, `game.droid`; detects running games (blocks one-click install). | `G3M/src/services/game_detection_service.py`, `G3M/src/config/config.py` `GAME_DATA_FILENAMES` | planned |
| G3M-37 | GameBanana compatibility label | Mods Browser marks files tagged with G3M's GameBanana tool ID as "g3m" (or "deltamod"). | `G3M/src/adapters/gamebanana_adapter.py` L390, L681 | planned |

## G3M mod package format

A G3M mod is a folder or ZIP with `mod_config.json` at its root. Reader and validator: `G3M/src/utils/mod/config.py` (`parse_mod_config`, `read_mod_config_bytes`, `write_mod_config`). Path resolution and planning: `G3M/src/utils/mod/operation_plan.py`. Execution: `G3M/src/services/mod_operation_executor.py`. Install from archive: `G3M/src/workers/install/url_install_worker.py` and `helpers_install.py` (`find_mod_config`, `load_mod_config`). The web equivalent is `G3M-MCE/src/data/modConfig.js` (`validateModConfig`, `buildModConfigData`).

### Files on disk

```text
my-mod.zip
├── mod_config.json        required, UTF-8 JSON, at most 4 MiB
├── *.g3mpatch / *.xdelta / *.vcdiff / *.csx / data.win ...   payloads
├── README.md               optional, referenced by an info operation
├── icon.png                optional, referenced by "icon"
├── any other files         kept even without an operation (script deps)
└── mod_versions/           optional, saved version archives (ZIP each)
```

A ZIP whose root holds `meta.json`, `meta.toml` or `_deltamodInfo.json` is treated as Deltamod, so G3M exports must not put those names at the root. An installed mod lives in `profiles/<profile>/...` under the G3M data root; its `${mod_path}` is that folder.

### Top-level fields

| Field | Required | Rule |
|---|---|---|
| `config_version` | yes | exactly `"2.0.0"` |
| `id` | yes | `[a-z][a-z0-9_-]{0,63}` |
| `name` | yes | at most 128 chars |
| `version` | yes | non-empty string, at most 128 chars |
| `authors` | yes | array, at most 64 entries of at most 128 chars; empty allowed |
| `game` | yes | same pattern as `id`; built-ins `deltarune`, `deltarunedemo`, `undertale`, `undertaleyellow`, `pizzatower`, `sugaryspire`, `frickbears3`, or a custom ID |
| `files` | yes | ordered array of operations and groups; may be empty |
| `description` | no | at most 16,384 chars |
| `homepage` | no | absolute HTTP(S), at most 2,048 chars |
| `icon` | no | `${mod_path}/...` image or HTTP(S) URL |
| `game_version` | no | label, at most 128 chars |
| `tags` | no | non-empty array of `textedit`, `customization`, `gameplay`, `other`, `CYOP/AFOM`, `FB-Addon` (at most 64) |
| `placeholders` | no | non-empty object or null, at most 128 entries |
| `dependencies`, `conflicts` | no | non-empty array or null, at most 512 each; `id` or `id:mode` with mode `before`, `after`, `before-step`, `after-step`, `before-priority`, `after-priority` |

Unknown fields, duplicate JSON keys, nesting above 64 levels, duplicate or self relations are rejected. Display strings are NFC-normalized. Constants: `MOD_CONFIG_*` in `G3M/src/utils/mod/config.py` L24-63.

### Paths

Every path starts with one root: `${mod_path}`, `${game_path}`, `${game_data_path}`, `${user_path}`, or a custom placeholder (name `[A-Za-z][A-Za-z0-9_]{0,63}`, case-insensitively unique, value built from one built-in root, no nesting). Forward slashes only; a trailing `/` means a directory. Rejected: `..`, backslashes, network or device paths, reserved names, placeholders mid-path, targets inside `${mod_path}` (also through a custom placeholder). Raw absolute paths are accepted with a warning. Path length at most 4,096.

Archive members are addressed as `.../archive.zip/member` (ZIP, 7z, RAR, TAR, compressed TAR, LZMA readable; RAR not writable as target; no nesting). A trailing slash addresses an archive directory.

### Operations

Leaf object: `source`, `type`, `target` (all types except `info`), optional `source_hash`, `target_hash` (`sha256:` plus 64 lowercase hex). At most 5,000 operations.

| Type | Effect |
|---|---|
| `patch` | Apply `.g3mpatch`, `.xdelta`, `.vcdiff`, `.csx` or a data file to an existing target data file. `.xdelta`/`.vcdiff` go to `xpatch apply`, everything else to `patch apply`; several patches on one target in one step go to `patch merge`. |
| `overwrite` | Copy file or folder (folder itself when target ends in `/`), replacing matches. |
| `soft-overwrite` | Copy only missing items. |
| `hard-overwrite` | Clear destination (rules in `G3MWiki/G3M/mods/mod-config.md`) then copy. |
| `extract` | Copy a folder's contents or unpack an archive into the target directory; file to `.lzma` packs a stream. |
| `soft-extract` | Extract keeping existing items. |
| `hard-extract` | Clear target directory, then extract. |
| `info` | Documentation file inside the mod (`.md`, `.markdown`, `.html`, `.htm`, `.pdf`, `.txt`); no `target` or `target_hash`. |

Groups: an object with exactly one key (group name, 1 to 128 chars, unique case-insensitively, not `source` or `type`) whose value is an operation array. At most 2,000 groups and 32 nesting levels. Groups only organize; order is depth-first array order.

### Per-game and per-chapter layout

There is no per-chapter folder convention in the package. Chapters are expressed through targets: `${game_path}/chapterN_windows/data.win` addresses DELTARUNE chapter N (`_DELTARUNE_CHAPTER_TARGET_RE` in `config.py`). The runtime rewrite (G3M-17) maps data filenames and `chapterN_windows` to the player's runtime. A package can hold actions for several chapters, usually one group per chapter (`G3MWiki/G3M/mods/package-examples.md`). Legacy and Deltamod imports map sections with `section_target_root` (`G3M/src/utils/mod/operation_plan.py` L34, `G3M-MCE/src/utils/migrationService.js` `sectionTargetRoot`).

### Hashes

File hash: SHA-256 of the bytes. Directory hash (`G3M/src/utils/mod/hashing.py`, mirrored in `G3M-MCE/src/utils/zipHandler.js` `hashPackagePath`): collect all entries under the folder with NFC-normalized relative paths, sort by UTF-8 bytes, reject duplicates after normalization, then for each entry write length-prefixed fields (8-byte big-endian length plus bytes): `D` or `F`, the relative path, then for a directory an empty field, for a file an 8-byte big-endian size field followed by the raw content. A ZIP-member hash is computed over the extracted member.

### Patch payload: `.g3mpatch`

A ZIP with `g3mpatch.json` at the root plus exported changed resources; optional `Xdelta/` with an exact binary fallback. Manifest keys: `createdAt`, `tool`, `library`, `original` and `modified` (filename, `size`, `md5`, bytecode version, GameMaker version, general info), `resources` (per type: `changed`, `new`, `deleted`), `statistics`, `applyPlan`. G3M also accepts an unpacked folder containing `g3mpatch.json`. Writer: G3MLib (`G3MLib.Modding`). Readers in G3M: `G3M/src/ui/dialogs/manual_install/detection.py` `_manifest`, `G3M/src/services/support_package_service.py` `g3mpatch_manifests`. Format page: `G3MWiki/G3MTool/patch-format.md`.

### Related manifests

`plugin_config.json` (integer `config_version` 1, `api_version`), `theme_config.json` (1.0.0), `game_version_data.json` (Game Versions export), profile ZIP. These are not mods and must not be confused with `mod_config.json` (`G3MWiki/G3M/advanced/file-formats.md`).

## URI schemes

Implementation: `G3M/src/config/config.py` L30-38, `G3M/src/app/protocol_handler.py`, `G3M/src/app/startup.py` (`register_url_protocol`, `SingleInstanceServer`, argv handling L385-400), `G3M/src/app/window/runtime.py` L76.

### Schemes and registration

Schemes: `g3m` (primary) and `deltahub` (legacy), handled identically. Registration runs at startup:

- Windows: `HKCU\Software\Classes\<scheme>` with default value `URL:<SCHEME> Protocol`, empty `URL Protocol`, and `shell\open\command` = `"<exe>" "%1"` (dev: `"<python>" "<src/main.py>" "%1"`).
- Linux: `~/.local/share/applications/g3m.desktop` with `Exec=<exe> %u` and `MimeType=x-scheme-handler/g3m;x-scheme-handler/deltahub;`, then `xdg-mime default` for each.
- macOS: no registration code in `register_url_protocol`.

### Delivery

The first argv entry starting with `g3m://` or `deltahub://` is the URL. If an instance is running (QLocalSocket `SINGLE_INSTANCE_KEY`, 500 ms connect), the raw URL string is sent over the socket and the new process exits 0. The server emits `url_received_signal` for payloads starting with a known prefix. Without a URL the payload is an activate token.

### Actions

| Form | Handler | Behaviour |
|---|---|---|
| `g3m://gb_pair/<member_id>/<secret_key>[/]` | `_pair_remote_install` | Full match of `(g3m|deltahub)://gb_pair/([\w-]+)/([\w-]+)/?`, ASCII, case-insensitive. Asks once, then stores the GameBanana Remote Install key. |
| `g3m://https://host/path.zip` (or `http://`) | `_enqueue_g3m_url` | Download link. Shows `ConfirmExternalDownloadDialog`, then queues a Downloads job (`SourceKind.G3M_PROTOCOL`, `TargetKind.MOD`). Display name is the URL basename without query. |
| `g3m://https://gamebanana.com/mods/<id>` | `_enqueue_g3m_url` | After confirmation, a GameBanana mod page URL goes to `mod_service.install_from_url`. |
| `g3m://file:///C:/path/mod.zip`, `g3m://file:///home/u/mod.zip` | `_local_protocol_path` | Local file. `netloc` must be empty or `localhost`; percent-decoded; UNC paths rejected; must be an existing file (not a directory). Queued as `SourceKind.LOCAL_FILE`. |

Parsing (`_parse_g3m_url`): strip the scheme prefix, cut at the first comma (later text is reserved for extra link data, so commas in URLs must be `%2C`), trim, strip trailing `/`, repair `https//` and `http//` (browsers drop the colon). Anything that is not HTTP(S) or a valid local file shows "mod not found". While a game is running, links are refused with a warning (pairing still works). There are no query parameters and no action keyword other than `gb_pair`.

### Producer side (MCE)

`buildG3MLink(path)` in `G3M-MCE/src/utils/g3mProtocol.js`: trims, removes surrounding quotes, converts `\` to `/`; requires an absolute path (`C:/x` or `/x`), a `.zip` suffix, no control or format characters, no `.` or `..` segments; percent-encodes each segment except a drive letter; returns `g3m://file:///C:/...` or `g3m://file:///home/...`. Tests: `G3M-MCE/src/utils/g3mProtocol.test.js`.

### Notes for g3mstudio://

To export to G3M, G3MStudio can emit the same `g3m://file:///<abs path>.zip` form as MCE, or launch G3M with that argument. A `g3mstudio://` scheme defined the same way keeps behaviour predictable: HKCU class key and `.desktop` handler with `x-scheme-handler/g3mstudio`, raw URL forwarded to a running instance, confirmation before any download, `file://` restricted to local existing files, comma reserved, `https//` repair. G3M does not know `g3mstudio://`, so links meant for G3M must keep using `g3m://`.

## Compatibility constraints

G3M drives the bundled G3MTool CLI as a subprocess (`G3M/src/adapters/g3mtool_adapter.py` `G3MToolManager`). It does not load G3MLib directly. A G3MTool 1.3.1 / G3MLib 1.1.0 update must keep the following.

### Executable location and layout

- Path `G3M/src/assets/bin/g3mtool_{windows|linux|macos}/G3MTool.exe` (Windows) or `G3MTool` (others). Overridable by setting `custom_g3mtool_path`. On non-Windows G3M runs `chmod 0700`.
- The bundled folder is a framework-dependent layout (`G3MTool.dll`, `G3MLib.dll`, `G3MTool.runtimeconfig.json`, satellite folders) with `GameSpecificData/` next to the executable, because `GameSpecificResolver.BaseDirectory` is the executable directory. A single-file release must still find `GameSpecificData`.
- Bundled xdelta must work, since `--xdelta-path` is only passed when `custom_xdelta_path` is set.

### Command lines G3M builds

| G3M method | Arguments |
|---|---|
| `apply_patch` | `patch apply <data> <patch> <output> [--log <path>] --cache <dir>` |
| `merge_patches` | `patch merge <original> <p1> <p2> ... --apply <output> [--out <patch>] [--code] [--properties] [--report <path>] [--log <path>] --cache <dir>` |
| `patch_create` | `patch create <original> <modified> <output> [--xdelta-fallback] --cache <dir>` |
| `batch_*` | `patch batch apply|create <original> <paths...> --out-dir <dir> [--continue-on-error] [--xdelta-fallback] --cache <dir>`; `patch batch merge <original> <set1> <set2>... --apply <dir> [--out <dir>] [--continue-on-error] [--code] [--properties] [--report] --cache <dir>` |
| `xpatch_apply`, `xpatch_create` | `xpatch apply|create <a> <b> <output>` (no `--cache`) |
| `info` | `info <target> [--verbose] --cache <dir>` |
| `diff` | `diff <file1> <file2> [<output-dir>] [--full] --cache <dir>` |
| `execute` | `execute <target> [--data <f>] [--output <f>] [--input <dir>] [-- <args>...]` |

Keep stable:

- `--cache <dir>` is appended to `patch apply`, `patch create`, `patch merge`, `patch validate`, all three `patch batch` commands, `info` and `diff`. `patch apply --cache` is undocumented in the README but required. Cache dir is `<G3M data>/cache/G3MTool`; `.g3mcache` files there must stay readable or be ignored safely after an update.
- `--xdelta-path <path>` is inserted before `--` (or at the end), so it must stay a recursive global option accepted after positional arguments on every command.
- Positional order and names: `original` first, merge inputs low to high priority (G3M reverses its UI list), optional `output` third positional.
- Batch merge sets are comma-joined; G3M refuses paths containing commas, so the comma separator must not change.
- `--sequential` is not used by G3M; `--json` is not used by G3M.
- `.csx` must keep working as the `<patch>` of `patch apply` and as an input of `patch merge`, with `Data` preloaded from `<data>` and the result written to `<output>`. `.vcdiff`/`.xdelta` go through `xpatch apply`, so `patch apply` handling of xdelta is only used by other callers.
- Data-file inputs (`.win` and other data files) must stay valid as a `patch apply` or `patch merge` input.

### Output G3M parses

- Exit code: 0 means success; any nonzero code is a failure and G3M shows `stderr`, else `stdout`, else the code. Merge conflicts must not turn into a nonzero exit, or launches with overlapping mods fail.
- Progress: stdout split on `\r` and `\n`, each line matched with `^(?P<label>.+?):\s*(?P<percent>\d+)%`. The `\r{operation}: {N}%` format of `LogService.Progress` must stay, flushed per update, on stdout. Other stdout lines must not look like `label: 50%` by accident.
- `diff` must write exactly one `.md` report into the given output directory; G3M opens the first `*.md` file found there and parses `#`, `##`, `###` headings into a tree (`G3M/src/ui/dialogs/g3mtool_diff_viewer.py`).
- `info` text output is shown verbatim in the Info tab; changes are cosmetic but visible.
- Merge `--report <path>` must write Markdown to exactly that path (G3M passes `{base}_merge_report.md`).
- `patch apply` and `xpatch apply` must write the output file at the given path; patch verification checks `os.path.isfile(output)`.
- The process must exit when stdin is `DEVNULL` and must not open a console window on Windows (G3M uses `CREATE_NO_WINDOW`). Child processes are killed as a tree on cancel, so outputs should be written atomically.

### Files G3M reads without G3MTool

- `g3mpatch.json` at the ZIP root of a `.g3mpatch` (or in an unpacked folder), UTF-8 with or without BOM, at most 8 MiB. Keys read: `original.md5`, `original.size`, `modified.md5` (manual-install matching) and the whole manifest (support packages). Renaming the manifest, moving it, or changing these keys or MD5 hex format breaks auto-detection.
- `.g3mpatch` extension and `Xdelta/` fallback folder name are part of the user-visible format.

### Byte-exact behaviour

- PizzaOven conversion keeps a generated `.g3mpatch` only if `patch apply` reproduces the modified `data.win` byte for byte; otherwise it falls back to xdelta, then a full `data.win`. If G3MLib 1.1.0 changes serialization so round trips are no longer identical, conversions silently switch to larger fallbacks (`G3M/src/utils/patching/patch_verification_utils.py`).
- Existing `.g3mpatch` files created by G3MTool 1.3.0 inside published mods must still apply and merge.

### Defaults and file names

- Default output names (`patch_{timestamp}.g3mpatch`, `{stem}_patched{ext}`, `diff/`, batch `{original}_{input}{ext}`, `{input}.g3mpatch`, `merge_{NNN}_...` with `_N` suffixes) are not relied on by G3M, which always passes explicit outputs, but users and scripts may rely on them.
- `--log default` path `logs/{command}_{timestamp}.log` next to the executable.

### JSON contract for other callers

Not used by G3M, but documented in `G3MWiki/G3MTool/automation.md` for scripts: `patch create` (`success`, `output`, `statistics`, `warnings`), `patch apply` (`success`, `output`, `details`, `warnings`), `patch validate` (`success`, compatibility data, `warnings`), `patch merge` (`success`, `output`, `applied`, `conflicts`, `autoMerged`, `warnings`), `patch batch` (`success`, `total`, `completed`, `failed`, `deduplicated`, `items`), `diff` (`success`, `output`, `differences`, `changed`, `new`, `deleted`), `info` (data metadata with `File`, `Resources`, or the raw manifest, no wrapper). JSON goes to stdout as one object; errors may be plain text on stderr.

### Known mismatches to settle

- Deltamod game map: MCE maps `other.frickbears3`, G3M's `DELTAMOD_GAME_MAP` does not (`G3M/src/adapters/deltamod_adapter.py` L38).
- Default `patch merge` output location: README says next to the executable, wiki says beside the original.
- Authors entry: MCE uses one per line, the G3M desktop editor uses commas.
