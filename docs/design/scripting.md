# Scripting

Status: proposed. The C# script runner, the script console, the UTMT-compatible script API, why no scripts are bundled (the built-in replacements are in [script-features.md](script-features.md)), the script dialogs and the safety rules. Requirements: F-MOD-3, F-PAR-2, F-PAR-3, F-SUITE-1 (G3MTool scripts). Shared rules: [ux-standards.md](ux-standards.md). The Scripts menu and keys are in [shell.md](shell.md) (section 2 Scripts menu, section 16 Script console). Plugins (ADR-0009) are a different mechanism: a plugin extends the app, a script automates one data file.

## 1. Purpose and entry points

Modders have years of `.csx` scripts written for UndertaleModTool. G3MStudio runs them unchanged where G3MLib allows, with real dialogs, a real selection and real progress, and adds a console and a safer way to run them.

Ways to run C#:

1. Scripts menu: pick a script from the list (UTMT-SCRIPT-01), Run Script..., Run Last Script.
2. Run Other Script... or drag a `.csx` onto the window (the drop asks what to do, shell 5.5 and 15).
3. The Script Console in the bottom panel: one line or a block of C# (UTMT-APP-05).
4. Outside the window: G3MTool runs `.csx` scripts (`G3MTool execute a.csx`). G3MStudio has no command line.
5. Project import stages ([projects.md](projects.md) section 9).
6. Command palette: `>Run Script`.

## 2. Layout

```
+--------------------------------------------------------------------------------------+
| Script Console                        [Run] [Cancel] [Clear]   Scope: Data file  [v] |
+--------------------------------------------------------------------------------------+
|  > Data.Sprites.Count                                                                |
|    412                                                                               |
|  > foreach (var s in Data.Sprites) if (s.Name.Content.StartsWith("spr_old")) Highl...|
|    ScriptMessage: 12 sprites found.                                                  |
|  >                                                                           [ctrl+L]|
+--------------------------------------------------------------------------------------+
```

The console is a tab of the bottom panel (shell 7.7). Input is a multi-line text box at the bottom with GML-free C# highlighting; output scrolls above it. Each run adds one entry: the input, then output lines (script messages, return value, errors) with a time stamp on hover.

The Scripts menu:

```
Scripts
  Run Script...                  Ctrl+Shift+R
  Run Last Script
  Check Script...
  New Script File...
  ---
  My Scripts          >          (user folder, subfolders become submenus)
  Project Scripts     >          (workspace folder, project mode only)
  ---
  Refresh Script List
  Open Scripts Folder
```

## 3. Script runner

### 3.1 Script folders and the menu (UTMT-SCRIPT-01, UTMT-SCRIPT-02)

Two roots are read, in this order, and shown as separate groups in the menu (no scripts are bundled, section 6):

| Root | Where | Writable |
| --- | --- | --- |
| User | `Scripts/` in the user settings folder | Yes |
| Workspace | `scripts/` inside the project folder, in project mode | Yes |

Subfolders become submenus. A submenu is read when it opens, so a new file shows without a restart; "Refresh Script List" re-reads all roots at once. An empty root shows "(No scripts found)" with a link that opens the folder. A root that does not exist shows "Folder not found: {path}" with "Create Folder". The same list feeds the command palette (`>Run Script: ExportAllCode`).

Run Script... opens a file picker on `.csx` and runs the chosen file (UTMT "Run other script"). A script chosen this way is remembered for Run Last Script.

### 3.2 Script engine (UTMT-SCRIPT-03)

