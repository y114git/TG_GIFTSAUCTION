# Code intelligence parity (2026-11)

Goal: the GML editor of Studio has the code features of GameMaker's code editor and UndertaleModTool (UTMT), and more. State is measured against the code of this branch. Done means it works in the editor and has a headless test. Logic lives in pure classes (`GmlDoc`, `GmlStructure`, `GmlSymbols`, `GmlLineEdits`, `GmlSignatures`, `GmlColors`, `LineDiff`, `CodeLoader`) so it is tested without a window.

Speed rule: one `GmlDoc` (tokens, bracket pairs, name index) is built per text version and shared. Typing only re-tokenizes after a 300 ms pause; hover waits 350 ms of a still pointer; bracket matching is a dictionary read. `GmlIntelTests.AnalysisOfAFiveThousandLineEntryIsFastAndLookupsAreInstant` builds a 10,000 line text (parse, folds, outline under 2 s once) and checks every lookup stays under 50 ms.

## GameMaker features

| Feature | State | Where |
| --- | --- | --- |
| Hover: function signature | done: own functions show real parameter names (defaults optional), built-in ones show `argument0` style names from the compiler list; no descriptions (the library has no documentation text) | `CodeEditorTab.Intel.cs`, `GmlSignatures`, `GmlNames.SignatureOf` |
| Hover: assets, variables, locals | done: kind and name, locals show their declaration line, sprites and other resources show a picture when the Explorer can make one | `HoverTextAt`, `ThumbnailLoader` |
| Hover: macro and enum | done: the `#macro` line or the enum head | `HoverText` |
| Hover: colour literals | done | `GmlColors` |
| Hover: compile problems | done: the compiler message at the squiggle | `HoverTextAt` |
| Signature help while typing | done: popup under the caret on `(` and `,`, Ctrl+Shift+Space, active parameter in bold | `UpdateSignatureHint`, `GmlSignatures` |
| Go to definition F12, Ctrl+click | done: locals, parameters, functions of the same text, scripts and resources | `GoToDefinition` |
| Peek definition Alt+F12 | done: a read only popup with the declaration or the first 40 lines of the script | `PeekAsync` |
| Find usages Shift+F12 | done: a resource name lists the resource's usages, any other name lists the places it is used in all code, with the line | `ShowNameUsagesAsync`, `CodeScan` |
| Rename symbol (F2) | done: a local or parameter in this text, a resource through the resource rename, any other name in all code and in strings that equal it, with a preview; collisions warn and can be ignored | `RenameSymbol`, `CodeRename`, `CodeRenameDialog` |
| Context menu | done: Go to Definition, Peek, Find Usages, Rename, Quick Fix, Copy Name, Toggle Comment, Cut, Copy, Paste | `BuildMenu` |
| Breadcrumb, outline, function list | done: breadcrumb of the caret and an Outline list of functions, enums, macros and regions; Ctrl+Shift+O | `GmlStructure`, `ShowOutline` |
| Code folding | done: braces, regions, block comments | `GmlStructure.Foldings` |
| Bracket matching and highlight | done | `HighlightRenderer` |
| Matching word highlight | done | `HighlightRenderer` |
| Multi-cursor | not possible: AvaloniaEdit has no multiple carets | |
| Duplicate, move line | done (Shift+Alt+Down, Alt+Up, Alt+Down) | `GmlLineEdits` |
| Toggle line and block comment | done (Ctrl+/, Shift+Alt+A) | `GmlLineEdits` |
| Snippets | done: 12 templates, Ctrl+J, indentation follows the line | `GmlSnippets` |
| Colour preview | done: a swatch before hex literals, `#RRGGBB` and `c_` names | `ColorSwatchGenerator` |
| Inline errors, squiggles, Problems | done for compile errors of Apply (squiggle, hover, Problems panel, F8 and Shift+F8); no background check while typing | `SquiggleRenderer` |
| Quick fixes Ctrl+. | done: change a misspelt name to a known one, declare as local or global, comment out a line with a problem | `GmlQuickFixes` |
| Find and replace | done (editor panel and the Search panel across all code) | `CodeSearch` |
| Bookmarks | done: gutter dot, Ctrl+F2, Alt+F2, Ctrl+Shift+F2; they follow edits; not kept after the tab closes | `BookmarkMargin` |
| Minimap | not possible: AvaloniaEdit has none | |
| Diff against original | done: Show Changes Against Original lists added and removed lines against the code when the tab opened | `LineDiff`, `CodeEditorTab.Diff.cs` |
| Indentation guides | done, option in Settings | `IndentGuideRenderer` |
| Word wrap | done, option in Settings | `EditorOptions` |
| Auto close brackets and quotes, step over | done | `GmlLineEdits` |
| Font ligatures | not done | |
| Format on paste | not done | |
| Linked editing | not done | |
| Completion | done (names, 2 letters) | `GmlCompletion` |

## UTMT extras

| Feature | State | Where |
| --- | --- | --- |
| Assembly view | done: separate window, editable, Apply assembles | `DisassemblyWindow` |
| Assembly when the decompiler fails | done: banner, Retry, assembly beside, parent script of a 2.3 child entry | `CodeLoader` |
| Decompiler settings | done | settings page |
| Jump from assembly line to source | not done | |
| Show code references of an asset | done | `UsageSearch` |
| Number to asset name | done | `NumberAssets` |

## Commands

Every code command is in the command registry (menu Code, command palette, Settings > Keyboard Shortcuts, rebindable). Defaults: F12 go to definition, Alt+F12 peek, Ctrl+Shift+Space parameter hints, Ctrl+. quick fix, Ctrl+J snippet, Ctrl+/ line comment, Shift+Alt+A block comment, Shift+Alt+Down duplicate line, Alt+Up and Alt+Down move line, Ctrl+F2 bookmark, Alt+F2 next bookmark, Ctrl+Shift+F2 previous bookmark, F8 and Shift+F8 problems, Ctrl+Shift+O outline. F2 renames the name under the caret when the focus is in a code editor, Shift+F12 is Find Usages.

## Not done and why

Multi-cursor and minimap need features AvaloniaEdit does not have. Font ligatures, format on paste, linked editing and the jump from an assembly line to the source are left. The squiggles show only the errors of the last Apply; a background syntax check while typing needs a compile entry point that does not touch the model.
