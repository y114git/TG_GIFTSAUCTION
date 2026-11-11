using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using G3MStudio.Commands;
using G3MStudio.Core.Commands;
using G3MStudio.Core.Navigation;
using G3MStudio.Localization;

namespace G3MStudio.Tests;

internal sealed class FakeShell : IShell
{
    public void ShowPalette() { }
    public void ToggleFullscreen() { }
    public void FocusRegion(int direction) => RegionSteps.Add(direction);
    public List<int> RegionSteps { get; } = [];

    public void EditImage() { }
    public void ReplaceAudio() { }
    public void EditAudio() { }
    public void ExportAudio() { }
    public void ExportPage() { }
    public void ReplacePage() { }
    public void ShowRoomOrder() { }

    public void ShowTextureGroups() { }

    public void ShowAudioGroups() { }
    public void ShowHexView() { }
    public void NewResource(string type) { }
    public void DuplicateResource() { }

    public void CopyToOtherFile() { }

    public void MoveResourceUp() { }

    public void MoveResourceDown() { }
    public void DeleteResource() { }
    public void Zoom(int step) { }
    public void Exit() { }
    public void NewDataFile() { }

    public void OpenDataFile() { }
    public void CloseWorkspace() { }
    public void Save() { }
    public void SaveAs() { }
    public void SaveCopy() { }
    public void ShowAbout() { }
    public void OpenDocumentation() { }
    public void SelectAll() { }
    public void Find() { }
    public void ShowSearch() { }
    public void FindUsages() { }
    public void FindUses() { }
    public void ExportSelection() { }
    public void ExportAll() { }
    public void Transfer() { }
    public void ImportResources() { }

    public void ImportPackage() { }
    public void ImportFiles() { }
    public void RevertToSaved() { }
    public void CheckForUpdates() { }
    public void RunGame() { }
    public void StopGame() { }
    public void RestartGame() { }
    public void RunWithRuntime() { }
    public void WriteOffsetMap() { }
    public void ExportChunk() { }
    public void ImportChunk() { }

    public void WriteDebugData() { }
    public void ReplaceFrame() { }
    public void DuplicateFrame() { }
    public void RecalculateMask() { }
    public void ImportMask() { }
    public void ExportMask() { }

    public void ExportGif() { }
    public void ImportSheet() { }
    public void ConvertImageToTiles() { }

    public void ExportAllAudio() { }
    public void RemoveFrame() { }
    public void MergePatches() { }

    public void CompareDataFiles() { }

    public void ExportModPack() { }
    public void RememberModBase() { }
    public void ClearModBase() { }
    public void ResetPrompts() { }
    public void MigrateRunOptions() { }

    public void ApplyModPack() { }
    public void RepackTextures() { }
    public void SaveRoomPng() { }
    public void PlayRoom() { }
    public void ExportTiles() { }
    public void ImportTiles() { }
    public void ShowCreationOrder() { }
    public void ToggleAutoTile() { }
    public void RetileSelection() { }

    public void ShowCreationCode() { }
    public void ExportMod() { }
    public void CreatePatch() { }
    public void ApplyPatch() { }
    public void ValidatePatch() { }
    public void ApplyMod() { }
    public void Undo() { }
    public void Redo() { }
    public void Rename() { }
    public void EnableProject() { }
    public void SyncProject() { }
    public void ProjectOptions() { }
    public void MoveDataFile() { }

    public void ResolveExternalChange() { }
    public void DisableProject() { }
    public void ShowPanel(string id) { }
    public void ShowSettings() { }
    public void ShowPlugins() { }
    public void ShowStrings() { }

    public void ResetLayout() { }

    public void ShowLayouts() { }

    public void UseLayoutPreset(string preset) { }

    public void SaveLayout() { }

    public void ToggleDocks() { }

    public void ToggleDock(string side) { }

    public void ShowAllWindows() { }

    public void CloseAllWindows() { }

    public void GoTo() { }

    public void RevealInExplorer() { }

    public void ToggleFavourite() { }

    public void AddTag() { }
    public void NewFolder() { }
    public void MoveToFolder() { }
    public void ExportPackage() { }
    public void AddToCollection() { }
    public void RemoveFromGroup() { }
    public void ColourLabel() { }
    public void BulkRename() { }
    public void ToggleFolderView() { }
    public void SelectByType() { }
    public void SelectContents() { }
    public void MoveToTextureGroup() { }
    public void MoveToAudioGroup() { }
    public void OpenFolder() { }
    public void FolderUp() { }

    public void ShowOnboarding() { }

    public void ShowTour() { }
    public void RunScript() { }

    public void RunCommand() { }
    public void QuickOpen() { }
    public void CloseTab() { }
    public void CloseAllTabs() { }
    public void CloseOtherTabs() { }
    public void ReopenTab() { }
    public void OpenDataFolder() { }
    public void CopyName() { }
    public void ShowDisassembly() { }
    public void ShowReplace() { }
    public void ImportCodeFolder() { }
    public void ImportCodeFiles() { }
    public void FindUnreferenced() { }
    public void FindChildren() { }
    public void NextTab() { }
    public void PreviousTab() { }
    public void TabBack() { }
    public void TabForward() { }
    public void PinTab() { }
    public void KeepTabOpen() { }
    public void SplitRight() { }
    public void CodeCommand(string id) { }
    public void ImageCommand(string id) { }
}

