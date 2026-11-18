# VS Code and Zed: interaction patterns for G3MStudio

Research date: 2026-10-25.

## Versions studied

| Editor | Stable version | Published | Source |
|---|---|---|---|
| Visual Studio Code | 1.143.1 (1.143 plus one patch) | 2026-10-22 | https://api.github.com/repos/microsoft/vscode/releases/latest and https://code.visualstudio.com/updates/v1_143 |
| Zed | 1.25.3 | 2026-10-23 | https://api.github.com/repos/zed-industries/zed/releases/latest and https://zed.dev/releases/stable |

The Zed install on this machine reports 1.25.3 (stable channel build 382) in the Zed.exe file properties, so it matches the docs read here. No Zed settings were changed. Only file names under the install and data folders were listed.

VS Code stable now ships weekly (https://code.visualstudio.com/docs/setup/setup-overview). The 1.143 notes are dominated by features that do not apply to G3MStudio, so this report uses the stable docs pages instead of the release notes for behavior, and the release notes only for versions and dates.

Zed's documentation is in a transition. The 1.2x docs describe a threads sidebar with several projects per window and a choice between a classic and a newer panel layout (https://zed.dev/docs/windows-and-projects). Only the parts that matter for a data editor are used below.

Scope note on evidence. Each claim cites the page it came from. Where a page was silent, this report says "not documented" and does not fill the gap from memory, except for the two cases marked "unverified".

## 1. Workspace model

### VS Code

How it works. A workspace is the set of folders open in a window. Single-folder state lives in `.vscode/`. A `.code-workspace` JSON file lists `folders` (relative paths allowed) and holds workspace-level settings, tasks and launch configs. Adding a second folder creates an `untitled.code-workspace` that is restored on restart until the user saves or deletes it. In multi-root workspaces, settings resolve in this order: user, workspace file, then per-folder, and the Settings editor shows a Folder Settings scope (https://code.visualstudio.com/docs/editing/workspaces/workspaces).

Session restore. VS Code reopens the last folder, open files and layout. `window.restoreWindows` takes `all`, `none`, `one` or `folders` (https://code.visualstudio.com/docs/getstarted/userinterface). Hot exit is `files.hotExit` with `off`, `onExit` and `onExitAndWindowClose`. Unsaved edits are backed up to `%APPDATA%\Code\Backups` on Windows and restored after an unexpected close (https://code.visualstudio.com/docs/editing/codebasics). The page does not state the default value of either setting (unverified: `onExit` and `all` are the commonly seen defaults).

Auto save. `files.autoSave` is `off` (default), `afterDelay` (with `files.autoSaveDelay`, default 1000 ms), `onFocusChange` or `onWindowChange`. A dot on the tab marks unsaved changes and the Explorer shows a count badge.

Local history. The Timeline view at the bottom of Explorer lists local saves and Git commits for the active file. Entries can be compared, restored or deleted. Per search results of the docs, the defaults are 50 entries per file, 256 KB maximum file size and a 10 second merge window (`workbench.localHistory.*`). The dedicated local history docs page returned 404, so these numbers come from the user interface and release note pages and should be re-checked in a running build (https://code.visualstudio.com/docs/getstarted/userinterface, https://code.visualstudio.com/updates/v1_66).

Workspace Trust. A new folder opens in Restricted Mode. Text editing always works. Terminal, tasks, debugging, workspace settings and extensions that did not opt in are disabled. A banner, a status bar badge and a Workspace Trust editor let the user trust a folder. Trusting a parent trusts its children. Settings that name executables (`git.path`, terminal executables) are honored only from user settings, never from workspace settings (https://code.visualstudio.com/docs/editing/workspaces/workspace-trust, https://code.visualstudio.com/docs/configure/settings).

Welcome and recents. The page set read here does not describe the Welcome page or the Recent list in detail. Both exist in the product (File > Open Recent, the Get Started tab), but nothing citable was found in the pages fetched.

What is good.
- Hot exit plus backups mean the user never sees a "save changes?" prompt on shutdown.
- Restricted Mode keeps the app useful (browse, read, edit text) while blocking anything that runs code. The trust decision is stored per folder and inherited down the tree.
- The security rule that executable paths come only from user settings is simple and prevents a whole class of attacks.
- Local history is independent of Git, so it protects folders that are not repositories.

What is weak.
- `.code-workspace` plus `.vscode/` plus folder settings gives three places for the same setting. Users do not know which file won.
- Multi-root is powerful and confusing. Many commands act on "the first folder".
- Local history is silent on large files (256 KB cap), which is the wrong default for binary-ish data files.

### Zed

How it works. Opening a folder defines the project. There is no workspace file. Extra roots are added with File > Add Folder to Project. Several projects can sit in one window, listed in the threads sidebar like browser tabs, and each project keeps its own file tree, git state, search scope and split layout. Per-project overrides go in `.zed/settings.json` (https://zed.dev/docs/windows-and-projects, https://zed.dev/docs/migrate/vs-code).

Recents and welcome. Open Recent (ctrl-r) lists past projects. Enter opens in the current window, ctrl+Enter in a new window. The welcome screen appears in the center pane only when no folder is open and offers open folder, clone and docs (https://zed.dev/docs/getting-started).

Session restore. `restore_on_startup` is `last_session` by default, with `last_workspace`, `empty_tab` and `launchpad` as alternatives. `session.restore_unsaved_buffers` is `true` by default, so Zed does not prompt on close and brings dirty buffers back (https://zed.dev/docs/configuring-zed). Autosave modes: `off` (default), `on_focus_change`, `on_window_change`, `after_delay` with milliseconds. Closing a dirty tab always saves. This machine has a state database under `%LOCALAPPDATA%\Zed\db` split into `0-stable` and `0-global`, so state is per release channel.

Worktree Trust. Every new worktree starts in Restricted Mode. Zed then ignores `.zed/settings.json` and refuses to install or start language servers and tool servers. Globally installed tools still run. An exclamation icon in the title bar opens the trust modal. Trust persists across restarts, is tracked separately for local, SSH and WSL, and trusting a directory trusts its children and later subdirectories. `session.trust_all_worktrees` exists but the docs advise against it, and auto-trusted worktrees are not saved. `workspace::ClearTrustedWorktrees` resets everything and restarts Zed (https://zed.dev/docs/worktree-trust).

Local history, timeline, backups. Zed documents none of these. File history exists only through Git (File History, from the project panel, git panel or tab). Unsaved buffers survive restart, but there is no snapshot-per-save feature.

What is good.
- "Folder is the project" is the lowest-friction mental model and needs no extra file.
- Restored dirty buffers by default, one setting to turn off.
- The trust indicator lives in the title bar where it stays visible, not in a dismissible banner.
- Trust state is inspected and cleared with named actions.

What is weak.
- No local history. A bad save cannot be undone after the buffer closes unless the folder is under Git.
- Trust and session state are in a database, not a readable file, so support cannot inspect them.
- Multi-project windows are new and the docs still describe two panel layouts (classic and a newer one), which signals churn.

### Recommendation for G3MStudio

1. Use "folder or data file is the workspace". Opening a GameMaker project folder or a single data file is the whole model. Skip a workspace file format and multi-root in v1. Write per-project state to a single JSON file in the project (name it `.g3mstudio/workspace.json`, ignored by version control) and per-user state to `%APPDATA%\G3MStudio`.
2. Restore on startup: last workspace, open tabs, active tab, dock sizes. Default on, one setting to change to "empty window".
3. Hot exit for edited data: keep unsaved edits as a journal in `%LOCALAPPDATA%\G3MStudio\backups` and restore silently. Show a "Restored unsaved changes" toast with a Discard link. Never block exit with a save prompt.
4. Auto save: default off, offer `after delay` and `on focus change`. For data files that other tools read, never default to on.
5. Add local history now, not later. On every save, copy the previous file contents to `%LOCALAPPDATA%\G3MStudio\history\<hash>\<timestamp>`. Keep 50 entries per file like VS Code, but lift the size cap to a total budget (for example 2 GB, oldest first) because data files are large. Expose a Timeline panel with Compare and Restore.
6. Add Restricted Mode only if a project can trigger code or external tools (scripts, import commands, asset converters). If it can, copy the Zed model: ignore project-level tool paths until trusted, show a title bar indicator, store trust per folder with inheritance, and take executable paths only from user settings.
7. Welcome page: shown only when no workspace is open. Contains Open Folder, Open File, a Recent list (with Remove from list) and a link to docs. Closing the last tab does not bring it back.

## 2. Layout

### VS Code

Parts. Activity Bar, Primary Side Bar, Secondary Side Bar (opposite side, shown by default for folders), Panel, Editor area, Status Bar (https://code.visualstudio.com/docs/getstarted/userinterface, https://code.visualstudio.com/docs/configure/custom-layout).

Moving things. Views and panels drag between the Primary Side Bar and the Panel, or onto each other to form groups. Keyboard equivalents are View: Move View and Move Focused View. Panel positions: left, right, top or bottom, and bottom-panel alignment `center`, `justify`, `left`, `right`. Activity Bar position: default, top, bottom, hidden, plus a compact size. Maximize panel is a toggle.

Editor groups. Splitting creates a group, with predefined grid layouts under View > Editor Layout. Groups drag to rearrange. Locked groups hold an editor stable (terminals auto-lock). An editor tab can be dragged out of the window into a floating window, which restores position after restart.

Tabs. Preview mode via `workbench.editor.enablePreview` (single-click opens a replaceable tab), pinned tabs with `pinnedTabSizing` (`normal`, `shrink`, `compact`) and an optional separate row, `wrapTabs`, `showTabs` (`multiple`, `single`, `none`), close-empty-groups control and double-click-to-maximize options. Ctrl+Tab cycles recent editors.

Modes. Zen Mode (`zenMode.*` settings, Ctrl+K Z, double Esc exits), Centered Layout, Full Screen. A Customize Layout control in the title bar toggles each part. Settings, Keyboard Shortcuts, Profiles and Workspace Trust open as centered modal editors by default (`workbench.editor.useModal`: `off`, `some`, `all`).

Reset and saved layouts. View: Reset View Locations returns views to defaults, Reset All Menus restores hidden toolbar actions, and Restore Defaults in Customize Layout resets layout settings. Layout can be saved inside a Profile (settings, keybindings, extensions, UI layout), tied to a folder, exported as `.code-profile` or a gist. Temporary profiles exist for the current session (https://code.visualstudio.com/docs/configure/profiles).

What is good: everything can be moved, there is a visible reset, and the layout is saved per profile. The Secondary Side Bar lets two views be visible at once.

What is weak: too many ways to hide the same thing (menu, context menu, command, setting). Reset is split across three commands. Floating windows and groups multiply state that has to be restored.

### Zed

Parts. Center (one or more panes in a split tree) plus up to three docks (left, right, bottom). A dock holds panels, a pane holds items. Panels are the project panel, outline panel, git panel, terminal, debugger and others (https://zed.dev/docs/development/glossary).

Docks. Each panel has a `dock` setting. Project, outline, git and collaboration panels allow left or right. Terminal and debugger allow left, right or bottom. Default widths are 240 (project), 300 (outline), 360 (git), 640 wide and 320 tall (terminal). `bottom_dock_layout` (`contained`, `full`, `left_aligned`, `right_aligned`) decides whether the bottom dock spans the window. `resize_all_panels_in_dock` makes panels in a dock resize together. Each panel's status bar button can be hidden by its `button` setting (https://zed.dev/docs/visual-customization). Toggle shortcuts: left dock Ctrl+B, bottom dock Ctrl+J, right dock Ctrl+R or Ctrl+Alt+B (https://zed.dev/docs/migrate/vs-code).

Drag to dock. The docs do not describe dragging a panel into another dock. In the source file for docks (`crates/workspace/src/dock.rs`) the only drag handler found is for resizing a dock (`DraggedDock`). So docking is changed through settings, not by dragging.

Panes and tabs. Splits with Ctrl+K and arrow keys. Preview tabs are on by default: one preview tab per pane, shown in italics, promoted by editing, double-click or pinning. Defaults differ by source: project panel and multibuffer on, file finder off. `tabs.close_position`, `show_close_button` (`hover`), `activate_on_close` (`history`), `git_status` and `show_diagnostics` (tab colored by error) are all settings. `max_tabs` caps tabs per pane. Ctrl+Tab opens a tab switcher sorted by recent use, and Ctrl+Backspace closes the highlighted tab from the switcher (https://zed.dev/docs/tab-switcher).

Layout modes. Centered layout settings (`left_padding`, `right_padding`) are in a commented-out "TBD" block in the docs. Zen mode is not documented. No named layout presets or saved layouts besides the classic/new layout toggle.

What is good: the dock model is small and predictable (three docks, each panel has one setting). Tab diagnostics coloring and the project panel git colors show state without extra UI. The tab switcher is the best "recent files" control of the two.

What is weak: no reset layout command, no saved layouts, no drag to redock, and zen/centered mode are undocumented.

### Recommendation for G3MStudio

1. Fixed regions, not a free grid. Left side bar (project tree, outline), right side bar (inspector/properties), bottom panel (problems, output, search results, history), center editor area with at most two split groups. Three docks like Zed, but allow the user to move a panel between left and right with a context menu item and a drag, which Zed lacks.
2. Activity strip on the far left with icons for the side bar views, like VS Code. It doubles as a visible list of what exists, which helps discoverability.
3. Tabs: preview tab (italic, single per group), pinned tabs (left, icon only), dirty dot replacing the close button, overflow chevron with a searchable list of all open tabs, Ctrl+Tab switcher in recency order. Middle-click closes. Offer Zed's colored tab by diagnostic state.
4. Layout persistence: save dock sizes, visibility, panel placement and tab groups in `workspace.json` per project and defaults in user settings.
5. One command "Reset Layout" in the View menu and the palette. Do not copy VS Code's three split reset commands.
6. Defer saved layout profiles and zen mode. Add a simple "Focus editor" toggle (hide all docks, restore on second press) as the cheap form of zen mode.

## 3. Command discovery

### VS Code

Command Palette (Ctrl+Shift+P) lists everything, `?` shows prefixes. Quick Open (Ctrl+P) finds files; `@` inside it goes to a symbol, `:` goes to a line, Ctrl+Enter opens to the side. Go to Symbol in file is Ctrl+Shift+O, in workspace Ctrl+T. Settings `workbench.editor.enablePreviewFromQuickOpen` controls preview behavior (https://code.visualstudio.com/docs/getstarted/userinterface, https://code.visualstudio.com/docs/editing/editingevolved).

Keybindings. A Keyboard Shortcuts editor (Ctrl+K Ctrl+S) lists every command with or without a binding and searches by name or by pressed keys. `keybindings.json` rules have `key`, `command`, optional `when` and `systemWide`. Rules are evaluated bottom to top, user rules append after defaults. Prefix a command with `-` to remove a binding. Chords are two keypresses (`ctrl+k ctrl+w`). `when` clauses use context keys with `==`, `!=`, `&&`, `||`, `=~`. "Show Same Keybindings" finds conflicts. Scan-code bindings (`[Slash]`) stay on the physical key on non-US layouts (https://code.visualstudio.com/docs/configure/keybindings).

Orientation aids. Breadcrumbs show file path plus symbol path (`breadcrumbs.*` settings, Ctrl+Shift+. focuses them). Outline is a section at the bottom of Explorer with sort modes, cursor tracking and a filter box. The status bar holds branch, problems, language and the notification bell. Menus exist, but the palette is the complete list.

What is good: the palette is truly complete, shortcuts are shown next to commands, and the key-recording search in the shortcuts editor solves "what is bound to this key".

What is weak: `when` contexts are hard to discover and debug. Keyboard layout handling is a recurring pain.

### Zed

Command Palette (Ctrl+Shift+P) runs actions in the form `namespace: action`. File Finder (Ctrl+P) also reaches symbols and commands. Symbols: Ctrl+Shift+O (file), Ctrl+T (project). Outline panel (Ctrl+Shift+B) is a persistent symbol tree that follows the cursor and is useful over multibuffers. Project panel is Ctrl+Shift+E. Tab switcher Ctrl+Tab (https://zed.dev/docs/command-palette, https://zed.dev/docs/finding-navigating, https://zed.dev/docs/outline-panel).

Keybindings. `keymap.json` is an array of `{context, bindings}` objects. Contexts form a tree rooted at `Workspace`, predicates support `&&`, `||`, `!`, parentheses and `>`. A deeper context wins, and at the same depth the later binding wins. Setting an action to `null` unbinds it. Pending chords show in the status bar and a which-key style list appears after a delay (`which_key.delay_ms`). `dev: open key context view` shows the active contexts, which solves the debugging problem. `base_keymap` selects Zed, VS Code, JetBrains, Sublime Text, Atom, Emacs, TextMate, Cursor or None. A keymap editor (Ctrl+K Ctrl+S) lists every action with its binding and edits `keymap.json` (https://zed.dev/docs/key-bindings).

What is good: base keymap choice lowers the switching cost to zero. The context viewer and the pending-keys indicator make chords usable.

What is weak: the palette docs omit history ordering and aliases. Action names are developer-style strings (`workspace: new file`), not task language.

### Recommendation for G3MStudio

1. One command registry: id, title, category, default keybinding, enable predicate (context key). Every menu item, toolbar button, context menu and palette entry calls a command id. This is the structural change that makes the rest cheap.
2. Command palette with fuzzy match, recency boost, category prefix, and the shortcut shown on the right. Prefix modes in one input box: `>` commands (default), none for files and assets, `@` for entries in the open data file, `:` for a row or index, `#` for a global symbol (object or sprite name).
3. Quick Open over the project tree and over entries inside open data files (sprites, objects, rooms, strings). This is the main speed feature for a data editor.
4. Keybinding editor with key-recording search, conflict list, and a JSON override file. Use a small set of context keys (for example `editorFocus`, `treeFocus`, `dirty`, `readOnly`). Support chords, show pending chord in the status bar. Ship a "VS Code" base keymap since many users already know it.
5. Breadcrumbs for the path inside the open data file (file > chunk > entry > field), and an Outline view that follows the selected entry.
6. Menus remain the guided path and always show the shortcut. The palette is the complete list. Anything only reachable by a menu is a bug.
7. Status bar: left side holds workspace-level items (dirty count, problems count, background task), right side holds contextual items (file format, selection, encoding, zoom). Follow https://code.visualstudio.com/api/ux-guidelines/status-bar: few items, short labels, icons only with a clear meaning.

## 4. Settings

### VS Code

Two surfaces on one store: a Settings editor (search as you type, tree grouped by category, "Commonly Used" group, colored bar on modified settings, gear menu with Reset Setting, Copy ID, Copy as JSON and a `vscode://settings/<id>` link) and `settings.json` with IntelliSense and error squiggles. Filters: `@modified`, `@id:`, `@ext:`, `@lang:`, `@feature:`, `@tag:`. Scopes in precedence order: default, user, remote, workspace, folder, with language-specific `"[lang]"` blocks. Objects merge, primitives and arrays replace. Policy settings from an administrator override everything. Windows path: `%APPDATA%\Code\User\settings.json`. Profiles can hold their own settings and are keyed to folders. Settings Sync copies settings, keybindings, snippets, tasks, UI state, extensions and profiles via a Microsoft or GitHub account, with a conflict diff editor, a sync log, up to 20 remote backups per category and a machine list (https://code.visualstudio.com/docs/configure/settings, https://code.visualstudio.com/docs/configure/profiles, https://code.visualstudio.com/docs/configure/settings-sync).

Good: the modified marker, per-setting reset and copy-as-JSON link, and the same setting being reachable from UI and file. Weak: the settings list is huge and some settings (colors) cannot be edited in the UI at all.

### Zed

A Settings Editor (Ctrl+,) with search by setting name, plus `settings.json` with an inline "or add this to settings.json" hint in the docs. Project settings go in `.zed/settings.json` and override or extend user values, with `"..."` meaning "keep the inherited list". `profiles` hold named temporary overrides with a `base` of `user` or `default`. `zed: import vs code settings` imports editor, files, terminal, tabs and git settings, but not keybindings or extensions (https://zed.dev/docs/configuring-zed, https://zed.dev/docs/migrate/vs-code). The Settings Editor UI is not described in the docs pages read, so its scopes are not documented here. There is no sync service.

Good: JSON stays the source of truth and the UI writes to it. The `"..."` inheritance token solves list merging. Weak: open issues show the UI and file drifting (for example https://github.com/zed-industries/zed/issues/59949, the UI ignores the file's indentation).

### Recommendation for G3MStudio

1. One settings schema in code (key, type, default, description, scope, category, restart-required flag). Generate both the Settings page and the JSON schema from it.
2. Settings page: search box on top, category list on the left, a blue bar on modified settings, a per-setting Reset, and "Open settings.json" button. Writes go through the same store as the file, and the file is watched so edits show up live.
3. Two scopes only: User and Workspace. Mark each setting as user-only or both. Machine-sensitive settings (external tool paths, executable locations) are user-only, as in VS Code.
4. Pretty-print the JSON the same way every time, keep comments if the user added them, and keep a `.bak` of the previous file before rewriting.
5. Skip profiles and sync for v1. If sync is wanted later, the schema flag "synced" is the hook.
6. Offer "Import settings from file" later, not a cloud account.

## 5. Search

### VS Code

Ctrl+Shift+F searches across files. Include and exclude boxes take comma-separated globs (`**` is assumed as a prefix in this view). Toggles for match case, whole word and regex. A Replace box shows a diff preview for pending changes and can replace everywhere, in one file or one hit. "Use Exclude Settings and Ignore Files" honors `search.exclude` and `.gitignore` (`search.useIgnoreFiles` defaults to true). Results are grouped per file in a tree with a single-click preview. A Search Editor opens results as a document with context lines (default one) and syntax highlighting. Up/Down in the box recall earlier searches (https://code.visualstudio.com/docs/editing/codebasics).

### Zed

Project search (Ctrl+Shift+F) opens its results in a multibuffer: one editable view that stitches excerpts from many files, with a divider line per excerpt. Editing an excerpt edits the real file, and Save writes all. The same multibuffer form is used for diagnostics (Ctrl+Shift+M), find-all-references, go to definition with several targets, and the project git diff. Multiple cursors work across excerpts (Ctrl+D next match, Ctrl+Shift+L all matches). `editor: open excerpts` jumps to a file, or `"double_click_in_multibuffer": "open"` makes double-click do it. The Outline panel shows the files in a multibuffer as a tree (https://zed.dev/docs/multibuffers, https://zed.dev/docs/outline-panel). The Zed pages read do not document regex, case, whole-word, include/exclude or replace options for project search.

### Comparison

| Topic | VS Code | Zed |
|---|---|---|
| Result shape | Tree of files and hits, optional Search Editor | One editable multibuffer |
| Replace | Built-in with preview diff | Edit in place with multi cursor |
| Filters | include/exclude globs, ignore-file toggle | Not documented on the pages read |
| Cost to implement | Moderate | High (a composite editor) |

### Recommendation for G3MStudio

1. Ship VS Code style search first: scope box (current file, open files, project, selection), case/word/regex toggles, include/exclude globs, a results tree (file > hits) with single-click preview, and replace with a preview diff and a per-hit checkbox. Search targets are strings, names and values inside data files, so results show the entry path (for example `objects/obj_player > Create > line 12`) not just a line number.
2. Keep search results in the bottom panel, with history of the last 10 queries.
3. Do not build a multibuffer for v1. The same value is gained by "Replace all in this result set" with a preview. Revisit if bulk editing across many entries is a top request.
4. The same result list component also renders Problems, References and Find Usages, so write it once.

## 6. Problems, output, notifications, progress

### VS Code

Problems panel (Ctrl+Shift+M) lists errors and warnings, the status bar shows counts, Alt+F8 steps through them in the file, and the lightbulb in the margin offers quick fixes (https://code.visualstudio.com/docs/editing/editingevolved). The Output panel has per-source channels (for example the sync log). Notifications appear bottom right as information, warning or error, are kept in a history reached from the bell in the status bar, and support per-source silencing and a global Do Not Disturb that also hides errors (https://code.visualstudio.com/docs/getstarted/userinterface). The extension UX guidelines are the clearest written policy: low-priority progress goes in the status bar, a progress notification is a "last resort", combine notifications, offer "Do not show again", never promote or ask for feedback on first install, and use a modal only when a decision is needed now (https://code.visualstudio.com/api/ux-guidelines/notifications, https://code.visualstudio.com/api/ux-guidelines/status-bar).

### Zed

Diagnostics show as underlines, scrollbar marks and hover text. `diagnostics_max_severity` filters by severity. Optional inline messages (`diagnostics.inline.enabled`, off by default). The project panel and tabs can be colored by error state (`project_panel.show_diagnostics` defaults to `all`, `tabs.show_diagnostics` defaults to `off`). Project-wide diagnostics open as a multibuffer (Ctrl+Shift+M). The UI checklist requires visible progress for slow work and a frame budget of 8 ms (https://zed.dev/docs/diagnostics, https://zed.dev/docs/development/ui-checklist). The pages read do not describe an output panel or the toast system.

### Recommendation for G3MStudio

1. A Problems panel is the home for all validation (broken references, missing assets, invalid values). Each item has severity, message, location (file and entry path) and a Fix action when one exists. Double-click navigates. A status bar item shows error and warning counts and opens the panel.
2. Color errors on tree nodes and tabs (Zed) so problems are visible without opening the panel.
3. An Output panel with named channels: Load, Save, Validation, Updates, Export. Channels also write to a rolling log file that the user can attach to a bug report.
4. Notifications: toast only for results of a user action or a recoverable failure. Rules copied from the VS Code guidelines: one at a time, merge duplicates, errors persist until dismissed, info auto-hides after about 5 seconds, every toast lands in the history behind a bell. No promotional toasts. Include a Do Not Disturb that does not hide errors, because hiding errors is a documented VS Code complaint ("Global Do Not Disturb also hides error notifications").
5. Progress: status bar item with a spinner and a click target that opens the Output channel; a determinate bar inside the affected view for loading a big data file (with Cancel); a blocking modal only for operations that must not be interrupted.

## 7. Git and source control

### VS Code

Source Control view (Ctrl+Shift+G) with Changes and Staged Changes lists, file stage with `+`, drag to stage, and Stage Selected Ranges for lines or blocks from the diff editor. Gutter bars: green added, blue modified, red triangle deleted, click opens an inline diff peek (`scm.diffDecorations*` settings). Commit box with Up/Down to cycle past messages, a longer message opens `COMMIT_EDITMSG` as a tab, amend and Undo Last Commit under More Actions. The Source Control Graph shows commit history with incoming and outgoing markers and offers cherry-pick and detached checkout. Timeline shows a file's commits and local saves. Blame is inline (`git.blame.editorDecoration.enabled`) or in the status bar. The status bar branch item opens a picker with local, remote and recent branches. Stash supports tracked, untracked and staged-only (Git 2.35 or later). Worktrees are created from the repositories view. The 3-way merge editor shows Incoming left, Current right and Result below, with per-conflict accept controls, a base view and a Complete Merge button that stages the file (https://code.visualstudio.com/docs/sourcecontrol/overview, /staging-commits, /history, /branches-worktrees, /merge-conflicts).

Good: line-level staging from the diff, local saves and commits in one Timeline, the graph. Weak: the merge editor's Complete Merge does not finish the operation, which surprises people. The 1.143 notes still list the multi-file diff list as experimental.

### Zed

Git panel (docked left by default, movable) shows Tracked and Untracked sections, checkboxes per entry and per section, and a commit editor at the bottom (Ctrl+Enter commits). It wraps messages at 72 characters by default and has an Uncommit button (soft reset). A project diff multibuffer (ctrl-g d) shows all changes as editable excerpts with `git: stage and next`. Hunk and file staging are documented, line-level staging is not. Inline diff hunks expand with `editor: expand all diff hunks`, Esc collapses. Split (side-by-side) is the default, unified is a setting (`diff_view_style`), and 1.22 added a toggle for it. Branch picker via `git: switch`, stash picker with apply, pop and drop, File History from the panel, tab or project panel, inline blame, worktree picker, and tag creation (1.24.2). Merge conflicts show per-conflict buttons for current, incoming or both, with the current branch in green and the incoming branch in blue. There is no commit graph documented (https://zed.dev/docs/git, https://zed.dev/releases/stable).

Good: the whole git flow fits one panel plus one diff view. The conflict buttons are enough for most conflicts. Weak: no graph, no documented line-level staging, and the commit editor is a second place for the same text.

### Recommendation for G3MStudio

G3MStudio edits data files, so it needs to coexist with Git, not replace a Git client.
1. P1: show Git status colors and letters (M, A, U) on the project tree and tabs, and the branch name in the status bar. Read-only, via LibGit2Sharp or the `git` CLI.
2. P1: a gutter or row marker for entries changed against HEAD in the open data file, with a click to see the old value (inline peek like VS Code). This is the data-editor form of the gutter diff.
3. P1: a diff view for two versions of one data file (HEAD vs working, or two history entries) that compares by entry, not by line. Use the same view for Local History compare.
4. P2: Stage, commit and push from the app. If built, copy Zed's single panel with a commit box, not a separate view per action.
5. P2: a 3-way merge view only if conflicts in data files turn out to be common. Start with "Open in external merge tool" and copy VS Code's Incoming/Current/Result layout if it is built.
6. Skip: commit graph, stash UI, worktrees, blame.

## 8. Updates

### VS Code

Channels: Stable (weekly) and Insiders (nightly, side by side with Stable, own settings). Windows User setup installs to `%LOCALAPPDATA%\Programs\Microsoft VS Code`, needs no admin and supports smoother background updates; System setup under Program Files needs elevation, and a User install run as Administrator cannot update. ZIP installs update manually. Linux updates through the apt or dnf repo set up by the package. `update.mode` is `default`, `start`, `manual` or `none` (the FAQ names `default` to `none` as the opt-out). The 1.109 release (2026-02-04) redesigned the Windows layout to use versioned package paths modeled on Chromium's updater, because the old atomic file replace could leave the app broken if interrupted. On macOS a second pending update replaces the first, so users restart once. Rollback is manual: uninstall and install an older version from a versioned URL (`https://update.code.visualstudio.com/1.83.1/...`), then set `update.mode` to `none`. A release notes tab opens after an update, and the editor offers a checkbox to stop showing it (`update.showReleaseNotes`) (https://code.visualstudio.com/docs/setup/setup-overview, /setup/windows, /supporting/faq, /updates/v1_109).

### Zed

Channels: Stable, Preview (about a week ahead of Stable), Nightly (checked every 15 minutes in source), plus Dev. The `auto_update` setting is on by default and the poll interval for stable is one hour (`crates/auto_update/src/auto_update.rs`). On macOS and Windows the user installs once and Zed then checks periodically; on Linux there is an install script with `ZED_VERSION` and `ZED_CHANNEL`. winget and Homebrew are supported (https://zed.dev/docs/installation, https://zed.dev/docs/windows, https://zed.dev/docs/update).

How the Windows update works, from the source: Zed downloads the new build in the background, cannot overwrite a running `Zed.exe`, so on quit or restart it launches `auto_update_helper.exe` (present in this machine's `tools` folder with a log file). The helper runs a list of jobs, each with an `apply` and a `rollback` function, which means a failed update restores the previous files. It uses the Windows Restart Manager to close handles. After updating, "View Release Notes" opens the web page `/releases/<channel>/<version>`. The title bar shows a confirmation when a manual check finds no update (1.22.0). The docs say nothing about downgrade beyond installing a specific version on Linux (https://github.com/zed-industries/zed/tree/main/crates/auto_update_helper).

### Comparison

| Topic | VS Code | Zed |
|---|---|---|
| Install scope on Windows | User or System setup | Per user, `%LOCALAPPDATA%\Programs\Zed` (as installed here) |
| Apply | Background download, prompt, restart | Background download, applied on quit or restart by helper |
| Failure safety | Versioned paths since 1.109 | Per-step rollback in helper |
| Channels | Stable, Insiders | Stable, Preview, Nightly |
| Release notes | In-app tab after update | Opens a web page on demand |
| Rollback by user | Manual reinstall | Not documented |

### Recommendation for G3MStudio

1. Per-user install in `%LOCALAPPDATA%\Programs\G3MStudio`, no admin prompt. Required for background updates to work without elevation.
2. Background download, verify a hash and a signature, stage into a versioned folder (`app-1.4.2`), and switch a small launcher stub on next start. This is the 1.109 design and avoids the broken-state failure. Keep the previous version folder so rollback is one click: "Revert to previous version" in the update menu.
3. Never replace files in place while running. If a helper process is needed, copy Zed's apply/rollback job list.
4. Channels: Stable and Preview. Preview uses a separate install folder, settings folder and state, as Zed does per channel.
5. Release notes: after an update, show a toast "Updated to X. What's new" linking to a notes tab or page. A setting to stop showing it. Never open a tab by surprise.
6. Settings: `update.mode` equivalent with `automatic`, `check only`, `manual`, `off`. Show last checked time and a Check Now button, and report "up to date" visibly (the 1.22.0 behavior).
7. Preserve the unsaved-edits journal across an update restart so an update never costs work.

## 9. Localization

### VS Code

English ships in the box. Other languages are Language Pack extensions from the Marketplace. On first run VS Code checks the OS UI language and offers to install a matching pack. The command Configure Display Language installs the pack if missing and writes the locale to `argv.json`, then asks for a restart. `--locale=fr` selects a language for one session. Supported packs include Chinese (simplified and traditional), French, German, Italian, Spanish, Japanese, Korean, Russian, Portuguese (Brazil), Turkish, Polish, Czech and Hungarian. Packs come from the Community Localization Project, open to anyone. Language packs cover the UI only, not programming language support (https://code.visualstudio.com/docs/configure/locales).

Good: packs are separate from the app, so a language can be updated without a release, and the OS language is detected. Weak: needs a restart, the pack is a separate download, and the user must know the command.

### Zed

No UI localization. The issue "Localization and support for different locales" (https://github.com/zed-industries/zed/issues/7409) is open, and a request for right-to-left text (https://github.com/zed-industries/zed/issues/31102) is open as well. A third-party fork translates strings at build time (named zed-globalization, found by search) which shows demand but is not a runtime i18n system. Zed's UI checklist only asks for short, unambiguous strings (https://zed.dev/docs/development/ui-checklist).

### Recommendation for G3MStudio

1. Treat localization as a launch requirement, since the editor targets modders in many regions. Put every UI string in resource files keyed by id from the first commit, with no concatenated sentences, plural and format placeholders, and no text in images.
2. Ship translations as plain files in a `lang` folder next to the app, loaded at startup, so a translator can add a language without rebuilding (this matches how VS Code packs work, without a marketplace).
3. Language selection: on first run use the OS UI language if a file exists, else English. A setting `ui.language` (`auto` or a code) with a restart required notice. Show the setting in its own language ("Deutsch") so a user in the wrong language can find it.
4. Fallback chain: selected language, then English, then the key id. Log missing keys to the Output channel.
5. Test layout in German and Russian (long) and in a right-to-left language, with scaled text. Avoid fixed-width buttons.
6. Number, date and file size formats come from the OS culture and not from the UI language, with an override.

## 10. Performance techniques

### VS Code

Extensions run in a separate extension host so a slow or broken extension does not block the UI or startup, and are activated lazily by events (`onLanguage`, `onCommand`, `workspaceContains`, `onStartupFinished`); activating on `*` is discouraged and `onStartupFinished` does not slow startup (https://code.visualstudio.com/api/advanced-topics/extension-host, https://code.visualstudio.com/api/references/activation-events). Shell environment resolution has a 10 second timeout setting (`application.shellEnvironmentResolutionTimeout`) and the FAQ suggests excluding big folders with `files.exclude` for faster file search (https://code.visualstudio.com/docs/supporting/faq). The built-in command "Developer: Startup Performance" prints a startup profile (marks and extension activation times). The docs give no numeric startup budget, so none is cited here.

### Zed

Zed draws its own UI with its GPU framework. The author's description (https://zed.dev/blog/videogame) gives a frame budget of 8.33 ms at 120 Hz, a custom shader per primitive (rectangle, shadow, text, icon, image), instanced drawing to batch many items per call, rounded rectangles via signed distance fields, text shaped by the OS and cached by text-and-font pair, glyphs rasterized to an alpha atlas texture with up to 16 sub-pixel variants, and SVG icons rasterized once as tintable masks. On Windows Zed needs a DirectX 11 GPU (https://zed.dev/docs/windows). The UI checklist sets a frame ceiling of 8 ms and requires visible progress for slow tasks. Zed's performance doc is about profiling (samply, Tracy, `ztracing` spans, `zlog::time!` with `warn_if_gt`) and says to profile release builds only (https://zed.dev/docs/performance).

### Avalonia

The Avalonia performance guide recommends compiled bindings, `TreeDataGrid` for large or hierarchical data (it virtualizes), avoiding `DataGrid` unless editing is needed, shallow visual trees, setting `TextBlock.Text` instead of `Run` elements, `StreamGeometry` over `PathGeometry`, reduced-size thumbnails, fixing binding errors (each one costs time and trace output), avoiding `RelativeSource.FindAncestor` in `DataTemplate`, and async data loading (https://docs.avaloniaui.net/docs/guides/development-guides/improving-performance). That page does not cover startup options (ReadyToRun, trimming) or render diagnostics, so those need a separate check in the Avalonia docs before they are adopted.

### Recommendation for G3MStudio

1. Virtualize every list and tree that can exceed a few hundred rows: project tree, entries list, search results, problems, history. Use `TreeDataGrid` for the tree, `ItemsControl` with a virtualizing panel for flat lists. Do not use a non-virtualized `StackPanel` in an `ItemsControl` for lists.
2. Parse and index data files on a background thread, show the tree shell at once, and fill it as chunks complete. Keep the UI-thread work per frame under 8 ms as the Zed checklist does (use 16 ms if 60 Hz only).
3. Load lazily: open a data file as an index first and decode each entry (sprite, sound, code) when it is selected. Cache decoded previews with an LRU cap.
4. Startup budget: window and last layout visible in under 1 second on a warm start, workspace restoration after that. Defer the update check, Git status, search index and plugin loading until after first paint (the `onStartupFinished` idea). Measure with an in-app "Startup Performance" report in the Output panel.
5. Use compiled bindings everywhere (`x:CompileBindings`), treat binding errors as test failures, and keep a debug build flag that logs layout passes over a threshold.
6. Rendering: stay on Avalonia's default GPU path. Do not write custom shaders or a custom text renderer. The Zed approach pays off for a code editor with a single huge scrolling surface, and a data editor made of forms and trees does not need it. Reduce redraw cost instead: small templates, no nested transparency layers, thumbnails at display size.
7. Add an optional performance overlay (frame time, memory, loaded entry count) behind a hidden setting, as a counterpart to Zed's `zlog::time!` warnings.

## Adopt for G3MStudio (ranked)

| # | Pattern | Source | Why | Priority |
|---|---|---|---|---|
| 1 | Command registry with ids, titles, shortcuts and context predicates, used by menus, toolbar and palette | VS Code keybindings, Zed actions | Every later feature (palette, keybinding editor, menus) reuses it | P0 |
| 2 | Command palette and Quick Open with prefix modes (`>`, `@`, `:`, `#`), shortcut shown in rows | VS Code, Zed | Fast access to a large feature set and to entries inside data files | P0 |
| 3 | Folder or data file as the workspace, with state saved per project | Zed | No workspace file to explain, least friction | P0 |
| 4 | Session restore plus hot exit journal for unsaved edits, no save prompt on quit | VS Code `files.hotExit`, Zed `restore_unsaved_buffers` | Updates and crashes never cost work | P0 |
| 5 | Local history snapshot on every save with Compare and Restore | VS Code Timeline | Protects destructive edits to data files, Zed lacks it | P0 |
| 6 | Resource-file localization from the first commit, OS language detection, `ui.language` setting, English fallback | VS Code language packs | Retrofitting strings later is expensive; modders are international | P0 |
| 7 | Virtualized tree and lists, background parsing, lazy entry decoding, compiled bindings | Avalonia guide, VS Code lazy activation, Zed frame budget | Large data files are the main performance risk | P0 |
| 8 | Fixed dock regions (left, right, bottom, center) with visible panel buttons and one Reset Layout command | Zed docks, VS Code reset | Predictable layout, easy to save and restore | P0 |
| 9 | Preview tab, pinned tabs, dirty dot, tab overflow list, Ctrl+Tab recency switcher | Zed, VS Code | Opening many entries without tab clutter | P1 |
| 10 | Problems panel with status bar counts, tree and tab error coloring, Output channels | VS Code Problems/Output, Zed tab and tree diagnostics | One home for validation and logs | P1 |
| 11 | Settings page from a schema, search, modified marker, per-setting reset, `settings.json` with live reload, User and Workspace scopes | VS Code | Both beginners and power users are served by one store | P1 |
| 12 | Keybinding editor with key-record search, conflict view, JSON overrides, chords, and a VS Code base keymap | VS Code, Zed `base_keymap` | Lowers switching cost, solves conflicts | P1 |
| 13 | Search panel with scope, regex, globs, results tree and replace preview, shared result list with Problems and References | VS Code | Bulk renames of strings and names are common | P1 |
| 14 | Per-user install, background download, staged versioned folder, rollback to previous version, release notes toast | VS Code 1.109, Zed helper rollback | No admin prompt, safe against interrupted updates | P1 |
| 15 | Toast and notification history with a bell, merge duplicates, progress in status bar first | VS Code UX guidelines | Prevents notification spam, keeps errors visible | P1 |
| 16 | Restricted Mode with title bar indicator, executable paths from user settings only | Zed, VS Code Workspace Trust | Needed only if projects can run tools or scripts | P1 |
| 17 | Breadcrumbs and Outline for the path inside a data file | VS Code, Zed outline panel | Orientation in deep data trees | P1 |
| 18 | Git status colors on tree and tabs, branch in status bar, gutter marker for entries changed vs HEAD | VS Code, Zed | Data files live in repos; read-only integration is cheap | P1 |
| 19 | Entry-level diff view for two versions of a file, reused by Local History | VS Code diff editor, Zed split/unified toggle | Makes history and Git changes readable | P1 |
| 20 | Welcome page shown only with no workspace open; Recent list with remove | Zed, VS Code | Fast start without nagging | P1 |
| 21 | Auto save options (off by default, after delay, on focus change) | VS Code, Zed | Familiar, but off by default for shared data files | P2 |
| 22 | Release channels (Stable, Preview) with separate state folders | Zed | Lets testers run side by side | P2 |
| 23 | Which-key style chord hint and key context viewer for debugging bindings | Zed | Makes chords usable | P2 |
| 24 | Focus editor toggle (zen light) | VS Code Zen | Cheap form of zen mode | P2 |
| 25 | Stage, commit and push from the app | Zed git panel | Only if users ask; external clients work today | P2 |
| 26 | Editable multi-excerpt results view | Zed multibuffer | High value, high cost | P2 |
| 27 | Saved layouts or profiles, settings sync | VS Code Profiles, Settings Sync | Rarely used for a single-purpose tool | P2 |

## Avoid

1. Do not build a multi-root workspace file (`.code-workspace`). VS Code users already struggle with three setting locations and "first folder" behavior.
2. Do not scatter reset and hide controls across menu, context menu, setting and command. Give each toggle one command and one setting.
3. Do not make Settings and Keyboard Shortcuts modal by default. VS Code opens them as centered overlays by default (`workbench.editor.useModal` defaults to `some`), which hides the editor behind. Use normal tabs.
4. Do not rely on a banner to communicate trust. A dismissible banner gets dismissed. Use a persistent title bar or status bar indicator.
5. Do not replace app files in place while running. VS Code needed a Windows layout redesign in 1.109 because atomic replacement could leave the app broken.
6. Do not open release notes or tabs unprompted, and do not show promotional toasts or feedback requests, which the VS Code guidelines explicitly forbid.
7. Do not let a global Do Not Disturb hide errors.
8. Do not copy Zed's missing pieces: no local history, no layout reset, no documented zen mode, and trust and session state kept in a database that support cannot read. Also note Zed's docs lag the product (a keymap editor and settings editor with few details), so keep your own docs current with each release.
9. Do not ship localization as a build-time string replacement (the Zed fork approach) or hard-code strings and plan to extract them later.
10. Do not write a custom GPU renderer or custom text layout. Zed's approach (custom shaders, glyph atlas) is justified for a code editor drawing millions of glyphs, and Avalonia already provides GPU rendering.
11. Do not use `DataGrid` or non-virtualized panels for large lists (Avalonia guide), and do not nest `RelativeSource.FindAncestor` in item templates.
12. Do not start work at launch that is not needed for first paint (VS Code advises against `*` activation). Update checks, Git scans and indexing wait until after the window is usable.
13. Do not default auto save to on for data files that other tools or a game build read.
14. Do not add settings sync, an account system or a plugin marketplace before the core editor is stable.

## Source index

VS Code
- https://code.visualstudio.com/updates/v1_143
- https://code.visualstudio.com/updates/v1_109
- https://code.visualstudio.com/docs/getstarted/userinterface
- https://code.visualstudio.com/docs/configure/custom-layout
- https://code.visualstudio.com/docs/configure/profiles
- https://code.visualstudio.com/docs/configure/settings
- https://code.visualstudio.com/docs/configure/settings-sync
- https://code.visualstudio.com/docs/configure/keybindings
- https://code.visualstudio.com/docs/configure/locales
- https://code.visualstudio.com/docs/editing/workspaces/workspaces
- https://code.visualstudio.com/docs/editing/workspaces/workspace-trust
- https://code.visualstudio.com/docs/editing/codebasics
- https://code.visualstudio.com/docs/editing/editingevolved
- https://code.visualstudio.com/docs/sourcecontrol/overview
- https://code.visualstudio.com/docs/sourcecontrol/staging-commits
- https://code.visualstudio.com/docs/sourcecontrol/history
- https://code.visualstudio.com/docs/sourcecontrol/branches-worktrees
- https://code.visualstudio.com/docs/sourcecontrol/merge-conflicts
- https://code.visualstudio.com/docs/setup/setup-overview
- https://code.visualstudio.com/docs/setup/windows
- https://code.visualstudio.com/docs/supporting/faq
- https://code.visualstudio.com/api/ux-guidelines/notifications
- https://code.visualstudio.com/api/ux-guidelines/status-bar
- https://code.visualstudio.com/api/references/activation-events
- https://code.visualstudio.com/api/advanced-topics/extension-host

Zed
- https://zed.dev/releases/stable
- https://zed.dev/docs/llms.txt
- https://zed.dev/docs/getting-started
- https://zed.dev/docs/windows-and-projects
- https://zed.dev/docs/worktree-trust
- https://zed.dev/docs/configuring-zed
- https://zed.dev/docs/key-bindings
- https://zed.dev/docs/command-palette
- https://zed.dev/docs/finding-navigating
- https://zed.dev/docs/multibuffers
- https://zed.dev/docs/outline-panel
- https://zed.dev/docs/project-panel
- https://zed.dev/docs/tab-switcher
- https://zed.dev/docs/diagnostics
- https://zed.dev/docs/git
- https://zed.dev/docs/visual-customization
- https://zed.dev/docs/migrate/vs-code
- https://zed.dev/docs/installation
- https://zed.dev/docs/update
- https://zed.dev/docs/windows
- https://zed.dev/docs/performance
- https://zed.dev/docs/development/glossary
- https://zed.dev/docs/development/ui-checklist
- https://zed.dev/blog/videogame
- https://github.com/zed-industries/zed/blob/main/crates/auto_update/src/auto_update.rs
- https://github.com/zed-industries/zed/tree/main/crates/auto_update_helper
- https://github.com/zed-industries/zed/blob/main/crates/workspace/src/dock.rs
- https://github.com/zed-industries/zed/issues/7409

Avalonia
- https://docs.avaloniaui.net/docs/guides/development-guides/improving-performance
