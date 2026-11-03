# Library and app split

Status: proposed (research for Q45, written 2026-10-26). Input for the G3MLib 1.1.0 release candidate and for [../design/script-features.md](../design/script-features.md). Nothing here was built or run; every code claim comes from reading source, with file and line so it can be checked. Rules this document follows: G3MLib stays low level (Q25), plugins get everything through G3MLib and extension points (Q47), the library is consumed from NuGet only (ADR-0003).

## 1. Method

1. Read UndertaleModTool 0.9.2.0 (master f43e12c, shallow clone, read only) for each feature SF-01 to SF-13: the bundled scripts, the script host and windows in `UndertaleModTool`, and the library code they call in `UndertaleModLib` and `Underanalyzer`.
2. Read G3MLib on `l2/batch6` (which contains batch4 and batch5) for the same features: what exists, what is parallel, cancellable, cached, thread safe.
3. For each feature: choose the split, design the efficient version, list the library gaps with an API sketch.

Line counts are rough: counted from file line numbers while reading, not with a tool. Treat them as orders of magnitude. G3MLib paths are relative to the `G3MLib` project folder; UTMT paths are relative to the repository root.

## 2. What UTMT teaches

UTMT does not have a "library versus GUI" split for batch work. It has three layers:

| Layer | Holds | Size |
| --- | --- | --- |
| UndertaleModLib and Underanalyzer | The data model, read and write, decompiler, compiler (`CodeImportGroup`, `CompileGroup`), texture decode helper (`TextureWorker`, about 300 lines) | The format, no workflows |
| UndertaleModTool (WPF) | Editors, the script host, a few windows (search in code, find references) | The find references logic alone is about 2,500 lines of lambdas here, not in the library |
| 110 bundled scripts | Every batch feature: walk, name rules, layout, group file lookup, packing, error text | 100 to 1,300 lines each; the same code is pasted into several scripts |

Result: for every feature in this study, 85 to 100 percent of the logic sits outside the library (table in section 4). The library is a model and a file format; the "tool" is copy-pasted script text. That is the first thing not to copy. The second is the list of weaknesses that repeat across features:

1. No cancellation anywhere. The script host disables the whole window for the run (`UndertaleModTool/MainWindow.xaml.cs:2306`), no script receives a token, and closing is the only abort.
2. Mutation in place with no staged copy. A failure in the middle leaves a half changed data file (`ImportGraphics.csx`, `Reduce...Pages.csx:122-123` clears all page items first, `GameObjectCopy.csx:184` swallows every error).
3. Linear lookups on big lists: `ByName`, `IndexOf`, `MakeString` (`UndertaleModLib/UndertaleDataExtensionMethods.cs:22-67, 148-186`, `UndertaleData.cs:421` uses reflection per call). Searching 100,000 strings for each of 1,000,000 references is the cost of `FindUnusedStrings.csx`.
4. No decompile cache. Every consumer decompiles again (`SearchInCodeWindow.xaml.cs:224`, `ICodeImportOperation.cs:173`, `CheckDecompiler.csx:96,110`), and replace decompiles every entry even when nothing matches.
5. Whole file work per item: an audio group file is read and written once per sound (`ExternalizeAllOGGs.csx:135-145`, `ImportSounds.csx:192-210`), the decoded page set is kept for the whole run (`TextureWorker.cs:14`), decode is serialized by one lock (`TextureWorker.cs:27`).
6. Disk round trips and double encodes: atlases are written as PNG into the program folder and read back (`ImportGraphics.csx:29,54`), items are dumped to files before packing (`NewTextureRepacker.csx:456`).
7. Quadratic packers: best fit scan plus `List.Remove` (`Reduce...Pages.csx:466-536`), LINQ sorts and a pairwise prune on every allocation (`NewTextureRepacker.csx:116-172`).
8. Global state: `static` fields shared between runs (`imagesToCleanup`, `FindReferences...MethodsMap.cs:46-50`, `isSearchInProgress`), so a failed run poisons the next.
9. Silent failure: `catch { }` blocks, `-1` sizes for unreadable images, error text written into the output file, empty WAV written for missing audio.
10. Integer traps: `(ushort)` casts in every texture script, `uint`/`int` casts in the extractor (`ExtractEmbeddedDataFile.csx:36-37`).
11. Silent corruption: audio entries removed without renumbering the other sounds' `AudioID` (`ImportSounds.csx:203,220`, `RemoveUnusedSounds.csx:120,151`); unknown chunks dropped by the converters.

G3MLib already beats UTMT in some places (lock free page cache, encoded frame cache, ambient cancellation with rollback, a reverse reference index, page items that move instead of being rebuilt, temp file plus move for audio group writes). It also inherited several of the weaknesses above; they are listed in section 7.3 because 1.1.0 is the last cheap moment to fix them.

## 3. Rules for the split

1. **Library: one operation on the model, done once, fast.** It owns model knowledge that is expensive or risky to rebuild outside: reference walking, indexes, packing, page editing, audio group files, compile, clone with remap, chunk order. Every public operation takes a `CancellationToken` and an `IProgress`, returns per item results (never swallows), and states its thread rule.
2. **App: policy, planning, presentation, undo.** Selection, name rules, layouts, option defaults, plan tables, sidecar files, merge of results, undo steps, jobs, caches whose lifetime follows the workspace (decompile text, reference index, name index).
3. **Plan and apply.** Anything that changes the model has a read only `Plan` (parallel, cancellable, no allocation of pixels or files) and a single threaded `Apply` that has no cancel point and returns a change set that can undo itself (the `RenameChangeSet` shape in `DataFile/References/RenameService.cs:41-90`). This gives "plan first", "cancel leaves the file untouched" and "one undo step" (script-features 3.1, 3.3, 3.4) without a staged copy of the whole model.
4. **Identity, not index.** The model references objects, indexes are produced at write time. Work on object identity; keep one `DataLookup` (name and string index) per opened file. Only index based data (audio ids, collision event object index, typed asset operands, texture page item order) needs remapping, and the library does that in one pass.
5. **Decode once, work on raw spans.** One decode of a page to raw BGRA, cut frames as spans, encode once per output. Memory bound by a byte budget, not by core count (script-features 3.11).
6. **One scan feeds many features.** One instruction pass yields variables, functions, strings and typed asset references per code entry; the reference index, SF-05, SF-06 and SF-07 read its result.
7. **Results, not exceptions and not silence.** `ItemResult(name, kind, message)` per item; the summary table of every batch dialog is that list.
8. **Atomic file output.** Every file the library rewrites goes through temp file and move (`Modding/Internal/OutputFileCommit.cs` exists). Cancel deletes temp files and leaves originals.
9. **Additive APIs can follow in 1.1.x; shape changes cannot.** Q3 and Q4 say breaking changes ship as 1.1.0. So 1.1.0 must contain every change that alters the signature or behaviour of an API that already exists on the branches, plus the primitives the beta features cannot be built efficiently without. Pure additions can ship in 1.1.x before the feature that needs them.

## 4. Summary table

Effort is for the whole feature, library plus app: S under 3 days, M under 2 weeks, L longer. "L-nn" are the library items of section 7.

