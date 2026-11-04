# Performance pass (2026-11)

Date: 2026-11-04. Status: measured on Windows x64, Release build, one machine. Numbers vary by 20 to 40% between runs (garbage collections, JIT); the tables give the typical run.

Goal (owner): scrolling, navigating, expanding, switching tabs, opening files, filtering and every edit feel fast on the biggest games. Budgets used here: 16 ms per frame, 50 ms per action.

## Method

- Edits on real files: `PerfRealFilesTests` (`EditsOnRealFiles`, `OpenTimesOnRealFiles`) opens the file with `WorkspaceService` and times each command on the calling thread, the way the window does.
- UI timings: `PerfRealUiTests` (`UiOnRealFiles`, `UiOnSyntheticFile`) opens the file in the headless main window with real Skia drawing. A step is the action plus the queued jobs (layout included) on the UI thread; the render tick that follows is reported as "headless frame". The harness does not read the picture back, because that copies 5 MB per frame and the garbage skews the timings.
- Both are skipped unless `G3MSTUDIO_PERF` is set. `G3MSTUDIO_PERF_FILES` lists data files separated by semicolons; `G3MSTUDIO_PERF_SYNTHETIC` gives the entry count of a generated file.
- Files: Pizza Tower (88 MB), Undertale Yellow 1.3.1 (152 MB, biggest), Undertale (60 MB), FRICKBEARS3, DELTARUNE launcher and chapters 1 to 4.
- Not measured by this pass: the existing `EditorBenchmarkTests` (code scroll) and `RoomBenchmarkTests` (room pan) were run again as they are.

## Findings and changes

| Finding | Before | After | Change |
| --- | --- | --- | --- |
| Rename an object or sprite, on the UI thread | 141 to 335 ms (each rename built the reference index) | 4 to 32 ms (Pizza Tower first call 63 ms, JIT) | `ReferenceCache`: one index per model, kept across renames, dropped by any other edit. The rename dialog builds it in the background while it is open. |
| Delete a resource with uses (Edit part) | 262 to 506 ms (a second index in `ClearReferencesCommand`) | 3 to 59 ms | Same cache; the usage lookup that asks the question builds the index off the UI thread and the edit reuses it. |
| Font replace, edit under the model lock | 499 to 1849 ms | 4 to 24 ms | `FontReplaceCommand.Prepare` draws the glyphs and builds the new texture page under the read lock on a pool thread (0.65 to 1.5 s there); `Apply` only swaps the page in. Journal replay still draws inside `Apply`. |
| Edit a property with 3 tabs open (Yellow) | 62 to 69 ms mean | 19 ms mean (Pizza Tower 45 ms) | Explorer name scan skipped for edits that cannot rename (`EditCommand.ChangesNames`), allocation free name comparison when it runs, object event list kept when nothing changed, tiered PGO off. |
| Explorer expand or collapse of the biggest type (68k to 124k rows) | 42 to 52 ms mean, p95 75 ms | 3 ms mean, p95 10 ms | `ExplorerRowList` reports an opened or closed type as one insertion or removal, so the list keeps its items and the scroll position. |
| Explorer scroll, 7 rows per frame | 9 to 16 ms mean | 5 to 6 ms mean | Row tips are reused by recycled list items. |
| Explorer filter typing, one key | 27 ms mean, p95 55 ms | 14 to 21 ms mean, p95 33 ms | Section counts kept, no recount per redraw. The list is still replaced on each key. |
| Show a hidden tab again | `CycleTab` 70 ms, then 50 ms of jobs | 14 ms, then 14 ms of jobs | Property tab built its rows twice on attach; code tab decompiled and replaced its text on every attach (also lost caret and scroll). Both keep what they show unless an edit came meanwhile. |
| Edit while the Strings window is open | 384 ms mean, up to 558 ms | 25 to 38 ms | The window rebuilt the reference index at once after each edit, and the next edit waited for its read lock. It now builds after edits pause for 1.5 s. |
| Strings window, 45k strings | open 134 ms, typing 27 ms mean | open 60 ms, typing 14 ms | Ordinal comparison, kind texts and resource names reused. |
| Code search progress | 12,000 UI jobs per search | about 190 | One report per 64 entries. |
| Memory per open and close of a tab, same sprite, room or code entry | sprite +7.6 MB, room +7.0 MB, code +3.9 MB per round | 0 to 0.2 MB | The window kept the view of every closed tab in three lists; `PruneViews` drops them on each dock change. Origin markers of removed sprite frames are cleared too. |
| Garbage collection pauses | one 20 to 45 ms collection per tab or edit (default youngest generation budget 12 MB, heap 300 to 700 MB) | most steps have none | The closed-tab leak fix. A `Gen0MaxBudget` runtimeconfig entry was tried and dropped: the runtime reads that setting from the environment only. |
| Tiered PGO | | 10 to 40% less on edits and typing in the harness | `TieredPGO` off in the program. |

