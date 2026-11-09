namespace G3MStudio.Core.Workspace;

public sealed partial class PixelBuffer
{
    /// <summary>A brush of <paramref name="size"/> pixels centred on the point (the point is the top left of the middle for an even size).</summary>
    public void Stamp(int x, int y, int size, Rgba color, BrushShape shape = BrushShape.Square)
    {
        var low = (size - 1) / 2;
        if (shape == BrushShape.Square)
        {
            for (var dy = 0; dy < size; dy++) Span(y - low + dy, x - low, x - low + size - 1, color);
            return;
        }

        var centre = (size - 1) / 2.0;
        var radius = centre * centre + 0.5;
        for (var dy = 0; dy < size; dy++)
            for (var dx = 0; dx < size; dx++)
                if ((dx - centre) * (dx - centre) + (dy - centre) * (dy - centre) <= radius) Set(x - low + dx, y - low + dy, color);
    }

    /// <summary>A line of stamps from one point to the other (Bresenham), so a fast drag leaves no gaps.</summary>
    public void Line(int x0, int y0, int x1, int y1, int size, Rgba color, BrushShape shape = BrushShape.Square) =>
        Scan.Line(x0, y0, x1, y1, (x, y) => Stamp(x, y, size, color, shape));

    /// <summary>Joins the points one after the other.</summary>
    public void Stroke(IReadOnlyList<(int X, int Y)> points, int size, Rgba color, BrushShape shape)
    {
        if (points.Count == 1) Stamp(points[0].X, points[0].Y, size, color, shape);
        for (var i = 1; i < points.Count; i++) Line(points[i - 1].X, points[i - 1].Y, points[i].X, points[i].Y, size, color, shape);
    }

    /// <summary>Sets the pixels of one row from <paramref name="x0"/> to <paramref name="x1"/>.</summary>
    internal void Span(int y, int x0, int x1, Rgba color)
    {
        if ((uint)y >= (uint)Height) return;
        x0 = Math.Max(x0, 0);
        x1 = Math.Min(x1, Width - 1);
        if (x0 > x1) return;
        if (Clip is not null)
        {
            for (var x = x0; x <= x1; x++) Set(x, y, color);
            return;
        }

        var value = ((uint)color.A << 24) | ((uint)color.B << 16) | ((uint)color.G << 8) | color.R;
        System.Runtime.InteropServices.MemoryMarshal.Cast<byte, uint>(Data.AsSpan()).Slice(y * Width + x0, x1 - x0 + 1).Fill(value);
        MarkDirty(x0, y, x1, y);
    }

    /// <summary>The rectangle between two corners, filled or as an outline one pixel wide.</summary>
    public void Rectangle(int x0, int y0, int x1, int y1, Rgba color, bool filled) => Rectangle((x0, y0), (x1, y1), color, filled ? color : null, 1);

    /// <summary>The rectangle between two corners: an outline of <paramref name="size"/> pixels, a fill, or both (either colour may be null).</summary>
    public void Rectangle((int X, int Y) a, (int X, int Y) b, Rgba? outline, Rgba? fill, int size)
    {
        int left = Math.Min(a.X, b.X), right = Math.Max(a.X, b.X), top = Math.Min(a.Y, b.Y), bottom = Math.Max(a.Y, b.Y);
        for (var y = top; y <= bottom; y++)
        {
            var edge = y - top < size || bottom - y < size;
            if (outline is { } line && edge) Span(y, left, right, line);
            else if (outline is { } side) Rows(y, left, right, size, side, fill);
            else if (fill is { } inner) Span(y, left, right, inner);
        }
    }

    // One row inside an outlined rectangle: the two side bars, and the fill between them.
    private void Rows(int y, int left, int right, int size, Rgba side, Rgba? fill)
    {
        Span(y, left, left + size - 1, side);
        Span(y, right - size + 1, right, side);
        if (fill is { } inner) Span(y, left + size, right - size, inner);
    }

    /// <summary>The ellipse inscribed in the box between two corners: an outline of <paramref name="size"/> pixels, a fill, or both (either colour may be null).</summary>
    public void Ellipse((int X, int Y) a, (int X, int Y) b, Rgba? outline, Rgba? fill, int size)
    {
        int left = Math.Min(a.X, b.X), right = Math.Max(a.X, b.X), top = Math.Min(a.Y, b.Y), bottom = Math.Max(a.Y, b.Y);
        double cx = (left + right) / 2.0, cy = (top + bottom) / 2.0, rx = (right - left + 1) / 2.0, ry = (bottom - top + 1) / 2.0;
        for (var y = top; y <= bottom; y++)
        {
            if (Extent(y, cy, cx, rx, ry) is not var (lo, hi)) continue;
            var inner = Extent(y, cy, cx, rx - size, ry - size);
            if (fill is { } inside) Span(y, lo, hi, inside);
            if (outline is not { } line) continue;
            if (inner is not var (c, d) || rx - size <= 0 || ry - size <= 0) Span(y, lo, hi, line);
            else
            {
                Span(y, lo, c - 1, line);
                Span(y, d + 1, hi, line);
            }
        }
    }

    // The first and last pixel of a row inside the ellipse (pixel centres), or null when the row misses it.
    private static (int, int)? Extent(int y, double cy, double cx, double rx, double ry)
    {
        if (rx <= 0 || ry <= 0) return null;
        var t = 1 - Math.Pow((y - cy) / ry, 2);
        if (t < 0) return null;
        var half = rx * Math.Sqrt(t);
        var (a, b) = ((int)Math.Ceiling(cx - half - 1e-9), (int)Math.Floor(cx + half + 1e-9));
        return a > b ? null : (a, b);
    }

    /// <summary>A closed polygon: the inside with the fill colour, the edges with the outline colour (either may be null).</summary>
    public void Polygon(IReadOnlyList<(int X, int Y)> points, Rgba? outline, Rgba? fill, int size, BrushShape shape = BrushShape.Square)
    {
        if (fill is { } inside) Scan.Polygon(points, Height, (y, from, to) => Span(y, from, to, inside));
        if (outline is not { } line) return;
        for (var i = 0; i < points.Count; i++)
        {
            var (nx, ny) = points[(i + 1) % points.Count];
            if (points.Count > 2 || i == 0) Line(points[i].X, points[i].Y, nx, ny, size, line, shape);
        }
    }

    /// <summary>A smooth curve through Bezier control points (De Casteljau), drawn as short lines.</summary>
    public void Curve(IReadOnlyList<(int X, int Y)> controls, int size, Rgba color, BrushShape shape, int iterations = 10)
    {
        if (controls.Count < 3) { Stroke(controls, size, color, shape); return; }
        var length = 0.0;
        for (var i = 1; i < controls.Count; i++) length += Math.Sqrt(Math.Pow(controls[i].X - controls[i - 1].X, 2) + Math.Pow(controls[i].Y - controls[i - 1].Y, 2));
        var steps = Math.Max(iterations, (int)length);
        var path = new List<(int X, int Y)>();
        for (var s = 0; s <= steps; s++)
        {
            var t = s / (double)steps;
            var work = controls.Select(c => ((double)c.X, (double)c.Y)).ToArray();
            for (var level = work.Length - 1; level > 0; level--)
                for (var k = 0; k < level; k++) work[k] = (work[k].Item1 + (work[k + 1].Item1 - work[k].Item1) * t, work[k].Item2 + (work[k + 1].Item2 - work[k].Item2) * t);
            path.Add(((int)Math.Round(work[0].Item1), (int)Math.Round(work[0].Item2)));
        }

        Stroke(path, size, color, shape);
    }
}
