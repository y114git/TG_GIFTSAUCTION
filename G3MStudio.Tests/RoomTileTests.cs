using G3MLib.DataFile;
using G3MLib.DataFile.Models;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

#pragma warning disable CA2000 // the room and tiles belong to the data, which is disposed
public sealed class RoomTileTests
{
    [Fact]
    public void AnOldStyleTileMovesAndDeletesWithUndoAndFromTheJournal()
    {
        using var data = TestData.Create(0);
        var room = new GameMakerRoom { Name = data.Strings.MakeString("rm_old", out _) };
        room.Tiles.Add(new GameMakerRoom.Tile { InstanceID = 7, X = 10, Y = 20 });
        room.Tiles.Add(new GameMakerRoom.Tile { InstanceID = 8, X = 30, Y = 40 });
        data.Rooms.Add(room);
        var history = new EditHistory(data);

        var move = new RoomTileCommand("rm_old", RoomTileCommand.Move, 7, 15, 25);
        history.Execute(move);
        Assert.Equal((15, 25), (room.Tiles[0].X, room.Tiles[0].Y));
        history.Undo();
        Assert.Equal((10, 20), (room.Tiles[0].X, room.Tiles[0].Y));

        history.Execute(new RoomTileCommand("rm_old", RoomTileCommand.Delete, 7));
        Assert.Equal([8u], room.Tiles.Select(t => t.InstanceID));
        history.Undo();
        Assert.Equal([7u, 8u], room.Tiles.Select(t => t.InstanceID)); // back at its place

        var replay = EditCommand.Create(move.Kind, move.Args);
        replay.Apply(data);
        Assert.Equal((15, 25), (room.Tiles[0].X, room.Tiles[0].Y));
        Assert.Throws<InvalidOperationException>(() => new RoomTileCommand("rm_old", RoomTileCommand.Delete, 99).Apply(data));
        Assert.Contains(move.Touches!, t => t.Type == "Rooms" && t.Name == "rm_old");
    }
}
