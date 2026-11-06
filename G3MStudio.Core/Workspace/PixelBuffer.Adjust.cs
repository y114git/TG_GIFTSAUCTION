namespace G3MStudio.Core.Workspace;

/// <summary>Colour adjustments and effects. They change the selected pixels only (all pixels without a selection) and leave fully clear pixels alone.</summary>
public sealed partial class PixelBuffer
{
    private void Map(Func<Rgba, int, int, Rgba> change)
    {
        for (var y = 0; y < Height; y++)
            for (var x = 0; x < Width; x++)
            {
                if (Data[(y * Width + x) * 4 + 3] == 0 || (Clip is { } clip && !clip.Contains(x, y))) continue;
                var next = change(Get(x, y), x, y);
                var i = (y * Width + x) * 4;
                (Data[i], Data[i + 1], Data[i + 2], Data[i + 3]) = (next.R, next.G, next.B, next.A);
            }

        MarkDirty(0, 0, Width - 1, Height - 1);
    }

    public void InvertColours() => Map((c, _, _) => new Rgba((byte)(255 - c.R), (byte)(255 - c.G), (byte)(255 - c.B), c.A));

    /// <summary>Levels per channel, 2 to 256: each channel is rounded to the nearest of that many steps.</summary>
    public void Posterize(int levels)
    {
        var steps = Math.Clamp(levels, 2, 256) - 1;
        byte Round(byte v) => (byte)Math.Round(Math.Round(v * steps / 255.0) * 255.0 / steps);
        Map((c, _, _) => new Rgba(Round(c.R), Round(c.G), Round(c.B), c.A));
    }

    /// <summary>Hue in degrees (-180 to 180); saturation, brightness and contrast from -100 to 100.</summary>
    public void Adjust(double hue, double saturation, double brightness, double contrast)
    {
        var factor = (100 + contrast) / 100;
        factor *= factor;
        Map((c, _, _) =>
        {
            var (h, s, v) = ToHsv(c);
            (h, s) = ((h + hue + 360) % 360, Math.Clamp(s * (1 + saturation / 100), 0, 1));
            var back = FromHsv(h, s, v, c.A);
            byte Channel(byte value) => (byte)Math.Clamp(Math.Round(((value / 255.0 + brightness / 100 - 0.5) * factor + 0.5) * 255), 0, 255);
            return new Rgba(Channel(back.R), Channel(back.G), Channel(back.B), c.A);
        });
    }

    /// <summary>Greyscale by an amount from 0 to 100 percent.</summary>
    public void Greyscale(int amount) => Map((c, _, _) =>
    {
        var grey = 0.299 * c.R + 0.587 * c.G + 0.114 * c.B;
        byte Mix(byte v) => (byte)Math.Round(v + (grey - v) * Math.Clamp(amount, 0, 100) / 100);
        return new Rgba(Mix(c.R), Mix(c.G), Mix(c.B), c.A);
    });

    private static readonly int[] Bayer = [0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5];

    /// <summary>Ordered dither (Bayer 4 x 4) to the given number of levels per channel, 2 to 16.</summary>
    public void Dither(int levels)
    {
        var steps = Math.Clamp(levels, 2, 16) - 1;
        Map((c, x, y) =>
        {
            var threshold = (Bayer[(y & 3) * 4 + (x & 3)] + 0.5) / 16 - 0.5;
            byte Round(byte v) => (byte)Math.Clamp(Math.Round((Math.Floor(v / 255.0 * steps + threshold + 0.5)) * 255.0 / steps), 0, 255);
            return new Rgba(Round(c.R), Round(c.G), Round(c.B), c.A);
        });
    }

    /// <summary>Gives the shapes an outline: every clear pixel next to a pixel with alpha (four directions) gets the colour.</summary>
    public void Outline(Rgba color)
    {
        var edge = new List<(int, int)>();
        for (var y = 0; y < Height; y++)
            for (var x = 0; x < Width; x++)
                if (Data[(y * Width + x) * 4 + 3] == 0 && (Solid(x - 1, y) || Solid(x + 1, y) || Solid(x, y - 1) || Solid(x, y + 1))) edge.Add((x, y));
        foreach (var (x, y) in edge) Set(x, y, color);
    }

    private bool Solid(int x, int y) => Contains(x, y) && Data[(y * Width + x) * 4 + 3] != 0;

    /// <summary>A box blur of the given radius (1 to 8) over the colours, weighted by alpha so clear pixels add no colour.</summary>
    public void Blur(int radius)
    {
        var source = Copy();
        var size = Math.Clamp(radius, 1, 8);
        Map((_, x, y) =>
        {
            double r = 0, g = 0, b = 0, a = 0, count = 0;
            for (var dy = -size; dy <= size; dy++)
                for (var dx = -size; dx <= size; dx++)
                {
                    if (!source.Contains(x + dx, y + dy)) continue;
                    var c = source.Get(x + dx, y + dy);
                    (r, g, b, a, count) = (r + c.R * c.A, g + c.G * c.A, b + c.B * c.A, a + c.A, count + 1);
                }

            return a == 0 ? Rgba.Clear : new Rgba((byte)(r / a), (byte)(g / a), (byte)(b / a), (byte)(a / count));
        });
    }

    private static (double H, double S, double V) ToHsv(Rgba c)
    {
        double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0, max = Math.Max(r, Math.Max(g, b)), delta = max - Math.Min(r, Math.Min(g, b));
        var h = delta == 0 ? 0 : max == r ? 60 * (((g - b) / delta + 6) % 6) : max == g ? 60 * ((b - r) / delta + 2) : 60 * ((r - g) / delta + 4);
        return (h, max == 0 ? 0 : delta / max, max);
    }

    private static Rgba FromHsv(double h, double s, double v, byte alpha)
    {
        double chroma = v * s, x = chroma * (1 - Math.Abs(h / 60 % 2 - 1)), m = v - chroma;
        var (r, g, b) = (int)(h / 60) switch { 0 => (chroma, x, 0.0), 1 => (x, chroma, 0.0), 2 => (0.0, chroma, x), 3 => (0.0, x, chroma), 4 => (x, 0.0, chroma), _ => (chroma, 0.0, x) };
        return new Rgba((byte)Math.Round((r + m) * 255), (byte)Math.Round((g + m) * 255), (byte)Math.Round((b + m) * 255), alpha);
    }
}
