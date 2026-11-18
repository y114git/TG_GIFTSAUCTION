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

    [Fact]
    public void ARoomCellOfASheetStoredWithoutItsEmptyMarginIsMovedBackAndCutToWhatIsStored()
    {
        using var set = Set(); // cells of 20 x 20 with the tile one border in: tile 1 is the sheet pixels 22..38
        // The page holds the sheet from its pixel 26: what comes before is empty and not stored.
        using var item = new GameMakerTexturePageItem { SourceX = 100, SourceY = 50, SourceWidth = 60, SourceHeight = 40, TargetX = 26, TargetY = 0, TargetWidth = 60, TargetHeight = 40, BoundingWidth = 86, BoundingHeight = 40 };
        using var layer = new GameMakerRoom.Layer { Data = new GameMakerRoom.Layer.LayerTilesData { Background = set, TilesX = 2, TilesY = 1, TileData = [[1, 2]] } };
        var cells = G3MStudio.Rooms.RoomScene.TileRects(layer, item).ToList();
        Assert.Equal(2, cells.Count);
        Assert.Equal(new Avalonia.Rect(0, 2, 12, 16), cells[0].Source); // the stored part only, relative to the stored picture (the page offset is added later)
        Assert.Equal(new Avalonia.Rect(4, 0, 12, 16), cells[0].Dest); // and drawn where that part falls inside the tile
        Assert.Equal(new Avalonia.Rect(16, 2, 16, 16), cells[1].Source);
        Assert.Equal(new Avalonia.Rect(16, 0, 16, 16), cells[1].Dest);

        // A mirrored cell is drawn flipped about the middle of the piece, so the piece must sit where the mirrored cell puts it: the missing margin ends up on the right.
        using var mirrored = new GameMakerRoom.Layer { Data = new GameMakerRoom.Layer.LayerTilesData { Background = set, TilesX = 1, TilesY = 1, TileData = [[1u | (1u << 28)]] } };
        var (_, mirroredDest, mirroredFlags) = G3MStudio.Rooms.RoomScene.TileRects(mirrored, item).Single();
        Assert.Equal(new Avalonia.Rect(0, 0, 12, 16), mirroredDest);
        Assert.Equal(1, mirroredFlags);
    }
}
