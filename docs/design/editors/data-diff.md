# Data file diff and Changes

Status: proposed. The Changes view (what differs from the original or last saved data file), the Compare Files tool, Local History compare, and the resource-aware diff views shared with Git. Built on G3MLib's diff. Shared rules: [../ux-standards.md](../ux-standards.md). Requirements: F-GIT-2, F-GIT-3, F-WS-6.

## 1. Purpose and entry points

Show exactly what changed, by resource, with a readable before and after. Opens from: the Changes panel (View > Changes), Local History > Compare, Mod > Compare Files..., the Git panel's Open Diff, the Explorer's "Modified Only" filter ("Show Changes"), and the editor gutter marker "changed against original".

## 2. Changes panel (F-GIT-3)

```
+-------------------------------------------------------------------------------+
| Changes   Against [Original file v]  [Refresh]    12 changed  3 new  1 deleted |
+-------------------------------------------------------------------------------+
| v Sprites (4)                       | spr_player   modified                     |
|     spr_player        M             |  Origin X: 32 -> 30                       |
|     spr_enemy         M             |  Frame 3: image changed [before|after]    |
|     spr_new           A             |  Collision mask: rectangle -> precise     |
| v Code (6)                          |                                           |
|     gml_Object_obj_p.. M            | [Revert This Resource] [Open Editor]      |
+-------------------------------------------------------------------------------+
```

Left: tree of changed resources grouped by type with letters M (modified), A (added), D (deleted), R (renamed or reindexed). Right: the diff of the selected resource. Header: the baseline combo "Against": Original file (as opened), Last saved, Backup N, Local history entry, Another data file... (UTMT-style compare), Git HEAD (project mode).

The panel fills as the diff runs as a job (G3MLib diff, cancellable, cached by file hash).

## 3. Diff views by resource type

| Type | View |
| --- | --- |
| Code | Side-by-side or unified text diff (toggle; default side by side above 900 px) with line numbers, intra-line highlights, F3 and Shift+F3 next and previous change |
| Strings, names, options | Table: field, before, after |
| Images (sprite frames, texture pages) | Before and after side by side with a slider overlay and a pixel difference mode; zoom linked |
| Rooms, objects, sequences and others | Property diff tree: path (for example `layers/Instances/inst_10AC2371/x`), before, after; added and removed elements listed |
| Audio | Size, format, duration before and after, with Play A and Play B |
| Binary or unknown chunks | Size and hash, hex diff on demand |

Every change row has: Revert This Change (returns that value to the baseline, one undo step), Copy, Open in Editor. Hunks in code diffs have per-hunk Revert.

Colours: added `Success`, removed `Danger`, changed `Warning`, always with the letters or +/- signs (not colour alone).

## 4. Compare Files tool (TOOL-24 to TOOL-26)

Mod > Compare Files... opens the Patch tools page with the Compare form (see patches.md section 8). Inputs: two data files, two patches, or a data file and a patch (data-vs-data, data-vs-patch, patch-vs-patch). Options: Full report (adds exact text, code and JSON hunks, deeper texture page item, reference and asset order detail), Cache folder. Output folder default: `diff/` next to the executable, changeable. Result: a Markdown report written to the folder and opened in the built-in Markdown viewer with Export; the same data feeds this Changes view. With the machine-readable option the Activity log shows the JSON summary (output path, mode, total and per-type counts, text diff count, warnings).

## 5. Local History (F-WS-6)

View > Local History lists saves of the open data file: time, size, summary ("12 resources changed"), label. Actions per entry: Compare With Current (opens Changes against that entry), Restore (replaces the workspace state, one undo step, after adding the current state as a new history entry), Rename, Delete, Export Data File. A setting caps count (default 50) and total size (default 2 GB, oldest first).

## 6. Gutter and Explorer markers

Explorer rows and tab titles show a dot when the resource differs from the baseline chosen in Settings > Changes (default Original file). In code editors a margin bar marks changed lines (green added, blue modified, red triangle deleted, each with a shape difference); click opens an inline peek of the old text.

## 7. Actions and shortcuts

| Command | Id | Windows and Linux | macOS |
| --- | --- | --- | --- |
| Next Change | `diffview.nextChange` | F3 | F3 |
| Previous Change | `diffview.previousChange` | Shift+F3 | Shift+F3 |
| Side by Side or Unified | `diffview.toggleLayout` | None | None |
| Revert This Change | `diffview.revertChange` | None | None |

## 8. Validation and messages

| Case | Message |
| --- | --- |
| Baseline file missing | "The original file was not found. Choose a baseline." with a picker |
| Files are not the same game | Warning "These files have different game names. Differences may be large." |
| Diff too large | "The diff has {n} changes. Showing the first 5,000. Export the full report." |
| Revert fails (depends on removed resource) | "Could not revert: '{name}' no longer exists. Restore it first." |

## 9. Undo

Revert of a change, resource or hunk is one step. Restore from Local History is one step.

## 10. Empty and loading states

- No changes: "No changes since the original file."
- Diff running: "Comparing... {done} of {total} resources" with Cancel.
- Binary view not available: "Hex diff is available for chunks up to 64 MB."

## 11. Parity coverage

- TOOL-24, TOOL-25, TOOL-26: section 4.
- G3M-26 (Info and Diff tools, Markdown viewer with export): section 4 and patches.md section 8.
- F-GIT-2, F-GIT-3 (not parity IDs): sections 2 and 3.
- UTMT-PROJ-06 (automatic change tracking marks changed resources): section 6 markers; "Marked for export" in projects.md.
