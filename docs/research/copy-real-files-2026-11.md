# Copy to Other Data File on real games (2026-11)

Date: 2026-11-05. Status: measured on Windows x64, Debug build, one machine (22 logical cores, 32 GB). Numbers vary by 20 to 40% between runs.

Goal: check [OI-337](../open-items.md) (Copy to Other Data File), which had only run on generated files, against real games: plan, copy, undo, redo, journal replay, save and open again, for same version and different version pairs.

## Method

`CopyRealFilesTests.RealGamesCopyBetweenEachOther` (skipped unless `G3MSTUDIO_PERF` is set). Pairs come from `G3MSTUDIO_COPY_PAIRS` (`source|target;source|target`) or, if that is empty, from `G3MSTUDIO_PERF_FILES` (every file into the next one). `G3MSTUDIO_COPY_LOG` appends the output to a file while the test runs; `G3MSTUDIO_COPY_BISECT` copies each sample item alone and saves, to find the item that breaks a save; `G3MSTUDIO_COPY_BIGROOMS` keeps the first room instead of the room with the fewest dependencies.

Both files are opened with `OpenDetachedAsync` and a temporary user folder. Nothing is written into the game folders: saves go to a temporary file, and the source is only read.

Sample per pair (from the source, first item that the planner does not refuse): one sprite with at least 3 frames, one object with events (a parent when there is one), one script, one sound with embedded audio, one room with at least 5 instances (the one with the fewest dependencies of the first 6), one font, one path, one timeline. Paths and timelines exist in few games.

Steps on the target, in order, with "Copy dependencies too" on:

| Step | Mode | Expected plan |
| --- | --- | --- |
| baseline | none | the target alone saves with `SavePipeline` and reads back (so a later failure is not blamed on the copy) |
| A | Rename | every free name is added; a taken name of a selected item is renamed |
| B | Rename | the same sample again: selected items renamed (`_copy`), dependencies skipped |
| C | Skip | nothing to apply, the target must not change |
| D | Replace | selected items replaced in place, dependencies skipped |

After every applied copy: the plan entries and their actions; one more entry per added or renamed item in each list, none for a replaced one; every applied item is in the target; a consistency check of the whole target (names unique, every page item in a texture page and inside it, sprite frames in the page item list, sound audio in the audio list and used once, object sprite, parent and mask in the file, event, script, room, timeline and instance code in the code list, start and end scripts in the code list, parent and child code entries pointing at each other, room instance ids unique and not above the last object id), compared with the same check on the target before the copy so old faults are not counted; a content fingerprint of each copied item against the source (sprite size, frame count and the pixels of every frame, object links and event layout with collision partners by name, sound bytes, room instances by object, position and scale, glyphs, path points, timeline moments); the code entries decompiled in the source and in the target and compared line by line. Then undo (the whole model signature, the item counts, the consistency check), redo, undo again and a command rebuilt from its journal arguments (`EditCommand.Create(kind, args)`), each compared with the state after the first copy. After A and after D the target is saved with `SavePipeline`, opened again with G3MLib (no more load warnings than before) and checked again, including the fingerprints. At the end the target is dropped with its edits unsaved and opened again: the journal must give back all applied edits and the same model. The source must be unchanged (names, counts, fingerprints of the sample items, and the bytes it would write).

## Files

| File | Version (bytecode) | Size |
| --- | --- | --- |
| DELTARUNE chapter 1 | 2023.6 (17) | 12 MB |
| DELTARUNE chapter 2 | 2023.6 (17) | 64 MB |
| DELTARUNE chapter 3 | 2023.6 (17) | 120 MB |
| DELTARUNE chapter 4 | 2023.6 (17) | 126 MB |
| DELTARUNE chapter 5 | 2023.6 (17) | 158 MB |
| Pizza Tower | 2022.9 (17) | 88 MB |
| Undertale Yellow 1.3.1 | 2023.4 (17) | 152 MB |
| FRICKBEARS3 | 2023.11 (17) | 30 MB (audio in audiogroup files) |
| Undertale | 1.0.0.1539 (16) | 60 MB |

## Results of the last full run (code of the fixes below)

12 pairs. Clean (all steps, save and reopen, journal replay, source unchanged): 7. Refused by the planner as expected (bytecode 16 against 17): 2. Failed at "save after A": 3, all fixed afterwards (bug 6 below).

| Pair (source to target) | Result |
| --- | --- |
| Ch 2 to Pizza Tower | clean |
| Ch 1 to Ch 2 | clean |
| Ch 3 to Ch 5 | clean |
| Ch 5 to Ch 4 | clean |
| Pizza Tower to Ch 3 | clean |
| Undertale Yellow to Pizza Tower | clean |
| Ch 4 to Undertale Yellow | clean |
| Undertale to Undertale Yellow, Undertale Yellow to Undertale | refused (bytecode 16 against 17), nothing changed |
| Pizza Tower to Undertale Yellow | failed at save before the fix (bug 6); clean after it |
| Undertale Yellow to FRICKBEARS3, Pizza Tower to FRICKBEARS3 | failed at save before the fix (bug 6); clean after it |

