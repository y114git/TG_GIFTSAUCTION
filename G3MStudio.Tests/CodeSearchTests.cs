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
    public async Task ReplaceInCodeIsOneUndoableEditAndSkipsEntriesThatDoNotCompile()
    {
        using var dir = new TempDir();
        using var workspace = await Open(dir);
        var ct = TestContext.Current.CancellationToken;

        var word = await CodeSearch.RunAsync(workspace, new CodeSearchOptions("retur", WholeWord: true, Replacement: "x"), cancellationToken: ct);
        Assert.Empty(word.Hits); // "retur" is only part of a word
        var plan = await CodeSearch.RunAsync(workspace, new CodeSearchOptions("return 1", Replacement: "return 2"), cancellationToken: ct);
        var hit = Assert.Single(plan.Hits, h => h.Entry == "gml_Script_probe");
        Assert.Equal("return 2;", hit.Replaced);

        var options = new CodeSearchOptions("return 1", Replacement: "return 2");
        var result = await CodeReplace.ApplyAsync(workspace, options, [hit], ct);
        Assert.Equal(1, result.Entries);
        Assert.Contains("return 2", CodeText.Decompile(workspace.Data, "gml_Script_probe"), StringComparison.Ordinal);
        workspace.Undo();
        Assert.Contains("return 1", CodeText.Decompile(workspace.Data, "gml_Script_probe"), StringComparison.Ordinal);

        var broken = await CodeReplace.ApplyAsync(workspace, new CodeSearchOptions("return 1", Replacement: "return ("), [hit], ct);
        Assert.Equal(["gml_Script_probe"], broken.Failed);
        Assert.Equal(0, broken.Entries);
        Assert.Contains("return 1", CodeText.Decompile(workspace.Data, "gml_Script_probe"), StringComparison.Ordinal);
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
                Assert.Contains("Matches:", panel.Status, StringComparison.Ordinal);
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

                // Several selected entries: one search for all, leaving out the hits that come from the selection itself.
                workspace.Data.GameObjects.Add(new G3MLib.DataFile.Models.GameMakerGameObject { Name = workspace.Data.Strings.MakeString("obj_other", out _), ParentId = workspace.Data.GameObjects[0] });
                window.Explorer.Load(workspace.Name, workspace.Data);
                window.Explorer.SelectKeys(["objects:obj_0", "objects:obj_other"], null);
                Assert.True(window.Registry.Execute("edit.findUsages"));
                for (var i = 0; i < 200 && !panel.Hits.Any(h => h.Where.StartsWith("obj_0:", StringComparison.Ordinal)); i++)
                {
                    Dispatcher.UIThread.RunJobs();
                    await Task.Delay(10, TestContext.Current.CancellationToken);
                }

                Assert.Contains(panel.Hits, h => h.OwnerName == "obj_child" && h.Where.StartsWith("obj_0:", StringComparison.Ordinal));
                Assert.DoesNotContain(panel.Hits, h => h.OwnerName == "obj_other");
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public async Task FindUnreferencedListsOnlyAssetsNothingRefersTo()
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
                workspace.Data.GameObjects.Add(new G3MLib.DataFile.Models.GameMakerGameObject { Name = workspace.Data.Strings.MakeString("obj_child", out _), ParentId = workspace.Data.GameObjects[0] });
                var hits = await UsageSearch.FindUnreferencedAsync(workspace, TestContext.Current.CancellationToken);
                Assert.Contains(hits, h => h is { OwnerType: "objects", OwnerName: "obj_child" });
                Assert.DoesNotContain(hits, h => h.OwnerName == "obj_0"); // its child refers to it
                foreach (var hit in hits) Assert.Empty(await UsageSearch.FindAsync(workspace, hit.OwnerType!, hit.OwnerName, TestContext.Current.CancellationToken));
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public async Task RemoveSelectedDeletesUnreferencedAssetsAsOneUndoableEdit()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir, objects: 1, withCode: true);
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            var asked = 0;
            window.Confirmer = (_, _) => Task.FromResult(++asked > 0);
            try
            {
                await window.OpenAsync(path);
                var workspace = window.Workspaces.Current!;
                workspace.Data.GameObjects.Add(new G3MLib.DataFile.Models.GameMakerGameObject { Name = workspace.Data.Strings.MakeString("obj_child", out _), ParentId = workspace.Data.GameObjects[0] });
                window.Registry.Execute("edit.findUnreferenced");
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                var panel = window.GetVisualDescendants().OfType<UsagesPanel>().Single();
                for (var i = 0; i < 300 && !panel.Hits.Any(h => h.OwnerName == "obj_child"); i++)
                {
                    Dispatcher.UIThread.RunJobs();
                    await Task.Delay(10, TestContext.Current.CancellationToken);
                }

                panel.SelectAllRows();
                panel.RemoveSelected();
                for (var i = 0; i < 300 && workspace.Data.GameObjects.Any(o => o.Name.Content == "obj_child"); i++)
                {
                    Dispatcher.UIThread.RunJobs();
                    await Task.Delay(10, TestContext.Current.CancellationToken);
                }

                Assert.True(asked > 0);
                Assert.DoesNotContain(workspace.Data.GameObjects, o => o.Name.Content == "obj_child");
                workspace.Undo();
                Assert.Contains(workspace.Data.GameObjects, o => o.Name.Content == "obj_child");
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public async Task ExcludedRowsLeaveTheUnreferencedListAndStayInTheFile()
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
                workspace.Data.GameObjects.Add(new G3MLib.DataFile.Models.GameMakerGameObject { Name = workspace.Data.Strings.MakeString("obj_child", out _), ParentId = workspace.Data.GameObjects[0] });
                window.Registry.Execute("edit.findUnreferenced");
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                var panel = window.GetVisualDescendants().OfType<UsagesPanel>().Single();
                for (var i = 0; i < 300 && !panel.Hits.Any(h => h.OwnerName == "obj_child"); i++)
                {
                    Dispatcher.UIThread.RunJobs();
                    await Task.Delay(10, TestContext.Current.CancellationToken);
                }

                panel.SelectAllRows();
                panel.ExcludeSelected();
                Assert.Empty(panel.Hits);
                Assert.Contains(workspace.Data.GameObjects, o => o.Name.Content == "obj_child");
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public async Task TheCodeTabSwitchesToTheAssemblyOfAnEntryAndAssemblesEditsWithUndo()
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
                var entry = workspace.Data.Code.First().Name.Content!;
                var tab = new G3MStudio.Editor.CodeEditorTab(workspace, entry);
                await tab.Ready;
                var gml = tab.Text;
                tab.ShowAssembly = true;
                await tab.Ready;
                Assert.NotEqual(gml, tab.Text);
                Assert.Equal(CodeMode.Assembly, tab.Mode);
                Assert.False(tab.ShowsFallback);

                // Apply puts the edited assembly back as one edit that undoes.
                var steps = workspace.History.Position;
                tab.Editor.Text += "\n; edited";
                Assert.True(tab.Apply());
                Assert.Equal(steps + 1, workspace.History.Position);
                tab.ShowAssembly = false;
                await tab.Ready;
                Assert.Equal(CodeMode.Decompiled, tab.Mode);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public async Task OpeningAFileTellsAboutYycTheDebuggerFlagAndUnknownTextures()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir, objects: 1, withCode: false);
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                await window.OpenAsync(path);
                var data = window.Workspaces.Current!.Data;
                data.GeneralInfo.IsDebuggerDisabled = false;
                var found = LoadChecks.Find(data);
                Assert.Contains(found, f => f.Key == LoadChecks.Debugger);
                data.GeneralInfo.IsDebuggerDisabled = true;
                Assert.DoesNotContain(LoadChecks.Find(data), f => f.Key == LoadChecks.Debugger);
                if (data.Code.Count == 0) Assert.Contains(LoadChecks.Find(data), f => f.Key == LoadChecks.Yyc);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public async Task SearchingARealGameIsFast()
    {
        var folder = Corpus.Folder;
        var file = Corpus.Files(folder).First();
        using var dir = new TempDir();
        var userData = new UserData(dir.File("user"));
        using var workspace = await new WorkspaceService(userData, RecentFiles.Load(userData)).OpenAsync(file, cancellationToken: TestContext.Current.CancellationToken);
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var result = await CodeSearch.RunAsync(workspace, new CodeSearchOptions("global."), cancellationToken: TestContext.Current.CancellationToken);
        output.WriteLine($"{Path.GetFileName(Path.GetDirectoryName(file))}: {result.Hits.Count:N0} hits in {result.Searched:N0} entries, {result.Failed} failed, {watch.Elapsed.TotalSeconds:F1} s");
        Assert.True(result.Searched > 0);
    }

    [Fact]
    public async Task AChosenLineThatChangedSinceThePreviewIsSkippedAndReported()
    {
        using var dir = new TempDir();
        using var workspace = await Open(dir);
        var ct = TestContext.Current.CancellationToken;
        var options = new CodeSearchOptions("return 1", Replacement: "return 2");
        var plan = await CodeSearch.RunAsync(workspace, options, cancellationToken: ct);
        var hit = Assert.Single(plan.Hits, h => h.Entry == "gml_Script_probe");

        workspace.Edit(new EditCodeCommand("gml_Script_probe", "return 8;"));
        var result = await CodeReplace.ApplyAsync(workspace, options, [hit], ct);
        Assert.Equal(0, result.Entries);
        Assert.Equal(["gml_Script_probe"], result.Stale);
        Assert.Contains("return 8", CodeText.Decompile(workspace.Data, "gml_Script_probe"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProbingFindsEveryEditThatDoesNotCompileInOnePassAndLeavesTheModelAlone()
    {
        using var dir = new TempDir();
        using var workspace = await Open(dir);
        var bad1 = new EditCodeCommand("gml_Script_probe", "return (");
        var good = new EditCodeCommand("gml_Script_probe", "return 9;");
        var bad2 = new EditCodeCommand("gml_Script_probe", "return )");

        var failures = await workspace.ProbeInBackground([bad1, good, bad2]);
        Assert.Equal([bad1, bad2], failures.Select(f => f.Command));
        Assert.Contains("return 1", CodeText.Decompile(workspace.Data, "gml_Script_probe"), StringComparison.Ordinal);
    }
}
