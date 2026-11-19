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
    public void AChildCodeEntryIsToldApartByItsName()
    {
        using var parent = new GameMakerCode { Name = new GameMakerString("gml_Object_o_Create_0") };
        static GameMakerCode Child(string name, GameMakerCode parent) => new() { Name = new GameMakerString(name), ParentEntry = parent };
        using var anonymous = Child("gml_Script_anon_gml_Object_o_Create_0_1", parent);
        using var structure = Child("gml_Script____struct___654_gml_Object_o_Create_0", parent);
        using var inner = Child("gml_Script_helper_gml_Object_o_Create_0", parent);
        Assert.Equal(ChildEntryKind.Anonymous, ChildEntries.KindOf(anonymous));
        Assert.Equal(ChildEntryKind.Struct, ChildEntries.KindOf(structure));
        Assert.Equal(ChildEntryKind.Inner, ChildEntries.KindOf(inner));
        Assert.Null(ChildEntries.KindOf(parent));
        Assert.Null(ChildEntries.KindOf("text"));
        var settings = new G3MStudio.Core.Settings.AppSettings { ExplorerInnerFunctions = G3MStudio.Core.Settings.EntryVisibility.Hide };
        Assert.Equal(G3MStudio.Core.Settings.EntryVisibility.Dim, ChildEntries.VisibilityOf(anonymous, settings));
        Assert.Equal(G3MStudio.Core.Settings.EntryVisibility.Hide, ChildEntries.VisibilityOf(inner, settings));
        Assert.Equal(G3MStudio.Core.Settings.EntryVisibility.Show, ChildEntries.VisibilityOf(parent, settings));
    }

    [Fact]
    public void ADimmedEntryKeepsItsRowAndAnExactNameFindsAHiddenOne()
    {
        var model = new ExplorerModel(key => key);
        using var data = TestData.Create(6);
        model.Load(data);
        static bool Probe(object? item) => item is GameMakerGameObject o && ResourceTypes.NameOf(o) == "obj_3";
        var before = model.Rows.Count(r => r.IsItem && r.Item is GameMakerGameObject);
        model.DimItem = Probe;
        model.RebuildRows();
        Assert.Equal(before, model.Rows.Count(r => r.IsItem && r.Item is GameMakerGameObject));
        Assert.Equal(1, model.Rows.Count(r => r.Dim));
        model.DimItem = null;
        model.HideItem = Probe;
        Assert.Equal(before - 1, model.Rows.Count(r => r.IsItem && r.Item is GameMakerGameObject));
        model.SetFilter("obj_3");
        Assert.Contains(model.Rows, r => r.IsItem && r.Text == "obj_3");
    }

    [Fact]
    public void HiddenEntriesAreCountedAndAJumpRevealsOne()
    {
        var model = new ExplorerModel(key => key);
        using var data = TestData.Create(6);
        model.Load(data);
        static bool Probe(object? item) => item is GameMakerGameObject o && ResourceTypes.NameOf(o) == "obj_3";
        model.HideItem = Probe;
        Assert.Equal(1, model.Hidden);
        Assert.DoesNotContain(model.Rows, r => r.IsItem && r.Text == "obj_3");
        Assert.True(model.Reveal(ResourceTypes.KeyOf("objects", "obj_3")));
        Assert.Contains(model.Rows, r => r.IsItem && r.Text == "obj_3");
        Assert.Equal(0, model.Hidden);
        model.HideItem = o => Probe(o) || o is GameMakerGameObject;
        Assert.DoesNotContain(model.Rows, r => r.IsItem && r.Text == "obj_3"); // new settings end the reveal
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
