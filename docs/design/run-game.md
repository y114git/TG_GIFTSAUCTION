# Run the game

Status: proposed. Launching the game with the current changes, choosing the runner, launching with mods, capturing logs and crash reports, and the GameMaker debugger launch. Requirements: F-MOD-2, F-SUITE-3, F-SUITE-6. Shared rules: [ux-standards.md](ux-standards.md). The Run menu and keys are in [shell.md](shell.md) section 2 (Run menu) and section 16. Settings used here are in [settings.md](settings.md) (Run page and Paths page).

## 1. Purpose and entry points

The test loop of a modder is: change something, run the game, look, repeat. G3MStudio makes the first step one key (F5) and never touches the original data file to do it. The same screen also covers the mod side that G3M handles (launch with a chosen set of mods, restore afterwards) so a modder does not need another program to test a mod stack.

Entry points: Run menu, the Launch Game button in the toolbar (split button: the arrow opens the launch profiles), F5, the command palette, the status bar item "Game running", the Start page card menu ("Launch Game") and the shell verbs registered by onboarding (UTMT-RUN-06).

## 2. Layout

```
+--------------------------------------------------------------------------------------+
| [> Launch Game v]  Profile: Default   Runner: Game executable   Mods: none           |  toolbar group
+--------------------------------------------------------------------------------------+

Launch With Options (Alt+F5)
+--------------------------------------------------------------------------------------+
| Runner                                                                               |
|  ( ) Game executable      undertale.exe                               game folder    |
|  ( ) GameMaker 1.4        Runner.exe                                  C:\...\GMS     |
|  (*) GameMaker runtime    2024.13.1.193   windows\x64\Runner.exe      runtimes cache |
|  ( ) Custom...            [ path                                            ] [...]  |
| Version      Path                                                                    |
|  2024.13.1.193  C:\ProgramData\GameMakerStudio2\Cache\runtimes\runtime-...\Runner.exe |
|  2023.8.2.108   ...                                                                  |
| Options                                                                              |
|  [x] Write the game log (-debugoutput)   [ ] Turn off the debugger flag             |
|  Extra arguments [                                                          ]       |
|  Working folder  [ game folder v ]       Environment [ Edit... ]                    |
| Mods: none [Choose Mods...]   Mode: Launch (restore after exit)                      |
|                                   [Save as Profile...]  [Cancel]  [Launch]           |
+--------------------------------------------------------------------------------------+
```

Game log and crash panels live in the bottom panel (section 7).

## 3. Launch Game (F5, UTMT-RUN-01, F-MOD-2)

`run.game` starts the game with the changes in the workspace, saved or not, without writing to the user's data file.

Steps, shown as one job "Launching {game}..." (ux-standards 9; it takes the workspace lock because it writes a build):

