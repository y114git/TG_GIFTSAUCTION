using G3MStudio.Core.Settings;

namespace G3MStudio.Tests;

public sealed class LayoutShiftsTests
{
    private static Dictionary<string, (double X, double Y)> Strip(params (string Id, double X)[] tabs) => tabs.ToDictionary(t => t.Id, t => (t.X, 0.0));

    [Fact]
    public void DroppedTabSettlesFromWhereItWasSeenNotWhereItStarted()
    {
        var layout = Strip(("a", 0), ("b", 100), ("c", 200)); // before the drag: a is first
        var seen = Strip(("a", 205), ("b", 0), ("c", 100)); // a is under the pointer at 205, b and c were shifted aside
        var after = Strip(("b", 0), ("c", 100), ("a", 200)); // the library reordered: a, b, c became b, c, a

        var moves = LayoutShifts.Between(LayoutShifts.Origin(layout, seen), after);

        var (item, dx, _) = Assert.Single(moves);
        Assert.Equal("a", item);
        Assert.Equal(5, dx); // 5 px, not the 200 px trip from the start
    }

    [Fact]
    public void WithoutADragTheLastLayoutIsTheOrigin()
    {
        var moves = LayoutShifts.Between(LayoutShifts.Origin(Strip(("a", 0), ("b", 100), ("c", 200)), null), Strip(("a", 0), ("c", 100)));
        var (item, dx, dy) = Assert.Single(moves);
        Assert.Equal(("c", 100.0, 0.0), (item, dx, dy));
    }

    [Fact]
    public void NewItemsAndUnmovedItemsAreNotAnimated()
    {
        var moves = LayoutShifts.Between(Strip(("a", 0), ("b", 100)), Strip(("a", 0.4), ("b", 100), ("n", 200)));
        Assert.Empty(moves);
    }
}
