using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using G3MLib.DataFile;
using G3MLib.DataFile.Models;
using G3MLib.DataFile.Util;
using ImageMagick;
using Xunit;

namespace G3MLib.Tests;

public sealed class TextureWorkerTests
{
    private static byte[] Encode(IMagickImage<byte> image)
    {
        using MemoryStream stream = new();
        image.Write(stream, MagickFormat.Png32);
        return stream.ToArray();
    }

    // Reference crop through the cached page image, the way frames were cut before the raw page cache.
    private static byte[] Reference(TextureWorker worker, GameMakerTexturePageItem item)
    {
        using IMagickImage<byte> image = worker.GetEmbeddedTexture(item.TexturePage).CloneArea(item.SourceX, item.SourceY, item.SourceWidth, item.SourceHeight);
        if (item.SourceWidth != item.TargetWidth || item.SourceHeight != item.TargetHeight)
        {
            image.InterpolativeResize(item.TargetWidth, item.TargetHeight, PixelInterpolateMethod.Bilinear);
        }
        image.Strip();
        return Encode(image);
    }

    [Fact]
    public void ExportedPngMatchesDirectEncodeAndRepeatedFramesAreIdentical()
    {
        using FileStream input = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Fixtures", "data-2026.win"));
        using GameMakerData data = GameMakerIO.Read(input);
        GameMakerTexturePageItem item = data.Sprites.Where(s => s != null).SelectMany(s => s.Textures).First(t => t?.Texture?.TexturePage != null).Texture;
        string directory = Path.Combine(Path.GetTempPath(), $"G3MLib-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            using TextureWorker worker = new();
            string first = Path.Combine(directory, "a.png");
            string second = Path.Combine(directory, "b.png");
            worker.ExportAsPNG(item, first);
            worker.ExportAsPNG(item, second);
            using IMagickImage<byte> frame = worker.GetTextureFor(item, "frame");
            Assert.Equal(Encode(frame), File.ReadAllBytes(first));
            Assert.Equal(File.ReadAllBytes(first), File.ReadAllBytes(second));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Theory]
    [InlineData("data-2022.win")]
    [InlineData("data-2026.win")]
    public void FramesMatchTheCachedPageCropWhenCutInParallel(string fixture)
    {
        using FileStream input = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Fixtures", fixture));
        using GameMakerData data = GameMakerIO.Read(input);
        GameMakerTexturePageItem[] items = data.Sprites.Where(s => s != null).SelectMany(s => s.Textures).Where(t => t?.Texture?.TexturePage != null).Select(t => t.Texture).ToArray();
        Assert.NotEmpty(items);
        using TextureWorker worker = new();
        Parallel.ForEach(items, item =>
        {
            using IMagickImage<byte> frame = worker.GetTextureFor(item, "frame");
            Assert.Equal(Reference(worker, item), Encode(frame));
        });
    }
}
