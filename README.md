# G3MLib

G3MLib reads, writes, analyzes, decompiles, and modifies GameMaker data files.

The `G3MLib.Analyzer` namespaces provide GML compilation, decompilation, bytecode analysis, and game-specific macro resolution.

The `G3MLib.DataFile` namespaces provide the data-file model, I/O, project serialization, and scripting APIs.

The `G3MLib.Modding` namespaces provide patch creation and application, patch merging, diff reports, resource import/export, integrity repair, caching, and xdelta execution. Hosts supply their own UI through the library's log and progress events.

For `ProjectContext` scripts, set `ScriptMessageHandler` and `ScriptWarningHandler` to receive text output. Interactive dialogs and opening URLs require a host implementation of `IScriptInterface`; the default context does not launch applications.

For xdelta operations, configure the executable supplied by your application before invoking patch services:

```csharp
XDeltaService.DefaultExecutablePath = xdeltaPath;
```

For concurrent host operations, use `LogService.BeginScope(messageCallback, progressCallback)` to route each operation's messages separately and `XDeltaService.UseExecutable(xdeltaPath)` to override its executable without changing the process default. Dispose both scopes after the awaited operation completes.

`G3MProjectService.Export` takes an `IProgress<BatchProgress>` (resources done, total, name of the resource just written) and a `CancellationToken`. A cancelled export leaves the project folder as it was. `G3MProjectService.Update` writes only the resources you name into an existing project folder, with the same files a full export writes for them, so a rename needs the renamed resource and the resources that refer to it, not a full export.

Other APIs added in 1.1.0: `GameMakerIO.ReadAsync` and `WriteAsync` with progress and cancellation; `ReferenceIndex` and `RenameService` for find usages, unreferenced assets and renames with undo; `GmlTokenizer` and compiler diagnostics with spans; `CodeImportGroup` to compile code into a data file with a result per operation; `TexturePageRepacker` and `PageEditor` for texture pages; `FrameExporter` and `AudioGroupFile` for frames and audio groups; `DataLookup` and `DataRepair`; `ScriptRunner` and `ScriptGlobals` for scripts; and readers and writers for GameMaker `.yy` files. A data model that several threads touch is guarded with `ModelLock`; the rules are in its documentation.

## Install

G3MLib targets .NET 10.

```shell
dotnet add package G3MLib
```

## Benchmarks

Measured on 2026-11-18 (G3MLib 1.1.4) using the `data.win` of Pizza Tower (92,440,092 bytes), Windows x64, .NET 10 Release, Intel Core Ultra 7 155H, and 31 GiB RAM. Tiered compilation was disabled for both libraries with `DOTNET_TieredCompilation=0`. Times are medians, with minimum–maximum in parentheses. MiB columns measure total managed allocations across all threads, not peak memory or native ImageMagick allocations.

The machine was not idle: other programs and build processes ran during every measurement, so absolute times vary between runs. Two earlier `--compare` runs on the same day were noisier (the complete read median was 1,633 ms and 1,478 ms for G3MLib) and were discarded; the table uses the third run, the quietest. Rows from the `--full` run show the same effect: sprite export (36.1 s), sprite import (238.0 s, maximum 534.6 s) and texture page repack (20.7–29.9 s) were slower than in earlier runs of similar code on a quiet machine. Judge a row by its minimum–maximum range, not its median alone, and rerun on your own hardware.

The six directly compared operations (complete read, general-information read, write, parse, compile, decompile) come from one `--compare` run: seven measured batches after three warm-up batches, alternating library order each round in the same process. Each batch contains 64 parsing or compilation operations, 16 general-information reads, or one full read, write, or decompilation. Every other row comes from one `--full` run on the same file (three measured runs after one warm-up). Garbage collection runs before each measurement, outside the timer.

