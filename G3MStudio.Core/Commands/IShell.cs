
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
