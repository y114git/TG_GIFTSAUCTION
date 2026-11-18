#nullable disable
#pragma warning disable CA2000, CA1849, IDE0305
using System.Diagnostics;
using System.Globalization;
using Avalonia.Threading;
using G3MStudio.Core.Settings;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;
using G3MStudio.Theme;

namespace G3MStudio.Tests;

/// <summary>
/// Generated files of three sizes (no game data): start-up, open, settings page, language switch with ten tabs open, animations on and off, and the memory
/// after 30 tabs were opened and closed. The numbers go to the test output; the budgets are loose, to catch a regression and not the noise of a busy machine.
/// </summary>
public sealed class StabilityBenchmarkTests(ITestOutputHelper output)
{
    private static readonly string[] Levels = [MotionPolicy.Off, MotionPolicy.Full, MotionPolicy.Off, MotionPolicy.Full];
    private static readonly string[] Languages = ["ru", "ja", "en", "zh_cn", "en"];

    private static string Iso(double ms) => ms.ToString("F1", CultureInfo.InvariantCulture);

    public static TheoryData<string, int> Sizes => new() { { "small", 200 }, { "medium", 5_000 }, { "big", 28_000 } };

    private static double Time(Action action)
    {
        var watch = Stopwatch.StartNew();
        action();
        Dispatcher.UIThread.RunJobs();
        return watch.Elapsed.TotalMilliseconds;
    }

    [Theory]
    [MemberData(nameof(Sizes))]
    public async Task StartOpenSettingsLanguageAnimationsAndTabMemory(string label, int objects)
    {
        using var dir = new TempDir();
        using (var data = TestData.Create(objects)) TestData.Write(dir.File("data.win"), data);
        await HeadlessApp.Run(async () =>
        {
            Loc.Current.SetLanguage("en");
            var userData = new UserData(dir.File("user"));
            var watch = Stopwatch.StartNew();
            var window = new MainWindow(new WorkspaceService(userData, RecentFiles.Load(userData)));
            window.Show();
            Dispatcher.UIThread.RunJobs();
            output.WriteLine($"{label} ({objects} objects): window start-up {Iso(watch.Elapsed.TotalMilliseconds)} ms");
            watch.Restart();
            await window.OpenAsync(dir.File("data.win"));
            Dispatcher.UIThread.RunJobs();
            var open = watch.Elapsed.TotalMilliseconds;
            output.WriteLine($"{label}: open file, explorer and layout {Iso(open)} ms, {window.Explorer.Model.Total} entries");
            Budget.AssertUnder(open, 20_000, label + " open", output);

            var settings = Time(() => window.ShowSettings());
            output.WriteLine($"{label}: settings page open {Iso(settings)} ms");
            Budget.AssertUnder(settings, 1_500, label + " settings open", output);
            window.ShowSettings(1);
            Dispatcher.UIThread.RunJobs();
            window.Docking.OpenTab("resource", ResourceTypes.KeyOf("objects", "obj_0"), false);

            for (var i = 0; i < 10; i++) window.Docking.OpenTab("resource", ResourceTypes.KeyOf(i % 2 == 0 ? "objects" : "scripts", (i % 2 == 0 ? "obj_" : "scr_") + i), false);
            Dispatcher.UIThread.RunJobs();
            var switches = new List<double>();
            foreach (var language in Languages) switches.Add(Time(() => Loc.Current.SetLanguage(language)));
            output.WriteLine($"{label}: language switch with 10 tabs and the settings page open, ms: {string.Join(" ", switches.Select(Iso))}");
            Budget.AssertUnder(switches.Max(), 2_500, label + " language switch", output);

            foreach (var level in Levels)
            {
                Motion.TimedInHeadless = level != MotionPolicy.Off;
                Motion.SetLevel(level);
                var ids = window.Docking.Tabs.Tabs.Select(t => t.Id).ToList();
                var times = ids.Select(_ => Time(() => window.Docking.CycleTab(1))).ToList();
                output.WriteLine($"{label}: animations {level}, activate each of {ids.Count} tabs, mean {Iso(times.Average())} ms, max {Iso(times.Max())} ms");
            }

            Motion.TimedInHeadless = false;
            Motion.SetLevel(MotionPolicy.Medium);
            foreach (var tab in window.Docking.Tabs.Tabs.ToList()) window.Docking.CloseTab(tab.Id);
            var heap = new List<long>();
            for (var round = 0; round < 30; round++)
            {
                var tab = window.Docking.OpenTab("resource", ResourceTypes.KeyOf("objects", "obj_" + round), false);
                Dispatcher.UIThread.RunJobs();
                if (tab is not null) window.Docking.CloseTab(tab.Id);
                Dispatcher.UIThread.RunJobs();
                if (round % 10 == 9)
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();
                    heap.Add(GC.GetTotalMemory(true) / 1024);
                }
            }

            output.WriteLine($"{label}: heap after 10, 20 and 30 opened and closed tabs, KB: {string.Join(" ", heap)}");
            Assert.True(heap[^1] - heap[0] < 8_192, $"{label}: the heap grew by {heap[^1] - heap[0]} KB over 20 more tabs");
            window.Workspaces.Close();
            window.Close();
        });
    }
}
