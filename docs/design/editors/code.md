# Code editor and code search

Status: proposed. Covers the editor for CODE entries (decompiled GML and disassembly), compile and assemble errors, the decompiler and compiler settings, go to definition, find usages, find unreferenced resources, and search and replace across all code. Built on AvaloniaEdit. Shared rules: [../ux-standards.md](../ux-standards.md). Shell: [../shell.md](../shell.md).

## 1. Purpose and entry points

Edit game logic as GML. The editor shows the decompiled source of a code entry, lets the user edit it, and replaces the bytecode when the text is applied. A second view shows the VM assembly for exact edits.

Opens from: Explorer > Data > Code, an object's event, a room's creation code, a script, a timeline moment, search results, Go To (`code:` prefix, `@`), find usages results, and links in Problems. Every code-bearing owner (object event, room, script, moment) opens the same editor on the owned entry, with a breadcrumb "obj_player > Create".

Games compiled with the YoYo Compiler (YYC) have no code in the file. The editor shows the state "This game was built with YYC. Its code is inside the executable and cannot be shown or edited." and the Search panel refuses code searches with the same sentence (UTMT-FILE-06). Bytecode versions below 13 or above 17 open the editor read-only with "Code editing is off for bytecode {n}. Saving the data file is still possible." (UTMT-FILE-05).

## 2. Layout

```
+----------------------------------------------------------------------------------------+
| gml_Object_obj_player_Create_0   [obj_player v] [Create v] [ ~ locals v]    Apply  F7   |  nav bar 28
+----------------------------------------------------------------------------------------+
| [Decompiled] [Disassembly] [Locals]            Name: gml_Object_obj_player_Create_0    |  view tabs 28
+--+-------------------------------------------------------------------------------+-----+
|1 | hp = 100;                                                                     | ... |  minimap
|2 | global.lives = 3;                                                              |     |
|3 | scr_init(hp);                                                                  |     |
|~ |                                                                                |     |
+--+-------------------------------------------------------------------------------+-----+
| Compile: OK   Unapplied: no    Ln 3, Col 14   GML   UTF-8   Zoom 100%                  |  editor status
+----------------------------------------------------------------------------------------+
```

The tab bar of the app already shows the resource; the nav bar adds three drop-downs for GameMaker users: owner (object or room or script), document (event, moment, creation code; inherited events greyed), and declaration (functions, locals, constructor members). Alt+1 opens the owner list, Alt+2 the document list, Alt+3 the declaration list (listed under Go in the palette; no default global shortcut to avoid Alt-letter rules, these three use Alt+digits only inside the code editor).

Gutter, left to right: bookmarks and error marks (16 px), line numbers (auto width), fold arrows (12 px). Right edge: a minimap (small, medium, large, or bar-only; setting `code.minimap`) that also shows diagnostics marks and the current search matches. Sticky headers show the enclosing function name when scrolled. Surface is `Bg.Sunken`, opaque even with the background image on.

Document view: for an object, "Show all events in one scroll view" (default off, setting `code.allEventsView`) stacks every event of the object as sticky-header sections, as GameMaker's Code Editor 2 does. The Object editor's "Open All Events in Tabs" opens one tab per event instead.

## 3. Views

### 3.1 Decompiled (UTMT-CODE-02)

1. The text is produced by the decompiler with the current settings (section 8). Decompile runs on a background job; for entries under 100 ms the text appears with no loading state, otherwise a skeleton line pattern and "Decompiling..." in the editor status.
2. A decompile failure puts a comment block at the top: `/* Could not decompile this code: {reason} */`, adds an entry in Problems (`G3MS-DECOMP-001`), and switches the editor to the Disassembly view with an info bar "Showing disassembly because decompiling failed. [Retry] [Copy Details]".
3. The code is editable unless the file is read-only. Editing sets the state "Unapplied" (status bar and tab italic dot). Nothing changes in the data file until Apply.
4. Apply (F7, command `code.apply`) compiles. Automatic apply happens on focus loss, on switching tab, and on save (setting `code.autoApply`: `onBlur` default, `onIdle`, `off`). The old behaviour of UTMT (apply on focus loss or Ctrl+K) is the default. Apply is one undo step "Edit {code name}".
5. Compile errors are listed in Problems with severity, message, owner path and `line:column`, and drawn as red squiggles with a tooltip. The bytecode is not replaced when there are errors; the editor stays "Unapplied" and the nav bar shows "{n} errors".

