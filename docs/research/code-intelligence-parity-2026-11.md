# Code intelligence parity (2026-11)

Goal: the GML editor of Studio has the code features of GameMaker's code editor and UndertaleModTool (UTMT), and more. State is measured against the code of this branch. Done means it works in the editor and has a headless test. Logic lives in pure classes (`GmlDoc`, `GmlStructure`, `GmlSymbols`, `GmlLineEdits`, `GmlSignatures`, `GmlColors`, `LineDiff`, `CodeLoader`) so it is tested without a window.

Speed rule: one `GmlDoc` (tokens, bracket pairs, name index) is built per text version and shared. Typing only re-tokenizes after a 300 ms pause; hover waits 350 ms of a still pointer; bracket matching is a dictionary read. `GmlIntelTests.AnalysisOfAFiveThousandLineEntryIsFastAndLookupsAreInstant` builds a 10,000 line text (parse, folds, outline under 2 s once) and checks every lookup stays under 50 ms.

## GameMaker features

| Feature | State | Where |
| --- | --- | --- |
| Hover: function signature | partial: built-in names show `name(argument0, [argument1])` from the compiler list; no descriptions or parameter names (no documentation source in the library) | `CodeEditorTab.Intel.cs`, `GmlNames.SignatureOf` |
| Hover: assets and variables | partial: kind and name (Function, Asset, Variable, Constant); no thumbnail yet | `CodeEditorTab.Intel.cs` |
| Hover: enum and macro values | missing | |
| Hover: colour literals | done: `#RRGGBB $BBGGRR` text for hex and `c_` names | `GmlColors` |
| Signature help while typing | logic done (call and argument index, optional and variadic labels); no popup yet | `GmlSignatures` |
| Go to definition F12, Ctrl+click | done for scripts and resources; not for locals | `CodeEditorTab.cs` `InstallGoTo` |
| Go to definition of a local or argument | logic done (`GmlSymbols.DeclarationOf`); not bound to a key | `GmlSymbols` |
| Peek definition | missing | |
| Find references Shift+F12 | done for resources (context menu and Shift+F12 open the Usages panel); not for functions and variables of the code | `GmlUsages`, Usages panel |
| Rename symbol with preview | logic done, with collision warnings that allow going on; no dialog yet | `GmlSymbols.PlanRename` |
| Rename across all code and strings | missing | |
| Context menu | done: Go to Definition, Find Usages, Copy Name, Toggle Comment, Cut, Copy, Paste | `BuildMenu` |
| Breadcrumb / outline / function list | logic done (functions, enums, macros, regions, breadcrumb of the caret); no panel yet | `GmlStructure.Outline`, `Around` |
| Code folding | done: braces, regions, block comments | `GmlStructure.Foldings` |
| Bracket matching and highlight | done | `BracketRenderer` |
| Matching word highlight | missing | |
| Multi-cursor | missing (AvaloniaEdit has none) | |
| Duplicate, move line | done (Shift+Alt+Down, Alt+Up, Alt+Down), one undo step | `GmlLineEdits` |
| Delete line | missing | |
| Toggle line and block comment | done (Ctrl+/, Shift+Alt+A) | `GmlLineEdits` |
| Snippets and templates | missing | |
| Colour preview swatch | logic done; swatch not drawn | `GmlColors` |
| Ctrl+click jump to resource | done | `InstallGoTo` |
| Inline errors, squiggles, Problems | partial: compile errors go to Problems; no squiggles | `Apply` |
| Quick fixes | missing | |
| Find and replace in a file | done (AvaloniaEdit search panel) | `ShowFind` |
| Find across all code | done (Search panel) | `CodeSearch` |
| Bookmarks | missing | |
| Minimap | missing | |
| Diff against original | logic done (`LineDiff`); no view | `LineDiff` |
| Clickable asset references with thumbnail | partial: Ctrl+click opens; no thumbnail | |
| Local variable and argument hints | partial: locals and parameters are coloured (`GmlScopes.Declarations`) | `GmlScopes` |
| Indentation guides | missing | |
| Word wrap | missing | |
| Font ligatures | missing | |
| Auto close brackets and quotes, step over | done | `GmlLineEdits.ClosingFor`, `StepsOver` |
| Format on paste | missing | |
| Linked editing | missing | |
| Completion | done (names, 2 letters) | `GmlCompletion` |

## UTMT extras

| Feature | State | Where |
| --- | --- | --- |
| Assembly view | done: separate window, editable, Apply assembles | `DisassemblyWindow` |
| Assembly beside the source | partial: button opens the window; no embedded split | `CodeEditorTab.Fallback.cs` |
| Decompiler settings | done | settings page |
| Jump from assembly line to source | missing | |
| Show code references of an asset | done (Find Usages) | `UsageSearch` |
| Decompile failure fallback | done: assembly with a banner, Retry, parent script for 2.3 child entries (read only), message wraps, selectable, Copy Details | `CodeLoader`, `CodeEditorTab.Fallback.cs` |
| Number to asset name | done | `NumberAssets` |

## Not done and why

Multi-cursor and minimap need AvaloniaEdit features it does not have. Hover thumbnails, the rename dialog with preview, signature popup, outline panel, colour swatches, bookmarks, diff view, peek and quick fixes are the next steps; their logic is in place and tested.
