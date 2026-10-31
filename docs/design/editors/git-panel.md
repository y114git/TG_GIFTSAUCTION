# Git panel

Status: proposed. Git for project folders: status, diff per resource, stage, unstage, discard, commit, branch, pull, push, fetch, history and conflict resolution. It uses the system `git` command (ADR-0004) and tracks the project folder, not the data file (ADR-0007). Shared rules: [../ux-standards.md](../ux-standards.md). Requirements: F-GIT-1, F-GIT-2, F-GIT-4.

## 1. Purpose and entry points

Keep mod history safe without leaving the editor. Opens as the Git tab of the left dock (Ctrl+Shift+G), from the status bar branch item, and from the Git menu. Available in project folder mode and in data file mode when the file's folder is a repository (then it shows the data file as one binary change).

When the system `git` is missing: the panel shows "Git was not found. Install Git or set its path in Settings > Paths." with a Browse button and a link. Nothing else in the app depends on Git.

## 2. Layout

```
+------------------------------------+
| Git   main  <- 1  -> 2   [Sync v]  |
+------------------------------------+
| [ commit message...              ] |
| [Commit v]  [Stage All]            |
+------------------------------------+
| v Staged Changes (2)               |
|    M spr_player           [-]      |
|    A obj_boss             [-]      |
| v Changes (5)                      |
|    M gml_Object_obj_p..   [+] [undo]|
|    D room_old             [+]      |
| v Untracked (1)                    |
|    ? notes/todo.txt       [+]      |
| v Merge Conflicts (0)              |
+------------------------------------+
| History >                          |
+------------------------------------+
```

The branch row shows ahead and behind counts; Sync runs pull then push. The message box is multi-line (72-character guide line), Ctrl+Enter commits, Up and Down in an empty box cycle previous messages. Lists use the shared result list: grouped by section, one row per changed resource (resource name, not file path; hover shows the path), letter status M, A, D, R, U, C with an icon, row actions on hover and on focus.

Resources are listed as G3MStudio resources (sprite, object, room), derived from the folder's files; unmapped files show their path.

## 3. Actions (F-GIT-1)

| Action | Behaviour |
| --- | --- |
| Stage, Unstage | Row buttons, Space on a selected row, drag between sections; Stage All and Unstage All in the header menu (`git.stageAll`, `git.unstageAll`). Line-level staging is available in the diff of text resources |
| Discard | Row button or Delete: confirms with the file names and counts: "Discard changes to 'spr_player'? This cannot be undone." (ux-standards section 7) |
| Commit | Needs a message and at least one staged file (`gitpanel.commit`, Ctrl+Enter). The message follows a 72-character wrap guide. "Commit" menu: Commit, Commit and Push, Amend Last Commit, Undo Last Commit (soft reset) |
| Branch | Status bar branch item or Switch Branch... lists local, remote and recent branches with search and "Create new branch from '{branch}'..."; switching with unsaved workspace edits first asks to save the project folder |
| Pull, Push, Fetch | Header Sync menu; progress in Jobs; credentials come from the user's Git credential helper, never stored by G3MStudio |
| History | Expand the History section: commit list (graph optional) with author, date, message; selecting a commit lists its files; double-click a file opens the diff; right-click: Revert To This Revision, Copy Hash, Checkout |
| Stash | Stash Changes... with a name (git.stash) |
| Clone, Init | `git.clone` takes a URL and folder; `git.init` offers to create a repository for the project folder with a default `.gitignore` (ignoring `.g3mstudio/`) |

## 4. Resource-aware diff (F-GIT-2)

Double-click a changed row opens the diff in an editor tab using the views of [data-diff.md](data-diff.md): code as a text diff, images side by side, and rooms and objects as a property diff. Editor gutters and Explorer rows show Git letters and colours for changes against HEAD, in addition to the baseline markers of the Changes view.

## 5. Merge conflicts (F-GIT-4)

The Merge Conflicts section lists conflicted resources. Buttons per file: Use Mine, Use Theirs, Merge. For text resources (code, JSON) Merge opens a three-way view: Incoming left, Current right, Result below, with per-conflict Accept Current, Accept Incoming, Accept Both, and a base view toggle; "Complete Merge" stages the file. For data files, Merge uses the patch merge engine from patches.md. An external merge tool can be set in Settings > Paths with macros `${scm_mine}`, `${scm_theirs}`, `${scm_base}`, `${scm_merged}` (defaults offered for KDiff3, Meld, TortoiseMerge, DiffMerge, Notepad++). After a pull, revert or checkout that changes files, the file watcher offers Reload (section 14.3 of shell.md).

## 6. Actions and shortcuts

| Command | Id | Windows and Linux | macOS |
| --- | --- | --- | --- |
| Commit Staged Changes | `gitpanel.commit` | Ctrl+Enter | Cmd+Enter |
| Stage or Unstage Selected | `gitpanel.toggleStage` | Space | Space |
| Open Diff | `gitpanel.openDiff` | Enter | Enter |
| Discard Changes... | `gitpanel.discard` | Delete | Delete |
| Refresh | `gitpanel.refresh` | F5 | F5 |

## 7. Validation and messages

| Case | Message |
| --- | --- |
| Empty message | "Enter a commit message." |
| Nothing staged | "Stage at least one change to commit." |
| Dirty switch | "Save the project folder before switching branches. [Save and Switch] [Cancel]" |
| Push rejected | "Could not push. The remote has changes you do not have. Pull first, then push again." |
| Authentication failed | "Could not sign in to the remote. Check your Git credentials in your Git credential manager." |
| Git command failed | Error pattern with the command and its output under Details |

## 8. Undo

Git operations are not part of the workspace undo stack. Discard is not undoable and confirms. Undo Last Commit is a soft reset and is itself offered as "Redo Commit" through the reflog message in the notification.

## 9. Empty and loading states

- Not a repository: "This folder is not a Git repository. [Initialize Repository] [Clone Repository...]"
- No changes: "No changes. Your project matches the last commit."
- Loading status: skeleton rows; status runs as a background job and never blocks typing.

## 10. Parity coverage

- UTMT-PROJ-12 (backup and restore of game files a project overwrites): data-file protection is in run-game.md section 5.3; project-level changes are protected here by history.
- F-GIT-1, F-GIT-2, F-GIT-4 (requirements, not parity IDs): sections 3 to 5.
- UTMT-PROJ-03 (save project exports marked assets to the folder): the Git panel tracks the result; see projects.md.
