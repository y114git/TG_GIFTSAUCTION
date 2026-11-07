namespace G3MStudio.Core.Workspace;

/// <summary>
/// Anti-aliased drawing for one stroke or shape. The coverage of every touched pixel is kept (the largest wins where parts overlap), and each pixel is blended
/// once from the picture as it was when the brush was made. So a stroke made of many segments looks the same as one drawn in a single pass, and the live editor
/// (segment by segment) and the journal replay (all at once) give the same pixels. A colour with alpha 0 erases.
/// </summary>
public sealed class SmoothBrush
{
    private readonly PixelBuffer _target;
    private readonly byte[] _base;
    private readonly byte[] _cover;
    private readonly Rgba _colour;
    private readonly double _radius;

    public SmoothBrush(PixelBuffer target, Rgba colour, double size)
    {
        (_target, _colour, _radius) = (target, colour, Math.Max(0.5, size / 2));
        _base = target.Snapshot();
        _cover = new byte[target.Width * target.Height];
    }

    /// <summary>A round dab, or the stroke from one point to another (pixel centres are at +0.5).</summary>
    public void Segment(double x0, double y0, double x1, double y1)
    {
        var reach = _radius + 1;
        int ax = Math.Max(0, (int)Math.Floor(Math.Min(x0, x1) - reach)), bx = Math.Min(_target.Width - 1, (int)Math.Ceiling(Math.Max(x0, x1) + reach));
        int ay = Math.Max(0, (int)Math.Floor(Math.Min(y0, y1) - reach)), by = Math.Min(_target.Height - 1, (int)Math.Ceiling(Math.Max(y0, y1) + reach));
        var (dx, dy) = (x1 - x0, y1 - y0);
        var length2 = dx * dx + dy * dy;
        for (var y = ay; y <= by; y++)
            for (var x = ax; x <= bx; x++)
            {
                var (px, py) = (x + 0.5 - x0, y + 0.5 - y0);
                var t = length2 == 0 ? 0 : Math.Clamp((px * dx + py * dy) / length2, 0, 1);
                var distance = Math.Sqrt((px - t * dx) * (px - t * dx) + (py - t * dy) * (py - t * dy));
                Cover(x, y, Math.Clamp(_radius + 0.5 - distance, 0, 1));
            }

        Blend(ax, ay, bx, by);
    }

    public void Path(IReadOnlyList<(double X, double Y)> points, bool closed)
    {
        if (points.Count == 1) Segment(points[0].X, points[0].Y, points[0].X, points[0].Y);
        for (var i = 1; i < points.Count; i++) Segment(points[i - 1].X, points[i - 1].Y, points[i].X, points[i].Y);
        if (closed && points.Count > 2) Segment(points[^1].X, points[^1].Y, points[0].X, points[0].Y);
    }

    /// <summary>The inside of a polygon (even-odd), sampled 4 x 4 in every pixel.</summary>
    public void Fill(IReadOnlyList<(double X, double Y)> polygon)
    {
        if (polygon.Count < 3) return;
        int ax = Math.Max(0, (int)Math.Floor(polygon.Min(p => p.X))), bx = Math.Min(_target.Width - 1, (int)Math.Ceiling(polygon.Max(p => p.X)));
        int ay = Math.Max(0, (int)Math.Floor(polygon.Min(p => p.Y))), by = Math.Min(_target.Height - 1, (int)Math.Ceiling(polygon.Max(p => p.Y)));
        for (var y = ay; y <= by; y++)
            for (var x = ax; x <= bx; x++)
            {
                var inside = 0;
                for (var sy = 0; sy < 4; sy++)
                    for (var sx = 0; sx < 4; sx++)
                        if (Inside(polygon, x + (sx + 0.5) / 4, y + (sy + 0.5) / 4)) inside++;
                if (inside > 0) Cover(x, y, inside / 16.0);
            }

        Blend(ax, ay, bx, by);
    }

    private static bool Inside(IReadOnlyList<(double X, double Y)> polygon, double x, double y)
    {
        var inside = false;
        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            if ((polygon[i].Y > y) != (polygon[j].Y > y) && x < (polygon[j].X - polygon[i].X) * (y - polygon[i].Y) / (polygon[j].Y - polygon[i].Y) + polygon[i].X) inside = !inside;
        return inside;
    }