Comparison sources: [UndertaleModLib f43e12c](https://github.com/UnderminersTeam/UndertaleModTool/tree/f43e12c445c37d50dc6244caa12ccab232983f3f) and [Underanalyzer 4ff50a8](https://github.com/UnderminersTeam/Underanalyzer/tree/4ff50a866b4c1a7acee8cebe6a56d6a48709b453). Every ratio uses one rule: the compared library's median divided by G3MLib's median, printed with two decimals. A ratio above 1 means G3MLib's median was lower. The verdict is computed from the ranges: when the minimum–maximum ranges of the two libraries overlap it reads "no clear difference", otherwise it names the library whose whole range is lower. `--compare --markdown` prints these rows. A dash means no equivalent operation was measured, not that the other library cannot perform it.

G3MLib is not faster in every row. Writing a complete data file had a ratio of 0.91, so UndertaleModLib's median was lower; parsing (1.01) and compiling (1.03) GML source were equal; the ranges overlap in all three, so there is no clear difference. G3MLib allocated slightly more than Underanalyzer while decompiling (23.874 MiB against 22.388 MiB). In this run the ranges favor G3MLib for the complete read, the general-information read and decompilation, but in one of the two discarded noisier runs the complete read ranges overlapped and in the other the decompile ranges overlapped, so treat the size of those gaps as uncertain.

| Operation | G3MLib, ms | UndertaleModLib, ms | Underanalyzer, ms | Time ratio | Verdict | G3MLib, MiB | UndertaleModLib, MiB | Underanalyzer, MiB |
| --- | ---: | ---: | ---: | ---: | --- | ---: | ---: | ---: |
| Read complete data file | 771.806 (619.797–1,002.708) | 1,321.198 (1,032.670–1,425.214) | — | 1.71× | G3MLib faster | 236.615 | 250.662 | — |
| Read general information only | 0.429 (0.293–0.598) | 2.508 (2.214–3.451) | — | 5.85× | G3MLib faster | 0.368 | 12.949 | — |
| Write complete data file to memory | 453.024 (386.788–553.884) | 413.966 (372.201–495.514) | — | 0.91× | no clear difference | 352.153 | 352.232 | — |
| Hash all resources | 1,594.632 (1,572.584–1,746.612) | — | — | — | — | 6,165.883 | — | — |
| Compute MD5 synchronously | 182.465 (178.671–186.974) | — | — | — | — | 1.003 | — | — |
| Compute MD5 asynchronously | 181.807 (179.175–190.740) | — | — | — | — | 1.004 | — | — |
| Build data-info snapshot | 1.335 (1.318–1.694) | — | — | — | — | 0.849 | — | — |
| Write analysis cache | 21.244 (20.693–21.660) | — | — | — | — | 0.081 | — | — |
| Read analysis cache | 190.572 (187.168–193.226) | — | — | — | — | 9.221 | — | — |
| Export all sprite metadata and PNG frames | 36,058.412 (34,410.956–37,109.832) | — | — | — | — | 13,777.235 | — | — |
| Parse 200-line GML source | 0.165 (0.141–0.205) | — | 0.167 (0.133–0.272) | 1.01× | no clear difference | 0.269 | — | 0.268 |
| Compile 200-line GML source | 0.187 (0.162–0.451) | — | 0.192 (0.149–0.256) | 1.03× | no clear difference | 0.323 | — | 0.322 |
| Decompile largest code entry (9,283 instructions) | 13.690 (11.112–15.069) | — | 22.379 (19.801–44.224) | 1.63× | G3MLib faster | 23.874 | — | 22.388 |
| Validate patch | 210.379 (210.129–212.342) | — | — | — | — | 1.059 | — | — |
| Create patch | 5,853.516 (5,120.109–6,282.336) | — | — | — | — | 12,775.588 | — | — |
| Apply patch | 4,550.636 (4,508.173–6,568.798) | — | — | — | — | 857.131 | — | — |
| Create standard diff report | 12,398.520 (12,235.615–12,958.360) | — | — | — | — | 15,038.803 | — | — |
| Create full diff report | 13,553.501 (11,870.451–15,020.887) | — | — | — | — | 15,043.846 | — | — |
| Merge 2 independent patches | 1,383.585 (1,301.506–1,394.227) | — | — | — | — | 303.187 | — | — |
| Merge 4 independent patches | 1,368.982 (1,302.299–1,420.367) | — | — | — | — | 305.278 | — | — |
| Merge 8 independent patches | 1,348.373 (1,307.214–1,466.832) | — | — | — | — | 309.432 | — | — |
| Merge and apply 8 independent patches | 4,879.963 (4,561.827–5,768.629) | — | — | — | — | 1,200.641 | — | — |
| Build reference index | 163.405 (161.066–164.681) | — | — | — | — | 187.615 | — | — |
| Rename 100 sprites (plan, apply, undo) | 65.121 (63.337–68.873) | — | — | — | — | 2.016 | — | — |
| Plan texture page repack | 29,164.236 (20,651.086–29,901.123) | — | — | — | — | 34,280.246 | — | — |
| Import all sprites into a new data file | 237,990.550 (236,314.279–534,633.187) | — | — | — | — | 11,003.523 | — | — |

The repack row only computes a plan and decodes every texture page of the game; the sprite import row imports the 31,388 exported PNG frames of the sprite export row into an empty data file, including packing and mask generation.

Reads reopen the file with a warm filesystem cache. Writes serialize an already loaded model to memory. The comparison checks room, code, and string counts, plus the selected code entry's instruction count. The upstream partial reader accepts its expected unread-tail warning; other warnings fail the run.

GML input contains 200 lines, from `var value0 = 0 + 1;` through `var value199 = 199 + 1;`, with a fresh mock game context per operation. Parsing includes tokenization and parse-tree post-processing; compilation also generates bytecode, without linking. Decompilation includes a fresh global context and function lookup initialization: G3MLib's context for its analyzer, UndertaleModLib's context for Underanalyzer. These timings include that integration work. The ranges overlap for parsing and compilation, so those medians show no speed difference between the libraries.

Hashing covers every resource. Cache writes serialize precomputed hashes, resource names, ordering, and the information snapshot; cache reads include source validation. Sprite export writes metadata and all PNG frames to a temporary directory, reusing output paths after warm-up. The harness checks the exported frame count. Directory cleanup is outside the timer.

Patch creation compares the input with a copy whose first room is one pixel wider. Validation checks that patch against the input; application writes a new data file. Diff reports compare the same pair without a supplied analysis cache. Merge operations combine patches changing 2, 4, or 8 different rooms. Application includes staged output validation and rollback support. The harness reads the applied and merged files and checks the room widths. These rows measure room-only changes.

### Reproduce

Set `DOTNET_TieredCompilation=0` in the benchmark process environment to match these measurements. For read/write, resource hashing, and MD5 across one or more files:

```shell
dotnet run --project G3MLib.Benchmarks -c Release -- path/to/data.win
```

For all operations, supply a data file with at least eight rooms. Sprite export and patch tests require temporary disk space and several minutes:

```shell
dotnet run --project G3MLib.Benchmarks -c Release -- --full path/to/data.win
```

For the direct comparison, check out the UndertaleModTool revision above with its Underanalyzer submodule, then pass its source directory:

```shell
dotnet run --project G3MLib.Benchmarks -c Release -p:UndertaleModToolPath=/path/to/UndertaleModTool -- --compare path/to/data.win
```

The optional upstream reference belongs only to the benchmark project. G3MLib has no dependency on UndertaleModLib or an application host. Game assets are not distributed with the benchmark. Results depend on input, runtime, filesystem cache, native codecs, power settings, and system load.

## Script compatibility

`G3MLib.ScriptCompat` compiles every `.csx` script under a folder against the G3MLib script host and runs those that need no dialog on a copy of a data file, then writes a report:

```shell
dotnet run --project G3MLib.ScriptCompat -c Release -- <scripts folder> <data file> <work folder> <report.md>
```

## License

G3MLib is GPL-3.0-only. It includes some UndertaleModLib-derived code under GPL-3.0 and Underanalyzer-derived code under MPL-2.0. See [LICENSE](LICENSE) and the license notices in the repository.
