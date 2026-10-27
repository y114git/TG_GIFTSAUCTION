using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using G3MLib.DataFile;
using G3MLib.DataFile.Models;
using G3MLib.DataFile.Util;
using G3MLib.Modding.Resources;
using ImageMagick;
using Xunit;

namespace G3MLib.Tests;

public sealed class FrameExporterTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"G3MLibFrames-{Guid.NewGuid():N}");

    public FrameExporterTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private static GameMakerData Load()
    {
        using FileStream input = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Fixtures", "data-2026.win"));
        return GameMakerIO.Read(input);
    }

    private static List<GameMakerTexturePageItem> Items(GameMakerData data) =>
        data.Sprites.Where(s => s != null).SelectMany(s => s.Textures).Select(t => t?.Texture).Where(t => t?.TexturePage != null).Take(40).ToList();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CutBgraEqualsTheImageFrame(bool padding)
    {
        using GameMakerData data = Load();
        using TextureWorker worker = new();
        foreach (GameMakerTexturePageItem item in Items(data))
        {
            ReadOnlyMemory<byte> raw = worker.CutBgra(item, padding, out int w, out int h);
            using IMagickImage<byte> image = worker.GetTextureFor(item, "frame", padding);
            Assert.Equal((int)image.Width, w);
            Assert.Equal((int)image.Height, h);
            Assert.Equal(image.GetPixels().ToByteArray(PixelMapping.BGRA), raw.ToArray());
        }
    }

    [Fact]
    public void EvictKeepsLaterExportsIdentical()
    {
        using GameMakerData data = Load();
        using TextureWorker worker = new();
        GameMakerTexturePageItem item = Items(data).First();
        string before = Path.Combine(_root, "before.png");
        string after = Path.Combine(_root, "after.png");
        worker.ExportAsPNG(item, before);
        worker.Evict(item.TexturePage);
        worker.ExportAsPNG(item, after);
        Assert.Equal(File.ReadAllBytes(before), File.ReadAllBytes(after));
    }

    [Fact]
    public void ExportPngWritesTheSameFilesAsTheWorker()
    {
        using GameMakerData data = Load();
        List<GameMakerTexturePageItem> items = Items(data);
        Directory.CreateDirectory(Path.Combine(_root, "batch"));
        List<FrameJob> jobs = items.Select((item, i) => new FrameJob(item, Path.Combine(_root, "batch", $"f{i}.png"), i % 2 == 0)).ToList();
        List<BatchProgress> reports = [];

        IReadOnlyList<ItemResult> results = FrameExporter.ExportPng(jobs, 2, new Progress(reports), CancellationToken.None);

        Assert.Equal(jobs.Count, results.Count(r => r.Kind == ResultKind.Written));
        using TextureWorker worker = new();
        for (int i = 0; i < jobs.Count; i++)
        {
            string expected = Path.Combine(_root, $"w{i}.png");
            worker.ExportAsPNG(jobs[i].Item, expected, null, jobs[i].Padding);
            Assert.Equal(File.ReadAllBytes(expected), File.ReadAllBytes(jobs[i].Path));
        }
        Assert.Equal(jobs.Count, reports.Max(r => r.Done));
    }

    [Fact]
    public void ExportPngReportsAFailedFrameAndContinues()
    {
        using GameMakerData data = Load();
        List<GameMakerTexturePageItem> items = Items(data).Take(1).ToList();
        List<FrameJob> jobs = [new(items[0], Path.Combine(_root, "missing", "a.png"), false), new(items[0], Path.Combine(_root, "b.png"), false)];

        IReadOnlyList<ItemResult> results = FrameExporter.ExportPng(jobs, 1, null, CancellationToken.None);

        Assert.Single(results, r => r.Kind == ResultKind.Failed);
        Assert.True(File.Exists(Path.Combine(_root, "b.png")));
    }

    [Fact]
    public void ExportPngCancels()
    {
        using GameMakerData data = Load();
        using CancellationTokenSource source = new();
        source.Cancel();
        List<FrameJob> jobs = Items(data).Select((item, i) => new FrameJob(item, Path.Combine(_root, $"c{i}.png"), false)).ToList();

        Assert.ThrowsAny<OperationCanceledException>(() => FrameExporter.ExportPng(jobs, 2, null, source.Token));
    }

    private sealed class Progress(List<BatchProgress> reports) : IProgress<BatchProgress>
    {
        public void Report(BatchProgress value) { lock (reports) reports.Add(value); }
    }
}
