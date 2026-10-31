# Architecture

Status: proposed. Nothing is implemented. Target release: 1.0.0-beta. Decisions marked "ADR" have a record in [decisions/](decisions/); "open" ones wait on [questions.md](questions.md) or a spike.

## Shape

```
G3MStudio.sln
  src/G3MStudio.Core      no Avalonia. Workspace, documents, commands, undo, save pipeline,
                          journal, settings, localization lookup, search index, git client.
  src/G3MStudio.App       Avalonia. Shell, docking, theme, views and view models, editors,
                          start page, dialogs, updater UI, Program.cs.
  tests/G3MStudio.Tests   xUnit. Core unit tests + Avalonia headless UI tests.
G3MLib                    separate repo, consumed only as the NuGet package G3MLib 1.1.0
                          (ADR-0003). All library work lands and ships there first.
```

Two projects, not one per editor. Editors live in `App/Editors/<Type>/`. A split happens only when build time or a second consumer asks for it. Each editor registers through one interface, so adding a resource type never touches the shell:

```csharp
interface IEditorProvider {
    bool CanOpen(ResourceRef r);
    EditorViewModel Create(ResourceRef r, Workspace ws);
}
```

## Main parts

| Part | Responsibility | Built on |
| --- | --- | --- |
| Workspace | One open data file: the G3MLib model (the live representation), open documents, layout, per-workspace settings, dirty state, journal, and the optional project (ADR-0010). | G3MLib `GameMakerData` |
| Command registry | Every action: id, localized title, icon, default shortcut, context predicate, handler. Menus, toolbars, context menus, palette and the shortcut editor render from it. | own, about 300 lines |
| Undo | Command objects that change the model and know their inverse. One stack per workspace, grouped by editor for display. Coalescing for drags and typing. | own; G3MLib models already raise `PropertyChanged` |
| Save pipeline | Build the data file from memory, write it next to the target, read it back with G3MLib, compare chunk counts and hashes, then atomic replace. 1 to 3 rotating backups of the previous file in the user data folder. Sync (project to data file) and Save use the same pipeline (ADR-0010). | G3MLib write, `File.Replace`/`rename` |
| Journal and recovery | Every edit is appended to a crash-safe journal in the user data folder (flushed at least every second). After a crash the journal is offered for restore. Never writes to the user's file. | save pipeline |
| Explorer | Virtualized resource tree, filter, sort, type groups, multi-select, context commands. | Avalonia `TreeDataGrid` or virtualized `TreeView` (spike) |
| Docking | Dock regions left, right, bottom, editor area with splits; float; persist; reset. | Dock.Avalonia 12.1.0.12 (spike) |
| Code editor | GML with highlighting from G3MLib's lexer, diagnostics, folding, find, go to definition, references, disassembly side view. | AvaloniaEdit 12.0.0 |
| Search | Background index of names, strings and decompiled code (cached per data file hash); query with regex, case, word; results panel. | G3MLib decompiler, `.g3mcache` |
| Git | Status, diff, stage, commit, branch, pull, push, history for project folders. | system `git` program (ADR-0004) |
| Scripting | `.csx` runner, console, UTMT-compatible `IScriptInterface` host. | G3MLib scripting, Roslyn |
| Updater | Check GitHub releases, download the zip for the OS and architecture, verify the SHA-256 digest, then a small external process replaces the program folder and restarts (ADR-0005). | own code |
| Localization | JSON string tables per language, `{l:Str Key}` markup extension, key checker test. | G3M's language file format (ADR-0006) |
| Settings | User and workspace JSON, schema with defaults, settings UI generated from the schema. | System.Text.Json |

## Threading

- The G3MLib model is owned by the UI thread. Edits happen there, through undo commands.
- Background work (decompile for search, export, diff, patch) runs on a snapshot or under a workspace lock that blocks edits and shows progress. G3MLib has process-wide statics and no locking (see [research/g3mlib.md](research/g3mlib.md)); this rule must be documented and enforced before any background job ships.
- Rule: no call over 100 ms on the UI thread. Long calls go through one job service with progress, cancellation and Output logging.

## Files on disk

| What | Where |
| --- | --- |
| User settings, keybindings, recent list | The per-user app data folder, never the program folder (Q28): `%LOCALAPPDATA%/G3MStudio` (Windows), `~/.config/G3MStudio` (Linux), `~/Library/Application Support/G3MStudio` (macOS) |
| Per data file state (layout, tabs, caches, recovery) | app data folder, keyed by a hash of the absolute path plus file size and MD5; game folders are often read-only or managed by Steam |
| Per project folder state | `.g3mstudio/` inside the folder, ignored by Git by default. The project folder itself lives in the user data folder by default, any location allowed (ADR-0010) |
| Backups of saved data files | the user data folder, 1 to 3 rotating copies (default 3), not in the game folder |

## Performance budget

Targets on a mid-range Windows x64 laptop, measured with the 92 MB Pizza Tower `data.win` used by G3MLib's benchmarks. Each release records the numbers in [status.md](status.md).

