# GameMaker format investigation (phase L1)

Log of format questions, the method used, and answers in our own words. Black-box first; any decompilation follows legal-re-policy.md and is noted per entry.

## Method: headless builds

GameMaker's build tool Igor compiles a project to a data file without the IDE:

```
Igor.exe -j=8 --project=<project>.yyp --rp=<runtime folder> --runtime=VM
         --user=<GameMaker user folder> --cache=<dir> --temp=<dir> --of=<out>.win
         -- windows Compile
```

- Igor lives in `<runtime>/bin/igor/windows/x64/Igor.exe`; the runtime folder is under the GameMaker cache (`runtimes/runtime-<version>`). The user folder holds the licence and is under the IDE's roaming settings.
- The TEST project (one sprite, one object, one room) built in about 0.4 s into a 4,428-byte data file. Exit code 0.
- Igor logs each chunk as it writes it and a summary line of asset counts (`sp=1,au=0,...,ob=1,ro=1,ma=6`), which gives the chunk order for free.
- Other workers include PackageZip, PackageNsis, Arm64 packages, Run, Clean. `--runtime=YYC` builds native code (no CODE chunk content).

Build notes from the first real projects:

- Projects that use prefabs (filters and effects from the Prefab Library) need `--pf=<prefab library folder>`; GameMaker keeps it under its ProgramData folder as `Prefabs/`. Without it the asset compiler fails with "Missing Prefab = io.gamemaker.gm_filter_...".
- The output file is named after the project ("Cover Assault.win"), not after `--of`, when the project name differs.
- A sound asset without an audio file makes Igor exit with code 1, but the data file is still written (the sound is left out).
- Chunk order written by LTS 2026 for a project with every resource type: GEN8 OPTN LANG EXTN SOND AGRP SPRT BGND PATH SCPT GLOB SHDR FONT TMLN OBJT FEDS ACRV SEQN TAGS ROOM UILR DAFL EMBI PSEM PSYS TPAG TGIN CODE VARI FUNC FEAT STRG TXTR AUDO (34 chunks).

This means L1 can generate test projects as files (the `.yyp` and `.yy` formats are JSON), build them in batch, and check every output with G3MLib, with no IDE automation.

## Questions

