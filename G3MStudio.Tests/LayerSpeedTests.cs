using System.Diagnostics;
using G3MLib.DataFile;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

/// <summary>
/// Layer actions on a large frame (2048 x 2048, ten layers with content) answer at once: hide, show, lock, rename, opacity, blend, reorder, add, delete, undo and redo.
/// Each number is the best of three runs of the whole edit (the command, its write into the texture page and the read the editor does after it).
/// </summary>
public sealed class LayerSpeedTests(ITestOutputHelper output)
{
    private const int Side = 2048, Count = 10;

    private static GameMakerData BigFrame()
    {
        var data = CopyResourcesTests.Source();
        var history = new EditHistory(data);
        history.Execute(new ImageEditCommand("spr_a", 0, null, [new ImageOp(ImageOps.Resize, Side.ToString(System.Globalization.CultureInfo.InvariantCulture), Side.ToString(System.Globalization.CultureInfo.InvariantCulture), "0", "0")]));
        var layers = Enumerable.Range(0, Count).Select(i =>
        {
            var pixels = new PixelBuffer(Side, Side);
            // a band of noise per layer: real pictures neither compress to nothing nor cover every pixel
            var seed = 2463534242u + (uint)i;
            for (var at = i * Side / Count * Side * 4; at < (i + 1) * Side / Count * Side * 4; at++)
            {
                seed ^= seed << 13;
                seed ^= seed >> 17;
                seed ^= seed << 5;
                pixels.Data[at] = (byte)seed;
            }

            return new PaintLayer("Layer " + (i + 1), pixels);
        }).ToList();
        var stack = new LayerSet(layers);
        history.Execute(new ReplaceFrameCommand("spr_a", 0, FramePicture.ToPng(stack.Flat)));
        LayerStore.Of(data).Set("spr_a", 0, stack);
        return data;
    }

    private static ImageEditCommand Op(string verb, int layer, string? extra = null) => new("spr_a", 0, null, [LayerSet.Op(verb, layer, extra)]);

    [Fact]
    public void LayerActionsOnABigFrameAnswerAtOnce()
    {
        using var data = BigFrame();
        var history = new EditHistory(data) { ByteLimit = long.MaxValue }; // the test is about speed, not about how many steps stay
        Assert.Equal(Count, LayerStore.Of(data).Read(data, "spr_a", 0).Count);
        var results = new List<(string What, double Ms)>();
        double Time(string what, Action action)
        {
            var best = double.MaxValue;
            for (var run = 0; run < 3; run++)
            {
                var watch = Stopwatch.StartNew();
                action();
                _ = LayerStore.Of(data).Read(data, "spr_a", 0).Flat; // what the editor reads after the edit
                best = Math.Min(best, watch.Elapsed.TotalMilliseconds);
            }

            results.Add((what, best));
            return best;
        }

        var flip = false;
        Time("hide or show", () => history.Execute(Op("visible", 4, (flip = !flip) ? "0" : "1")));
        Time("lock", () => history.Execute(Op("lock", 4, flip ? "1" : "0")));
        Time("rename", () => history.Execute(Op("rename", 4, "Name " + Environment.TickCount64)));
        Time("opacity", () => history.Execute(Op("opacity", 4, (flip = !flip) ? "50" : "80")));
        Time("blend", () => history.Execute(Op("blend", 4, (flip = !flip) ? "Multiply" : "Screen")));
        Time("move up", () => history.Execute(Op("move", 4, "1")));
        Time("add", () => history.Execute(Op("add", 4, "New")));
        Time("delete", () => history.Execute(Op("delete", 5)));
        Time("undo", history.Undo);
        Time("redo", history.Redo);
        foreach (var (what, ms) in results) output.WriteLine($"{what}: {ms:F1} ms");
        foreach (var (what, ms) in results) Budget.AssertUnder(ms, 250, $"{what} on {Count} layers of {Side} x {Side}", output);
    }

    [Fact]
    public async Task TheEyeButtonInTheEditorShowsTheChangeAtOnce()
    {
        using var dir = new TempDir();
        var path = dir.File("data.win");
        LayerSet stack;
        using (var data = BigFrame())
        {
            stack = LayerStore.Of(data).Read(data, "spr_a", 0);
            TestData.Write(path, data);
        }

        var userData = new UserData(dir.File("user"));
        using var workspace = await new WorkspaceService(userData, RecentFiles.Load(userData)).OpenAsync(path, cancellationToken: TestContext.Current.CancellationToken);
        LayerStore.Of(workspace.Data).Set("spr_a", 0, stack);
        await HeadlessApp.Run(async () =>
        {
            using var view = new G3MStudio.Editor.ImageEditorView(new G3MStudio.Editor.WorkspaceImageSession(workspace, "spr_a"));
            var window = await LayoutAudit.Host(view, 1200, 800);
            Assert.Equal(Count, view.Canvas.Layers.Count);
            var times = new List<double>();
            for (var run = 0; run < 4; run++)
            {
                var watch = Stopwatch.StartNew();
                Assert.True(view.Run("img.layerVisible")); // the eye of the active layer, as a click on it does
                times.Add(watch.Elapsed.TotalMilliseconds);
            }

            Assert.True(view.Canvas.Layers.Layers[^1].Visible); // four toggles: shown again
            window.Close();
            output.WriteLine($"eye button in the editor: best {times.Min():F1} ms, first {times[0]:F1} ms");
            Budget.AssertUnder(times.Skip(1).Min(), 250, $"hide or show in the editor on {Count} layers of {Side} x {Side}", output);
        });
    }
}