### 3.2 Disassembly (UTMT-CODE-04, UTMT-CODE-05)

VM assembly with the syntax highlighting of the assembler. Editing and Apply assemble instead of compile; errors use the same Problems entries (`G3MS-ASM-001`). A banner above the text warns once per session: "Edited assembly bypasses checks. Incorrect instructions can crash the game." with "Understood". The Locals tab (3.3) is next to it.

### 3.3 Locals

The code locals table of the entry (UTMT-AST-12 is the full editor; this tab is a view of the same data): index, name, with "Add Local" row. See [variables-functions.md](variables-functions.md).

### 3.4 Properties strip (UTMT-CODE-01)

Above the text, collapsed by default (toggle "Properties"): Name (read-only here, rename in the Explorer), Locals count, Arguments count, Bytecode offset (hex, copy), Parent entry (Reference field, for GMS 2.3 child functions), Length in instructions. Edit rules follow [variables-functions.md](variables-functions.md).

## 4. Editing features

| Feature | Behaviour |
| --- | --- |
| Syntax highlighting (UTMT-CODE-06) | GML and VM assembly definitions; themes from G3M-style tokens; 4 scopes of rainbow brackets optional |
| Semantic colouring (UTMT-CODE-07) | Functions bold in `Warning`, globals, constants, instance variables and locals each in a distinct token colour; colours meet 4.5:1 on `Bg.Sunken`; kinds also differ by style (bold, italic, underline) so colour is not the only cue |
| Completion | Opens after 2 typed characters with a 150 ms delay (setting). Candidates: functions of the GameMaker version of the file, script and resource names, locals, globals, instance variables. Initials matching (`sdm` finds `show_debug_message`). Enter accepts and adds parentheses. Tooltip shows signature and return type |
| Hover | Resource names show a thumbnail (sprite) or details; numbers that match a resource index show the name |
| Go to definition (UTMT-CODE-08) | F12 or Ctrl+click on a script, function, resource or GMS 2.3 sub-function name; opens in the current tab. Ctrl+middle-click or Ctrl+Alt+click opens a new tab. Peek: Alt+F12 shows an inline preview |
| Number resolution (UTMT-CODE-09) | Right-click a number > "Show Resources With This Number": a menu lists resources whose index equals the number and constants with that value; choose one to open it |
| Find and replace (UTMT-CODE-10) | Ctrl+F opens the in-editor find widget (Match Case, Whole Word, Regular Expression, previous, next, count). Ctrl+H adds the replace row. Works in both views. "No results" in `Text.Muted` |
| Zoom (UTMT-CODE-11) | Ctrl+wheel and Ctrl+= / Ctrl+- / Ctrl+0 zoom the editor text; the size carries to the next code editor (`code.zoom` setting) |
| Folding | Functions, braced blocks, switch and case, multi-line comments; Ctrl+Shift+[ and Ctrl+Shift+]; Ctrl+K Ctrl+0 and Ctrl+K Ctrl+J for all |
| Multi-cursor | Ctrl+Alt+Up and Down add cursors; Ctrl+D selects the next occurrence; Ctrl+Shift+L selects all; Shift+Alt+drag for column selection |
| Rename symbol | F2 renames a local, argument or global in this entry; for resources it calls the rename service with a reference preview |
| Indentation guides, whitespace, brace matching | On by default, in settings |
| Text comments for Undertale and Deltarune (UTMT-CODE-12) | Command `code.textComments` toggles resolved English text as comments after `scr_gettext` and `scr_84_get_lang_string` calls, read from `gml_Script_textdata_en` or `lang/lang_en.json`. Shown dimmed, never written to the data file |
| Modern GML (UTMT-CODE-15) | Template strings, nullish compound assignment, `variable_get_hash()` folding; the built-in function list is filtered by the GameMaker version |
| Game-specific definitions (UTMT-CODE-16) | JSON definitions for generic GameMaker, Undertale and Deltarune resolve macros, enums and asset types (including the AudioGroup macro type) in the decompiled text. Detection matches by game name regex. A status chip "Definitions: Undertale" shows which set is active; click to change or turn off |
| Position memory (UTMT-CODE-13, UTMT-TAB-08) | Caret, scroll, zoom and active view restore per tab; opening from a search result jumps to the line and flashes it for 800 ms (Reduce motion: no flash, a static highlight) |

Undo inside the text uses the text editor's own history until Apply. Apply adds one workspace undo step. See section 10.

