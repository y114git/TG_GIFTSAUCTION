# 0010 Workspace, project and data file sync

Status: accepted direction (owner, 2026-10-26); details marked "to decide" below are still open. Nothing is implemented before they are decided. Supersedes the parts of ADR-0007 and ADR-0008 that describe how a project and a data file relate.

## Three things, kept apart

| Thing | What it is | Where it lives |
| --- | --- | --- |
| Data file | The game's real file (`data.win` and the other formats). It is the main structure users work on. | Wherever the game is. Never inside the project. |
| Representation | The loaded in-memory model of the data file, as UndertaleModTool shows it. It is the live model. | Memory (like RAM). |
| Project | An opt-in layer: the whole content of the data file in a `.yyp`-like folder layout (ADR-0008), with history. It secures all resources even if the data file is corrupted or disappears (like an SSD behind the RAM). | User data folder by default, custom location allowed. |

Between them sits a safety net: the recovery journal and 1 to 3 rotating backups of the data file, always on, in the user data folder.

The project and the data file are separate entities. They are connected only by sync. Every part is customizable and the parts work separately or together, integrated without friction.

## Modes

- Direct mode (default). Opens like UndertaleModTool, in about a second. No project and no extra disk use. The safety net (journal and backups) is always on.
- Project mode (opt-in per data file). Turned on by an always-visible "Enable project" action, offered once at first open and available at any time; a setting changes the default for new files. Enabling exports the current representation into the project in the background with progress; edits made meanwhile are queued. The project then adds: complete resource copies, snapshots and history, Git, rebuilding a lost data file.
- A data file that has a project can still be opened in Direct mode at any time. Disabling a project asks: keep it (archived, can be re-enabled) or delete it to free disk space. The data file is never touched by this.

## Editing, autosave and sync

1. The representation is the live model. Every edit is appended to the journal in user data at once (flushed at least every second).
2. Direct mode: autosave writes only the journal. The data file changes only on Save, or when auto-save-to-file is enabled for that data file (off by default). After a crash the journal is offered for restore.
3. Project mode: autosave also writes the changed resources into the project (seconds at most).
4. Sync (project to data file) and Save (representation to data file) both build the data file from memory (about 1 s for large games), validate it by reading it back with G3MLib, and replace the original atomically (write next to it, verify, rename, rotate backups). Rebuilding from project files (18 to 24 s on large games) happens only when the data file is gone or corrupt.
5. In Project mode, auto sync on save is a setting (default on). The Sync button is always present. When auto sync is off the title bar shows "N changes not synced to data file".
6. Close or restart (including restart to update): project changes are saved automatically. If the project holds changes not yet in the data file, a prompt offers Sync changes, Do it later or Cancel (Cancel returns to the app as if nothing was closed). Direct mode with unsaved changes uses the same prompt with Save, Do it later (kept in the journal) or Cancel.
7. After restart the same workspace, tabs and selection are restored.

## Snapshots and history (project mode)

- Built-in local snapshots: named or timed, content-addressed so only changed resources are stored; compare and restore per resource; fast and lightweight.
- Git works on the project folder if `git` is installed (Git panel). Snapshots work without git. Snapshots and Git can be used separately or together.
- Limits (count, age, total size) are customizable; the default is no limit.
- Backups of the data file: 1 to 3 rotating copies by default, customizable, in the user data folder (not in the game folder).

## Scenarios

| Situation | Behaviour |
| --- | --- |
| First open of a data file | Direct mode, usable at once. One-time offer to enable the project (never repeated unless asked). |
| Reopen a known data file with a project | Match by identity (size and MD5 at first open, plus path), open the project, compare the data file hash with the last synced hash. |
| Data file changed outside (patched by another tool, game update) | Ask: bring the external changes into the project (resource-level three-way merge: base = last synced resource hashes, theirs = data file, ours = project; conflicts only where the same resource changed on both sides, resolved per resource), overwrite the data file with the project, or keep both (new workspace). Direct mode without a project: reload prompt. |
| Data file missing or corrupt | With a project: rebuild it from the project to a chosen path. Without: restore from the rotating backups, or the journal. |
| Project folder edited outside (text editor, Git pull) | A file watcher reloads the changed resources, with a prompt if they were also edited in the app. |
| Project folder lost | Re-export from the data file. |
| Same game opened from a different path | Identity finds the project; ask whether it is the same workspace (update the stored path) or a new one. |
| Two windows on one data file | The second opens read-only with a notice. |
| Direct mode save while a project exists | Allowed; the project later sees an external change and handles it as above. |
| Data file locked (game running, Steam updating) | Retry, then report which program holds it and keep the edits; Sync retries later. |
| Data file in a protected or read-only folder | Save asks for another location; journal and project keep the work. |
| Create a data file from scratch (advanced) | G3MLib creates an empty data file for a chosen GameMaker version and runtime layout; it opens as a normal workspace. Supported versions are decided in the first implementation spike. |

## Disk use

A full project of a game is larger than the data file (Pizza Tower: 65,000 files, PNG frames the main cost). It exists only when the user enables it. The settings page lists each project with its size and offers archive or delete.

## Still to decide

1. Whether Enable project happens silently in the background or always asks for a location the first time.
2. Snapshot triggers by default (on every sync, on a timer, manual only).
3. Whether the project may be shared on its own with other users (Git remote) and what the app shows when a shared project meets a different base data file.
4. How several data files of one game (Deltarune chapters) relate: separate projects (proposed).
