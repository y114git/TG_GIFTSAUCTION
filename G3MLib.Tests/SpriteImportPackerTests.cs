using System;
using System.IO;
using System.Linq;
using G3MLib.DataFile;
using G3MLib.DataFile.Models;
using G3MLib.DataFile.Util;
using G3MLib.Modding.Resources;
using ImageMagick;
using Xunit;

namespace G3MLib.Tests;

public sealed class SpriteImportPackerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"G3MLibSprites-{Guid.NewGuid():N}");

    public SpriteImportPackerTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private void WriteFrame(string sprite, int frame, int width, int height, MagickColor color)
    {
        string folder = Path.Combine(_root, sprite);
        Directory.CreateDirectory(folder);
        string meta = Path.Combine(folder, sprite + ".json");
        if (!File.Exists(meta))
            File.WriteAllText(meta, "{}");
        new MagickImage(color, (uint)width, (uint)height).Write(Path.Combine(folder, $"{sprite}_{frame}.png"), MagickFormat.Png32);
    }

    [Fact]
    public void ImportedSpritesSurviveASaveAndKeepTheirPixelsAndWholePageFrames()
    {
        WriteFrame("spr_a", 0, 20, 10, MagickColors.Red);
        WriteFrame("spr_a", 1, 20, 10, MagickColors.Blue);
        WriteFrame("spr_b", 0, 33, 17, MagickColors.Green);
        WriteFrame("spr_wide", 0, 2048, 6, MagickColors.White);

        string output = Path.Combine(_root, "out.win");
        using (GameMakerData data = GameMakerData.CreateNew2026LTS())
        {
            ResourceImportService.SetPatchFileSystem(null);
            ResourceImportService.SetDataPaths(null, null);
            Assert.True(ResourceImportService.Import("Sprites", data, _root));
            using FileStream stream = File.Create(output);
            GameMakerIO.Write(stream, data);
        }

        using FileStream input = File.OpenRead(output);
        using GameMakerData reread = GameMakerIO.Read(input);
        Assert.Equal(new[] { "spr_a", "spr_b", "spr_wide" }, reread.Sprites.Select(s => s.Name.Content).OrderBy(n => n));
        Assert.Equal(2, reread.Sprites.ByName("spr_a").Textures.Count);
        Assert.Equal(new[] { (ushort)2048 }, new[] { reread.Sprites.ByName("spr_wide").Textures[0].Texture.SourceWidth });

        using TextureWorker worker = new();
        (string name, int frame, MagickColor color)[] expected = [("spr_a", 0, MagickColors.Red), ("spr_a", 1, MagickColors.Blue), ("spr_b", 0, MagickColors.Green), ("spr_wide", 0, MagickColors.White)];
        foreach (var (name, frame, color) in expected)
        {
            using IMagickImage<byte> pixels = worker.GetTextureFor(reread.Sprites.ByName(name).Textures[frame].Texture, name);
            Assert.Equal(color, pixels.GetPixels().GetPixel(0, 0).ToColor());
        }
    }
}
