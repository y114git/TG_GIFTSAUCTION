# Patches: create, apply, validate, merge

Status: proposed. The Patch tools page replaces the G3MTool GUI and covers every G3MTool command: patch create, apply, validate, merge, batch, compare, inspect, xdelta, and running scripts and programs. It also covers the patch tools of G3M. Shared rules: [../ux-standards.md](../ux-standards.md). Requirements: F-MOD-1, F-SUITE-1, F-MOD-4.

## 1. Purpose and entry points

Make a mod from a changed data file, apply someone else's mod, check that a patch fits a game, and combine several mods. Opens as a tab "Patch Tools" from Mod > Patch > ..., File > Export > Export as Patch..., the palette (`>Patch`), a dropped `.g3mpatch`, `.xdelta`, `.vcdiff` or `.csx` file, and the Library of a mod project. One operation is preselected by the entry point.

The page works without an open workspace (inputs are files). With a workspace open, "Use Current Workspace" fills the original or modified field from it (including unsaved edits, via a temporary save).

## 2. Layout

```
+--------------------------------------------------------------------------------------+
| Patch Tools                                                                           |
+------------------------+-------------------------------------------------------------+
| Operation              | Create Patch                                                 |
|  o Create patch        |  Original data file   [ C:\...\data.win        ] [Browse]    |
|  o Apply patch         |  Modified file        [ C:\...\modded.win      ] [Browse] [Use Current Workspace]|
|  o Validate patch      |  Output               [ C:\...\mod.g3mpatch    ] [Browse]    |
|  o Merge patches       |  [ ] Create xdelta instead   [ ] Embed xdelta fallback       |
|  o Compare files       |  [ ] Batch mode                                               |
|  o Inspect file        |  Cache folder         [ ...                    ] [Browse]    |
|  o Create xdelta       |                                                              |
|  o Apply xdelta        |  [Run Operation]  Ctrl+Enter   [Clear]  [Copy Command Line]   |
|  o Run xdelta args     +-------------------------------------------------------------+
|  o Run script          | Activity                                    [Expand] [Save] [Clear]|
|  o Run program         |  [=====>      ] Creating patch: 42%                          |
+------------------------+  colour-coded selectable log...                              |
                         +-------------------------------------------------------------+
```

Left: 11 operations as a radio list (TOOL-42). Right: the form of the selected operation, then the Activity panel (TOOL-53). The Activity panel is also a bottom panel tab (Jobs) so it stays reachable while editing.

## 3. Common controls (TOOL-43, TOOL-49, TOOL-50)

