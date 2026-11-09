# Languages

Status: proposed. The editor for the LANG chunk: the language table of a game, with entry IDs, languages, regions and the text of each entry. Shared rules: [../ux-standards.md](../ux-standards.md). Shell: [../shell.md](../shell.md). The General Info page "Language" shows the read-only summary and links here ([general-info.md](general-info.md) section 3.6).

## 1. Purpose and entry points

A few GameMaker games store their translated text in the LANG chunk: a list of entry IDs and, for each language, a name, a region and one string per entry. The game picks a language by name. UTMT shows only the counts (UTMT-GEN-08: an unknown value, the language count and the entry count) and no entries. G3MStudio edits the table as a grid, so modders can translate or correct a game's built-in languages without code changes.

Opens from Explorer > Data > Language, the General Info Language page ("Open Language Editor"), Go To `lang:`, and string Find Usages results that name a language entry.

## 2. Layout

```
+---------------------------------------------------------------------------------------+
| Languages   2 languages, 214 entries                [search...] [Aa]  [Add Language] [..]|
+-----------------------------------------------------------------------------------------+
| Entry ID            | en (US)                  | ru (RU)                | missing         |
| menu_start          | Start game               | Start game (ru)        |                 |
| menu_quit           | Quit                     | Quit (ru)              |                 |
| item_potion_desc  > | Restores 20 HP.          |                        | ru              |
|  [Add entry...]                                                                         |
+-----------------------------------------------------------------------------------------+
| Language details:  Name [en]  Region [US]   Entries 214   Missing 0   [Copy to new...]   |
+-----------------------------------------------------------------------------------------+
```

A virtualized grid: one row per entry ID, one column per language, plus a "missing" column that lists languages with an empty cell. The detail bar below shows the selected column's language fields. Header: counts and tools. Cells are String reference fields (editing a shared string asks the shared-string question, UTMT-AST-08). Columns freeze the Entry ID column.

## 3. Language table

### 3.1 Header fields (UTMT-GEN-08)

| Field | Control | Rules |
| --- | --- | --- |
| Unknown value | Read-only number | The first field of the chunk; kept as stored and shown with a tooltip "Purpose unknown. Kept unchanged." |
| Language count | Read-only | Equals the number of columns |
| Entry count | Read-only | Equals the number of rows |

The counts update as languages and entries are added or removed, so they cannot go out of sync with the table.

### 3.2 Languages (columns)

| Field | Control |
| --- | --- |
| Name | Text, for example `en` or `English`; unique |
| Region | Text, for example `US` |

Column commands: Add Language (empty, or "Copy From..." an existing language), Rename, Delete (confirm: "Delete language '{name}' with {n} strings?", undoable), move left and right with Alt+Left and Alt+Right.

### 3.3 Entries (rows)

Entry IDs are strings the game uses to look up text. Add entry (Insert) asks for an ID and creates empty cells for all languages; duplicate IDs are refused. Delete removes the row from all languages. Rename an ID asks the usual shared-string question because the ID string may be used elsewhere. Reorder is not needed; sorting the view (header click) does not change file order, and "Sort File Order" is an explicit command.

## 4. Editing and translation helpers

1. Edit a cell with F2 or typing; Enter commits and moves down; Tab moves right; Escape reverts. Multi-line text is edited in a popup (Shift+Enter inserts a line break).
2. Filter: "Missing translations" shows rows with an empty cell in any language; "Same as source" shows rows where a language equals the base language; a Language combo picks the base.
3. Search covers IDs and text in all languages; Replace is available in the cell scope.
4. Import and Export: CSV (ID, language columns) and JSON. Export includes a column per language; import matches by ID and shows a dry-run list (added, changed, unchanged) before Apply. This is the intended translator workflow.
5. Character check: a cell with characters that the game's fonts do not have (the font ranges from [fonts.md](fonts.md)) gets a marker and a Problems information item "The font '{font}' has no glyph for {n} characters." The check is a hint; the game may use other fonts.
6. Placeholder check: format markers such as `{0}` or `%s` that differ from the base language raise a warning in the cell.

## 5. Actions and shortcuts

| Command | Id | Windows and Linux | macOS |
| --- | --- | --- | --- |
| Add Entry | `languages.addEntry` | Insert | Insert |
| Add Language... | `languages.addLanguage` | None | None |
| Delete Language | `languages.deleteLanguage` | None | None |
| Move Language Left | `languages.moveLeft` | Alt+Left | Option+Left |
| Move Language Right | `languages.moveRight` | Alt+Right | Option+Right |
| Show Missing Translations | `languages.showMissing` | None | None |
| Export... | `languages.export` | None | None |
| Import... | `languages.import` | None | None |
| Sort File Order | `languages.sort` | None | None |

## 6. Validation and messages

| Case | Message |
| --- | --- |
| Duplicate language name | "A language named '{name}' already exists." |
| Duplicate entry ID | "'{id}' is already an entry." |
| Empty ID | "An entry needs an ID." |
| Import with unknown language column | "Column '{name}' is not a language of this game. Add it as a new language?" |
| Cell much longer than the base text | Information "This text is {n} times longer than '{base}'. It may not fit." |
| Chunk has pointers beyond the input | Reported by the loader as a load warning; the editor shows the part that was read |

## 7. Undo

Undoable: cell commits, language and entry add and delete, imports and sort. Grouping: typing coalesces until commit; every other action is one step named after it.

## 8. States

Empty:
- Data file without LANG: the Explorer row is hidden; General Info shows "This data file has no language table." (general-info.md).
- LANG with languages but no entries: "This language table has no entries. Add an entry or import a CSV."

Loading:
- A grid with over 10,000 rows is virtualized; row heights are fixed (one line) with a popup editor for long text.

Error:
- Table unreadable: "The language table could not be read: {error}. The part that was read is shown."
- Import failed: "'{file}' could not be imported: {error}."

## 9. Parity coverage

| ID | Feature | Where |
| --- | --- | --- |
| UTMT-GEN-08 | Language (LANG): unknown value, language count, entry count | 3.1 (read-only fields kept) and 3.2, 3.3 (entries, which UTMT lacks) |
| UTMT-AST-08 | Shared string edits | 2 |
| UTMT-CTRL-02 | String reference field | 2, 4 |