| Feature | UTMT: app or script side | UTMT: library side | Proposed split | Library gaps | Effort |
| --- | --- | --- | --- | --- | --- |
| SF-01 Export | about 2,000 script lines (20 scripts, 100 to 200 each) plus 130 host | about 300 (`TextureWorker`) | App: layout, names, sidecar, plan, room render, GIF. Library: page ordered frame cut, audio group reader, per item results | L-02, L-03, L-07 | M |
| SF-02 Import | about 1,300 (graphics), 310 (sounds), 53 (GML) | about 100 images, 650 `CodeImportGroup` | App: scan, plan, parallel decode and trim, policy. Library: packer, page editor, mask, compile group, lookups | L-01, L-02, L-04, L-05, L-08 | L |
| SF-03 Search and replace | 476 (window, find only), 54 (script) | 0 (find), about 250 plus decompiler and compiler (replace) | App: search over cached decompiled text, hit list, plan. Library: compile group with plan, stage, diagnostics | L-08 | M |
| SF-04 Rename by pattern | about 80 (a text box and a dialog) | about 20 | App: pattern engine, preview. Library: batch rename plan and apply | L-10, L-01 | S |
| SF-05 Find resources | about 2,500 (find references) plus 170 (scripts) | 0 | Library: reference index (exists). App: queries, results | L-09 | S |
| SF-06 Integrity | about 325 | shared decompiler, compiler | Library: read only check, locals, counters. App: phases, cache, report | L-08, L-11 | M |
| SF-07 Remove unreferenced | about 925 | 0 | Library: unreferenced for all kinds, bulk removal, audio renumber. App: report, selection | L-07, L-09, L-11 | M |
| SF-08 Texture page tools | about 1,700 (a packer pasted into two scripts) | about 270 | Library: repack plan and apply, page editor. App: scope, options, scale math, plan table | L-04, L-05 | L |
| SF-09 Copy from another file | about 1,190 | about 150 | Library: dependency list, clone with remap, code copy. App: selection, clash rule, plan | L-13 | L |
| SF-10 Audio tools | about 900 (pasted four times) | about 150 | Library: audio group file, id remap. App: policy, transcode, backup | L-07 | M |
| SF-11 Asset and room order | about 260 | 0 | App: all of it, on the reference index. Library: nothing new | L-09 | S |
| SF-12 Compare folders | 44 | about 25 | App only (Magick.NET is already a dependency) | none | S |
| SF-13 Data file tools | about 330 | about 70 | Library: ordered chunk write, converter. App: locator, chunk table, dialogs | L-12, L-16 | M |

## 5. Features

Each section: how UTMT does it (and where it hurts), what G3MLib has, the split, the efficient design, the gaps.

### SF-01 Export Resources

**UTMT.** Twenty scripts, each with its own walk and naming. Host side: `RunScript` (`MainWindow.xaml.cs:2300-2362`) disables the window and runs Roslyn; progress is a polled shared int (`:2097-2230`). Library side: `TextureWorker` (299 lines) for page decode and frame cut, Underanalyzer for GML, nothing for sounds (`ExportAllSounds.csx:45-90` reads group files itself). `ExportAllSprites.csx:26-92` is the best design in the pack (group frames by page, one worker per page, parallelism `cores/4`); the tileset, font, mask, GIF and texture scripts share one `TextureWorker` that keeps every decoded page until the end and decodes under one global lock (`TextureWorker.cs:27`). Weaknesses: no cancel, UI frozen, no name sanitizing (`a:b`, `CON`, case clashes overwrite), empty WAV for missing audio (`ExportAllSounds.csx:110`), error text written into the `.gml` (`ExportAllCode.csx:53-56`).

**G3MLib today.** `ResourceExportService` (3,100 lines, static) writes the mod archive layout, not the SF-01 layouts. Good parts: `TextureWorker` is lock free per page with a raw BGRA copy and an encoded frame cache keyed by geometry (`DataFile/Util/TextureWorker.cs:18-110`); parallel per type through `CancellableParallel`; ambient cancel with rollback (`Modding/Logging/ModdingCancellation.cs:47-86`); page export writes stored bytes without re-encode (`ResourceExportService.cs:1049`). Weak parts: only four public overloads take a token (`ExportTypes`, `ExportSelectiveExceptCode`, `ExportCodeEntries`, `ExportCodeEntriesSelective`); no per item progress or result; empty `catch {}` at `ResourceExportService.cs:1279-1287, 1535, 1566, 2797-2832`; `ExportSounds` parses the whole audio group file for every sound (`:1520`) and the group cache above it stores only null (`:1464-1491`), which is O(sounds x group size); `TextureWorker` never evicts; `SafeName` (`:50`) maps different names to one folder silently.

**Split.** App owns layout, file naming, the plan, `settings.json`, the room renderer and GIF (Magick.NET, already a G3MLib dependency). Library supplies frames, sound bytes and decompiled text through primitives that take a caller chosen target, not a layout.

**Design.**
1. Plan: names go through one sanitizer in the app (reserved names, trailing dots, case clashes on case insensitive disks, 96 char limit with hash) before any file is touched; the plan table is that list.
2. Frames: group jobs by texture page, decode a page once to raw BGRA, cut all its frames as spans, encode in parallel, evict the page. Peak memory is `pagesInFlight x page size`. Shared rectangles encode once (the cache exists). Offer PNG compression level 1 as a "fast" option; the default zlib level dominates CPU on large exports.
3. Sounds: one `AudioGroupReader` per run, one parse per group file, entries read on demand (L-07). Missing audio is a warning row, no file.
4. Code: parallel decompile with one shared `GlobalDecompileContext` and one `DecompileContext` per worker; a failure marks one entry and writes the assembly fallback. Bound the degree by memory, not only cores.
5. Output: write into the target folder with truncating create; for "cancel keeps files written so far" nothing more is needed. Incremental re-export: store `ResourceHashService.HashAll` per resource in the sidecar and skip unchanged items.
6. Never write the PNG for an unchanged stored page (already done).

**Gaps.** L-02 (token, progress, per item results on the per type methods), L-03 (eviction and a raw frame cut with a path chosen by the caller), L-07 (audio group reader). Room PNG, GIF, sidecar, naming: app.

### SF-02 Import Resources

**UTMT.** `ImportGraphics.csx` (808 lines) and `ImportGraphicsAdvanced.csx` (about 1,250) contain the whole job. Hot spots: two recursive `Directory.GetFiles` per file in the validity pass (`:739, :762`), so O(files x tree); every source image kept in memory until the end (`:15, :486`); a best fit packer with `List.Remove` and a shrink loop that re-lays out the atlas log2(size) times (`:396-420, :573-648`); atlases written as PNG into `ExePath/Packager` and read back (`:29, :54`), then PNG encoded again into the data file (`:59`); `ByName` and `MakeString` linear per frame (`:98, :143`); collision mask by `GetPixel` per pixel (`:260`); modal questions inside the loop (`:724`); any exception after the first atlas leaves the file half imported. Sounds (`ImportSounds.csx`): group file rewritten per sound, `AudioID` not renumbered after `Remove` (`:203, :220`). The one good split is GML: `ImportGML.csx` is 53 lines because `CodeImportGroup` (about 650 lines) compiles the whole queue as one group.

**G3MLib today.** `ResourceImportService.Import` dispatches 30 types over an archive folder with ambient cancel and coarse progress (`Modding/Resources/ResourceImportService.cs:1548-1658`); no parallelism, no staged commit ("discard it" on cancel, `:1547`). `ImportSprites` builds local name and string dictionaries (`:5682-5697`), so it is O(1) per lookup, but `MakeString` is still linear (`DataFile/GameMakerDataExtensionMethods.cs:161-174`), the packer is a ported copy of UTMT's (`SpritePacker`, `:117-380`) next to the better `TextureGroupPacker`, and the mask code is the same `GetPixel` loop (`:6578`). `TexturePageRepacker` (`DataFile/Util/TexturePageRepacker.cs`) keeps item objects so references stay valid, shares equal rectangles and rebuilds group page lists, but is serial, has no token or progress, copies every image through `new MagickImage` (`:112, :124`) and writes `(ushort)` unchecked (`:68-69`). `CodeImportGroup` has the UTMT API, no token, no progress, no parallelism (`DataFile/Compiler/CodeImportGroup.cs:407-599`).

