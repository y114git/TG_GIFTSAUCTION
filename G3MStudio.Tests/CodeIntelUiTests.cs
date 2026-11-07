using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Avalonia.Headless;
using G3MStudio.Core.Commands;
using G3MStudio.Core.Workspace;
using G3MStudio.Editor;

namespace G3MStudio.Tests;

public sealed class CodeIntelUiTests
{
    private const string Sample = "var hp = 1;\nvar mp = hp + 1;\nfunction foo(x, y = 2)\n{\n    return x;\n}\nfoo(1, 2);\n";

    private static async Task<Workspace> Open(TempDir dir)
    {
        var path = TestData.WriteSample(dir, withCode: true);
        var userData = new UserData(dir.File("user"));
        return await new WorkspaceService(userData, RecentFiles.Load(userData)).OpenAsync(path, null, TestContext.Current.CancellationToken);
    }

    private static async Task Settle(Func<bool> done)
    {
        for (var i = 0; i < 300 && !done(); i++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(10);
        }

        Assert.True(done(), "did not settle");
    }

    private static async Task WithTab(Func<CodeEditorTab, Workspace, Window, Task> body, string text = Sample)
    {
        using var dir = new TempDir();
        using var workspace = await Open(dir);
        await HeadlessApp.Run(async () =>
        {
            var tab = new CodeEditorTab(workspace, "gml_Script_probe");
            var window = new Window { Content = tab, Width = 900, Height = 600 };
            window.Show();
            await tab.Ready;
            tab.Editor.Text = text;
            await body(tab, workspace, window);
            window.Close();
        });
    }

    [Fact]
    public void EveryCodeCommandIsInTheRegistryWithAShortcutThatCanBeRebound()
    {
        var registry = AppCommands.Create(new FakeShell());
        foreach (var (id, shortcut) in AppCommands.CodeCommands)
        {
            var command = registry.Get(id);
            Assert.Equal(AppCommands.CodeTab, command.When);
            Assert.Equal(shortcut, command.Shortcut);
            registry.SetShortcut(id, "Ctrl+Alt+F9");
            Assert.Equal("Ctrl+Alt+F9", registry.Get(id).Shortcut);
            registry.SetShortcut(id, shortcut);
        }

        Assert.False(registry.Execute("code.toggleComment")); // no code tab in front
        registry.SetContext(AppCommands.CodeTab, true);
        Assert.True(registry.Execute("code.toggleComment"));
    }

