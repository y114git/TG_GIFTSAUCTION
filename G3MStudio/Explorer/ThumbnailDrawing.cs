using System.Diagnostics;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.Immutable;
using G3MStudio.Rooms;

namespace G3MStudio.Explorer;

/// <summary>The drawing of the thumbnails: pieces of texture pages, a room at a small scale, and path or curve line art. Each method reads only what it is given, so it may run on any thread.</summary>
internal static class ThumbnailDrawing
{
    private const int Side = ThumbnailService.Side, PlotSide = ThumbnailService.PlotSide;

    internal static PixelSize Fit(double width, double height, int side, out double scale)
    {
        scale = Math.Min(1, side / Math.Max(width, height));
        return new PixelSize(Math.Max(1, (int)Math.Round(width * scale)), Math.Max(1, (int)Math.Round(height * scale)));
    }

#pragma warning disable CA2000 // each target is handed to the caller as the picture
    // The pieces side by side (one for a sprite or page, the two glyphs of a font sample), scaled down to fit and never up.
    // A source of width 0 means the whole page.
    internal static IImage? DrawPieces(List<(Bitmap Page, Rect Source)> pieces)
    {
        var parts = pieces.Select(p => (p.Page, Source: p.Source.Width > 0 ? p.Source.Intersect(new Rect(0, 0, p.Page.PixelSize.Width, p.Page.PixelSize.Height)) : new Rect(0, 0, p.Page.PixelSize.Width, p.Page.PixelSize.Height))).Where(p => p.Source.Width > 0 && p.Source.Height > 0).ToList();
        if (parts.Count == 0) return null;
        double width = parts.Sum(p => p.Source.Width), height = parts.Max(p => p.Source.Height);
        var size = Fit(width, height, Side, out var scale);
        var target = new RenderTargetBitmap(size);
        using var context = target.CreateDrawingContext(true);
        // Pixel art stays sharp when it is not made smaller.
        using var options = context.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = scale < 1 ? BitmapInterpolationMode.HighQuality : BitmapInterpolationMode.None });
        double x = 0;
        foreach (var (page, source) in parts)
        {
            context.DrawImage(page, source, new Rect(x * scale, 0, source.Width * scale, source.Height * scale));
            x += source.Width;
        }

        return target;
    }

    // The room at a small scale, in draw order; when the budget runs out the rest is left undrawn.
    internal static IImage? DrawRoom(RoomScene scene, Stopwatch clock)
    {
        if (scene.Width < 1 || scene.Height < 1) return null;
        var size = Fit(scene.Width, scene.Height, Side, out var scale);
        var target = new RenderTargetBitmap(size);
        using var context = target.CreateDrawingContext(true);
        using var options = context.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = BitmapInterpolationMode.MediumQuality });
        using var zoom = context.PushTransform(Matrix.CreateScale(scale, scale));
        var fill = scene.FillColor is { } c ? Color.FromRgb((byte)c, (byte)(c >> 8), (byte)(c >> 16)) : Colors.Black;
        context.FillRectangle(new ImmutableSolidColorBrush(fill), new Rect(0, 0, scene.Width, scene.Height));
        for (var i = 0; i < scene.Items.Count; i++)
        {
            if ((i & 255) == 0 && clock.Elapsed > ThumbnailService.RoomBudget) break;
            RoomView.DrawItem(context, scene.Items[i], scene.Items[i].Dest);
        }

        return target;
    }

    // Paths and curves: the points scaled into a square, drawn as thick lines so they stay visible when the row shrinks them.
    internal static IImage? DrawLines(List<List<(float X, float Y)>> lines, bool closed, Color line, Color point)
    {
        var all = lines.SelectMany(l => l).ToList();
        if (all.Count < 2) return null;
        float left = all.Min(p => p.X), right = all.Max(p => p.X), top = all.Min(p => p.Y), bottom = all.Max(p => p.Y);
        const double Margin = 6;
        double span = Math.Max(Math.Max(right - left, bottom - top), 1e-3), scale = (PlotSide - 2 * Margin) / span;
        var offsetX = (PlotSide - (right - left) * scale) / 2;
        var offsetY = (PlotSide - (bottom - top) * scale) / 2;
        Point At((float X, float Y) p) => new(offsetX + (p.X - left) * scale, offsetY + (p.Y - top) * scale);
        var target = new RenderTargetBitmap(new PixelSize(PlotSide, PlotSide));
        using var context = target.CreateDrawingContext(true);
        for (var l = 0; l < lines.Count; l++)
        {
            var pen = new Pen(new ImmutableSolidColorBrush(l % 2 == 0 ? line : point), 4, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
            var points = lines[l];
            for (var i = 1; i < points.Count; i++) context.DrawLine(pen, At(points[i - 1]), At(points[i]));
            if (closed && points.Count > 2) context.DrawLine(pen, At(points[^1]), At(points[0]));
        }

        return target;
    }
#pragma warning restore CA2000
}
