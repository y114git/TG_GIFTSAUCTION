using G3MStudio.Core.Localization;

namespace G3MStudio.Core.Commands;

/// <summary>What commands need from the window. The window implements it; tests fake it.</summary>
public interface IShell
{
    void ShowPalette();

    void ToggleFullscreen();

    /// <summary>Zoom step: +1 or -1, or 0 to reset.</summary>
    void Zoom(int step);

    void Exit();

    void OpenDataFile();

    void CloseWorkspace();

    void Save();

    void SaveAs();

    void SaveCopy();

    void ShowAbout();

    void OpenDocumentation();

    /// <summary>Select All and Find go to the text editor that has the focus.</summary>
    void SelectAll();

    void Find();

    void ShowSearch();

    /// <summary>Lists where the selected resource (or the one in the tab in front) is used.</summary>
    void FindUsages();

    void ExportSelection();

    void ExportAll();

    void ImportResources();

    void RevertToSaved();

    void CheckForUpdates();

    void RunGame();

    void StopGame();

    void ReplaceFrame();

    void DuplicateFrame();

    void RecalculateMask();

    void ImportMask();

    void ExportMask();

    void RemoveFrame();

    void MergePatches();

    void CompareFiles();

    void RepackTextures();

    void SaveRoomPng();

    void PlayRoom();

    void ExportTiles();

    void ImportTiles();

    void ShowCreationOrder();

    void ExportMod();

    void CreatePatch();

    void ApplyPatch();

    void ValidatePatch();

    void Undo();

    void Redo();

    /// <summary>Renames the selected resource (Explorer selection, else the active tab).</summary>
    void Rename();

    void EnableProject();

    void SyncProject();

    void ProjectOptions();

    void MoveDataFile();

    /// <summary>Asks how to handle a data file that changed outside.</summary>
    void ResolveExternalChange();

    void DisableProject();

    /// <summary>Shows a dock panel by its id, opening it again when it was closed.</summary>
    void ShowPanel(string id);

    void ShowSettings();

    void ShowPlugins();

    void ShowStrings();

    void RunScript();

    /// <summary>Go to Resource (Ctrl+P).</summary>
    void QuickOpen();

    void CloseTab();

    void CloseAllTabs();

    /// <summary>Pins the active tab, or unpins it.</summary>
    void PinTab();

    void KeepTabOpen();

    void SplitRight();
}

public static class AppCommands
{
    public const string WorkspaceOpen = "workspaceOpen";

    /// <summary>Set while the tab in front is a sprite or a room, which the commands of the Sprite and Room menus work on.</summary>
    public const string SpriteTab = "spriteTab", RoomTab = "roomTab";

    /// <summary>The panels that can be shown again from View > Panels after they were closed. The key is the dock panel id.</summary>
    public static readonly string[] Panels = ["Explorer", "Search", "Inspector", "Problems", "Output", "Usages", "History", "Git"];

    /// <summary>
    /// Menu bar: title key and the entries in order. "-" is a separator. An id with three parts (view.language.en) goes into a submenu
    /// named by its first two parts (menu.sub.view.language).
    /// </summary>
    public static readonly (string TitleKey, string[] Ids)[] Menus =
    [
        ("menu.file", ["file.open", "file.save", "file.saveAs", "file.saveCopy", "file.revert", "-", "file.exportSelection", "file.exportAll", "file.import", "-", "settings.open", "-", "file.close", "file.exit"]),
        ("menu.edit", ["edit.undo", "edit.redo", "-", "edit.rename", "edit.selectAll", "-", "edit.find", "edit.findInCode", "edit.findUsages", "go.quickOpen"]),
        ("menu.view", ["view.commandPalette", "-", .. Panels.Select(p => "view.panel." + p.ToLowerInvariant()), "-", "view.zoomIn", "view.zoomOut", "view.zoomReset", "view.fullscreen", "-", "view.language.en", "view.language.ru", "view.language.es", "view.language.ja", "view.language.ko", "view.language.zh_cn", "view.language.zh_tw"]),
        ("menu.project", ["project.enable", "project.sync", "project.options", "project.resolve", "project.moveDataFile", "project.disable"]),
        ("menu.sprite", ["edit.replaceFrame", "edit.duplicateFrame", "edit.removeFrame", "-", "edit.recalculateMask", "edit.importMask", "edit.exportMask"]),
        ("menu.room", ["room.play", "-", "room.creationOrder", "room.exportTiles", "room.importTiles", "room.savePng"]),
        ("menu.run", ["run.game", "run.stop"]),
        ("menu.mods", ["mods.createPatch", "mods.applyPatch", "mods.mergePatches", "mods.validatePatch", "mods.compareFiles", "mods.exportMod"]),
        ("menu.tools", ["tools.plugins", "tools.strings", "tools.runScript", "tools.repackTextures"]),
        ("menu.window", ["tabs.close", "tabs.closeAll", "-", "tabs.pin", "tabs.keepOpen", "tabs.splitRight"]),
        ("menu.help", ["help.documentation", "help.checkUpdates", "-", "help.about"]),
    ];

    /// <summary>The language key of a command title: a panel command is named like the panel itself, every other one "cmd.&lt;id&gt;".</summary>
    public static string TitleKey(string id) => id.StartsWith("view.panel.", StringComparison.Ordinal) ? "panel." + id["view.panel.".Length..] : "cmd." + id;