## Opening a file

Milliseconds, first run of the process (the second run is within 10% except where noted). "Read" is `GameMakerIO.ReadAsync` alone; `OpenAsync` is the workspace service (read, identity hash, journal); the Explorer column is `ExplorerModel.Load`.

| File | Size | Entries | Read | Identity hash | OpenAsync | Explorer model | Window: file chosen to first frame |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| DELTARUNE launcher | 2 MB | 2,863 | 61 | 6 | 13 | 0 | not run |
| DELTARUNE chapter 1 | 12 MB | 28,613 | 224 | 26 | 250 | 2 | not run |
| FRICKBEARS3 | 29 MB | 76,009 | 580 | 63 | 485 | 16 | not run |
| Undertale | 60 MB | 100,280 | 982 | 119 | 859 | 30 | not run |
| DELTARUNE chapter 2 | 63 MB | 97,280 | 755 | 150 | 763 | 64 | not run |
| Pizza Tower | 88 MB | 117,928 | 828 | 213 | 795 | 63 | 1,495 |
| DELTARUNE chapter 3 | 120 MB | 135,809 | 1,183 | 244 | 1,027 | 67 | not run |
| DELTARUNE chapter 4 | 125 MB | 159,627 | 1,152 | 224 | 943 | 80 | not run |
| Undertale Yellow | 152 MB | 123,758 | 1,104 | 327 | 1,143 | 14 | 1,761 to 1,996 |

- `OpenAsync` is the same as the bare read: Studio adds no measurable time to G3MLib (the identity hash runs beside the read). Open time is G3MLib's read.
- The window adds 350 to 500 ms after the workspace is ready: `ShowWorkspace` takes about 50 ms (Explorer load 27, project 15, history 5), the rest is the first layout and the JIT of the editor code, because the program is published without ReadyToRun (commit 2b31148). A tiny file takes 510 ms the same way.
- Not changed: nothing to fix on the Studio side that is measurable above the read itself.

## Not fixed

- Opening a tab for the first time in a session: 60 to 440 ms on the UI thread, mostly JIT of that editor type (sprite 420 to 960 ms, room 140 to 440 ms, sequence 170 ms). Warm: object 43 to 65 ms, script 31 to 48 ms, room 70 to 200 ms, sprite with 182 frames 180 to 250 ms. A property list builds the first 20 rows at once and the others near the view; this did not move the numbers in the harness, so the cost is elsewhere (the dock and the controls of the header views). A lazy strip of sprite frames was tried and reverted: no gain.
- Switching between open tabs: mean 45 to 58 ms with 15 to 20 tabs, p95 about 150 ms (room and sprite tabs). A switch to a text-only tab costs 5 to 8 ms, so the dock is not the cost; the content is measured and rendered again after it was detached.
- `EditCodeCommand`: 30 to 100 ms (compile and import under the write lock). Needs the library to compile without mutating the model.
- Rename right after a property edit when the dialog was not open for long: the first rename builds the index (about 250 ms) on the UI thread if the background build has not finished.
- `ReferenceIndex.Build` takes 150 to 440 ms in G3MLib 1.1.2 on these files (parallel already). An incremental update after property edits would remove the rebuild after every edit; `ReferenceIndex.Update(owners)` exists but needs the set of changed owners per command. Not changed in G3MLib.
- Filter typing replaces the whole list, so each key costs 14 to 30 ms; narrowing the previous result instead of scanning all entries would save the scan (4 to 8 ms).
- Opening a data file reads 0.5 to 1.2 s in G3MLib; a partial or lazy read would need library work.
- A synthetic file of 100,000 objects and scripts did not finish in the test run and was stopped (not investigated); the 100k string case was measured with the real games (31k to 64k strings) and the Usages panel with 50,000 generated hits.
