using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

public sealed class ResourceMoveTests
{
    private static string Order(G3MLib.DataFile.GameMakerData data) => string.Join(",", data.GameObjects.Select(o => o.Name.Content));

    [Fact]
    public void AnEntryMovesOnePlaceAndBackAndStopsAtTheEnds()
    {
        using var data = TestData.Create(3);
        var history = new EditHistory(data);
        Assert.Equal("obj_0,obj_1,obj_2", Order(data));
        Assert.False(ResourceMoveCommand.CanMove(data, "objects", "obj_0", -1));
        Assert.False(ResourceMoveCommand.CanMove(data, "objects", "obj_2", 1));
        Assert.False(ResourceMoveCommand.CanMove(data, "strings", "x", 1)); // data types are not reordered
        Assert.True(ResourceMoveCommand.CanMove(data, "objects", "obj_1", -1));

        var command = new ResourceMoveCommand("objects", "obj_1", -1);
        history.Execute(command);
        Assert.Equal("obj_1,obj_0,obj_2", Order(data));
        history.Undo();
        Assert.Equal("obj_0,obj_1,obj_2", Order(data));
        history.Redo();
        Assert.Equal("obj_1,obj_0,obj_2", Order(data));

        // The journal rebuilds the same move from its arguments.
        var replay = EditCommand.Create(command.Kind, command.Args);
        history.Undo();
        replay.Apply(data);
        Assert.Equal("obj_1,obj_0,obj_2", Order(data));
        Assert.Throws<InvalidOperationException>(() => new ResourceMoveCommand("objects", "obj_1", -1).Apply(data));
    }
}
