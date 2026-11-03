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
        using var worker = new TextureWorker();
        using var image = worker.GetTextureFor(item, sprite);
        image.Alpha(AlphaOption.Set);
        using var pixels = image.GetPixels();
        return new PixelBuffer((int)image.Width, (int)image.Height, pixels.ToByteArray(PixelMapping.RGBA) ?? throw new InvalidOperationException("The frame has no pixels."));
    }

    /// <summary>A texture page item cut from its page, as a PNG (UTMT-TEX-02).</summary>
    public static byte[] ItemToPng(GameMakerTexturePageItem item, string name)
    {
        using var worker = new TextureWorker();
        using var image = worker.GetTextureFor(item, name);
        return image.ToByteArray(MagickFormat.Png);
    }

    public static byte[] ToPng(PixelBuffer picture)
    {
        var settings = new PixelReadSettings((uint)picture.Width, (uint)picture.Height, StorageType.Char, PixelMapping.RGBA);
        using var image = new MagickImage(picture.Data, settings);
        return image.ToByteArray(MagickFormat.Png);
    }
}