**Split.** App: scan the folder once into a dictionary, derive frames, duplicates and missing indexes from it; read sizes from headers for the plan; decode and trim in parallel under a byte budget; build the plan table; policy dialogs once per run. Library: packer, page editor, mask builder, compile group, lookups, and one `Apply` with no cancel point.

**Design.**
1. Plan is read only: one `EnumerateFiles` into a dictionary keyed by normalized name (O(n)), header size via `MagickImageInfo`, no pixel decode. Plan cost follows file count.
2. Prepare: decode and trim each file once to raw BGRA on the worker pool, keep only the trimmed span, release the source. Bytes in flight are capped.
3. Pack in memory per texture group (groups are independent, so groups run in parallel), compose the atlas buffer and encode once in the page's target format; never touch disk. New pages follow the target version's format rules.
4. "Replace in place" uses a page editor: decode a page once, blit all replacements, encode once. 500 frames over 12 pages cost 12 encodes, not a repack.
5. Content hash: identical frames across sprites share one page item; a hash of each source file plus geometry in the workspace temp folder makes a second import of an unchanged folder a no-op.
6. Apply: single thread, last, no cancel point. Undo stores the old `EmbeddedTextures` list and old item rectangles by reference (the repacker swaps page objects, `TexturePageRepacker.cs:164`), not pixel copies.
7. GML: queue every file, one `CodeImportGroup.Import`; compile errors carry file, line, column; nothing is written when any file fails unless "Skip failed files" (needs the dry run of L-08).

**Gaps.** L-01, L-02, L-04 (add raw BGRA, plan and apply, page editor), L-05 (one packer, span mask), L-08 (compile group: token, progress, diagnostics, dry run). Public re-resolve of cached references after list changes (script-features section 7) is not needed under rule 4: references are objects. Check that when L-01 lands and drop the item if so.

### SF-03 Search and Replace in Code and Strings

**UTMT.** Find is a 476 line WPF window with no library call (`Windows/SearchInCodeWindow.xaml.cs`): `Parallel.ForEach` over entries (`:198`), one shared `GlobalDecompileContext`, decompile inside the loop (`:224`), culture sensitive compare (`:266`), progress through `Dispatcher.Invoke` per entry (`:249`, which serializes workers on the UI thread), no cancel (`:74`, a TODO), swallowed errors (`:243-246`), result order by `Array.IndexOf` (`:325`, O(n x m)), static state (`:27-29`). Replace is the 54 line `FindAndReplace.csx` queueing into `CodeImportGroup`: decompile, replace and compile run in strict sequence on one task, the decompile happens even when nothing matches (`ICodeImportOperation.cs:173` before the check at `:206`), a failure leaves earlier entries already changed (`CodeImportGroup.cs:388-391, 639-644`). Search and replace can disagree: ordinal replace against culture search, multiline always on in replace. The string table is not searchable at all.

**G3MLib today.** Same shape: `CodeImportGroup.QueueFindReplace` and `QueueRegexFindReplace` (`:456, :489`), `Import` (`:599`), serial, in place, no token. Better: `DecompileContext` takes a token and has `DecompileToSourceMap` (`Analyzer/Decompiler/DecompileContext.cs:74-80, 206`); `GlobalDecompileContext` is shareable after construction (`DataFile/Decompiler/GlobalDecompileContext.cs`). There is no decompile cache and no string search.

**Split.** Search is text work over decompiled strings: app. The library does not need a `FindInCode`; it needs the compile side to be safe (L-08). A decompile cache belongs to the workspace, so it is app code.

**Design.**
1. Decompile cache in G3MStudio: key = (entry identity, instruction content hash, settings hash, game version); value = text plus line start offsets. A cold full search costs one decompile of everything, in parallel; every later search is a regex over strings. Invalidate one entry on edit or recompile; invalidate all when functions, scripts or asset names change (the rename plan knows the set). Optionally persist next to the project, keyed by file hash.
2. Pre-filter: a literal query is checked against the string, variable and function tables and an identifier to entries index built during the same decompile pass; entries that cannot match are skipped.
3. Matching: one compiled regex (`NonBacktracking` or with a timeout), ordinal by default, same options for find and replace; line and column from `Array.BinarySearch` over line starts.
4. Replace Selected: the hit list already says which entries match, so only those are decompiled again (cost follows hits, not code size). New text per entry is computed purely; then one `CodeImportGroup` with `QueueReplace(entry, newText)` per entry compiles as one group (L-08: token, progress, per entry result, dry run). Failure marks that entry and leaves it unchanged. Update the cache and `ReferenceIndex.Update` for changed owners only.
5. Strings scope: search the `Strings` list (O(total chars)). Replace edits `GameMakerString.Content`; code refers to the string object, so no reference needs rewriting. Show `FindUsages(string).Count` per change; warn on creating a duplicate text (`MakeString` returns the first).

**Gaps.** L-08 only. Everything else is app code. This is a deliberate reduction from script-features section 7 ("code search without import"): a library search call would force a second cache and hide the decompile cost from the app.

### SF-04 Rename Resources by Pattern

**UTMT.** No rename service. The name box is a string reference control (`Controls/UndertaleStringReference.xaml.cs:149-178`): a dialog offers "change this value" (`MakeString`, a linear scan of all strings) or "change the shared string" (edits `Content`, so every holder of that string changes silently). Code entry names, locals, function names and literals in code stay stale. No identifier check, no clash check, no undo, one dialog per rename.

**G3MLib today.** `RenameService.RenameResource`, `RenameVariable`, `RenameFunction` (`DataFile/References/RenameService.cs:129, 180, 221`): identifier and keyword validation, clash check, rewrite of embedded names in code, locals and function names, refusal before any change, `RenameChangeSet` with undo and redo. Cost per call: an O(A) clash scan (`:139-148`), new `Regex` objects (`:295-313`), a match over every code, locals and function name (`:153-164`) and a code name `HashSet` (`:336`). A batch of N is O(N x (A + C)) with heavy garbage, and sequential calls cannot express a swap (`a`<->`b`) or a shift (`n1`->`n2`, `n2`->`n3`) because the second step meets `NameInUse` (`:145`).

**Split.** The pattern engine (prefix, suffix, find and replace, numbering, case) is pure string work: app. The library receives final (resource, new name) pairs.

**Design.** `PlanRenames` validates all requests against the final name set, parses every code, locals and function name once into (kind, resource name, suffix) with one `GeneratedRegex` or a hand split on `gml_Object_`, `gml_Script_`, `gml_Room_`, `gml_RoomCC_`, `gml_Timeline_`, `gml_GlobalScript_`, looks the resource name up in the request dictionary, and records the change. `Apply` writes it as one change set and one undo step. Collision events (`gml_Object_<a>_Collision_<b>`) need both positions handled; test an object whose name is a prefix of another. The preview table computes lazily per visible page; the clash check runs once on a worker. The old single methods become wrappers over a plan of one.

**Gaps.** L-10 (batch plan and apply), L-01.

### SF-05 Find Resources

**UTMT.** All reference logic is in the WPF project: `UndertaleResourceReferenceMethodsMap.cs` (1,750 lines of per type lambdas), the two dialogs (about 700), static state (`:46-50`), a `List.Add` from many workers (`:1693`), "not in the map" shown as "no references found" (`:1552`, `FindReferencesResults.xaml.cs:70-77`). Finding unreferenced assets calls the predicate once per asset, O(assets x model) (`:1676`). The two scripts add hand loops with `ToLower()` per compare, `IndexOf` per hit, `Array.IndexOf` sort, and read only `Room.GameObjects`, not layer instances (`FindObjectsInRooms.csx:32-58`).