## 5. Actions and shortcuts

| Command | Id | Windows and Linux | macOS |
| --- | --- | --- | --- |
| Apply (Compile or Assemble) | `code.apply` | F7 | F7 |
| Switch Decompiled and Disassembly | `code.toggleView` | F4 | F4 |
| Show Disassembly Beside Decompiled | `code.sideBySide` | None | None |
| Rename Symbol | `code.rename` | F2 | F2 |
| Quick Fix | `code.quickFix` | Ctrl+. | Cmd+. |
| Trigger Suggestions | `code.suggest` | Ctrl+Space | Control+Space |
| Parameter Hints | `code.parameterHints` | Ctrl+Shift+Space | Cmd+Shift+Space |
| Fold | `code.fold` | Ctrl+Shift+[ | Cmd+Option+[ |
| Unfold | `code.unfold` | Ctrl+Shift+] | Cmd+Option+] |
| Fold All | `code.foldAll` | Ctrl+K Ctrl+0 | Cmd+K Cmd+0 |
| Unfold All | `code.unfoldAll` | Ctrl+K Ctrl+J | Cmd+K Cmd+J |
| Toggle Line Comment | `code.toggleComment` | Ctrl+/ | Cmd+/ |
| Toggle Block Comment | `code.blockComment` | Shift+Alt+A | Shift+Option+A |
| Duplicate Line | `code.duplicateLine` | Shift+Alt+Down | Shift+Option+Down |
| Move Line Up | `code.moveLineUp` | Alt+Up | Option+Up |
| Move Line Down | `code.moveLineDown` | Alt+Down | Option+Down |
| Join Lines | `code.joinLines` | None | None |
| Add Next Occurrence | `code.selectNext` | Ctrl+D | Cmd+D |
| Select All Occurrences | `code.selectAllOccurrences` | Ctrl+Shift+L | Cmd+Shift+L |
| Add Cursor Above | `code.cursorAbove` | Ctrl+Alt+Up | Cmd+Option+Up |
| Add Cursor Below | `code.cursorBelow` | Ctrl+Alt+Down | Cmd+Option+Down |
| Go to Matching Brace | `code.matchBrace` | Ctrl+Shift+\ | Cmd+Shift+\ |
| Zoom Editor In | `code.zoomIn` | Ctrl+= | Cmd+= |
| Zoom Editor Out | `code.zoomOut` | Ctrl+- | Cmd+- |
| Reset Editor Zoom | `code.zoomReset` | Ctrl+0 | Cmd+0 |
| Show Resources With This Number | `code.showAssetsForNumber` | None | None |
| Show Game Text as Comments | `code.textComments` | None | None |
| Export Code to File... | `code.exportCode` | None | None |
| Import Code From File... | `code.importCode` | None | None |
| Revert to Original Code | `code.revertCode` | None | None |
| Open Definition in New Tab | `code.openInNewTab` | None | None |

Context menu: Go to Definition, Peek Definition, Find Usages, Show Resources With This Number (on a number), Rename Symbol, Quick Fix, Cut, Copy, Paste, Insert (UUID, MD5, SHA1, SHA256, Base64 encode and decode), Convert Case, Fold, Copy as Disassembly.

## 6. Compile errors and diagnostics

1. Syntax checking runs 2000 ms after typing stops (`code.syntaxDelay`, GameMaker default) on a background job and updates squiggles and Problems. Squiggles: red for errors, `Warning` for warnings, `Info` for suggestions; each has an icon in the gutter, not colour only.
2. Problems rows read: `Error  GM1010  Unknown function 'foo'  obj_player > Create  3:5`. The code is the compiler's id; the full text is in the tooltip.
3. Each error has a "Show in Code" action and, when the compiler offers one, a Fix action (for example "Declare '{name}' as a global").
4. Messages follow the error pattern of ux-standards section 8: what happened, why, what to do next. Example: "Could not apply the code. 'foo' is not a function or script in this game. Check the spelling or create the script, then apply again."
5. F8 and Shift+F8 step through problems in the active code entry.
6. Unused or undeclared variable warnings never block applying.
7. If Apply is requested with errors, the status bar reads "Not applied: {n} errors" and the tab keeps the Unapplied dot. Saving the data file with unapplied code asks once: "Save without applying '{name}'? Its changes will stay in the editor but not in the file. [Apply and Save] [Save Without Applying] [Cancel]".