public sealed class CommandTests
{
    private static CommandRegistry NewRegistry() => AppCommands.Create(new FakeShell());

    [Fact]
    public void TransferIsTheOnlyMenuEntryForEveryWayOfMovingResourcesAndKeepsEachCommand()
    {
        var registry = NewRegistry();
        var menuIds = AppCommands.Menus.SelectMany(m => m.Ids).ToList();
        Assert.Contains("file.transfer", menuIds);
        foreach (var id in TransferWindow.Modes)
        {
            Assert.NotNull(registry.Find(id));
            Assert.DoesNotContain(id, menuIds);
            Assert.NotEqual("cmd." + id, Loc.Current["cmd." + id]);
        }
    }

    private static RawInputModifiers Raw(KeyModifiers m) =>
        (m.HasFlag(KeyModifiers.Control) ? RawInputModifiers.Control : 0) | (m.HasFlag(KeyModifiers.Shift) ? RawInputModifiers.Shift : 0) |
        (m.HasFlag(KeyModifiers.Alt) ? RawInputModifiers.Alt : 0) | (m.HasFlag(KeyModifiers.Meta) ? RawInputModifiers.Meta : 0);

    private static MainWindow OpenWindow(List<string> executed)
    {
        Loc.Current.SetLanguage("en");
        var window = new MainWindow();
        window.Registry.Executed += executed.Add;
        window.Registry.SetContext(AppCommands.WorkspaceOpen, true);
        window.Show();
        // After Show: the window sets these itself from the tabs it holds, and a fresh window has none.
        window.Registry.SetContext(AppCommands.ResourceSelected, true);
        foreach (var type in Core.Workspace.ResourceListCommand.Creatable) window.Registry.SetContext(AppCommands.CanNew(type), true);
        window.Registry.SetContext(AppCommands.SpriteTab, true);
        window.Registry.SetContext(AppCommands.PictureTab, true);
        window.Registry.SetContext(AppCommands.RoomTab, true);
        window.Registry.SetContext(AppCommands.CodeTab, true);
        window.Registry.SetContext(AppCommands.ImageTab, true);
        window.Registry.SetContext(AppCommands.ProjectOn, true);
        window.Registry.SetContext(AppCommands.ProjectOff, true);
        return window;
    }

    private static void CloseAll(MainWindow window)
    {
        window.GoToBox?.Close();
        Loc.Current.SetLanguage("en");
        window.Close();
    }

    [Fact]
    public void ImageEditorCommandsCarryTheirMenuPathInThePalette()
    {
        var registry = NewRegistry();
        registry.SetContext(AppCommands.ImageTab, true);
        var entries = MainWindow.CommandEntries(registry, Loc.Current).ToList();
        Assert.Equal("Image > Selection > Copy", entries.Single(e => e.Key == "img.copy").Name);
        Assert.Equal("Image > Tools > Pencil", entries.Single(e => e.Key == "img.tool.pencil").Name);
        Assert.Equal(entries.Count, entries.Select(e => e.Name).Distinct().Count());
    }

    // The command palette: Go To over the enabled commands, typed with the command prefix and the title.
    private static GoToWindow PaletteFor(MainWindow window, string title)
    {
        var entries = MainWindow.CommandEntries(window.Registry, Loc.Current).ToList();
        var palette = new GoToWindow(text => GoToSearch.Run(entries, text, []), _ => Task.FromResult(""));
        palette.Chosen += (entry, _) => window.Registry.Execute(entry.Key);
        palette.Text = ">" + title;
        Dispatcher.UIThread.RunJobs();
        return palette;
    }

    // Every item below the menu bar, including the ones inside a submenu (Language, Panels).
    private static IEnumerable<MenuItem> MenuItems(MainWindow window) =>
        window.GetVisualDescendants().OfType<Menu>().Single().Items.OfType<MenuItem>().SelectMany(Below);

    private static IEnumerable<MenuItem> Below(MenuItem menu) => menu.Items.OfType<MenuItem>().SelectMany(i => i.Command is null ? Below(i) : [i]);

    [Fact]
    public void EveryCommandHasAMenuEntryAndAUniqueShortcut()
    {
        var registry = NewRegistry();
        var all = registry.All.ToList();
        var inMenus = AppCommands.Menus.SelectMany(m => m.Ids).Where(id => id != "-").ToList();
        Assert.Equal(all.Select(c => c.Id).Where(id => !AppCommands.TransferModes.Contains(id)).Order(), inMenus.Order()); // the transfer modes sit behind the one Transfer entry
        var shortcuts = all.SelectMany(c => c.Gestures).Select(g => Shortcuts.Parse(g)!.ToString()).ToList();
        Assert.Equal(shortcuts.Count, shortcuts.Distinct().Count());
    }

