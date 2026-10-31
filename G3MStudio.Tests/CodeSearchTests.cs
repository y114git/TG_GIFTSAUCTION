using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using G3MLib.DataFile;
using G3MStudio.Core.Workspace;
using G3MStudio.Panels;

namespace G3MStudio.Tests;

public sealed class CodeSearchTests(ITestOutputHelper output)
{
    private static async Task<Workspace> Open(TempDir dir)
    {
        var path = TestData.WriteSample(dir, objects: 1, withCode: true);
        var userData = new UserData(dir.File("user"));
        return await new WorkspaceService(userData, RecentFiles.Load(userData)).OpenAsync(path);
    }

    [Fact]
    public async Task SearchFindsLinesWithOptionsAndCountsWhatItSearched()
    {
        using var dir = new TempDir();
        using var workspace = await Open(dir);
        var ct = TestContext.Current.CancellationToken;
        var reported = 0;

        var plain = await CodeSearch.RunAsync(workspace, new CodeSearchOptions("RETURN"), new Progress<(int, int)>(_ => reported++), ct);
        Assert.Contains(plain.Hits, h => h.Entry == "gml_Script_probe" && h.Line == 1);
        Assert.Equal(0, plain.Failed);
        Assert.True(plain.Searched > 0);

        Assert.DoesNotContain((await CodeSearch.RunAsync(workspace, new CodeSearchOptions("RETURN", CaseSensitive: true), cancellationToken: ct)).Hits, h => h.Entry == "gml_Script_probe");
        Assert.Contains((await CodeSearch.RunAsync(workspace, new CodeSearchOptions(@"ret\w+ \d", UseRegex: true), cancellationToken: ct)).Hits, h => h.Entry == "gml_Script_probe");
        await Assert.ThrowsAnyAsync<ArgumentException>(() => CodeSearch.RunAsync(workspace, new CodeSearchOptions("(", UseRegex: true), cancellationToken: ct));
        await Assert.ThrowsAsync<ArgumentException>(() => CodeSearch.RunAsync(workspace, new CodeSearchOptions(""), cancellationToken: ct));

        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CodeSearch.RunAsync(workspace, new CodeSearchOptions("return"), cancellationToken: cancelled.Token));
    }

    [Fact]
    public async Task TheDecompileContextIsSharedUntilTheNextEdit()
    {
        using var dir = new TempDir();
        using var workspace = await Open(dir);
        var first = workspace.DecompileContext();
        Assert.Same(first, workspace.DecompileContext());
        workspace.Edit(new EditCodeCommand("gml_Script_probe", "return 3;"));
        var second = workspace.DecompileContext();
        Assert.NotSame(first, second);
        workspace.Undo();
        Assert.NotSame(second, workspace.DecompileContext());
        Assert.Contains("return 1", CodeText.Decompile(workspace.Data, "gml_Script_probe", workspace.DecompileContext()), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ThePanelSearchesAndAHitOpensItsEntry()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir, objects: 1, withCode: true);
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                await window.OpenAsync(path);
                window.Registry.Execute("edit.findInCode");
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                var panel = window.GetVisualDescendants().OfType<SearchPanel>().Single();
                window.GetVisualDescendants().OfType<TextBox>().Single(t => t.Name == "SearchQuery").Text = "return";
                await panel.RunAsync();
                Assert.Contains(panel.Hits, h => h.Entry == "gml_Script_probe");
                Assert.Contains("matches", panel.Status, StringComparison.Ordinal);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public async Task PanelLabelsFollowTheLanguage()
    {
        await HeadlessApp.Run(() =>
        {
            var panel = new SearchPanel();
            var git = new G3MStudio.Git.GitPanel();
            var window = new Window { Content = new StackPanel { Children = { panel, git } } };
            window.Show();
            try
            {
                Button Run() => window.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "SearchRun");
                Button Commit() => window.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "GitCommit");
                Assert.Equal("Search", Run().Content);
                Localization.Loc.Current.SetLanguage("ru");
                Dispatcher.UIThread.RunJobs();
                Assert.Equal("Искать", Run().Content);
                Assert.Equal("Закоммитить", Commit().Content);
            }
            finally
            {
                Localization.Loc.Current.SetLanguage("en");
                window.Close();
            }
        });
    }

    [Fact]
    public async Task UsagesListWhatHoldsAReferenceAndTheUsagesCommandShowsThem()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir, objects: 1, withCode: true);
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                await window.OpenAsync(path);
                var workspace = window.Workspaces.Current!;
                // An object whose parent is another object holds a reference to it.
                workspace.Data.GameObjects.Add(new G3MLib.DataFile.Models.GameMakerGameObject { Name = workspace.Data.Strings.MakeString("obj_child", out _), ParentId = workspace.Data.GameObjects[0] });
                var hits = await UsageSearch.FindAsync(workspace, "objects", "obj_0", TestContext.Current.CancellationToken);
                Assert.Contains(hits, h => h.OwnerType == "objects" && h.OwnerName == "obj_child");

                window.Explorer.Select("objects:obj_0");
                window.Registry.Execute("edit.findUsages");
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                var panel = window.GetVisualDescendants().OfType<UsagesPanel>().Single();
                for (var i = 0; i < 200 && panel.Hits.Count == 0; i++)
                {
                    Dispatcher.UIThread.RunJobs();
                    await Task.Delay(10, TestContext.Current.CancellationToken);
                }

                Assert.Contains(panel.Hits, h => h.OwnerName == "obj_child");
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public async Task SearchingARealGameIsFast()
    {
        var folder = Environment.GetEnvironmentVariable("G3MSTUDIO_CORPUS_DIR");
        Assert.SkipWhen(string.IsNullOrEmpty(folder) || !Directory.Exists(folder), "G3MSTUDIO_CORPUS_DIR is not set");
        var file = Directory.EnumerateFiles(folder, "data.win", SearchOption.AllDirectories).OrderBy(f => new FileInfo(f).Length).First();
        using var dir = new TempDir();
        var userData = new UserData(dir.File("user"));
        using var workspace = await new WorkspaceService(userData, RecentFiles.Load(userData)).OpenAsync(file, cancellationToken: TestContext.Current.CancellationToken);
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var result = await CodeSearch.RunAsync(workspace, new CodeSearchOptions("global."), cancellationToken: TestContext.Current.CancellationToken);
        output.WriteLine($"{Path.GetFileName(Path.GetDirectoryName(file))}: {result.Hits.Count:N0} hits in {result.Searched:N0} entries, {result.Failed} failed, {watch.Elapsed.TotalSeconds:F1} s");
        Assert.True(result.Searched > 0);
    }
}
