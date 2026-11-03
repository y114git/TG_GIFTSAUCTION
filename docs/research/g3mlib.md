# G3MLib and G3MTool GUI: what G3MStudio can build on

Research date: 2026-10-25.

Scope: G3MLib 1.0.1 (`G3MLib/G3MLib/G3MLib.csproj`, .NET 10, C# 14) and G3MTool GUI 1.0.0 (`G3MTool/G3MToolGUI/G3MToolGUI.csproj`, Avalonia 12.1.4). All paths are relative to the folder that holds the G3M repositories. Nothing in either repository was changed. Test runs built `bin/` and `obj/` output only.

Lineage note. Most of `G3MLib/DataFile` follows the UndertaleModLib layout with the `Undertale` prefix renamed to `GameMaker`. Most of `G3MLib/Analyzer` follows Underanalyzer (MPL-2.0 headers in files such as `G3MLib/G3MLib/Analyzer/Compiler/Errors/ParserError.cs`). The license is GPL-3.0-only (`G3MLib/README.md`). G3MStudio inherits that license if it links G3MLib.

Dependencies of the library: Magick.NET-Q8-AnyCPU 14.17.2, Microsoft.CodeAnalysis.CSharp.Scripting 5.9.0, PropertyChanged.Fody 4.1.0, SharpZipLib 1.4.2 (`G3MLib/G3MLib/G3MLib.csproj`).

## 1. Data model

### Chunks and resource lists

`GameMakerChunkFORM.ChunkConstructors` in `G3MLib/G3MLib/DataFile/GameMakerChunks.cs` lists every chunk the reader accepts. `GameMakerData` in `G3MLib/G3MLib/DataFile/GameMakerData.cs` exposes each one as a typed property.

| Chunk | `GameMakerData` property | Model file (`G3MLib/G3MLib/DataFile/Models/`) | Notes |
|---|---|---|---|
| GEN8 | `GeneralInfo` | `GameMakerGeneralInfo.cs` | Bytecode version, runtime version, branch, game ID |
| OPTN | `Options` | `GameMakerGeneralInfo.cs` (`GameMakerOptions`) | Constants list |
| LANG | `Language` | same | |
| EXTN | `Extensions` | `GameMakerExtension.cs` | Files, functions, args, options |
| SOND | `Sounds` | `GameMakerSound.cs` | |
| AGRP | `AudioGroups` | `GameMakerAudioGroup.cs` | |
| SPRT | `Sprites` | `GameMakerSprite.cs` | Normal, Spine, SWF, vector types (see `G3MLib/G3MLib.Tests/SpriteSerializationTests.cs`) |
| BGND | `Backgrounds` | `GameMakerBackground.cs` | Also tilesets |
| PATH | `Paths` | `GameMakerPath.cs` | |
| SCPT | `Scripts` | `GameMakerScript.cs` | |
| GLOB, GMEN | `GlobalInitScripts`, `GameEndScripts` | `GameMakerGlobalInit.cs` | |
| SHDR | `Shaders` | `GameMakerShader.cs` | GLSL ES, GLSL, HLSL and console raw blobs (`GameMakerRawShaderData`) |
| FONT | `Fonts` | `GameMakerFont.cs` | Glyphs and kerning |
| TMLN | `Timelines` | `GameMakerTimeline.cs` | |
| OBJT | `GameObjects` | `GameMakerGameObject.cs` | Events, physics |
| ROOM | `Rooms` | `GameMakerRoom.cs` | See room layers below |
| UILR | none; use `data.FORM.UILR.List` | `GameMakerUINode.cs` | UI layers, flex panels, UI instances |
| DAFL | none | `GameMakerEmptyChunk` | Always empty |
| EMBI | `EmbeddedImages` | `GameMakerEmbeddedImage.cs` | |
| TPAG | `TexturePageItems` | `GameMakerTexturePageItem.cs` | |
| TGIN | `TextureGroupInfo` | `GameMakerTextureGroupInfo.cs` | Includes external texture groups |
| CODE | `Code` | `GameMakerCode.cs` | Instructions; child entries via `ParentEntry` |
| VARI | `Variables` | `GameMakerVariable.cs` | |
| FUNC | `Functions`, `CodeLocals` | `GameMakerFunction.cs` | |
| STRG | `Strings` | `GameMakerString.cs` | Shared string objects |
| TXTR | `EmbeddedTextures` | `GameMakerEmbeddedTexture.cs` | `TexData` wraps `GMImage` |
| AUDO | `EmbeddedAudio` | `GameMakerEmbeddedAudio.cs` | Raw bytes |
| ACRV | `AnimationCurves` | `GameMakerAnimationCurve.cs` | GMS 2.3+ |
| SEQN | `Sequences` | `GameMakerSequence.cs` | Tracks, keyframes, moments, broadcast messages |
| TAGS | `Tags` | `GameMakerTags.cs` | GMS 2.3+ |
| FEAT | `FeatureFlags` | `GameMakerFeatureFlags.cs` | GMS 2.3+ |
| FEDS | `FilterEffects` | `GameMakerFilterEffect.cs` | GMS 2.3.6+ |
| PSEM, PSYS | `ParticleSystemEmitters`, `ParticleSystems` | `GameMakerParticleSystem.cs` | GM 2023.2+ |

An unknown chunk name throws `IOException("Unknown chunk ...")` (`G3MLib/G3MLib/DataFile/GameMakerChunkTypes.cs` lines 80 and 149). A raw passthrough base class `GameMakerUnsupportedChunk` exists in the same file but no registered chunk uses it.

Room layers. `GameMakerRoom.LayerType` has `Path`, `Background`, `Instances`, `Assets`, `Tiles`, `Effect` and `Path2` (`GameMakerRoom.cs` line 1433). Layer data classes are `LayerInstancesData`, `LayerTilesData`, `LayerBackgroundData`, `LayerAssetsData` and `LayerEffectData`. Asset layers hold `Tile`, `SpriteInstance`, `SequenceInstance`, `ParticleSystemInstance` and `TextItemInstance`. Rooms also keep legacy `Background` and `View` lists and `GameObject` instances.

Debug data. `GameMakerIO`'s reader has `ReadGameMakerDebugData()` for `.yydebug` files (`G3MLib/G3MLib/DataFile/GameMakerIO.cs` line 358, models in `G3MLib/G3MLib/DataFile/ModelsDebug/`).

Lookup helpers. `GameMakerData.ByName`, `IndexOf`, `IndexOfByName`, a string indexer `data["Sprites"]` and a `Type` indexer (`GameMakerData.cs` lines 32 to 460). List helpers `ByName`, `MakeString`, `Define`, `EnsureDefined`, `DefineLocal` are in `G3MLib/G3MLib/DataFile/GameMakerDataExtensionMethods.cs`.

New files. `GameMakerData.CreateNew()` builds a minimal file. `CreateNew2026LTS()` adds the 2.3+ chunks and sets 2026.1 LTS (`GameMakerData.cs` lines 731 to 823).

### Versions and detection

- Bytecode 13 to 17 is supported. Outside that range `UnsupportedBytecodeVersion` is set and CODE, VARI and FUNC are kept without parsing instructions (`G3MLib/G3MLib/DataFile/Models/GameMakerGeneralInfo.cs` line 600, `GameMakerChunks.cs` line 1781).
- GMS2 runtime versions are not stored in GEN8 after 2.x. The reader infers them from chunk layout and calls `SetGMS2Version` at about 60 sites (`GameMakerChunks.cs`, `GameMakerLists.cs`, and the `Models/` files). Detected points range from 2.0.6 to 2026.1 (2026.1 at `GameMakerChunks.cs` line 1362).
- LTS handling: `BranchType` is `Pre2022_0`, `LTS2022_0` or `Post2022_0` (`GameMakerGeneralInfo.cs` line 243). `IsVersionAtLeast`, `IsNonLTSVersionAtLeast`, `IsLTSBranch`, `IsGameMaker2` and `IsYYC` are on `GameMakerData`.
- 2023.11 function naming is detected from `gml_Script_` names that contain `@` (`GameMakerIO.cs` line 336).

### Platforms

`DataFileExtensionUtil.ValidExtensions` is `.win`, `.ios`, `.droid`, `.unx` (`G3MLib/G3MLib/Modding/Internal/DataFileExtensionUtil.cs`). The binary format is the same for all four. External audio groups are separate `audiogroupN.dat` FORM files next to the data file (`G3MLib/G3MLib/Modding/Patching/PatchInputService.cs`, `ExternalAudioGroupPaths`). Test fixtures cover runtime eras 2022, 2023, 2024 and 2026 (`G3MLib/G3MLib.Tests/Fixtures/data-*.win`, `debug-*.yydebug`).

## 2. Read and write

| Capability | API | File |
|---|---|---|
| Full read from any `Stream` | `GameMakerIO.Read(stream, warningHandler, messageHandler, onlyGeneralInfo)` | `G3MLib/G3MLib/DataFile/GameMakerIO.cs` line 1054 |
| Partial read | `onlyGeneralInfo: true` reads GEN8 and stops | same, `GameMakerChunkFORM.UnserializeChunk` |
| Write to any `Stream` (file or `MemoryStream`) | `GameMakerIO.Write(stream, data, messageHandler)` | line 1065 |
| Offset map for hex inspection | `GameMakerIO.GenerateOffsetMap(stream)` | line 1076 |
| Staged, validated file write | `PatchInputService.WriteDataFile(data, outputPath, sourcePath)` | `G3MLib/G3MLib/Modding/Patching/PatchInputService.cs` line 22 |
| Cheap info snapshot and cache | `G3MCacheService.BuildInfoSnapshot`, `TryReadDataInfoSnapshot` | `G3MLib/G3MLib/Modding/Caching/G3MCacheService.cs` |
| MD5 | `FileHash.ComputeMd5Async(path, token)` | `G3MLib/G3MLib/Modding/Hashing/FileHash.cs` |

There is no lazy or per-chunk read: every read parses the whole FORM in two passes (object count, then data). There is no async read or write and no `CancellationToken` on either. The benchmark in `G3MLib/README.md` shows 642 ms to read and 418 ms to write a 92 MB file, so an editor must run both off the UI thread.

Events. The reader and writer only take string callbacks (`WarningHandlerDelegate(string warning, bool isImportant)` and `MessageHandlerDelegate(string)`, `GameMakerIO.cs` lines 175 and 181). They report phases such as "Resolving resource IDs..." with no numeric progress. Numeric progress exists only in the modding layer through `LogService` (section 6).

Errors.
- `SubmitWarning` appends the file offset. With no handler an important warning throws `IOException`; a minor one is dropped (`GameMakerIO.cs` line 228).
- FORM and chunk bounds are checked and throw `IOException` (`GameMakerChunks.cs` lines 125 to 135, `GameMakerIO.cs` line 268).
- After a read, `ThrowIfUnreadObjects` throws if any pointer target was never parsed (`GameMakerIO.cs` line 738). After a write, `ThrowIfUnwrittenObjects` does the same.
- A failure in the game-specific resolver becomes a non-important warning and an empty registry (`GameMakerIO.cs` line 322).

Round-trip fidelity. `ReadWriteReadDataFixture` asserts the written bytes equal the original bytes for each fixture (`G3MLib/G3MLib.Tests/GameMakerIoTests.cs` line 160).

Transactional output. `WriteDataFile` writes to a `.g3mlib-output-<guid>` staging folder, flushes to disk, re-reads the staged file with `GameMakerIO.Read` to validate it, copies external audio groups, then commits with `File.Replace` and per-file backups (`G3MLib/G3MLib/Modding/Internal/OutputFileCommit.cs`). On a commit failure it rolls back. If rollback also fails, the backups stay in a `.g3mlib-backup-<guid>` folder and an `AggregateException` names it.

Validation and repair. `DataIntegrityService.RepairAndValidate(data)` and `ValidateOnly(data)` return `Errors` and `Repairs` lists (`G3MLib/G3MLib/Modding/Integrity/DataIntegrityService.cs`). Repairs: script and code links, code parent links, code serialization order, room code references, GEN8 branch, local child function references, function references, sound to audio-group references. Validation: scripts, code and functions; objects; textures; sounds; rooms, instances and tiles; particles. Patch apply runs it on the output (`G3MLib/G3MLib/Modding/Patching/PatchService.cs` lines 215 and 2647).

## 3. Project serialization

G3MLib has two separate on-disk formats.

### 3a. `ProjectContext` mod projects

A project is a folder with a main JSON file (`ProjectMainOptions`, `G3MLib/G3MLib/DataFile/Project/ProjectMainOptions.cs`). Its fields: `Name`, `Flags`, `VerifyStrings`, `AssetsDataFilePath`, `ErrorOnWarnings` (default true), `ExcludeDirectories`, `SubProjects`, `Patches` (BPS file patches, applied in `ProjectContextImportSteps.cs` line 238), `ExternalFiles`, `FileCopies`, `FileDeletes`, `PreImportScripts`, `PreAssetImportScripts`, `PostImportScripts`. An optional `general-info-options.json` sits beside it (`ProjectContextAssets.cs`).

Asset files. `Export` writes `<plural type>/<name>.json`, or `<plural type>/<name>/<name>.json` for types that need their own folder (`ProjectContext.cs` lines 517 to 566). Import scans every non-hidden subfolder for `*.json` and for loose `*.gml` files (`ProjectContextAssets.cs`).

| `SerializableAssetType` | Folder | Own folder | Side files | Serializer (`G3MLib/G3MLib/DataFile/Project/SerializableAssets/`) |
|---|---|---|---|---|
| GameObject | `objects` | no | none | `SerializableGameObject.cs` |
| Path | `paths` | no | none | `SerializablePath.cs` |
| Code | `code` | no | `.gml` | `SerializableCode.cs` |
| Script | `scripts` | no | none | `SerializableScript.cs` |
| Sound | `sounds` | yes | `.wav` or `.ogg` (import also finds `.mp3`) | `SerializableSound.cs` |
| Room | `rooms` | no | none | `SerializableRoom.cs` |
| Background | `backgrounds` | yes | `.png` | `SerializableBackground.cs` |
| Sprite | `sprites` | yes | `<name>_<i>.png`, `<name>_<i>.mask.png` | `SerializableSprite.cs` |
| Sequence | `sequences` | no | none | `SerializableSequence.cs` |
| AnimationCurve | `animcurves` | no | none | `SerializableAnimationCurve.cs` |
| Font | `fonts` | yes | `.png` | `SerializableFont.cs` |
| Shader | `shaders` | yes | `.txt` per source | `SerializableShader.cs` |
| ParticleSystem | `particlesystems` | no | none | `SerializableParticleSystem.cs` |

Types the project format does not cover: extensions, timelines, texture groups, embedded images, tags, feature flags, filter effects, UI layers, emitters as standalone assets, audio groups, global init scripts, strings, variables, functions.

Behavior that matters for an editor:
- Export is selective. Only assets passed to `MarkAssetForExport` are written (`ProjectContext.cs` line 370). There is no "export everything" call; the host must mark each asset.
- Code without known source is decompiled at export. A decompiler failure writes a comment block with the exception instead of failing (`ProjectContext.cs` lines 474 to 497).
- A file whose name equals the asset name omits `DataName`, so a file can be renamed or copied (`ProjectContext.cs` line 560).
- Name collisions get `_2` to `_11` suffixes, then throw (`ProjectContext.cs` line 541).
- `Import` either uses supplied data or loads, imports and saves the data file itself, with `GameFileBackup` restore on failure (`ProjectContext.cs` line 270, `GameFileBackup.cs`).
- `RecompileAllCodeSources` sorts code by name and compiles through `CodeImportGroup` (`ProjectContext.cs` line 596).
- Round trips are tested for rooms, sprites and all exportable assets of each fixture (`G3MLib/G3MLib.Tests/ProjectContextTests.cs` lines 97 to 166).

Git-friendliness. JSON uses `WriteIndented = true` and relaxed escaping (`ProjectContext.cs` line 121). One asset per file. Room asset instances keep a fixed JSON property order (`G3MLib/G3MLib.Tests/RoomJsonTests.cs`). The format does not record resource order in the data file, so asset indexes of new assets depend on import order. Directory enumeration order is file-system order, not sorted (`ProjectContextAssets.cs`).

### 3b. Modding resource export (patch payload format)

`ResourceExportService` (`G3MLib/G3MLib/Modding/Resources/ResourceExportService.cs`) and `ResourceImportService` (`ResourceImportService.cs`) cover all 30 types in `ResourceTypeRegistry.AllTypes` (`G3MLib/G3MLib/Modding/Internal/ResourceTypeRegistry.cs`): GeneralInfo, Options, GlobalScripts, Scripts, Language, FeatureFlags, Tags, FilterEffects, AudioGroups, EmbeddedAudio, TextureGroupInfo, EmbeddedTextures, TexturePageItems, EmbeddedImages, Sprites, Backgrounds, Fonts, Sounds, Paths, Tilesets, Shaders, Timelines, GameObjects, AnimationCurves, ParticleSystemEmitters, ParticleSystems, Sequences, Rooms, CodeEntries, Extensions. Import follows `ResourceTypeRegistry.ImportOrder`.

Outputs are indented JSON per resource, PNG frames, `.gml` plus `.asm` per code entry, original sound bytes, and order files: `asset_order.txt`, `object_events.json`, `sprite_frame_map.json`, `texture_page_items.json` (`ResourceExportService.cs`, `ExportAssetOrder` at line 2787). `ExportCodeEntriesToMemory` returns GML and assembly strings without disk I/O (line 2615). This format records resource order, so it is the stronger base for a git-friendly full-game project. It is undocumented outside the code.

## 4. GML

| Piece | API | File |
|---|---|---|
| Batch import and compile | `CodeImportGroup` with `QueueReplace`, `QueueAppend`, `QueuePrepend`, `QueueFindReplace`, `QueueTrimmedLinesFindReplace`, `QueueRegexFindReplace`, then `Import(throwOnFailedCompile)` returning `CompileResult` | `G3MLib/G3MLib/DataFile/Compiler/CodeImportGroup.cs` |
| Lower-level batch compile | `CompileGroup.QueueCodeReplace`, `Compile()` | `G3MLib/G3MLib/DataFile/Compiler/CompileGroup.cs` |
| Single-source compile | `CompileContext(code, scriptKind, globalScriptName, gameContext)` with `Parse()`, `Compile()`, `Link()` | `G3MLib/G3MLib/Analyzer/Compiler/CompileContext.cs` |
| Decompile | `new DecompileContext(globalContext, code, settings, token).DecompileToString()` or `DecompileToAST()` | `G3MLib/G3MLib/Analyzer/Decompiler/DecompileContext.cs` |
| Shared decompile state | `GlobalDecompileContext(data)`; asset name and id lookups `GetAssetName`, `GetAssetId`, `GetScriptId`, `GetRoomInstanceId` | `G3MLib/G3MLib/DataFile/Decompiler/GlobalDecompileContext.cs` |
| Output style | `DecompileSettings`: indent string, semicolons, CSS colors, brace placement, blank-line rules, cleanup passes, enum creation | `G3MLib/G3MLib/Analyzer/Decompiler/DecompileSettings.cs` |
| Disassembly | `code.Disassemble(vars, locals)`, `FindBlockAddresses` | `G3MLib/G3MLib/DataFile/Decompiler/Disassembler.cs` |
| Assembly | `Assembler.Assemble(source, data)`, `AssembleOne`, bulk caches `SetLookupCaches`/`ClearLookupCaches` | `G3MLib/G3MLib/DataFile/Decompiler/Assembler.cs` |
| Builtin symbols | `BuiltinList.Functions` (name, arg count, classification) and `Constants` | `G3MLib/G3MLib/DataFile/Compiler/BuiltinList.cs` |

Error reporting. `CompileResult.Errors` yields `CompileError` with `Code`, `BaseMessage` and `GenerateDetailedMessage()` (`G3MLib/G3MLib/DataFile/Compiler/CompileResult.cs`). Line and column exist only inside the message text, as "around line N, column M" (`ParserError.cs` line 35). `LexerError` stores a private text position. No error exposes line, column, span or severity as fields. Decompiler warnings are a `List<IDecompileWarning>` on `DecompileContext` (`DecompileDataLeftoverWarning.cs`).

Game-specific data. Definitions for `undertale`, `deltarune` and generic `gamemaker`, plus decompiler macro configs, live in `G3MLib/G3MLib/DataFile/GameSpecificData/{Definitions,Analyzer}/`. They are embedded in the DLL and also copied to output (`G3MLib.csproj`). `GameSpecificResolver` matches them by conditions and loads them at read time (`G3MLib/G3MLib/DataFile/Decompiler/GameSpecificResolver.cs`). Macro types are in `G3MLib/G3MLib/Analyzer/Decompiler/GameSpecific/MacroTypes/`.

Editor-relevant visibility.
- Compiler lexer, tokens, parser and parse tree are `internal` (`Lexer/Token.cs` `IToken`, `Parser/ParseContext.cs`, `Nodes/IASTNode.cs`). `InternalsVisibleTo` covers only `G3MLib.Tests`. An editor cannot reuse the lexer for highlighting without a library change.
- Decompiler AST is public (`IStatementNode`, `AST/Nodes/`), but `ASTPrinter` builds a plain string with no map from text ranges to nodes or instructions.
- `FunctionScope` is public. `CompileContext` exposes `Macros`, `Enums`, `OutputGlobalFunctionNames` and `OutputFunctionEntries`.
- No find-references, rename or go-to-definition support exists.

## 5. Scripting

- `IScriptInterface` (`G3MLib/G3MLib/DataFile/Scripting/IScriptInterface.cs`) is the UTMT-shaped host contract: `Data`, `FilePath`, `ScriptPath`, `Highlighted`, `Selected`, `CanSave`, `ExePath`, `MainThreadAction`, `ScriptMessage`, `ScriptWarning`, `ScriptQuestion`, `ScriptError`, `ScriptOpenURL`, `RunUMTScript`, `LintUMTScript`, `GetDecompiledText`, `GetDisassemblyText`, `AreFilesIdentical`, `ScriptInputDialog`, `SimpleTextInput`, `SimpleTextOutput`, `ClickableSearchOutput`, the progress-bar family, `EnableUI`, `ChangeSelection`, `PromptChooseDirectory`, `PromptLoadFile`, `PromptSaveFile`.
- The only library implementation is `ProjectContext` (`ProjectContextScriptInterface.cs`). It sends messages to `ScriptMessageHandler`/`ScriptWarningHandler`, returns `false` from `ScriptQuestion`, and throws for prompts and `ScriptOpenURL`.
- Script options: imports `G3MLib.DataFile`, `.Models`, `.Decompiler`, `.Scripting`, `.Compiler`, `System`, `System.IO`, `System.Collections.Generic`, `System.Text.RegularExpressions`; references G3MLib, Magick.NET and the analyzer; debug info on for line numbers (`G3MLib/G3MLib/DataFile/Util/ScriptingUtil.cs` line 23). `PrettifyException` formats script stack traces.
- `UTMTScriptTranslator.Translate` rewrites identifiers with Roslyn tokens: `UndertaleModLib` to `G3MLib.DataFile`, `Underanalyzer` to `G3MLib.Analyzer`, `Undertale*` to `GameMaker*`, `undertaleData` to `gameMakerData` (`G3MLib/G3MLib/DataFile/Legacy/UTMTScriptTranslator.cs`). Only the G3MTool hosts call it. `ProjectContext` script runs do not (`ProjectContextScripts.cs`).
- `ScriptGlobals` is not in G3MLib. G3MTool has two copies: `G3MTool/G3MToolCLI/Models/Scripting/ScriptGlobals.cs` and `G3MTool/G3MToolGUI/Services/ScriptExecutionService.cs` line 121. Neither implements `IScriptInterface`, and several signatures differ from it. Examples: `ScriptInputDialog(title, label, defaultInput, allowMultiline, showDialog)` against the 7-parameter interface method; `PromptLoadFile(filter, defaultPath)` against `(defaultExt, filter)`; `SetProgressBar(..., int, int)` against `double`.

UTMT features missing or stubbed, from the code here:
- `SyncBinding`, `DisableAllSyncBindings`, `SetFinishedMessage`, `ChangeSelection`, `StartProgressBarUpdater` are no-ops in the GUI host (`ScriptExecutionService.cs` lines 171 to 179).
- `ClickableSearchOutput` and `SimpleTextOutput` print text only in `ProjectContext`.
- `Highlighted` and `Selected` have no source of truth, since no host has a resource tree.
- `GetDecompiledText` is missing from both G3MTool `ScriptGlobals` copies (only `ProjectContext` has it).
- Not checked against UTMT source in this pass: profile-mode members and other members UTMT declares that `IScriptInterface.cs` lacks. Diff against the UTMT revision named in `G3MLib/README.md` before claiming script compatibility.

## 6. Modding

| Feature | API | File |
|---|---|---|
| Create patch (`.g3mpatch` zip with `g3mpatch.json` manifest and changed resources, optional xdelta fallback) | `PatchService.CreatePatchAsync` | `G3MLib/G3MLib/Modding/Patching/PatchService.cs` line 830 |
| Apply patch, with integrity repair, output validation and rollback | `PatchService.ApplyPatchAsync(dataPath, patchPath, outputPath, allowXdeltaFallback, verifyModifiedHash)` | line 1950 |
| Validate patch | `PatchService.ValidatePatchAsync` | line 6953 |
| Normalize any input (data file, xdelta) to a G3M patch | `PatchService.EnsureG3MPatchAsync`, `PatchInputService.MaterializeDataAsync` | line 7015, `PatchInputService.cs` line 80 |
| Manifest | `G3MPatchManifest`: tool, library, original and modified `DataFileInfo`, per-type `ResourceTypeChanges`, `PatchStatistics`, `PatchApplyPlan` | `G3MPatchManifest.cs` |
| Merge 2 or more patches | `MergeService.MergePatchesAsync` with `MergeOptions` (`UseCodeMerge`: LCS three-way GML merge; `UsePropertyMerge`: three-way JSON merge; `UseSequentialMerge`: low memory; `ReportPath`) | `G3MLib/G3MLib/Modding/Merging/MergeService.cs` lines 17, 964, 3408 |
| Diff report (Markdown, `Standard` or `Full`) | `DiffService.CompareAsync` | `G3MLib/G3MLib/Modding/Diffing/DiffService.cs` |
| Per-resource SHA-256 hashes | `ResourceHashService.HashAll(data)` returns type to name to hash | `G3MLib/G3MLib/Modding/Resources/ResourceHashService.cs` |
| Analysis cache (`.g3mcache`) | `G3MCacheService` read/write of hashes, names, order and info snapshot | `G3MLib/G3MLib/Modding/Caching/G3MCacheService.cs` |
| xdelta | `XDeltaService` (create, apply, raw args, timeout); process default `DefaultExecutablePath`, scoped override `UseExecutable(path)` | `G3MLib/G3MLib/Modding/XDelta/XDeltaService.cs` |
| Log and progress | `LogService.BeginScope(onMessage, onProgress, verbose, suppress)`; `ModdingLogMessage` (level, text), `ModdingProgress` (operation, current, total, complete) | `G3MLib/G3MLib/Modding/Logging/LogService.cs` |

Images. Input formats: PNG, JPEG, BMP, GIF, WebP, TIFF, DDS, QOI through Magick.NET (`G3MLib/G3MLib/DataFile/Util/TextureWorker.cs` line 15). Texture page formats: raw BGRA, PNG, GameMaker QOI, BZip2 QOI, DDS (`GMImage.ImageFormat`, `G3MLib/G3MLib/DataFile/Util/GMImage.cs`). `TextureWorker.GetTextureFor(item)` crops a texture page item to an image, and `ExportCollisionMaskPNG` renders masks.

Audio. Sounds export as their stored bytes, using the sound's `Type` extension, default `.ogg` (`ResourceExportService.cs` line 1483). Import accepts `.ogg` or `.wav` files as-is (`ResourceImportService.cs` line 3950). There is no decoding, transcoding, resampling or playback code anywhere in G3MLib.

## 7. Gaps for an interactive editor

| Topic | Current state | Evidence |
|---|---|---|
| Undo/redo, change tracking | None. The only dirty tracking is the manual `MarkAssetForExport` set with the `UnexportedAssetsChanged` event. | `ProjectContext.cs` lines 74 to 420 |
| Property change notification | PropertyChanged.Fody weaves `INotifyPropertyChanged` into the classes that declare it (about 29 model files). Lists derive from `GameMakerObservableList<T> : ObservableCollection<T>`. Bindings work; change history does not. | `G3MLib/G3MLib/DataFile/FodyWeavers.xml`, `GameMakerLists.cs` line 13 |
| Object identity and references | Resources are plain object references. Cross-references use `GameMakerResourceById<T, ChunkT>`, which holds the object and resolves its index at write time. Names are shared `GameMakerString` objects. There is no reverse index and no "find usages". | `GameMakerIO.cs` line 21, `GameMakerIO.cs` line 836 |
| Rename | Changing a name string renames every user of that string object. Code that refers to assets by index or by function name is not updated, and no rename service exists. | `GameMakerString.cs`, `GameMakerDataExtensionMethods.cs` |
| Incremental or transactional save | Every save writes the whole file. Atomic staged commit exists only through `PatchInputService.WriteDataFile`. | `GameMakerIO.cs` line 1065, `OutputFileCommit.cs` |
| Texture page repacking | `TextureGroupPacker` packs new images into new pages and appends them; existing pages are never repacked. `ResourceImportService` has its own atlas packer. | `G3MLib/G3MLib/DataFile/Util/TextureGroupPacker.cs` line 226, `ResourceImportService.cs` line 168 |
| Audio decode and playback | None. | search of `G3MLib/G3MLib` for Vorbis, NAudio, OpenAL returns nothing |
| Font glyph rendering | Glyph metrics and the texture item exist. No TTF rasterizing, no text layout. | `GameMakerFont.cs` |
| Room rendering | Room, layer, tile and instance data only. No compositor, no draw order helper, no view math. | `GameMakerRoom.cs` |
| Thread-safety | `GameMakerData` has no locking. Hosts marshal model edits through `MainThreadAction` on `ProjectContext`, `CodeImportGroup`, `CompileGroup` and `Assembler`. `LogService` and `ResourceImportService` state is `AsyncLocal`. Assembler caches are `[ThreadStatic]`. `GameSpecificResolver` uses a static lock. `XDeltaService.DefaultExecutablePath` and `ModdingMetadata.ToolName`/`ToolVersion` are process-wide statics. Exporters use `Parallel.ForEach`. | `LogService.cs` line 25, `ResourceImportService.cs` line 398, `Assembler.cs` line 53, `GameSpecificResolver.cs` line 74, `ModdingMetadata.cs` |
| Cancellation | Only `DecompileContext` takes a `CancellationToken`. Read, write, patch, merge and export do not. | `DecompileContext.cs` line 37 |

## 8. G3MTool GUI

Architecture. Code-behind, no MVVM or view models.
- `G3MTool/G3MToolGUI/Program.cs`: classic desktop lifetime, `UsePlatformDetect`, `LogToTrace`.
- `App.axaml`: `FluentTheme` plus `Styles.axaml`. `App.axaml.cs` sets `XDeltaService.DefaultExecutablePath` from the embedded binary at startup.
- `MainWindow.axaml(.cs)`: one window, 850x680 (minimum 760x560). An operation `ComboBox` drives which path rows and option checkboxes show (`OperationView` records, `ConfigureOperation`). Work runs with `Task.Run(() => _operations.RunAsync(request))`. Closing is deferred while an operation runs.
- `Services/ModdingOperationService.cs`: `ModdingOperation` enum (14 operations), `ModdingOperationRequest` and `ModdingOperationResult` records, one `switch` dispatch. Each run opens `LogService.BeginScope(...)` and re-raises `LogReceived`/`ProgressChanged` events. Batch operations call `RunAsync` recursively. Temp work goes to `%TEMP%/g3mtool-gui/<guid>` and is deleted in `finally`.
- `Services/ScriptExecutionService.cs`: compiles `.csx` with Roslyn, after `UTMTScriptTranslator.Translate`. It caches the last compiled script under a `Lock`. Nested `RunUMTScript` calls must stay inside the root script folder. Output goes through `PatchInputService.WriteDataFile`.
- `Services/ScriptInteraction.cs`: five async delegates (question, input, open, save, folder) that the window marshals with `Dispatcher.UIThread.InvokeAsync`. This decouples script UI from the window.
- `Services/ExternalProcessService.cs`: runs a program and streams stdout and stderr lines into `LogService`.
- `Services/EmbeddedXDeltaPathProvider.cs`: extracts the per-RID embedded xdelta to a temp folder, sets the Unix execute bit and deletes the folder on exit. The CLI has an identical copy at `G3MTool/G3MToolCLI/Services/Execution/EmbeddedXDeltaPathProvider.cs`.

Log view. `SelectableTextBlock` with colored `Run`s per level: Information `#6DE985`, Warning `#F6C453`, Error `#FF7474`, Trace `#9CA3AF`. The buffer is trimmed past 1,000,000 characters (`MainWindow.axaml.cs` lines 298 to 349). Log font: `Cascadia Mono, Consolas`, 12 px.

Styling (`G3MTool/G3MToolGUI/Styles.axaml`).

| Token or selector | Value |
|---|---|
| `G3MBackground` | `#282828` |
| `G3MElements` | `#222222` |
| `G3MBorder` | `#039D5B` |
| `G3MAccent` | `#6DE985` |
| `G3MText` | `#E8E9EB` |
| `G3MMuted` | `#9CA3AF` |
| `PanelBrush` | `#E6222222` |
| Window font | `Segoe UI, Arial` |
| `Border.card` | border `#80039D5B`, 2 px, radius 10, padding 10 |
| `Button` | background `#2B3431`, hover `#3A4A43`, border `#7A039D5B`, 2 px, radius 6, padding 10,5, 0.12 s brush transitions |
| `Button.primary` | background `#039D5B`, border `#6DE985`, white SemiBold text |
| `TextBox` | background `#CC171717`, border `#70616B78`, 2 px, radius 5, padding 8,4, focus border `#039D5B` |
| `ComboBox` | as TextBox, padding 10,7 |
| `ProgressBar` | track `#3A3A3A`, fill accent, height 7, 0.18 s value transition |

The window also draws `Assets/images/G3MTool_bg.png` at 0.18 opacity behind everything (`MainWindow.axaml`).

Patterns worth reusing in G3MStudio: the request/result operation service with one log scope per operation; `ScriptInteraction` delegates for script UI; staged writes through `PatchInputService.WriteDataFile`; the embedded xdelta extractor; the color tokens.

Release targets and packaging (`G3MTool/.github/workflows/build.yml`, `G3MToolGUI.csproj`).
- RIDs: `win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`, `osx-x64`, `osx-arm64`, each on a native runner.
- With a RID set: self-contained, single file, compressed, native libraries and content self-extracted, no PDBs. The `GameSpecificData` folder is removed from publish output because the library embeds it.
- xdelta binaries per RID are embedded resources from `G3MToolGUI/Assets/bin/<rid>/`.
- macOS GUI builds are wrapped in `G3MToolGUI.app` with a generated `Info.plist`. Every build is zipped with `LICENSE`, `THIRD_PARTY_NOTICES.md`, `SECURITY.md` and `licenses/`.
- Manual `workflow_dispatch`. Component tests run before publishing. The release tag defaults to the UTC date `YYYY.MM.DD`. No code signing or notarization step exists.
- Both GUI and CLI consume G3MLib as the NuGet package `G3MLib` 1.0.1, not as a project reference.

## 9. Tests

| Project | Framework | Test files | `[Fact]` / `[Theory]` | Result on 2026-10-25 |
|---|---|---|---|---|
| `G3MLib/G3MLib.Tests` | xUnit v3 4.0.2, VSTest runner | 72 | 651 / 28 | 771 passed, 0 failed, 10 s |
| `G3MTool/G3MToolCLI.Tests` | xUnit v3 | 5 | 23 / 2 | 27 passed, 0 failed, 45 s |
| `G3MTool/G3MToolGUI.Tests` | xUnit v3, Avalonia.Headless 12.1.4 | 2 | 12 / 1 | 14 passed, 0 failed, 20 s |

Commands, as used by CI (`G3MLib/.github/workflows/test.yml`, `G3MTool/.github/workflows/build.yml`):

```shell
cd G3MLib
dotnet test G3MLib.Tests/G3MLib.Tests.csproj --configuration Release

cd G3MTool
dotnet test G3MTool.sln --configuration Release
```

Coverage by area, from file names in `G3MLib/G3MLib.Tests/`: decompiler control flow and output (the largest group), compiler lexer, parser and bytecode generation, I/O round trips on four fixtures, project round trips, sprites, rooms, strings, UI nodes, patch, diff, merge inputs, hashing, cache, integrity, xdelta, output commit, legacy script translation. Benchmarks are a separate console project, `G3MLib/G3MLib.Benchmarks`.

## Gaps G3MStudio must fill

| Gap | Why it matters | Suggested home |
|---|---|---|
| Undo/redo command stack over model edits | Every editor action must be reversible; the model only notifies, it does not record. | G3MStudio, with a small G3MLib hook if edits need to go through one API |
| Public lexer tokens and structured diagnostics (line, column, span, severity) | Syntax highlighting, squiggles and jump-to-error need positions as data, not message text. | G3MLib |
| Decompiler source map (text range to AST node and instruction offset) | Click-to-bytecode, breakpoints, and keeping edits aligned with disassembly. | G3MLib |
| Reverse reference index and find-usages | Rename, safe delete and "where is this sprite used" need it; references are forward-only today. | G3MLib |
| Rename refactoring across names, code and functions | Renaming a script or object must update GML, function names and code entry names together. | G3MLib (service), G3MStudio (UI) |
| Async read and write with numeric progress and cancellation | A 92 MB file takes about 0.6 s to read and 0.4 s to write on a fast machine and longer on slow ones; the UI must show progress and allow cancel. | G3MLib |
| Full-game git-friendly project format with sorted, stable output and resource order | `ProjectContext` covers 13 types and exports only marked assets; the modding export covers 30 types and order but is undocumented and patch-oriented. One of them must become the editor's save format. | G3MLib |
| Unknown chunk passthrough | New GameMaker releases add chunks; today the whole file fails to load. `GameMakerUnsupportedChunk` exists but is unused. | G3MLib |
| Texture page repacking of existing pages | Editing sprites over time fragments pages; GameMaker games expect compact atlases. | G3MLib |
| Audio decode and playback (OGG, WAV, optionally MP3) | Sound preview in the editor. | G3MStudio (playback), G3MLib only if decoding is needed for import conversion |
| Font preview and TTF import | Fonts are opaque glyph tables today; editing needs rendering and regeneration. | G3MStudio (preview), G3MLib (import) |
| Room renderer (layers, tiles, instances, views, depth order) | Room editing is the main visual task in an UndertaleModTool replacement. | G3MStudio, with geometry helpers in G3MLib |
| `IScriptInterface` host with real selection, search output and dialogs | UTMT scripts depend on selection, prompts and clickable search results; today hosts stub or throw. | G3MStudio |
| One shared `ScriptGlobals` matching `IScriptInterface`, with the translator applied in every path | Two diverging copies exist in G3MTool, and `ProjectContext` skips the translator. | G3MLib |
| UTMT API compatibility check | Script compatibility is unverified against the UTMT revision G3MLib was compared to. | G3MLib tests |
| Thread model for live editing | `GameMakerData` has no locking and some statics are process-wide; background decompile or export during edits needs a documented rule. | G3MLib (document and fix statics), G3MStudio (enforce) |
| Cancellation for patch, merge, export and import | Long modding operations (5 to 9 s on the benchmark file) cannot be stopped. | G3MLib |
| Code signing and notarization in release CI | Unsigned single-file builds trigger SmartScreen and Gatekeeper warnings. | G3MStudio CI |

## Rename and thread model

Work items 5 (rename part) and 8 of the 1.1.0 plan, branch `l2/eb` (from `l2/batch4`). Commits: `b8bb903` rename service, `aed7a36` thread model. Suite: 1055 of 1055 tests (1007 at start).

### Rename service

`G3MLib.DataFile.References.RenameService` (static):

- `RenameResource(data, resource, newName, index = null)` for sprite, object, room, script, sound, font, path, timeline, shader, sequence, animation curve, particle system and background (tile set).
- `RenameVariable(data, oldName, newName, index = null)` renames every variable entry with that name, whatever its instance type, and the matching code locals.
- `RenameFunction(data, oldName, newName, index = null)` renames a function defined in game code (`gml_Script_name` entries, the code entries and code locals with that name). A name that belongs to a script renames the script.
- Result: `RenameChangeSet`, a list of `RenameChange(String, OldValue, NewValue, Location)`. `Undo()` restores the old names, `Redo()` applies them again; both throw `InvalidOperationException` and change nothing if a name was edited after the rename. `Uses` lists the holders of the renamed string (code string literals and others) when a `ReferenceIndex` is passed.
- Refusals are `RenameException` with `Reason` (`InvalidName`, `NameInUse`, `NotFound`, `Builtin`) and a message that names the conflict. All checks run before the first change. Valid names are ASCII identifiers and not GML keywords. A new asset name must be free in all asset lists, new code entry names must be free in the code list, a new variable name must be free among variable entries. Built-in variables and built-in or extension functions are refused.

Behaviour: resources are referenced by object, so a rename changes text only. The name is one shared string entry, so object events, room instances, layers, sequences, fonts, tile sets and the other resource references need no edit. The service also rewrites names that embed the resource name: `gml_Object_<name>_<Event>_<n>` (and the collision form `..._Collision_<name>`), `gml_Script_<name>`, `gml_GlobalScript_<name>`, `gml_Room_<name>_Create`, `gml_RoomCC_<name>_<id>_Create`, `gml_Timeline_<name>_<moment>`, the matching function entries and code locals, and anonymous function entries that contain those prefixes.

Limits:

- A string entry is shared by all its holders. Code string literals equal to the name change with it (intended for `asset_get_index("name")`), and so does any unrelated holder that shares the entry, for example a layer with the same name. The change set and `Uses` show them.
- Names inside longer strings, and references stored as plain numbers, are not found.
- A `GlobalDecompileContext` caches asset names; build a new one after a rename. A `ReferenceIndex` stays valid because it is keyed by object.
- Old-style code names for object events in GameMaker Studio 1 data (numbers instead of names in collision events) are not special-cased.

Tests (`RenameServiceTests`, 24 cases): one per resource type on the 2026 fixture, object, script and room code names, code literals, variable, function, invalid and colliding names, refused rename changes nothing, undo guard, rename and back byte-identical on the fixture. Rename and rename back writes a byte-identical data file for Pizza Tower and Undertale (object, script, room, sprite, sound, font, timeline and a global variable renamed and restored; run with `G3MLIB_RENAME_DATA_FILES` set to the two paths).

### Thread model

Rule, written as a remark on `GameMakerData`: a data file is owned by one thread, which makes all edits. Background jobs work on a snapshot (a second `GameMakerData` read from the file) or hold `ModelLock.Read()` while the owner holds `ModelLock.Write()` for each edit. Concurrent reads are safe when nobody edits.

`G3MLib.DataFile.ModelLock`: reader-writer lock, `Read()` and `Write()` return an `IDisposable`, no recursion, kept by the host next to the model (not a property of `GameMakerData`, because reflection over its properties would pick it up).

Statics found and handled:

| State | Result |
| --- | --- |
| `GlobalDecompileContext` built global functions in place on the shared data | Built in a local and published when complete |
| `PrepareForCompilation` lazy builtin list, code builder and asset lookups | Serialized by a per-context lock |
| `GameSpecificResolver.BaseDirectory`, `EmbeddedDataAssembly` | Volatile backing fields; definitions were already behind a lock |
| `LogService.Verbose`, `Suppress` defaults | Volatile |
| `XDeltaService.DefaultExecutablePath`, `ModdingMetadata.ToolName`, `ToolVersion` | Volatile |
| `Assembler` lookup caches | Already per thread; documented, call `ClearLookupCaches` when done |
| `LogService`, `ResourceImportService`, `XDeltaService` scopes | Already per async operation |

Tests (`ThreadModelTests`): decompile (three workers) and delta project export (code, scripts, objects) on background threads while another thread edits sprite, sound and font properties, compared with single-threaded output, 20 runs, all passed; locked structural edits (add and remove sprite) against locked decompile jobs; lock exclusion; parallel read, decompile and write of eight copies gives the same bytes; eight contexts built and prepared on one model.

Benchmarks (Pizza Tower, `--full`, two runs per branch, best of minimum times): no operation moved beyond the run-to-run noise of this machine, which was about 10 to 15 percent in both directions even on untouched code (md5 +9.6 percent, decompile -14.7 percent). The changed code is not on the read, write or export hot paths. A quiet-machine rerun belongs to plan item J.