| # | Question | Method | Answer | Status |
| --- | --- | --- | --- | --- |
| 1 | G3MLib reports "GM 2024.14", bytecode 17, for a file built with runtime 2026.0.0.23. Did the format stop changing at 2024.14, or does G3MLib miss newer markers? | Code comparison with UTMT (gamemaker-format-coverage.md, section 2) and fixture builds. | Detection works from markers: UILR gives 2024.13, the AGRP path field 2024.14; the next rule (2026.1) needs OBJT without the Managed field, which LTS 2026 still writes. Neither library has a marker between 2024.14 and 2026.1, and every LTS 2026 fixture round-trips byte-identical, so the layout matches. Display should say "2024.14 or later". | answered |
| 2 | Which chunks and fields does each resource feature produce (every layer type, event type, option)? | One test project per feature; build; byte diff against a base build (gamemaker-fixtures.md). | 37 variants done (events, texture formats, sound compression, room layers, sprite options, tile animation); more listed there. | in progress |
| 3 | Does G3MLib round-trip every built test file byte-identically? | Read and write with G3MLib 1.0.1 (G3MTool CLI `execute` with an empty script), compare bytes. Corpus run 2026-10-25. | LTS 2026 builds: TEST with one asset of every type (29,772 bytes, 34 chunks) and the Cover Assault template game (74,071,320 bytes: 163 sprites, 57 sounds, 396 code entries, 83 objects, UI layers, effects) are byte-identical; Cover Assault reads and writes in 1.4 s through the CLI. Corpus: 10 of 11 identical: TEST build, Deltarune launcher and chapters 1 to 5, Pizza Tower, Undertale Yellow, FRICKBEARS3. Undertale differs in 2 bytes, see question 4. Read plus write takes 1.1 s (3 MB) to 9.3 s (165 MB) through the CLI, including process start. | answered for the corpus; test projects pending |
| 4 | Why does Undertale (bytecode 16) not round-trip? | Byte compare and chunk map. | Bytes at file offsets 72 and 73 (GEN8 data offset 56, the version build field) change from `03 06` (build 1539) to `39 05` (build 1337; an earlier note said 1351 because `cmp -l` prints octal). Cause: G3MLib unserializes the GEN8 version into a struct copy returned by a property, so the stored version is lost on read and the default 1.0.0.1337 is written (gamemaker-format-coverage.md, gap 1). UTMT keeps it in a public field. The same bug breaks GMS1 padding, extension product IDs for GMS 1.4 builds 1773 and later, and GMS 2.0 to 2.2.0 detection. | answered, fix first in L2 |
| 5 | Deltarune reports "GM 2022.0 (raw: 2023.6.0.0)". Is the detected version right for a 2022 LTS game, and how do LTS builds mark themselves? | Code comparison; LTS 2022 builds still to do. | 2023.6 comes from the FONT layout check; the branch is set to LTS at the end of FORM when no non-LTS marker was seen; "2022.0" is fixed text for any LTS branch. A wording fix, plus gap 5 (G3MLib gates 2024.6 and 2024.11 fields on non-LTS where UTMT does not). LTS 2022 builds (runtime 2022.0.3.99) report the same label and all 36 fixtures round-trip byte-identical. | answered |
| 6 | What is the layout of a GMAssetTrack in SEQN, and can a GameMaker build contain one? | UTMT source (it throws "not implemented" for this track); then decompiled the asset compiler (GMAssetCompiler.dll, LTS 2026 runtime 2026.0.0.23) and read the track model mapping and keyframe serialization. | Neither source gives a layout. The compiler has no track named GMAssetTrack: the compiled model name is the name of the IDE track type, and the only asset-reference types are graphic, instance, sequence, particle and audio tracks, which compile under their own names. A compiled track is: model name, name, builtin name, traits, creation flag, counts of tags, owned resources and sub-tracks, those lists, then a keyframe store (count; per keyframe key, length, stretch, disabled, channel count; per channel an id and the payload, aligned to 4). Asset-reference payloads are one int, the exported index of a sprite, sound, sequence, object or particle system. A GMAssetTrack is probably a name used by older builds; no current build emits it, so G3MLib keeps throwing with a clear message. | answered; layout of GMAssetTrack itself unknown |

## Components that write data files (for reference)

- `GMAssetCompiler` (.NET) under `<runtime>/bin/assetcompiler/windows/<arch>/` writes the chunks. Decompiling it is allowed by our policy only for a specific format question the black-box method cannot answer.

## Project format acceptance

Checks from ADR 0008 run against the project service on branch `l2/project-format` (head 1d53610). Inputs were read only. A throwaway runner outside the repository exported each file, exported it a second time, rebuilt from the first export and compared bytes. No failure was found, so no library change was needed. Times are wall clock on the development machine, one run, library built in debug configuration.

### Check 1: export, rebuild, byte-identical

| File | Resources | Result | Export | Rebuild |
|---|---|---|---|---|
| Undertale | 11899 | identical | 31.6 s | 9.2 s |
| Deltarune launcher | 394 | identical | 1.7 s | 0.3 s |
| Deltarune chapter 1 | 3870 | identical | 17.4 s | 3.0 s |
| Deltarune chapter 2 | 13060 | identical | 82.2 s | 16.7 s |
| Deltarune chapter 3 | 17961 | identical | 107.0 s | 27.4 s |
| Deltarune chapter 4 | 21819 | identical | 132.5 s | 30.2 s |
| Deltarune chapter 5 | 30869 | identical | 110.1 s | 23.6 s |
| Pizza Tower | 20279 | identical | 110.5 s | 18.0 s |
| Undertale Yellow 1.3.1 | 21754 | identical | 108.3 s | 17.4 s |
| FRICKBEARS3 | 12360 | identical | 103.3 s | 8.9 s |
| 10 igor fixtures (base, events_all and 8 variants of textures, sprites, sounds, rooms, tilesets) | 27 to 43 | identical | 0.1 to 0.2 s | 0.02 to 0.04 s |
| Fixtures 2022.x and 2026 (base, events_all) | 23 to 43 | identical | 0.1 to 0.15 s | 0.02 to 0.03 s |

