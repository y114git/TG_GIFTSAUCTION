using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

public sealed class LayerOrderTests
{
    private static readonly (uint Id, int Depth)[] Layers = [(1, -100), (2, 0), (3, 100), (4, 200)];

    [Fact]
    public void MovingToTheFrontPutsTheLayerBelowTheFirstDepth() => Assert.Equal([(4u, -200)], LayerOrder.Move(Layers, 3, 0));

    [Fact]
    public void MovingToTheBackPutsTheLayerBehindTheLast() => Assert.Equal([(1u, 300)], LayerOrder.Move(Layers, 0, 3));

    [Fact]
    public void MovingBetweenTwoNeighboursTakesTheMiddleDepth() => Assert.Equal([(1u, 50)], LayerOrder.Move(Layers, 0, 1));

    [Fact]
    public void WithNoRoomBetweenNeighboursEveryLayerIsSpacedAgain()
    {
        (uint, int)[] tight = [(1, 0), (2, 1), (3, 2)];
        var changes = LayerOrder.Move(tight, 2, 1);
        var depth = changes.ToDictionary(c => c.Id, c => c.Depth);
        var final = tight.Select(l => (l.Item1, Depth: depth.GetValueOrDefault(l.Item1, l.Item2))).OrderBy(l => l.Depth).Select(l => l.Item1);
        Assert.Equal([1u, 3, 2], final);
    }

    [Fact]
    public void NoMoveOrBadIndexChangesNothing()
    {
        Assert.Empty(LayerOrder.Move(Layers, 1, 1));
        Assert.Empty(LayerOrder.Move(Layers, 9, 0));
    }
}