**G3MLib today.** `ReferenceIndex` (`DataFile/References/ReferenceIndex.cs`): parallel, cancellable `Build` (`:66`), incremental `Update` (`:79`), O(1) `FindUsages` (`:100`), reflection walker with cached members and compiled getters so a new model property needs no table (`:295-499`), `Reference` with owner, holder, member and instruction offset (`:22`). It covers "who uses this", "objects placed in rooms" (filter by owner type) and "objects that use a sprite" (filter by holder and member) today. Weak: `Update` re-reflects every list on each call (`:82, :184-220`), no progress on `Build`, `FindUnreferenced` handles assets only (`:148`), no parent chain, layer name only through the holder.

**Split.** Library: index, walker, queries by target. App: query dialog, results, open-at-place, instance listing (room walk over `GameObjects` and layer instances with dedupe by identity, 20 lines the room editor needs anyway).

**Design.** Build the index once per opened file in the background, update on each edit transaction with the exact changed owners; every query is then a dictionary hit and the Explorer's Find Usages is instant. Pattern queries compile once and run over a per list name index (L-01) and union the `FindUsages` results. Inheritance: `FindUsages(parent)` filtered by member `ParentId` gives children in O(1), so "includes parent objects" needs no new map.

**Gaps.** L-09 (progress on `Build`, cheap `Update`, filtered `FindUsages`).

### SF-06 Integrity Check and Repair

**UTMT.** Four scripts, about 325 lines, no GUI. `CheckDecompiler.csx` decompiles and recompiles every entry twice in one serial task, replacing entries in the open data (`:99-100`) and writing three text files next to the data file (`:43-48`); it swallows the error text (`:122`). `FindUnknownFunctions.csx` is O(F x (S + C)) with list lookups (`:65, 74-80`) and O(U^2) pruning, and removes functions without checking instruction references. `VariableFixer.csx` sets `IsDebuggerDisabled = true` as a hidden side effect (`:15`). `RestoreMissingCodeLocals.csx` is O(C^2) (`:12`), writes a one local placeholder with `LocalsCount = 1` and treats child entries as roots (`:8-20`).

**G3MLib today.** `DataIntegrityService` (`Modding/Integrity/DataIntegrityService.cs`, static, 946 lines): eight repairs and a validation, plain string results with no entry, severity or repair id, no token, no progress; `ValidateRooms` repairs `layer.ParentRoom` inside the "read only" validate (`:821-824`). Missing: round trip check, unknown functions, locals presence, variable counter consistency. `VarCount1/2` are incremented on define (`DataFile/GameMakerDataExtensionMethods.cs:232-262`), never recomputed.

**Split.** Library: pure checks and pure rebuild functions. App: phases, scheduling, per entry result cache, report, the Fix actions and the checkpoint.

**Design.**
1. Three phases with different cost: structure (milliseconds, linear), tables (functions, variables, locals, counters: one instruction pass shared with the reference index), round trip (minutes). Run the first two on open and after edits; round trip on demand, and only for entries whose instruction hash changed since the last good result.
2. Round trip on scratch copies: decompile (parallel, array indexed by entry so output order is stable), compile into a scratch entry, disassemble both, normalize known noise (the `setowner` pattern in `CheckDecompiler.csx:24`), report the first differing line with three lines of context. The open file is never changed; this needs the dry run of L-08, because `CompileGroup` defines functions and variables in the data as it goes (`DataFile/Compiler/CompileGroup.cs:433-450`).
3. Locals: `BuildCodeLocals` from the local variable operands of the instructions, first use order, `arguments` first, skipping child entries. Counters: `RecomputeVariableCounters` pure, `ApplyVariableCounters` renumbers ids; the writer calls it when variables were added (script-features 4.6).
4. Unknown functions: hash sets of defined names (scripts, code, builtins, extension functions) built once; removal only when the reference index shows no instruction uses it.

**Gaps.** L-08 (dry run), L-09 (the shared scan), L-11 (locals, counters, unknown functions as pure functions). A structured `Check` that returns findings with severity and repair id is additive and can live in the app on top of those; fix `ValidateRooms` writing during validation in 1.1.0 (section 7.3).

### SF-07 Remove Unreferenced Resources

**UTMT.** About 925 script lines and no library support. `FindUnusedStrings.csx` (701 lines) has a hand written walker over 24 model types (`:72-587`) that misses particle systems, tags, sequence curves, extension option strings and the 2023 types, so a string used only there is deleted; it wraps parts in `try { } catch { }` (`:263-283`). `Data.Strings.IndexOf` per reference (O(S x R)), `Strings.Remove` in a loop (O(S x U), `:62`), removes variables and functions while iterating by index and skips elements (`:628-648`), always removes variables and functions even when asked only about strings (`:37`), never fixes `VarCount` or ids, decides before showing any count. `RemoveUnusedSounds.csx` compares blobs with `SequenceEqual` in three nested loops (`:101-153`), so identical audio hides an unused blob, deletes group files before the data file is saved (`:86-92`), and does not renumber audio ids.

**G3MLib today.** `ReferenceIndex` already reaches strings, variables, functions, code entries, texture items and audio through the walker, with no table to forget a type; `FindUnreferenced` only reports assets (`:148-181`). No bulk removal, no audio group file API, no renumber.

**Split.** Library: unreferenced for every kind from the one index, one pass compaction, audio renumber. App: report as plan, check boxes, "what becomes unused after removal", checkpoint, undo.

**Design.**
1. One index build covers every kind; each report is O(items) with O(1) lookups. UTMT's string pass alone is O(S x R).
2. Transitive effect without a rebuild: decrement counters on the index graph (`_ownerTargets` already stores owner to targets), report "N more after removing variables".
3. Removal: one compaction pass per list with a reference equality set (not `Remove` per item). Because references are objects, nothing is rewritten; only index based data is remapped: audio ids, the collision event object index (`DataIntegrityService.cs:690`), texture item order.
4. Audio by `(GroupID, AudioID)`, never by byte compare. Hash blobs only to detect duplicates.
5. Audio group files: open, remove, `Compact()` returns the old to new id map, one `SoundIdRemap.Apply` pass over the sounds, save through temp file and move; back up first.
6. Counters: `ApplyVariableCounters` after variables or functions are removed (UTMT never did).

**Gaps.** L-07, L-09 (unreferenced for all kinds, transitive), L-11.

### SF-08 Texture Page Tools

**UTMT.** About 1,700 script lines (a 300 line packer pasted into two scripts) against about 270 library lines. `ReduceEmbeddedTexturePages.csx` and `ImportGraphics_Full_Repack.csx` delete every page item and rebuild them by parsing file names (`:122, :163-232`): texture group links, `Scaled`, `GeneratedMips`, tile and room references are lost, and the pages are always PNG (`:142`, a TODO). Per item: one page decode, two PNG encodes, two PNG decodes. `NewTextureRepacker.csx` keeps the item objects (good) but sorts ascending by size (wrong for MaxRects, `:399`), uses LINQ ordering and an O(s^2) prune per allocation (`:116-172`), `AsParallel` on every allocation (`:152`), `IndexOf` inside a parallel select (`:244`), and clears `EmbeddedTextures` before success is known (`:378`). `ScaleAllTextures.csx` casts to `ushort` and `uint` without checks (`:50-79, :123-126`) and hard codes `room_ruins1` (`:132`), which throws for any other game.

**G3MLib today.** `TexturePageRepacker` moves existing items (`:142-150`), packs per texture group (`:82-88`), keeps shared rectangles shared (`:105-121`), follows the old page format (`:129`), copies `Scaled` and `GeneratedMips` (`:179-183`), skips when the result is not smaller (`:139-140`). `TextureGroupPacker` (747 lines): MaxRects with area sort (`:187-195`), duplicate detection by a 5 pixel hash then compare (`:149-174, :629-688`), power of two crop, one `ConvertToFormat` per page (`:381`). Weak: serial, no token, no progress, no plan, border copy per pixel through `GetPixel` and `SetPixel` (`:399-410`), per insert prune O(f^2) (`:415-531`), pages packed one after another although independent, `ResizeImage` offers bilinear and nearest only (`TextureWorker.cs:230`), nothing for scaling or page replace.

