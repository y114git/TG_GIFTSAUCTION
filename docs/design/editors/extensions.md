# Extensions

Status: proposed. The editor for EXTN resources: the extension, its files, functions, constants and options. Shared rules: [../ux-standards.md](../ux-standards.md). Native files that an extension uses are loose files next to the game, not inside the data file.

## 1. Purpose and entry points

View and edit extension metadata and the function table that GML calls. Opens from Explorer > Extensions, Go To `ext:`, and References results.

## 2. Layout

```
+---------------------------------------------------------------------------------+
| ext_discord   Index 0   v1.0.2   3 files   14 functions                          |
+-------------------------+-------------------------------------------------------+
| Extension               | File: discord.dll                                      |
| Name  [ext_discord]     |  Kind [DLL v]  Init [ ]  Cleanup [ ]                  |
| Class [ ]               |  Functions                                            |
| Version [1].[0].[2]     |  Name   External name   ID   Kind  Return  Args       |
| Folder [ ]              |  Init   discord_init    1     DLL   real    string    |
| Product ID [ ]          |  [Add Function]                                       |
| Files                   |  Constants  [Add Constant]                            |
|  > discord.dll          |                                                       |
|    other.gml            | Options                                               |
| [Add File]  Options     |  Name  Value  Kind   [Add Option]                     |
+-------------------------+-------------------------------------------------------+
```

Left: extension fields and the file tree. Right: the selected file's editor; function and constant tables; options.

## 3. Extension fields (UTMT-AST-04)

| Field | Control | Rules |
| --- | --- | --- |
| Name | Name field | |
| Class name | Text | |
| Version | Three number fields | Integers 0 and up |
| Folder name | Text | Relative path, no `..` |
| Product ID | Text (GUID or hex) | |
| Files | Tree (Add File with kind templates: GML, DLL, Java, JavaScript, Objective-C, other) | Template creates a file entry with default init and cleanup names |
| Options | List grid: Name, Value, Kind (Boolean, Real, String, Colour, Choice, Resource) | "Add Option" row; templates by kind |

## 4. File editor (UTMT-AST-05)

| Field | Control | Rules |
| --- | --- | --- |
| File name | Text | A file name only; shown with "Exists next to the game: yes or no" when it can be checked |
| Kind | Combo | |
| Init function name, cleanup function name | Text | Optional |
| Functions | List grid (section 5) | |
| Constants | List grid: Name, Value, "Hidden" | Name identifier pattern |

## 5. Function editor (UTMT-AST-06)

| Field | Control | Rules |
| --- | --- | --- |
| Name (GML name) | Name field | Identifier pattern; unique in the extension |
| External name | Text | The exported symbol |
| ID | Number, integer (read-only unless "Edit IDs" is on) | |
| Kind | Combo: DLL function call types per file kind | |
| Return type | Combo: Real, String | |
| Argument types | Ordered list of Combo (Real, String); up to 17 arguments | Add, remove, reorder with Alt+Up and Alt+Down |

## 6. Actions and shortcuts

| Command | Id | Windows and Linux | macOS |
| --- | --- | --- | --- |
| Add Extension File | `extensions.addFile` | None | None |
| Add Function | `extensions.addFunction` | Insert | Insert |
| Add Constant | `extensions.addConstant` | None | None |
| Add Option | `extensions.addOption` | None | None |

## 7. Validation and messages

| Case | Message |
| --- | --- |
| Duplicate function name | "'{name}' is already a function in this extension." |
| More than 17 arguments | "A function can have at most 17 arguments." |
| Native file missing | Warning "'{file}' was not found next to the game." |
| Invalid version number | "Version parts must be whole numbers." |

## 8. Undo

Each commit, add, remove and reorder is one step.

## 9. Empty and loading states

- No extensions: "This game has no extensions."
- Extension without files: "This extension has no files. Choose Add File."

## 10. Parity coverage

- UTMT-AST-04: sections 2 and 3.
- UTMT-AST-05: section 4.
- UTMT-AST-06: section 5.
- UTMT-TREE-04 (extension defaults on add: folder, class, version, product ID): section 3.