## 7. Import and export of code

`code.exportCode`: saves the active entry as a `.gml` file (UTF-8, LF). `code.importCode`: loads a `.gml` file into the entry (replaces the text, sets Unapplied). Bulk: Explorer > Code > right-click > "Export Selected Code..." and "Import Code From Folder..." (UTMT-CODE-17): choose a folder of `.gml` files; a preview table lists each file, its target entry and the action (Replace, Create, Skip); missing resources can be created ("Create missing resources"); collision event names and cross references are resolved; one undo step for the whole import; results go to Output > Import.

## 8. Compiler and decompiler settings (UTMT-SET-16, UTMT-GMLS-01 to 08)

Tools > "Compiler and Decompiler Settings..." opens a settings page (a tab, not a modal) with a live preview pane showing a sample entry.

| Group | Setting | Control |
| --- | --- | --- |
| Formatting | Indentation style | Combo: Tab, 2 spaces, 4 spaces, 8 spaces (UTMT-GMLS-01) |
| | Always use semicolons; CSS hex colour literals; print warnings; enum and macro declarations at top; open block braces on same line; remove braces of single-line blocks | Six checkboxes (UTMT-GMLS-02) |
| Empty lines | After local declarations; around enums; around branch statements; before and after switch cases; around function declarations; around static initialization | Six checkboxes (UTMT-GMLS-03) |
| Clean-up passes | Try/catch/finally rewriting; else into continue; default argument values; builtin array variable syntax; scoped local declarations; template strings | Six checkboxes (UTMT-GMLS-04) |
| Enums | Create enum declarations; unknown enum name pattern; unknown enum value pattern | Checkbox, two text fields with `{index}` placeholder help (UTMT-GMLS-05) |
| Arguments | Unknown argument naming pattern | Text with `{index}` (UTMT-GMLS-06) |
| Stack | Allow leftover VM stack data | Checkbox with warning text (UTMT-GMLS-07) |
| Rooms | Named instance ID prefix | Text, shared by compiler and decompiler (UTMT-GMLS-08) |

Buttons: Reset to Defaults (confirm), Apply to Open Editors. Changing a setting re-decompiles open editors that have no unapplied edits; editors with unapplied edits keep their text and show an info bar "Decompiler settings changed. [Re-decompile]".

## 9. Search and replace across code

The Search panel (left dock, Ctrl+Shift+F) and the Search Results list (bottom panel) share one component with Problems and References.

### 9.1 Search panel

```
+----------------------------------------+
| Search                                  |
| [ find text...            ] [Aa][Ab|][.*]|
| [ replace...              ]  [Replace All]|
| v Scope: Code  Strings  Names  Rooms     |
| Files to include: [ code name filter ]   |
| [ ] Search disassembly  [ ] Multiline    |
| 3 results in 2 entries         [Stop]    |
+----------------------------------------+
```

Fields and options (UTMT-SRCH-01):

| Control | Rules |
| --- | --- |
| Find text | History of the last 10 queries (Alt+Up and Alt+Down) |
| Match Case, Whole Word, Regular Expression | Toggles with Alt+C, Alt+W, Alt+R |
| Multiline regular expression | Alt+M; patterns may span lines |
| Search disassembly | Alt+A; searches the assembly text instead of GML |
| Scope | Checklist: Code, Strings, Names (resource names), Room creation code (part of Code), Shaders, Disassembly; default Code |
| Code name filter | Text with its own Match Case and Regular Expression toggles; limits which entries are searched |
| Replace | Text and "Replace All"; the replace row is hidden until Ctrl+Shift+H or the chevron |
| Ignore comments | Only applies to text typed in G3MStudio (comments do not survive decompiling) |

The search runs in parallel over all entries on a background job against the decompile cache (warm: under 1 s; cold: under 10 s in the background with a progress count "Searching... {done} of {total}"). A second search while one runs asks to replace it (UTMT-SRCH-02). Stop (Escape) cancels. When project sources exist, the project's GML files are searched instead of the decompiled text.

### 9.2 Results (UTMT-SRCH-02, UTMT-APP-12)

Grouped by entry: `gml_Object_obj_player_Create_0 (2)` with children `Line 3: scr_init(hp);` and the match highlighted. Keyboard: Up and Down move, Enter opens at the line, Ctrl+Enter opens in a new tab, F4 and Shift+F4 step through all results, Left and Right collapse and expand a group. Context menu: Open, Open in New Tab, Copy, Copy All, Export Results..., Dismiss Result. The summary line reads "{n} results in {m} entries" and is a live region.

