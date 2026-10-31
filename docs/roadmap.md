# Roadmap to 1.0.0-beta

Phases are ordered by dependency. A milestone is done when every acceptance check passes on Windows x64 and the CI builds all six RIDs (win, linux, osx; x64, arm64). Requirement IDs refer to [requirements.md](requirements.md); parity IDs to [research/utmt-parity.md](research/utmt-parity.md).

1.0.0-beta is the first public release and must be complete: every parity item, every P0 and P1 requirement, the performance budget met, six languages.

## L. G3MLib 1.1.0, G3MTool 1.3.1 / 1.0.1 (before any G3MStudio code)

G3MStudio consumes G3MLib only from NuGet (ADR-0003), so the library is finished first.

### L1 GameMaker investigation

| Work | Acceptance check |
| --- | --- |
| Format coverage map: every chunk, field and version flag G3MLib reads, against GameMaker LTS 2026 and current monthly runtimes | Table in `research/gamemaker-format.md` with "covered, partial, missing" per field |
| Black-box corpus: GameMaker test projects that use every resource type, layer type, event type and option, built with the installed GameMaker LTS 2026 for Windows (VM and YYC where relevant) and other targets the install allows | Each built data file opens in G3MLib, round-trips byte-identical, and every feature in the project is visible in the model |
| Runtime behaviour questions the editor depends on (texture page layout rules, audio group files, room creation order, instance IDs, string interning) answered by experiment | Each answer recorded with the test project that shows it |
| Disassembly tools (REA with Ghidra) only after the owner clears the EULA question (Q16) | Owner decision recorded in questions.md |

### L2 Library work