### Timings and memory of step A (first copy, dependencies on)

| Pair | Entries (add, refused, skip) | Plan | Copy | Package | Peak working set over start | Undo | Redo | Replay from arguments | Save | Open again | Journal replay on reopen (with the open) |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Ch 1 to Ch 2 | 21 (6, 2, 13) | 51 ms | 0.55 s | 146 KB | +39 MB | 4 ms | 144 ms | 164 ms | 2.3 s | 0.6 s | 2.0 s |
| Ch 3 to Ch 5 | 61 (19, 25, 17) | 174 ms | 1.9 s | 191 KB | +144 MB | 21 ms | 359 ms | 376 ms | 4.8 s | 1.1 s | 4.0 s |
| Ch 5 to Ch 4 | 35 (10, 12, 13) | 317 ms | 1.2 s | 168 KB | +62 MB | 20 ms | 181 ms | 176 ms | 3.7 s | 0.9 s | 2.7 s |
| Ch 2 to Pizza Tower | 320 (173, 144, 3) | 349 ms | 11.5 s | 1228 KB | +107 MB | 12 ms | 2.0 s | 2.1 s | 3.1 s | 0.8 s | 7.4 s |
| Pizza Tower to Ch 3 | 578 (265, 310, 3) | 660 ms | 14.8 s | 1711 KB | +127 MB (managed +676 MB) | 15 ms | 3.6 s | 4.0 s | 3.5 s | 0.9 s | 14.0 s |
| Undertale Yellow to Pizza Tower | 71 (47, 22, 2) | 150 ms | 2.7 s | 446 KB | +99 MB | 7 ms | 0.6 s | 0.6 s | 2.8 s | 0.7 s | 3.3 s |
| Ch 4 to Undertale Yellow | 344 (156, 180, 8) | 411 ms | 7.4 s | 876 KB | +255 MB | 12 ms | 1.7 s | 2.0 s | 4.0 s | 0.9 s | 7.1 s |

The copies B (rename) and D (replace) of the same sample, whose dependencies already exist, take 0.4 to 1.1 s, redo and replay 0.1 to 0.2 s. Planning is 28 to 660 ms. Undo is 4 to 70 ms in every run. The package grows with the number of items (146 KB for 6 items and their 15 dependencies, 1.7 MB for 265 items), is stored as text in the command (about 4/3 of the ZIP) and goes to the journal as a side file above 64 KB. Peak figures are the working set sampled every 15 ms during the copy; the figure often shows +0 MB because the heap was already large from the read before. The absolute working set of both open games was 0.8 to 3.7 GB.

Redo and replay take about as long as the first copy minus the export (the library import and the code compile), 2 to 4 s for a copy of 270 items. Reopening with a journal takes as long as opening the file plus replaying the edits.

### What was checked and matched

For every clean pair, in steps A, B and D: all planned actions as expected for the mode; item counts of every type exact; the target consistent; sprite frames, pixels, object links and events, sound audio bytes, room instances, glyphs and path points equal to the source (after saving and opening again as well); undo restores the whole model signature and the counts, redo and a command rebuilt from its journal arguments reach the same state as the first copy; the journal recovers all applied edits and the same model after the target was dropped; the source is unchanged (names, counts, fingerprints, written bytes).

Code read back: in the clean pairs, 54 of 54 (Ch 2 to Pizza Tower), 4 of 4, 15 of 15, 7 of 7, 59 of 59, 47 of 47, 56 of 56 entries decompile to the same text, except lines where the decompiler writes a constant by name in one file and as a number in the other: 4 asset names shown as the target's index (compiled correctly: the number is the asset's index in the target) and 13 to 14 names of constants of the source's GameMaker version (for example `vk_enter` against 13) in a target of another version. No line was left unexplained.

## Bugs found and fixed