1. Checks: a data file is open; the bytecode is supported (otherwise the code cannot be written, and the launch uses the original file: "Code is read-only for this file, so the game starts without code edits."); the runner exists (4); no game of this workspace is already running (then Restart, 7.1).
2. Build: the current model is written to a temporary data file with the save pipeline (write, validation read). Two header changes apply to the temporary copy only: the Steam app id is set to 0 so the Steam client does not take over the start, and the GameMaker debugger flag is cleared (UTMT-FILE-07: the normal runner hangs with it).
3. Place: the temporary file is `g3mstudio_run.tmp` in the data file's folder by default (the game also reads the files next to its data file: audio group files, streamed sounds, included files). Setting `run.tempLocation`: `gameFolder` (default) or `appData`; with `appData` the files next to the data file are linked or copied (hard links where possible) and the launch says so in Output. The file is deleted after the game exits and at the next start of G3MStudio if left behind.
4. Start: the runner is `{GEN8 file name}.exe -game {temp file}` on Windows. Linux and macOS use the platform runner (table below). The working folder is the game folder.
5. Watch: the process is tracked; the status bar shows "Game running" with the elapsed time; Output channel "Game" receives its output (7.1).
6. First launch of a session shows the one-time warning in an info bar (setting `confirm.launchTemporary`, UTMT-SET-08): "The game runs a temporary copy. Your changes are not saved to the data file." with "Save First", "Launch" and "Don't Warn Again" (the Confirmations page). It never blocks F5 after the box is ticked.
7. In project mode the build goes to the project output folder instead (`{project}/build/`, replaced on each launch, listed in the project's `.gitignore`), so the project and the game folder stay apart. The build comes from the model in memory, so a launch never waits for a sync.

Runners by platform:

| Platform of the data file | Runner | Notes |
| --- | --- | --- |
| Windows (`data.win`) | `{name}.exe` in the game folder | Native on Windows; on Linux and macOS through the configured Wine or Proton command (`run.wineCommand`) |
| Linux (`game.unx`) | `{name}` in the game folder | Executable bit checked; "The runner is not executable. Make it executable?" |
| macOS (`game.ios` in a `.app`) | The `.app` bundle's executable | The temporary file goes inside `Contents/Resources/` of a copy of the bundle, because bundle contents must not change |
| Android (`game.droid`), iOS | None | "G3MStudio cannot start this platform. Use Export Modified Data File..." with the export action |

The temporary build step is skipped when nothing changed since the last launch (same model hash); the launch then takes under a second.

Failure messages (ux-standards 8): the runner is missing ("Could not start the game. '{runner}' was not found. Choose a runner in Launch With Options or set the game folder in Settings > Paths."), the game exits within 2 seconds with a nonzero code ("The game closed right away (code {code}). Open the game log."), the build failed ("Could not build the temporary data file. {reason}"), a path failed the confinement check (UTMT-FILE-14).

## 4. Runner choice (Alt+F5, UTMT-RUN-02, UTMT-RUN-03)

`run.withOptions` opens the dialog of section 2. The choices:

| Runner kind | Source | Notes |
| --- | --- | --- |
| Game executable | `{GEN8 file name}` in the game folder | Default |
| GameMaker 1.4 | `Runner.exe` in the GMS 1.4 folder (`paths.gameMakerStudio1`) | Listed when the folder exists |
| GameMaker runtime | Every runtime folder under `paths.gameMakerRuntimes` | Version and path columns; the dialog prefers `windows\x64\Runner.exe` of a runtime and falls back to the 32-bit one. Runtimes of GameMaker releases, LTS and Beta are all listed when found in the default locations of the platform |
| Custom | Any file chosen | Remembered in the list with a label |

Rules:

1. The runtime list sorts newest first; the entry nearest to the data file's detected GameMaker version is preselected and tagged "Closest to this game" (platforms-formats.md section 7.4).
2. Before launching with another runner the dialog offers to clear the debugger flag when it is set (UTMT-FILE-07): "The debugger flag is set. Turn it off for this launch?" The flag change applies to the temporary copy only.
3. "Write the game log" adds `-debugoutput {temp name}.gamelog.txt`; the file is read live (section 7.1). Default on for a non-default runner.
4. Extra arguments are split with shell rules, shown as the final command line below the field ("Command line: ...") so it can be copied.
5. The environment editor is a list grid (name, value) for variables set for this launch only.
6. "Save as Profile..." stores the whole dialog (runner, options, mods, mode) under a name. Launch Profiles (`run.profiles`) lists them; the toolbar arrow switches the active profile; the default profile is "Default" and cannot be deleted.
7. The runner and profile chosen last persist per workspace.
8. A runtime folder that fails to read shows in the list as "{path} (unreadable)" in `Warning`.

## 5. Launch with mods (F-SUITE-3)

`run.withMods` opens the Choose Mods dialog. It uses G3M's launch model so a modder gets the same results in both programs.

### 5.1 Choosing mods

1. Sources: mods installed by G3M (read from its profiles folder when found), G3M mod folders and `.zip` files, `.g3mpatch` and `.xdelta` files, and the current workspace's mod project (the mod being built, listed first as "Current project").
2. A row per mod: name, version, game, enabled check box, step number, priority. Mods in one step share the same input and are merged with a patch merge, the higher priority row winning; steps run in order, each on the result of the previous step (G3M-13). Drag handles and Move Up and Move Down buttons reorder (the keyboard alternative).
3. The base of the first step is the current workspace build (so unsaved edits are included), not the file on disk. A row at the top shows "Workspace changes" and can be unchecked to start from the original file.
4. Relationship checks run when the list changes (G3M-14): missing dependencies, conflicts, and the recommended order. Findings are listed under the table, each with a button ("Add Dependency", "Apply Recommended Order"). Errors block Launch; warnings do not.
5. Chapter mode for games that have chapters (G3M-16): a chapter picker (all, or one chapter) with its own list and steps per chapter. It cannot combine with a Steam launch.
6. Mod targets are rewritten for the runtime in use: `data.win`, `game.unx` and `game.ios` become the name the platform expects, and `chapterN_windows` becomes `chapterN_mac` on macOS (G3M-17).

### 5.2 Modes

| Mode | Behaviour |
| --- | --- |
| Launch | Apply, start the game, restore everything after it exits (default) |
| Launch and Keep Changes | Apply and start; do not restore. Confirms: "The mods stay in the game folder. Continue?" |
| Launch Patching Only | Apply and stop, no start. Confirms the same way |

### 5.3 What happens

One job with these named steps in the Jobs panel: Back up files, Apply step {n} of {m}, Start game, Wait for exit, Restore files.

1. Back up (UTMT-PROJ-12): every game file a mod will overwrite or delete is copied to `.g3mstudio/backup/{time}/` next to a manifest (path, size, SHA-256) before it changes. The manifest is written first, then the files, so a crash leaves a usable record.
2. Apply: operations of every enabled mod run in order (patch, overwrite, soft and hard variants, extract, as in the G3M package format). Failures roll back through the manifest ("{mod} could not be applied. The game folder was restored.") and show in Problems with the mod and operation.
3. Start with the runner of section 4, then wait.
4. Restore after exit: files return from the backup and the manifest is deleted. If the user closes G3MStudio while the game runs, the restore runs when the game exits or at the next start ("Game files from the last launch are still changed. Restore them now?" with "Restore" and "Keep").
5. Force restore: `run.restoreFiles` ("Restore Game Files...") lists backups and restores a chosen one, also when a file was changed outside (it overwrites with a confirm that names the files).
6. A launch through Steam with mods shows the warning `steam_launch_with_mods` from G3M: "Steam can start the game again and skip the mods. Launch without Steam is recommended."
7. A launch refuses to start when another game of the same folder is already running ("{game} is already running. Close it first.").

## 6. Launch under the GameMaker debugger (UTMT-RUN-04, UTMT-RUN-05)

Hidden unless `run.showGameMakerDebugger` is on (UTMT-SET-05); the Settings page says where it appears. `run.gmsDebug`:

1. Sets the debugger flag in the temporary copy, builds it, writes a stub project file (`.gmx`) beside it, starts the runner and then `GMDebug.exe` against the `.yydebug` file and the debugger port. Both processes show in the Game channel; closing the debugger stops the game.
2. Debug data (UTMT-RUN-05) comes from the mode dialog the command opens: "No debug data" (default), "Decompiled source", "Disassembly". The first writes an empty `.yydebug`; the others write the code text and the breakpoint offsets of each code entry. The format is described in platforms-formats.md section 9.1. The dialog states that the mode with source takes longer ("About {n} seconds for this file").
3. A failure to find `GMDebug.exe` says: "GameMaker's debugger was not found. Set the GameMaker 1.4 folder in Settings > Paths."
4. The debugger works for GameMaker 1.4 bytecode only; for others the command is disabled with the reason.

## 7. Logs, debugging aids and crash reports

### 7.1 Game log (F-SUITE-3, F-SUITE-6)

The Output channel "Game" shows what the process prints (standard output and error) and the lines of the `-debugoutput` log, with a time stamp each. A separate tab "Game Log" in the bottom panel adds:

| Part | Behaviour |
| --- | --- |
| Toolbar | Follow (auto-scroll, default on), filter box, level toggles (Info, Warning, Error), Pause, Clear, Copy All, Save to File..., Open Log File, Restart (`run.restart`), Stop (`run.stop`) |
| Lines | Time, level icon, text. Text of the form `gml_Object_obj_player_Step_0 (line 12)` or `at gml_Script_scr_x (line 4)` becomes a link that opens the code entry at that line (decompiled lines map back through the source map); `data.win` offsets and resource names link to the resource |
| Count | A tab label "Game Log 3" counts new errors since the tab was last seen |
| Retention | The last 20 runs are kept in app data; the log picker lists them with start time, runner, mods and exit code |

Stop (Shift+F5) ends the process tree. Restart (Ctrl+Shift+F5) stops it and launches again with the same profile. Exit code and duration end each run: "Game exited with code 0 after 4 min 12 s."

### 7.2 Crash reports (F-SUITE-6)

`run.crashReports` opens the Crash Reports list: a table of runs that ended with a crash (nonzero exit, a runtime "Unable to find..." or "Unhandled Exception" text, or a crash dump written by the runtime next to the game).

1. Row: time, game, runner, mods, exit code, first error line.
2. The detail view shows the error text, the last 200 log lines, the call stack as links (as in 7.1), the launch settings and the list of resources changed in the workspace since the last clean run ("Changed since last clean run: 14 resources"), which is the useful clue for modders.
3. Actions: Copy Details, Export Report (a `.zip` with the text, the log and the profile; no game files), Open Code at Top Frame, Re-run With Original Data (launches the unmodified file to tell a mod crash from a game crash).
4. Reports older than 30 days are removed unless starred (`run.crashRetentionDays`).

### 7.3 Mod conflict view (F-SUITE-6)

Opens from Run > Launch With Mods... > "Check Conflicts" and from Mod > Check Mod Dependencies. It shows the result of the steps against a temporary copy without starting the game:

| Tab | Content |
| --- | --- |
| Steps | Each step, its mods, whether the merge had conflicts, time taken |
| Resources | Table of resources changed by more than one mod: resource, mods in order, which one won, the kind of overlap (same field, same code entry, same file) |
| Files | Game files written by more than one mod, with sizes and hashes |
| Problems | Relationship findings and failures |

Rows open the resource diff (data-diff view) and "Export Report" writes a Markdown and a JSON file. The result matches what the real launch will do because it runs the same operations (G3M-22).

### 7.4 Other debugging aids

1. "Show Game Log" (`run.openLog`) opens the last log in the Game Log tab.
2. The status bar item "Game running" lists PID, runner and elapsed time on hover and opens the log on click.
3. "Reload Code in Running Game" is not offered: GameMaker has no hot reload for data files, and the spec does not pretend it has. Restart is one key.
4. Game launch hooks for plugins (before launch, after exit, on failure) run at steps 2 and 4 of section 3 and section 5.3 (F-PLG-2).
5. Assets that cannot be loaded because of a bad edit are caught earlier by `tools.integrity` (platforms-formats.md section 8) and by the Problems panel; the launch dialog shows "{n} problems. Launch anyway?" when errors exist.

## 8. Actions and shortcuts

| Command | Id | Windows and Linux | macOS |
| --- | --- | --- | --- |
| Launch Game | `run.game` | F5 | F5 |
| Launch With Options... | `run.withOptions` | Alt+F5 | Option+F5 |
| Launch With Mods... | `run.withMods` | None | None |
| Launch and Keep Changes... | `run.keepChanges` | None | None |
| Stop Game | `run.stop` | Shift+F5 | Shift+F5 |
| Restart Game | `run.restart` | Ctrl+Shift+F5 | Cmd+Shift+F5 |
| Launch Under GameMaker Debugger | `run.gmsDebug` | None | None |
| Launch Profiles... | `run.profiles` | None | None |
| Show Game Log | `run.openLog` | None | None |
| Crash Reports... | `run.crashReports` | None | None |
| Restore Game Files... | `run.restoreFiles` | None | None |
| Check Mod Conflicts... | `run.checkConflicts` | None | None |

The keymap preset "UTMT" binds the debugger launch to Shift+F5 as UTMT does and moves Stop to Ctrl+F5 (presets change bindings only, ux-standards 3).

## 9. Validation and messages

| Case | Message |
| --- | --- |
| No data file | "Open a data file first." |
| Original read-only location | Allowed: the temporary file goes to app data (`run.tempLocation` forced) and Output says why |
| Runner not set for a custom launch | "Choose a runner." |
| Mod without a game match | Warning "'{mod}' is made for {game}, not {open game}." |
| Dependency missing | Error "'{mod}' needs '{dependency}'." with "Add Dependency" |
| Two enabled mods conflict | Warning "'{a}' and '{b}' change the same {kind}. The higher priority wins." |
| Restore failed | Error "Could not restore '{file}'. The backup is in '{folder}'." with "Open Backup Folder" and "Copy Details" |
| Launch with Keep Changes | Confirm: "The mods stay in the game folder until you restore them. Continue?" |

## 10. Undo

Launching changes nothing in the workspace, so there is nothing to undo. Mod steps and file operations happen on temporary copies and the game folder under the backup manifest, not in the model. Launch settings are not part of the undo stack.

## 11. Empty and loading states

- Game Log tab, no run yet: "Nothing logged yet. Launch the game with F5."
- Crash Reports, none: "No crashes recorded."
- Runtime list empty: "No GameMaker runtimes were found. Install GameMaker or set the runtimes folder in Settings > Paths." with the game executable still selectable.
- Building: the toolbar button shows a spinner and "Preparing..."; the Jobs row shows the step.

## 12. Parity coverage

| ID | Feature | Section |
| --- | --- | --- |
| UTMT-RUN-01 | Temp run | 3 |
| UTMT-RUN-02 | Run with other runner | 4 |
| UTMT-RUN-03 | Runtime picker | 4 |
| UTMT-RUN-04 | Run under GMS debugger | 6 |
| UTMT-RUN-05 | Debug data generation | 6 |
| UTMT-FILE-07 | Debugger flag warnings | 3, 4 |
| UTMT-FILE-14 | Path confinement | 3 |
| UTMT-PROJ-12 | Game file backup | 5.3 |
| UTMT-SET-05 | Debugger menu toggle | 6 |
| UTMT-SET-08 | Warn about temp running | 3 |
| G3M-13 | Priority and steps | 5.1 |
| G3M-14 | Relationship checks at launch | 5.1 |
| G3M-15 | Launch modes | 5.2 |
| G3M-16 | Chapter mode | 5.1 |
| G3M-17 | Runtime target rewrite | 5.1 |
| G3M-18 | Backup, restore, rollback | 5.3 |
| G3M-22 | Analyze actual launch result | 7.3 |
| G3M-29 | Patching warnings | 5.3 |

UTMT-RUN-06 (shell verbs) is in [onboarding.md](onboarding.md) section 7. The launch arguments and the `g3mstudio://launch` link are in [shell.md](shell.md) section 13.1.
