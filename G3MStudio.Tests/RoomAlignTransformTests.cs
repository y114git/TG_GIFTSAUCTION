using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

public sealed class RoomAlignTransformTests
{
    private static readonly RoomBox[] Three = [new(0, 0, 10, 10), new(20, 5, 40, 15), new(100, 30, 110, 60)];

    [Fact]
    public void AlignMovesEveryBoxToTheEdgeOrCentreOfTheGroup()
    {
        Assert.Equal([(0, 0), (-20, 0), (-100, 0)], RoomAlign.Align(Three, AlignMode.Left));
        Assert.Equal([(100, 0), (70, 0), (0, 0)], RoomAlign.Align(Three, AlignMode.Right));
        Assert.Equal([(0, 0), (0, -5), (0, -30)], RoomAlign.Align(Three, AlignMode.Top));
        Assert.Equal([(0, 50), (0, 45), (0, 0)], RoomAlign.Align(Three, AlignMode.Bottom));
        Assert.Equal([(50, 0), (25, 0), (-50, 0)], RoomAlign.Align(Three, AlignMode.CenterHorizontal));
        Assert.Equal((0, 25), RoomAlign.Align(Three, AlignMode.MiddleVertical)[0]);
    }

    [Fact]
    public void AlignOfOneBoxMovesNothing() => Assert.Equal([(0, 0)], RoomAlign.Align([new RoomBox(3, 4, 5, 6)], AlignMode.Left));

    [Fact]
    public void DistributeMakesTheGapsEqualAndKeepsTheOutermost()
    {
        var moves = RoomAlign.Distribute(Three, DistributeMode.Horizontal);
        Assert.Equal(0, moves[0].Dx);
        Assert.Equal(0, moves[2].Dx);
        var placed = Three.Select((b, i) => b with { Left = b.Left + moves[i].Dx, Right = b.Right + moves[i].Dx }).OrderBy(b => b.Left).ToList();
        Assert.Equal(placed[1].Left - placed[0].Right, placed[2].Left - placed[1].Right, 1);
        Assert.All(moves, m => Assert.Equal(0, m.Dy));
    }

    [Fact]
    public void DistributeWorksInInputOrderNotPositionOrder()
    {
        RoomBox[] boxes = [new(100, 0, 110, 10), new(0, 0, 10, 10), new(20, 0, 30, 10)];
        var moves = RoomAlign.Distribute(boxes, DistributeMode.Horizontal);
        Assert.Equal((0, 0), moves[0]);
        Assert.Equal((0, 0), moves[1]);
        Assert.Equal((30, 0), moves[2]);
    }

    [Fact]
    public void DistributeVerticallyMovesOnlyY() => Assert.All(RoomAlign.Distribute(Three, DistributeMode.Vertical), m => Assert.Equal(0, m.Dx));

    [Fact]
    public void RotateOrbitsCounterClockwiseAndAddsToTheRotation()
    {
        var turned = RoomTransform.Rotate(new Placement(10, 0, 1, 1, 350), 0, 0, 90);
        Assert.Equal(0, turned.X, 6);
        Assert.Equal(-10, turned.Y, 6);
        Assert.Equal(80, turned.Rotation, 6);
    }

    [Fact]
    public void AngleOfPointsCounterClockwise()
    {
        Assert.Equal(0, RoomTransform.AngleOf(0, 0, 5, 0), 6);
        Assert.Equal(90, RoomTransform.AngleOf(0, 0, 0, -5), 6);
        Assert.Equal(15, RoomTransform.SnapAngle(17, 15));
    }

    [Fact]
    public void ScaleAboutAnAnchorMovesThePositionAndScales()
    {
        var scaled = RoomTransform.Scale(new Placement(30, 20, 1, 2, 0), 10, 10, 2, 0.5);
        Assert.Equal(new Placement(50, 15, 2, 1, 0), scaled);
    }

    [Fact]
    public void CornerDragGivesFactorsAgainstTheOppositeCorner()
    {
        var box = new RoomBox(10, 10, 30, 20);
        var (fx, fy, ax, ay) = RoomTransform.Factors(box, Handle.BottomRight, 50, 30, false, false);
        Assert.Equal((2d, 2d, 10d, 10d), (fx, fy, ax, ay));
        var (ux, uy, _, _) = RoomTransform.Factors(box, Handle.BottomRight, 50, 12, true, false);
        Assert.Equal((2d, 2d), (ux, uy));
        var (cx, _, centerX, _) = RoomTransform.Factors(box, Handle.Right, 40, 15, false, true);
        Assert.Equal((2d, 20d), (cx, centerX));
    }

    [Fact]
    public void EdgeDragScalesOneAxisAndDraggingPastTheAnchorMirrors()
    {
        var box = new RoomBox(10, 10, 30, 20);
        var (fx, fy, _, ay) = RoomTransform.Factors(box, Handle.Right, 20, 99, false, false);
        Assert.Equal((0.5d, 1d, 15d), (fx, fy, ay));
        Assert.True(RoomTransform.Factors(box, Handle.Right, 0, 0, false, false).Fx < 0);
        Assert.Equal(0.01, Math.Abs(RoomTransform.Factors(box, Handle.Right, 10, 0, false, false).Fx), 6);
    }
}
