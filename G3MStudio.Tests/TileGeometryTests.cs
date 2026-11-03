using G3MLib.DataFile.Models;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

public sealed class TileGeometryTests
{
    private static GameMakerBackground Set() => new()
    {
        GMS2TileWidth = 16,
        GMS2TileHeight = 16,
        GMS2OutputBorderX = 2,
        GMS2OutputBorderY = 2,
        GMS2TileColumns = 4,
        GMS2TileCount = 6,
    };

    [Fact]
    public void ATilePixelFindsItsIndexAndTheIndexFindsItsRectangle()
    {
        using var set = Set(); // cells of 20 x 20, tiles one border into them
        Assert.Equal(0, TileGeometry.IndexAt(set, 3, 3));
        Assert.Equal(1, TileGeometry.IndexAt(set, 25, 5));
        Assert.Equal(4, TileGeometry.IndexAt(set, 5, 25));
        Assert.Equal(-1, TileGeometry.IndexAt(set, 45, 25)); // row 1 column 2 is tile 6, past the count of 6
        Assert.Equal(-1, TileGeometry.IndexAt(set, 100, 5)); // past the last column
        Assert.Equal(-1, TileGeometry.IndexAt(set, -1, 5));
        Assert.Equal((22, 2, 16, 16), TileGeometry.RectOf(set, 1));
        Assert.Equal((2, 22, 16, 16), TileGeometry.RectOf(set, 4));
        Assert.Equal(1, TileGeometry.IndexAt(set, TileGeometry.RectOf(set, 1).X + 5, TileGeometry.RectOf(set, 1).Y + 5));
    }
}
