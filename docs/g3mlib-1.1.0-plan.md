# G3MLib 1.1.0 plan

The final library work list. G3MStudio 1.0.0-beta is built on G3MLib 1.1.0 from NuGet only (ADR-0003). Every item lands on local branches, test first; the full suite and the corpus byte-identity check must pass after each. Release to NuGet only with the owner's yes, then G3MTool CLI 1.3.1 / GUI 1.0.1, then a G3M compatibility check (roadmap phase L3).

Branch chain: `l2/format-gaps` -> `l2/batch2` -> `l2/project-format` -> next work branches from `l2/project-format`.

## Done (verified 2026-10-25, 981 tests)

| Item | Where |
| --- | --- |
| Format gaps 1, 2, 4 to 12, 14; gap 3 not produced by GameMaker | l2/format-gaps, l2/batch2, l2/asset-track (cherry-picked) |
| Out-of-range resource ids of any type kept | l2/batch2 |
| Async read and write with progress and cancellation (work item 1) | l2/batch2 |
| Unknown chunk passthrough (work item 2) | l2/format-gaps |
| Project format ADR-0008: `.g3mproj`, full and delta, Build, deleted resources, migration registry, all resource types; byte-identical rebuild of fixtures, Pizza Tower, Cover Assault; edited room reads back (work item 6) | l2/project-format |

## To do, in order

| # | Item | Why G3MStudio needs it | Acceptance |
| --- | --- | --- | --- |
| A | Export speed: Pizza Tower full export takes 14 minutes | Opening a game as a project must be usable | Full export under 60 s for Pizza Tower; parallel per type; texture export reuses decoded pages |
| B | Thread model: isolate or remove process-wide statics; document that the model is owned by one thread and background jobs use snapshots or a lock (work item 8) | Background search, export and decompile during edits (architecture.md, Threading) | Test that runs decompile and export on background threads while edits happen, no corruption |
| C | Public lexer tokens and structured diagnostics with span and severity (work item 3) | Code editor highlighting, squiggles, Problems panel | Every compiler error has file, line, column, length, severity; token API used by a highlighter test |
| D | Decompiler source map (work item 4) | Click from GML to bytecode, breakpoint-like navigation, disassembly side view | Map from each output line range to instruction offsets; round-trip test on the largest Pizza Tower entry |
| E | Reverse reference index, find usages, rename service (work item 5) | Find usages, safe delete, rename in every editor | Index built in under 2 s for Pizza Tower; rename of a sprite updates objects, rooms, code references; tests per type |
| F | Cancellation for patch, merge, export, import (work item 10) | Every long operation can be cancelled | Cancelled operation leaves inputs untouched; tests per service |
| G | One ScriptGlobals matching IScriptInterface; UTMT bundled-script compatibility run (work item 7) | UTMT scripts run unchanged | Report of the 110 UTMT scripts: compiles, runs on a fixture, or listed reason |
| H | Texture page repacking (work item 9) | Sprite import and edits keep pages compact | Repack after adding sprites; output loads in the runner (manual check) and round-trips |
| I | Remaining project format items: virtual folder editing, strings shortening, variables and functions import, `.yy` interop (F-WS-11) | Explorer folders, full editing, GameMaker project import | Tests per item; a GameMaker sprite and object imported from a `.yyp` build and run |
| J | Benchmarks and README: rerun the full benchmark set on the reference machine, update tables, CHANGELOG entry for 1.1.0 in the library's style | Release requirement (Q3) | No regression above 5% except where a fix requires it, stated in the changelog |
| K | Leftover temp files: find which tool leaves `.data.win._<random>.win` (corpus.md) | Users' game folders stay clean | Root cause fixed in the tool that creates them |

## Item status (2026-10-26)

| Item | State |
| --- | --- |
| A to G, K | Done on `l2/batch5` (status.md, "L2 state"). |
| H | Done on `l2/batch6`: `TexturePageRepacker`. Runner load check still manual. |
| I | Not started: the two library operations have no definition and no `.yy` code exists. Waiting for the owner, OI-052. Virtual folder editing moves to G3MStudio (Q25). |
| J | Done on `l2/batch6` (d46d353) with the caveats in status.md; the changelog entry is a draft for the owner to trim. |

## Release checklist (nothing here has been run)

Order matters. Each step that leaves this PC needs the owner's explicit yes.

Before the release (local, no approval needed):

