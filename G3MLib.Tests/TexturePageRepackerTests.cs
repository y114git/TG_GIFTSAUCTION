using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using G3MLib.DataFile;
using G3MLib.DataFile.Models;
using G3MLib.DataFile.Util;
using ImageMagick;
using Xunit;

namespace G3MLib.Tests;

public sealed class TexturePageRepackerTests
{
    private static GameMakerData Load(string fixture)
    {
        using FileStream input = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Fixtures", fixture));
        return GameMakerIO.Read(input);
    }

    private static GameMakerData RoundTrip(GameMakerData data)
    {
        using MemoryStream stream = new();
        GameMakerIO.Write(stream, data);
        stream.Position = 0;
        return GameMakerIO.Read(stream);
    }

    private static byte[] Pixels(TextureWorker worker, GameMakerTexturePageItem item)
    {
        using IMagickImage<byte> image = worker.GetTextureFor(item, "frame");
        return image.ToByteArray(MagickFormat.Rgba);
    }

    private static List<byte[]> Snapshot(GameMakerData data)
    {
        using TextureWorker worker = new();
        return data.TexturePageItems.Select(item => Pixels(worker, item)).ToList();
    }

    private static MagickImage Solid(int width, int height, MagickColor color) => new(color, (uint)width, (uint)height);

    private static long PageArea(GameMakerData data) => data.EmbeddedTextures.Sum(t => (long)t.TextureData.Width * t.TextureData.Height);

    [Theory]
    [InlineData("data-2022.win")]
    [InlineData("data-2026.win")]
    public void RepackKeepsEveryFrameAndNeverGrowsThePages(string fixture)
    {
        using GameMakerData data = Load(fixture);
        List<byte[]> before = Snapshot(data);
        long areaBefore = PageArea(data);

        new TexturePageRepacker(data).Repack();

        Assert.Equal(before, Snapshot(data));
        Assert.True(PageArea(data) <= areaBefore, $"{fixture}: {areaBefore} -> {PageArea(data)}");
        using GameMakerData reloaded = RoundTrip(data);
        Assert.Equal(before, Snapshot(reloaded));
    }

    [Fact]
    public void ReplaceChangesOnlyTheReplacedFrame()
    {
        using GameMakerData data = Load("data-2026.win");
        List<byte[]> before = Snapshot(data);
        int index = data.TexturePageItems.ToList().FindIndex(item => item.SourceWidth > 4 && item.SourceHeight > 4 && item.TexturePage != null);
        GameMakerTexturePageItem target = data.TexturePageItems[index];
        using MagickImage replacement = Solid(target.SourceWidth + 3, target.SourceHeight + 5, new MagickColor(255, 0, 0, 255));

        TexturePageRepacker repacker = new(data);
        repacker.Replace(target, replacement);
        repacker.Repack();

        using GameMakerData reloaded = RoundTrip(data);
        using TextureWorker worker = new();
        GameMakerTexturePageItem reloadedTarget = reloaded.TexturePageItems[index];
        using IMagickImage<byte> shown = worker.GetTextureFor(reloadedTarget, "frame");
        Assert.Equal(replacement.Width, shown.Width);
        Assert.Equal(replacement.Height, shown.Height);
        Assert.Equal(replacement.ToByteArray(MagickFormat.Rgba), shown.ToByteArray(MagickFormat.Rgba));

        List<byte[]> after = Snapshot(reloaded);
        for (int i = 0; i < before.Count; i++)
        {
            if (i != index)
                Assert.Equal(before[i], after[i]);
        }
    }

