using System.Globalization;
using System.Text;

namespace G3MStudio.Core.Workspace;

/// <summary>A set of selected pixels as a byte mask (1 selected), the same size as the picture it belongs to.</summary>
public sealed class PixelSelection
{
    public PixelSelection(int width, int height, byte[]? mask = null)
    {
        (Width, Height) = (width, height);
        Mask = mask ?? new byte[width * height];
        if (Mask.Length != width * height) throw new ArgumentException("The mask does not match the size.", nameof(mask));
    }

    public int Width { get; }

    public int Height { get; }

    public byte[] Mask { get; }

    public bool Contains(int x, int y) => (uint)x < (uint)Width && (uint)y < (uint)Height && Mask[y * Width + x] != 0;

    public bool IsEmpty => Array.IndexOf(Mask, (byte)1) < 0;

    public PixelSelection Copy() => new(Width, Height, (byte[])Mask.Clone());

    public static PixelSelection All(int width, int height) => new(width, height, [.. Enumerable.Repeat((byte)1, width * height)]);

    /// <summary>The rectangle between two corners (both included).</summary>
    public static PixelSelection Rectangle(int width, int height, int x0, int y0, int x1, int y1)
    {
        var selection = new PixelSelection(width, height);
        for (var y = Math.Max(0, Math.Min(y0, y1)); y <= Math.Min(height - 1, Math.Max(y0, y1)); y++)
            for (var x = Math.Max(0, Math.Min(x0, x1)); x <= Math.Min(width - 1, Math.Max(x0, x1)); x++) selection.Mask[y * width + x] = 1;
        return selection;
    }

    /// <summary>The inside of a free-hand outline (even-odd rule).</summary>
    public static PixelSelection Lasso(int width, int height, IReadOnlyList<(int X, int Y)> outline)
    {
        var selection = new PixelSelection(width, height);
        Scan.Polygon(outline, height, (y, from, to) =>
        {
            for (var x = Math.Max(0, from); x <= Math.Min(width - 1, to); x++) selection.Mask[y * width + x] = 1;
        });
        // The outline itself belongs to the selection, so a lasso drawn around pixels holds them.
        for (var i = 0; i < outline.Count; i++)
            Scan.Line(outline[i].X, outline[i].Y, outline[(i + 1) % outline.Count].X, outline[(i + 1) % outline.Count].Y, (x, y) =>
            {
                if ((uint)x < (uint)width && (uint)y < (uint)height) selection.Mask[y * width + x] = 1;
            });
        return selection;
    }

    /// <summary>The pixels near the colour of the clicked pixel: connected ones, or every one of the picture.</summary>
    public static PixelSelection Wand(PixelBuffer picture, int x, int y, int tolerance, bool contiguous, bool colourOnly = false) =>
        picture.Contains(x, y) ? new PixelSelection(picture.Width, picture.Height, picture.FloodMask(x, y, tolerance, contiguous, colourOnly)) : new PixelSelection(picture.Width, picture.Height);