1. Path inputs: text box with Browse and drag-and-drop; Add Files appends to multi-value lists, which take one item per line and can be reordered with Alt+Up and Alt+Down. Recently used paths drop down per field.
2. Cache folder (TOOL-10, TOOL-49): optional `.g3mcache` directory used by create, apply, validate, merge, batch, info and diff. A help text says "Speeds up repeat runs. Safe to delete." with "Clear Cache" (confirms with size).
3. Output file dialogs suggest the right extension: `.md` for Compare, `.xdelta` for Create xdelta, `.win` (or the original's extension) for Apply, Apply xdelta and Run script.
4. Every field validates on commit; Run is disabled with a tooltip naming the first missing field ("Choose the original data file.").
5. Form reset: Clear resets fields of this operation and never deletes outputs (TOOL-56).
6. Copy Command Line: copies the equivalent G3MTool command (`G3MTool patch create ...`, section 10) so work can be repeated in the G3MTool CLI.

## 4. Operations

Inputs may be `.g3mpatch`, `.xdelta`, `.vcdiff`, `.csx` or a data file (`.win`, `.ios`, `.droid`, `.unx`) wherever a patch is accepted (TOOL-37). Data outputs are validated before they replace a file (TOOL-40, TOOL-57).

### 4.1 Create Patch (TOOL-07 to TOOL-10)

| Field | Control | Notes |
| --- | --- | --- |
| Original | Path | Required |
| Input | Path (patch, script or modified data file) | The input is materialized against the original and the result validated before the patch is written |
| Output | Path | Default `{input}.g3mpatch` |
| Create xdelta instead | Checkbox (TOOL-08) | Writes `.xdelta`; disabled while the fallback box is on: "Choose one of xdelta and the fallback." |
| Embed xdelta fallback | Checkbox (TOOL-09) | Stores an exact binary patch under `Xdelta/` inside the `.g3mpatch` |
| Cache folder | Path | |

### 4.2 Apply Patch (TOOL-11, TOOL-12, TOOL-51)

| Field | Control | Notes |
| --- | --- | --- |
| Data file | Path | |
| Patch | Path (any supported input; a `.csx` gets the data as its data global) | Converted to a temporary `.g3mpatch` first, then applied |
| Output | Path | Default `{data}_patched{ext}`; the result is saved and reopened to verify |
| Use xdelta fallback | Checkbox | Off: normal apply, then the embedded xdelta if apply fails and one exists. On: embedded xdelta first, then normal apply |

Apply Patch also runs from the Explorer toolbar on the open workspace ("Apply Patch to This Data File"): it applies in memory as one undo step.

### 4.3 Validate Patch (TOOL-13)

Fields: Patch (path), Data file (optional), Cache folder. Result in the log and a summary card: archive OK, manifest OK, compatible with the data file (original MD5 and size match, resources present), warnings. Validation errors list file and field.

### 4.4 Merge Patches (TOOL-14 to TOOL-19)

| Field | Control | Notes |
| --- | --- | --- |
| Original | Path | |
| Patches | Ordered list of inputs, lowest priority first (the order of G3MTool), with a priority note "Later rows win" and Alt+Up and Alt+Down | Any mix of supported inputs. Each input is derived independently from the same original |
| Apply to data file | Output path | Writes merged data (`--apply`) |
| Keep merged patch | Output path | Writes the merged `.g3mpatch` (`--out`); with neither output, a merged `.g3mpatch` is written to the default folder shown in the field hint |
| Merge GML code | Checkbox (TOOL-16) | 3-way merge of code files |
| Merge JSON properties | Checkbox (TOOL-17) | Deep merge of property files |
| Low-memory merge | Checkbox (TOOL-19) | Sequential; excludes the two above: "Low-memory merge cannot be combined with code or property merge." |
| Report | Output path (TOOL-18) | Markdown merge report: conflicts and decisions |

Unchecked options resolve overlaps by priority (TOOL-47). The merge priority order in G3M lists highest priority first (G3M-25); the page shows a toggle "List order" with "Lowest first (G3MTool)" and "Highest first (G3M)" so imported lists keep meaning; the stored form is always the CLI order. A merge with conflicts still completes with exit status 0 and shows the report summary "Merged with 4 conflicts. Open Report."

### 4.5 Compare and Inspect (TOOL-24 to TOOL-29, TOOL-52)

Compare: two inputs, output folder (default `diff/`), Full report checkbox (TOOL-25), cache. Result opens in the Changes view (data-diff.md). Inspect: one input, Verbose checkbox (TOOL-28; always rereads the data file), cache; writes data file info (resource counts, general info, short breakdowns) or the patch manifest summary into the Activity log and the info cache (TOOL-27, TOOL-29, TOOL-52). A "Copy as JSON" button shows the machine-readable result.

### 4.6 xdelta (TOOL-30, TOOL-31, TOOL-34, TOOL-45)

Create xdelta: original, modified, output (default `{modified}.xdelta`). Apply xdelta: original, patch, output (default `{original-stem}_patched{ext}`). Run xdelta with arguments: free text for the arguments, run through the bundled or configured binary. All three use the xdelta path from Settings > Tools (`--xdelta-path`, TOOL-06).

### 4.7 Run script and Run program (TOOL-32, TOOL-33)

Run script: script (`.csx`), data file, output file, input directory (optional, passed to the script as its first argument), extra arguments. The output is written after success. Interactive script dialogs (TOOL-54: question, input with title, label, default and multi-line, open file, save file, choose directory) appear as native dialogs; Cancel returns no selection; the script runs off the UI thread. Run program: executable, arguments (one per line), working directory; the exit code is shown. See scripting.md for the script host.

## 5. Batch mode (TOOL-20 to TOOL-23, TOOL-44, TOOL-46)

The "Batch mode" checkbox turns Create, Apply or Merge into independent jobs on one original.

| Operation | Batch inputs | Output |
| --- | --- | --- |
| Create | Modified files (list) | Output directory; names `{input}.g3mpatch` or `.xdelta` |
| Apply | Patches (list) | Output directory; names `{original}_{input}{original-ext}` |
| Merge | One comma-separated low-to-high set per line | Data output directory (apply) and patch directory (out), plus options code, properties, report |

Shared options: Continue after errors (TOOL-46), Use xdelta fallback, Cache folder. Identical jobs (same input hashes) run once and the earlier output is copied under the next name (TOOL-23). The job list shows each job with status, duration, output, and a summary at the end ("12 done, 1 failed"). Jobs run sequentially.

## 6. Activity panel (TOOL-53 to TOOL-55)

1. A progress bar with the operation label ("Creating patch: 42%"), values monotonic per operation, 100% at completion.
2. A selectable, colour-coded log with timestamps and level icons (info, warning, error), filters by level, search, Expand (full tab), Save to file, Clear. Warnings are lines starting `[Warning]`; errors use the error pattern with a stable code.
3. Run is disabled while a job runs (TOOL-55). Unlike G3MTool, Cancel is available: it stops at the next safe point, deletes partial outputs and logs "Cancelled. No changes were made." Closing the tab or window during a job asks "A patch operation is running. Cancel it and close?".
4. Results end with a card: Open Report, Show in File Manager, Open Output as Workspace (for data outputs).

## 7. Convert and G3M parity (G3M-23 to G3M-27)

1. Convert DATA files (G3M-23): Mod > Patch tools > Convert: input is a mod project (see mod-packaging.md); the operation converts its patch and data operations to `g3mpatch`, `xdelta`, `data.win`, `game.ios`, `game.unx` or `game.win` and saves the result as a new mod version.
2. Patch tool modes (G3M-24): the operation list covers g3mpatch, xdelta and csx modes; actions Create, Apply, Convert; batch create and apply; continue on error; embed xdelta fallback. A CSX apply runs the equivalent of `execute <script> --data <orig> --output <out>`.
3. Merge tool (G3M-25): section 4.4 including report `{base}_merge_report.md` and batch sets.
4. Info and Diff tools (G3M-26): section 4.5 and data-diff.md; the Markdown viewer exports.
5. Tool settings (G3M-27): Settings > Tools has custom G3MTool path (used only for "Run external G3MTool"), custom xdelta path, Clear G3MTool cache (`<data>/cache/G3MTool`), and the default switches for code and properties merge. G3MStudio's own engine is G3MLib, so the G3MTool path is optional.

## 8. Compare form

Same layout as other forms: File 1, File 2 (data file, patch or either), Output folder, Full report, Cache folder; Run. Result opens the Markdown report and the Changes view.

## 9. Output and exit behaviour (TOOL-38 to TOOL-41)

1. Progress text in the log mirrors the G3MTool progress (`{operation}: {N}%`); warnings and errors keep the `[Warning]` and `Error:` prefixes in saved logs.
2. Success shows a green check card; failure shows an error card with the code, message and "Copy Details". A merge with conflicts counts as success with a warning (TOOL-39).
3. Outputs are validated before they replace a file; previous batch outputs are kept (TOOL-40). When replacing an existing output fails, the old file stays and the log says so (TOOL-57).
4. Game-specific data (TOOL-41): the folder `GameSpecificData` is bundled with G3MStudio; the manifest `tool` field is stamped "G3MStudio" with its version. If the folder is missing the page shows "Game-specific data was not found. Reinstall G3MStudio."

## 10. G3MTool command line equivalents (TOOL-01 to TOOL-06)

G3MStudio has no command line (questions.md Q24). Scripting and automation use the G3MTool CLI, which keeps its own command grammar so scripts and G3M keep working. This table maps the G3MTool options to the screens of this page.

| G3MTool CLI feature | GUI home |
| --- | --- |
| Interactive prompt (TOOL-01) | G3MTool CLI only; the GUI equivalent is the Script Console (scripting.md) |
| `-v`, `--verbose` (TOOL-02) | Activity panel "Verbose" toggle and Settings > Tools > Log level |
| `-l`, `--log <path>` (TOOL-03) | Settings > Logs: "Write a log file per operation" with a folder |
| `--json` (TOOL-04) | Copy as JSON button on results |
| `--version`, `-V` (TOOL-05) | Help > About |
| `--xdelta-path` (TOOL-06) | Settings > Tools > xdelta path |

## 11. Actions and shortcuts

| Command | Id | Windows and Linux | macOS |
| --- | --- | --- | --- |
| Run Operation | `patches.run` | Ctrl+Enter | Cmd+Enter |
| Cancel Operation | `patches.cancel` | Escape | Escape |
| Clear Form | `patches.clearForm` | None | None |
| Copy as G3MTool Command Line | `patches.copyCommand` | None | None |
| Save Activity Log... | `patches.saveLog` | None | None |
| Open Report | `patches.openReport` | None | None |

## 12. Validation and messages

| Case | Message |
| --- | --- |
| Missing required path | "Choose the {field}." |
| Output same as input | "The output cannot be the same file as the input." |
| Patch not for this game | "This patch was made for a different file (original MD5 {a}, yours {b}). Applying may fail. Apply anyway?" with Apply Anyway and Cancel |
| xdelta and fallback both on | Section 4.1 |
| Low-memory with code merge | Section 4.4 |
| Output exists | "'{file}' exists. Replace it? The old file is kept as '{file}.bak'." |
| Tool failed | "Could not {operation}. {reason}. {next step}." with Copy Details |

## 13. Undo

Operations on files are not undoable but never overwrite without a backup. Applying a patch to the open workspace is one undo step.

## 14. Empty and loading states

- First open: operation list and an example line under the form ("Example: choose your original data.win and a modified copy, then Run.").
- Activity empty: "Nothing has run yet."
- Large inputs: indeterminate progress with the stage name until the first percentage arrives.

## 15. Parity coverage

- TOOL-01, TOOL-02, TOOL-03, TOOL-04, TOOL-05, TOOL-06: section 10.
- TOOL-07, TOOL-08, TOOL-09, TOOL-10: section 4.1.
- TOOL-11, TOOL-12, TOOL-51: section 4.2.
- TOOL-13: section 4.3.
- TOOL-14, TOOL-15, TOOL-16, TOOL-17, TOOL-18, TOOL-19, TOOL-47: section 4.4.
- TOOL-20, TOOL-21, TOOL-22, TOOL-23, TOOL-44, TOOL-46: section 5.
- TOOL-24, TOOL-25, TOOL-26, TOOL-27, TOOL-28, TOOL-29, TOOL-48, TOOL-52: section 4.5 and data-diff.md.
- TOOL-30, TOOL-31, TOOL-34, TOOL-45: section 4.6.
- TOOL-32, TOOL-33, TOOL-54: section 4.7.
- TOOL-37, TOOL-40, TOOL-57: sections 4 and 9.
- TOOL-38, TOOL-39, TOOL-41: section 9.
- TOOL-42, TOOL-43, TOOL-49, TOOL-50, TOOL-56: sections 2 and 3.
- TOOL-53, TOOL-55: section 6.
- G3M-23, G3M-24, G3M-25, G3M-26, G3M-27: section 7.
