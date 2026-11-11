namespace G3MStudio.Core.Commands;

public static partial class AppCommands
{
    public const string WorkspaceOpen = "workspaceOpen";

    /// <summary>Set while a resource is selected in the Explorer or open in the tab in front, which rename, duplicate, move, delete and copy name work on.</summary>
    public static string CanNew(string type) => "canNew." + type;

    public const string ResourceSelected = "resourceSelected";

    /// <summary>Set while the tab in front is a sprite or a room, which the commands of the Sprite and Room menus work on.</summary>
    public const string SpriteTab = "spriteTab", PictureTab = "pictureTab", RoomTab = "roomTab", ProjectOn = "projectOn", ProjectOff = "projectOff";

    public static CommandRegistry Create(IShell shell)
    {
        var registry = new CommandRegistry();
        AddFileAndRun(registry, shell);
        AddEditAndMods(registry, shell);
        AddViewAndTools(registry, shell);
        AddOrganize(registry, shell);
        foreach (var (id, shortcut) in CodeCommands) registry.Add(new Command(id, shortcut, () => shell.CodeCommand(id), CodeTab));
        foreach (var (id, shortcut) in ImageCommands) registry.Add(new Command(id, shortcut, () => shell.ImageCommand(id), ImageTab, Aliases: ImageAliases.GetValueOrDefault(id)));
        return registry;
    }

    private static void AddFileAndRun(CommandRegistry registry, IShell shell)
    {
        void Add(string id, string? shortcut, Action run, string? when = null) => registry.Add(new Command(id, shortcut, run, when));

        Add("file.newData", "Ctrl+N", shell.NewDataFile);
        Add("file.open", "Ctrl+O", shell.OpenDataFile);
        Add("file.save", "Ctrl+S", shell.Save, WorkspaceOpen);
        Add("file.saveAs", "Ctrl+Shift+S", shell.SaveAs, WorkspaceOpen);
        Add("file.saveCopy", null, shell.SaveCopy, WorkspaceOpen);
        Add("file.revert", null, shell.RevertToSaved, WorkspaceOpen);
        Add("file.exportSelection", null, shell.ExportSelection, WorkspaceOpen);
        Add("file.exportAll", null, shell.ExportAll, WorkspaceOpen);
        Add("file.transfer", null, shell.Transfer, WorkspaceOpen);
        Add("file.import", null, shell.ImportResources, WorkspaceOpen);
        Add("file.importPackage", null, shell.ImportPackage, WorkspaceOpen);
        Add("file.importFiles", null, shell.ImportFiles, WorkspaceOpen);
        Add("file.importCodeFolder", null, shell.ImportCodeFolder, WorkspaceOpen);
        Add("file.importCodeFiles", null, shell.ImportCodeFiles, WorkspaceOpen);
        Add("help.checkUpdates", null, shell.CheckForUpdates);
        Add("run.game", "F5", shell.RunGame, WorkspaceOpen);
        Add("run.withRuntime", null, shell.RunWithRuntime, WorkspaceOpen);
        Add("run.restart", "Ctrl+F5", shell.RestartGame, WorkspaceOpen);
        Add("run.stop", "Shift+F5", shell.StopGame);
        Add("edit.editAudio", null, shell.EditAudio, WorkspaceOpen);
        Add("edit.replaceAudio", null, shell.ReplaceAudio, WorkspaceOpen);
        Add("edit.exportAudio", null, shell.ExportAudio, WorkspaceOpen);
        Add("edit.editImage", null, shell.EditImage, PictureTab);
        Add("edit.replaceFrame", null, shell.ReplaceFrame, SpriteTab);
        Add("edit.duplicateFrame", null, shell.DuplicateFrame, SpriteTab);
        Add("edit.recalculateMask", null, shell.RecalculateMask, SpriteTab);
        Add("edit.importMask", null, shell.ImportMask, SpriteTab);
        Add("edit.exportMask", null, shell.ExportMask, SpriteTab);
        Add("edit.exportGif", null, shell.ExportGif, SpriteTab);
        Add("edit.importSheet", null, shell.ImportSheet, SpriteTab);
        Add("edit.exportAllAudio", null, shell.ExportAllAudio, WorkspaceOpen);
        Add("edit.removeFrame", null, shell.RemoveFrame, SpriteTab);
        Add("mods.createPatch", null, shell.CreatePatch, WorkspaceOpen);
        Add("mods.applyPatch", null, shell.ApplyPatch);
        Add("mods.mergePatches", null, shell.MergePatches);
        Add("mods.compareDataFiles", null, shell.CompareDataFiles);
        Add("mods.exportPack", null, shell.ExportModPack, WorkspaceOpen);
        Add("mods.rememberBase", null, shell.RememberModBase, WorkspaceOpen);
        Add("mods.clearBase", null, shell.ClearModBase);
        Add("settings.resetPrompts", null, shell.ResetPrompts);
        Add("run.migrateOptions", null, shell.MigrateRunOptions, WorkspaceOpen);
        Add("mods.applyPack", null, shell.ApplyModPack, WorkspaceOpen);
        Add("tools.exportPage", null, shell.ExportPage, WorkspaceOpen);
        Add("tools.replacePage", null, shell.ReplacePage, WorkspaceOpen);
        Add("tools.repackTextures", null, shell.RepackTextures, WorkspaceOpen);
        Add("room.creationCode", null, shell.ShowCreationCode, RoomTab);
        Add("room.creationOrder", null, shell.ShowCreationOrder, RoomTab);
        Add("room.exportTiles", null, shell.ExportTiles, RoomTab);
        Add("room.importTiles", null, shell.ImportTiles, RoomTab);
        Add("room.imageToTiles", null, shell.ConvertImageToTiles, RoomTab);
        Add("room.play", "Ctrl+Shift+F5", shell.PlayRoom, RoomTab);
        Add("room.savePng", null, shell.SaveRoomPng, RoomTab);
        Add("room.autoTile", null, shell.ToggleAutoTile, RoomTab);
        Add("room.retileSelection", null, shell.RetileSelection, RoomTab);
    }

