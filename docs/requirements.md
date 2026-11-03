# Requirements

Status words used in every doc: planned, in progress, implemented (code exists), verified (tested and checked by hand or by tests). IDs are stable; never reuse a removed ID.

Priority: P0 = required for 1.0.0-beta, P1 = expected for beta, cut only with a written reason the owner accepts, P2 = after beta.

## Functional

### Parity

| ID | Requirement | Priority |
| --- | --- | --- |
| F-PAR-1 | Every item in [research/utmt-parity.md](research/utmt-parity.md) (UTMT 0.9.2.0: 272 items) works in G3MStudio 1.0.0-beta, or is marked not applicable with a reason the owner accepts. The parity file's P0/P1/P2 column only orders the work. Every item carries a status in that file. | P0 |
| F-PAR-2 | UTMT `.csx` scripts run unchanged where G3MLib's API allows. Known gaps are listed per script. | P0 |
| F-PAR-3 | No scripts are bundled (Q42). Every UTMT bundled script has a built-in feature (design/script-features.md), a plugin candidate, or a written reason why not (research/utmt-scripts-analysis.md). | P1 |

### Workspace

| ID | Requirement | Priority |
| --- | --- | --- |
| F-WS-1 | Opening a data file opens a workspace: resource tree, editors, layout and per-file state. No setup step. | P0 |
| F-WS-2 | The workspace remembers open tabs, layout, selection and scroll per data file and restores them on reopen. | P0 |
| F-WS-3 | Start page with recent workspaces, pinned workspaces, open file, open folder and drag and drop. | P0 |
| F-WS-4 | Every edit is appended to a crash-safe journal in the user data folder (flushed at least every second); a crash or power loss never loses more than the last second. Closing with changes that are not in the data file shows the prompt of F-WS-14. | P0 |
| F-WS-5 | Saving and sync build the data file from memory, write it next to the target, validate it by reading it back, and replace the target only after both succeed. 1 to 3 rotating backups (default 3) are kept in the user data folder. | P0 |
| F-WS-6 | Built-in snapshots in project mode: named or timed, content-addressed, compare and restore per resource. Limits (count, age, total size) are customizable; the default is no limit. | P1 |
| F-WS-0 | Direct mode (default, no project) with an always-on safety net (journal and rotating backups); Project mode as an opt-in layer enabled by an always-visible Enable project action; the two are connected only by sync (ADR-0010). | P0 |
| F-WS-7 | Project mode: export the data file to a project folder with one folder per resource in GameMaker's layout (ADR-0008) and rebuild the data file from it. The folder lives in the user data folder by default, any location allowed. It is the unit that Git tracks (ADR-0007). | P0 |
| F-WS-8 | External changes to the open data file or project folder are detected and offered for reload. | P1 |
| F-WS-9 | Several workspaces open at once (one per window). | P1 |
| F-WS-10 | Open UTMT 0.9 project files (project JSON, GML sources, import stages) so UTMT users can move without converting by hand. | P1 |
| F-WS-11 | Import resources from GameMaker .yyp/.yy projects into a data file where G3MLib can compile them, and export resources as .yy folders. | P1 |
| F-WS-12 | Sync (project to data file) is atomic and fast; auto sync on save is a setting (default on); a Sync button is always present; with auto sync off the title bar shows "N changes not synced to data file". | P0 |
| F-WS-13 | Autosave: in Direct mode it writes only the journal (writing the data file is a per-file setting, off by default); in project mode it also writes the changed resources into the project. | P0 |
| F-WS-14 | Close or restart (including restart to update): project changes are saved automatically; if changes are not yet in the data file a prompt offers Sync changes (Save in Direct mode), Do it later or Cancel. After restart the workspace, tabs and selection return. | P0 |
| F-WS-15 | Data file changed outside: project mode offers a resource-level three-way merge into the project, overwrite of the data file, or keep both; Direct mode offers reload. A data file that is missing or corrupt is restored from backups or the journal, or rebuilt from the project. | P0 |
| F-WS-16 | Disabling a project asks to keep it (archived) or delete it; the data file is never touched. Settings list each project with its size. | P1 |
| F-WS-17 | Advanced: create a new empty data file from scratch for a chosen GameMaker version (supported versions decided in the first implementation spike). | P1 |

### Shell and layout

| ID | Requirement | Priority |
| --- | --- | --- |
| F-UI-1 | Docking: panels dock left, right and bottom, split, tab, float, resize, hide and show. Layout persists. One command resets it. | P0 |
| F-UI-2 | Editor area with tabs, preview tabs, pinned tabs, dirty markers, split editors and a recency switcher. | P0 |
| F-UI-3 | Command palette with every command, its shortcut and fuzzy search. Menus, toolbars and palette share one command registry. | P0 |
| F-UI-4 | Quick open for resources by name across all types, with type prefixes. | P0 |
| F-UI-5 | Keyboard shortcut editor with search, conflict detection, import and export. | P1 |
| F-UI-6 | Settings UI with search; settings stored as JSON (user and per-workspace scopes). | P0 |
| F-UI-7 | Status bar: data file info (GameMaker version, bytecode version, platform), save state, background task progress. | P0 |
| F-UI-8 | Output panel with channels, Problems panel, notification history. | P0 |
| F-UI-9 | Saved named layouts and the Studio, Code and Canvas presets (ADR-0010). | P0 |
| F-UI-10 | Canvas workspaces with linked floating editor cards, and floating OS windows for any panel or editor (ADR-0010). | P0 |

