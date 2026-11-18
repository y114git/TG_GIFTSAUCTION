using System.Diagnostics;
using G3MLib.DataFile;
using G3MLib.DataFile.Models;
using G3MLib.DataFile.Util;
using G3MStudio.Core.Workspace;

#pragma warning disable CA2000 // the data model owns what is added to it

namespace G3MStudio.Tests;

/// <summary>
/// Placing pictures on a large file stays quick: 40 pages of 2048 x 2048 (stored as PNG, as a game keeps them) with 5000 page items. A single frame resize,
/// a page cut to a quarter and a sprite of 200 frames resized each plan and apply within their budgets, and only the pages the edit writes are decoded.
/// </summary>
public sealed class TextureFitSpeedTests(ITestOutputHelper output)
{
    private const int Pages = 40, Side = 2048, Cell = 160, PerPage = 125, Frames = 200;

    private static GameMakerData Big()
    {
        var data = CopyResourcesTests.Source();
        var history = new EditHistory(data);
        var group = new GameMakerTextureGroupInfo { Name = data.Strings.MakeString("tg_big") };
        data.TextureGroupInfo.Add(group);
        var picture = new GMImage(Side, Side);
        var bgra = picture.GetRawImageData();
        for (var i = 0; i < bgra.Length; i += 4) (bgra[i], bgra[i + 1], bgra[i + 2], bgra[i + 3]) = ((byte)(i >> 4), (byte)(i >> 12), (byte)(i >> 20), 255);
        var png = picture.ConvertToPng().GetData();
        var pages = new List<GameMakerEmbeddedTexture>();
        for (var p = 0; p < Pages; p++)
        {
            var page = new GameMakerEmbeddedTexture
            {
                TextureData = new GameMakerEmbeddedTexture.TexData { Image = GMImage.FromPng(png) },
                TextureWidth = Side,
                TextureHeight = Side,
                Name = new GameMakerString($"Texture big {p}"),
                TextureInfo = group,
            };
            data.EmbeddedTextures.Add(page);
            group.TexturePages.Add(new(page));
            pages.Add(page);
            for (var c = 0; c < PerPage; c++)
            {
                var (w, h) = (40 + (c * 37 + p) % 110, 40 + (c * 53 + p) % 110);
                var item = new GameMakerTexturePageItem { TexturePage = page, SourceX = (ushort)(c % 12 * Cell), SourceY = (ushort)(c / 12 * Cell), SourceWidth = (ushort)w, SourceHeight = (ushort)h };
                (item.TargetWidth, item.TargetHeight, item.BoundingWidth, item.BoundingHeight) = ((ushort)w, (ushort)h, (ushort)w, (ushort)h);
                data.TexturePageItems.Add(item);
            }
        }

        // spr_a moves onto the first big page; spr_many has 200 frames of 32 x 32 over the pages
        var a = data.Sprites.First(s => s.Name.Content == "spr_a").Textures[0].Texture!;
        SpriteTemplate.RemoveItem(data, a);
        data.Sprites.First(s => s.Name.Content == "spr_a").Textures[0].Texture = data.TexturePageItems.First(i => i.TexturePage == pages[0]);
        history.Execute(ResourceTemplates.New(data, "sprites", "spr_many"));
        var many = data.Sprites.First(s => s.Name.Content == "spr_many");
        SpriteTemplate.RemoveItem(data, many.Textures[0].Texture!);
        many.Textures.Clear();
        foreach (var item in data.TexturePageItems.Where(i => i.TexturePage?.TextureInfo == group).Take(Frames)) many.Textures.Add(new GameMakerSprite.TextureEntry { Texture = item });
        foreach (var page in data.EmbeddedTextures.Where(t => t.TextureInfo is null)) Join(page, group);
        PagePixels.ForgetDecoded();
        return data;
    }

    private static void Join(GameMakerEmbeddedTexture page, GameMakerTextureGroupInfo group)
    {
        page.TextureInfo = group;
        group.TexturePages.Add(new(page));
    }

    private (double Ms, long Bytes) Measure(string what, Action action)
    {
        var bytes = GC.GetTotalAllocatedBytes(true);
        var watch = Stopwatch.StartNew();
        action();
        var result = (watch.Elapsed.TotalMilliseconds, GC.GetTotalAllocatedBytes(true) - bytes);
        output.WriteLine($"{what}: {result.TotalMilliseconds:F0} ms, {result.Item2 / 1048576.0:F1} MB allocated");
        return result;
    }

    private static int Decoded(GameMakerData data) => data.EmbeddedTextures.Count(t => t.TextureData?.Image is { } image && (image.Format == GMImage.ImageFormat.RawBgra || PagePixels.IsDecoded(image)));

    [Fact]
    public void AFrameResizeAPageResizeAndAResizeOf200FramesStayQuickOnALargeFile()
    {
        using var data = Big();
        var history = new EditHistory(data);
        var frame = data.Sprites.First(s => s.Name.Content == "spr_a").Textures[0].Texture!;

        var (plan, _) = Measure("plan one frame", () => TextureFit.Plan(data, [new FitRequest(frame, 150, 150)]));
        var (apply, _) = Measure("apply one frame", () => history.Execute(new TextureFitCommand("sprites", "spr_a", 0, FramePicture.ToPng(new PixelBuffer(150, 150)))));
        Assert.True(Decoded(data) <= 2, $"{Decoded(data)} pages decoded for one frame");
        Budget.AssertUnder(plan, 200, "plan one frame", output);
        Budget.AssertUnder(apply, 1500, "apply one frame", output);

        var page = PageResizeCommand.Find(data, "Texture big 1");
        var (pagePlan, _) = Measure("plan page cut", () => TextureFit.Plan(data, [], page, (Side / 2, Side / 2)));
        var (pageApply, _) = Measure("apply page cut", () => history.Execute(new PageResizeCommand("Texture big 1", Side / 2, Side / 2)));
        Budget.AssertUnder(pagePlan, 500, "plan page cut", output);
        Budget.AssertUnder(pageApply, 4000, "apply page cut", output);

        var many = data.Sprites.First(s => s.Name.Content == "spr_many");
        var spec = new ResizeSpec(false, 120, 120);
        var (manyPlan, _) = Measure("plan 200 frames", () => TextureFit.Plan(data, [.. many.Textures.Select(t => TextureFitCommand.RequestFor(data, "sprites", t.Texture!, 120, 120))]));
        var (manyApply, _) = Measure("apply 200 frames", () => history.Execute(SpriteResize.Build(data, "spr_many", 0, spec, true, null)));
        Budget.AssertUnder(manyPlan, 1000, "plan 200 frames", output);
        Budget.AssertUnder(manyApply, 20000, "apply 200 frames", output);
        Measure("undo 200 frames", history.Undo);
    }
}
