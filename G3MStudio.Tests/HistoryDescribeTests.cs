using G3MStudio.Core.Workspace;
using G3MStudio.History;

namespace G3MStudio.Tests;

public sealed class HistoryDescribeTests
{
    [Fact]
    public void AGoToRowCarriesTheFullDescriptionInItsTip()
    {
        var text = "Converts a value to a string, using the current locale and the given number of decimals.";
        var tip = GoToWindow.Tip(new G3MStudio.Core.Navigation.GoToEntry(G3MStudio.Core.Navigation.GoToKind.Reference, "string_format", "ref:string_format", "", text));
        Assert.EndsWith(text, tip, StringComparison.Ordinal);
    }

    [Fact]
    public void EveryEditKindHasAReadableLabel()
    {
        var loaded = typeof(EditCommand).Assembly.GetTypes().Count(t => !t.IsAbstract && t.IsSubclassOf(typeof(EditCommand)));
        Assert.Equal(loaded, EditCommand.Kinds.Count);
        Assert.DoesNotContain("", EditCommand.Kinds);
        Assert.All(EditCommand.Kinds, kind => Assert.NotEqual("history.kind." + kind, HistoryView.KindLabel(kind)));
    }

    [Fact]
    public void ResourceListEditsAndGroupsAroundThemReadAsWhatTheyDo()
    {
        var delete = new ResourceListCommand("objects", ResourceListCommand.Delete, "obj_a");
        var create = new ResourceListCommand("fonts", ResourceListCommand.New, "fnt_a");
        Assert.Equal("Delete Object: obj_a", HistoryView.Describe(delete));
        Assert.Equal("New Font: fnt_a", HistoryView.Describe(create));
        Assert.Equal("Delete Object: obj_a", HistoryView.Describe(new GroupCommand([new ClearReferencesCommand("objects", "obj_a"), delete])));
        Assert.Equal("New Font: fnt_a", HistoryView.Describe(new GroupCommand([create, new EditCodeCommand("x", "y")])));
        Assert.Equal("Delete 2 resources", HistoryView.Describe(new GroupCommand([delete, new ResourceListCommand("scripts", ResourceListCommand.Delete, "scr_a")])));
        Assert.Equal("New 2 resources", HistoryView.Describe(new GroupCommand([new GroupCommand([create]), new GroupCommand([new ResourceListCommand("objects", ResourceListCommand.New, "obj_b")])])));
        Assert.Equal("Delete Tile Sets: ts_a", HistoryView.Describe(new ResourceListCommand("tileSets", ResourceListCommand.Delete, "ts_a")));
        Assert.Equal("Copy Sprite: spr_b", HistoryView.Describe(new CopyResourcesCommand([], default!, [new CopyStep("sprites", "spr_b", false)], 0)));
    }
}
