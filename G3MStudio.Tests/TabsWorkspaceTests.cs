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
