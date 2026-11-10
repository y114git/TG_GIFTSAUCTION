
namespace G3MStudio.Core.Commands;

/// <summary>What commands need from the window. The window implements it; tests fake it.</summary>
public interface IShell : IOrganizeShell
{
    void ShowPalette();

    void ToggleFullscreen();

    /// <summary>Moves the keyboard focus to the next (1) or previous (-1) region: menu, Explorer, editor area, bottom panels, status bar.</summary>
    void FocusRegion(int direction);

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

    /// <summary>Ctrl+Shift+H: opens the Search panel with the cursor in the replace box.</summary>
    void ShowReplace();

    /// <summary>Lists where the selected resource (or the one in the tab in front) is used.</summary>
    void FindUsages();

    void FindUses();

    void ExportSelection();

    void ExportAll();

    /// <summary>One entry for every way resources move between data files and folders: asks which one (import from a file, copy, package, files, mod ZIP, mod pack).</summary>
    void Transfer();

    void ImportResources();

    void ImportPackage();

    void ImportFiles();

    /// <summary>Import GML: replaces the code of existing entries from .gml and .asm files named after them, picked as a folder or as files.</summary>
    void ImportCodeFolder();

    void ImportCodeFiles();

    void RevertToSaved();

    void CheckForUpdates();

    void RunGame();

    void StopGame();
    void RestartGame();

    void RunWithRuntime();

    void WriteOffsetMap();

    /// <summary>Copies the raw bytes of one chunk of the data file on disk to a file.</summary>
    void ExportChunk();

    /// <summary>Replaces the raw bytes of one chunk of the data file on disk from a file, after a warning, and loads the file again.</summary>
    void ImportChunk();

    void WriteDebugData();

    void ReplaceFrame();

    /// <summary>Opens the painter on the clicked frame of the sprite in front.</summary>

    /// <summary>Opens the image editor as a tab on the clicked frame of the sprite in front; every edit is one undoable step.</summary>
    void EditImage();

    /// <summary>Replaces the audio of the sound in front with a WAV or OGG file of the same kind.</summary>
    void ReplaceAudio();

    /// <summary>Opens the audio editor on the WAV sound selected: cut, volume and fades.</summary>
    void EditAudio();

    /// <summary>Saves the audio of the sound in front to a file.</summary>
    void ExportAudio();

    /// <summary>Saves the audio of every sound to a folder, with progress and Cancel.</summary>
    void ExportAllAudio();

    /// <summary>Saves the embedded texture page in front as a PNG.</summary>
    void ExportPage();

    /// <summary>Replaces the picture of the embedded texture page in front with a PNG of the same size.</summary>
    void ReplacePage();

    void DuplicateFrame();

    void RecalculateMask();

    void ImportMask();

    void ExportMask();

    /// <summary>Saves the sprite in front as an animated GIF.</summary>
    void ExportGif();

    /// <summary>Cuts a picture or GIF into frames of the sprite in front.</summary>
    void ImportSheet();

    void RemoveFrame();

    void MergePatches();


    void CompareDataFiles();
    void ExportModPack();

    void RememberModBase();

    void ClearModBase();

    void ResetPrompts();

    void MigrateRunOptions();
    void ApplyModPack();

    void RepackTextures();

    void SaveRoomPng();

    void PlayRoom();

    void ExportTiles();

    void ImportTiles();

    /// <summary>Builds a tile layer from a picture by matching the tiles of a tile set.</summary>
    void ConvertImageToTiles();

    void ShowCreationOrder();

    void ShowCreationCode();

    void ToggleAutoTile();

    void RetileSelection();

    void ExportMod();

    void CreatePatch();

    void ApplyPatch();

    void ValidatePatch();
    void ApplyMod();

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

    /// <summary>Copies the selected resources, with what they use, into another data file as one undoable edit of that file.</summary>
    void CopyToOtherFile();

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

    /// <summary>Opens the dialog that lists texture groups with their members and moves resources between them.</summary>
    void ShowTextureGroups();

    /// <summary>Opens the dialog that lists audio groups with their sounds and moves sounds between them.</summary>
    void ShowAudioGroups();

    /// <summary>Opens the hex view of the data file as it is on disk.</summary>
    void ShowHexView();

    /// <summary>Puts the panels back where they start; open editors stay open.</summary>
    void ResetLayout();

    /// <summary>Opens the window that saves, loads and deletes named layouts.</summary>
    void ShowLayouts();

    /// <summary>Puts a built-in preset (<see cref="Workspace.LayoutBook.BuiltIn"/>) or a saved layout in place; open editors stay.</summary>
    void UseLayoutPreset(string preset);

    /// <summary>Asks for a name and saves the current layout under it.</summary>
    void SaveLayout();

    /// <summary>Folds every dock, or opens them all again when they are folded.</summary>
    void ToggleDocks();

    /// <summary>Folds or opens one dock: "left", "right" or "bottom".</summary>
    void ToggleDock(string side);

    /// <summary>Opens every panel and every dock.</summary>
    void ShowAllWindows();

    /// <summary>Closes every editor tab, pinned ones too.</summary>
    void CloseAllWindows();

    /// <summary>Selects the resource of the tab in front in the Explorer and shows the Explorer.</summary>
    void RevealInExplorer();

    /// <summary>Adds the resource in front to the Favourites of the Quick Access group, or takes it out.</summary>
    void ToggleFavourite();

    /// <summary>Asks for a tag and puts it on the resource in front.</summary>
    void AddTag();

    /// <summary>Go To (Ctrl+T): resources, code, strings, functions, variables, commands, settings pages and open tabs.</summary>
    void GoTo();

    void ShowOnboarding();

    void ShowTour();

    void RunScript();

    void RunCommand();

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

    /// <summary>Runs a code editor command (an id of <see cref="AppCommands.CodeCommands"/>) on the code tab in front.</summary>
    void CodeCommand(string id);

    /// <summary>Runs an image editor command (an id of <see cref="AppCommands.ImageCommands"/>) on the image tab in front.</summary>
    void ImageCommand(string id);
}

public static partial class AppCommands
{
    public const string WorkspaceOpen = "workspaceOpen";

    /// <summary>Set while the tab in front is a sprite or a room, which the commands of the Sprite and Room menus work on.</summary>
    public const string SpriteTab = "spriteTab", RoomTab = "roomTab", ProjectOn = "projectOn", ProjectOff = "projectOff";

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
        Add("edit.editImage", null, shell.EditImage, SpriteTab);
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
        Add("edit.rename", "F2", shell.Rename, WorkspaceOpen);
        Add("edit.delete", null, shell.DeleteResource, WorkspaceOpen);
        Add("edit.duplicate", null, shell.DuplicateResource, WorkspaceOpen);
        Add("edit.copyToOtherFile", null, shell.CopyToOtherFile, WorkspaceOpen);
        Add("edit.reveal", null, shell.RevealInExplorer, WorkspaceOpen);
        Add("edit.toggleFavourite", null, shell.ToggleFavourite, WorkspaceOpen);
        Add("edit.addTag", null, shell.AddTag, WorkspaceOpen);
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
        Add("edit.replaceInCode", "Ctrl+Shift+H", shell.ShowReplace, WorkspaceOpen);
        Add("edit.findUsages", "Shift+F12", shell.FindUsages, WorkspaceOpen);
        Add("edit.findUses", null, shell.FindUses, WorkspaceOpen);
    }
}
