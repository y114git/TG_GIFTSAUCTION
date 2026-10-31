using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using G3MStudio.Core.Notifications;
using G3MStudio.Core.Workspace;
using G3MStudio.Explorer;
using G3MStudio.Localization;

namespace G3MStudio.Tests;

public sealed class ShellWorkspaceTests(ITestOutputHelper output)
{
    private static readonly string[] Filters = ["obj_4", "spr:12", "text 777", "zzz-none", "e", ""];

    private static MainWindow NewWindow(TempDir dir)
    {
        Loc.Current.SetLanguage("en");
        var userData = new UserData(dir.File("user"));
        var window = new MainWindow(new WorkspaceService(userData, RecentFiles.Load(userData)));
        window.Show();
        return window;
    }

    private static async Task Settle(Func<bool> done)
    {
        for (var i = 0; i < 1000 && !done(); i++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(10);
        }

        Assert.True(done(), "the window did not settle");
    }

    // Templates are applied by layout, so run it before looking into the visual tree.
    private static T Named<T>(Control root, string name) where T : Control
    {
        Dispatcher.UIThread.RunJobs(); // dock content is created when the layout is first shown
        (root as TopLevel)?.UpdateLayout();
        return root.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);
    }

    private static List<ExplorerRow> Rows(MainWindow window) => [.. Named<ListBox>(window, "List").ItemsSource!.Cast<ExplorerRow>()];

    private static async Task AssertOpens(MainWindow window, string path, Action open)
    {
        open();
        await Settle(() => window.Workspaces.Current is not null && window.FindControl<Border>("Progress") is { IsVisible: false });
        Assert.Equal(Path.GetFullPath(path), window.Workspaces.Current!.Path);
        Assert.True(Named<Grid>(window, "WorkArea").IsVisible);
        Assert.False(Named<Border>(window, "Progress").IsVisible);
        window.Workspaces.Close();
        Assert.True(window.FindControl<Control>("Start")!.IsVisible);
    }

    [Fact]
    public async Task EveryEntryPointOpensADataFile()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir);
        await HeadlessApp.Run(async () =>
        {
            var window = NewWindow(dir);
            try
            {
                // Recent card: first open by path, then the card of that file.
                await window.OpenAsync(path);
                window.Workspaces.Close();
                window.UpdateLayout();
                var card = window.GetVisualDescendants().OfType<Button>().First(b => b.Tag is string tag && tag == Path.GetFullPath(path) && b.Content is StackPanel);
                await AssertOpens(window, path, () => card.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)));

                // File > Open runs the registry command (the picker itself needs a real window).
                var executed = new List<string>();
                window.Registry.Executed += executed.Add;
                window.UpdateLayout();
                MenuItem? menuOpen = null;
                for (var i = 0; i < 50 && menuOpen is null; i++)
                {
                    Dispatcher.UIThread.RunJobs();
                    window.UpdateLayout();
                    menuOpen = Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(window).OfType<MenuItem>().FirstOrDefault(m => (string?)m.Header == "Open...");
                    if (menuOpen is null) await Task.Delay(20);
                }
                menuOpen!.Command!.Execute(null);
                Assert.Contains("file.open", executed);

                // The start page button runs the same command.
                executed.Clear();
                Named<Button>(window, "OpenButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.Equal(["file.open"], executed);

                // Command line and drop.
                await AssertOpens(window, path, () => window.OpenStartupFile(["--smoke", path]));
                await AssertOpens(window, path, () => window.OpenDropped([path]));
            }
            finally
            {
                window.Workspaces.Close();
                window.Close();
            }
        });
    }

    [Fact]
    public async Task OpeningShowsTheExplorerTitleAndRecoveredEditsAndCloseReturnsToTheStartPage()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir, objects: 4);
        await HeadlessApp.Run(async () =>
        {
            var window = NewWindow(dir);
            try
            {
                await window.OpenAsync(path);
                Assert.Equal("New GameMaker Game - G3MStudio", window.Title);
                Assert.True(window.Registry.Execute("file.save"));
                await Settle(() => window.Notifications.History.Any(n => n.Text.StartsWith("Saved", StringComparison.Ordinal)));
                var rows = Rows(window);
                Assert.Contains(rows, r => r is { Kind: ExplorerRowKind.Type, Key: "objects", Count: 4 });
                Assert.Contains(rows, r => r.Text == "obj_3");
                Assert.Equal(Loc.Current.Format("explorer.count", ("count", Named<ExplorerView>(window, "Explorer").Model.Total)), Named<TextBlock>(window, "Footer").Text);

                window.Workspaces.Current!.History.Execute(new RenameResourceCommand("objects", "obj_0", "obj_first"));
                Dispatcher.UIThread.RunJobs();
                Assert.StartsWith("● ", window.Title, StringComparison.Ordinal);
                Assert.Contains(Rows(window), r => r.Text == "obj_first");
                window.Registry.Execute("edit.undo");
                Assert.Contains(Rows(window), r => r.Text == "obj_0");
                window.Registry.Execute("edit.redo");
                Assert.Contains(Rows(window), r => r.Text == "obj_first");

                // Close with the edit unsaved, reopen: the journal brings it back and the status line says so.
                window.AskClose = _ => Task.FromResult(CloseChoice.DoItLater);
                window.Registry.Execute("file.close");
                Assert.Null(window.Workspaces.Current);
                await window.OpenAsync(path);
                Assert.Contains(Rows(window), r => r.Text == "obj_first");
                Assert.Equal("Recovered 1 unsaved edits", window.Notifications.Active[^1].Text);
            }
            finally
            {
                window.Workspaces.Close();
                window.Close();
            }
        });
    }

    [Fact]
    public async Task ABrokenFileShowsAnErrorAndKeepsTheStartPage()
    {
        using var dir = new TempDir();
        var bad = dir.File("bad.win");
        await File.WriteAllTextAsync(bad, "not a data file", TestContext.Current.CancellationToken);
        await HeadlessApp.Run(async () =>
        {
            var window = NewWindow(dir);
            try
            {
                await window.OpenAsync(bad);
                Assert.Null(window.Workspaces.Current);
                Assert.StartsWith("Could not open 'bad.win'", window.Notifications.Active.Single().Text, StringComparison.Ordinal);
                Assert.Equal(NotificationLevel.Error, window.Notifications.Active.Single().Level);
                Assert.False(Named<Border>(window, "Progress").IsVisible);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public async Task TheListIsVirtualizedAndFiltersFast50000EntryTree()
    {
        await HeadlessApp.Run(() =>
        {
            Loc.Current.SetLanguage("en");
            using var data = ExplorerModelTests.Generate(50_000);
            var view = new ExplorerView();
            var window = new Window { Width = 400, Height = 700, Content = view };
            window.Show();
            try
            {
                var load = Stopwatch.StartNew();
                view.Load("big.win", data);
                window.UpdateLayout();
                output.WriteLine($"load and first layout of 50,000 entries: {load.ElapsedMilliseconds} ms");

                var items = view.GetVisualDescendants().OfType<ListBoxItem>().Count();
                output.WriteLine($"realized rows: {items}");
                Assert.InRange(items, 1, 100);

                // Scroll through the full tree; each step must draw a different picture or the numbers mean nothing.
                var viewer = Named<ListBox>(view, "List").GetVisualDescendants().OfType<ScrollViewer>().First();
                var frames = new List<double>();
                var pictures = new HashSet<string>();
                for (var i = 0; i < 80; i++)
                {
                    viewer.Offset = new Vector(0, 24 * 7 * (i + 1));
                    var step = Stopwatch.StartNew();
                    Dispatcher.UIThread.RunJobs();
                    var frame = window.CaptureRenderedFrame()!;
                    if (i >= 10) frames.Add(step.Elapsed.TotalMilliseconds);
                    using var png = new MemoryStream();
                    frame.Save(png, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
                    pictures.Add(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(png.ToArray())));
                }

                var ordered = frames.Order().ToList();
                output.WriteLine($"scroll frame: mean {frames.Average():F2} ms, p95 {ordered[(int)(ordered.Count * 0.95)]:F2} ms, max {ordered[^1]:F2} ms over {frames.Count} frames");
                Assert.True(pictures.Count > 70, $"only {pictures.Count} distinct frames");
                Budget.AssertUnder(ordered[(int)(ordered.Count * 0.95)], 100, "p95 scroll frame cost", output);

                var box = Named<TextBox>(view, "FilterBox");
                var slowest = 0L;
                foreach (var filter in Filters)
                {
                    // Best of three: a busy machine can stall one run, a real slowdown slows all three.
                    var best = long.MaxValue;
                    for (var run = 0; run < 3; run++)
                    {
                        box.Text = filter.Length == 0 ? "x" : "";
                        window.UpdateLayout();
                        var timer = Stopwatch.StartNew();
                        box.Text = filter;
                        Assert.Equal(filter, view.Model.Filter);
                        window.UpdateLayout();
                        best = Math.Min(best, timer.ElapsedMilliseconds);
                    }

                    output.WriteLine($"filter '{filter}': best with layout {best} ms, {view.Model.Rows.Count} rows");
                    slowest = Math.Max(slowest, best);
                }

                Budget.AssertUnder(slowest, 100, "slowest filter including layout", output);
                Assert.InRange(view.Model.Total, 50_000, 50_100);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public async Task ClickingATypeCollapsesItAndTheLanguageSwitchRelabelsTheTree()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir, objects: 2);
        await HeadlessApp.Run(async () =>
        {
            var window = NewWindow(dir);
            try
            {
                await window.OpenAsync(path);
                var list = Named<ListBox>(window, "List");
                list.UpdateLayout();
                var before = Rows(window).Count;
                var objects = Rows(window).Single(r => r is { Kind: ExplorerRowKind.Type, Key: "objects" });
                list.SelectedItem = objects;
                list.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Left });
                Assert.Equal(before - 2, Rows(window).Count);

                Loc.Current.SetLanguage("ru");
                Dispatcher.UIThread.RunJobs();
                Assert.Contains(Rows(window), r => r.Kind == ExplorerRowKind.Type && r.Text == "Объекты");
                Assert.Contains("Ресурсов:", Named<TextBlock>(window, "Footer").Text, StringComparison.Ordinal);
            }
            finally
            {
                Loc.Current.SetLanguage("en");
                window.Workspaces.Close();
                window.Close();
            }
        });
    }

    [Fact]
    public async Task EveryBorderInTheOpenWorkspaceViewIsTwoPixels()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir);
        await HeadlessApp.Run(async () =>
        {
            var window = NewWindow(dir);
            try
            {
                var bad = new List<string>();
                void Walk()
                {
                    window.UpdateLayout();
                    foreach (var visual in window.GetVisualDescendants())
                    {
                        var thickness = visual switch { Border b => b.BorderThickness, TemplatedControl t => t.BorderThickness, _ => default(Thickness?) };
                        if (thickness is not { } th) continue;
                        foreach (var side in new[] { th.Left, th.Top, th.Right, th.Bottom })
                            if (side is not (0 or 2)) bad.Add($"{visual.GetType().Name} {th}");
                    }
                }

                Walk(); // start page with a recent card
                await window.OpenAsync(path);
                Walk(); // explorer, filter box, list
                Assert.Empty(bad.Distinct());
            }
            finally
            {
                window.Workspaces.Close();
                window.Close();
            }
        });
    }
}
