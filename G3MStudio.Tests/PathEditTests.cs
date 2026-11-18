using G3MLib.DataFile;
using G3MLib.DataFile.Models;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

#pragma warning disable CA2000 // the data model owns what is added to it
public sealed class PathEditTests
{
    private static GameMakerPath Build(GameMakerData data)
    {
        var path = new GameMakerPath { Name = data.Strings.MakeString("pth_a") };
        foreach (var (x, y) in new[] { (0f, 0f), (10f, 4f), (20f, 8f) }) path.Points.Add(new GameMakerPath.PathPoint { X = x, Y = y });
        data.Paths.Add(path);
        return path;
    }

    private static float[] Xs(GameMakerPath p) => [.. p.Points.Select(q => q.X)];

    [Fact]
    public void ReverseFlipShiftDeleteAndFlagsUndoAsOneStep()
    {
        using var data = TestData.Create(0);
        var path = Build(data);
        var history = new EditHistory(data);
        history.Execute(new PathEditCommand("pth_a", PathEditCommand.Reverse, []));
        Assert.Equal([20f, 10f, 0f], Xs(path));
        history.Execute(new PathEditCommand("pth_a", PathEditCommand.FlipX, [0, 1]));
        Assert.Equal([10f, 20f, 0f], Xs(path));
        history.Execute(new PathEditCommand("pth_a", PathEditCommand.FlipY, []));
        Assert.Equal([0f, 4f, 8f], [.. path.Points.Select(q => q.Y)]);
        history.Execute(new PathEditCommand("pth_a", PathEditCommand.Shift, [2], 5, 1));
        Assert.Equal(5f, path.Points[2].X);
        history.Execute(new PathEditCommand("pth_a", PathEditCommand.Delete, [0, 1]));
        Assert.Single(path.Points);
        history.Execute(new PathEditCommand("pth_a", PathEditCommand.Closed, [], 1));
        Assert.True(path.IsClosed);
        for (var i = 0; i < 6; i++) history.Undo();
        Assert.Equal([0f, 10f, 20f], Xs(path));
        Assert.False(path.IsClosed);
    }

    [Fact]
    public void ReplayRebuildsAndLastPointIsProtected()
    {
        using var data = TestData.Create(0);
        var path = Build(data);
        var replay = EditCommand.Create(PathEditCommand.Name, new PathEditCommand("pth_a", PathEditCommand.Shift, [0, 2], 1.5f, -1).Args);
        replay.Apply(data);
        Assert.Equal([1.5f, 10f, 21.5f], Xs(path));
        replay.Revert(data);
        Assert.Equal([0f, 10f, 20f], Xs(path));
        Assert.Throws<InvalidOperationException>(() => new PathEditCommand("pth_a", PathEditCommand.Delete, []).Apply(data));
        Assert.Equal(7f, PathTurn.Flip(3, 0, 10));
    }
}
