using G3MStudio.Core.Workspace;
using G3MStudio.History;

namespace G3MStudio.Tests;

public sealed class HistoryDescribeTests
{
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
