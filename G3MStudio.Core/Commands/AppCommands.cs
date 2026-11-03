
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

    void NewDataFile();

    void CloseWorkspace();

    void Save();

    void SaveAs();

    void SaveCopy();

    void ShowAbout();

    /// <summary>Opens the folder of the settings, journals and plugins in the file manager.</summary>
    void OpenDataFolder();

    /// <summary>Copies the name of the selected resource to the clipboard.</summary>
    void CopyName();

    /// <summary>Shows the VM assembly of the code entry in front.</summary>
    void ShowDisassembly();

    /// <summary>Lists the assets nothing refers to in the Usages panel.</summary>
    void FindUnreferenced();
    void FindChildren();

    /// <summary>Ctrl+Tab and Ctrl+Shift+Tab: the next and the previous tab of the group in front.</summary>
    void NextTab();

    void PreviousTab();

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

    void RunWithRuntime();

    void WriteOffsetMap();

    void ReplaceFrame();

    /// <summary>Opens the painter on the clicked frame of the sprite in front.</summary>
    void PaintFrame();

    /// <summary>Replaces the audio of the sound in front with a WAV or OGG file of the same kind.</summary>
    void ReplaceAudio();

    /// <summary>Opens the audio editor on the WAV sound selected: cut, volume and fades.</summary>
    void EditAudio();

    /// <summary>Saves the audio of the sound in front to a file.</summary>
    void ExportAudio();

    /// <summary>Saves the embedded texture page in front as a PNG.</summary>
    void ExportPage();

    /// <summary>Replaces the picture of the embedded texture page in front with a PNG of the same size.</summary>
    void ReplacePage();

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

    void ShowCreationCode();

    void ExportMod();

    void CreatePatch();

    void ApplyPatch();

    void ValidatePatch();
    void ApplyMod();

    void ApplyPatchOpen();

    void ApplyModOpen();

    void Undo();

    void Redo();

    /// <summary>Renames the selected resource (Explorer selection, else the active tab).</summary>
    void Rename();

    /// <summary>Asks for a name and adds an empty resource of the type (an Explorer type key).</summary>
    void NewResource(string type);

    /// <summary>Deletes the selected resource when nothing uses it.</summary>
    void DeleteResource();

    /// <summary>Asks for a name and copies the selected object or script under it.</summary>
    void DuplicateResource();

    void MoveResourceUp();

    void MoveResourceDown();

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

    /// <summary>Opens the window that orders the rooms of the game.</summary>
    void ShowRoomOrder();

    /// <summary>Opens the hex view of the data file as it is on disk.</summary>
    void ShowHexView();

    /// <summary>Puts the panels back where they start; open editors stay open.</summary>
    void ResetLayout();

    /// <summary>Opens the window that saves, loads and deletes named layouts.</summary>
    void ShowLayouts();

    /// <summary>The Code preset (true) or the default Studio layout (false).</summary>
    void UseLayoutPreset(string preset);

    /// <summary>The welcome window with the language choice.</summary>
    void ShowOnboarding();

    /// <summary>The short tour of the window.</summary>
    void ShowTour();

    void RunScript();

    /// <summary>Go to Resource (Ctrl+P).</summary>
    void QuickOpen();

    void CloseTab();

    void CloseAllTabs();

    /// <summary>Closes the tabs but the one in front.</summary>
    void CloseOtherTabs();

    /// <summary>Reopen Closed Tab (Ctrl+Shift+T).</summary>
    void ReopenTab();

    /// <summary>Back and Forward through the resources shown (Alt+Left, Alt+Right).</summary>
    void TabBack();

    void TabForward();

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
    /// <summary>The "New" submenu of the File menu: one command per type that can be created from nothing.</summary>
    public static readonly string[] ResourceNew = [.. Workspace.ResourceListCommand.Creatable.Select(t => "file.new." + t)];

    public static readonly string[] Panels = ["Explorer", "Search", "Inspector", "Problems", "Output", "Usages", "History", "Git"];

    /// <summary>
    /// Menu bar: title key and the entries in order. "-" is a separator. An id with three parts (view.panel.explorer) goes into a submenu
    /// named by its first two parts (menu.sub.view.panel).
    /// </summary>
    public static readonly (string TitleKey, string[] Ids)[] Menus =
    [
        ("menu.file", ["file.newData", "file.open", .. ResourceNew, "file.save", "file.saveAs", "file.saveCopy", "file.revert", "-", "file.exportSelection", "file.exportAll", "file.import", "-", "settings.open", "-", "file.close", "file.exit"]),
        ("menu.edit", ["edit.undo", "edit.redo", "-", "edit.rename", "edit.duplicate", "edit.moveUp", "edit.moveDown", "edit.copyName", "edit.delete", "edit.selectAll", "-", "edit.find", "edit.findInCode", "edit.findUsages", "edit.findUnreferenced", "edit.findChildren"]),
        ("menu.view", ["view.commandPalette", "go.quickOpen", "-", .. Panels.Select(p => "view.panel." + p.ToLowerInvariant()), "-", "view.zoomIn", "view.zoomOut", "view.zoomReset", "view.fullscreen"]),
        ("menu.project", ["project.enable", "project.sync", "project.options", "project.resolve", "project.moveDataFile", "project.disable"]),
        ("menu.sprite", ["edit.paintFrame", "edit.replaceFrame", "edit.duplicateFrame", "edit.removeFrame", "-", "edit.recalculateMask", "edit.importMask", "edit.exportMask"]),
        ("menu.sound", ["edit.editAudio", "edit.replaceAudio", "edit.exportAudio"]),
        ("menu.room", ["room.play", "-", "room.creationCode", "room.creationOrder", "room.exportTiles", "room.importTiles", "room.savePng"]),
        ("menu.run", ["run.game", "run.withRuntime", "run.stop"]),
        ("menu.mods", ["mods.createPatch", "mods.applyPatch", "mods.applyMod", "mods.applyPatchOpen", "mods.applyModOpen", "mods.mergePatches", "mods.validatePatch", "mods.compareFiles", "mods.exportMod"]),
        ("menu.tools", ["tools.plugins", "tools.strings", "tools.roomOrder", "tools.hexView", "tools.offsetMap", "tools.disassemble", "tools.runScript", "-", "tools.repackTextures", "tools.exportPage", "tools.replacePage"]),
        ("menu.window", ["tabs.close", "tabs.closeOthers", "tabs.closeAll", "tabs.reopen", "-", "tabs.next", "tabs.previous", "tabs.back", "tabs.forward", "-", "tabs.pin", "tabs.keepOpen", "tabs.splitRight", "-", "window.layout.studio", "window.layout.code", "window.layout.canvas", "window.layout.reset", "window.layout.saved"]),
        ("menu.help", ["help.gettingStarted", "help.tour", "help.documentation", "help.checkUpdates", "help.openDataFolder", "-", "help.about"]),
    ];

    /// <summary>The language key of a command title: a panel command is named like the panel itself, every other one "cmd.&lt;id&gt;".</summary>
    public static string TitleKey(string id) => id.StartsWith("view.panel.", StringComparison.Ordinal) ? "panel." + id["view.panel.".Length..] : "cmd." + id;

    public static CommandRegistry Create(IShell shell)
    {
        var registry = new CommandRegistry();
        void Add(string id, string? shortcut, Action run, string? when = null) => registry.Add(new Command(id, shortcut, run, when));

        Add("file.newData", "Ctrl+N", shell.NewDataFile);
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
        Add("run.withRuntime", null, shell.RunWithRuntime, WorkspaceOpen);
        Add("run.stop", "Shift+F5", shell.StopGame);
        Add("edit.editAudio", null, shell.EditAudio, WorkspaceOpen);
        Add("edit.replaceAudio", null, shell.ReplaceAudio, WorkspaceOpen);
        Add("edit.exportAudio", null, shell.ExportAudio, WorkspaceOpen);
        Add("edit.paintFrame", null, shell.PaintFrame, WorkspaceOpen);
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
        Add("tools.exportPage", null, shell.ExportPage, WorkspaceOpen);
        Add("tools.replacePage", null, shell.ReplacePage, WorkspaceOpen);
        Add("tools.repackTextures", null, shell.RepackTextures, WorkspaceOpen);
        Add("room.creationCode", null, shell.ShowCreationCode, RoomTab);
        Add("room.creationOrder", null, shell.ShowCreationOrder, RoomTab);
        Add("room.exportTiles", null, shell.ExportTiles, RoomTab);
        Add("room.importTiles", null, shell.ImportTiles, RoomTab);
        Add("room.play", "Ctrl+F5", shell.PlayRoom, RoomTab);
        Add("room.savePng", null, shell.SaveRoomPng, RoomTab);
        Add("mods.exportMod", null, shell.ExportMod, WorkspaceOpen);
        Add("mods.validatePatch", null, shell.ValidatePatch);
        Add("mods.applyMod", null, shell.ApplyMod);
        Add("mods.applyPatchOpen", null, shell.ApplyPatchOpen, WorkspaceOpen);
        Add("mods.applyModOpen", null, shell.ApplyModOpen, WorkspaceOpen);
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
        Add("edit.delete", null, shell.DeleteResource, WorkspaceOpen);
        Add("edit.duplicate", null, shell.DuplicateResource, WorkspaceOpen);
        Add("edit.moveUp", null, shell.MoveResourceUp, WorkspaceOpen);
        Add("edit.moveDown", null, shell.MoveResourceDown, WorkspaceOpen);
        Add("edit.copyName", null, shell.CopyName, WorkspaceOpen);
        Add("edit.findUnreferenced", null, shell.FindUnreferenced, WorkspaceOpen);
        Add("edit.findChildren", null, shell.FindChildren, WorkspaceOpen);
        Add("tabs.next", "Ctrl+Tab", shell.NextTab, WorkspaceOpen);
        Add("tabs.previous", "Ctrl+Shift+Tab", shell.PreviousTab, WorkspaceOpen);
        foreach (var type in Workspace.ResourceListCommand.Creatable) Add("file.new." + type, null, () => shell.NewResource(type), WorkspaceOpen);
        Add("edit.selectAll", "Ctrl+A", shell.SelectAll, WorkspaceOpen);
        Add("edit.find", "Ctrl+F", shell.Find, WorkspaceOpen);
        Add("edit.findInCode", "Ctrl+Shift+F", shell.ShowSearch, WorkspaceOpen);
        Add("edit.findUsages", "Shift+F12", shell.FindUsages, WorkspaceOpen);
        Add("view.commandPalette", "Ctrl+Shift+P", shell.ShowPalette);
        Add("view.fullscreen", "F11", shell.ToggleFullscreen);
        Add("view.zoomIn", "Ctrl+OemPlus", () => shell.Zoom(1));
        Add("view.zoomOut", "Ctrl+OemMinus", () => shell.Zoom(-1));
        Add("view.zoomReset", "Ctrl+D0", () => shell.Zoom(0));
        Add("tabs.close", "Ctrl+W", shell.CloseTab, WorkspaceOpen);
        Add("tabs.closeOthers", null, shell.CloseOtherTabs, WorkspaceOpen);
        Add("tabs.closeAll", "Ctrl+Shift+W", shell.CloseAllTabs, WorkspaceOpen);
        Add("tabs.reopen", "Ctrl+Shift+T", shell.ReopenTab, WorkspaceOpen);
        Add("tabs.back", "Alt+Left", shell.TabBack, WorkspaceOpen);
        Add("tabs.forward", "Alt+Right", shell.TabForward, WorkspaceOpen);
        Add("tabs.pin", null, shell.PinTab, WorkspaceOpen);
        Add("tabs.keepOpen", null, shell.KeepTabOpen, WorkspaceOpen);
        Add("tabs.splitRight", "Ctrl+OemPipe", shell.SplitRight, WorkspaceOpen);
        Add("go.quickOpen", "Ctrl+P", shell.QuickOpen, WorkspaceOpen);
        Add("settings.open", "Ctrl+OemComma", shell.ShowSettings);
        Add("tools.plugins", null, shell.ShowPlugins);
        Add("tools.strings", null, shell.ShowStrings, WorkspaceOpen);
        Add("tools.roomOrder", null, shell.ShowRoomOrder, WorkspaceOpen);
        Add("tools.hexView", null, shell.ShowHexView, WorkspaceOpen);
        Add("tools.offsetMap", null, shell.WriteOffsetMap, WorkspaceOpen);
        Add("tools.disassemble", null, shell.ShowDisassembly, WorkspaceOpen);
        Add("help.gettingStarted", null, shell.ShowOnboarding);
        Add("help.tour", null, shell.ShowTour);
        Add("window.layout.studio", null, () => shell.UseLayoutPreset("studio"), WorkspaceOpen);
        Add("window.layout.code", null, () => shell.UseLayoutPreset("code"), WorkspaceOpen);
        Add("window.layout.canvas", null, () => shell.UseLayoutPreset("canvas"), WorkspaceOpen);
        Add("window.layout.reset", null, shell.ResetLayout, WorkspaceOpen);
        Add("window.layout.saved", null, shell.ShowLayouts, WorkspaceOpen);
        Add("tools.runScript", null, shell.RunScript, WorkspaceOpen);
        Add("help.about", null, shell.ShowAbout);
        Add("help.openDataFolder", null, shell.OpenDataFolder);
        Add("help.documentation", "F1", shell.OpenDocumentation);
        return registry;
    }
}
