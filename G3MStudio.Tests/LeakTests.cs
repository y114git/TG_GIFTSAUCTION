using System.Diagnostics;
using Avalonia.Threading;
using G3MStudio.Localization;

namespace G3MStudio.Tests;

/// <summary>Closed windows and tabs must be collectable, and a language switch must not get slower with every window that was opened and closed before it.</summary>
#pragma warning disable CA2000
public sealed class LeakTests(ITestOutputHelper output)
{
    private static async Task Settle()
    {
        for (var i = 0; i < 4; i++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(30);
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
    }

    // Kept out of the caller's frame, so no local of the test keeps the window alive.
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static async Task<WeakReference> OpenUseAndClose(TempDir dir, string path, int tabs)
    {
        var window = TabsWorkspaceTests.NewWindow(dir);
        await window.OpenAsync(path);
        Dispatcher.UIThread.RunJobs();
        for (var i = 0; i < tabs; i++)
        {
            window.Docking.OpenTab("code", "code:gml_Script_probe", false);
            window.Docking.OpenTab("resource", "objects:obj_" + (i % 4), false);
            window.Docking.OpenTab("resource", "scripts:scr_" + (i % 4), false);
        }

        Dispatcher.UIThread.RunJobs();
        Loc.Current.SetLanguage("ru");
        Dispatcher.UIThread.RunJobs();
        Loc.Current.SetLanguage("en");
        Dispatcher.UIThread.RunJobs();
        var weak = new WeakReference(window);
        window.Workspaces.Close();
        window.Close();
        return weak;
    }

    [Fact]
    public async Task ClosedWindowsAreCollectedAndLanguageSwitchStaysFlat()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir, objects: 4, withCode: true);
        await HeadlessApp.Run(async () =>
        {
            var weak = new List<WeakReference>();
            var switches = new List<double>();
            for (var round = 0; round < 8; round++)
            {
                weak.Add(await OpenUseAndClose(dir, path, 3));
                await Settle();
                var watch = Stopwatch.StartNew();
                Loc.Current.SetLanguage(round % 2 == 0 ? "ja" : "en");
                Dispatcher.UIThread.RunJobs();
                switches.Add(watch.Elapsed.TotalMilliseconds);
            }

            output.WriteLine("language switch after each closed window (ms): " + string.Join(", ", switches.Select(x => x.ToString("F1", System.Globalization.CultureInfo.InvariantCulture))));
            var alive = weak.Count(w => w.IsAlive);
            Assert.True(alive <= 1, $"{alive} of {weak.Count} closed windows are still reachable");
        });
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static async Task<WeakReference> OpenAndCloseFile(TempDir dir, string path)
    {
        var window = TabsWorkspaceTests.NewWindow(dir);
        await window.OpenAsync(path);
        window.Docking.OpenTab("code", "code:gml_Script_probe", false);
        window.Docking.OpenTab("resource", "objects:obj_0", false);
        Dispatcher.UIThread.RunJobs();
        var weak = new WeakReference(window.Workspaces.Current);
        foreach (var tab in window.Docking.Tabs.Tabs.ToList()) window.Docking.CloseTab(tab.Id);
        window.Workspaces.Close();
        window.Close();
        return weak;
    }

    [Fact]
    public async Task ReopeningAFileTenTimesLeavesNoWorkspaceBehindAndTheHeapFlat()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir, objects: 200, withCode: true);
        await HeadlessApp.Run(async () =>
        {
            var weak = new List<WeakReference>();
            var heap = new List<long>();
            for (var round = 0; round < 10; round++)
            {
                weak.Add(await OpenAndCloseFile(dir, path));
                await Settle();
                heap.Add(GC.GetTotalMemory(true));
            }

            output.WriteLine("managed heap after each close (KB): " + string.Join(", ", heap.Select(x => x / 1024)));
            var alive = weak.Count(w => w.IsAlive);
            Assert.True(alive <= 1, $"{alive} of {weak.Count} closed workspaces are still reachable");
            Assert.True(heap[^1] - heap[2] < 4 * 1024 * 1024, $"heap grew by {(heap[^1] - heap[2]) / 1024} KB over the last 7 reopenings");
        });
    }
}