    [Fact]
    public void RepackOfConfiguredDataFilesKeepsEveryFrame()
    {
        string? configured = Environment.GetEnvironmentVariable("G3MLIB_REPACK_DATA_FILES");
        if (string.IsNullOrWhiteSpace(configured))
            return;

        foreach (string path in configured.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            using FileStream input = File.OpenRead(path);
            using GameMakerData data = GameMakerIO.Read(input);
            List<string> before = Hashes(data);
            long areaBefore = PageArea(data);
            TexturePageRepacker repacker = new(data);
            using MagickImage extra = Solid(40, 30, new MagickColor(1, 2, 3, 255));
            repacker.Add(extra);
            repacker.Repack();

            using GameMakerData reloaded = RoundTrip(data);
            List<string> after = Hashes(reloaded);
            Assert.Equal(before, Hashes(data).Take(before.Count).ToList());
            Assert.Equal(before.Count + 1, after.Count);
            // The in-memory round trip has no game folder, so external pages cannot reload there.
            int[] inFile = Enumerable.Range(0, before.Count).Where(i => !data.TexturePageItems[i].TexturePage.TextureExternal).ToArray();
            Assert.NotEmpty(inFile);
            Assert.All(inFile, i => Assert.Equal(before[i], after[i]));
            Assert.True(PageArea(reloaded) <= areaBefore + (4096L * 4096), $"{path}: {areaBefore} -> {PageArea(reloaded)}");
        }
    }

