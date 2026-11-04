#nullable disable
#pragma warning disable CA2025, CA2000, CA1849, IDE0305, CA1859, IDE0370, IDE0060, IDE0028
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

namespace G3MStudio.Tests;

/// <summary>
/// Frame and action timings of the main window on a big data file, in the headless session with real drawing. Runs only when G3MSTUDIO_PERF is set;
/// G3MSTUDIO_PERF_FILES lists data files separated by semicolons and G3MSTUDIO_PERF_SYNTHETIC gives the entry count of a generated file.
/// Every step ends with a rendered frame, so the numbers are the cost until the picture is ready.
/// </summary>
public sealed class PerfRealUiTests(ITestOutputHelper output)
{
    private static bool Enabled => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("G3MSTUDIO_PERF"));

    private static readonly List<double> Frames = [];

    private static double LastGcMs { get; set; }

    private static string LastGcs { get; set; } = "";

    /// <summary>Runs the action and the queued jobs (layout included) and returns that time, the work of the UI thread; the time of the rendered frame is kept for <see cref="Report"/>.</summary>
    private static double Step(Window window, Action action)
    {
        var pause = GC.GetTotalPauseDuration();
        var (g0, g1, g2) = (GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2));
        var watch = Stopwatch.StartNew();
        action();
        Dispatcher.UIThread.RunJobs();
        var ui = watch.Elapsed.TotalMilliseconds;
        LastGcMs = (GC.GetTotalPauseDuration() - pause).TotalMilliseconds;
        var info = GC.GetGCMemoryInfo(GCKind.Any);
        LastGcs = $"{GC.CollectionCount(0) - g0}/{GC.CollectionCount(1) - g1}/{GC.CollectionCount(2) - g2}, last GC gen {info.Generation} promoted {info.PromotedBytes / 1024} KB heap {info.HeapSizeBytes / 1048576} MB gen0 before {info.GenerationInfo[0].SizeBeforeBytes / 1024} KB, server {System.Runtime.GCSettings.IsServerGC} concurrent {AppContext.GetData("System.GC.Concurrent")}";
        // A render tick without reading the picture back: the read-back copies the whole window (5 MB) each time and its garbage skews the timings.
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Frames.Add(watch.Elapsed.TotalMilliseconds - ui);
        return ui;
    }

    private void Report(string label, IReadOnlyList<double> times)
    {
        var sorted = times.Order().ToList();
        var frame = Frames.Count == 0 ? 0 : Frames.TakeLast(times.Count).Average();
        var flag = sorted[^1] > 50 ? "  <-- slow" : "";
        output.WriteLine($"  {label}: n {times.Count}, UI thread first {times[0]:F1}, mean {times.Average():F1}, p95 {sorted[(int)(sorted.Count * 0.95)]:F1}, max {sorted[^1]:F1} ms; headless frame mean {frame:F1} ms{flag}");
    }

    [Fact]
    public async Task UiOnRealFiles()
    {
        Assert.SkipUnless(Enabled, "G3MSTUDIO_PERF is not set");
        foreach (var file in PerfRealFilesTests.Files) await Scenario(file);
    }

    [Fact]
    public async Task UiOnSyntheticFile()
    {
        Assert.SkipUnless(Enabled, "G3MSTUDIO_PERF is not set");
        var count = Environment.GetEnvironmentVariable("G3MSTUDIO_PERF_SYNTHETIC");
        Assert.SkipWhen(string.IsNullOrEmpty(count), "G3MSTUDIO_PERF_SYNTHETIC is not set");
        using var dir = new TempDir();
        var path = dir.File("synthetic.win");
        using (var data = TestData.Create(int.Parse(count, System.Globalization.CultureInfo.InvariantCulture))) TestData.Write(path, data);
        await Scenario(path);
    }

    private async Task Scenario(string path)
    {
        using var dir = new TempDir();
        await HeadlessApp.Run(async () =>
        {
            Loc.Current.SetLanguage("en");
            var userData = new UserData(dir.File("user"));
            var window = new MainWindow(new WorkspaceService(userData, RecentFiles.Load(userData))) { Width = 1500, Height = 900 };
            window.Show();
            Dispatcher.UIThread.RunJobs();
            window.CaptureRenderedFrame();
            var watch = Stopwatch.StartNew();
            double changedAt = 0, afterJobs = 0;
            window.Workspaces.CurrentChanged += _ => changedAt = watch.Elapsed.TotalMilliseconds;
            await window.OpenAsync(path);
            var opened = watch.Elapsed.TotalMilliseconds;
            Dispatcher.UIThread.RunJobs();
            afterJobs = watch.Elapsed.TotalMilliseconds;
            output.WriteLine($"  open: workspace ready and shown at {changedAt:F0} ms, OpenAsync returned at {opened:F0} ms, queued jobs done at {afterJobs:F0} ms");
            window.CaptureRenderedFrame();
            var workspace = window.Workspaces.Current;
            var data = workspace.Data;
            output.WriteLine($"{path} ({new FileInfo(path).Length / 1048576} MB): window.OpenAsync to first frame {watch.Elapsed.TotalMilliseconds:F0} ms, {window.Explorer.Model.Total} entries");

            WindowsPart(window, workspace);
            EditPart(window, workspace);
            MemoryPart(window, workspace);
            ExplorerPart(window);
            TabsPart(window, workspace);
            window.Workspaces.Close();
            window.Close();
        });
    }

    private void WindowsPart(MainWindow window, Workspace workspace)
    {
        var data = workspace.Data;
        var sprite = data.Sprites.OrderByDescending(x => x.Textures.Count).First();
        var key = ResourceTypes.KeyOf("sprites", sprite.Name.Content);
        var times = new List<double>();
        for (var i = 0; i < 3; i++)
        {
            times.Add(Step(window, () => window.Docking.OpenTab("resource", key, false)));
            foreach (var tab in window.Docking.Tabs.Tabs.ToList()) window.Docking.CloseTab(tab.Id);
            Step(window, () => { });
        }

        Report($"open the sprite with the most frames ({sprite.Textures.Count}), three times", times);

        var strings = new List<double>();
        StringsWindow strWindow = null;
        strings.Add(Step(window, () => { strWindow = new StringsWindow(workspace); strWindow.Show(); }));
        var box = strWindow.GetVisualDescendants().OfType<TextBox>().First(b => b.Name == "StringFilter");
        output.WriteLine($"  Strings window: {data.Strings.Count} strings; open {strings[0]:F1} ms");
        var typing = new List<double>();
        foreach (var text in new[] { "a", "al", "alp", "alph", "alpha", "alph", "alp", "al", "a", "" }) typing.Add(Step(strWindow, () => box.Text = text));
        Report("Strings window, filter typing (one key)", typing);
        var list = strWindow.GetVisualDescendants().OfType<ListBox>().First(b => b.Name == "StringList");
        var scroll = list.GetVisualDescendants().OfType<ScrollViewer>().First();
        var frames = new List<double>();
        for (var i = 0; i < 60; i++) frames.Add(Step(strWindow, () => scroll.Offset = new Avalonia.Vector(0, 24 * 9 * (i + 1))));
        Report("Strings window, scroll", frames);
        var edit = new List<double>();
        for (var i = 0; i < 3; i++) edit.Add(Step(window, () => workspace.Edit(new SetPropertyCommand("objects", data.GameObjects[1].Name.Content, "Depth", (i + 5).ToString(System.Globalization.CultureInfo.InvariantCulture)))));
        Report("an edit with the Strings window open", edit);
        strWindow.Close();
        Step(window, () => { });

        var hits = Enumerable.Range(0, 50_000).Select(i => new UsageHit("objects", "obj_" + i, "Sprite")).ToList();
        var panel = new G3MStudio.Panels.UsagesPanel();
        var host = new Window { Content = panel, Width = 600, Height = 500 };
        host.Show();
        var usage = new List<double>();
        usage.Add(Step(host, () => _ = panel.ShowAsync("x", Task.FromResult<IReadOnlyList<UsageHit>>(hits))));
        Report("Usages panel shows 50,000 hits", usage);
        host.Close();
    }

    private void EditPart(MainWindow window, Workspace workspace)
    {
        var data = workspace.Data;
        var obj = data.GameObjects[data.GameObjects.Count / 2].Name.Content;
        var sprite = data.Sprites[data.Sprites.Count / 2].Name.Content;
        var code = data.Code.OrderByDescending(c => c.Instructions.Count).First().Name.Content;
        window.Docking.OpenTab("resource", "objects:" + obj, false);
        window.Docking.OpenTab("resource", "sprites:" + sprite, false);
        window.Docking.OpenTab("code", "code:" + code, false);
        Step(window, () => { });
        window.Docking.OpenTab("resource", "objects:" + obj, false);
        Step(window, () => { });
        var times = new List<double>();
        for (var i = 0; i < 12; i++) times.Add(Step(window, () => workspace.Edit(new SetPropertyCommand("objects", obj, "Depth", (i + 3).ToString(System.Globalization.CultureInfo.InvariantCulture)))));
        {
            var field = typeof(EditHistory).GetField("Changed", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            foreach (var handler in ((Delegate)field.GetValue(workspace.History)).GetInvocationList())
            {
                var t = Stopwatch.StartNew();
                ((Action)handler)();
                var ran = t.Elapsed.TotalMilliseconds;
                Dispatcher.UIThread.RunJobs();
                output.WriteLine($"    Changed handler {handler.Method.DeclaringType?.Name}.{handler.Method.Name}: {ran:F1} ms, with jobs {t.Elapsed.TotalMilliseconds:F1} ms");
            }
        }

        for (var k = 0; k < 3; k++)
        {
            var t = Stopwatch.StartNew();
            workspace.Edit(new SetPropertyCommand("objects", obj, "Depth", (k + 90).ToString(System.Globalization.CultureInfo.InvariantCulture)));
            var edited = t.Elapsed.TotalMilliseconds;
            t.Restart();
            Dispatcher.UIThread.RunJobs();
            output.WriteLine($"    one edit: Edit() returned after {edited:F1} ms, queued jobs then {t.Elapsed.TotalMilliseconds:F1} ms");
        }

        Report("edit a property with 3 tabs open (Edit + all handlers + layout)", times);
        times.Clear();
        for (var i = 0; i < 4; i++) times.Add(Step(window, workspace.Undo));
        Report("undo", times);
        var spriteName = sprite;
        times.Clear();
        for (var i = 0; i < 4; i++)
        {
            var from = spriteName;
            spriteName = sprite + "_r" + i;
            times.Add(Step(window, () => workspace.Edit(new RenameResourceCommand("sprites", from, spriteName))));
        }

        Report("rename a sprite (index kept by the cache, tabs follow)", times);
        times.Clear();
        for (var i = 0; i < 4; i++) times.Add(Step(window, () => workspace.Edit(new SetPropertyCommand("objects", obj, "Depth", (i + 30).ToString(System.Globalization.CultureInfo.InvariantCulture)))));
        Report("edit a property again", times);
        foreach (var tab in window.Docking.Tabs.Tabs.ToList()) window.Docking.CloseTab(tab.Id);
        Step(window, () => { });
    }

    private void MemoryPart(MainWindow window, Workspace workspace)
    {
        foreach (var type in new[] { "objects", "sprites", "rooms", "code", "scripts", "fonts" })
        {
            var items = ResourceTypes.All.First(t => t.Key == type).Items(workspace.Data).ToList();
            var key = ResourceTypes.KeyOf(type, ResourceTypes.NameOf(Pick(type, items)));
            Step(window, () => window.Docking.OpenTab(type == "code" ? "code" : "resource", key, false));
            foreach (var tab in window.Docking.Tabs.Tabs.ToList()) window.Docking.CloseTab(tab.Id);
            Step(window, () => { });
            var heap = new List<long>();
            for (var round = 0; round < 8; round++)
            {
                Step(window, () => window.Docking.OpenTab(type == "code" ? "code" : "resource", key, false));
                Step(window, () => { });
                foreach (var tab in window.Docking.Tabs.Tabs.ToList()) window.Docking.CloseTab(tab.Id);
                Step(window, () => { });
                Dispatcher.UIThread.RunJobs();
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                heap.Add(GC.GetTotalMemory(true) / 1024);
            }

            output.WriteLine($"  memory after open and close of {type}, KB: {string.Join(" ", heap)} (growth per round {(heap[^1] - heap[1]) / (double)(heap.Count - 2):F0} KB)");
        }
    }

    private void ExplorerPart(MainWindow window)
    {
        var explorer = window.Explorer;
        var model = explorer.Model;
        var biggest = model.Rows.Where(r => r.IsHeader && r.Key != ExplorerModel.DataGroup).OrderByDescending(r => r.Count).First();
        var times = new List<double>();
        if (!biggest.Expanded) times.Add(Step(window, () => model.Toggle(biggest.Key)));
        for (var i = 0; i < 4; i++)
        {
            times.Add(Step(window, () => model.Toggle(biggest.Key)));
            times.Add(Step(window, () => model.Toggle(biggest.Key)));
        }

        {
            var field = typeof(ExplorerModel).GetField("Changed", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            var handler = (Action)field.GetValue(model);
            field.SetValue(model, null);
            var pure = new List<double>();
            for (var i = 0; i < 4; i++)
            {
                var t = Stopwatch.StartNew();
                model.Toggle(biggest.Key);
                pure.Add(t.Elapsed.TotalMilliseconds);
            }

            field.SetValue(model, handler);
            if (pure.Count % 2 == 1) model.Toggle(biggest.Key);
            Step(window, () => { });
            output.WriteLine($"  model alone (Rebuild without the view): {string.Join(", ", pure.Select(x => x.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)))} ms; rows {model.Rows.Count}");
            var showRows = typeof(G3MStudio.Explorer.ExplorerView).GetMethod("ShowRows", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            var t2 = Stopwatch.StartNew();
            showRows.Invoke(explorer, null);
            output.WriteLine($"  ShowRows (ItemsSource, footer): {t2.Elapsed.TotalMilliseconds:F1} ms");
            t2.Restart();
            Dispatcher.UIThread.RunJobs();
            output.WriteLine($"  jobs after it (layout of the list): {t2.Elapsed.TotalMilliseconds:F1} ms");
        }

        output.WriteLine($"Explorer: biggest type {biggest.Key} with {biggest.Count} entries, {model.Rows.Count} rows now");
        Report("expand and collapse (toggle + frame)", times);

        var list = explorer.GetVisualDescendants().OfType<ListBox>().First();
        var scroll = list.GetVisualDescendants().OfType<ScrollViewer>().First();
        var rowHeight = 24.0;
        var frames = new List<double>();
        for (var i = 0; i < 200; i++) frames.Add(Step(window, () => scroll.Offset = new Avalonia.Vector(0, rowHeight * 7 * (i + 1))));
        Report("scroll 7 rows per frame", frames.Skip(5).ToList());
        frames.Clear();
        for (var i = 0; i < 20; i++) frames.Add(Step(window, () => scroll.Offset = new Avalonia.Vector(0, rowHeight * 2000 * ((i * 7919) % 17))));
        Report("scroll jump (random far position)", frames);

        var box = explorer.GetVisualDescendants().OfType<TextBox>().First();
        frames.Clear();
        foreach (var text in new[] { "o", "ob", "obj", "obj_", "obj_a", "obj_ab", "obj_a", "obj_", "obj", "ob", "o", "" }) frames.Add(Step(window, () => box.Text = text));
        Report("filter typing, one key", frames);
        frames.Clear();
        foreach (var text in new[] { "spr:a", "scr:s", "zzzz", "" }) frames.Add(Step(window, () => box.Text = text));
        Report("filter with prefix and no match", frames);
    }

    private void TabsPart(MainWindow window, Workspace workspace)
    {
        var data = workspace.Data;
        var opened = new List<string>();
        var perType = new List<(string Type, string Key, double Ms)>();
        var frameOf = new Dictionary<string, double>();
        foreach (var type in ResourceTypes.All)
        {
            var items = type.Items(data)?.ToList();
            if (items is null || items.Count == 0 || type.Key is "strings" or "variables" or "functions" or "codeLocals") continue;
            var item = Pick(type.Key, items);
            var key = ResourceTypes.KeyOf(type.Key, ResourceTypes.NameOf(item));
            var ms = Step(window, () => window.Docking.OpenTab(type.Key == "code" ? "code" : "resource", key, false));
            perType.Add((type.Key, key, ms));
            frameOf[type.Key] = Frames[^1];
            opened.Add(key);
        }

        output.WriteLine("Open one tab of each type (OpenTab + layout + frame):");
        foreach (var (type, key, ms) in perType) output.WriteLine($"  {type,-16} UI thread {ms,7:F1} ms, headless frame {frameOf[type],6:F1} ms{(ms > 50 ? "  <-- slow" : "")}  {(key.Length > 50 ? key[..50] : key)}");

        {
            var obj = data.GameObjects[data.GameObjects.Count / 2].Name.Content;
            for (var round = 0; round < 3; round++)
            {
                var t = Stopwatch.StartNew();
                var tab = new Editor.PropertyGridTab(workspace, "objects", obj);
                var built = t.Elapsed.TotalMilliseconds;
                var host = new Window { Content = tab, Width = 900, Height = 700 };
                t.Restart();
                host.Show();
                var shown = t.Elapsed.TotalMilliseconds;
                t.Restart();
                Dispatcher.UIThread.RunJobs();
                var jobs = t.Elapsed.TotalMilliseconds;
                t.Restart();
                host.CaptureRenderedFrame();
                output.WriteLine($"  PropertyGridTab alone: ctor {built:F1}, Show {shown:F1}, jobs {jobs:F1}, frame {t.Elapsed.TotalMilliseconds:F1} ms, {tab.GetVisualDescendants().Count()} visuals");
                host.Close();
            }
        }

        foreach (var tab in window.Docking.Tabs.Tabs.ToList()) window.Docking.CloseTab(tab.Id);
        Step(window, () => { });
        foreach (var key in new[] { "filterEffects:_filter_pixelate", "filterEffects:_filter_distort", "plainthree", "textureGroups:BattleHotlandFloweyYarn" })
        {
            var t = Stopwatch.StartNew();
            window.Docking.OpenTab("resource", key, false);
            var open = t.Elapsed.TotalMilliseconds;
            t.Restart();
            Dispatcher.UIThread.RunJobs();
            output.WriteLine($"  lone tab {key}: OpenTab {open:F1} ms, jobs {t.Elapsed.TotalMilliseconds:F1} ms");
            window.CaptureRenderedFrame();
        }

        output.WriteLine("Open a second tab of each type (code already compiled, so the steady cost):");
        foreach (var tab in window.Docking.Tabs.Tabs.ToList()) window.Docking.CloseTab(tab.Id);
        Step(window, () => { });
        var second = new List<double>();
        foreach (var type in ResourceTypes.All)
        {
            var items = type.Items(data)?.ToList();
            if (items is null || items.Count < 3 || type.Key is "strings" or "variables" or "functions" or "codeLocals") continue;
            var item = items[items.Count / 3];
            var key = ResourceTypes.KeyOf(type.Key, ResourceTypes.NameOf(item));
            var ms = Step(window, () => window.Docking.OpenTab(type.Key == "code" ? "code" : "resource", key, false));
            second.Add(ms);
            output.WriteLine($"  {type.Key,-16} UI thread {ms,7:F1} ms, GC pause {LastGcMs:F1} (gen0/1/2 {LastGcs}){(ms > 16 ? "  <-- over one frame" : "")}  {(key.Length > 50 ? key[..50] : key)}");
        }

        Report("second tab of each type", second);
        foreach (var tab in window.Docking.Tabs.Tabs.ToList()) window.Docking.CloseTab(tab.Id);
        Step(window, () => { });
        foreach (var key in opened) window.Docking.OpenTab(key.StartsWith("code:", StringComparison.Ordinal) ? "code" : "resource", key, false);
        Step(window, () => { });

        // Which part of a tab change costs: the handlers of Docking.Changed one by one, then the dock operation and the frame alone.
        var changed = (Delegate)typeof(G3MStudio.Docking.StudioDock).GetField("Changed", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(window.Docking);
        foreach (var handler in changed.GetInvocationList())
        {
            var watch = Stopwatch.StartNew();
            ((Action)handler)();
            output.WriteLine($"  Docking.Changed handler {handler.Method.DeclaringType?.Name}.{handler.Method.Name}: {watch.Elapsed.TotalMilliseconds:F1} ms");
        }

        var cycle = Stopwatch.StartNew();
        window.Docking.CycleTab(1);
        output.WriteLine($"  CycleTab call alone: {cycle.Elapsed.TotalMilliseconds:F1} ms");
        cycle.Restart();
        Dispatcher.UIThread.RunJobs();
        output.WriteLine($"  jobs after it: {cycle.Elapsed.TotalMilliseconds:F1} ms");
        cycle.Restart();
        window.UpdateLayout();
        output.WriteLine($"  layout after it: {cycle.Elapsed.TotalMilliseconds:F1} ms, {window.GetVisualDescendants().Count()} visuals in the window");
        cycle.Restart();
        window.CaptureRenderedFrame();
        output.WriteLine($"  frame after it: {cycle.Elapsed.TotalMilliseconds:F1} ms");
        cycle.Restart();
        window.CaptureRenderedFrame();
        output.WriteLine($"  frame with nothing changed: {cycle.Elapsed.TotalMilliseconds:F1} ms");

        cycle.Restart();
        foreach (var visual in window.GetVisualDescendants().OfType<Avalonia.Visual>()) visual.InvalidateVisual();
        window.CaptureRenderedFrame();
        output.WriteLine($"  frame with every visual invalidated: {cycle.Elapsed.TotalMilliseconds:F1} ms");
        var editorArea = window.GetVisualDescendants().OfType<Dock.Avalonia.Controls.DocumentControl>().FirstOrDefault();
        if (editorArea is not null)
        {
            cycle.Restart();
            foreach (var visual in editorArea.GetVisualDescendants().OfType<Avalonia.Visual>()) visual.InvalidateVisual();
            window.CaptureRenderedFrame();
            output.WriteLine($"  frame with the editor area invalidated: {cycle.Elapsed.TotalMilliseconds:F1} ms, {editorArea.GetVisualDescendants().Count()} visuals, size {editorArea.Bounds.Size}");
        }

        {
            window.Docking.CycleTab(1);
            Dispatcher.UIThread.RunJobs();
            var t = Stopwatch.StartNew();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            output.WriteLine($"  ForceRenderTimerTick after a tab switch: {t.Elapsed.TotalMilliseconds:F1} ms");
            t.Restart();
            window.GetLastRenderedFrame();
            output.WriteLine($"  GetLastRenderedFrame: {t.Elapsed.TotalMilliseconds:F1} ms");
            window.Docking.CycleTab(1);
            t.Restart();
            for (var k = 0; k < 4; k++)
            {
                Dispatcher.UIThread.RunJobs();
                output.WriteLine($"    RunJobs {k}: {t.Elapsed.TotalMilliseconds:F1} ms");
                t.Restart();
            }

            window.CaptureRenderedFrame();
            output.WriteLine($"    capture: {t.Elapsed.TotalMilliseconds:F1} ms");
            window.Docking.CycleTab(1);
            Dispatcher.UIThread.RunJobs();
            t.Restart();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            output.WriteLine($"  two ticks after a tab switch: {t.Elapsed.TotalMilliseconds:F1} ms");
        }

        var plain = new List<double>();
        foreach (var tab in window.Docking.Tabs.Tabs.ToList()) window.Docking.CloseTab(tab.Id);
        Step(window, () => { });
        window.Docking.OpenTab("resource", "plainone", false);
        window.Docking.OpenTab("resource", "plaintwo", false);
        Step(window, () => { });
        for (var i = 0; i < 10; i++) plain.Add(Step(window, () => window.Docking.CycleTab(1)));
        Report("switch between two tabs with a TextBlock only", plain);
        foreach (var tab in window.Docking.Tabs.Tabs.ToList()) window.Docking.CloseTab(tab.Id);
        Step(window, () => { });
        foreach (var key in opened) window.Docking.OpenTab(key.StartsWith("code:", StringComparison.Ordinal) ? "code" : "resource", key, false);
        Step(window, () => { });

        var switches = new List<double>();
        for (var i = 0; i < 40; i++) switches.Add(Step(window, () => window.Docking.CycleTab(1)));
        Report("switch tab (" + opened.Count + " open)", switches);
        var reopen = new List<double>();
        foreach (var key in opened.Take(10))
        {
            var type = ResourceTypes.SplitKey(key)!.Value.Type;
            reopen.Add(Step(window, () => window.Docking.OpenTab(type == "code" ? "code" : "resource", key, false)));
        }

        Report("select an open tab by key", reopen);
        var close = new List<double>();
        foreach (var tab in window.Docking.Tabs.Tabs.ToList()) close.Add(Step(window, () => window.Docking.CloseTab(tab.Id)));
        Report("close tab", close);
    }

    private static object Pick(string type, List<object> items) => type switch
    {
        "sprites" => items.OfType<G3MLib.DataFile.Models.GameMakerSprite>().OrderByDescending(s => s.Textures.Count).First(),
        "rooms" => items.OfType<G3MLib.DataFile.Models.GameMakerRoom>().OrderByDescending(r => r.GameObjects.Count + r.Tiles.Count + (r.Layers?.Count ?? 0)).First(),
        "code" => items.OfType<G3MLib.DataFile.Models.GameMakerCode>().OrderByDescending(c => c.Instructions.Count).First(),
        _ => items[items.Count / 2],
    };
}
