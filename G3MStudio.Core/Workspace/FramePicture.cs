using G3MLib.DataFile;
using G3MLib.DataFile.Models;
using G3MLib.DataFile.Util;
using ImageMagick;

namespace G3MStudio.Core.Workspace;

/// <summary>A sprite frame as pixels for the painter, and the pixels back as a PNG for <see cref="ReplaceFrameCommand"/>. The picture is the part of the frame that is stored on the texture page.</summary>
public static class FramePicture
{
    public static PixelBuffer Read(GameMakerData data, string sprite, int frame)
    {
        var item = (data.Sprites.FirstOrDefault(s => s.Name.Content == sprite) ?? throw new InvalidOperationException($"Sprite '{sprite}' does not exist.")).Textures.ElementAtOrDefault(frame)?.Texture
            ?? throw new InvalidOperationException($"Sprite '{sprite}' has no frame {frame}.");
        return ReadItem(item, sprite);
    }

    /// <summary>The pixels of a picture file (png, jpg, bmp, gif, webp) from its bytes; throws when they cannot be read.</summary>
    public static PixelBuffer FromBytes(byte[] bytes)
    {
        using var image = ReplaceFrameCommand.ReadPicture(bytes);
        image.Alpha(AlphaOption.Set);
        using var pixels = image.GetPixels();
        return new PixelBuffer((int)image.Width, (int)image.Height, pixels.ToByteArray(PixelMapping.RGBA) ?? throw new InvalidOperationException("The picture has no pixels."));
    }

    /// <summary>A texture page item as pixels (a frame, or the picture of a tile set).</summary>
    public static PixelBuffer ReadItem(GameMakerTexturePageItem item, string name)
    {
        using var worker = new TextureWorker();
        using var image = worker.GetTextureFor(item, name);
        image.Alpha(AlphaOption.Set);
        using var pixels = image.GetPixels();
        return new PixelBuffer((int)image.Width, (int)image.Height, pixels.ToByteArray(PixelMapping.RGBA) ?? throw new InvalidOperationException("The picture has no pixels."));
    }

    /// <summary>A texture page item cut from its page, as a PNG (UTMT-TEX-02).</summary>
    public static byte[] ItemToPng(GameMakerTexturePageItem item, string name)
    {
        using var worker = new TextureWorker();
        using var image = worker.GetTextureFor(item, name);
        return image.ToByteArray(MagickFormat.Png);
    }

    /// <summary>The pictures are kept as base64 in the journal arguments, so the total is capped.</summary>
    public const long MaxPngBytes = 64L * 1024 * 1024;

    public static List<byte[]> ToPngs(IEnumerable<PixelBuffer> frames, long maxBytes)
    {
        var pngs = frames.Select(ToPng).ToList();
        var total = pngs.Sum(p => (long)p.Length);
        return total > maxBytes ? throw new InvalidDataException($"The frames are {total / 1048576} MB as PNG; the limit is {maxBytes / 1048576} MB. Import fewer or smaller frames.") : pngs;
    }

    public static byte[] ToPng(PixelBuffer picture)
    {
        var settings = new PixelReadSettings((uint)picture.Width, (uint)picture.Height, StorageType.Char, PixelMapping.RGBA);
        using var image = new MagickImage(picture.Data, settings);
        return image.ToByteArray(MagickFormat.Png);
    }
}