    public static CommandRegistry Create(IShell shell, Localizer localizer)
    {
        var registry = new CommandRegistry();
        void Add(string id, string? shortcut, Action run, string? when = null) => registry.Add(new Command(id, shortcut, run, when));

        Add("file.open", "Ctrl+O", shell.OpenDataFile);
        Add("file.save", "Ctrl+S", shell.Save, WorkspaceOpen);
        Add("file.saveAs", "Ctrl+Shift+S", shell.SaveAs, WorkspaceOpen);
        Add("file.saveCopy", null, shell.SaveCopy, WorkspaceOpen);
        Add("file.revert", null, shell.RevertToSaved, WorkspaceOpen);
        Add("file.exportSelection", null, shell.ExportSelection, WorkspaceOpen);
        Add("file.exportAll", null, shell.ExportAll, WorkspaceOpen);
        Add("file.import", null, shell.ImportResources, WorkspaceOpen);
        Add("help.checkUpdates", null, shell.CheckForUpdates);
        Add("run.game", "F5", shell.RunGame, WorkspaceOpen);
        Add("run.stop", "Shift+F5", shell.StopGame);
        Add("edit.replaceFrame", null, shell.ReplaceFrame, SpriteTab);
        Add("edit.duplicateFrame", null, shell.DuplicateFrame, SpriteTab);
        Add("edit.recalculateMask", null, shell.RecalculateMask, SpriteTab);
        Add("edit.importMask", null, shell.ImportMask, SpriteTab);
        Add("edit.exportMask", null, shell.ExportMask, SpriteTab);
        Add("edit.removeFrame", null, shell.RemoveFrame, SpriteTab);
        Add("mods.createPatch", null, shell.CreatePatch, WorkspaceOpen);
        Add("mods.applyPatch", null, shell.ApplyPatch);
        Add("mods.mergePatches", null, shell.MergePatches);
        Add("mods.compareFiles", null, shell.CompareFiles);
        Add("tools.repackTextures", null, shell.RepackTextures, WorkspaceOpen);
        Add("room.creationOrder", null, shell.ShowCreationOrder, RoomTab);
        Add("room.exportTiles", null, shell.ExportTiles, RoomTab);
        Add("room.importTiles", null, shell.ImportTiles, RoomTab);
        Add("room.play", "Ctrl+F5", shell.PlayRoom, RoomTab);
        Add("room.savePng", null, shell.SaveRoomPng, RoomTab);
        Add("mods.exportMod", null, shell.ExportMod, WorkspaceOpen);
        Add("mods.validatePatch", null, shell.ValidatePatch);
        Add("file.close", "Ctrl+Alt+W", shell.CloseWorkspace, WorkspaceOpen);
        Add("file.exit", "Ctrl+Q", shell.Exit);
        Add("edit.undo", "Ctrl+Z", shell.Undo, WorkspaceOpen);
        Add("edit.redo", "Ctrl+Y", shell.Redo, WorkspaceOpen);
        Add("project.enable", null, shell.EnableProject, WorkspaceOpen);
        Add("project.sync", null, shell.SyncProject, WorkspaceOpen);
        Add("project.options", null, shell.ProjectOptions, WorkspaceOpen);
        Add("project.resolve", null, shell.ResolveExternalChange, WorkspaceOpen);
        Add("project.moveDataFile", null, shell.MoveDataFile, WorkspaceOpen);
        Add("project.disable", null, shell.DisableProject, WorkspaceOpen);
        foreach (var panel in Panels) Add("view.panel." + panel.ToLowerInvariant(), null, () => shell.ShowPanel(panel));
        Add("edit.rename", "F2", shell.Rename, WorkspaceOpen);
        Add("edit.selectAll", "Ctrl+A", shell.SelectAll, WorkspaceOpen);
        Add("edit.find", "Ctrl+F", shell.Find, WorkspaceOpen);
        Add("edit.findInCode", "Ctrl+Shift+F", shell.ShowSearch, WorkspaceOpen);
        Add("edit.findUsages", "Shift+F12", shell.FindUsages, WorkspaceOpen);
        Add("view.commandPalette", "Ctrl+Shift+P", shell.ShowPalette);
        Add("view.fullscreen", "F11", shell.ToggleFullscreen);
        Add("view.zoomIn", "Ctrl+OemPlus", () => shell.Zoom(1));
        Add("view.zoomOut", "Ctrl+OemMinus", () => shell.Zoom(-1));
        Add("view.zoomReset", "Ctrl+D0", () => shell.Zoom(0));
        foreach (var code in new[] { "en", "ru", "es", "ja", "ko", "zh_cn", "zh_tw" })
            Add("view.language." + code, null, () => localizer.SetLanguage(code));
        Add("tabs.close", "Ctrl+W", shell.CloseTab, WorkspaceOpen);
        Add("tabs.closeAll", "Ctrl+Shift+W", shell.CloseAllTabs, WorkspaceOpen);
        Add("tabs.pin", null, shell.PinTab, WorkspaceOpen);
        Add("tabs.keepOpen", null, shell.KeepTabOpen, WorkspaceOpen);
        Add("tabs.splitRight", "Ctrl+OemPipe", shell.SplitRight, WorkspaceOpen);
        Add("go.quickOpen", "Ctrl+P", shell.QuickOpen, WorkspaceOpen);
        Add("settings.open", "Ctrl+OemComma", shell.ShowSettings);
        Add("tools.plugins", null, shell.ShowPlugins);
        Add("tools.strings", null, shell.ShowStrings, WorkspaceOpen);
        Add("tools.runScript", null, shell.RunScript, WorkspaceOpen);
        Add("help.about", null, shell.ShowAbout);
        Add("help.documentation", "F1", shell.OpenDocumentation);
        return registry;
    }
}