1. Scripts are compiled with Roslyn C# scripting through G3MLib, with one `ScriptGlobals` object whose members match `IScriptInterface` (G3MLib work item 7). The class is the one G3MLib gives G3MTool too, so a script behaves the same in both.
2. Default imports: `System`, `System.IO`, `System.Linq`, `System.Text`, `System.Text.RegularExpressions`, `System.Collections.Generic`, `G3MLib.DataFile`, `G3MLib.DataFile.Models`, `G3MLib.DataFile.Util`, `G3MLib.Decompiler`, `G3MLib.Compiler`, `G3MLib.Scripting`, `ImageMagick`, `Newtonsoft.Json`. Default references: G3MLib, the analyzer, Magick.NET and Newtonsoft.Json.
3. Name translation: before compiling, the UTMT translator rewrites `UndertaleModLib` to `G3MLib.DataFile`, `Underanalyzer` to `G3MLib.Analyzer`, `Undertale*` types to `GameMaker*` and `undertaleData` to `gameMakerData`. It is applied on every path (editor, console, project stages), unlike today's G3MLib where only the G3MTool hosts call it. A script using the old names runs without edits; a script using the new names runs too.
4. Warm-up: on start the engine compiles a one-line script in a background job so the first real run does not pay the Roslyn start-up cost. The status bar shows no job for it. Compiled scripts are cached by file hash in app data.
5. `#load "other.csx"` resolves from the script's own folder. A path that leaves that folder (`..`, absolute, or a link that resolves outside) is rejected with `G3MS-SCRIPT-004` (UTMT-FILE-14).
6. Line numbers: debug information is on, so errors show the line in the script.
7. A script runs on a background thread under the workspace lock (architecture "Threading"). `MainThreadAction(action)` marshals a call to the UI thread. While the lock is held the Explorer and editors show a "Script running" banner and ignore edits; panning, zooming and reading keep working.
8. Only one script runs per workspace. A second Run while one runs shows "A script is already running. Wait for it, or cancel it from the Jobs panel."
9. Cancelling: Cancel (Escape in the console, Jobs panel, or the progress dialog) sets a flag. The script stops at its next call into the API (any dialog, progress update or `MainThreadAction`) with a `ScriptCancelledException`; a script in a tight loop that never calls the API cannot be stopped, and the Jobs row then says "Cancel requested. Waiting for the script." with a "Force Stop" action that ends the run and restores the checkpoint (3.4).

### 3.3 Errors and results (UTMT-SCRIPT-04)

| Case | Behaviour |
| --- | --- |
| Compile error | Problems entries with source "Script", file, line and column, one per error; the first error opens as a message dialog "Script '{name}' has {n} errors." with "Show Problems". Nothing runs. |
| Exception in the script | Message dialog: title "Script '{name}' failed.", one sentence with the exception message, an expander with the formatted stack trace (script lines first), buttons "Copy Details" and "Close". Output channel Scripts gets the same text. The checkpoint is offered: "Undo Script Changes" (3.4). |
| `ScriptError("text")` call | Same dialog without a stack trace; the script then ends. |
| `ScriptCancelledException` | Ends quietly: Output line "Script cancelled." No dialog. |
| Normal end | Notification "{script} finished in {seconds} s." with "Show Output" (shell 8), unless suppressed (3.6). |
| Console run | The expression's value is printed under the input; an exception prints in `Danger` text with the first stack line. No dialog. |

`ScriptExecutionSuccess`, `ScriptErrorMessage` and `ScriptErrorType` hold the result of the last run for scripts that start other scripts.

### 3.4 Checkpoint and undo

The model has no per-edit undo while a script runs (G3MLib). Before a script runs, the workspace takes a checkpoint: the current journal state plus a copy of the model as the save pipeline would write it, kept in app data and dropped after 24 hours or when the workspace closes. After the run, Undo reverts the whole run as one step named "Run Script '{name}'" by restoring the checkpoint. Edits made after the script stay in the undo stack above it.

Checkpoints cost time on big files, so they are written on the job service (about 1 s for a 92 MB file) and the script starts when the copy is ready. Setting `scripts.checkpoint`: `always` (default), `ask` or `never`. With `never`, Undo for the run is unavailable and the notification says so.

### 3.5 Nested scripts and linting (UTMT-SCRIPT-05)

`RunUMTScript(path)` runs another script in the same run (same checkpoint, same lock, same progress). Relative paths resolve from the calling script. Depth is limited to 16: "Scripts are nested too deeply."

`LintUMTScript(path)` compiles a script without running it and returns whether it compiled. Scripts > Check Script... runs it on a chosen file and reports in Problems. A folder choice checks every `.csx` in it (this replaces `LintAllScripts.csx`). The compatibility suite runs the same check on the 110 UTMT scripts from a checkout, outside the app.

### 3.6 Finished message (UTMT-SCRIPT-06)

By default a normal end shows the finished notification. `SetFinishedMessage(false)` suppresses it for that run; `SetFinishedMessage(true)` restores it. Setting `scripts.showFinished` (default on) turns the notification off for all scripts, except after an error.

### 3.7 Script helpers (UTMT-SCRIPT-07)

