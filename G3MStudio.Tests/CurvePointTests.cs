using G3MLib.DataFile;
using G3MLib.DataFile.Models;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

#pragma warning disable CA2000 // the data model owns what is added to it
public sealed class CurvePointTests
{
    private static GameMakerAnimationCurve Build(GameMakerData data)
    {
        var channel = new GameMakerAnimationCurve.Channel { Name = data.Strings.MakeString("c"), Points = [] };
        foreach (var (x, v) in new[] { (0f, 0f), (1f, 1f), (2f, 0.5f) }) channel.Points.Add(new GameMakerAnimationCurve.Channel.Point { X = x, Value = v });
        var curve = new GameMakerAnimationCurve { Name = data.Strings.MakeString("curve_a"), Channels = [channel] };
        data.AnimationCurves.Add(curve);
        return curve;
    }

    private static float[] Xs(GameMakerAnimationCurve curve) => [.. curve.Channels[0].Points.Select(p => p.X)];

    [Fact]
    public void AddMoveDeleteKeepTheListSortedAndUndoRestores()
    {
        using var data = TestData.Create(0);
        var curve = Build(data);
        var history = new EditHistory(data);
        history.Execute(new CurvePointCommand("curve_a", 0, CurvePointCommand.Add, -1, new CurveValue(1.5f, 2)));
        Assert.Equal([0f, 1f, 1.5f, 2f], Xs(curve));
        history.Execute(new CurvePointCommand("curve_a", 0, CurvePointCommand.Move, 0, new CurveValue(3, 9)));
        Assert.Equal([1f, 1.5f, 2f, 3f], Xs(curve));
        Assert.Equal(9f, curve.Channels[0].Points[3].Value);
        history.Execute(new CurvePointCommand("curve_a", 0, CurvePointCommand.Delete, 1));
        Assert.Equal([1f, 2f, 3f], Xs(curve));
        history.Undo();
        history.Undo();
        history.Undo();
        Assert.Equal([0f, 1f, 2f], Xs(curve));
        Assert.Equal(0f, curve.Channels[0].Points[0].Value);
    }

    [Fact]
    public void HandlesAndTypeReplayFromArgsAndUndo()
    {
        using var data = TestData.Create(0);
        var curve = Build(data);
        var handles = EditCommand.Create(CurvePointCommand.Name, new CurvePointCommand("curve_a", 0, CurvePointCommand.Handles, 1, new CurveValue(Handles: [0.1f, 0.2f, 0.3f, 0.4f])).Args);
        handles.Apply(data);
        var p = curve.Channels[0].Points[1];
        Assert.Equal((0.1f, 0.2f, 0.3f, 0.4f), (p.BezierX0, p.BezierY0, p.BezierX1, p.BezierY1));
        handles.Revert(data);
        Assert.Equal(0f, p.BezierX0);

        var type = new CurvePointCommand("curve_a", 0, CurvePointCommand.Type, 0, new CurveValue(2));
        type.Apply(data);
        Assert.Equal(GameMakerAnimationCurve.Channel.CurveType.Bezier, curve.Channels[0].Curve);
        type.Revert(data);
        Assert.Equal(GameMakerAnimationCurve.Channel.CurveType.Linear, curve.Channels[0].Curve);
        Assert.Throws<InvalidOperationException>(() => new CurvePointCommand("curve_a", 0, CurvePointCommand.Delete, 9).Apply(data));
    }
}