| Measure | Target |
| --- | --- |
| Cold start to start page | under 1.0 s |
| Open data file to usable explorer | under 1.5 s (G3MLib read is about 0.65 s) |
| Open a code entry (decompile + highlight) | under 100 ms typical, 300 ms largest |
| Full code search, warm cache | under 1 s; cold under 10 s in background |
| Save (write + validate + replace) | under 2 s, off the UI thread |
| Memory above the G3MLib model | under 200 MB with 20 editors open |
| Idle CPU | 0% with no animation running |
| Frame time while scrolling trees and lists | 16 ms at 60 Hz |

## Licensing

G3MLib is GPL-3.0-only (it ports UndertaleModLib code), so G3MStudio is GPL-3.0-only. Candidate libraries are MIT or compatible: Avalonia, Dock.Avalonia, AvaloniaEdit, CommunityToolkit.Mvvm, Magick.NET (Apache-2.0).

## G3MLib work needed

From the gaps table in [research/g3mlib.md](research/g3mlib.md#gaps-g3mstudio-must-fill). Each item is a single operation (see "G3MLib scope" below). These land in G3MLib 1.1.0 before G3MStudio work starts (roadmap phase L), not as editor-side workarounds:

1. Async read and write with progress and cancellation.
2. Unknown chunk passthrough.
3. Public lexer tokens and structured diagnostics (span, severity).
4. Decompiler source map.
5. Reverse reference index, find usages, rename primitives.
6. Per-resource serializers for the documented, sorted, stable project format: ADR-0008 (superset of the UTMT 0.9 project format). Folder orchestration is G3MStudio (Q25).
7. One `ScriptGlobals` matching `IScriptInterface`; UTMT API compatibility tests.
8. Thread model: remove or isolate process-wide statics.
9. Texture page repacking.
10. Cancellation for patch, merge, export, import.
11. Every gap in research/gamemaker-format-coverage.md, section 4 (starting with the GEN8 version that is lost on read).

## G3MLib scope (questions.md Q25)

G3MLib stays low level: single operations, with batch-execution optimizations. It does not combine operations into end-user workflows. G3MStudio implements the workflows on top of it.

| Item | Scope | Note |
| --- | --- | --- |
| Read and write, async with progress and cancellation (1) | G3MLib (primitive) | |
| Unknown chunk passthrough (2) | G3MLib (primitive) | |
| Lexer tokens and structured diagnostics (3) | G3MLib (primitive) | |
| Decompiler source map (4) | G3MLib (primitive) | |
| Reverse reference index (5) | G3MLib (primitive) | |
| Rename primitives (5): change one name and the references that point to it | G3MLib (primitive) | Preview, confirmation, undo and multi-step rename flows are G3MStudio |
| Per-resource serialize and deserialize (6, 7) | G3MLib (primitive) | One serializer per resource type |
| Project folder service: enumerate, write and read a whole folder, delta and full modes, Build, deleted resources, migration registry (6) | move to G3MStudio (workflow), keep until G3MStudio takes it over | Built on `l2/project-format`; see the ADR-0008 re-review |
| Export speed (plan item A) | G3MLib (primitive) for per-resource export and parallel batch execution; the "export the whole game to a folder" flow is G3MStudio | |
| ScriptGlobals and script host (7) | G3MLib (primitive) | |
| Thread model (8) | G3MLib (primitive) | |
| Texture page repacking (9) | G3MLib (primitive) | |
| Cancellation for long operations (10) | G3MLib (primitive) | |
| Format gaps (11) | G3MLib (primitive) | |
| Patch, merge and diff services | G3MLib (primitive) | Existing; G3MTool uses them |
| Change tracking, undo, save pipeline (write, validation read, backup, atomic replace) | G3MStudio (workflow) | Already planned in the Main parts table |
| Git integration | G3MStudio (workflow) | ADR-0004 |
| Virtual folder editing, GameMaker project import orchestration (plan item I) | G3MStudio (workflow); per-resource `.yy` converters stay G3MLib (primitive) | |

Built work is not deleted. An item marked "move to G3MStudio" stays on its branch until G3MStudio takes it over, then is removed from G3MLib in a later release. The full per-item list with branches is in [g3mlib-1.1.0-plan.md](g3mlib-1.1.0-plan.md#scope-review-q25).

## Platform support (questions.md Q26, N-PLT-2)

1. G3MStudio is fully supported on every supported OS and architecture: Windows, Linux and macOS, x64 and arm64. Each feature is built with the best practice for each platform and with libraries that run on all of them.
2. Development is CI-first: every change builds and tests on all six targets (roadmap M0 rule). A manual check on one OS does not replace the CI run.
3. A feature may be blocked on one OS or architecture only after extensive CI tests prove it impossible there. The evidence goes into [research/platform-proof-log.md](research/platform-proof-log.md): what was tried, CI run references, result.
4. Before a block, a partial implementation is attempted (for example a reduced feature, another API, or a user-driven fallback). The partial version ships when it works.
5. A blocked or partial feature shows a visible warning in the UI where the feature lives, naming the platform and what is missing. It is not hidden or silently disabled.
6. A block is reviewed at every release; when the platform or a library changes, the feature is retried.