**Split.** Library: a plan (placements only, no pixels), an apply that moves rectangles and writes pages, a page editor. App: scope, option defaults, eligibility, the scale factor math over items, glyphs and tiles (checked `long` arithmetic, the overflow list for the plan), page replace from `index.png`, Magick resample, the plan table. The scale math reads public model rectangles, so it stays out of the library.

**Design.**
1. `Plan`: (item, source page, source rect, target page, target rect) from a better free rectangle structure and largest first order, O(n log n). The plan table shows pages before and after and the size difference without writing.
2. `Apply`: blit from raw source pages (page cache already exists) into raw target buffers, composite each output page on its own task, encode each page once in the group's format. Pages that receive no moved item are left byte for byte (no decode, no encode). Item objects keep identity, so sprites, fonts, tilesets and groups stay valid; only source rectangles are rewritten; texture group page lists are rebuilt, never cleared.
3. Scale: resample pages on a worker pool bounded by `memoryBudget / (w x h x 4 x 2)`; integer factors with nearest filter are a plain row copy. Plan fails when an item would exceed 65,535 or is not integral. Remap with `long` math and an edge correction pass.
4. Cancel is allowed everywhere except `Apply`; the checkpoint plus the old page list by reference is the undo record.

**Gaps.** L-04, L-05. Scale and replace pages: app.

### SF-09 Copy From Another Data File

**UTMT.** Five scripts, about 1,190 lines, about 150 library lines used. No deep clone: `CopySpriteBgFont.csx` copies fields by hand and shares donor objects (`GMS2TileIds`, `SpineTextures`, `YYSWF`, `V2Sequence`, `V3NineSlice`, `SepMasks`, `:108, :160-166`), so the donor graph leaks into the target; it sets `GMS2ExportedSprite = null` with a FIXME (`:105`). It reads the whole donor, matches names with `ToLower()` per request against every donor asset (`:78-82`), appends all donor pages and removes unused ones afterwards (`:70-76, :287-290`), so one used item brings a whole donor page; `IndexOf` per sprite frame (`:424-443`). Sounds: the cache dictionary is a local, so it never hits (`CopySound.csx:233-235`); one whole group file rewrite per sound. Objects: decompile the donor event, recompile in the target with one `CodeImportGroup` per action (`GameObjectCopy.csx:124-144`), losing code locals (`nativelocals` is never added, `:161-163`), and swallow every error (`:184-188`). Existing assets are overwritten in place with no rename or skip choice.

**G3MLib today.** A second file can be read (`GameMakerIO.Read(Stream, ..., onlyGeneralInfo)`, `DataFile/GameMakerIO.cs:1082`), and `MergeService` remaps ids and audio ids between files, but around patch folders and not in memory (`Modding/Merging/MergeService.cs:2255-2304, 4870-5152`). `GameMakerCode.Append` and `Replace` accept instruction lists (`DataFile/Models/GameMakerCode.cs:1871, 1884`); `Assembler` exists. No general `Clone`, no dependency walk, no property copier, no code copy between files.

**Split.** Library: the primitives that need model knowledge: dependency listing, clone with remap, page rectangle transfer, bytecode copy with operand remap and a verifier. App: which dependencies to include, the clash rule (Skip, Replace, Rename), the property field list, the plan, undo.

**Design.**
1. Dependency listing walks the reachable graph with a visited set (sprite to texture items, object to sprite, parent, mask, code and the sounds, objects and scripts its events reference, room to everything). For code it uses the instruction operands, not text search.
2. Clone with remap: a caller supplied map from source resource to target resource; references in the map are replaced, references not in it become null and are returned as `Unresolved`. Strings are re-interned through `DataLookup`. The batch form shares one lookup and one decode cache. Donor objects never survive in the target.
3. Pages: copy rectangles, not pages. Group requested items by donor page so each page decodes once, add them to the target's `TexturePageRepacker`, one repack at the end.
4. Code: copy bytecode with function, variable, string and asset operands remapped, locals and counters carried over, then verify (stack balance, branch targets in range, operand ids valid). No decompile and no recompile. Phases for a parallel batch: collect names, intern single threaded, rewrite instructions in parallel.
5. Donor is opened read only, optionally without audio payloads and with lazy page images (L-14), and disposed at the end.
6. `edit.duplicate` is the same code with donor equal to target: skip re-interning and decoding, copy the row and share the page item.

**Gaps.** L-13 (large), L-14 (small), L-01. Property copy by field list is a small copier over the public property registry (`Modding/Internal/ResourceTypeRegistry.cs`); it returns `(field, old, new)` for the plan.

### SF-10 Audio Tools

**UTMT.** Four scripts, about 900 lines pasted between them, about 150 library lines (a model and read and write). Every audio operation loads a whole `audiogroupN.dat` through `UndertaleIO.Read` and writes it back with `FileMode.Create`, no temp file (`ExternalizeAllOGGs.csx:135-145`): k sounds in one group cost k reads and k writes. Ids: `Remove` shifts later entries and the sounds keep their old `AudioID` (`ImportSounds.csx:203, 220`, `RemoveUnusedSounds.csx:120, 151`). Duplicates found with `SequenceEqual` per pair (`RemoveUnusedSounds.csx:110, 141`). Externalize writes `.wav` files, sets the flag to `.ogg`, and tells the user to convert by hand (`ExternalizeAllOGGs.csx:70, 155`); `files[0]` is unchecked (`:107`); `groupedExport` is uninitialized when no audio groups are used (`:50-58`).

**G3MLib today.** Models match UTMT (`DataFile/Models/GameMakerEmbeddedAudio.cs`, `GameMakerSound.cs:111-131`; sound to audio file is a resource reference). `ResourceImportService.TryWriteExternalSoundAudio` (`:4333-4391`) already writes a temp file and moves it, but still reads and writes the whole group per sound and uses static `_loadedDataPath` and `_savingDataPath` (`:4335`). `MergeService` repairs duplicate audio ids with a map in one pass (`:2254-2307`). `ResourceHashService.HashEmbeddedAudio` hashes in parallel (`:116-130`). No audio group API, no flag helper, no codec.

**Split.** Library: the audio group file, the id remap, the storage flag table in one place. App: selection, target storage, group naming, backup, plan, transcoding (a managed codec or the system one), the "OGG GameMaker cannot decode" message.

**Design.**
1. Group by file: one `Open` and one `Save` per group file for all its sounds; different groups in parallel. `Open` reads the AUDO table of offsets and sizes only; entries load on demand; externalize copies entry to output with `Stream.CopyTo`, so only one entry is in memory.
2. Ids: remove and add produce a map; `SoundIdRemap.Apply` fixes every sound in one pass; the plan stores the inverse for undo.
3. Storage as metadata: embedded uncompressed, embedded compressed, compressed streamed, external. The flag combinations from the scripts (`ExternalizeAllOGGs.csx:247-266`, `ImportSounds.csx:228-247`) live in one `SoundStorage` operation set. "Convert flag only" never touches bytes.
4. Transcode is CPU bound per entry: worker pool bounded by cores and by the largest decoded size, progress per entry. G3MLib only needs `AudioProbe.Detect` (header sniff) and an interface the app implements.
5. Back up each group file before rewriting; temp file plus move; cancel deletes temp files.

**Gaps.** L-07. Transcode: app.

### SF-11 Asset Order and Room Order

**UTMT.** Four scripts, about 260 lines, no library. `ImportAssetOrder.csx` is O(n^2) on observable lists: `order.Contains` on a `List<string>` (`:34`), `Remove` per item (`:59`), `Insert(0)` per item (`:61`); duplicate names are dropped silently (`:35`); a name made of digits is read as an index (`:46-47`); a missing name aborts after earlier types were already reordered (`:53`); unlisted assets end up in reversed relative order, not at the end. `MatchRoomOrderToInternalOrder.csx:17-18` cuts the display string at the first space and throws when it is unresolved.

