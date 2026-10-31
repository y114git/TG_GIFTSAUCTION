using Avalonia.VisualTree;
using Avalonia.Threading;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

namespace G3MStudio.Tests;

public sealed class TabsWorkspaceTests
{
    internal static MainWindow NewWindow(TempDir dir)
    {
        Loc.Current.SetLanguage("en");
        var userData = new UserData(dir.File("user"));
        var window = new MainWindow(new WorkspaceService(userData, RecentFiles.Load(userData)));
        window.Show();
        return window;
    }

    private static ExplorerRow Row(MainWindow window, string type, string name)
    {
        if (window.Explorer.Model.Rows.Any(r => r is { IsItem: false, Expanded: false } && r.Key == type)) window.Explorer.Model.Toggle(type);
        return window.Explorer.Model.Rows.Single(r => r.IsItem && r.Key == type && r.Text == name);
    }

    private static string[] Titles(MainWindow window) => [.. window.Docking.Documents().Select(d => d.Title)];

    [Fact]
    public async Task AG3mstudioLinkOpensItsDataFileAndABrokenOneOnlyNotifies()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir, objects: 2);
        await HeadlessApp.Run(async () =>
        {
            var window = NewWindow(dir);
            try
            {
                window.OpenLink("g3mstudio://nonsense");
                Assert.Null(window.Workspaces.Current);
                window.OpenStartupFile([Core.Links.StudioLink.ForFile(path)]);
                for (var i = 0; i < 100 && window.Workspaces.Current is null; i++)
                {
                    await Task.Delay(50);
                    Dispatcher.UIThread.RunJobs();
                }

                Assert.Equal(Path.GetFullPath(path), window.Workspaces.Current?.Path);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public void TheOperatingSystemLanguageIsMatchedToAnAvailableOneAndEnglishOtherwise()
    {
        string[] available = ["en", "ru", "es", "ja", "ko", "zh_cn", "zh_tw"];
        string Detect(string culture) => Core.Localization.Localizer.Detect(new System.Globalization.CultureInfo(culture), available);
        Assert.Equal("ru", Detect("ru-RU"));
        Assert.Equal("ja", Detect("ja-JP"));
        Assert.Equal("zh_cn", Detect("zh-CN"));
        Assert.Equal("zh_tw", Detect("zh-TW"));
        Assert.Equal("zh_tw", Detect("zh-Hant"));
        Assert.Equal("es", Detect("es-MX"));
        Assert.Equal("en", Detect("fr-FR"));
    }

    [Fact]
    public async Task TheWelcomeWindowAppearsOnceSwitchesTheLanguageAndIsSkippedByTheEnvironmentVariable()
    {
        using var dir = new TempDir();
        using var other = new TempDir();
        await HeadlessApp.Run(() =>
        {
            Environment.SetEnvironmentVariable("G3MSTUDIO_NO_ONBOARDING", null);
            var window = NewWindow(dir);
            try
            {
                window.RunFirstLaunch();
                var welcome = window.OwnedWindows.OfType<OnboardingWindow>().Single();
                var list = welcome.GetVisualDescendants().OfType<Avalonia.Controls.ListBox>().Single(l => l.Name == "OnboardingLanguages");
                Assert.Equal(7, list.ItemCount);
                list.SelectedIndex = 3; // languages are listed by code: en, es, ja, ko, ru, zh_cn, zh_tw
                Assert.Equal("ko", window.Workspaces.Settings.Current.Language);
                Assert.Equal("ko", Loc.Current.Language);
                Assert.False(window.Workspaces.Settings.Current.OnboardingDone);
                welcome.Close();
                Assert.True(window.Workspaces.Settings.Current.OnboardingDone);

                window.RunFirstLaunch(); // answered once: it does not come back
                Assert.Empty(window.OwnedWindows.OfType<OnboardingWindow>());
                Assert.True(window.Registry.Execute("help.gettingStarted")); // but Help > Getting Started opens it again
                Assert.Single(window.OwnedWindows.OfType<OnboardingWindow>());
                foreach (var open in window.OwnedWindows.ToList()) open.Close();
            }
            finally
            {
                window.Close();
                Loc.Current.SetLanguage("en");
            }

            var skipped = NewWindow(other);
            try
            {
                Environment.SetEnvironmentVariable("G3MSTUDIO_NO_ONBOARDING", "1");
                skipped.RunFirstLaunch();
                Assert.Empty(skipped.OwnedWindows.OfType<OnboardingWindow>());
            }
            finally
            {
                Environment.SetEnvironmentVariable("G3MSTUDIO_NO_ONBOARDING", null);
                skipped.Close();
            }

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task TheWindowMenuResetsTheLayoutAndSavesLoadsAndDeletesNamedLayouts()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir, objects: 3);
        await HeadlessApp.Run(async () =>
        {
            var window = NewWindow(dir);
            try
            {
                await window.OpenAsync(path);
                window.Explorer.RequestOpen(Row(window, "objects", "obj_0"), true);
                Dispatcher.UIThread.RunJobs();
                window.Docking.Factory.CloseDockable(window.Docking.Find<Dock.Model.Core.IDockable>("Search")!);
                Assert.Null(window.Docking.Find<Dock.Model.Core.IDockable>("Search"));
                Assert.True(window.Registry.Execute("window.layout.reset"));
                Assert.NotNull(window.Docking.Find<Dock.Model.Core.IDockable>("Search")); // the panel is back
                Assert.Equal(["obj_0"], Titles(window)); // the editor stayed open

                Assert.True(window.Registry.Execute("window.layout.saved"));
                var layouts = window.OwnedWindows.OfType<LayoutsWindow>().Single();
                T Part<T>(string name) where T : Avalonia.Controls.Control => layouts.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);
                Part<Avalonia.Controls.TextBox>("LayoutName").Text = "mine";
                Part<Avalonia.Controls.Button>("LayoutSave").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
                Assert.Equal(["mine"], window.Workspaces.Settings.Current.SavedLayouts.Keys);

                window.Docking.Factory.CloseDockable(window.Docking.Find<Dock.Model.Core.IDockable>("Search")!);
                Part<Avalonia.Controls.ListBox>("SavedLayouts").SelectedIndex = 0;
                Part<Avalonia.Controls.Button>("LayoutLoad").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
                Assert.NotNull(window.Docking.Find<Dock.Model.Core.IDockable>("Search"));
                Assert.Equal(["obj_0"], Titles(window));

                Part<Avalonia.Controls.Button>("LayoutDelete").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
                Assert.Empty(window.Workspaces.Settings.Current.SavedLayouts);
                layouts.Close();
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public async Task ReopeningADataFileRestoresTabsLayoutAndSelection()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir, objects: 3);
        await HeadlessApp.Run(async () =>
        {
            var window = NewWindow(dir);
            try
            {
                await window.OpenAsync(path);
                window.Explorer.RequestOpen(Row(window, "objects", "obj_0"), true);
                window.Explorer.RequestOpen(Row(window, "objects", "obj_1"), true);
                window.Explorer.RequestOpen(Row(window, "scripts", "scr_2"), false);
                Assert.Equal(["obj_1", "scr_2"], Titles(window));
                var pinned = window.Docking.Tabs.Tabs.Single(t => t.Key == "scripts:scr_2");
                window.Docking.SetPinned(pinned.Id, true);
                window.Explorer.Select("objects:obj_1");
                window.Docking.SplitTab(window.Docking.Tabs.Tabs.Single(t => t.Key == "objects:obj_1").Id, Dock.Model.Core.DockOperation.Right);
                Dispatcher.UIThread.RunJobs();
            }
            finally
            {
                window.Close();
            }

            var again = NewWindow(dir);
            try
            {
                await again.OpenAsync(path);
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(2, again.Docking.Tabs.Tabs.Count);
                Assert.True(again.Docking.Tabs.Tabs.Single(t => t.Key == "scripts:scr_2").Pinned);
                Assert.True(again.Docking.Tabs.Tabs.Single(t => t.Key == "objects:obj_1").Preview);
                Assert.Equal(2, again.Docking.Groups().Count());
                Assert.Equal("objects:obj_1", again.Explorer.SelectedKey);
            }
            finally
            {
                again.Close();
            }
        });
    }

    [Fact]
    public async Task ATabForAResourceThatIsGoneIsDroppedOnReopen()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir, objects: 3);
        await HeadlessApp.Run(async () =>
        {
            var window = NewWindow(dir);
            try
            {
                await window.OpenAsync(path);
                window.Explorer.RequestOpen(Row(window, "objects", "obj_2"), false);
                window.Explorer.RequestOpen(Row(window, "objects", "obj_0"), false);
            }
            finally
            {
                window.Close();
            }

            using (var data = TestData.Create(objects: 2)) TestData.Write(path, data);
            var again = NewWindow(dir);
            try
            {
                await again.OpenAsync(path);
                Assert.Equal(["obj_0"], Titles(again));
            }
            finally
            {
                again.Close();
            }
        });
    }
}
