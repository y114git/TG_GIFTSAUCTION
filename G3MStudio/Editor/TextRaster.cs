using System.Globalization;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Editor;

/// <summary>
/// Draws text into pixels for the Text tool with Avalonia's own text engine (no extra dependency). Only the coverage is taken from the engine; the colour is the
/// tool colour. Without smooth the edges are cut at half coverage, which keeps pixel art crisp. Smooth text is drawn four times as large and averaged down, so its soft
/// edges do not depend on the font smoothing of the platform. The pixels go into the edit (a stamp op), so a replay never depends on the fonts of the machine.
/// </summary>
public static class TextRaster
{
    private const int Oversample = 4;

    // A bitmap above this is refused: a long line at a large size would need hundreds of megabytes.
    private const long MaxPixels = 16_000_000;

    // The interface font of the theme, which is what the rest of the window is drawn in.
    private static FontFamily Themed() =>
        Application.Current is { } app && app.TryGetResource("Font.Body", app.ActualThemeVariant, out var body) && body is FontFamily themed ? themed : FontFamily.Default;

    public static PixelBuffer? Render(string text, string font, double size, Rgba colour, bool smooth)
    {
        if (string.IsNullOrEmpty(text) || size < 1) return null;
        var family = font.Length > 0 ? new FontFamily(font) : Themed();
        var (cover, width, height) = smooth ? Smoothed(Coverage(text, family, size * Oversample)) : Coverage(text, family, size);
        if (width == 0 || height == 0) return null;
        var picture = new PixelBuffer(width, height);
        for (var i = 0; i < cover.Length; i++)
        {
            var alpha = smooth ? colour.A * cover[i] / 255 : cover[i] >= 128 ? colour.A : 0;
            if (alpha > 0) (picture.Data[i * 4], picture.Data[i * 4 + 1], picture.Data[i * 4 + 2], picture.Data[i * 4 + 3]) = (colour.R, colour.G, colour.B, (byte)alpha);
        }

        return picture;
    }

    // The alpha of white text on a clear bitmap, one byte a pixel.
    private static (byte[] Cover, int Width, int Height) Coverage(string text, FontFamily family, double size)
    {
        var formatted = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface(family), size, Brushes.White);
        var (width, height) = ((int)Math.Ceiling(formatted.Width) + 2, (int)Math.Ceiling(formatted.Height) + 2);
        if ((long)width * height > MaxPixels) return ([], 0, 0);
        using var target = new RenderTargetBitmap(new PixelSize(width, height), new Vector(96, 96));
        using (var context = target.CreateDrawingContext()) context.DrawText(formatted, new Point(1, 1));
        var raw = new byte[width * height * 4];
        var handle = GCHandle.Alloc(raw, GCHandleType.Pinned);
        try
        {
            target.CopyPixels(new PixelRect(0, 0, width, height), handle.AddrOfPinnedObject(), raw.Length, width * 4);
        }
        finally
        {
            handle.Free();
        }

        var cover = new byte[width * height];
        for (var i = 0; i < cover.Length; i++) cover[i] = raw[i * 4 + 3];
        return (cover, width, height);
    }

    private static (byte[] Cover, int Width, int Height) Smoothed((byte[] Cover, int Width, int Height) big)
    {
        int width = (big.Width + Oversample - 1) / Oversample, height = (big.Height + Oversample - 1) / Oversample;
        var small = new byte[width * height];
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var sum = 0;
                for (var dy = 0; dy < Oversample; dy++)
                    for (var dx = 0; dx < Oversample; dx++)
                    {
                        int bx = x * Oversample + dx, by = y * Oversample + dy;
                        if (bx < big.Width && by < big.Height) sum += big.Cover[by * big.Width + bx];
                    }

                small[y * width + x] = (byte)(sum / (Oversample * Oversample));
            }

        return (small, width, height);
    }
}