**G3MLib today.** A correct O(n) `Reorganize` exists but is private to the patch importer (`Modding/Resources/ResourceImportService.cs:1327-1411`: one name dictionary, used set, duplicates, unnamed and null handled). `ReferenceIndex` finds typed asset operands with their instruction offset (`Member == "AssetReference"`, `:412-418`). Plain integer pushes are not found.

**Split.** All app. The order text format, the reorder (build the new list in an array, one `Clear` and `AddRange`), the room order helpers (set `RoomOrder` from `Rooms`, sort `Rooms` by `RoomOrder`) and the dependency report are small. The report uses the reference index: typed operands for the moved assets, collision event subtype (an object index), and pre 2.3 integer pushes that equal a moved index flagged "possible". Refuse when typed references exist and no fix is chosen; the fix rewrites typed operands and collision subtypes in the same undo step.

**Gaps.** None beyond L-09. Optionally make `Reorganize` public later; the app version is short.

### SF-12 Compare Folders

**UTMT.** `MergeImages.csx` (44 lines): scans A recursively but looks files up flat by name in B (`:24, :28`), so subfolders collide and the output is flat (`:43`); files only in B never appear; sequential; one core; forces 8 bit BGRA (`TextureWorker.cs:123-126`) and loses 16 bit depth; an unreadable file aborts the run.

**G3MLib today.** Magick.NET-Q8-AnyCPU 14.17.2 is already a dependency (`G3MLib.csproj:35`). Nothing for pairing or diff, and nothing is needed in the library.

**Split.** App only.

**Design.** Status first without decoding: pair by relative path (case rule explicit, "by name" mode lists duplicates instead of overwriting), compare length and a fast hash (the existing `Modding/Hashing/FileHash.cs` or `XxHash3`); equal bytes means same. Decode only on view, one pair at a time, with an LRU of 2 to 4 images. The viewer decodes through the UI framework's bitmap type, so Magick loads only for writing. Difference overlay is a per pixel loop over spans. "Save Side by Side" runs `Parallel.ForEachAsync` bounded by `4 x w x h x 3` bytes per task, keeps subfolders, writes truncating, ends with a summary table. Text files use a line diff on a worker.

**Gaps.** None.

### SF-13 Data File Tools

**UTMT.** Converters `15_To_16.csx` (160 lines), `16_To_17.csx` (37), `ConvertFrom17to16.csx` (30): mutate the loaded model, add or remove chunks, rebuild `FORM.Chunks` from a hard coded name list and let the writer re-emit. Weaknesses: the version is set before the chunk lookup that can throw (`16_To_17.csx:8-15`, `15_To_16.csx:124-126`), so a failure leaves a half converted model; chunks not in the list (`GMEN`, `TAGS`, `FEAT`, `PSEM`, `UILR`) are dropped silently (`:121-125`); hard coded game names (`15_To_16.csx:6-15`); destructive edits (`Options.Constants.Clear()`, `Build = 1804`, `:111-116`); chunk order depends on `Dictionary` insertion after a `Remove`. `ExtractEmbeddedDataFile.csx` (79 lines) scans byte by byte with a 4 KB `BinaryReader` and no progress, consumes bytes on a partial match so overlapping headers are missed (`:64`), reads the whole file into one `byte[]` with `(int)size` and `uint` arithmetic that wraps above 2 GB (`:36-37`), has no EOF guard (the null buffer is written, `:40-49`), opens read and write with no sharing (`:29`), takes the first match with no validation.

**G3MLib today.** `GameMakerChunkFORM.Chunks` is a `Dictionary<string, GameMakerChunk>` with a second dictionary by type (`DataFile/GameMakerChunks.cs:23, 28`) kept in sync by hand; the writer emits in enumeration order. The reader validates lengths against the FORM and keeps unknown chunks and trailing bytes (`DataFile/GameMakerIO.cs:282-320`, `GameMakerChunks.cs:133-190`). `GameMakerIO.WriteAsync(path)` writes a temp file and moves it, cancel leaves the target alone (`:1173-1199`). `reader.AllChunkNames` is internal (`:219`). `IsTPAG4ByteAligned` is public and honoured on write (`GameMakerData.cs:342`, `GameMakerChunks.cs:1724-1737`). No converter exists in G3MLib or in G3MTool: a search for `ConvertBytecode`, `UpgradeBytecode` and `ExtractEmbedded` finds nothing, although [../design/shell.md](../design/shell.md) lists `tools.convertBytecode` as existing. That command is new work.

**Split.** Library: the chunk layout knowledge (ordered `AddChunk`, `RemoveChunk`, canonical write order, unknown chunks kept) and the version transform on the model. App: locator and chunk table (pure byte functions, no model), dialogs, the "Save As only" and read back check.

**Design.**
1. Locator: memory map the file in 64 MB views with an 8 byte overlap, `Span.IndexOf("FORM"u8)` then check `GEN8` at +8, validate the chunk table (4 character names, lengths summing exactly to the FORM end, bytecode 13 to 17), report `Truncated` for dumps. Offsets and lengths are `long`. Copy with a 1 MB buffer. All candidates are listed, with game name from GEN8.
2. Chunk table: read chunk headers by seeking (about 40 reads), no model load; this feeds the Data File Settings panel with sizes.
3. Converter: `Plan` (chunks to add, remove, notes, refusals; 2.3 and newer refused with the reason), `Apply` builds every new chunk first and swaps them in with one ordered rewrite, so a failure leaves the model unchanged. Unknown chunks stay. No game name checks, only capability checks. No constants cleared unless the plan lists it.
4. Verify on a staged file: `WriteAsync(path)`, then read the chunk table and GEN8 only, full parse compare on request.

**Gaps.** L-12 (ordered chunk write), L-16 (converter).

## 6. Shared pieces G3MStudio builds once

Not library work; every feature above uses them.

| Piece | Used by | Notes |
| --- | --- | --- |
| Job service (progress row, cancel, result table, Copy and Save Log) | all | The one place that turns `IProgress` and `ItemResult` into the UI of script-features 3.3 and 3.5 |
| Plan frame (selection tree, options, plan line, Show Plan, virtualized table) | all batch dialogs | Pages of 1,000 rows |
| Name sanitizer and clash planner | SF-01, SF-04, SF-02 | Reserved names, trailing dots, case clashes, length |
| Decompile cache | SF-03, SF-06, code editor | Key: entry identity, instruction hash, settings, version; line starts stored |
| Workspace indexes (`ReferenceIndex`, `DataLookup`) | SF-02 to SF-11, Explorer | Built in the background on open, updated with the exact changed owners per edit |
| Undo journal over library change sets | all writing features | One step per run, named after the command |
| Raw image buffer helpers (BGRA span, trim, blit, hash) | SF-02, SF-08, SF-09, SF-12 | Avoid a `MagickImage` per frame |

## 7. The 1.1.0 library list

### 7.1 Must ship in 1.1.0

These change the shape of APIs that already exist on the branches, or are primitives the beta features cannot be built on efficiently. Items are single operations with batch execution (Q25).

**L-01 `DataLookup`** (S to M). One pass index over the file; `MakeString` and `ByName` inside the library use it.
```csharp
public sealed class DataLookup {                       // DataFile
    public static DataLookup Build(GameMakerData data, CancellationToken ct = default); // O(A + S)
    public GameMakerString? FindString(string s);                                        // O(1)
    public GameMakerString GetOrAddString(string s);                                     // O(1) amortized, keeps the index in step
    public bool TryGet<T>(string name, out T resource, out int index) where T : GameMakerNamedResource; // O(1)
    public void Add<T>(T item) where T : GameMakerNamedResource;
}
```
Thread rule: one writer, concurrent readers while no writer runs; a version counter on the lists detects a stale index. Replaces the local dictionaries at `ResourceImportService.cs:5682-5697` and the linear scan at `GameMakerDataExtensionMethods.cs:161-174`.

