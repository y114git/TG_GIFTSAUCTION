using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using G3MStudio.Commands;
using G3MStudio.Core.Commands;
using G3MStudio.Localization;

namespace G3MStudio.Tests;

internal sealed class FakeShell : IShell
{
    public void ShowPalette() { }
    public void ToggleFullscreen() { }
    public void Zoom(int step) { }
    public void Exit() { }
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
    public void ExportSelection() { }
    public void ExportAll() { }
    public void ImportResources() { }
    public void RevertToSaved() { }
    public void CheckForUpdates() { }
    public void RunGame() { }
    public void StopGame() { }
    public void ReplaceFrame() { }
    public void DuplicateFrame() { }
    public void RecalculateMask() { }
    public void ImportMask() { }
    public void ExportMask() { }
    public void RemoveFrame() { }
    public void MergePatches() { }
    public void CompareFiles() { }
    public void RepackTextures() { }
    public void SaveRoomPng() { }
    public void PlayRoom() { }
    public void ExportTiles() { }
    public void ImportTiles() { }
    public void ShowCreationOrder() { }

    public void ShowCreationCode() { }
    public void ExportMod() { }
    public void CreatePatch() { }
    public void ApplyPatch() { }
    public void ValidatePatch() { }
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

    public void ShowOnboarding() { }
    public void RunScript() { }
    public void QuickOpen() { }
    public void CloseTab() { }
    public void CloseAllTabs() { }
    public void PinTab() { }
    public void KeepTabOpen() { }
    public void SplitRight() { }
}

public sealed class CommandTests
{
    private static CommandRegistry NewRegistry() => AppCommands.Create(new FakeShell(), Loc.Current);

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
        window.Registry.SetContext(AppCommands.SpriteTab, true);
        window.Registry.SetContext(AppCommands.RoomTab, true);
        return window;
    }

    private static void CloseAll(MainWindow window)
    {
        window.Palette?.Close();
        Loc.Current.SetLanguage("en");
        window.Close();
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
        Assert.Equal(all.Select(c => c.Id).Order(), inMenus.Order());
        var shortcuts = all.Where(c => c.Shortcut is not null).Select(c => Shortcuts.Parse(c.Shortcut)!.ToString()).ToList();
        Assert.Equal(shortcuts.Count, shortcuts.Distinct().Count());
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
                    var item = MenuItems(window).Single(m => m.Command is RegistryCommand && (string?)m.Header == Loc.Current[AppCommands.TitleKey(id)]);
                    Assert.True(item.Command!.CanExecute(null), id);
                    item.Command.Execute(null);
                    Assert.Equal([id], executed);
                }
                finally { CloseAll(window); }

                // Palette: pick the row of the command and press Enter.
                executed = [];
                window = OpenWindow(executed);
                try
                {
                    var palette = new PaletteWindow(window.Registry, Loc.Current);
                    palette.Show();
                    var results = palette.FindControl<ListBox>("Results")!;
                    palette.FindControl<TextBox>("Input")!.Focus();
                    results.SelectedItem = palette.Items.Single(i => i.Id == id);
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
                var palette = new PaletteWindow(window.Registry, Loc.Current);
                Assert.DoesNotContain(palette.Items, i => i.Id == "file.save");
                Assert.Contains(palette.Items, i => i.Id == "file.open");

                window.Registry.SetContext(AppCommands.WorkspaceOpen, true);
                Assert.True(save.Command.CanExecute(null));

                // The Sprite and Room commands also need their own kind of tab in front.
                window.Registry.SetContext(AppCommands.SpriteTab, false);
                window.Registry.SetContext(AppCommands.RoomTab, false);
                Assert.False(window.Registry.Execute("edit.replaceFrame"));
                Assert.False(window.Registry.Execute("room.play"));
                Assert.Contains(new PaletteWindow(window.Registry, Loc.Current).Items, i => i.Id == "file.save");
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
                window.Registry.Execute("view.language.ru");
                Dispatcher.UIThread.RunJobs();
                Assert.Contains(MenuItems(window), m => (string?)m.Header == "Сохранить");
                Assert.Contains(new PaletteWindow(window.Registry, Loc.Current).Items, i => i.Title == "Сохранить");
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
