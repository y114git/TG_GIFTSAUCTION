# Included files

Status: proposed. A folder view of the loose files that ship next to the data file (GameMaker's Included Files, the `datafiles` folder), with open, import and external edit. These files are not inside the data file. Shared rules: [../ux-standards.md](../ux-standards.md).

## 1. Purpose and entry points

Show and manage the game's loose files (JSON, images, DLLs, language files) beside the data file, since many mods change them. Opens from Explorer > Included Files, File > Import > Included File..., the palette (`>Included Files`), and drops onto the Explorer.

## 2. Layout

```
+--------------------------------------------------------------------------------------+
| Included Files   C:\...\Undertale\   312 files, 41 MB            [Refresh] [Show in File Manager]|
+--------------------------------------------------------------------------------------+
| Name                        | Size    | Modified          | Kind        |             |
| v lang/                     |         |                   |             |             |
|     lang_en.json            | 12 KB   | 2024-03-02 11:00  | JSON        |             |
|   mus_intro.ogg             | 1.1 MB  | 2024-03-02 11:00  | Audio       |             |
+--------------------------------------------------------------------------------------+
```

A tree table of the data file's folder (and subfolders), excluding the data file itself, its backups and `audiogroupN.dat` files (shown under Linked Files). Columns Name, Size, Modified, Kind. A filter box and Kind filter (Audio, Images, JSON and text, Libraries, Other).

## 3. Actions

| Action | Behaviour |
| --- | --- |
| Open | Text, JSON, YAML, INI, XML, CSV and Markdown files open in the code editor with matching highlighting; images in a preview tab; audio in the sound preview; other files in the default program |
| Open in External Program | Uses the program set per file type in Settings > Paths; watches the file and offers Reload |
| Import Included File... | Copies chosen files or folders into the tree after a conflict table (Replace, Keep, Rename per file and for all) |
| New File, New Folder, Rename, Delete | Operate on disk after confirming; renames of files that a sound or code refers to by name show "Streamed sounds and code may refer to '{name}'. Rename anyway?" |
| Reveal | Show in the file manager |
| Refresh | Rescans; the view also updates from the file watcher |

Changes to included files are writes to the game folder, so each is explicit and listed in Output > Files; none is part of the data file undo stack. Delete moves to the recycle bin or trash where the platform has one. A file that a sound references (streamed external audio) shows a link icon and "Used by snd_bgm".

## 4. Validation and messages

| Case | Message |
| --- | --- |
| Folder read-only | "'{folder}' is read-only. Choose another folder or fix its permissions." |
| Name clash | "'{name}' already exists." |
| Path outside the game folder | "'{path}' is outside the game folder and was not used." |
| File in use | "'{file}' is in use by another program." |

## 5. States

Empty:
- No loose files: "There are no included files next to this data file."

Loading:
- Scanning: "Scanning..." with a count; scans run off the UI thread and stop early with "Showing the first 10,000 files." for huge folders.

Error:
- Folder unreadable: "'{folder}' could not be read: {error}."
- File operation failed: the messages of section 4.

## 6. Undo

Not undoable. Changes to included files are writes to the game folder, so each is explicit and listed in Output > Files. Delete moves to the recycle bin or trash where the platform has one, which is the way back. Nothing here joins the data file undo stack.

## 7. Parity coverage

- UTMT-FILE-14: section 4 (paths stay inside the expected folder).
- UTMT-PROJ-09 (external files, file copies, file deletes): the project options list the same files, see projects.md.