**L-02 Batch shaped export and import methods** (M). Every public per type method gets `IProgress<BatchProgress>`, `CancellationToken`, and returns `IReadOnlyList<ItemResult>`; the empty `catch {}` blocks go; the statics `_loadedDataPath` and `_savingDataPath` go.
```csharp
public sealed record ItemResult(string Name, ResultKind Kind, string? Message);        // Written, Skipped, Warning, Failed
public sealed record BatchProgress(int Done, int Total, string Item);
public static IReadOnlyList<ItemResult> ExportSprites(GameMakerData data, string outDir, ISet<string>? filter,
    IProgress<BatchProgress>? progress, CancellationToken ct);                          // same shape for every type
```
The ambient token stays as the implementation; the parameter is the public surface.

**L-03 Frame cut and eviction** (M). Lets the app export frames to paths it chooses, one page in memory at a time.
```csharp
public sealed class TextureWorker {                                                      // existing, extended
    public ReadOnlyMemory<byte> CutBgra(GameMakerTexturePageItem item, bool padding, out int w, out int h); // raw span cut, no MagickImage
    public void Evict(GameMakerEmbeddedTexture page);                                    // frees raw page, decode and encoded frames of that page
}
public static class FrameExporter {
    public static IReadOnlyList<ItemResult> ExportPng(IReadOnlyList<FrameJob> jobs, int maxPagesInFlight,
        IProgress<BatchProgress>? progress, CancellationToken ct);                       // groups by page, evicts when a page is done
}
public sealed record FrameJob(GameMakerTexturePageItem Item, string Path, bool Padding);
```
O(frames + pages decoded). Thread safe; one worker per run.

**L-04 `TexturePageRepacker` v2 and page editor** (M to L).
```csharp
public TexturePageRepacker Add(PreparedFrame frame, GameMakerTextureGroupInfo? group = null);   // raw BGRA, no Magick copy
public RepackPlan Plan(RepackOptions o, IProgress<BatchProgress>? p = null, CancellationToken ct = default); // read only, O(n log n)
public void Apply(RepackPlan plan);                                                      // single thread, no cancel point
public sealed class PageEditor {                                                         // replace in place
    public static PageEditor Open(GameMakerEmbeddedTexture page);                        // decodes once
    public void Blit(GameMakerTexturePageItem item, ReadOnlySpan<byte> bgra, int w, int h); // size must match, else error
    public void Commit();                                                                // one encode in the page's format
}
public sealed record PreparedFrame(int Width, int Height, int TargetX, int TargetY, ReadOnlyMemory<byte> Bgra);
```
`RepackOptions`: max page size, padding, power of two, max item size, keep alone predicate, group key, scope (all or listed texture groups). Size writes become checked (`TexturePageRepacker.cs:68-69`). Groups are packed in parallel; `Apply` composites output pages in parallel.

**L-05 One packer, span mask** (M, mostly internal). Delete the `SpritePacker` copy (`ResourceImportService.cs:117-380`) and use `TextureGroupPacker`; replace `CopyBorder` per pixel calls (`TextureGroupPacker.cs:399-410`) with row spans; add `MaskBuilder.Build` and `ReadMask` over spans (`ResourceImportService.cs:6578`, `TextureWorker.cs:254-283`); a free rectangle structure with largest first order.

**L-07 Audio group file** (M).
```csharp
public sealed class AudioGroupFile : IDisposable {                                       // not thread safe per instance
    public static AudioGroupFile Open(string path, CancellationToken ct = default);      // reads the AUDO table only
    public ReadOnlyMemory<byte> GetEntry(int id);
    public int Add(ReadOnlyMemory<byte> data);
    public void Replace(int id, ReadOnlyMemory<byte> data);
    public void Remove(int id);
    public IReadOnlyDictionary<int, int> Compact();                                      // old id to new id
    public void Save(string path, bool backup, IProgress<double>? p = null, CancellationToken ct = default); // temp file + move
}
public static class SoundIdRemap { public static void Apply(GameMakerData data, int groupId, IReadOnlyDictionary<int, int> map); } // O(sounds)
public sealed class AudioGroupReader : IDisposable { public ReadOnlyMemory<byte>? GetAudio(GameMakerSound s, out AudioSource src); } // one parse per group, cached
```
Fixes `ExportSounds` (`ResourceExportService.cs:1464-1520`). `AudioIndex.Unreferenced` by `(GroupID, AudioID)` belongs to L-09.

**L-08 Compile group v2** (L). `CodeImportGroup`, `CompileGroup` and the find and replace operations get:
- `CancellationToken` and `IProgress<BatchProgress>`;
- a result per queued operation (`Applied`, `NoMatch`, `Failed` with file, line, column, message) from the existing diagnostics, so a patch that matches nothing is visible (this is also the "strict no match is an error" rule of the plugin candidates, without a separate patch layer);
- a **dry run** that compiles into scratch entries and defines nothing in `Data` (today `CompileGroup` links and defines functions and variables as it goes, `DataFile/Compiler/CompileGroup.cs:433-450`);
- a **stage** mode: compile everything, then commit all entries or none;
- decompile of find and replace operations only for entries whose text can match is the app's job (it passes known new text with `QueueReplace`), so no parallel compile is promised; independent decompile is parallel in the app.

**L-09 `ReferenceIndex` v2** (M).
- `IProgress` on `Build`; `Update` O(changed owners) (cache the member lists, `ReferenceIndex.cs:82, 184-220`, and track list versions);
- `FindUsages(target, ownerType?, member?)` without allocating the full list;
- `FindUnreferenced` for strings, variables, functions, embedded audio, texture items and empty pages, plus `Transitive` (worklist on `_ownerTargets`);
- public `CodeScan.Scan(GameMakerCode)` returning variables, functions, strings and typed asset references per entry (today `ModelWalker.ScanCode` is internal, `:393`), used by SF-05, SF-06, SF-07 and SF-11.

**L-10 Batch rename** (M): `PlanRenames(data, requests, index?, progress?, ct)` and `Apply(plan)` as in SF-04; plan holds `Conflicts` and `Changes`; swaps and shifts are legal; the single methods wrap a plan of one.

**L-11 Locals and counters** (S), pure functions: `BuildCodeLocals(data, entry)`, `RestoreMissingCodeLocals(data, progress?, ct)`, `RecomputeVariableCounters(data)`, `ApplyVariableCounters(data)`; the writer calls the last when variables were added.

**L-12 Ordered chunk write** (S): `GameMakerChunkFORM.AddChunk`, `RemoveChunk`, `SetChunkOrder` keep `Chunks` and `ChunksTypeDict` together; the writer emits a canonical order and keeps unknown chunks in place; `AllChunkNames` becomes public. This is a correctness fix, not a feature: today the order after `Remove` plus `Add` is whatever the dictionary gives.

### 7.2 Additive, can follow in 1.1.x before the feature starts

| Item | Feature | Notes |
| --- | --- | --- |
| L-13 `ResourceDependencies.Find`, `ResourceCloner` (clone with remap, batch form), `TexturePageTransfer.Copy`, `CodeCopier.Copy` and `CodeVerifier.Check` | SF-09 | Large. Needs model access, but the feature is outside the beta (OI-055 recommendation). Sketches in SF-09. |
| L-14 `GameMakerIO.Read(..., ReadOptions)` with `SkipChunks` and lazy page images, token on the sync facade | SF-09 | Donor load without audio |
| L-15 `AudioProbe.Detect(ReadOnlySpan<byte>)` | SF-10 | O(1) header sniff |
| L-16 `BytecodeConverter.CanConvert`, `Plan`, `Apply` for 15 to 16, 16 to 17, 17 to 16 | SF-13 | Needs L-12 first. Command is new work, see SF-13 |
| L-17 Structured integrity `Check(data, options, progress, ct)` returning findings with severity and repair id, `Repair` | SF-06 | App can wrap the pure functions first |
| `ResourcePropertyCopy.Copy(src, dst, fieldNames)` | SF-09 | Small, over the type registry |