The helper class `ScriptingUtil` is available to scripts as in UTMT: file-name checks (`CheckFileName`-style validation against reserved names), image helpers (load, crop, convert to texture page item data), hex and byte helpers, and `PrettifyException`. Behaviour follows G3MLib; this spec adds nothing but the guarantee that every helper UTMT scripts use is present. A missing helper is a bug to list in the compatibility table (section 5.3).

## 4. Script console (UTMT-APP-05)

1. Enter runs the input. Shift+Enter inserts a new line. Up and Down walk the history (last 200 entries, kept per user). The input keeps focus after a run.
2. The input is a C# script body. `Data`, `FilePath`, `Selected`, `Highlighted` and every other global are available. Each run is its own script: variables do not persist, except through `Data` or the `State` dictionary the console adds (`State["x"] = 1`).
3. Completion and parameter hints come from Roslyn for the globals and G3MLib types. Ctrl+Space opens completion.
4. The console shares the engine, the checkpoint and the lock with the Scripts menu. Output of `ScriptMessage` and friends prints in the console; dialogs open as dialogs.
5. Pasting more than one line runs nothing until Enter.
6. Escape cancels a running entry. Ctrl+L clears the output (the history stays).
7. Scope drop-down: "Data file" (default) or "Project". In project mode `Project` is set (API-01).
8. The console is read-only when a script from the Scripts menu is running.
9. Empty state: "Type C# and press Enter. The globals are Data, FilePath and Selected." with a "Show Help" link that opens the API list of section 5.

## 5. Script API

### 5.1 Contract

The contract is `IScriptInterface` from G3MLib, signature for signature (work item 7). The existing G3MTool copies of `ScriptGlobals` differ from it (`ScriptInputDialog` parameters, `PromptLoadFile` argument order, `SetProgressBar` types); G3MStudio does not keep those differences. Where a G3MTool-only signature existed, an overload keeps old G3MTool scripts working.

### 5.2 Members

| Group | Members | G3MStudio behaviour |
| --- | --- | --- |
| State (UTMT-API-01) | `Data`, `Project`, `FilePath`, `ScriptPath`, `ExePath`, `CanSave`, `IsAppClosed`, `ScriptExecutionSuccess`, `ScriptErrorMessage`, `ScriptErrorType`, `MainThreadAction` | `Data` is the open data file model. `Project` is null in data file mode. `FilePath` is the data file path. `ExePath` is the folder of the program (the bundled Scripts root parent). `CanSave` is false for read-only locations and unsupported bytecode. `IsAppClosed` turns true when the window closes. |
| Selection (UTMT-API-01) | `Highlighted`, `Selected` | `Highlighted` is the resource selected in the Explorer, `Selected` is the resource open in the focused editor tab (UTMT meaning). Both are null in headless runs. A multi-selection gives its first item; `SelectedItems` lists all. |
| Data (UTMT-API-02) | `EnsureDataLoaded()`, `MakeNewDataFile()`, `AreFilesIdentical(a, b)` | `EnsureDataLoaded` throws the "no data file" script error when none is open. `MakeNewDataFile` opens a new blank workspace, after the unsaved-changes rule in [shell.md](shell.md) section 14.5. `AreFilesIdentical` compares by size then SHA-256, streaming. |
| Messages (UTMT-API-03) | `ScriptMessage`, `ScriptWarning`, `ScriptQuestion`, `ScriptError`, `SetUMTConsoleText`, `ScriptOpenURL` | Dialogs in section 7. `SetUMTConsoleText` writes a line to the console output. `ScriptOpenURL` asks first ("This script wants to open {url}. Open it?") for any URL; only `http`, `https` and `mailto` are allowed. |
| Input (UTMT-API-04) | `ScriptInputDialog`, `SimpleTextInput`, `SimpleTextOutput`, `InitializeScriptDialog` | Section 7. |
| Search output (UTMT-API-05) | `ClickableSearchOutput` (two overloads) | Results go to the Search results tab as the shared result list, one group per code entry, with the failed-entries list as a second group. Clicking opens the code editor at the line. |
| Progress (UTMT-API-06) | `SetProgressBar`, `UpdateProgressBar`, `UpdateProgressValue`, `UpdateProgressStatus`, `AddProgress`, `IncrementProgress`, parallel variants, `GetProgress`, `SetProgress`, `HideProgressBar`, `StartProgressBarUpdater`, `StopProgressBarUpdater`, `EnableUI` | One job row in Jobs and the status bar; a progress dialog for scripts that call `SetProgressBar` (section 7.5). Parallel variants are thread-safe. The updater pair is a real timer that refreshes the bar every 100 ms. `EnableUI` hides the blocking progress overlay; the workspace stays locked until the script ends. |
| Files (UTMT-API-07) | `PromptChooseDirectory`, `PromptLoadFile`, `PromptSaveFile` | System pickers through the storage provider. Filters in UTMT syntax (`"Images (*.png)\|*.png\|All files\|*.*"`) are converted. Cancel returns null. |
| Code text (UTMT-API-08) | `GetDecompiledText`, `GetDisassemblyText` | [editors/code.md](editors/code.md) section 7. |
| Navigation (UTMT-API-09) | `ChangeSelection(object, inNewTab)` | Selects the resource in the Explorer and opens it (a new tab when `inNewTab`). Items not shown in the Explorer open their parent editor. |
| Removed members (UTMT-API-10) | `SyncBinding`, `DisableAllSyncBindings` | UTMT removed them in 0.9.0.0. G3MStudio keeps both as members that do nothing and print one Output line per run: "SyncBinding was removed in UTMT 0.9.0 and has no effect here." Scripts that depend on the old binding behaviour show that line, not a compile error. |
| Extras from G3MTool (TOOL-35) | `DataFilePath`, `InputDir`, `OutputDir`, `Verbose`, `RunUMTScript` | `InputDir` and `OutputDir` are set by G3MTool (`--input`, `--output`) and are null in G3MStudio. `Verbose` follows the Output channel log level. |

