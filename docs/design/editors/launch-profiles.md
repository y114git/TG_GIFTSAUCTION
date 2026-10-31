# Launch profiles, mod diagnostics and game versions

Status: proposed. The parts of the G3M launcher that are not the launch itself: profiles, desktop shortcuts, Mod Diagnostics, logs, game versions and game detection. Launching, runner choice, launch with mods, game log, crash reports and mod conflicts are in [../run-game.md](../run-game.md). Shared rules: [../ux-standards.md](../ux-standards.md). Requirements: F-SUITE-3, F-SUITE-6.

## 1. Purpose and entry points

Keep several mod setups per game, understand what a mod combination will do before launching, and manage copies of the game folder. Commands: Run > Launch Profiles..., Run > Mod Diagnostics..., Tools > Game Versions..., the Output channels, Help > Create Support Package.

## 2. Profiles (G3M-19) and desktop shortcuts (G3M-20)

Run > Launch Profiles... opens a tab with a profile list on the left and the selected profile on the right.

```
+--------------------------------------------------------------------------------------+
| Launch Profiles                                   [New] [Duplicate] [Rename] [Delete]  |
+--------------------------+-----------------------------------------------------------+
| Default                  | Profile: Hard Mode Run          Chapter Mode [ ]          |
| Hard Mode Run          > | Priority  Step  Mod                       Enabled          |
| Sprite Test              |   1        1    Hard Mode                   [x]            |
|                          |   2        2    Menu Fix                    [x]            |
| [Import ZIP] [Export ZIP]| [Create Desktop Shortcut...]  [Launch Game]                |
+--------------------------+-----------------------------------------------------------+
```

1. A profile holds the enabled mods, their priority and steps, and Chapter Mode settings (run-game.md section 5). Create, Duplicate, Rename, Delete (confirms: "Delete profile 'Hard Mode Run'? The mods stay installed."), Export ZIP and Import ZIP.
2. Profiles are stored in `profiles/<name>/` with `<name>.json` and `mods_data.json`, in the format G3M uses, so both tools read the same profiles.
3. Desktop shortcut: "Create Desktop Shortcut..." saves a selection and its steps as a shortcut that opens the link `g3mstudio://launch?file=...&profile=<name>` (shell.md section 13.1). Before saving it checks that the program path is stable and shows the target. On Linux it writes a `.desktop` file; on macOS an `.app` stub; on Windows a `.lnk`.
4. Switching profiles with unsaved edits in the profile page asks to save first.

## 3. Mod Diagnostics (G3M-21)

Run > Mod Diagnostics... opens a tab with pages Overview, File Tree, DATA Impact, Preview and Compare, and Problems.

| Page | Content |
| --- | --- |
| Overview | Counts of mods, operations, files and resources touched; severity totals; the launch order |
| File Tree | A tree of files each mod writes, with the mod name per node and overlap markers |
| DATA Impact | Resource changes per mod by type (sprites, code, rooms ...) from the patch manifests; overlap detection between mods |
| Preview and Compare | Pick two mods or a mod and the original; opens the Changes view (data-diff.md) |
| Problems | Findings: file overlap, data overlap, missing dependency, conflict, wrong order, each with severity (icon plus word), the mods involved and a suggested fix |

Filters by mod and by resource type apply to every page. All pages read manifests and run offline; nothing is applied to the game. A finding row has "Open Mod" and "Show in Mod Order".

## 4. Logs (G3M-28)

The log files `logs/g3m.log`, `logs/patching.log`, `logs/conflicts.log` and `logs/shortcut.log` (archives in `logs/g3m/` and `logs/patching/`) are shown as Output channels (Output > G3M, Patching, Conflicts, Shortcuts) and in a Logs tab: file list, history by date, search, level filter, Export and Open Folder. Each launch and patch job writes its own timestamped section.

## 5. Game versions (G3M-33)

Tools > Game Versions... opens a tab listing saved versions of the game folder with name, size and date.

| Action | Behaviour |
| --- | --- |
| Create Version | Copies the game folder to the app data folder. Option "Apply a profile to a temporary copy first" creates the version from the modded state. Shows size and free space before starting; Cancel stops and deletes the partial copy |
| Apply Version | Restores a version to the game folder. "Replace the whole game folder" is optional; otherwise only differing files change. Confirms with the file count and size: "This replaces {n} files ({size}) in '{folder}'. The current state is not kept unless you create a version first." |
| Import ZIP, Export ZIP | Uses `game_version_data.json` inside the ZIP |
| Delete | Confirms |

All actions are cancellable jobs. Archives live in the app data folder, never in the game folder.

## 6. Game detection (G3M-36)

The program recognises `data.win`, `game.ios`, `game.win`, `game.unx` and `game.droid` and detects running games. While a game runs, commands that change its files (apply version, install, launch and keep changes) are disabled with "Close the game first." The start page and onboarding use the same detection (onboarding.md section 8).

## 7. Actions and shortcuts

Commands: `run.profiles`, `run.diagnostics`, `tools.gameVersions`, `help.supportPackage`, `run.openLog` (menus in shell.md section 2). In lists: Enter opens, Delete removes after confirmation, Alt+Up and Alt+Down reorder rows.

## 8. Validation and messages

| Case | Message |
| --- | --- |
| Profile name empty or taken | "Enter a name that is not used by another profile." |
| Profile mod missing | "'{mod}' is no longer installed. Remove it from the profile or install it again." |
| Not enough space for a version | "Not enough space for this version. It needs {size} and the drive has {free}." |
| Version ZIP invalid | "'{file}' is not a game version archive. It needs game_version_data.json." |
| Shortcut target moved | "The shortcut points to a program that no longer exists. Create it again." |

## 9. Undo

Profile edits are steps in the profile page until saved. Game version actions are not undoable; they confirm and keep copies by design.

## 10. Empty and loading states

- No profiles: "Only the Default profile exists. Create a profile to keep a separate mod setup."
- Diagnostics running: "Reading mod {n} of {total}..." with Cancel.
- No versions: "No game versions yet. Create one to keep a copy of the game folder."

## 11. Parity coverage

- G3M-19, G3M-20: section 2.
- G3M-21: section 3.
- G3M-28: section 4.
- G3M-33: section 5.
- G3M-36: section 6.
