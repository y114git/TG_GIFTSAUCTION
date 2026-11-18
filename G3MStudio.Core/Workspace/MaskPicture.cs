using ImageMagick;

namespace G3MStudio.Core.Workspace;

/// <summary>Converts a collision mask to a black and white PNG and back. Solid pixels are white.</summary>
public static class MaskPicture
{
    public static int Stride(int width) => (width + 7) / 8;

    public static byte[] ToPng(byte[] mask, int width, int height)
    {
        var rgba = new byte[width * height * 4];
        var stride = Stride(width);
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var value = (byte)((mask[y * stride + x / 8] & (0x80 >> (x % 8))) != 0 ? 255 : 0);
                var at = (y * width + x) * 4;
                (rgba[at], rgba[at + 1], rgba[at + 2], rgba[at + 3]) = (value, value, value, 255);
            }

        using var image = new MagickImage(rgba, new MagickReadSettings { Format = MagickFormat.Rgba, Width = (uint)width, Height = (uint)height, Depth = 8 });
        return image.ToByteArray(MagickFormat.Png);
    }

    /// <summary>A pixel is solid when it is opaque and lighter than mid grey. The picture must have the mask's size.</summary>
    public static byte[] FromPicture(byte[] picture, int width, int height)
    {
        using var image = ReplaceFrameCommand.ReadPicture(picture);
        if (image.Width != (uint)width || image.Height != (uint)height) throw new InvalidDataException($"The mask picture must be {width} x {height}, not {image.Width} x {image.Height}.");
        image.Depth = 8;
        var rgba = image.ToByteArray(MagickFormat.Rgba);
        var stride = Stride(width);
        var mask = new byte[stride * height];
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var at = (y * width + x) * 4;
                if (rgba[at + 3] > 127 && (rgba[at] + rgba[at + 1] + rgba[at + 2]) / 3 > 127) mask[y * stride + x / 8] |= (byte)(0x80 >> (x % 8));
            }

        return mask;
    }
}
