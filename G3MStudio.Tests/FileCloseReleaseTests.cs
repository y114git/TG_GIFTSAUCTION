using Avalonia.Headless;
using Avalonia.Threading;

namespace G3MStudio.Tests;

#pragma warning disable CA2000
/// <summary>Closing a file, with tabs open or after they were closed, must leave nothing that still reaches the closed workspace.</summary>
public sealed class FileCloseReleaseTests
{
    private static async Task Settle()
    {
        for (var i = 0; i < 4; i++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(100);
            AvaloniaHeadlessPlatform.ForceRenderTimerTick(3);
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static async Task<WeakReference> OpenWork(MainWindow window, string path, bool closeTabsFirst)
    {
        await window.OpenAsync(path);
        Dispatcher.UIThread.RunJobs();
        var workspace = window.Workspaces.Current;
        window.Docking.OpenTab("code", "code:gml_Script_probe", false);
        window.Docking.OpenTab("resource", "objects:obj_0", false);
        window.Docking.OpenTab("resource", "scripts:scr_0", false);
        Dispatcher.UIThread.RunJobs();
        if (closeTabsFirst) foreach (var tab in window.Docking.Tabs.Tabs.ToList()) window.Docking.CloseTab(tab.Id);
        window.Workspaces.Close();
        return new WeakReference(workspace);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ClosedWorkspaceIsUnreachableAfterTheViewsSettle(bool closeTabsFirst)
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir, objects: 20, withCode: true);
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            for (var round = 0; round < 3; round++)
            {
                var weak = await OpenWork(window, path, closeTabsFirst);
                await Settle();
                Assert.False(weak.IsAlive, $"round {round}: the closed workspace is still reachable");
            }

            window.Close();
        });
    }
}