Every rebuild of an untouched project reported all resources as unchanged and no warnings.

### Check 2: export twice, no file differences

| File | Result |
|---|---|
| All files in check 1 | The two project trees have the same SHA-256 over sorted relative paths and file hashes. |

### Check 3: edit one resource, rebuild, reopen

| File | Edit | Result |
|---|---|---|
| Deltarune chapter 1 | one appended GML line in a global script, one pixel in a sprite frame PNG | Rebuild imported 2 resources and left 3868 unchanged. Exporting the rebuilt file again differs from the first export in exactly four files: the edited script (`.gml` and `.asm`) and the edited sprite (`.json` for the moved texture rectangle, and the PNG). The GML marker is present after reopening. |
| Fixture events_all | same two edits | Same result: the script `.asm`, the sprite `.json` and the sprite PNG are the only differences. |

The sound files of chapter 1 are read from audio files next to the original data file, so a rebuilt file written elsewhere exports without them. This is how external audio works, not a project format fault. Chunk by chunk comparison of the rebuilt file differs in most chunks because every offset moves when one code entry changes size, so the comparison was done on the exported trees instead.

### Check 4: UTMT 0.9 projects

| Input | Result |
|---|---|
| UTMT sample projects | None exist in the UTMT source checkout (no project manifests). |
| Fixture events_all, written with the existing UTMT layout export, then opened and rebuilt | Opens and rebuilds without warnings: 0.65 s to export, 0.27 s to open and rebuild. The rebuilt file is not byte-identical (the UTMT layout reimports assets and repacks textures). Exporting it again differs from the original export in 3 of 59 files: sprite texture rectangles, event action fields on one object (`useApplyTo`, `argumentCount`) and `effectEnabled` on room layers. |

The UTMT layout is the compatibility path, not the native format, so these differences are listed as open rather than fixed. A UTMT export opening in UTMT 0.9 itself is the manual check and was not run.

### Check 5: timing

| Project | Resources | Export | Project file parse | Rebuild including base file read |
|---|---|---|---|---|
| Pizza Tower | 20279 | 110.5 s | 37 ms | 18.0 s |
| Deltarune chapter 5 | 30869 | 110.1 s | 67 ms | 23.6 s |

Open to usable: the project file with the full resource list (the explorer data) parses in 67 ms for the largest project (30869 resources), well under the 2 s target. The library has no separate open call yet, so this measures the manifest only; resource folders are read when a resource is opened or the project is rebuilt.

### Fixes made

None. No acceptance check failed.

### Left open

- Export speed: about 110 s for the largest chapter, dominated by writing about 30000 resource folders. Fine for a one-time export, slow for a full re-export.
- UTMT layout loses event action fields and room layer effect flags on reimport (check 4).
- No library entry point opens a project and returns the explorer model; a service for that is needed before the 2 s target can be measured end to end.
- A UTMT export opening in UTMT 0.9 was not tested.

### Round 2: export speed, open API, UTMT layout, all fixture builds

Branch `l2/batch3` on top of `l2/project-format`, head 56d5fb9, 857 tests passing (851 at the start). Inputs were read only. Times are wall clock on the development machine (22 logical cores, Windows Defender real-time scanning on), release configuration, one run each. The checks 1 and 2 numbers in the first round were taken in the debug configuration, so the before column below was re-measured on the old code in release.

#### Export profile and changes

Where the 101.8 s of the Pizza Tower export went (stage times from a stopwatch per resource type, then per phase):

| Phase | Time |
|---|---|
| Sprites (texture page decode, frame crop, PNG encode, file writes) | 39 s |
| Code entries (decompile, disassemble) | 8 s |
| Tilesets, scripts, rooms, objects, fonts | 5 s |
| Normalizing 32840 text files, one at a time | 27 s |
| Listing and hashing 64559 files, one at a time | 21 s |