The ten items in [architecture.md](architecture.md#g3mlib-work-needed), plus every gap L1 finds, plus whatever the full parity matrix and the planned editors need (find usages for all ~40 reference sources, texture formats QOI, BZ2-QOI, DDS, UTMT project import).

Acceptance: G3MLib tests green; new APIs covered by tests; UTMT 110 bundled scripts compatibility report generated; benchmarks rerun and README results updated; no regression against the 1.0.1 numbers.

### L3 Release

G3MLib 1.1.0 published to NuGet after the owner approves. Then G3MTool moves to G3MLib 1.1.0 as CLI 1.3.1 and GUI 1.0.1 (Q18, Q30): library upgrade, minimal fixes, cancel support for long operations such as patching, and no other new features. Every change adds as few lines of new code as possible. Every existing `.g3mpatch`, xdelta and `.csx` workflow keeps working (regression suite of real patches and scripts), new library features exposed where useful. Then G3M is checked against the new G3MTool (CLI arguments, outputs, exit codes) and updated only if needed.

## M0 Foundations and spikes

| Work | Acceptance check |
| --- | --- |
| Solution skeleton (ADR-0002), CI on six RIDs, self-contained portable zips named `G3MStudio-<OS>-<ARCH>-<VERSION>.zip` with the files in the zip root (Q27) | CI green; each zip extracts, starts and shows a window |
| Rule: CI-first development on all six targets (win, linux, osx; x64, arm64) | From M0 on, every change builds and runs its tests and headless UI tests on all six targets before it counts as done. A platform-specific failure is fixed or recorded in [research/platform-proof-log.md](research/platform-proof-log.md); nothing is skipped silently (N-PLT-2) |
| Theme tokens from design.md, Fluent overrides | Headless test walks every control template in use and finds only 2 px borders |
| Localization pipeline (ADR-0006), glossary for the six languages | Key checker and glossary consistency tests pass; switching language at runtime relabels the shell |
| Command registry and palette prototype | 20 commands reachable from menu, palette and shortcut |
| Docking spike (Dock.Avalonia) | Dock, split, float, close, persist, restore and reset work on Windows, Linux X11, Linux Wayland and macOS |
| Code editor spike (AvaloniaEdit + G3MLib tokens) | Largest code entry of the test game opens, highlights and scrolls at 60 fps |
| Room render spike | One GMS2 and one GMS1 room render with tiles and instances at 60 fps while panning |
| Updater spike (ADR-0005) | Own updater: update, interrupted download, SHA-256 mismatch, replace of a running program folder, and a non-writable folder verified on Windows and one other OS |

## M1 Shell and workspace

Requirements: F-WS-0 to F-WS-6, F-WS-12 to F-WS-17, F-UI-1 to F-UI-8, F-RES-1, F-RES-12, N-PERF-2, N-SAFE-1.

| Work | Acceptance check |
| --- | --- |
| Start page, recent and pinned list, drag and drop | Open a data file from each entry point |
| Workspace open with progress, per-file state restore | Reopen restores tabs, layout and selection |
| Resource explorer | 50,000 entries scroll at 60 fps; filter returns in under 100 ms |
| Tabs, preview tabs, pinned tabs, splits | Behaviour matches the VS Code research doc |
| Undo stack and command model | Every edit in M1 undoes and redoes; history list shows it |
| Save pipeline and 1 to 3 rotating backups in the user data folder | Kill-during-save test leaves either the old or the new valid file, never a broken one |
| Sync atomicity (project to data file, and Save): build from memory, validation read, atomic replace, backup rotation | Kill at every step leaves the old or the new valid data file; a locked file retries and keeps the edits |
| Journal, recovery, hot exit | Crash test restores unsaved edits from the journal (at most 1 s lost); a missing or corrupt data file is restored from backups or journal, and rebuilt from the project when one exists |
| Close and restart prompt | Sync changes (Save in Direct mode), Do it later and Cancel behave as ADR-0010 item 6; Cancel returns to the app unchanged |
| Three-way external change merge (project mode) | Base = last synced resource hashes, theirs = data file, ours = project; conflicts only where one resource changed on both sides, resolved per resource; tested with a patched data file and a game update |
| Enable project, Direct mode | Enable project exports in the background with progress and queued edits; Direct mode opens in about 1 s with no project |
| Settings UI, shortcut editor, Output, Problems, notifications, status bar | Every setting and shortcut is editable and searchable |
| Quick open and command palette | Any resource opens in under 3 keystrokes after the prefix |

## M2 Core editors

Requirements: F-RES-2 (first half), F-RES-3, F-RES-4, F-RES-5, F-RES-7, F-RES-8, F-RES-9, F-MOD-3.

General info and options, strings, code (decompile, compile, disassembly, assembly), scripts, global and local variables, functions, code locals, global init and game end, objects with events, sprites, backgrounds and tile sets, texture pages and embedded textures, sounds and audio groups, fonts, image editor, search across code and strings, C# script runner and console with the UTMT-compatible host.

Acceptance: each item's parity entry is "verified"; edit, save, reopen and run the game for each resource type on the corpus games.

## M3 Rooms and remaining editors

Requirements: F-RES-2 (rest), F-RES-6, F-RES-10, F-RES-11, F-PAR-2, F-PAR-3.

Room editor (layers, instances, tiles, backgrounds, views, creation code, GMS1 and GMS2), tile layer editor, paths, timelines, shaders, extensions, sequences, animation curves, particle systems, filters and effects, rename with reference update, find usages, UTMT bundled scripts run or replaced.

Acceptance: every parity item in these areas verified; UTMT script compatibility report shows every script working or replaced by a built-in command.

## M4 Project folders, Git and modding

Requirements: F-WS-7 to F-WS-10, F-GIT-1 to F-GIT-4, F-MOD-1, F-MOD-2, F-MOD-4.

Export to and build from a project folder, open UTMT 0.9 projects, Git panel with resource-aware diff and conflict resolution, diff of the data file against the original, patch create, validate, apply and merge, run game, batch export and import, integrity check, multiple windows.

Acceptance: round trip data file to folder to data file is byte-identical on the corpus when nothing changed; a commit, branch, pull and push cycle works against GitHub.

## M5 Beta release

Requirements: F-UPD-1 to F-UPD-5, F-PRIV-1, F-L10N-1 to F-L10N-5, N-PLT-1, N-PERF-1, N-SIZE-1.

Own updater live on GitHub Releases, Sentry crash reporting wired by CI secrets, six languages complete, the six portable zips (Q27; unsigned, with documented first-run steps), performance pass against the budget, full parity audit, G3MWiki pages for G3MStudio.

Acceptance: performance budget met; parity matrix has no item without "verified" or an accepted "not applicable"; smoke test passes on all six zips.

## After beta (P2)

Themes system, saved named layouts, a canvas view for editors if users ask for it.
