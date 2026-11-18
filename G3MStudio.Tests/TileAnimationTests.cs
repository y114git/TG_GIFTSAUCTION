using G3MLib.DataFile;
using G3MLib.DataFile.Models;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

#pragma warning disable CA2000 // the data model owns what is added to it
public sealed class TileAnimationTests
{
    private static GameMakerBackground Build(GameMakerData data, int tiles = 3)
    {
        var set = new GameMakerBackground { Name = data.Strings.MakeString("ts_a"), GMS2TileCount = (uint)tiles, GMS2ItemsPerTileCount = 1 };
        for (var i = 0; i < tiles; i++) set.GMS2TileIds.Add(new GameMakerBackground.TileID { ID = (uint)i });
        data.Backgrounds.Add(set);
        return set;
    }

    [Fact]
    public void FramesGrowTheCountPadWithTheLastFrameAndUndoRestores()
    {
        using var data = TestData.Create(0);
        var set = Build(data);
        var history = new EditHistory(data);
        history.Execute(new TileAnimationCommand("ts_a", TileAnimationCommand.Frames, 1, [1, 2, 0]));
        Assert.Equal(3u, set.GMS2ItemsPerTileCount);
        Assert.Equal([0u, 0u, 0u, 1u, 2u, 0u, 2u, 2u, 2u], set.GMS2TileIds.Select(t => t.ID));
        history.Execute(new TileAnimationCommand("ts_a", TileAnimationCommand.Speed, length: 33333));
        Assert.Equal(33333, set.GMS2FrameLength);
        history.Undo();
        history.Undo();
        Assert.Equal(1u, set.GMS2ItemsPerTileCount);
        Assert.Equal([0u, 1u, 2u], set.GMS2TileIds.Select(t => t.ID));
        Assert.Equal(66666, set.GMS2FrameLength);
    }

    [Fact]
    public void BadInputIsRefusedAndReplayRebuilds()
    {
        using var data = TestData.Create(0);
        Build(data);
        Assert.Throws<InvalidOperationException>(() => new TileAnimationCommand("ts_a", TileAnimationCommand.Frames, 9, [1]).Apply(data));
        Assert.Throws<InvalidOperationException>(() => new TileAnimationCommand("ts_a", TileAnimationCommand.Frames, 0, []).Apply(data));
        Assert.Throws<InvalidOperationException>(() => new TileAnimationCommand("ts_a", TileAnimationCommand.Speed, length: 0).Apply(data));
        var replay = EditCommand.Create(TileAnimationCommand.Name, new TileAnimationCommand("ts_a", TileAnimationCommand.Frames, 0, [0, 1]).Args);
        replay.Apply(data);
        replay.Revert(data);
        Assert.Equal(15.0, Math.Round(Editor.TileAnimationView.FramesPerSecond(66666), 0));
    }
}