    /// <summary>The smallest rectangle that holds every selected pixel, or null when nothing is selected.</summary>
    public Box? Bounds()
    {
        int x0 = Width, y0 = Height, x1 = -1, y1 = -1;
        for (var y = 0; y < Height; y++)
        {
            var row = Mask.AsSpan(y * Width, Width);
            var first = row.IndexOf((byte)1);
            if (first < 0) continue;
            (x0, x1, y0, y1) = (Math.Min(x0, first), Math.Max(x1, row.LastIndexOf((byte)1)), Math.Min(y0, y), y);
        }

        return x1 < 0 ? null : new Box(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
    }

    public PixelSelection Inverted()
    {
        return new PixelSelection(Width, Height, [.. Mask.Select(cell => (byte)(1 - cell))]);
    }

    /// <summary>Grows the selection by <paramref name="pixels"/> (shrinks it when negative), one pixel at a time in the four directions.</summary>
    public PixelSelection Expanded(int pixels)
    {
        var grow = pixels > 0;
        var current = grow ? this : Inverted();
        for (var step = 0; step < Math.Abs(pixels); step++) current = current.Dilated();
        return grow ? current : current.Inverted();
    }

    private PixelSelection Dilated()
    {
        var next = Copy();
        for (var y = 0; y < Height; y++)
            for (var x = 0; x < Width; x++)
                if (Mask[y * Width + x] == 0 && (Contains(x - 1, y) || Contains(x + 1, y) || Contains(x, y - 1) || Contains(x, y + 1))) next.Mask[y * Width + x] = 1;
        return next;
    }

    /// <summary>The selected pixels that touch an unselected one or the picture edge (the outline used by Stroke).</summary>
    public PixelSelection Border()
    {
        var next = new PixelSelection(Width, Height);
        for (var y = 0; y < Height; y++)
            for (var x = 0; x < Width; x++)
                if (Contains(x, y) && !(Contains(x - 1, y) && Contains(x + 1, y) && Contains(x, y - 1) && Contains(x, y + 1))) next.Mask[y * Width + x] = 1;
        return next;
    }

    /// <summary>Run lengths for the journal: "width,height:" then runs of unselected, selected, unselected and so on.</summary>
    public string ToText()
    {
        var text = new StringBuilder().Append(Width.ToString(CultureInfo.InvariantCulture)).Append(',').Append(Height.ToString(CultureInfo.InvariantCulture)).Append(':');
        byte state = 0;
        var run = 0;
        foreach (var cell in Mask)
        {
            if (cell != state)
            {
                text.Append(run.ToString(CultureInfo.InvariantCulture)).Append(' ');
                (state, run) = (cell, 0);
            }

            run++;
        }

        return text.Append(run.ToString(CultureInfo.InvariantCulture)).ToString();
    }

    public static PixelSelection FromText(string text)
    {
        var parts = text.Split(':');
        var size = parts[0].Split(',');
        var selection = new PixelSelection(int.Parse(size[0], CultureInfo.InvariantCulture), int.Parse(size[1], CultureInfo.InvariantCulture));
        var at = 0;
        byte state = 0;
        foreach (var run in parts[1].Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var length = int.Parse(run, CultureInfo.InvariantCulture);
            if (state == 1) Array.Fill(selection.Mask, (byte)1, at, length);
            (at, state) = (at + length, (byte)(1 - state));
        }

        return selection;
    }
}

/// <summary>Scanline helpers shared by the polygon tools and the lasso.</summary>
internal static class Scan
{
    /// <summary>Calls <paramref name="span"/> for each row with the first and last pixel inside the polygon (even-odd, tested at pixel centres).</summary>
    public static void Polygon(IReadOnlyList<(int X, int Y)> points, int height, Action<int, int, int> span)
    {
        if (points.Count < 3) return;
        var top = Math.Max(0, points.Min(p => p.Y));
        var bottom = Math.Min(height - 1, points.Max(p => p.Y));
        var crossings = new List<double>();
        for (var y = top; y <= bottom; y++)
        {
            crossings.Clear();
            var centre = y + 0.5;
            for (var i = 0; i < points.Count; i++)
            {
                var (a, b) = (points[i], points[(i + 1) % points.Count]);
                if ((a.Y + 0.5 <= centre) == (b.Y + 0.5 <= centre)) continue;
                crossings.Add(a.X + 0.5 + (centre - a.Y - 0.5) * (b.X - a.X) / (b.Y - a.Y));
            }

            crossings.Sort();
            for (var i = 0; i + 1 < crossings.Count; i += 2) span(y, (int)Math.Ceiling(crossings[i] - 0.5), (int)Math.Floor(crossings[i + 1] - 0.5));
        }
    }

    /// <summary>Bresenham: calls <paramref name="plot"/> for every pixel from one point to the other.</summary>
    public static void Line(int x0, int y0, int x1, int y1, Action<int, int> plot)
    {
        int dx = Math.Abs(x1 - x0), dy = -Math.Abs(y1 - y0), sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1, error = dx + dy;
        while (true)
        {
            plot(x0, y0);
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
}