### 5.3 Compatibility table

Every API member has a test in the compatibility suite. A member that cannot match UTMT is listed with a reason in the compatibility notes (wiki), and the script that needs it shows an info bar when run: "This script uses {member}, which behaves differently here. See the compatibility notes." Known entries at design time:

| Member | Difference |
| --- | --- |
| `SyncBinding`, `DisableAllSyncBindings` | No effect (see above) |
| `ScriptOpenURL` | Confirmation first |
| Scripts using WPF or Windows Forms types (`ExportAllRoomsToPNG.csx`, `ImportGraphicsAdvanced.csx`, `AutoBackup.csx` window) | Compile on Windows only when the types exist; elsewhere a message names the missing type. Their work is available as built-in features ([script-features.md](script-features.md): SF-01 room images, SF-02 strips and origins, Fonts editor) |
| Profile mode members | Not present, as in UTMT 0.9 |

## 6. No bundled scripts (F-PAR-3)

G3MStudio ships no scripts (owner decision Q42). The 110 scripts of UndertaleModTool were analysed one by one ([../research/utmt-scripts-analysis.md](../research/utmt-scripts-analysis.md)); their useful concepts are built-in features, described in [script-features.md](script-features.md): 36 scripts define a feature, 32 are merged into one, 24 are plugin candidates for later (section 5 of that document) and 18 are dropped with a written reason. Every script has exactly one of these outcomes, which is how F-PAR-3 is met.

Consequences for this document:

1. The Scripts menu has no bundled folders. Its roots are the user `Scripts/` folder and the workspace `scripts/` folder (section 3.1). The first run of an empty user folder shows "(No scripts found)" with "Open Scripts Folder" and "New Script File...".
2. The trust rule for bundled scripts in section 8 does not apply; every script asks on first run.
3. `Scripts/COMPATIBILITY.md` is not shipped. The compatibility table of section 5.3 lives in the wiki and in the unit tests.
4. Old UTMT scripts still run: the translator, the full `IScriptInterface` and the compatibility suite stay, so a user's own copy of any UTMT script works where G3MLib allows. The compatibility suite runs the 110 scripts from a checkout of UTMT in the build pipeline, not from the app.
5. Where an old script has a built-in replacement, the replacement is the supported route; a user script is never blocked or rewritten.
6. G3MTool keeps and runs its own scripts (TOOL-36); G3MStudio does not copy them.

## 7. Script dialogs

All script dialogs use the shared controls (ux-standards section 11) and appear over the window that owns the workspace. While a dialog is open the script waits; Escape is Cancel.

### 7.1 Message, warning, error and question (UTMT-API-03)