    private void Cover(int x, int y, double amount)
    {
        var value = (byte)Math.Round(amount * 255);
        var i = y * _target.Width + x;
        if (value > _cover[i]) _cover[i] = value;
    }

    private void Blend(int x0, int y0, int x1, int y1)
    {
        for (var y = y0; y <= y1; y++)
            for (var x = x0; x <= x1; x++)
            {
                var cover = _cover[y * _target.Width + x];
                if (cover == 0 || (_target.Clip is { } clip && !clip.Contains(x, y))) continue;
                var i = (y * _target.Width + x) * 4;
                var under = new Rgba(_base[i], _base[i + 1], _base[i + 2], _base[i + 3]);
                var result = _colour.A == 0 ? under with { A = (byte)(under.A * (255 - cover) / 255) } : (_colour with { A = (byte)(_colour.A * cover / 255) }).Over(under);
                (_target.Data[i], _target.Data[i + 1], _target.Data[i + 2], _target.Data[i + 3]) = (result.R, result.G, result.B, result.A);
            }

        _target.MarkDirty(x0, y0, x1, y1);
    }
}

public sealed partial class PixelBuffer
{
    /// <summary>
    /// Draws an anti-aliased shape: "line" (an open path, also the pencil), "rect" or "ellipse" (two corners), "polygon" (closed) or "curve" (a Bezier through
    /// the controls). The fill goes first, then the outline of the given width, centred on the pixel centres.
    /// </summary>
    public void Smooth(string shape, IReadOnlyList<(int X, int Y)> points, Rgba? outline, Rgba? fill, double size)
    {
        var path = SmoothPath(shape, points, size);
        var closed = shape is "rect" or "ellipse" or "polygon";
        if (fill is { } inside && closed) new SmoothBrush(this, inside, 1).Fill(path);
        if (outline is { } colour) new SmoothBrush(this, colour, size).Path(path, closed);
    }

    private static List<(double X, double Y)> SmoothPath(string shape, IReadOnlyList<(int X, int Y)> points, double size)
    {
        if (shape is "rect" or "ellipse" && points.Count >= 2)
        {
            double x0 = Math.Min(points[0].X, points[1].X), y0 = Math.Min(points[0].Y, points[1].Y), x1 = Math.Max(points[0].X, points[1].X) + 1, y1 = Math.Max(points[0].Y, points[1].Y) + 1;
            var inset = Math.Min(size / 2, Math.Min(x1 - x0, y1 - y0) / 2);
            (x0, y0, x1, y1) = (x0 + inset, y0 + inset, x1 - inset, y1 - inset);
            if (shape == "rect") return [(x0, y0), (x1, y0), (x1, y1), (x0, y1)];
            double cx = (x0 + x1) / 2, cy = (y0 + y1) / 2, rx = (x1 - x0) / 2, ry = (y1 - y0) / 2;
            var steps = Math.Max(24, (int)(Math.PI * (rx + ry)));
            return [.. Enumerable.Range(0, steps).Select(i => (cx + rx * Math.Cos(2 * Math.PI * i / steps), cy + ry * Math.Sin(2 * Math.PI * i / steps)))];
        }

        var centres = points.Select(p => (p.X + 0.5, p.Y + 0.5)).ToList();
        return shape == "curve" && centres.Count >= 3 ? Bezier(centres) : centres;
    }

    private static List<(double X, double Y)> Bezier(List<(double X, double Y)> controls)
    {
        var length = 0.0;
        for (var i = 1; i < controls.Count; i++) length += Math.Sqrt(Math.Pow(controls[i].X - controls[i - 1].X, 2) + Math.Pow(controls[i].Y - controls[i - 1].Y, 2));
        var steps = Math.Max(10, (int)length);
        var path = new List<(double X, double Y)>();
        for (var s = 0; s <= steps; s++)
        {
            var work = controls.ToArray();
            for (var level = work.Length - 1; level > 0; level--)
                for (var k = 0; k < level; k++) work[k] = (work[k].X + (work[k + 1].X - work[k].X) * s / steps, work[k].Y + (work[k + 1].Y - work[k].Y) * s / steps);
            path.Add(work[0]);
        }

        return path;
    }
}