1. Merge decision: `l2/batch6` is the tip of the chain `format-gaps -> batch2 -> project-format -> batch3 -> batch4 -> batch5 -> batch6`. Compare `git log main..l2/batch6` with this plan, then decide how it lands on `main` (fast-forward is possible because the chain is linear).
2. Version: set `<Version>` in `G3MLib/G3MLib.csproj` from `1.0.1` to `1.1.0`. Rename the CHANGELOG heading `[Unreleased]` (if used) to `[1.1.0] - <release date>`.
3. Notices: `THIRD_PARTY_NOTICES.md` lists Magick.NET `14.17.1` but the project references `14.17.2`; make them match. Check that no new package was added since 1.0.1 (compare `G3MLib.csproj` package references).
4. Full test run, Release configuration: `dotnet test G3MLib.Tests -c Release` must be green three times in a row.
5. Corpus check: set `G3MLIB_EXTERNAL_DATA_FILES` and `G3MLIB_REPACK_DATA_FILES` (path-separator list) and run `dotnet test G3MLib.Tests`; Pizza Tower and Cover Assault full export plus rebuild must stay byte-identical (research/gamemaker-format.md, "Project format acceptance").
6. Manual checks still open (OI-038): the GameMaker runner loads a game whose pages were repacked; UTMT 0.9 opens a UTMT-layout export.
7. Pack and inspect: `dotnet pack G3MLib/G3MLib.csproj -c Release -o <empty folder>`. Open the `.nupkg` as a zip and confirm it holds `lib/net10.0/G3MLib.dll`, `G3MLib.xml`, `LICENSE`, `README.md`, `CHANGELOG.md`, `THIRD_PARTY_NOTICES.md`, `licenses/Apache-2.0.txt`, `licenses/MIT.txt`, `licenses/MPL-2.0.txt`, and `contentFiles/any/any/GameSpecificData/**`. The nuspec must show license file `LICENSE`, repository `https://github.com/y114git/G3MLib.git`, and dependencies Magick.NET-Q8-AnyCPU, SharpZipLib and the two Microsoft.CodeAnalysis packages only.
8. Trace scan of the staged diff, commit messages and the package contents for tool, vendor or model names and for local paths.

Licence notices that must stay true: GPL-3.0-only for the library; UndertaleModLib-derived code under GPL-3.0; Underanalyzer-derived code under MPL-2.0 with file headers kept and `licenses/MPL-2.0.txt` shipped; Apache-2.0 (Magick.NET) and MIT (SharpZipLib, Roslyn scripting) texts shipped. The benchmark project's optional UndertaleModLib reference stays out of the package.

Needs the owner's yes (do not run before):

9. Push `main` and the tag `v1.1.0` to the public repository.
10. `dotnet nuget push <package>.nupkg --api-key <key> --source https://api.nuget.org/v3/index.json`. The key is the owner's; it is never written to a file or a log.
11. GitHub release notes from the CHANGELOG entry.
12. Then G3MTool CLI 1.3.1 and GUI 1.0.1 (package reference to 1.1.0 only, status.md "G3M and G3MTool after the library release"), then the G3M compatibility check (roadmap phase L3).

Owner must approve before step 9: OI-038 (export target and manual checks), OI-039 (higher-level types that stay public), OI-052 (item I scope), and any regression above 5% listed in the CHANGELOG draft notes.

## Scope review (Q25)

Owner decision (questions.md Q25): G3MLib stays low level. Single operations with batch-execution optimizations are G3MLib. Workflows that combine operations belong in G3MStudio. Nothing built is deleted: "move to G3MStudio" items stay until G3MStudio takes them over.

| Item | Scope | Action |
| --- | --- | --- |
| Format gaps, out-of-range ids | G3MLib (primitive) | Keep |
| Async read and write with progress and cancellation (1) | G3MLib (primitive) | Keep |
| Unknown chunk passthrough (2) | G3MLib (primitive) | Keep |
| Project format ADR-0008: per-resource serialize and deserialize | G3MLib (primitive) | Keep |
| Project format ADR-0008: folder enumeration, full and delta modes, Build, deleted resources, migration registry, UTMT 0.9 conversion | move to G3MStudio (workflow), keep until G3MStudio takes it over | Re-review table in ADR-0008 |
| A Export speed | G3MLib (primitive) for per-resource export and parallel batch; whole-game folder export flow moves | Keep the parallel and decode-reuse work; move the flow |
| B Thread model | G3MLib (primitive) | Keep |
| C Lexer tokens and diagnostics | G3MLib (primitive) | Keep |
| D Decompiler source map | G3MLib (primitive) | Keep |
| E Reverse reference index | G3MLib (primitive) | Keep |
| E Rename primitives | G3MLib (primitive) | Keep; preview and undo are G3MStudio |
| F Cancellation | G3MLib (primitive) | Keep |
| G ScriptGlobals and script host | G3MLib (primitive) | Keep |
| H Texture page repacking | G3MLib (primitive) | Keep |
| I Virtual folder editing | move to G3MStudio (workflow), keep until G3MStudio takes it over | Explorer state belongs to the workspace |
| I Strings shortening, variables and functions import | G3MLib (primitive) | Keep |
| I `.yy` interop | G3MLib (primitive) per-resource converters; import orchestration moves | Keep converters |
| J Benchmarks and README | G3MLib | Keep |
| K Leftover temp files | G3MLib / G3MTool | Keep |

Not G3MLib work at all: change tracking, undo, save pipeline, Git, Build and Export workflows (architecture.md, "G3MLib scope").

## Not in 1.1.0

Debugger protocol, YYC code (native, not decompilable), new GameMaker runtimes after LTS 2026 (handled by unknown chunk passthrough and a 1.1.x release).