### Resources and editors

| ID | Requirement | Priority |
| --- | --- | --- |
| F-RES-1 | Resource explorer: all resource types, virtualized, filter, sort, search, multi-select, drag to reorder where order matters, context actions per type. | P0 |
| F-RES-2 | Dedicated editors for every resource type UTMT edits (list in the parity matrix). | P0 |
| F-RES-3 | Code editor: GML decompile and compile, syntax highlighting, errors with positions, find and replace, go to definition, find references, folding, disassembly view. | P0 |
| F-RES-4 | Global search across code, strings and names, with regex, case and whole-word options, replace in code, and results that open the exact spot. | P0 |
| F-RES-5 | Sprite and texture tools: frame view, origin, collision mask, import and export (PNG, strips, GIF), texture page view and repacking. | P0 |
| F-RES-6 | Room editor: layers, instances, tiles, backgrounds, views, creation code, with drag, snap, multi-select and property editing. | P0 |
| F-RES-7 | Audio: playback, waveform, import and export, audio group handling, embedded and external sounds. | P0 |
| F-RES-8 | Font editor: glyph view, glyph metrics, texture preview, import. | P0 |
| F-RES-9 | Image editor for frames and tiles (pencil, eraser, fill, select, colour picker, layers optional). | P1 |
| F-RES-10 | Sequence, animation curve, particle system, path and timeline editors. | P1 |
| F-RES-11 | Rename with reference update and "find usages" for every resource type. | P1 |
| F-RES-12 | Undo and redo for every edit, per editor and global, with a history list. | P0 |

### Version control

| ID | Requirement | Priority |
| --- | --- | --- |
| F-GIT-1 | Git panel for a project folder: status, diff per resource, stage, unstage, discard, commit, branch, pull, push, fetch, history. | P0 |
| F-GIT-2 | Resource-aware diff: code as text diff, images side by side, rooms and objects as property diff. | P1 |
| F-GIT-3 | Changes view for the data file itself without Git: G3MLib diff against the original or last saved file. | P0 |
| F-GIT-4 | Merge conflict resolution for text resources; G3MLib patch merge for data files. | P1 |

### Modding pipeline

| ID | Requirement | Priority |
| --- | --- | --- |
| F-MOD-1 | Create, validate, apply and merge `.g3mpatch` and xdelta patches from the UI (G3MLib). | P0 |
| F-MOD-2 | Run the game from the editor with the current changes, choosing the runner. | P0 |
| F-MOD-3 | C# script console and script runner with UTMT-compatible API and dialogs. | P0 |
| F-MOD-4 | Batch operations: export all, import all, find unused, integrity check and repair. | P1 |

### Modding suite

G3MStudio also replaces G3MTool and the modder side of G3M and G3M-MCE. Inventory: [research/modding-suite.md](research/modding-suite.md).

| ID | Requirement | Priority |
| --- | --- | --- |
| F-SUITE-1 | Every G3MTool feature is available in the G3MStudio GUI (patch create, apply, validate, merge, compare, inspect, batch, xdelta, scripts, external programs). G3MStudio has no command line; scripting and automation use G3MTool (questions.md Q24). | P0 |
| F-SUITE-2 | Every G3M-MCE feature: create and edit G3M mods (metadata, games and chapters, files, tags), import other mod formats, package and validate. | P0 |
| F-SUITE-3 | Launch the game with the current changes or with selected mods, capture its output and logs, and show diagnostics. | P0 |
| F-SUITE-4 | Export: to G3M (`g3m://`), as a G3M mod, as a patch, as a modified data file, or any selection of resources, each with or without its settings (for example sprite images only, or images with origin, mask and speed). | P0 |
| F-SUITE-5 | `g3mstudio://` URI scheme for other apps to open files, mods and actions in G3MStudio. | P0 |
| F-SUITE-6 | Debugging aids: runtime log viewer, crash report reader, mod conflict view. | P1 |

### Onboarding

| ID | Requirement | Priority |
| --- | --- | --- |
| F-ONB-1 | First launch: language, theme import from G3M, file associations (every extension UTMT associates, plus `.g3mpatch` and other G3M formats), short tour. Every choice can be changed later in settings. | P0 |

### Plugins (ADR-0009)

| ID | Requirement | Priority |
| --- | --- | --- |
| F-PLG-1 | Plugins are folders or zips with plugin.json and C# sources (.cs or .csx, compiled on load) or a prebuilt .dll; any other files allowed. | P0 |
| F-PLG-2 | Plugins can add commands, menus, toolbars, dock panels, editor tabs, resource editors, inspectors, explorer actions, importers, exporters, file types, settings pages, status bar items, jobs, game launch hooks and themes. | P0 |
| F-PLG-3 | Plugins edit the open data file through the same undo-aware API as built-in editors. | P0 |
| F-PLG-4 | Enable, disable, update and reload without restarting; a failing plugin never crashes the app. | P0 |
| F-PLG-5 | Install from folder, zip or GitHub URL; one-time trust prompt showing source, author and declared permissions. | P0 |
| F-PLG-6 | Plugin template command and hot reload while developing. | P1 |
| F-PLG-7 | Plugins ship their own translations and settings. | P1 |
| F-PLG-8 | Plugin catalog inside the app. | P2 |