Message dialog (ux-standards 11). `ScriptMessage` has icon Info and one button "OK" (the one place "OK" is allowed: it acknowledges, nothing else changes). `ScriptWarning` has icon Warning. `ScriptError` has icon Error, "Copy Details" and "Close", and ends the script. `ScriptQuestion` has Yes and No; focus starts on No when the text contains "delete", "overwrite" or "replace". Long text scrolls inside the dialog (maximum 70% of the window height) and is selectable. A question has "Always choose this" only when the script passes a stable id (the G3MStudio overload); otherwise it never appears.

### 7.2 Text input (UTMT-API-04)

`ScriptInputDialog(title, label, defaultInput, cancelText, submitText, isMultiline, preventClose)` and `SimpleTextInput(title, label, defaultValue, allowMultiline)` open the text input dialog (UTMT-APP-13): title, prompt, single-line or multi-line box, custom button captions. Enter submits in single-line mode, Ctrl+Enter in multi-line. `preventClose` stops Escape and the close button from ending the dialog; Cancel still works. The returned value is null on cancel.

### 7.3 Text output (UTMT-API-04)

`SimpleTextOutput(title, label, defaultText, allowMultiline)` shows a read-only box with Copy and Save to File. `InitializeScriptDialog` is accepted and resets dialog defaults.

### 7.4 Clickable output (UTMT-API-05, UTMT-APP-12)

Results open in the Search results tab (shell 7.3), not a separate window; "Open in New Window" on the tab menu gives UTMT's window. Context menu: Open, Open in New Tab, Copy, Copy All.

### 7.5 Progress

A script that calls `SetProgressBar(message, status, value, maximum)` gets a progress dialog (modal to the workspace, not to the app): message, status line, bar, elapsed time and "Cancel". `HideProgressBar` closes it. A script that only calls `UpdateProgressValue` without `SetProgressBar` shows the Jobs row only.

### 7.6 File prompts (UTMT-API-07)

System dialogs. `PromptSaveFile` asks before overwriting an existing file (the system dialog does this). Initial folder is the last folder used for that kind of prompt in this workspace.

## 8. Safety

Scripts are code with the rights of the user: they can read and write any file the user can and use the network. G3MStudio cannot sandbox managed code in process, so the rules below are about informed consent.

1. Trust prompt on first run (ux-standards 7): "Run '{name}'?" with the path, size, SHA-256, last modified time, a "Show Source" button that opens it read-only, and "Run Once", "Always Run This Script", "Cancel". "Always" stores path and SHA-256 in workspace-independent user state; a changed file asks again. No script is bundled, so every script asks once (see OI-016 for scripts that arrive inside patches).
2. Scripts dropped on the window, opened from "Run Other Script" and scripts from a project folder always ask the first time. Scripts started by the console are the user's own input and do not ask.
3. A workspace opened from a project folder, a downloaded mod or a link is "Restricted" until the user trusts it (status bar item, shell 4). While restricted, project scripts and plugins do not run. The user trusts per folder.
4. Runs without a person (automation) belong to G3MTool; G3MStudio always shows the trust prompt.
5. Path confinement (UTMT-FILE-14): `#load`, `RunUMTScript` and paths derived from the data file (for example game files next to it) are checked to stay under the expected folder. Paths a script builds itself are not restricted.
6. Network: nothing is blocked. A script that calls `ScriptOpenURL` asks first. The trust prompt says scripts can use the network.
7. Every run is logged in the Scripts Output channel with the script name, SHA-256, start, end, result and the number of resources changed.
8. A script cannot save the workspace or close it during a run unless it calls the API for it; `Data` saved through G3MLib directly writes to disk without the validation read, so the API `SaveFile` helper is preferred and the docs say why.

## 9. Actions and shortcuts

| Command | Id | Windows and Linux | macOS |
| --- | --- | --- | --- |
| Run Script... | `scripts.run` | Ctrl+Shift+R | Cmd+Shift+R |
| Run Last Script | `scripts.runLast` | None | None |
| Refresh Script List | `scripts.refresh` | None | None |
| Open Scripts Folder | `scripts.openFolder` | None | None |
| Check Script... | `scripts.lint` | None | None |
| New Script File... | `scripts.new` | None | None |
| Cancel Running Script | `scripts.cancel` | None | None |
| Show Script Console | `view.console` | Ctrl+` | Control+` |
| Run Console Input | `console.run` | Enter | Enter |
| Insert New Line in Console | `console.newline` | Shift+Enter | Shift+Enter |
| Previous or Next Console Command | `console.history` | Up / Down | Up / Down |
| Cancel Running Script (Console) | `console.cancel` | Escape | Escape |
| Clear Console | `console.clear` | Ctrl+L | Cmd+L |
| Trust Scripts in This Folder | `scripts.trustFolder` | None | None |

