# Tags, feature flags and data files

Status: proposed. The editors for three small chunks: TAGS (asset tags), FEAT (runtime feature flags) and DAFL (data files, an empty chunk kept for round-trip). UTMT has no editor for any of them. Shared rules: [../ux-standards.md](../ux-standards.md). Shell: [../shell.md](../shell.md).

## 1. Purpose and entry points

Tags are labels that GameMaker projects attach to resources. The chunk keeps the tag list and a map from assets to tags. Feature flags are names of runtime features a game turns on. UTMT reads and writes both and shows nothing (UTMT-NOED-03, UTMT-NOED-04). For a modder, tags are a free way to filter resources in the Explorer (the Quick Access > Tags group in [../shell.md](../shell.md) section 5.2 already reads them), and feature flags explain why a game behaves differently on a given runtime.

Opens from Explorer > Data > Tags, Feature Flags and Data Files; Quick Access > Tags (click a tag to filter, "Edit Tags..." to open this editor); Go To `tag:` and `feat:`; and the Inspector's Tags row on any resource.

## 2. Tags (UTMT-NOED-03)

The chunk exists from GMS 2.3 and has no version marker of its own.

```
+--------------------------------------------------------------------------------------+
| Tags   9 tags, 142 tagged resources                          [search...]  [New Tag]   |
+----------------------------+---------------------------------------------------------+
| Tag               Uses     | Tag 'enemy'                                              |
| boss               4       |  Name [enemy]  (string)                                  |
| enemy            > 38      |  Tagged resources (38)   [Add Resources...]  [Remove]    |
| ui                 21      |   Type     Name                                          |
|  ...                       |   Object   obj_slime                                     |
| [New tag...]               |   Sprite   spr_slime                                     |
+----------------------------+---------------------------------------------------------+
```

1. Left: tag list with use counts; right: the selected tag's name and the resources that carry it (list grid with type icon, name, Open, Remove). "Add Resources..." opens a multi-select picker with type filters.
2. A second view, "By resource", lists resources with their tags as chips; adding or removing a chip edits the map. The Inspector shows the same chips for the selected resource (Tags row) so tagging needs no trip here.
3. Rename a tag edits its shared string with the usual prompt (ux-standards section 11). Delete a tag removes it from all resources after a count ("Remove tag 'enemy' from 38 resources?" with Undo in the notification).
4. Tag names: letters, digits, underscores, spaces and hyphens are allowed by GameMaker; G3MStudio accepts any non-empty text without leading or trailing spaces, and warns for characters other tools may reject.
5. Resources are stored by asset name or ID depending on version; the editor resolves them to the live resource, and shows entries that no longer resolve in the invalid style with "Remove Missing".
6. Export and import tags as JSON (tag name to resource names) for sharing between data files.
7. Filtering the Explorer by a tag is a shell feature; this editor offers "Show in Explorer" per tag.

## 3. Feature flags (UTMT-NOED-04)

The chunk exists from GM 2022.8: a chunk version and a list of names.

1. List grid: Name (string reference field), Uses (usually 0; the runtime reads the chunk, code does not), Remove. The last row "Add feature flag..." appends a name. Names are free text, listed in file order.
2. A "Known flags" picker offers names seen in real games (kept in the app's metadata file) with a one-line description from the research notes, so the user does not have to type them. Unknown names are valid.
3. The chunk header shows "Chunk version 1" read-only. Duplicate names are refused: "'{name}' is already a feature flag."
4. A flag's presence is what matters; there is no on/off value in the chunk. The page says so: "Feature flags are on when listed."
5. Editing a flag can change how the runtime behaves, so a banner shows once per session: "Changing feature flags can change how the game runs. Test the game after saving."

## 4. Data files chunk (UTMT-NOED-07)

DAFL is an empty chunk. Included files (loose files next to the game) are not stored in it; they appear under Explorer > Included Files. The Data Files row opens a page that says: "This chunk is empty. Included data files are stored next to the game and are listed under Included Files." with a button "Open Included Files". The chunk is written back unchanged.

## 5. Actions and shortcuts

| Command | Id | Windows and Linux | macOS |
| --- | --- | --- | --- |
| New Tag | `tags.new` | Insert | Insert |
| Delete Tag | `tags.delete` | Delete | Delete |
| Add Resources to Tag... | `tags.addResources` | None | None |
| Show Tag in Explorer | `tags.showInExplorer` | None | None |
| Remove Missing Entries | `tags.removeMissing` | None | None |
| Export Tags... | `tags.export` | None | None |
| Import Tags... | `tags.import` | None | None |
| Add Feature Flag | `features.add` | Insert | Insert |
| Remove Feature Flag | `features.remove` | Delete | Delete |

## 6. Validation and messages

| Case | Message |
| --- | --- |
| Duplicate tag | "A tag named '{name}' already exists." |
| Empty tag name | "A tag needs a name." |
| Resource in map no longer exists | Warning "Tag '{tag}' lists a resource that does not exist." with Remove Missing |
| Duplicate feature flag | See 3 |
| Tags chunk absent (before GMS 2.3) | Group hidden; Inspector Tags row hidden |

## 7. Undo

Tag operations, map edits, flag additions and removals and imports are single steps named after the action.

## 8. Empty and loading states

- No tags: "This data file has no tags. Create one to label resources."
- No feature flags: "No feature flags are set."
- DAFL: see section 4.

## 9. Parity coverage

| ID | Feature | Where |
| --- | --- | --- |
| UTMT-NOED-03 | Tags (TAGS) | 2 |
| UTMT-NOED-04 | Feature flags (FEAT) | 3 |
| UTMT-NOED-07 | Data files (DAFL) | 4 |
| UTMT-CTRL-02, UTMT-CTRL-06 | String reference field, list grid | 2, 3 |