### Updates

| ID | Requirement | Priority |
| --- | --- | --- |
| F-UPD-1 | Check the GitHub releases of the owner's repository at startup and on demand; show release notes. The highest of the latest release, the optional pre-release and the running version wins. If it is higher, download the zip for the detected OS and architecture; "Restart to Update" applies it. | P0 |
| F-UPD-2 | Downloads use HTTPS from the owner's repository releases only and are compared with GitHub's SHA-256 digest of the asset; a mismatch deletes the file and refuses the update. No code signing. A failed or interrupted update leaves the installed version running. | P0 |
| F-UPD-3 | Setting to include pre-releases, and an off switch for the check. | P1 |
| F-UPD-4 | "Restart to Update" saves everything (F-WS-14), then a small external updater process closes the app, extracts all files of the zip into the folder of the previous executable, replaces them and starts the new executable. The user returns to almost the same state. | P0 |
| F-UPD-5 | If the program folder is not writable, the update is not applied; a message names the downloaded zip path so the user can extract it by hand. | P0 |

### Privacy

| ID | Requirement | Priority |
| --- | --- | --- |
| F-PRIV-1 | Crash reporting through Sentry, opt-out with scrubbing (no IP, no user name, paths reduced to relative or hashed parts, no data file content, no breadcrumbs with file names). Onboarding shows a notice and a switch before any report can be sent; a setting changes it later. Keys come from CI secrets only. A build without keys, an invalid key, a failing network or an exhausted Sentry quota never causes an error or a crash. | P0 |

### Localization

| ID | Requirement | Priority |
| --- | --- | --- |
| F-L10N-1 | All UI strings come from resource files from the first commit. No literal UI text in code or XAML. | P0 |
| F-L10N-2 | Languages: the G3M set (en, ru, es, ja, ko, zh_cn, zh_tw), all complete for beta. English is the fallback. | P0 |
| F-L10N-3 | One terminology glossary per language, used by every string. | P0 |
| F-L10N-4 | Translations are written string by string from the glossary (F-L10N-3), which is built from established human translations: G3M's language files, VS Code's official language packs, GameMaker's IDE packs and UTMT where it has terms. No bulk machine translation. Known bad terms in those sources are not copied. Native speakers review when available. | P0 |
| F-L10N-5 | Per-language fonts (CJK fallbacks), plural rules, number and date formats. | P0 |

## Non-functional

| ID | Requirement | Priority |
| --- | --- | --- |
| N-UI-1 | Visual style follows G3M (see [design.md](design.md)): one default theme, 90% G3M design and style and 10% GameMaker, VS Code and Zed, with no light theme and a fully customizable look, including the G3M background; every border is exactly 2 px. | P0 |
| N-UI-2 | Beginner friendly: clear labels, tooltips with shortcuts, helpful errors with a next step, confirmation only for destructive actions, undo instead of prompts where possible. | P0 |
| N-UI-3 | Keyboard reachable: every command has a palette entry; focus is always visible. | P0 |
| N-PLT-1 | Windows, Linux and macOS on x64 and arm64 (G3M's matrix), shipped as six portable zips named `G3MStudio-<OS>-<ARCH>-<VERSION>.zip`, self-contained (no runtime install), files in the zip root. No installers and no native packages for beta (Q27). Tested on each before a release. | P0 |
| N-PLT-2 | Full support on every supported OS and architecture, using best practices and suitable libraries (questions.md Q26). A feature may be blocked on one platform only after documented, extensive CI evidence that it is impossible there, recorded in [research/platform-proof-log.md](research/platform-proof-log.md). A partial implementation is attempted first, and a blocked feature shows a visible warning in the UI. | P0 |
| N-PERF-1 | Measured targets in [architecture.md](architecture.md#performance-budget); each release records results. | P0 |
| N-PERF-2 | No operation over 100 ms runs on the UI thread. Long operations show progress and can be cancelled. | P0 |
| N-SAFE-1 | No write to the user's data file without a backup and a validation read. | P0 |
| N-SAFE-2 | Destructive actions are undoable or confirmed, and say what they affect. | P0 |
| N-SIZE-1 | Self-contained build as light as possible without losing performance or quality: single file if that proves best, otherwise a plain program folder; size and first-launch time tracked per release; trimming evaluated. User data lives in the per-user app data folder, never next to the program. | P1 |
| N-QA-1 | Automated tests for G3MStudio logic; G3MLib round-trip tests on real games; UI smoke tests (Avalonia headless). | P0 |
| N-DOC-1 | This knowledge base stays current; [status.md](status.md) is updated at the end of every work session. | P0 |
