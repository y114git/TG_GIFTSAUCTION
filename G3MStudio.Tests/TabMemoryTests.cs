using System.Reflection;
using Avalonia.Controls;
using Avalonia.Threading;

namespace G3MStudio.Tests;

public sealed class TabMemoryTests
{
    private static int Held(MainWindow window, string field) => ((System.Collections.IDictionary)typeof(MainWindow).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!).Count;

    [Fact]
    public async Task ClosedTabsAreNotKeptAliveByTheWindow()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir, objects: 4, withCode: true);
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            await window.OpenAsync(path);
            Dispatcher.UIThread.RunJobs();
            var tabs = new List<WeakReference>();
            for (var round = 0; round < 5; round++)
            {
                var tab = window.Docking.OpenTab("code", "code:gml_Script_probe", false)!;
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(1, Held(window, "_codeTabs"));
                tabs.Add(new WeakReference(window.Docking.DocumentOf(tab.Id)!.Content is ContentControl { Content: { } editor } ? editor : null));
                window.Docking.CloseTab(tab.Id);
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(0, Held(window, "_codeTabs"));
            }

            Assert.Equal(0, Held(window, "_frameViews"));
            Assert.Equal(0, Held(window, "_roomViews"));
            for (var i = 0; i < 3; i++)
            {
                Dispatcher.UIThread.RunJobs();
                await Task.Delay(50);
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }

            // The runtime may still hold the one or two newest views (dock and dispatcher bookkeeping); a leak keeps all of them.
            Assert.All(tabs.Take(3), t => Assert.False(t.IsAlive, "a closed code tab is still reachable"));
            window.Workspaces.Close();
            window.Close();
        });
    }
}
