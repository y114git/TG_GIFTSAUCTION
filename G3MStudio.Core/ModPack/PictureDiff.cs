using G3MLib.DataFile;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Core.ModPack;

/// <summary>Ways to compare two pictures of different files: the frames of a sprite as pixels, and the overlay, swipe and difference views of them.</summary>
public static class PictureDiff
{
    /// <summary>The frames of a sprite or tile set (null for a frame that cannot be read); empty when the resource has no pictures.</summary>
    public static IReadOnlyList<PixelBuffer?> Frames(GameMakerData data, string type, string name)
    {
        if (PropertyModel.FindRoot(data, type, name) is not { } root) return [];
        using var worker = new G3MLib.DataFile.Util.TextureWorker();
        return [.. Fingerprinter.Textures(root).Select(item => Read(worker, item, name))];
    }

    private static PixelBuffer? Read(G3MLib.DataFile.Util.TextureWorker worker, G3MLib.DataFile.Models.GameMakerTexturePageItem item, string name)
    {
        try
        {
            using var image = worker.GetTextureFor(item, name);
            image.Alpha(ImageMagick.AlphaOption.Set);
            using var pixels = image.GetPixels();
            return new PixelBuffer((int)image.Width, (int)image.Height, pixels.ToByteArray(ImageMagick.PixelMapping.RGBA) ?? throw new InvalidOperationException("The frame has no pixels."));
        }
        catch (Exception e) when (e is InvalidOperationException or ImageMagick.MagickException or ArgumentException)
        {
            return null;
        }
    }

    private static (int Width, int Height) Size(PixelBuffer? a, PixelBuffer? b) => (Math.Max(Math.Max(a?.Width ?? 1, b?.Width ?? 1), 1), Math.Max(Math.Max(a?.Height ?? 1, b?.Height ?? 1), 1));

    // A copy of the picture on a transparent canvas of the given size, at the top left.
    private static PixelBuffer OnCanvas(PixelBuffer? picture, int width, int height)
    {
        var canvas = new PixelBuffer(width, height);
        if (picture is null) return canvas;
        foreach (var row in Enumerable.Range(0, Math.Min(picture.Height, height)))
            Buffer.BlockCopy(picture.Data, row * picture.Width * 4, canvas.Data, row * width * 4, Math.Min(picture.Width, width) * 4);
        return canvas;
    }

    /// <summary>The right picture drawn over the left one with <paramref name="opacity"/> (0 to 1).</summary>
    public static PixelBuffer Overlay(PixelBuffer? left, PixelBuffer? right, double opacity)
    {
        var (w, h) = Size(left, right);
        var (under, over) = (OnCanvas(left, w, h), OnCanvas(right, w, h));
        foreach (var i in Enumerable.Range(0, w * h))
        {
            var top = Pixel(over.Data, i * 4);
            top = top with { A = (byte)(top.A * Math.Clamp(opacity, 0, 1)) };
            Store(under.Data, i * 4, top.Over(Pixel(under.Data, i * 4)));
        }

        return under;
    }

    /// <summary>The left picture up to column <paramref name="x"/>, the right picture from there on.</summary>
    public static PixelBuffer Swipe(PixelBuffer? left, PixelBuffer? right, int x)
    {
        var (w, h) = Size(left, right);
        var (a, b) = (OnCanvas(left, w, h), OnCanvas(right, w, h));
        var from = Math.Clamp(x, 0, w) * 4;
        foreach (var row in Enumerable.Range(0, h))
            Buffer.BlockCopy(b.Data, row * w * 4 + from, a.Data, row * w * 4 + from, w * 4 - from);
        return a;
    }

    /// <summary>Red where the pictures differ (paler for a small difference), the left picture dimmed where they match. <paramref name="differing"/> counts the pixels that differ.</summary>
    public static PixelBuffer Difference(PixelBuffer? left, PixelBuffer? right, out int differing)
    {
        var (w, h) = Size(left, right);
        var (a, b) = (OnCanvas(left, w, h), OnCanvas(right, w, h));
        var result = new PixelBuffer(w, h);
        differing = 0;
        foreach (var i in Enumerable.Range(0, w * h))
        {
            var (p, q) = (Pixel(a.Data, i * 4), Pixel(b.Data, i * 4));
            var delta = Math.Max(Math.Max(Math.Abs(p.R - q.R), Math.Abs(p.G - q.G)), Math.Max(Math.Abs(p.B - q.B), Math.Abs(p.A - q.A)));
            if (delta == 0)
            {
                Store(result.Data, i * 4, new Rgba((byte)(p.R / 3), (byte)(p.G / 3), (byte)(p.B / 3), (byte)(p.A / 3 + 40)));
                continue;
            }

            differing++;
            Store(result.Data, i * 4, new Rgba(255, (byte)(255 - delta), (byte)(255 - delta), 255));
        }

        return result;
    }

    private static Rgba Pixel(byte[] data, int at) => new(data[at], data[at + 1], data[at + 2], data[at + 3]);

    private static void Store(byte[] data, int at, Rgba color) => (data[at], data[at + 1], data[at + 2], data[at + 3]) = (color.R, color.G, color.B, color.A);
}
