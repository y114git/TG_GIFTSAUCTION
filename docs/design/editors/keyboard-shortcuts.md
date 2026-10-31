# Keyboard shortcuts editor

Status: proposed. The editor for key bindings, keymap presets and `keybindings.json` (F-UI-5). Other settings are in [../settings.md](../settings.md). The full default map is generated in [../shell.md](../shell.md#16-keyboard-map). Shared rules: [../ux-standards.md](../ux-standards.md).

## 1. Purpose and entry points

See and change every shortcut, find conflicts, and switch to a keymap people already know. Opens as a tab with Ctrl+K Ctrl+S (Cmd+K Cmd+S), File > Keyboard Shortcuts, the palette (`>Keyboard Shortcuts`), a context menu "Change Keybinding..." on any menu item, and the Settings page link.

## 2. Layout

```
+--------------------------------------------------------------------------------------+
| Keyboard Shortcuts   [search commands or keys... ] [Record Keys]   Preset [Studio v]  |
+--------------------------------------------------------------------------------------+
| Command                         | Keybinding            | When             | Source   |
| Save                            | Ctrl+S                | -                | Default  |
| Go to Resource...               | Ctrl+P                | -                | Default  |
| Apply (Compile or Assemble)     | F7                    | code editor      | Default  |
| Add Next Occurrence             | Ctrl+D                | code editor      | Default  |
+--------------------------------------------------------------------------------------+
| 0 conflicts   [Show Conflicts]      [Import...] [Export...] [Open keybindings.json]    |
+--------------------------------------------------------------------------------------+
```

A table of every command in the registry, virtualized: Command (localized title; id in a tooltip), Keybinding (keys drawn with platform names, or None), When (the context in plain words), Source (Default, Preset, User, Plugin). Sortable by any column; when sorted by command, category headers group the rows. A filter row offers Modified only, Conflicts only, No binding.

## 3. Searching and recording

1. The search box matches command title, id and the key text. "Record Keys" turns the box into a key recorder: press a combination and the table filters to commands bound to it. Escape stops recording. A chord shows its first key and waits 2 seconds.
2. Recording is cancelled by IME composition; modifier-only presses are ignored.

## 4. Editing a binding

1. Enter on a row (or double-click the Keybinding cell) opens an inline recorder. Press the new keys; Enter accepts, Escape cancels. Backspace clears the binding; Delete resets the row to its default.
2. "Add Another Binding" gives a command a second key.
3. When: free text from a short list of context keys (`editorFocus`, `textInputFocus`, `editorType == room`, `explorerFocus`, `canvasFocus`, `projectMode`, `gameRunning`); the editor offers them as suggestions and rejects unknown keys with "Unknown context '{key}'.".
4. Conflicts: if the key is already bound in an overlapping context a warning icon and text show the other command: "Ctrl+D is also bound to Add Next Occurrence when the code editor has focus." Choices: Replace (removes the other binding), Keep Both (allowed only when the contexts cannot be active together), Cancel. The Show Conflicts filter lists all clashes.
5. Chord prefixes are checked: a key that starts a chord cannot also be a single-key binding in the same context.
6. Keys that act only on an unfocused text field (single letters, digits) are accepted only in contexts that exclude text input; the table says "Not while typing" in the When column.
7. Undo of binding changes: each change is one step of the editor (Ctrl+Z inside the table), and "Reset All..." confirms.

## 5. Presets

`keybindings.preset`: Studio (default map), VS Code, GameMaker, UTMT. A preset only rebinds commands; every command exists in all presets.

| Preset | Source |
| --- | --- |
| Studio | The map in shell.md section 16 |
| VS Code | Common VS Code defaults for the commands G3MStudio shares (palette, quick open, find, tabs, terminals not applicable) |
| GameMaker | GameMaker's hotkey files mapped by command name where a command exists (bookmarks Ctrl+1 to 9, Go To Ctrl+T, Ctrl+Alt+Arrow card focus, room and sprite editor keys), with macOS overrides |
| UTMT | The UTMT keys: Ctrl+K compile or assemble, Shift+F5 debugger launch, Ctrl+Q close, and the room and tile editor keys of UTMT-KEY-05 and UTMT-KEY-06 |

Choosing a preset shows "This changes {n} bindings" with a list and Apply or Cancel. User overrides stay on top of the preset. Presets can be exported as the same JSON file.

## 6. Import, export and the file

`keybindings.json` (user folder) is an array of `{ "key", "command", "when" }`; a command with a leading minus (`"-save"`) removes a binding. The file is watched: edits apply live, invalid entries are reported ("keybindings.json line {l}: unknown command '{id}'") and the last good set stays active. Import and Export use the same format. Workspace scope does not apply to shortcuts.

## 7. Actions and shortcuts

Table keys: Enter edits, Backspace clears, Delete resets, Ctrl+F focuses the search, Tab moves between the search box and the table.

| Command | Id | Windows and Linux | macOS |
| --- | --- | --- | --- |
| Open settings.json | `settings.openJson` | None | None |
| Reset Setting | `settings.reset` | None | None |
| Record Key | `settings.recordKey` | Enter | Enter |
| Clear Key Binding | `settings.clearKey` | Delete | Delete |
| Reset All Settings... | `settings.resetAll` | None | None |
| Focus Settings Search | `settings.focusSearch` | Ctrl+F | Cmd+F |
| Switch Scope (User, Workspace) | `settings.switchScope` | None | None |

## 8. Validation and messages

| Case | Message |
| --- | --- |
| Unknown command in file | "keybindings.json line {l}: unknown command '{id}'." |
| Invalid key text | "'{text}' is not a valid key combination." |
| Reserved by the system | "'{keys}' is used by {platform} and cannot be assigned." (for example Alt+F4, Cmd+Q cannot be removed) |
| Unbinding a command with no other key | Info "'{command}' now has no shortcut. It stays in the palette and menus." |

## 9. Undo

See section 4 item 7.

## 10. Empty and loading states

- Search with no match: "No commands match '{query}'."
- Conflicts filter with none: "No conflicts."

## 11. Parity coverage

- UTMT-KEY-01 to UTMT-KEY-06: section 5 (UTMT preset) and the default map in shell.md section 16.