"New Script File..." asks for a name and creates a `.csx` with the globals documented in a header comment in the User root, then opens it in the code editor with C# highlighting. The first line of every template names the file and nothing else.

## 10. Validation and messages

| Case | Message |
| --- | --- |
| No data file open | "Open a data file first." (menu items disabled with the same tooltip) |
| Script file missing | "'{file}' no longer exists. Refresh the script list." |
| Script file unreadable | "Could not read '{file}'. {reason}" |
| Script not UTF-8 | Read as UTF-8 with BOM detection; if invalid, a Warning "'{file}' is not valid UTF-8. Characters may be wrong." |
| Second script while one runs | "A script is already running. Wait for it, or cancel it from the Jobs panel." |
| Nesting too deep | "Scripts are nested too deeply." |
| `#load` outside the folder | "'{path}' is outside the script's folder." (`G3MS-SCRIPT-004`) |
| Cancelled | "Script cancelled." |
| Script changed resources and the workspace is read-only | "This data file cannot be saved, so the script's changes would be lost. Use Save As first." offered before the run |
| Checkpoint failed | "Could not take a checkpoint. Run without undo?" with "Run", "Cancel" |

## 11. Undo

One script run is one undo step ("Run Script 'name'"). Console entries that change the model are one step each ("Run Console Input"). Redo reapplies the saved result of the run (the model after the script). The checkpoint is dropped after 24 hours or at workspace close; after that the step is greyed in Undo History with the text "Checkpoint expired".

## 12. Empty and loading states

- Scripts menu, no script found: "(No scripts found)".
- Console empty: see section 4 rule 9.
- First run after start: while Roslyn warms up (rare, only if a script starts within the first seconds), the Jobs row says "Preparing the script engine..." and the run starts when ready.
- Large output: the console keeps the last 10,000 lines and shows "Earlier output was removed. Save the log to keep it." with a link to the Output channel.

## 13. Parity coverage

| ID | Feature | Section |
| --- | --- | --- |
| UTMT-SCRIPT-01 | Scripts menu | 2, 3.1 |
| UTMT-SCRIPT-02 | Run other script | 3.1 |
| UTMT-SCRIPT-03 | C# script engine | 3.2 |
| UTMT-SCRIPT-04 | Script error reporting | 3.3 |
| UTMT-SCRIPT-05 | Nested scripts and linting | 3.5 |
| UTMT-SCRIPT-06 | Finished message | 3.6 |
| UTMT-SCRIPT-07 | Script helpers | 3.7 |
| UTMT-API-01 | State accessors | 5.2 |
| UTMT-API-02 | Data helpers | 5.2 |
| UTMT-API-03 | Message dialogs | 5.2, 7.1 |
| UTMT-API-04 | Input and output dialogs | 5.2, 7.2, 7.3 |
| UTMT-API-05 | Clickable search output | 5.2, 7.4 |
| UTMT-API-06 | Progress API | 5.2, 7.5 |
| UTMT-API-07 | File prompts | 5.2, 7.6 |
| UTMT-API-09 | Navigation | 5.2 |
| UTMT-API-10 | Removed members | 5.2, 5.3 |
| UTMT-APP-05 | C# command box | 4 |
| UTMT-APP-12 | Clickable results window | 7.4 |
| UTMT-APP-13 | Text input dialogs | 7.2 |
| UTMT-APP-15 | Message boxes | 7.1 |
| UTMT-FILE-14 | Path confinement | 3.2, 8 |
| TOOL-35 | CSX host API | 5.2 |
| TOOL-36 | Bundled scripts (G3MTool keeps its own; none in G3MStudio) | 6 |
| F-PAR-3 | UTMT bundled scripts become built-in features | 6, [script-features.md](script-features.md) |
| TOOL-54 | Interactive scripts | 7 |

UTMT-API-08 (code text) is covered in [editors/code.md](editors/code.md) section 7. UTMT-PLAT-04 (scripts shipped beside the program) is in [platforms-formats.md](platforms-formats.md) section 5. The command line side of scripts (UTMT-CLI-02, UTMT-CLI-08) is G3MTool CLI/GUI (not duplicated).