### 7.3 Debts in existing G3MLib code to fix in 1.1.0 (no new API)

| Debt | Where | Why now |
| --- | --- | --- |
| Silent `catch {}` | `ResourceExportService.cs:1279-1287, 1535, 1566, 2797-2832` | Covered by L-02 |
| Dead group cache, parse per sound | `ResourceExportService.cs:1464-1520` | Covered by L-07 |
| Static state | `ResourceImportService.cs:4335` (`_loadedDataPath`, `_savingDataPath`) | Two runs cannot overlap; the thread model item (plan B) |
| Validate that repairs | `DataIntegrityService.cs:821-824` | "Read only" check writes |
| Linear `MakeString`, `ByName`, `IndexOf` | `GameMakerDataExtensionMethods.cs:22-62, 161-174`, `GameMakerData.cs:448-453` | Covered by L-01 |
| `CompileGroup` defines as it goes | `CompileGroup.cs:392-450` | Covered by L-08 |
| `TextureWorker` never evicts | `TextureWorker.cs` | Covered by L-03 |
| `ReferenceIndex.Update` re-reflects | `ReferenceIndex.cs:82, 184-220` | Covered by L-09 |
| Unchecked `(ushort)` | `TexturePageRepacker.cs:68-69` | Covered by L-04 |
| Dictionary order chunk write | `GameMakerChunks.cs:23` | Covered by L-12 |
| `SafeName` maps different names to one folder silently | `ResourceExportService.cs:50` | App sanitizer replaces it for SF-01; the library keeps its archive behaviour |

### 7.4 Safe to build in G3MStudio (not library work)

SF-01 layouts, `settings.json`, GIF and strip (Magick.NET), room PNG renderer. SF-02 folder scan, plan, GIF and strip splitter, `.yy` font reader. SF-03 search, hit list, decompile cache, string search. SF-04 pattern engine. SF-05 queries, instance listing. SF-06 phases and report. SF-07 report, selection. SF-08 scale math and page replace (L-06 in earlier drafts, moved here because it only reads public rectangles). SF-10 transcode, backup folder. SF-11 order text format, reorder, room order, dependency report. SF-12 all of it. SF-13 locator, chunk table, dialogs. Shared pieces of section 6.

### 7.5 What this changes in the earlier lists

- script-features section 7 had twelve gaps. Mapping: code search without import (dropped, app), reference visitor (L-09), locals and counters (L-11), image resize (app, Magick.NET), GIF, WAV, OGG (app), audio group helper (L-07), bytecode level code copy and dependencies (L-13), instance listing (app), index dependent report (app on L-09), data file locator (app), re-resolve references (not needed under rule 4, check with L-01), model aliases for the four API differences (unchanged, OI-040).
- OI-054 recommended three items (reference visitor, audio group helper, code copy). This study keeps the first two, moves code copy to 1.1.x, and adds the shape changes L-01, L-02, L-03, L-04, L-05, L-08, L-10, L-12 that the first list missed. See OI-058.

### 7.6 Final names on `l2/split` (2026-10-26)

Where the built API differs from the sketches above. Details and test counts: docs/status.md.

| Item | Built as |
| --- | --- |
| L-01 | `DataLookup.Build`, `FindString`, `GetOrAddString(string, out int)`, `TryGet<T>`, `Add<T>`; lists have `Version` (`IVersionedList`). |
| L-02 | Overloads `Export<Type>(data, outDir, [filter], IProgress<BatchProgress>?, CancellationToken)` and `ResourceImportService.Import(type, data, dir, progress, token)` returning `IReadOnlyList<ItemResult>`; `ResultKind` has Written, Skipped, Warning, Failed. |
| L-03 | `TextureWorker.CutBgra`, `TextureWorker.Evict`, `FrameExporter.ExportPng(jobs, maxPagesInFlight, progress, ct)`, `FrameJob`. |
| L-04 | `TexturePageRepacker.Add(PreparedFrame, group)`, `Plan(RepackOptions, progress, ct)`, `Apply(RepackPlan)`, `PageEditor`; `RepackOptions(MaxPageSize, Border, Groups)` only. |
| L-05 | `SpritePacker` removed; `MaskBuilder.Build` and `ReadWhite`. |
| L-07 | `AudioGroupFile`, `SoundIdRemap.Apply`, `AudioGroupReader.GetAudio`, `AudioSource`. |
| L-08 | `CodeImportGroup.Import(CodeImportMode, progress, ct)` returning `CodeImportResult`; `CodeImportMode` Apply, DryRun, Stage; `CompileGroup.Check`, `Compile(ct)`. |
| L-09 | `ReferenceIndex.Build(data, progress, ct)`, `FindUsages(target, ownerType, member)`, `FindUnreferenced(UnreferencedKind, transitive)`, `CodeScan.Scan`, `AudioIndex.Unreferenced`. |
| L-10 | `RenameService.PlanRenames`, `RenameService.Apply`, `RenameRequest.ForResource/ForVariable/ForFunction`, `RenamePlan`, `RenameFailure.Overlap`. |
| L-11 | `DataRepair.BuildCodeLocals`, `RestoreMissingCodeLocals`, `RecomputeVariableCounters`, `ApplyVariableCounters`. |
| L-12 | `GameMakerChunkFORM.AddChunk`, `RemoveChunk`, `SetChunkOrder`, `ChunkOrder`, `CanonicalOrder`; `GameMakerReader.AllChunkNames` public. |

## 8. Plugin API notes (Q47)

Q47 needs no change of shape in [../decisions/0009-plugins.md](../decisions/0009-plugins.md); it needs additions that follow from this study. Applied to that file:

1. `IStudioContext` exposes the shared services of section 6 (job service, decompile cache, reference index, data lookup, undo scope), so a plugin author calls them and does not rebuild them.
2. A threading rule the same as G3MLib's: background reads use a model snapshot or the read scope; writes go through one undo aware edit call; the plan and apply shape is the way batch work is written.
3. No separate code patch layer. The verbs (replace, append, prepend, find and replace, regex) already exist in `CodeImportGroup`; with L-08 each queued operation reports applied, no match or failed, which covers the strict "no match is an error" rule of OI-053. A helper library is added only if two real plugins repeat the same code.
4. Built-in import and export formats of SF-01 and SF-02 register through the same provider extension point plugins use, so a plugin can add a format to the shared dialog and its plan table.

## 9. Not verified

- Nothing was built or run. Every UTMT and G3MLib statement comes from reading source; no timing was measured. Estimates of cost (for example the rename batch) are arithmetic from the code, not benchmarks.
- Line counts are rough. For some files only a line range was read (`UndertaleResourceReferenceMap.cs`, `CompileGroup.cs`, the XAML of the name editors, `StringUpdateWindow`).
- Not read in depth: `ResourceTypeRegistry.cs`, `ResourceAssetUtil.cs`, `G3MCacheService` (whether it could hold decompile text), `GameMakerObservableList` (bulk remove), `15_to_17_To_16.csx`.
- Thread safety of `CompileGroup` and `DecompileContext` use per worker is assumed from the class comments; test it before relying on parallel compile.
- `ExportAllRoomsToPNG` and `ExportAllFonts` internals were only searched, not read.
- G3MTool conversion code was searched for `ConvertBytecode`, `convert-bytecode` and `BytecodeConvert` (no match); the G3MTool CLI help was not run.