1. A source file with no particle chunk made every copy fail ("Value cannot be null (source)" in `ResourceCopier.AddEmitters`). Deltarune 2023.6 has no particle chunk. Fixed with a null guard; generated test `AFileWithoutParticleSystemsCanBeCopiedFrom`.
2. Code of a script in a GameMaker 2.3 file is named `gml_GlobalScript_<script>`. The renamer knew only `gml_Script_`, so the code of a renamed script kept its old name, was compiled over the target's code of that name, and the script was not added (script count one short; the target's own code replaced). Fixed with the prefix in `CodePrefixes`; test `TheGlobalScriptCodeOfARenamedScriptGetsTheNewNameAndTheTargetCodeStays`.
3. Undo of a copy that had added a global script left a start script entry (GLOB chunk) pointing at code that undo had removed. Every later save failed with "The given key 'gml_GlobalScript_...' was not present in the dictionary". The compiler adds the start script entry on its own when it creates `gml_GlobalScript_*` code. `ModelSnapshot` now also keeps `GlobalInitScripts` and `GameEndScripts`. Test `UndoTakesTheStartScriptThatTheCompiledGlobalScriptAddedOutOfTheList`.
4. Undo of a copy that replaced an item threw `NotSupportedException: Collection was of a fixed size` when the item held an array (`ShallowMemento` cleared and refilled every list). Arrays of numbers (audio and picture bytes, which a replace swaps for new arrays) are now skipped, and arrays of objects are put back in place. This also stopped the memento from boxing every byte of audio. Test `AMementoPutsBackAFixedSizeListInPlaceAndSkipsNumberArrays`.
5. A script that defines a function or code entry the target already has under another script (for example `sound_play` in Deltarune chapter 4 and in Undertale Yellow) added a second code entry and script of the same name, so the list held duplicates. The planner now refuses such a script as an entry ("has the code entry X, which the target already has under another script"), and the objects that use it call the target's own. Test `AScriptWhoseCodeEntryNameIsTakenInTheTargetIsRefused`. Limit: only for a free script name; with Rename or Replace the compiled functions can still meet functions of the target, which is not checked.

6. A room copied from an older file than the target (Pizza Tower 2022.9 or Undertale Yellow into FRICKBEARS3 2023.11, Pizza Tower into Undertale Yellow) saved a file that did not read back. Found by copying the room alone into FRICKBEARS3 and filling the null fields one by one: the asset layers of the copied room had `LayerAssetsData.ParticleSystems` and `TextItems` equal to null (the source has no particle systems), while a target of 2023.2 or newer, not LTS, writes a pointer for each list (`IsNonLTSVersionAtLeast(2023, 2)`). The room data is then broken in the written file, the reader's version guess changes, and the failure shows up in a later chunk (code entry 1 of CODE, or PSEM "only one element"). Deltarune chapter 3 is LTS-like, writes no such pointer and so was never affected. `CopyResourcesCommand.FillLayerLists` sets every null list of the copied rooms' asset layers to an empty list after the import. Test `ARoomFromAnOlderFileGetsTheLayerListsTheTargetWritesAndSavesReadable` (generated files, target branch set to non-LTS; fails without the fix). All three real pairs re-run clean with dependencies, through save and reopen. Suggestion for the library owner: the room import should leave empty lists instead of null, or the layer writer should write an empty list for a null one. Minimal case: a room with one assets layer whose `ParticleSystems` is null, written into a data file with `Branch = Post2022_0` and version 2023.2 or newer, then read back.

## Not fixed

- Code of a newer GameMaker version can use syntax that an older target cannot compile (FRICKBEARS3, 2023.11, to Pizza Tower, 2022.9: template strings). The copy stops with "The code could not be compiled in the target: ... Cannot use template strings in this GameMaker version", the target is left as it was (checked), but only at apply time. For a copy that pulled in 1582 items this took about 4 minutes and held about 9 GB before it failed. The plan only warns that the target is older. A pre-check of the code against the target version is the fix.
- A room can pull in most of a game (the room `TitleScreenMap` of FRICKBEARS3: 3288 plan entries, 1706 of them refused, 1048 sprites, 474 objects). The plan takes 2 s; the copy is far slower and larger than the 256 MB package limit allows to predict. The test therefore takes the room with the fewest dependencies unless `G3MSTUDIO_COPY_BIGROOMS` is set.
- In a game of GameMaker 2.3 and later the plan lists every function script (`gml_Script_name`) as refused ("part of a larger code entry"): 144 of 320 entries for chapter 2 into Pizza Tower, 310 of 578 for Pizza Tower into chapter 3. The owning script is copied (its code is compiled again and the function entries come back: 225 of them in the first pair), so nothing is lost, but the preview shows a long list of refusals that are not problems. Mapping a function script to the script that owns its parent code in `CopyGraph` would remove them.
- The compile adds the function entries of a copied script on its own, so the script count of the target grows by more than the number of planned scripts (225 more for chapter 2 into Pizza Tower). The preview does not say so.

## Remaining risk

- Rooms from a newer file into an older target (for example FRICKBEARS3 into Pizza Tower) were not run on real files for the layer lists; an older writer ignores extra lists, but a save check on a real pair is open.
- Different versions: only the pairs in the table were run. Nothing was copied into a 2.x or GameMaker Studio 1 file (Undertale is bytecode 16 and every other game here is 17, so those pairs are refused).
- Sounds that live in audiogroup files are refused (4 in the FRICKBEARS3 plans; the sample had none), so a room with such sounds is copied without them.
- Sequences, particle systems, animation curves, shaders and tile sets were only copied as dependencies of the sample (11 shaders in one FRICKBEARS3 plan that was refused for another reason); none had an item of its own in the sample.
- The code compare is a text compare of the decompiler output, not of the bytecode; an instruction level compare was not done.
- The test needs 4 to 10 GB of memory for the larger pairs and 1 to 15 minutes per pair; it is a manual check.
