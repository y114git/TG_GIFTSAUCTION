using G3MLib.DataFile.Models;
using G3MStudio.Editor;

namespace G3MStudio.Tests;

public sealed class PageHitTests
{
    [Fact]
    public void TheSmallestItemUnderThePointIsFoundAndOtherPagesAreIgnored()
    {
        var page = new GameMakerEmbeddedTexture();
        var other = new GameMakerEmbeddedTexture();
        var large = new GameMakerTexturePageItem { TexturePage = page, SourceX = 0, SourceY = 0, SourceWidth = 100, SourceHeight = 100 };
        var small = new GameMakerTexturePageItem { TexturePage = page, SourceX = 10, SourceY = 10, SourceWidth = 20, SourceHeight = 20 };
        var elsewhere = new GameMakerTexturePageItem { TexturePage = other, SourceX = 10, SourceY = 10, SourceWidth = 5, SourceHeight = 5 };
        var items = new[] { large, small, elsewhere };
        Assert.Same(small, TexturePagePreview.ItemAt(page, items, 15, 15));
        Assert.Same(large, TexturePagePreview.ItemAt(page, items, 50, 50));
        Assert.Null(TexturePagePreview.ItemAt(page, items, 100, 100)); // the far edge is outside
        Assert.Same(large, TexturePagePreview.ItemAt(page, items, 0, 0));
    }

    [Fact]
    public void TheTileSetLineGivesSizeColumnsBorderAndSeparation()
    {
        using var set = new GameMakerBackground { GMS2TileWidth = 16, GMS2TileHeight = 24, GMS2TileColumns = 8, GMS2TileCount = 64, GMS2OutputBorderX = 1, GMS2OutputBorderY = 2, GMS2TileSeparationX = 3, GMS2TileSeparationY = 4 };
        Assert.Equal("Tile size: 16 x 24. Columns: 8. Tiles: 64. Border 1, 2. Separation 3, 4.", TileSetInfoView.Describe(set));
    }
}
