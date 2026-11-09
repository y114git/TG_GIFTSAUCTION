using G3MLib.DataFile.Models;

namespace G3MStudio.Core.Workspace;

/// <summary>
/// Where the tiles of a tile set lie on its texture (UTMT-BG-03, UTMT-BG-04): every tile sits in a cell of its size plus the output border on both sides plus the separation, and the tile starts one border into the cell.
/// shortcut: this follows the exported layout of GameMaker 2.3 and later; a tile set packed some other way picks the wrong tile, change this if a corpus game shows one.
/// </summary>
public static class TileGeometry
{
    private static (int W, int H) Cell(GameMakerBackground b) => ((int)(b.GMS2TileWidth + 2 * b.GMS2OutputBorderX + b.GMS2TileSeparationX), (int)(b.GMS2TileHeight + 2 * b.GMS2OutputBorderY + b.GMS2TileSeparationY));

    /// <summary>The tile index at a pixel of the texture item, or -1 outside every tile.</summary>
    public static int IndexAt(GameMakerBackground b, int x, int y)
    {
        var (cw, ch) = Cell(b);
        if (b.GMS2TileColumns == 0 || cw <= 0 || ch <= 0 || x < 0 || y < 0) return -1;
        var (col, row) = (x / cw, y / ch);
        if (col >= b.GMS2TileColumns) return -1;
        var index = row * (int)b.GMS2TileColumns + col;
        return index < b.GMS2TileCount ? index : -1;
    }

    /// <summary>The pixels of a tile on the texture item: left, top, width, height.</summary>
    public static (int X, int Y, int W, int H) RectOf(GameMakerBackground b, int index)
    {
        var (cw, ch) = Cell(b);
        var columns = Math.Max(1, (int)b.GMS2TileColumns);
        return (index % columns * cw + (int)b.GMS2OutputBorderX, index / columns * ch + (int)b.GMS2OutputBorderY, (int)b.GMS2TileWidth, (int)b.GMS2TileHeight);
    }
}