Causes found: texture pages were decoded while holding one lock, and every frame crop took the same lock, so the parallel sprite loop ran largely in sequence; text normalization and resource hashing re-read every file serially; resource types ran one after another; frames that share a page rectangle were encoded again for each sprite (31388 frames, 22402 distinct rectangles in Pizza Tower). Content hashing found almost no extra duplicates (21782 distinct images among the 22402 rectangles), so it was not added.

| Commit | Change |
|---|---|
| 406f865 | Pages decode in parallel, one lazy decode per page. Frames are cut from the decoded raw page without a shared lock. |
| c5ff2ce | Normalizing and hashing run in one parallel pass over the resource units, one read per file. |
| 4282863 | A frame PNG is encoded once per page rectangle and reused. |
| 2df3e33 | Resource types export concurrently into separate staging folders; warnings stay in type order. |

Timings (export, including reading the data file):

| File | Before | After |
|---|---|---|
| Pizza Tower (20279 resources) | 101.8 s | 35.1 s |
| Deltarune chapter 5 (30869 resources) | 124.3 s | 33.9 s |

The 15 s target was not reached. What is left on Pizza Tower: sprites about 23 s and the final normalize and hash pass about 8 s. The PNG encoder is the long pole: encoding scales to about 4 times a single thread on 22 threads, and creating the roughly 65000 small files costs about 0.25 ms each even with parallel writers (30000 small files took 7 s in a bare test, 12 s when written into one folder). The file contents must stay byte-identical, so the encoder settings cannot change; the PNG bytes of the encoder in use were not reproducible with the zlib in the runtime (no compression level or strategy matched), so a managed encoder is not an option without changing output. A rebuild still hashes every unit one at a time (17.5 s and 22.4 s) and was left as is.

Byte identity: the exported tree hash (SHA-256 over sorted relative paths and file hashes) is the same before and after for both files (Pizza Tower BB4FAADC2C4C8685, chapter 5 291E9E763D89F9D8, repeated runs). Checks 1 and 2 were rerun on the final build:

| File | Rebuild byte-identical | Export twice identical | Export | Rebuild | Unchanged on rebuild |
|---|---|---|---|---|---|
| Pizza Tower | yes | yes | 35.1 s | 17.5 s | 20279 of 20279 |
| Deltarune chapter 5 | yes | yes | 33.9 s | 22.4 s | 30869 of 30869 |

#### Open API (commit 9bd168e)

`G3MProjectService.OpenAsync(projectFile, progress, cancellationToken)` returns a `G3MProject`: name, format version, mode, base file identity, the resource list (name, project path, type, hash), `OfType`, `Find`, `GetPath`, resource order, virtual folders and the audio and texture groups. Resource folders are not read. Progress and cancellation work like `GameMakerIO.ReadAsync`; a project newer than the library or a missing file throws `ProjectException`.

| Project | Resources | Open to usable |
|---|---|---|
| Deltarune chapter 5 | 30869 | 153 to 225 ms (three runs, the first includes JIT), target 2 s |

#### UTMT layout (commit 56d5fb9)

The UTMT 0.9 project serializers do not store the fields in question. Events are saved as one code action with fixed values (library 1, id 603, kind 7, apply-to on, one argument), room layers have no effect enabled flag, and sprites keep only the frame images, which are repacked into new texture rectangles on import. The layout therefore stays identical to UTMT. Instead, the UTMT-layout export adds a warning when it writes objects, rooms or sprites, listing what the layout cannot hold: event action fields, room layer effect flags and sprite texture page rectangles. The native project format keeps all three.

#### All fixture builds

The acceptance runner (export, export again, rebuild, byte compare) was run on every fixture build, not only the earlier 14:

| Set | Builds | Rebuild byte-identical | Export twice identical | Warnings |
|---|---|---|---|---|
| First fixture set (base, 16 event variants, events_all, 4 room, 4 sound, 6 sprite, 3 texture, tileset animation) | 36 | 36 | 36 | none |
| 2026 runtime (adds fonts, sequences, particles, filters, extensions, UI layers, options and audio group variants) | 51 | 51 | 51 | none |
| 2022 LTS runtime (variants including the 2.3 field name rooms) | 39 | 39 | 39 | none |

