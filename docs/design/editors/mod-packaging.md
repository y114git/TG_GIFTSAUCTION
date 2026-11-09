# Mod packaging and export

Status: proposed. The Mod editor (replaces G3M-MCE and G3M's desktop mod editor), mod import (G3M, Deltamod, PizzaOven, plain archives), mod versions, validation, ZIP packaging, export to G3M (`g3m://`), export as patch or data file, and the `g3mstudio://` link scheme. Shared rules: [../ux-standards.md](../ux-standards.md). Requirements: F-SUITE-2, F-SUITE-4, F-SUITE-5. Format reference: [../research/modding-suite.md](../../research/modding-suite.md#g3m-mod-package-format).

## 1. Purpose and entry points

Create a mod that G3M installs: metadata, which games and chapters it targets, which files go where, tags, compatibility. Also package the current workspace changes as a mod in two clicks.

Opens as a tab "Mod: {name}" from File > New > G3M Mod..., Mod > Open Mod..., File > Export > Export as G3M Mod..., File > Import > G3M Mod..., a dropped `.zip` with `mod_config.json`, `mod_config.json` alone, or any archive or file (MCE-01 to MCE-04), and `g3mstudio://` links.

Start page of the Mod editor when nothing is open: three cards "Create Mod", "Edit Mod", "Download G3M link" (MCE-01) and a drop zone "Drop a mod archive, mod_config.json or any file here."

## 2. Opening and importing (MCE-02 to MCE-06, G3M-02 to G3M-08)

| Input | Behaviour |
| --- | --- |
| G3M ZIP (MCE-02) | Finds the single `mod_config.json` whose folder contains every file (the mod root). Rejects ambiguous roots and files outside the root, ignores `__MACOSX/`, migrates the config |
| `mod_config.json` alone (MCE-03) | Opens the config without bundled files; the Files tab marks sources "Not bundled" and the packaging check fails until they are added |
| Plain ZIP or single file (MCE-04) | Starts an empty config with the contents bundled and unassigned in the file tree |
| Deltamod (MCE-05, G3M-03) | One root `meta.json`, `meta.toml` or `_deltamodInfo.json` plus `modding.xml` (each at most 4 MiB, no DOCTYPE or ENTITY, 1 to 5000 `<patch>` entries). Maps game ids (`toby.deltarune` to `deltarune` and the rest of the table) and shows an import summary with what could not be converted. The game map includes `other.frickbears3` (G3M's lacks it) |
| Legacy config (MCE-06, G3M-02) | Config version empty, null or `1.0.0` with object-shaped files: merges metadata, maps section keys (-1 deltarune, -10 deltarunedemo, -20 undertale, -30 undertaleyellow, -40 pizzatower and the rest) and migrates chapter ids. A banner lists "Converted from version 1.x" and what changed |
| PizzaOven and CYOP/AFOM (G3M-04) | Converts packages. For `data.win` it tries a patch (`data.g3mpatch`), then an xdelta patch (`data.win.xdelta`), then a full `data.win`; each result is accepted only if it re-applies to the original |
| Unconfigured files (G3M-05, manual install) | A wizard builds a mod from files: per file the action is Copy/Replace, Apply patch, INFO/README, Extract or Skip. `.g3mpatch` files are auto-matched by `g3mpatch.json` original MD5 and size and modified MD5; `.xdelta` and `.vcdiff` by name hints |
| Package with `mod_versions/` (G3M-06) | Asks which versions to import and which is active; only versions whose config is current or migratable are offered |
| Duplicate mod id (G3M-07) | Dialog "A mod with this id exists": Merge (save the incoming package as a version) or Replace (install in place) |
| Unknown game (G3M-08) | Dialog: Install Manually or Add Game (a custom game dialog prefilled with the `game` id and GameBanana game id). Custom ids look like `custom_*` |

Errors follow ux-standards section 8, for example: "Could not open 'mod.zip'. It contains two mod_config.json files, so the mod root is unclear. Remove one and try again."

## 3. Editor layout

```
+--------------------------------------------------------------------------------------+
| Mod: Hard Mode   id hard_mode  v1.2.0  Undertale      [Validate] [Save ZIP...] [Export v]|
+--------------------------------------------------------------------------------------+
| Metadata | Compatibility | Files | Placeholders | Help | Preview                      |
+--------------------------------------------------------------------------------------+
|  Files tab:                                                                           |
|  Operations                                  | Bundled files        [search...]         |
|  v Chapter 1                                  | v patches/                                |
|     patch  data.g3mpatch -> ${game_path}/data.win | data.g3mpatch  (used)                |
|  info  README.md                              | README.md       (used)                   |
|  [Add Operation] [New Group]                  | [Add Files] [Add Folder]                 |
+--------------------------------------------------------------------------------------+
```

Tabs: Metadata, Compatibility, Files, Placeholders, Help, Preview. Field errors highlight after edit or on a save attempt; an error summary "There is a problem" at the top links to each field, opening the tab and expanding collapsed groups (MCE-20). Unsaved changes are guarded: closing or navigating asks to Save, Discard or Cancel (MCE-25, though the workspace hot-exit journal also keeps the draft).

## 4. Metadata tab (MCE-07, MCE-08, G3M-09)

| Field | Control | Rules |
| --- | --- | --- |
| Game | Combo of 7 built-ins (deltarune, deltarunedemo, undertale, undertaleyellow, pizzatower, sugaryspire, frickbears3) plus "Custom game ID..." | Custom id pattern `[a-z][a-z0-9_-]{0,63}` |
| Mod name | Text | 1 to 128 chars |
| Mod ID | Text | Pattern `[a-z][a-z0-9_-]{0,63}`; "Generate from name" button |
| Mod version | Text | Non-empty, at most 128 chars |
| Game version label | Text | At most 128 chars |
| Authors | Multi-line text, one author per line (comma separated in the desktop import) | At most 64 authors of at most 128 chars; empty allowed |
| Description | Multi-line text | At most 16,384 chars, counter shown |
| Homepage | Text | Absolute HTTP or HTTPS URL, at most 2,048 chars |
| Icon | Path field with drop target (image file or URL) | `${mod_path}/...` image or HTTP(S) URL |
| Tags | Checkbox group (MCE-08): textedit, customization, gameplay, other; CYOP/AFOM shown only for Pizza Tower; FB-Addon only for Frickbears 3 | Imported labels map by keyword to the closest tag, unknown to `other`; "Tags are not valid for this game." when a game-limited tag is on after the game changes |

## 5. Compatibility tab (MCE-09)

Two list grids, Dependencies and Conflicts. Columns: Mod ID (text), Order (combo: No order, before, after, before-step, after-step, before-priority, after-priority). Rules: at most 512 each; no duplicates or self-relations ("A mod cannot depend on itself.").

## 6. Files tab (MCE-10 to MCE-15, G3M-09)

Operations list (left) and bundled files tree (right).

| Field | Control | Rules |
| --- | --- | --- |
| Operation type | Combo of 8: patch, overwrite, soft-overwrite, hard-overwrite, extract, soft-extract, hard-extract, info | Info has no target |
| Source | Path with drop target and a Browse in the bundle | Must start with a root: `${mod_path}`, `${game_path}`, `${game_data_path}`, `${user_path}` or a custom placeholder. Forward slashes; trailing `/` means a directory; `..` and backslashes rejected |
| Target | Path | Same roots; targets inside `${mod_path}` rejected |
| Include source hash, include target hash | Checkboxes with the computed `sha256:` value shown, copy button | MCE-16 |

1. Add Operation (Insert), Delete (Delete), reorder with Alt+Up and Alt+Down or drag. Dragging over a tab opens it after a delay; the arrow buttons do the same (MCE-12).
2. Groups (MCE-11): New Group; name 1 to 128 chars, unique case-insensitively, not `source` or `type`; nest up to 32 levels, at most 2,000 groups; remove asks to confirm and says how many operations it contains. DELTARUNE chapters are usually one group each, with targets like `${game_path}/chapter2_windows/data.win`.
3. Bundled files tree (MCE-13): files and folders with search, Add Files, Add Folder (keeps structure), remove with confirmation; clicking a file sets the selected operation's source or creates an operation. Files without an operation stay in the package (script dependencies) and show "Not used by an operation".
4. Drop targets (MCE-14): drop on Source (sets path), Icon (image or URL), placeholder path, and hash fields (hash without bundling). Dragging a bundled file onto a field uses the internal drag format; the keyboard alternative is the Browse button.
5. Browse inside a bundled ZIP (MCE-15): the source picker can choose a file or folder inside a bundled archive as `${mod_path}/x.zip/member`; archives of ZIP, 7z, RAR, TAR and compressed TAR read, RAR cannot be a target, no nesting.
6. Hash calculation (MCE-16): the source hash for a bundled file, folder or ZIP member uses G3M's algorithm (SHA-256 for files; for folders a hash over NFC-normalized, UTF-8-sorted relative paths with length-prefixed fields). The target hash comes from a local original file that is not bundled. Hashes work for 7z, RAR, TAR and compressed archives too.
7. Operation result preview: a line under each operation shows what it will do in words ("Applies patch data.g3mpatch to the game's data.win using patch apply"), including the runtime rewrite of data file names for the player's platform (G3M-17): `data.win`, `game.unx`, `game.ios` become the runtime's name, and `chapterN_windows` becomes `chapterN_mac` on macOS.

## 7. Placeholders tab (MCE-17)

List grid of custom placeholders: Name (pattern `[A-Za-z][A-Za-z0-9_]{0,63}`, unique case-insensitively, built-in names reserved), Path (one built-in root plus a path; no nesting). At most 128 entries. The built-in roots are listed read-only at the top with their meaning.

## 8. Help and Preview tabs (MCE-18, MCE-19)

Help: short topics for metadata, placeholders, custom placeholders, operations, processing order, compatibility and hashes. Preview: read-only text view of the generated `mod_config.json` (2-space indent, trailing newline), with Copy and "Save As...".

## 9. Validation (MCE-20, MCE-21, MCE-22, G3M-01)

Validation runs live per field and fully on Validate or Save. Rules come from the G3M mod format 2.0.0: unknown fields, duplicate JSON keys and nesting above 64 levels are rejected; display strings are NFC-normalized; paths limited to 4,096 chars; at most 5,000 operations; `mod_config.json` at most 4 MiB.

Package checks (MCE-21): every source must be bundled (files, folders, ZIP members); declared source hashes are recomputed and compared; the root must not contain `meta.json`, `meta.toml`, `_deltamodInfo.json` or a bundled `mod_config.json`; a file with these names would make G3M treat the ZIP as Deltamod.

Package limits (MCE-22): at most 10,000 entries, 1 GiB total, 512 MiB per file; encrypted and split ZIPs, symlinks, compression ratio above 1000:1 on import, duplicate paths, case conflicts and unsafe names are rejected with the reason.

Messages: "'{field}' is required.", "Mod ID must start with a lowercase letter and use only lowercase letters, digits, hyphens and underscores.", "The source '{path}' is not in the package. Add the file or change the path.", "Hash mismatch for '{path}': expected {a}, found {b}. Recalculate the hash or replace the file." Each links to the field.

## 10. Saving and export (MCE-23, MCE-24, G3M-10, F-SUITE-4)

Export menu (File > Export):

| Command | Result |
| --- | --- |
| Save ZIP... (MCE-23) | Writes the canonical `mod_config.json` (2-space indent, trailing newline) at the root, all files and directories (including empty ones), DEFLATE level 6; entries over 1000:1 are stored uncompressed. Atomic: temp file, then replace. Asks which saved versions travel along (G3M-10): active only, all, ask |
| Export to G3M (MCE-24) | After Save ZIP, opens `g3m://file:///...` built from the saved file's absolute path (trimmed, backslashes converted, `.zip` required, no `.` or `..` segments, percent-encoded per segment). If G3M is installed the OS opens it; otherwise the dialog shows the link with Copy and "Show File". No full-path prompt is needed because the app already knows the path |
| Export as G3M mod from workspace | Wizard: choose which changes (all resources, a selection, or by type), how to ship them (g3mpatch, xdelta, full data file, or selected resources as files), metadata (prefilled from the game), and options per resource type with or without settings: for sprites, images only, or images with origin, mask and speed (F-SUITE-4). Result is a ready mod project the user can still edit |
| Export as patch / modified data file / selected resources / project folder | The matching commands in the File > Export menu; each opens a short dialog with the same "with or without settings" choice |
| Drag-out export (G3M-10) | Dragging a mod from the Library of mod projects to the file manager exports the ZIP |

## 11. Mod versions (G3M-11)

A Versions panel lists saved versions of the mod (archives in `mod_versions/` inside the mod): Save Current, Import From File, Download From GameBanana, Switch, Delete. Switching loads that version into the editor after saving the current one; the active version is marked. Deleting asks to confirm.

## 12. Modpacks and conversion (G3M-12, G3M-13)

"Create Modpack..." (Mod menu): choose mods and steps; a priority list with steps (mods in one step share the same input data and merge with patch merge, higher row wins; steps run in order on the previous step's result). The tool applies the selection to a temporary game copy and bundles the result as one mod; option "Use xdelta instead of full data files". Progress as a job; the result opens as a mod project. Same logic as launch with mods (run-game.md section 5.1).

## 13. Links: `g3mstudio://` and forwarding (G3M-31, G3M-32, G3M-37)

Scheme: `g3mstudio://`, registered like G3M's `g3m://`: Windows `HKCU\Software\Classes\g3mstudio`, Linux `.desktop` with `x-scheme-handler/g3mstudio` and `xdg-mime default`, macOS in `Info.plist`. A second launch forwards the raw URL to the running instance (single instance socket, 500 ms) and exits.

Actions:

| Form | Behaviour |
| --- | --- |
| `g3mstudio://file:///C:/path/data.win`, `.zip`, `.g3mpatch` | Opens the local file (existing file only; `netloc` empty or `localhost`; percent-decoded; UNC paths rejected) in a window; mods open in the Mod editor, patches in Patch Tools, data files as a workspace |
| `g3mstudio://https://host/path.zip` | Shows "Download and open this mod?" with the host and file name, then downloads to the cache and opens it. Always confirms |
| `g3mstudio://action/<name>` | Named actions limited to: `open`, `new-mod`, `patch-tools` |

Parsing follows G3M: strip the scheme, cut at the first comma (commas must be `%2C`), trim, strip trailing `/`, repair `https//` and `http//`; anything else shows "Link not recognised." While a game launched by G3MStudio is running, links open files but refuse downloads.

G3M's own schemes: `g3m://` and `deltahub://` links are for G3M (install). G3MStudio does not take them over; "Export to G3M" produces `g3m://file:///...` and "Send to G3M" uses it. GameBanana remote install pairing (`g3m://gb_pair/...`, G3M-32) and the GameBanana "g3m" compatibility label (G3M-37) are G3M features for players; G3MStudio does not handle them. Links of the form `g3m://gb_pair/...` and `g3m://https://...` are forwarded to G3M when it is installed, and the Mod editor only edits the tags the GameBanana label is based on.

## 14. Support package (G3M-30)

Help > "Create Support Package..." collects diagnostics with redaction: logs, settings (secrets and personal paths removed), Output channels, the Problems list, and reads `g3mpatch.json` from every `.g3mpatch` involved (up to 8 MiB each). It shows a preview of every file and a redaction summary before saving a ZIP; nothing is uploaded.

## 15. Actions and shortcuts

| Command | Id | Windows and Linux | macOS |
| --- | --- | --- | --- |
| Add Operation | `modpkg.addOperation` | Insert | Insert |
| Delete Operation | `modpkg.deleteOperation` | Delete | Delete |
| Move Operation Up | `modpkg.moveUp` | Alt+Up | Option+Up |
| Move Operation Down | `modpkg.moveDown` | Alt+Down | Option+Down |
| New Group | `modpkg.newGroup` | None | None |
| Add Files... | `modpkg.addFiles` | None | None |
| Add Folder... | `modpkg.addFolder` | None | None |
| Calculate Hash | `modpkg.hash` | None | None |
| Preview mod_config.json | `modpkg.previewConfig` | None | None |
| Save Mod ZIP... | `modpkg.pack` | None | None |

## 16. Undo

Field commits, operation add, delete, reorder, group and placeholder edits are steps in the mod editor's own history. The history is per mod project and persists until close; Ctrl+Z works across tabs of the editor.

## 17. Empty and loading states

- New mod: Metadata tab with defaults, Files tab with "No operations yet. Add an operation or drag files here."
- Package build: progress with the current file name; Cancel deletes the temporary ZIP.
- Archive scan: "Reading '{file}'..." with a bar for large archives.

## 18. Parity coverage

- MCE-01, MCE-02, MCE-03, MCE-04: sections 1 and 2.
- MCE-05, MCE-06, G3M-02, G3M-03, G3M-04, G3M-05, G3M-06, G3M-07, G3M-08: section 2.
- MCE-07, MCE-08, G3M-09: section 4.
- MCE-09: section 5.
- MCE-10, MCE-11, MCE-12, MCE-13, MCE-14, MCE-15, MCE-16: section 6.
- MCE-17: section 7.
- MCE-18, MCE-19: section 8.
- MCE-20, MCE-21, MCE-22, MCE-25, G3M-01: sections 3 and 9.
- MCE-23, MCE-24, G3M-10: section 10.
- MCE-26: localization follows ux-standards section 13 (all seven languages).
- G3M-11: section 11.
- G3M-12, G3M-13: section 12.
- G3M-17: section 6, item 7.
- G3M-30: section 14.
- G3M-31, G3M-32, G3M-37: section 13.
