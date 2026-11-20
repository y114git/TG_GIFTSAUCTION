using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using G3MLib.DataFile.Models;

namespace G3MStudio.Editor;

/// <summary>The tiles of a tile set drawn in another number of columns, for looking only: the tile set, its picture and its tile numbers are not changed.</summary>
public static class TileArrange
{
    /// <summary>A copy of the tile set's geometry that the view can give another column count. It is not part of the data and holds nothing that needs disposing.</summary>
#pragma warning disable CA2000 // a plain geometry holder: never added to the data, nothing unmanaged inside
    public static GameMakerBackground ViewOf(GameMakerBackground set) => new()
    {
        GMS2TileWidth = set.GMS2TileWidth,
        GMS2TileHeight = set.GMS2TileHeight,
        GMS2OutputBorderX = set.GMS2OutputBorderX,
        GMS2OutputBorderY = set.GMS2OutputBorderY,
        GMS2TileSeparationX = set.GMS2TileSeparationX,
        GMS2TileSeparationY = set.GMS2TileSeparationY,
        GMS2TileColumns = set.GMS2TileColumns,
        GMS2TileCount = set.GMS2TileCount,
    };
#pragma warning restore CA2000

    /// <summary>Draws every cell (tile with its border and separation) of <paramref name="source"/> at the place it has in <paramref name="columns"/> columns.</summary>
    public static IImage Compose(IImage source, GameMakerBackground set, int columns)
    {
        var oldColumns = Math.Max(1, (int)set.GMS2TileColumns);
        var count = (int)Math.Min(set.GMS2TileCount, 16384);
        var cellW = (int)(set.GMS2TileWidth + 2 * set.GMS2OutputBorderX + set.GMS2TileSeparationX);
        var cellH = (int)(set.GMS2TileHeight + 2 * set.GMS2OutputBorderY + set.GMS2TileSeparationY);
        if (cellW <= 0 || cellH <= 0 || count <= 0 || columns < 1) return source;
        var rows = (count + columns - 1) / columns;
        var width = Math.Min(columns * cellW, 16384);
        var height = Math.Min(rows * cellH, 16384);
        var target = new RenderTargetBitmap(new PixelSize(width, height));
        using (var context = target.CreateDrawingContext())
        using (context.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = BitmapInterpolationMode.None }))
        {
            for (var i = 0; i < count; i++)
            {
                var from = new Rect(i % oldColumns * cellW, i / oldColumns * cellH, cellW, cellH);
                var to = new Rect(i % columns * cellW, i / columns * cellH, cellW, cellH);
                context.DrawImage(source, from, to);
            }
        }

        return target;
    }
}