Every rebuild of an untouched project reported all resources as unchanged. A UTMT export opening in UTMT 0.9 itself is still the open manual check.

#### Export speed, second pass (commits 8f4f376 and 704729d)

Measured on the same machine in alternating runs against the previous branch tip (export of the loaded file, wall time, 22 logical cores):

| File | Before | After |
|---|---|---|
| Pizza Tower | 36.3 to 36.8 s | 25.8 to 26.6 s |
| Deltarune chapter 5 | 37.1 to 37.2 s | 26.7 to 28.5 s |
| Undertale | 13.0 s | 9.9 s |
| Cover Assault build | 9.6 s | 10.0 s (small project, within noise) |

The 74 s seen after the last merge was not reproduced here (36 s on the same file); it was probably taken under load.

Per phase on Pizza Tower, before then after (the types run side by side, so the phase total is the slowest type):

| Phase | Before | After |
|---|---|---|
| Sprites (31388 frames, 22402 distinct rectangles) | 30.7 s | 21.2 s |
| Code entries (decompile and disassemble) | 16.7 s | 15.1 s |
| Tilesets | 6.1 s | 5.2 s |
| Scripts, rooms, objects, fonts, textures | each under 4 s | each under 3 s |
| Final normalize and hash pass | 9.1 s | 3.7 s |
| Move of staged folders | 0.2 s | 0.2 s |

Two changes made the difference. Text files (json, gml, asm) were written with a BOM and CRLF and then rewritten in place by the normalize pass; they are now written in the final form during a project export, which removes about 35000 rewrites (rewriting them in a bare test took 9.5 s) and leaves the hash pass read-only. And the thread pool is given 6 threads per core as its minimum for the duration of the export: most workers spend their time blocked on file creation (Defender scans every new file), so the default one thread per core left the cores half idle; 66 workers on 22 cores took the sprite export from 32.5 s to 25 s, and more threads gave nothing further.

Tried and dropped, no gain or worse: hashing each sprite while it is written (1 s), capping the native image library to one thread (none), server garbage collection and a larger gen0 budget (none), ordering sprites by size (worse, 30 s, because frames of one texture page no longer run together and pages are decoded under contention), sharing one texture worker between types (tilesets decode their pages through a different path, small gain). A decompiled-code cache was not built: code entries are not on the critical path (15 s against 21 s for sprites), so it would save about 1 to 2 s on a first export and only matters for repeated exports of one data file. Skipping unchanged files would not help either, because the export writes into a fresh staging folder and file creation is the cost.

What is left is outside the library. Encoding 22402 distinct frames is about 58 s of CPU in the PNG encoder, and the encoder library scales to about 4 times a single thread. Creating 65000 files is limited by the file system and the virus scanner (about 5000 files per second in a bare test, whatever the thread count). Output must stay byte-identical, so neither can be replaced. The 15 s wish is not reachable for Pizza Tower or chapter 5 on this machine; the 60 s target is met with a wide margin.

Byte identity after the change: tree hashes equal the previous branch tip on all four files (Pizza Tower 15B6141ECA3186F3, chapter 5 930F51ED2088493E, Undertale A0EF1F460B64066F, Cover Assault build D5A100A151D02EBF). The acceptance runner reported on the final build:

| File | Rebuild byte-identical | Export twice identical | Export | Rebuild | Unchanged on rebuild |
|---|---|---|---|---|---|
| Pizza Tower | yes | yes | 27.4 s | 19.8 s | 20584 of 20584 |
| Deltarune chapter 5 | yes | yes | 25.5 s | 25.8 s | 31615 of 31615 |
| Undertale | yes | yes | 9.6 s | 10.2 s | 12159 of 12159 |
| Cover Assault build | yes | yes | 9.7 s | 1.2 s | 1214 of 1214 |

The library suite passes (1008 tests, one added for the normalized text writer).