    private static void AddEditAndMods(CommandRegistry registry, IShell shell)
    {
        void Add(string id, string? shortcut, Action run, string? when = null) => registry.Add(new Command(id, shortcut, run, when));

        Add("mods.exportMod", null, shell.ExportMod, WorkspaceOpen);
        Add("mods.validatePatch", null, shell.ValidatePatch);
        Add("mods.applyMod", null, shell.ApplyMod);
        Add("file.close", "Ctrl+Alt+W", shell.CloseWorkspace, WorkspaceOpen);
        Add("file.exit", "Ctrl+Q", shell.Exit);
        Add("edit.undo", "Ctrl+Z", shell.Undo, WorkspaceOpen);
        Add("edit.redo", "Ctrl+Y", shell.Redo, WorkspaceOpen);
        Add("project.enable", null, shell.EnableProject, ProjectOff);
        Add("project.sync", null, shell.SyncProject, ProjectOn);
        Add("project.options", null, shell.ProjectOptions, ProjectOn);
        Add("project.resolve", null, shell.ResolveExternalChange, ProjectOn);
        Add("project.moveDataFile", null, shell.MoveDataFile, ProjectOn);
        Add("project.disable", null, shell.DisableProject, ProjectOn);
        foreach (var panel in Panels) Add("view.panel." + panel.ToLowerInvariant(), null, () => shell.ShowPanel(panel));
        Add("edit.rename", "F2", shell.Rename, ResourceSelected);
        Add("edit.delete", null, shell.DeleteResource, ResourceSelected);
        Add("edit.duplicate", null, shell.DuplicateResource, ResourceSelected);
        Add("edit.copyToOtherFile", null, shell.CopyToOtherFile, WorkspaceOpen);
        Add("edit.reveal", null, shell.RevealInExplorer, WorkspaceOpen);
        Add("edit.toggleFavourite", null, shell.ToggleFavourite, WorkspaceOpen);
        Add("edit.addTag", null, shell.AddTag, WorkspaceOpen);
        Add("edit.moveUp", null, shell.MoveResourceUp, ResourceSelected);
        Add("edit.moveDown", null, shell.MoveResourceDown, ResourceSelected);
        Add("edit.copyName", null, shell.CopyName, ResourceSelected);
        Add("edit.findUnreferenced", null, shell.FindUnreferenced, WorkspaceOpen);
        Add("edit.findChildren", null, shell.FindChildren, WorkspaceOpen);
        Add("tabs.next", "Ctrl+Tab", shell.NextTab, WorkspaceOpen);
        Add("tabs.previous", "Ctrl+Shift+Tab", shell.PreviousTab, WorkspaceOpen);
        foreach (var type in Workspace.ResourceListCommand.Creatable) Add("file.new." + type, null, () => shell.NewResource(type), CanNew(type));
        Add("edit.selectAll", "Ctrl+A", shell.SelectAll, WorkspaceOpen);
        Add("edit.find", "Ctrl+F", shell.Find, WorkspaceOpen);
        Add("edit.findInCode", "Ctrl+Shift+F", shell.ShowSearch, WorkspaceOpen);
        Add("edit.replaceInCode", "Ctrl+Shift+H", shell.ShowReplace, WorkspaceOpen);
        Add("edit.findUsages", "Shift+F12", shell.FindUsages, WorkspaceOpen);
        Add("edit.findUses", null, shell.FindUses, WorkspaceOpen);
    }
}
