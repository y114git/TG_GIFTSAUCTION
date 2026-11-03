namespace G3MStudio.Core.Workspace;

/// <summary>One pixel, straight (not premultiplied) alpha.</summary>
public readonly record struct Rgba(byte R, byte G, byte B, byte A)
{
    public static readonly Rgba Clear = new(0, 0, 0, 0);

    /// <summary>Reads #RRGGBB or #RRGGBBAA; null for anything else.</summary>
    public static Rgba? Parse(string? text)
    {
        if (text is null || text.Length is not (7 or 9) || text[0] != '#' || !uint.TryParse(text.AsSpan(1), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var v)) return null;
        return text.Length == 7 ? new Rgba((byte)(v >> 16), (byte)(v >> 8), (byte)v, 255) : new Rgba((byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v);
    }

    public override string ToString() => A == 255 ? $"#{R:X2}{G:X2}{B:X2}" : $"#{R:X2}{G:X2}{B:X2}{A:X2}";
}

/// <summary>
/// A picture as RGBA bytes with the drawing tools of the sprite painter: a square brush, line, rectangle and flood fill. Every tool sets pixels
/// exactly (no blending), so an erase is a stamp of clear. Pure data, so the tools are tested without a window.
/// </summary>
public sealed class PixelBuffer
{
    public PixelBuffer(int width, int height, byte[]? rgba = null)
    {
        if (width < 1 || height < 1 || (long)width * height * 4 > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(width), "The picture size is not usable.");
        (Width, Height) = (width, height);
        Data = rgba ?? new byte[width * height * 4];
        if (Data.Length != width * height * 4) throw new ArgumentException("The pixel data does not match the size.", nameof(rgba));
    }

    public int Width { get; }

    public int Height { get; }

    /// <summary>Row by row, four bytes a pixel: red, green, blue, alpha.</summary>
    public byte[] Data { get; }

    public bool Contains(int x, int y) => (uint)x < (uint)Width && (uint)y < (uint)Height;

    public Rgba Get(int x, int y)
    {
        var i = (y * Width + x) * 4;
        return new Rgba(Data[i], Data[i + 1], Data[i + 2], Data[i + 3]);
    }

    public void Set(int x, int y, Rgba color)
    {
        if (!Contains(x, y)) return;
        var i = (y * Width + x) * 4;
        (Data[i], Data[i + 1], Data[i + 2], Data[i + 3]) = (color.R, color.G, color.B, color.A);
    }

    /// <summary>A square of <paramref name="size"/> pixels centred on the point (the point is the top left of the middle for an even size).</summary>
    public void Stamp(int x, int y, int size, Rgba color)
    {
        var low = (size - 1) / 2;
        for (var dy = 0; dy < size; dy++)
            for (var dx = 0; dx < size; dx++) Set(x - low + dx, y - low + dy, color);
    }

    /// <summary>A line of stamps from one point to the other (Bresenham), so a fast drag leaves no gaps.</summary>
    public void Line(int x0, int y0, int x1, int y1, int size, Rgba color)
    {
        int dx = Math.Abs(x1 - x0), dy = -Math.Abs(y1 - y0), sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1, error = dx + dy;
        while (true)
        {
            Stamp(x0, y0, size, color);
            if (x0 == x1 && y0 == y1) return;
            var twice = 2 * error;
            if (twice >= dy)
            {
                error += dy;
                x0 += sx;
            }

            if (twice > dx) continue;
            error += dx;
            y0 += sy;
        }
    }

    /// <summary>The rectangle between two corners, filled or as an outline one pixel wide.</summary>
    public void Rectangle(int x0, int y0, int x1, int y1, Rgba color, bool filled)
    {
        int left = Math.Min(x0, x1), right = Math.Max(x0, x1), top = Math.Min(y0, y1), bottom = Math.Max(y0, y1);
        for (var y = top; y <= bottom; y++)
            for (var x = left; x <= right; x++)
                if (filled || x == left || x == right || y == top || y == bottom) Set(x, y, color);
    }

    /// <summary>Paints the connected area (up, down, left, right) that has the colour of the clicked pixel.</summary>
    public void Fill(int x, int y, Rgba color)
    {
        if (!Contains(x, y)) return;
        var target = Get(x, y);
        if (target == color) return;
        var pending = new Stack<(int X, int Y)>();
        pending.Push((x, y));
        while (pending.Count > 0)
        {
            var (px, py) = pending.Pop();
            if (!Contains(px, py) || Get(px, py) != target) continue;
            Set(px, py, color);
            pending.Push((px + 1, py));
            pending.Push((px - 1, py));
            pending.Push((px, py + 1));
            pending.Push((px, py - 1));
        }
    }

    public byte[] Snapshot() => (byte[])Data.Clone();

    public void Restore(byte[] snapshot) => Buffer.BlockCopy(snapshot, 0, Data, 0, Data.Length);
}