    private static List<string> Hashes(GameMakerData data)
    {
        using TextureWorker worker = new();
        return data.TexturePageItems.Select(item => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Pixels(worker, item)))).ToList();
    }

    [Fact]
    public void ManyAddedFramesPackIntoFewPagesWithTheGivenPixels()
    {
        using GameMakerData data = Load("data-2026.win");
        int count = data.TexturePageItems.Count;
        TexturePageRepacker repacker = new(data);
        List<MagickImage> images = Enumerable.Range(0, 60).Select(i => Solid(9 + (i % 5), 6 + (i % 3), new MagickColor((byte)(i * 4), (byte)i, 0, 255))).ToList();
        foreach (MagickImage image in images)
            repacker.Add(image);
        repacker.Repack();

        using GameMakerData reloaded = RoundTrip(data);
        using TextureWorker worker = new();
        for (int i = 0; i < images.Count; i++)
            Assert.Equal(images[i].ToByteArray(MagickFormat.Rgba), Pixels(worker, reloaded.TexturePageItems[count + i]));
        Assert.True(PageArea(reloaded) <= 4 * 256 * 256);
    }

    [Fact]
    public void AddAppendsAnItemWithTheGivenPixels()
    {
        using GameMakerData data = Load("data-2026.win");
        int count = data.TexturePageItems.Count;
        using MagickImage image = Solid(13, 7, new MagickColor(0, 255, 0, 255));

        TexturePageRepacker repacker = new(data);
        GameMakerTexturePageItem added = repacker.Add(image);
        repacker.Repack();

        Assert.Equal(count + 1, data.TexturePageItems.Count);
        Assert.Same(added, data.TexturePageItems[count]);
        using GameMakerData reloaded = RoundTrip(data);
        using TextureWorker worker = new();
        using IMagickImage<byte> shown = worker.GetTextureFor(reloaded.TexturePageItems[count], "frame");
        Assert.Equal(image.ToByteArray(MagickFormat.Rgba), shown.ToByteArray(MagickFormat.Rgba));
        Assert.Equal(13, reloaded.TexturePageItems[count].BoundingWidth);
        Assert.Equal(7, reloaded.TexturePageItems[count].BoundingHeight);
    }

    [Fact]
    public void ItemsSharingARectangleStillShareItAfterRepack()
    {
        using GameMakerData data = Load("data-2026.win");
        var shared = data.TexturePageItems
            .GroupBy(i => (i.TexturePage, i.SourceX, i.SourceY, i.SourceWidth, i.SourceHeight))
            .Where(g => g.Count() > 1)
            .Select(g => g.ToList())
            .FirstOrDefault();
        if (shared is null)
            return;

        new TexturePageRepacker(data).Repack();

        Assert.All(shared, item => Assert.Equal((shared[0].TexturePage, shared[0].SourceX, shared[0].SourceY), (item.TexturePage, item.SourceX, item.SourceY)));
    }

    [Fact]
    public void PlanChangesNothingAndApplyEqualsRepack()
    {
        using GameMakerData planned = Load("data-2026.win");
        using GameMakerData direct = Load("data-2026.win");
        using MagickImage frame = Solid(11, 5, new MagickColor(9, 8, 7, 255));
        List<byte[]> before = Snapshot(planned);

        TexturePageRepacker repacker = new(planned);
        repacker.Add(frame);
        RepackPlan plan = repacker.Plan(new RepackOptions());
        Assert.Equal(before, Snapshot(planned));
        Assert.Equal(before.Count, planned.TexturePageItems.Count);
        Assert.Equal(1, plan.GroupCount);
        repacker.Apply(plan);

        TexturePageRepacker other = new(direct);
        other.Add(frame);
        other.Repack();
        Assert.Equal(Hashes(direct), Hashes(planned));
    }

    [Fact]
    public void PlanHonoursCancellation()
    {
        using GameMakerData data = Load("data-2026.win");
        using System.Threading.CancellationTokenSource source = new();
        source.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => new TexturePageRepacker(data).Plan(new RepackOptions(), null, source.Token));
    }

    [Fact]
    public void PreparedFrameAddsTheGivenPixels()
    {
        using GameMakerData data = Load("data-2026.win");
        int count = data.TexturePageItems.Count;
        byte[] bgra = Enumerable.Range(0, 6 * 4 * 4).Select(i => (byte)(i * 7)).ToArray();
        for (int i = 3; i < bgra.Length; i += 4) bgra[i] = 255;

        TexturePageRepacker repacker = new(data);
        GameMakerTexturePageItem item = repacker.Add(new PreparedFrame(6, 4, 2, 3, bgra));
        repacker.Repack();

        using GameMakerData reloaded = RoundTrip(data);
        using TextureWorker worker = new();
        GameMakerTexturePageItem shown = reloaded.TexturePageItems[count];
        Assert.Equal((2, 3, 8, 7), (shown.TargetX, shown.TargetY, shown.BoundingWidth, shown.BoundingHeight));
        Assert.Equal(bgra, worker.CutBgra(shown, false, out _, out _).ToArray());
        Assert.Same(item, data.TexturePageItems[count]);
    }

    [Fact]
    public void FramesLargerThanTheSizeFieldsAreRejected()
    {
        using GameMakerData data = Load("data-2026.win");
        using MagickImage wide = Solid(70000, 1, new MagickColor(1, 1, 1, 255));
        Assert.Throws<OverflowException>(() => new TexturePageRepacker(data).Add(wide));
    }

    [Fact]
    public void PageEditorReplacesOnlyTheBlittedFrame()
    {
        using GameMakerData data = Load("data-2026.win");
        List<byte[]> before = Snapshot(data);
        int index = data.TexturePageItems.ToList().FindIndex(item => item.SourceWidth > 2 && item.SourceHeight > 2 && item.TexturePage != null && !item.TexturePage.TextureExternal);
        GameMakerTexturePageItem target = data.TexturePageItems[index];
        int w = target.SourceWidth, h = target.SourceHeight;
        byte[] bgra = new byte[w * h * 4];
        for (int i = 0; i < bgra.Length; i += 4) { bgra[i] = 10; bgra[i + 1] = 20; bgra[i + 2] = 30; bgra[i + 3] = 255; }

        PageEditor editor = PageEditor.Open(target.TexturePage);
        Assert.Throws<ArgumentException>(() => editor.Blit(target, bgra, w + 1, h));
        editor.Blit(target, bgra, w, h);
        editor.Commit();

        using GameMakerData reloaded = RoundTrip(data);
        using TextureWorker worker = new();
        Assert.Equal(bgra, worker.CutBgra(reloaded.TexturePageItems[index], false, out _, out _).ToArray());
        List<byte[]> after = Snapshot(reloaded);
        for (int i = 0; i < before.Count; i++)
        {
            bool samePage = data.TexturePageItems[i].TexturePage == target.TexturePage;
            if (i != index && !samePage)
                Assert.Equal(before[i], after[i]);
        }
    }
}
