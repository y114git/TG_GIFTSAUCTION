using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using G3MLib.DataFile;
using G3MStudio.Rooms;

namespace G3MStudio.Tests;

/// <summary>
/// Room render spike: build time and pan frame cost of a custom control. Numbers go to the test output (use -showLiveOutput).
/// The corpus cases run only when G3MSTUDIO_CORPUS_DIR names a folder that holds data.win files; the synthetic case always runs.
/// </summary>
public sealed class RoomBenchmarkTests(ITestOutputHelper output)
{
    private const int PanFrames = 240;
    private const int WarmupFrames = 10;
    private const double ViewWidth = 1100, ViewHeight = 700;

    [Fact]
    public Task SyntheticRoomPans() => Measure("synthetic 300 x 200 tiles", () => RoomScene.Synthetic(300, 200));

    [Fact]
    public Task LargestGms2CorpusRoomPans() => MeasureCorpus(gms2: true);

    [Fact]
    public Task LargestGms1CorpusRoomPans() => MeasureCorpus(gms2: false);

    private Task MeasureCorpus(bool gms2)
    {
        var folder = Environment.GetEnvironmentVariable("G3MSTUDIO_CORPUS_DIR");
        Assert.SkipWhen(string.IsNullOrEmpty(folder) || !Directory.Exists(folder), "G3MSTUDIO_CORPUS_DIR is not set");
        var (file, index, label) = FindRoom(folder, gms2);
        Assert.SkipWhen(file is null, $"no {(gms2 ? "GMS2" : "GMS1")} room with content in the corpus");
        return Measure(label, () =>
        {
            // Textures are converted while the data file is open; the bitmaps outlive it.
            using var stream = File.OpenRead(file);
            using var data = GameMakerIO.Read(stream);
            return RoomScene.Build(data.Rooms[index]);
        });
    }

    // Layers present means a Game Maker Studio 2 room. Size = tiles and instances the room draws.
    private static (string? File, int Index, string Label) FindRoom(string folder, bool gms2)
    {
        var best = (File: (string?)null, Index: -1, Label: "", Count: 0);
        foreach (var file in Directory.EnumerateFiles(folder, "data.win", SearchOption.AllDirectories))
        {
            using var stream = File.OpenRead(file);
            using var data = GameMakerIO.Read(stream);
            for (var i = 0; i < data.Rooms.Count; i++)
            {
                var room = data.Rooms[i];
                if (room.Layers?.Count > 0 != gms2) continue;
                var count = gms2
                    ? room.Layers!.Sum(l => (l.InstancesData?.Instances.Count ?? 0) + (l.TilesData?.TileData?.Sum(r => r.Count(t => t != 0)) ?? 0))
                    : (room.Tiles?.Count ?? 0) + (room.GameObjects?.Count ?? 0);
                if (count > best.Count) best = (file, i, $"{Path.GetFileName(Path.GetDirectoryName(file))} {room.Name.Content} ({room.Width}x{room.Height}, {count:N0} tiles and instances)", count);
            }
        }

        return (best.File, best.Index, best.Label);
    }

    private async Task Measure(string label, Func<RoomScene> build)
    {
        var watch = Stopwatch.StartNew();
        RoomScene? scene = null;
        double[] frames = [];
        double firstFrame = 0, built = 0;
        var drawn = 0;
        await HeadlessApp.Run(() =>
        {
            scene = build();
            built = watch.Elapsed.TotalMilliseconds;
            (firstFrame, frames, drawn) = Pan(scene);
        });
        output.WriteLine($"{label}: {scene!.Items.Count:N0} items ({scene.Skipped:N0} skipped), room {scene.Width}x{scene.Height}");
        output.WriteLine($"build (read, decode pages, draw list): {built:F1} ms; first frame: {firstFrame:F1} ms; items drawn in the last frame: {drawn:N0}");
        output.WriteLine($"pan frame: mean {frames.Average():F2} ms, p95 {frames.Order().ElementAt((int)(frames.Length * 0.95)):F2} ms, max {frames.Max():F2} ms over {frames.Length} frames");
        Assert.NotEmpty(scene.Items);
        Budget.AssertUnder(frames.Order().ElementAt((int)(frames.Length * 0.95)), 100, "p95 pan frame cost", output);
    }

    private static (double First, double[] Frames, int Drawn) Pan(RoomScene scene)
    {
        var view = new RoomView { Scene = scene };
        var window = new Window { Width = ViewWidth, Height = ViewHeight, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        var watch = Stopwatch.StartNew();
        window.CaptureRenderedFrame();
        var first = watch.Elapsed.TotalMilliseconds;

        // Triangle sweep, so even a room smaller than the window keeps content in view.
        var rangeX = Math.Max(scene.Width - ViewWidth, 300);
        var rangeY = Math.Max(scene.Height - ViewHeight, 300);
        var startX = scene.Width < ViewWidth ? (scene.Width - ViewWidth) / 2 - 150 : 0;
        var startY = scene.Height < ViewHeight ? (scene.Height - ViewHeight) / 2 - 150 : 0;
        var frames = new List<double>();
        var seen = new HashSet<string>();
        for (var i = 0; i < PanFrames + WarmupFrames; i++)
        {
            view.Offset = new Point(startX + Triangle(i * 3, rangeX), startY + Triangle(i * 2, rangeY));
            watch.Restart();
            Dispatcher.UIThread.RunJobs();
            var frame = window.CaptureRenderedFrame()!;
            var ms = watch.Elapsed.TotalMilliseconds;
            if (i >= WarmupFrames) frames.Add(ms);
            using var png = new MemoryStream();
            frame.Save(png, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
            seen.Add(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(png.ToArray())));
        }

        // Every pan step must have produced a different picture, or the numbers measure nothing.
        Assert.True(seen.Count > (PanFrames + WarmupFrames) * 0.8, $"only {seen.Count} distinct frames");
        Assert.True(view.DrawnItems > 0, "the last frame drew nothing");
        var drawn = view.DrawnItems;
        window.Close();
        return (first, frames.ToArray(), drawn);
    }

    private static double Triangle(double distance, double range)
    {
        var phase = distance % (2 * range);
        return phase <= range ? phase : 2 * range - phase;
    }
}