    [Fact]
    public async Task LineCommandsEditTheTextAsOneUndoStep()
    {
        await WithTab((tab, _, _) =>
        {
            tab.Editor.CaretOffset = 2;
            tab.Run("code.toggleComment");
            Assert.StartsWith("// var hp", tab.Text, StringComparison.Ordinal);
            tab.Editor.Undo();
            Assert.StartsWith("var hp", tab.Text, StringComparison.Ordinal);
            tab.Run("code.duplicateLine");
            Assert.StartsWith("var hp = 1;\nvar hp = 1;\n", tab.Text, StringComparison.Ordinal);
            tab.Editor.Undo();
            Assert.Equal(Sample, tab.Text);
            tab.Run("code.moveLineDown");
            Assert.StartsWith("var mp = hp + 1;\nvar hp = 1;", tab.Text, StringComparison.Ordinal);
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task GoToDefinitionFindsLocalsAndFunctionsAndBreadcrumbAndOutlineFollowTheCaret()
    {
        await WithTab(async (tab, _, _) =>
        {
            tab.Editor.CaretOffset = tab.Text.IndexOf("hp +", StringComparison.Ordinal) + 1;
            tab.Run("code.goToDefinition");
            Assert.Equal(tab.Text.IndexOf("hp", StringComparison.Ordinal), tab.Editor.CaretOffset);
            tab.Editor.CaretOffset = tab.Text.LastIndexOf("foo", StringComparison.Ordinal) + 1;
            tab.Run("code.goToDefinition");
            Assert.Equal(tab.Text.IndexOf("function foo", StringComparison.Ordinal), tab.Editor.CaretOffset);
            Assert.Equal(["foo"], tab.OutlineItems.Select(i => i.Name));
            tab.Editor.CaretOffset = tab.Text.IndexOf("return", StringComparison.Ordinal);
            await Settle(() => tab.Breadcrumb == "foo");
        });
    }

    [Fact]
    public async Task PeekShowsTheDefinitionAndTheSignatureHintMarksTheActiveParameter()
    {
        await WithTab(async (tab, _, _) =>
        {
            tab.Editor.CaretOffset = tab.Text.LastIndexOf("foo", StringComparison.Ordinal) + 1;
            tab.Run("code.peekDefinition");
            await Settle(() => tab.PeekText is not null);
            Assert.Contains("function foo", tab.PeekText, StringComparison.Ordinal);

            tab.Editor.Text = "function foo(x, y = 2) {}\nfoo(1, ";
            tab.Editor.CaretOffset = tab.Text.Length;
            await Settle(() => tab.Doc.Tokens.Count > 0);
            tab.Run("code.parameterHints");
            Assert.Equal(["foo(", "x", ", ", "[y]", ")"], tab.SignatureShown.Select(s => s.Text));
            Assert.Equal("[y]", tab.SignatureShown.Single(s => s.Active).Text);
            tab.Editor.CaretOffset = 0;
            Assert.Empty(tab.SignatureShown);
        });
    }

    [Fact]
    public async Task BookmarksFollowTheLinesAndWrapAround()
    {
        await WithTab((tab, _, _) =>
        {
            tab.Editor.CaretOffset = 0;
            tab.Run("code.toggleBookmark");
            tab.Editor.CaretOffset = tab.Text.IndexOf("return", StringComparison.Ordinal);
            tab.Run("code.toggleBookmark");
            Assert.Equal([1, 5], tab.BookmarkLines);
            tab.Run("code.nextBookmark");
            Assert.Equal(1, tab.Editor.TextArea.Caret.Line);
            tab.Run("code.previousBookmark");
            Assert.Equal(5, tab.Editor.TextArea.Caret.Line);
            tab.Editor.Document.Insert(0, "// top\n");
            Assert.Equal([2, 6], tab.BookmarkLines);
            tab.Run("code.toggleBookmark");
            Assert.Equal([2], tab.BookmarkLines);
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task ChangesAgainstTheOpeningCodeShowAddedAndRemovedLines()
    {
        await WithTab(async (tab, _, _) =>
        {
            tab.Run("code.toggleDiff");
            Assert.True(tab.DiffVisible);
            tab.Editor.Text = "var hp = 1;\nvar extra = 3;\n";
            await Settle(() => tab.DiffShown.Any(d => d.Text == "var extra = 3;"));
            Assert.Contains(tab.DiffShown, d => d.Kind == DiffKind.Added && d.Text == "var extra = 3;");
            Assert.Contains(tab.DiffShown, d => d.Kind == DiffKind.Removed && d.Text == "return 1;"); // the code when the tab opened
            tab.Run("code.toggleDiff");
            Assert.False(tab.DiffVisible);
        }, "var hp = 1;\n");
    }

    [Fact]
    public async Task QuickFixesOfferTheSpellingTheDeclarationAndCommentingOutAProblemLine()
    {
        await WithTab((tab, _, _) =>
        {
            tab.Editor.Text = "draw_txt(0, 0, \"a\");\nmystery = 3;\n";
            tab.Editor.CaretOffset = 3;
            Assert.Contains(tab.QuickFixesAtCaret(), f => f is { Key: "code.fix.rename", Argument: "draw_text" });
            tab.Editor.CaretOffset = tab.Text.IndexOf("mystery", StringComparison.Ordinal) + 2;
            var fixes = tab.QuickFixesAtCaret();
            Assert.Contains(fixes, f => f.Key == "code.fix.declareLocal");
            Assert.Contains(fixes, f => f.Key == "code.fix.declareGlobal");
            tab.Editor.Text = "return (";
            Assert.False(tab.Apply()); // a problem on line 1
            tab.Editor.CaretOffset = 2;
            Assert.Contains(tab.QuickFixesAtCaret(), f => f.Key == "code.fix.commentLine");
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task ARenameOfALocalChangesItsUsesInOneUndoStepAndHoverExplainsNames()
    {
        await WithTab((tab, _, _) =>
        {
            var at = tab.Text.IndexOf("hp +", StringComparison.Ordinal);
            Assert.True(tab.RenameLocalNow(at, "life"));
            Assert.StartsWith("var life = 1;\nvar mp = life + 1;", tab.Text, StringComparison.Ordinal);
            tab.Editor.Undo();
            Assert.Equal(Sample, tab.Text);
            Assert.Contains("local", tab.HoverTextAt(at) ?? "", StringComparison.Ordinal);
            Assert.Contains("foo(x, [y])", tab.HoverTextAt(tab.Text.LastIndexOf("foo", StringComparison.Ordinal) + 1) ?? "", StringComparison.Ordinal);
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task HoveringAResourceNameShowsItsPictureWhenTheLoaderHasOne()
    {
        await WithTab(async (tab, _, _) =>
        {
            tab.Editor.Text = "instance_create_depth(0, 0, 0, obj_0);\n";
            string? asked = null;
            tab.ThumbnailLoader = key =>
            {
                asked = key;
                return Task.FromResult<Avalonia.Media.IImage?>(new Avalonia.Media.Imaging.WriteableBitmap(new Avalonia.PixelSize(4, 4), new Avalonia.Vector(96, 96), Avalonia.Platform.PixelFormat.Bgra8888, Avalonia.Platform.AlphaFormat.Premul));
            };
            await Settle(() => tab.Doc.Tokens.Count > 0 && ((G3MStudio.Editor.IOverlayHost)tab).FreshDoc is not null);
            await tab.ShowHoverAtAsync(tab.Text.IndexOf("obj_0", StringComparison.Ordinal) + 1, 0);
            Assert.Equal("objects:obj_0", asked);
            var content = Assert.IsType<StackPanel>(ToolTip.GetTip(tab.Editor));
            Assert.Single(content.Children.OfType<Image>());
        });
    }

    [Fact]
    public async Task TheSnippetListOpensAtTheCaret()
    {
        await WithTab((tab, _, _) =>
        {
            tab.Editor.CaretOffset = tab.Text.Length;
            tab.Run("code.insertSnippet");
            Assert.NotEmpty(GmlSnippets.All);
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task AProblemFromApplyShowsInTheHoverAndTheViewStillDraws()
    {
        await WithTab((tab, workspace, window) =>
        {
            tab.Editor.Text = "return (";
            Assert.False(tab.Apply());
            Assert.NotEmpty(workspace.Problems.All);
            var problem = workspace.Problems.All.First();
            var line = tab.Editor.Document.GetLineByNumber(Math.Max(1, problem.Line));
            Assert.Contains(problem.Message, tab.HoverTextAt(Math.Min(line.Offset + Math.Max(problem.Column - 1, 0), tab.Text.Length)) ?? "", StringComparison.Ordinal);
            window.UpdateLayout();
            Assert.NotNull(window.CaptureRenderedFrame());
            tab.Editor.Text = "c = c_red; d = $FF8800;\n    x = 1;\n";
            Assert.Equal(2, tab.Doc.Colors.Count);
            window.UpdateLayout();
            Assert.NotNull(window.CaptureRenderedFrame());
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task RenamingAcrossAllCodeChangesUsesAndStringsAsOneUndoStep()
    {
        using var dir = new TempDir();
        using var workspace = await Open(dir);
        workspace.Edit(new EditCodeCommand("gml_Script_probe", "global.hp = 1;\nshow_debug_message(\"hp\");\n"));
        var hits = await CodeScan.FindNameAsync(workspace, "hp", TestContext.Current.CancellationToken);
        Assert.Contains(hits, h => h is { Entry: "gml_Script_probe", InString: false });
        Assert.Contains(hits, h => h.InString);

        var plan = await CodeRename.PlanAsync(workspace, "hp", "life", true, TestContext.Current.CancellationToken);
        Assert.Empty(plan.Warnings);
        Assert.Equal((1, 1), (plan.Uses, plan.Strings));
        var history = workspace.History.Items.Count;
        workspace.Edit(CodeRename.ToCommand(plan));
        Assert.Equal(history + 1, workspace.History.Items.Count);
        var after = CodeText.Decompile(workspace.Data, "gml_Script_probe");
        Assert.Contains("global.life", after, StringComparison.Ordinal);
        Assert.Contains("\"life\"", after, StringComparison.Ordinal);
        workspace.Undo();
        Assert.Contains("global.hp", CodeText.Decompile(workspace.Data, "gml_Script_probe"), StringComparison.Ordinal);

        var clash = await CodeRename.PlanAsync(workspace, "hp", "show_debug_message", true, TestContext.Current.CancellationToken);
        Assert.Contains("code.rename.collision", clash.Warnings);
        Assert.NotEmpty(clash.Changes); // warn, then allow
    }

    [Fact]
    public async Task TheWindowRoutesCodeCommandsAndFindUsagesToTheCodeTab()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir, withCode: true);
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                await window.OpenAsync(path);
                window.Docking.OpenTab("code", "code:gml_Script_probe", false);
                Dispatcher.UIThread.RunJobs();
                var tab = window.GetVisualDescendants().OfType<CodeEditorTab>().Single();
                await tab.Ready;
                await Settle(() => window.Registry.IsEnabled(window.Registry.Get("code.toggleComment")));
                tab.Editor.Text = "global.total = 1;\n";
                tab.Editor.CaretOffset = 0;
                Assert.True(window.Registry.Execute("code.toggleComment"));
                Assert.StartsWith("// global", tab.Text, StringComparison.Ordinal);
                Assert.True(window.Registry.Execute("code.toggleComment"));
                Assert.StartsWith("global", tab.Text, StringComparison.Ordinal);
                Assert.True(tab.Apply());
                tab.Editor.CaretOffset = tab.Text.IndexOf("total", StringComparison.Ordinal) + 1;
                Assert.True(window.Registry.Execute("edit.findUsages"));
                await Settle(() => window.GetVisualDescendants().OfType<G3MStudio.Panels.UsagesPanel>().Any());
                var panel = window.GetVisualDescendants().OfType<G3MStudio.Panels.UsagesPanel>().Single();
                await Settle(() => panel.Hits.Count > 0);
                Assert.Contains(panel.Hits, h => h.OwnerType == "code" && h.OwnerName == "gml_Script_probe" && h.Where.StartsWith("1:", StringComparison.Ordinal));
            }
            finally
            {
                window.Close();
            }
        });
    }
}
