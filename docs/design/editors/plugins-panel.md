# Plugins panel

Status: proposed. Install, trust, enable, disable, update, reload and develop plugins (ADR-0009), and the plugin surfaces other parts of the app expose. Shared rules: [../ux-standards.md](../ux-standards.md). Requirements: F-PLG-1 to F-PLG-8.

## 1. Purpose and entry points

Extend G3MStudio with commands, panels, editors, importers, exporters, themes and more, written as a folder of C# sources. Opens as the Plugins tab of the left dock (Ctrl+Shift+X), from Tools > Plugins, and from `file.new.plugin`.

## 2. Layout

```
+------------------------------------+
| Plugins   [Install v] [Reload All] |
| [search installed...]              |
+------------------------------------+
| v Installed (4)                    |
|  [icon] Save Manager     v1.2  [on]|
|         Edit DELTARUNE saves        |
|  [icon] Pixel Tools      v0.4  [on]|
|         ! Failed to activate        |
| v Disabled (1)                      |
| v Updates (1)                       |
+------------------------------------+
| Details of the selected plugin      |
+------------------------------------+
```

Rows: icon, name, version, one-line description, an enable switch (a labelled toggle, not colour only) and a state line (Active, Disabled, Failed to activate, Update available). Details pane (tab in the editor area when opened with Enter): id, author, version, minimum Studio version, activation events, declared permissions, homepage, README, Output channel link, settings link, commands it adds, and buttons Reload, Disable or Enable, Update, Uninstall, Open Folder.

## 3. Install and trust (F-PLG-5)

`plugins.install` offers: From Folder..., From ZIP..., From GitHub URL... (a GitHub repository or release URL; downloads over HTTPS to a temp folder). Drag a folder or ZIP onto the panel does the same.

Trust prompt, shown once per plugin and again when an update changes its permissions list:

```
Install 'Save Manager' v1.2?
Source   https://github.com/example/save-manager
Author   Example Author
Permissions this plugin says it needs: files, network
Plugins have full access to your computer, like scripts. Only install plugins you trust.
[Install and Enable]  [Install Disabled]  [Cancel]
```

Permissions are information, not a sandbox (ADR-0009). The prompt names the files the plugin will run. There is no marketplace in the beta (F-PLG-8 is P2).

## 4. Manage (F-PLG-4)

1. Enable, disable, update and reload happen without restarting: each plugin is loaded in its own collectible load context. Reload keeps open editors of that plugin and re-activates them.
2. A plugin failing on activation is disabled with a notification "'{name}' failed to start and was turned off. [Show Details]". Errors inside a plugin are caught at the boundary, written to its Output channel (Output > Plugins > {name}) and never crash the app.
3. Activation (`startup`, `onCommand:<id>`, `onResource:<type>`, `onFile:<pattern>`) is shown in Details; most plugins load on demand, not at startup.
4. Updates: "Check for Updates" per plugin when the manifest has a source; Update shows release notes and the trust prompt again if permissions changed.
5. Plugin settings pages appear in Settings under Plugins > {name} (F-PLG-7 translations: plugins may ship `lang/<code>.json`).

## 5. Develop (F-PLG-6)

`file.new.plugin` creates a ready-to-edit folder from a template (manifest, entry file with `Activate` and `Deactivate`, an example command, assets folder). The Plugins panel "Developer" section has Watch for Changes (hot reload on save, compile errors in Problems with source "Plugin") and Open in Editor (the code editor, plain text editing of `.cs` and `.csx` files). The plugin manifest `plugin.json` has fields id, name, version, authors, description, entry, references, minStudioVersion, activation, permissions, homepage, icon.

## 6. What plugins can add (F-PLG-2, F-PLG-3)

Commands, menus and toolbars, dock panels, editor tabs, resource editors, inspectors, explorer actions, importers, exporters, file types, settings pages, status bar items, jobs, game launch hooks and themes. All of them use the same registry and surfaces as built-in features, so a plugin command appears in the palette and the shortcut editor with the plugin name as its category; a plugin panel appears in the View menu; a plugin editor gets the shared editor frame, tab behaviour, undo and Inspector. Plugins edit the data file only through the undo-aware API.

Plugin UI follows the same standards: the host checks that plugin icons are single-colour vector shapes bound to the text colour, and plugin AXAML uses the shared style resources, so 2 px borders and focus rings apply.

## 7. Bundled optional plugins (G3M-34, G3M-35)

Two plugins from G3M are offered in the panel's Recommended section, installed on demand:

1. Save Manager (G3M-34): DELTARUNE save slots per chapter (Chapter 1 to 5 tabs, three main slots), named collections (copy from main, overwrite main, rename, delete), show, edit, erase, import, export, and a save editor with a simple mode. Hooks before and after game launch to back up and restore saves.
2. Custom Save Folders (G3M-35): a separate save folder name per game, profile or mod for modded sessions, applied through the launch hooks.

## 8. Actions and shortcuts

| Command | Id | Windows and Linux | macOS |
| --- | --- | --- | --- |
| Install Plugin... | `plugins.install` | None | None |
| Reload Plugin | `plugins.reload` | None | None |
| Disable Plugin | `plugins.disable` | None | None |
| Open Plugin Folder | `plugins.openFolder` | None | None |

## 9. Validation and messages

| Case | Message |
| --- | --- |
| Manifest missing | "No plugin.json was found in '{folder}'." |
| Invalid manifest field | "plugin.json: '{field}' {reason}." |
| Needs newer Studio | "'{name}' needs G3MStudio {min} or newer. You have {current}." |
| Compile error | Problems row with file, line and message; the plugin stays disabled |
| Duplicate id | "A plugin with the id '{id}' is already installed. Replace it? [Replace] [Cancel]" |

## 10. Undo

Install, enable and disable are not undoable but are reversible from the panel. Plugin edits to the data file are normal undo steps.

## 11. Empty and loading states

- No plugins: "No plugins installed. Plugins add commands, editors and tools. Install one from a folder, a ZIP or a GitHub link, or create your own."
- Loading: rows appear as manifests are read; compile on first load shows "Compiling '{name}'..." and caches by content hash.

## 12. Parity coverage

- G3M-34: section 7, item 1.
- G3M-35: section 7, item 2.
- F-PLG-1 to F-PLG-8 (requirements): sections 2 to 6.
