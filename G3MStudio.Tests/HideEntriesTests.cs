using G3MLib.DataFile.Models;
using G3MStudio.Core.Workspace;
using G3MStudio.Editor;

namespace G3MStudio.Tests;

public sealed class HideEntriesTests
{
    [Fact]
    public void TheExplorerLeavesOutWhatTheHidePredicateSaysYesTo()
    {
        var model = new ExplorerModel(key => key);
        using var data = TestData.Create(6);
        model.Load(data);
        var before = model.Rows.Count(r => r.IsItem && r.Item is GameMakerGameObject);
        Assert.True(before > 0);
        model.HideItem = item => item is GameMakerGameObject o && ResourceTypes.NameOf(o) == "obj_3";
        Assert.Equal(before - 1, model.Rows.Count(r => r.IsItem && r.Item is GameMakerGameObject));
        model.HideItem = null;
        Assert.Equal(before, model.Rows.Count(r => r.IsItem && r.Item is GameMakerGameObject));
    }

    [Fact]
    public void ACodeEntryWithAParentHasNoTextOfItsOwn()
    {
        var health = new ResourceHealth();
        using var parent = new GameMakerCode { Name = new GameMakerString("gml_GlobalScript_a") };
        using var child = new GameMakerCode { Name = new GameMakerString("gml_Script_a"), ParentEntry = parent };
        Assert.True(health.Hides(child));
        Assert.False(health.Hides(parent));
        using var whole = new GameMakerFont { Texture = new GameMakerTexturePageItem() };
        using var cut = new GameMakerFont();
        Assert.False(ResourceHealth.IsBroken(whole));
        Assert.True(ResourceHealth.IsBroken(cut));
    }

    [Fact]
    public async Task ADoubleClickOnAUseSelectsThatUseInTheCode()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir, withCode: true);
        var userData = new UserData(dir.File("user"));
        using var workspace = await new WorkspaceService(userData, RecentFiles.Load(userData)).OpenAsync(path, null, TestContext.Current.CancellationToken);
        await HeadlessApp.Run(async () =>
        {
            var tab = new CodeEditorTab(workspace, "gml_Script_probe");
            var window = new Avalonia.Controls.Window { Content = tab, Width = 900, Height = 600 };
            window.Show();
            await tab.Ready;
            tab.Editor.Text = "a(foo);\nfoo(1);\nxfoo(2);\nfoo(3);";
            tab.SelectUse("foo", 1, 0);
            Assert.Equal(8, tab.Editor.SelectionStart); // the second whole word; "xfoo" is another name
            tab.SelectUse("foo", 0, 4);
            Assert.Equal(25, tab.Editor.SelectionStart);
            tab.SelectUse("foo", 9, 0); // fewer uses than asked for: the first one
            Assert.Equal(2, tab.Editor.SelectionStart);
            window.Close();
            return;
        });
    }
}
