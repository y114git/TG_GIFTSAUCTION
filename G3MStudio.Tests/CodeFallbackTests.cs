using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using G3MStudio.Core.Workspace;
using G3MStudio.Editor;

namespace G3MStudio.Tests;

public sealed class CodeFallbackTests
{
    private static string Fail() => throw new InvalidOperationException("Unexpected exception thrown in decompiler during control flow analysis\nat deep.stack.line");

    [Fact]
    public void AFailingDecompilerFallsBackToTheAssemblyWithTheFirstLineOfTheReason()
    {
        var load = CodeLoader.Load(Fail, () => "pushi.e 1\nexit.i", "gml_Script_parent");
        Assert.Equal(CodeMode.Assembly, load.Mode);
        Assert.Equal("pushi.e 1\nexit.i", load.Text);
        Assert.Equal("Unexpected exception thrown in decompiler during control flow analysis", load.Reason);
        Assert.Equal("gml_Script_parent", load.ParentName);
    }

    [Fact]
    public void AWorkingDecompilerIsUsedAsIs()
    {
        var load = CodeLoader.Load(() => "return 1;", () => throw new InvalidOperationException("not needed"));
        Assert.Equal((CodeMode.Decompiled, "return 1;", null), (load.Mode, load.Text, load.Reason));
    }

    [Fact]
    public void WhenTheAssemblyFailsToTheParentTextIsTheSecondFallbackAndNothingLeftThrows()
    {
        var parent = CodeLoader.Load(Fail, Fail, "gml_Script_parent", () => "function a() {}");
        Assert.Equal((CodeMode.Parent, "function a() {}"), (parent.Mode, parent.Text));
        Assert.Throws<InvalidOperationException>(() => CodeLoader.Load(Fail, Fail));
        Assert.Throws<InvalidOperationException>(() => CodeLoader.Load(Fail, Fail, "p", Fail));
    }

    [Fact]
    public void TheParentTextCanBeAskedForAfterTheAssemblyWasShown()
    {
        var load = CodeLoader.LoadParent("why", "gml_Script_parent", () => "function a() {}");
        Assert.Equal((CodeMode.Parent, "why", "gml_Script_parent"), (load.Mode, load.Reason, load.ParentName));
    }

    [Fact]
    public async Task TheTabShowsTheAssemblyUnderABannerAndTheParentRead_Only()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir, withCode: true);
        var userData = new UserData(dir.File("user"));
        using var workspace = await new WorkspaceService(userData, RecentFiles.Load(userData)).OpenAsync(path, null, TestContext.Current.CancellationToken);
        await HeadlessApp.Run(async () =>
        {
            var tab = new CodeEditorTab(workspace, "gml_Script_probe", parent => parent
                ? new CodeLoad("function parent() {}", CodeMode.Parent, "boom", "gml_Script_parent")
                : new CodeLoad("pushi.e 1", CodeMode.Assembly, "boom", "gml_Script_parent"));
            var window = new Window { Content = tab, Width = 700, Height = 400 };
            window.Show();
            await tab.Ready;
            Assert.Equal("pushi.e 1", tab.Text);
            Assert.True(tab.ShowsFallback);
            Assert.False(tab.Editor.IsReadOnly);
            var banner = tab.GetVisualDescendants().OfType<SelectableTextBlock>().Single(t => t.Name == "FallbackText");
            Assert.Equal("Decompiler failed: boom. Showing assembly.", banner.Text);
            var buttons = tab.GetVisualDescendants().OfType<Button>().Where(b => b.Name is "Retry" or "SideBySide" or "ShowParent").ToList();
            Assert.Equal(3, buttons.Count);

            buttons.Single(b => b.Name == "ShowParent").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            for (var i = 0; i < 300 && tab.Mode != CodeMode.Parent; i++)
            {
                Dispatcher.UIThread.RunJobs();
                await Task.Delay(10);
            }

            Assert.Equal("function parent() {}", tab.Text);
            Assert.True(tab.Editor.IsReadOnly);
            Assert.Contains("gml_Script_parent", banner.Text, StringComparison.Ordinal);
            window.Close();
        });
    }

    [Fact]
    public void QuickOpenListsEachNameOnceAndAScriptBeforeTheCodeOfTheSameName()
    {
        using var data = TestData.Create(objects: 1, withCode: true);
        var found = ResourceSearch.Search(data, "probe", []);
        Assert.Equal(found.Count, found.Select(f => f.Key).Distinct().Count());
        var script = found.ToList().FindIndex(f => f.Type == "scripts");
        var code = found.ToList().FindIndex(f => f.Type == "code");
        if (script >= 0 && code >= 0) Assert.True(script < code);
        Assert.Equal("{ }", G3MStudio.QuickOpenWindow.Badge("code"));
        Assert.NotEqual(G3MStudio.QuickOpenWindow.Badge("scripts"), G3MStudio.QuickOpenWindow.Badge("code"));
    }
}