    [Fact]
    public void ABadShortcutTextParsesToNullInsteadOfThrowing()
    {
        Assert.Null(Shortcuts.Parse("Ctrl+Foo"));
        Assert.Null(Shortcuts.Parse("Ctrl+Foo")); // cached
        Assert.NotNull(Shortcuts.Parse("Ctrl+K"));
    }

    [Fact]
    public async Task EveryCommandRunsFromMenuPaletteAndShortcut()
    {
        await HeadlessApp.Run(() =>
        {
            foreach (var id in NewRegistry().All.Select(c => c.Id).ToList())
            {
                // Menu: the item bound to the command, localized, enabled, runs it.
                var executed = new List<string>();
                var window = OpenWindow(executed);
                try
                {
                    if (!AppCommands.TransferModes.Contains(id)) // a transfer mode has no menu entry of its own
                    {
                        // Two menus may share a title (Edit > Select All, Image > Selection > Select All): one of the items runs this command.
                        var items = MenuItems(window).Where(m => m.Command is RegistryCommand && (string?)m.Header == Loc.Current[AppCommands.TitleKey(id)]).ToList();
                        Assert.NotEmpty(items);
                        Assert.Contains(items, item =>
                        {
                            executed.Clear();
                            Assert.True(item.Command!.CanExecute(null), id);
                            item.Command.Execute(null);
                            return executed.SequenceEqual([id]);
                        });
                    }
                }
                finally { CloseAll(window); }

                // Palette: pick the row of the command and press Enter.
                executed = [];
                window = OpenWindow(executed);
                try
                {
                    var palette = PaletteFor(window, Loc.Current[AppCommands.TitleKey(id)]);
                    palette.Show();
                    var results = palette.GetVisualDescendants().OfType<ListBox>().Single();
                    palette.GetVisualDescendants().OfType<TextBox>().Single().Focus();
                    results.SelectedItem = palette.Items.Single(i => i.Key == id);
                    palette.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
                    Assert.Equal([id], executed);
                }
                finally { CloseAll(window); }

                // Shortcut: the command's own gesture on the main window, if it has one.
                var shortcut = Shortcuts.Parse(NewRegistry().Get(id).Shortcut);
                if (shortcut is null) continue;
                executed = [];
                window = OpenWindow(executed);
                try
                {
                    window.KeyPress(shortcut.Key, Raw(shortcut.KeyModifiers), PhysicalKey.None, null);
                    Assert.Equal([id], executed);
                }
                finally { CloseAll(window); }
            }
        });
    }

    [Fact]
    public async Task CommandsNeedingAWorkspaceAreDisabledAndHiddenFromThePaletteWithoutOne()
    {
        await HeadlessApp.Run(() =>
        {
            var executed = new List<string>();
            var window = OpenWindow(executed);
            try
            {
                window.Registry.SetContext(AppCommands.WorkspaceOpen, false);
                Assert.False(window.Registry.Execute("file.save"));
                window.KeyPress(Key.S, RawInputModifiers.Control, PhysicalKey.None, null);
                Assert.Empty(executed);

                var save = MenuItems(window).Single(m => (string?)m.Header == "Save");
                Assert.False(save.Command!.CanExecute(null));
                Assert.DoesNotContain(PaletteFor(window, "Save").Items, i => i.Key == "file.save");
                Assert.Contains(PaletteFor(window, "Open").Items, i => i.Key == "file.open");

                window.Registry.SetContext(AppCommands.WorkspaceOpen, true);
                Assert.True(save.Command.CanExecute(null));

                // The Sprite and Room commands also need their own kind of tab in front.
                window.Registry.SetContext(AppCommands.SpriteTab, false);
                window.Registry.SetContext(AppCommands.PictureTab, false);
                window.Registry.SetContext(AppCommands.RoomTab, false);
                Assert.False(window.Registry.Execute("edit.replaceFrame"));
                Assert.False(window.Registry.Execute("room.play"));
                Assert.Contains(PaletteFor(window, "Save").Items, i => i.Key == "file.save");
            }
            finally { CloseAll(window); }
        });
    }

    [Fact]
    public async Task LanguageCommandRelabelsMenusAndPalette()
    {
        await HeadlessApp.Run(() =>
        {
            var window = OpenWindow([]);
            try
            {
                Loc.Current.SetLanguage("ru");
                Dispatcher.UIThread.RunJobs();
                Assert.Contains(MenuItems(window), m => (string?)m.Header == "Сохранить");
                Assert.Contains(PaletteFor(window, "Сохранить").Items, i => i.Name == "Сохранить");
            }
            finally { CloseAll(window); }
        });
    }

    [Fact]
    public async Task ZoomStepsAreClampedAndReset()
    {
        await HeadlessApp.Run(() =>
        {
            var window = OpenWindow([]);
            try
            {
                window.Zoom(1);
                Assert.Equal(1.1, window.ZoomFactor);
                for (var i = 0; i < 20; i++) window.Zoom(-1);
                Assert.Equal(0.5, window.ZoomFactor);
                window.Zoom(0);
                Assert.Equal(1, window.ZoomFactor);
            }
            finally { CloseAll(window); }
        });
    }
}
