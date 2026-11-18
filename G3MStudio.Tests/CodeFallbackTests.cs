using Avalonia.Controls;
using Avalonia.VisualTree;
using G3MStudio.Core.Workspace;
using G3MStudio.Editor;
using G3MStudio.Localization;

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
    public void NothingLeftToShowThrowsWhenTheAssemblyFailsToo()
    {
        Assert.Throws<InvalidOperationException>(() => CodeLoader.Load(Fail, Fail));
    }

    [Fact]
    public async Task TheTabShowsTheAssemblyUnderABannerAndGoToParentAsksForTheParentTab()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir, withCode: true);
        var userData = new UserData(dir.File("user"));
        using var workspace = await new WorkspaceService(userData, RecentFiles.Load(userData)).OpenAsync(path, null, TestContext.Current.CancellationToken);
        await HeadlessApp.Run(async () =>
        {
            var tab = new CodeEditorTab(workspace, "gml_Script_probe", () => new CodeLoad("pushi.e 1", CodeMode.Assembly, "boom", "gml_Script_parent"));
            var window = new Window { Content = tab, Width = 700, Height = 400 };
            window.Show();
            await tab.Ready;
            Assert.Equal("pushi.e 1", tab.Text);
            Assert.True(tab.ShowsFallback);
            Assert.False(tab.Editor.IsReadOnly);
            var banner = tab.GetVisualDescendants().OfType<SelectableTextBlock>().Single(t => t.Name == "FallbackText");
            Assert.Equal("Decompiler failed: boom. Showing assembly.", banner.Text);
            var buttons = tab.GetVisualDescendants().OfType<Button>().Where(b => b.Name is "Retry" or "ShowParent").ToList();
            Assert.Equal(2, buttons.Count);

            string? requested = null;
            tab.GoToRequested += key => requested = key;
            buttons.Single(b => b.Name == "ShowParent").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Assert.Equal("code:gml_Script_parent", requested);
            Assert.Equal("pushi.e 1", tab.Text); // the parent opens in its own tab; this one keeps its text
            window.Close();
        });
    }

    [Fact]
    public async Task TheToolbarButtonsFollowALanguageChange()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir, withCode: true);
        var userData = new UserData(dir.File("user"));
        using var workspace = await new WorkspaceService(userData, RecentFiles.Load(userData)).OpenAsync(path, null, TestContext.Current.CancellationToken);
        await HeadlessApp.Run(async () =>
        {
            Loc.Current.SetLanguage("en");
            var tab = new CodeEditorTab(workspace, "gml_Script_probe", () => new CodeLoad("pushi.e 1", CodeMode.Assembly, "boom", "gml_Script_parent"));
            var window = new Window { Content = tab, Width = 700, Height = 400 };
            try
            {
                window.Show();
                await tab.Ready;
                Loc.Current.SetLanguage("ru");
                string Of(string name) => (string)tab.GetVisualDescendants().OfType<Button>().Single(b => b.Name == name).Content!;
                Assert.Equal(Loc.Current["code.save"], Of("Apply"));
                Assert.Equal(Loc.Current["code.revert"], Of("Revert"));
                Assert.Equal(Loc.Current["code.retry"], Of("Retry"));
                Assert.Equal(Loc.Current["code.showParent"], Of("ShowParent"));
                Assert.Equal(Loc.Current["code.showAssembly"], Of("AssemblyToggle"));
                Assert.Equal(Loc.Current["editor.typingInsert"], tab.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "TypingMode").Text);
            }
            finally
            {
                Loc.Current.SetLanguage("en");
                window.Close();
            }
        });
    }

    [Fact]
    public Task TheTextFileButtonsFollowALanguageChange() => HeadlessApp.Run(() =>
    {
        Loc.Current.SetLanguage("en");
        var view = new TextFileView("abc");
        var window = new Window { Content = view, Width = 500, Height = 300 };
        try
        {
            window.Show();
            Loc.Current.SetLanguage("ru");
            Assert.Equal(Loc.Current["code.save"], view.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "TextApply").Content);
        }
        finally
        {
            Loc.Current.SetLanguage("en");
            window.Close();
        }

        return Task.CompletedTask;
    });

    [Fact]
    public void AScriptAndTheCodeOfTheSameNameGetDifferentBadges()
    {
        Assert.Equal("{ }", G3MStudio.GoToWindow.Badge("code"));
        Assert.NotEqual(G3MStudio.GoToWindow.Badge("scripts"), G3MStudio.GoToWindow.Badge("code"));
    }
}
