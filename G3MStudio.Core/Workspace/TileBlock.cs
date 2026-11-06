namespace G3MStudio.Core.Workspace;

/// <summary>A rectangle of tile values taken from a tile layer, row by row. 0 is an empty cell.</summary>
public sealed record TileBlock(int Width, int Height, uint[] Cells)
{
    public uint this[int x, int y] => Cells[y * Width + x];
}

/// <summary>Pure tile layer operations behind selection, cut, copy, paste, flip, the line tool and the brush size. Results are cell lists for <see cref="RoomTilesCommand"/>.</summary>
public static class TileBlocks
{
    private const uint Mirror = 1u << 28;
    private const uint Flip = 1u << 29;
    private const uint Rotate = 1u << 30;

    /// <summary>The cells of the rectangle (x, y, width, height) that lie inside the layer.</summary>
    public static TileBlock Copy(uint[][] rows, int x, int y, int width, int height)
    {
        var (x0, y0) = (Math.Max(0, x), Math.Max(0, y));
        var y1 = Math.Min(rows.Length, y + height);
        var cells = new List<uint>();
        var x1 = y1 > y0 ? Math.Min(rows[y0..y1].Min(r => r.Length), x + width) : x0;
        for (var row = y0; row < y1; row++)
            for (var column = x0; column < x1; column++) cells.Add(rows[row][column]);
        return new TileBlock(Math.Max(0, x1 - x0), Math.Max(0, y1 - y0), [.. cells]);
    }

    /// <summary>Mirrors the block left to right. A tile also flips its own picture: the mirror flag, or the flip flag when the tile is turned a quarter.</summary>
    public static TileBlock FlipHorizontal(TileBlock block) => Reflect(block, true);

    /// <summary>Mirrors the block top to bottom, with the flags changed to match.</summary>
    public static TileBlock FlipVertical(TileBlock block) => Reflect(block, false);

    private static TileBlock Reflect(TileBlock block, bool horizontal)
    {
        var cells = new uint[block.Cells.Length];
        for (var y = 0; y < block.Height; y++)
            for (var x = 0; x < block.Width; x++)
                cells[y * block.Width + x] = Reflect(horizontal ? block[block.Width - 1 - x, y] : block[x, block.Height - 1 - y], horizontal);
        return block with { Cells = cells };
    }

    /// <summary>The same tile seen in a mirror. A quarter turn swaps the axes, so then the other flag changes.</summary>
    public static uint Reflect(uint tile, bool horizontal)
    {
        if ((tile & 0x0007FFFF) == 0) return tile;
        return tile ^ (horizontal == ((tile & Rotate) == 0) ? Mirror : Flip);
    }

    /// <summary>The cells that change when the block is put with its top-left corner at (x, y). Empty cells of the block leave the layer as it is unless <paramref name="withEmpty"/> is set. Cells outside the layer are dropped.</summary>
    public static List<(int X, int Y, uint Value)> Paste(uint[][] rows, TileBlock block, int x, int y, bool withEmpty = false)
    {
        var changes = new List<(int, int, uint)>();
        for (var by = 0; by < block.Height; by++)
            for (var bx = 0; bx < block.Width; bx++)
            {
                int cx = x + bx, cy = y + by;
                if (cy < 0 || cy >= rows.Length || cx < 0 || cx >= rows[cy].Length) continue;
                var value = block[bx, by];
                if ((value != 0 || withEmpty) && rows[cy][cx] != value) changes.Add((cx, cy, value));
            }

        return changes;
    }

    /// <summary>Empties the cells of a rectangle that are not empty yet (cut and delete).</summary>
    public static List<(int X, int Y, uint Value)> Clear(uint[][] rows, int x, int y, int width, int height)
    {
        var changes = new List<(int, int, uint)>();
        for (var cy = Math.Max(0, y); cy < Math.Min(rows.Length, y + height); cy++)
            for (var cx = Math.Max(0, x); cx < Math.Min(rows[cy].Length, x + width); cx++)
                if (rows[cy][cx] != 0) changes.Add((cx, cy, 0u));
        return changes;
    }

    /// <summary>Flips the tiles inside a rectangle in place; the changed cells only.</summary>
    public static List<(int X, int Y, uint Value)> FlipInPlace(uint[][] rows, int x, int y, int width, int height, bool horizontal)
    {
        var block = Copy(rows, x, y, width, height);
        var flipped = horizontal ? FlipHorizontal(block) : FlipVertical(block);
        return Paste(rows, flipped, Math.Max(0, x), Math.Max(0, y), withEmpty: true);
    }

    /// <summary>The cells on the straight line between two cells, both ends included (Bresenham).</summary>
    public static List<(int X, int Y)> Line((int X, int Y) from, (int X, int Y) to)
    {
        var cells = new List<(int, int)>();
        int dx = Math.Abs(to.X - from.X), dy = -Math.Abs(to.Y - from.Y);
        int sx = from.X < to.X ? 1 : -1, sy = from.Y < to.Y ? 1 : -1;
        var (x, y, error) = (from.X, from.Y, dx + dy);
        while (true)
        {
            cells.Add((x, y));
            if (x == to.X && y == to.Y) return cells;
            var twice = 2 * error;
            if (twice >= dy) { error += dy; x += sx; }
            if (twice <= dx) { error += dx; y += sy; }
        }
    }

    /// <summary>The square of <paramref name="size"/> cells a side around a cell; for an even size the extra row and column lie right and below.</summary>
    public static List<(int X, int Y)> Brush((int X, int Y) center, int size)
    {
        var n = Math.Max(1, size);
        var (x0, y0) = (center.X - (n - 1) / 2, center.Y - (n - 1) / 2);
        return [.. Enumerable.Range(y0, n).SelectMany(y => Enumerable.Range(x0, n).Select(x => (x, y)))];
    }

    /// <summary>The cells of a rectangle of cells given by two corners, either way round, first corner first.</summary>
    public static (int X, int Y, int Width, int Height) Rectangle((int X, int Y) a, (int X, int Y) b) =>
        (Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Abs(a.X - b.X) + 1, Math.Abs(a.Y - b.Y) + 1);
}
