# 0009 Plugins

Status: accepted (owner, 2026-10-25). Requirements F-PLG-1 to F-PLG-8.

## Goal

Plugins can add any functionality: commands, menus, panels, editor tabs, resource editors, inspectors, explorer actions, importers and exporters, file types, settings pages, status bar items, background jobs, game launch hooks, and themes. Writing one must be as simple as writing a UndertaleModTool script.

## Shape

A plugin is a folder (or a `.zip` of one) with any files in any structure:

```
MyPlugin/
  plugin.json        manifest
  src/Main.cs        entry point named in the manifest (.cs or .csx)
  src/...            more .cs files, optional
  lib/*.dll          optional prebuilt references
  ui/*.axaml         optional Avalonia views
  assets/...         icons, images, data, translations (lang/<code>.json)
```

`plugin.json` fields: `id` (reverse domain), `name`, `version` (SemVer), `authors`, `description`, `entry` (path to the entry file), `references` (NuGet ids or local dlls), `minStudioVersion`, `activation` (`startup`, `onCommand:<id>`, `onResource:<type>`, `onFile:<pattern>`), `permissions` (informational list shown at install: files, network, processes), `homepage`, `icon`.

## Runtime

- G3MStudio compiles the C# sources with Roslyn on first load and caches the assembly by content hash. `.csx` entry files run in the same host as UTMT-style scripts. A prebuilt `.dll` entry is also accepted.
- Each plugin loads in its own collectible `AssemblyLoadContext`, so it can be disabled, updated and reloaded without restarting the app.
- The entry exposes one class implementing `IStudioPlugin` with `Activate(IStudioContext)` and `Deactivate()`.
- `IStudioContext` is the whole public API, the same objects G3MStudio's own features use: command registry, menus and toolbars, dock panels and editor tabs (Avalonia controls or AXAML), editor providers per resource type, the current workspace and G3MLib model with undo-aware edit methods, selection and events (opened, saved, selection changed, before and after save, before and after game launch), Output channels and notifications, progress jobs, settings (schema-based, appear in the settings UI), localization, file dialogs, the game launcher and the patch services.
- Built-in features are written against the same API where practical, so the API stays complete.
- Errors in a plugin are caught at the boundary, logged to its Output channel and never crash the app; a plugin that fails on activation is disabled with a notification.

## Trust and distribution

- Full access, like VS Code extensions and UTMT scripts. On first install or update G3MStudio shows source, author, declared permissions and asks once.
- Install from a folder, a `.zip`, or a GitHub URL; Plugins panel to enable, disable, update, reload and open the folder. A catalog comes after beta.
- A plugin template command creates a ready-to-edit plugin folder; hot reload on save while developing.
- UTMT `.csx` scripts keep working as plain scripts; a plugin can bundle and register scripts as commands.

## Additions from the library split study (2026-10-26, Q47)

Source: [../research/library-gui-split.md](../research/library-gui-split.md), section 8. They extend the API; none changes the shape above.

- `IStudioContext` exposes the shared services the built-in features use: job service (progress, cancel, result table), decompile cache, reference index, name and string lookup, undo scope. A plugin calls them and does not rebuild them.
- Threading follows G3MLib: background reads use a model snapshot or the read scope; every write goes through one undo aware edit call; batch work is written as a read only plan followed by one apply.
- No separate code patch layer. The patch verbs already exist in G3MLib's `CodeImportGroup`; once each queued operation reports applied, no match or failed, plugins use it directly (OI-053). A helper library is added when two real plugins repeat the same code.
- The built-in import and export formats register through the same provider extension point plugins use, so a plugin can add a format to the shared batch dialog and its plan table.

## Rejected

- Sandboxed permissions: large cost, breaks UTMT script compatibility.
- `.dll`-only plugins: needs an SDK, unfriendly to modders.