Clicking a result opens a preview tab (italic); the match is selected in the editor.

### 9.3 Replace

1. "Replace All" first opens a preview: a checkbox list of every change with before and after lines, grouped by entry, with Select All and Deselect All, and a total "{n} changes in {m} entries".
2. Apply changes the text of each entry and compiles each one. Entries that fail to compile are skipped and reported in Problems; the dialog stays open with those entries marked "Failed" so the user can fix the pattern. Nothing is half-applied: the whole replace is one undo step, and if any selected entry fails the user chooses "Apply the rest" or "Cancel All" before anything is written.
3. Replace in strings and names edits the string table (with the shared edit prompt of strings.md) and is previewed the same way.
4. Single replace: the row's Replace button replaces only that result.

## 10. Find usages, references and unreferenced resources

### 10.1 Find usages (UTMT-SRCH-03, UTMT-SRCH-05, UTMT-SRCH-07)

`go.references` (Shift+F12) works from the Explorer (any resource), reference fields, the code editor (symbol at the caret), the tile set editor (a tile id) and texture page items. A small dialog "Find Usages of '{name}'" lists reference types to scan with Select All and Deselect All and remembers the selection: sprites, tile sets and backgrounds, fonts (including texture groups), sounds, audio groups, paths, scripts, code, shaders, timelines, objects (parent, sprite, mask, events), rooms (instances, tiles, layers, sprite, sequence and particle instances, backgrounds, effect properties, creation code), variables, functions, code locals, strings, texture page items, embedded textures, embedded images, embedded audio, texture groups, particle systems and emitters, sequences (tracks, keyframes, moments, broadcast messages), option constants. The list adapts to the GameMaker version of the file.

Results open in the References panel as a tree: reference type > owner > location, with a name filter box, Open, Open in New Tab, Copy Name, nested "Find Usages" on any result, and "Export Results..." (default name `references_of_{name}.txt`). The panel header shows "{n} usages of '{name}'".

### 10.2 Show child objects (UTMT-SRCH-06)

The Object editor button "Show Child Objects" fills References with objects whose parent is the current object.

### 10.3 Tile references (UTMT-SRCH-07)

In the tile set editor, "Find Rooms Using This Tile" lists rooms and layers that use the selected tile id.

### 10.4 Find unreferenced resources (UTMT-SRCH-04)

`resource.findUnreferenced` opens the same type picker, then lists unreferenced resources grouped by type in References. "Export Results..." writes `unreferenced_assets.txt`. Each row has "Delete" (after the reference scan) and "Find Usages". A note says "A resource can be used by name at run time without being found here." with a link to the help topic.

## 11. Empty and loading states

- No code entries: "This data file has no code." (for example, an audio group file).
- Entry selected but still decompiling: the document area shows 12 skeleton lines and the status "Decompiling...".
- YYC and unsupported bytecode: section 1.
- Search with no matches: "No results for '{query}' in {scope}. Try turning off Match Case or Whole Word."
- Search not yet run: "Type to search. Press Enter to run."

## 12. Undo

Text edits before Apply: editor-local history, limit 200 steps per document (setting). Apply, Replace All, Import Code, and revert are workspace steps named "Edit {name}", "Replace '{find}' in {n} entries", "Import {n} code files". Undo of Apply restores the previous bytecode exactly. Undo is available per editor and globally (Edit > Undo History).

## 13. Parity coverage

- UTMT-CODE-01: section 3.4.
- UTMT-CODE-02, UTMT-CODE-03: sections 3.1 and 6.
- UTMT-CODE-04, UTMT-CODE-05: section 3.2.
- UTMT-CODE-06 to UTMT-CODE-13: section 4.
- UTMT-CODE-15, UTMT-CODE-16: section 4.
- UTMT-CODE-17: section 7.
- UTMT-GMLS-01 to UTMT-GMLS-08, UTMT-SET-16: section 8.
- UTMT-SRCH-01, UTMT-SRCH-02, UTMT-APP-12: section 9.
- UTMT-SRCH-03 to UTMT-SRCH-07: section 10.
- UTMT-FILE-05, UTMT-FILE-06: section 1.
- UTMT-KEY-04: section 5.
- UTMT-API-08: section 7 (text of code by name is available to scripts through the same decompile service).
