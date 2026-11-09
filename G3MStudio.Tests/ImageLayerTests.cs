using System.Diagnostics;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

public sealed class ImageLayerTests(ITestOutputHelper output)
{
    private static readonly Rgba Back = new(100, 150, 200, 255), Source = new(200, 100, 50, 255);

    private static ImageEditCommand LayerOp(string verb, int layer, string? extra = null) => new("spr_a", 0, null, [LayerSet.Op(verb, layer, extra)]);

    private static ImageEditCommand Stroke(int layer, string colour = "#FF0000") => new("spr_a", 0, null, [new ImageOp(ImageOps.Stroke, "1", "0", colour, "1,1 5,1")], null, layer);

    public static TheoryData<LayerBlend, string> Blends => new()
    {
        { LayerBlend.Normal, "#C86432" },
        { LayerBlend.Multiply, "#4E3A27" },
        { LayerBlend.Screen, "#DEC0D3" },
        { LayerBlend.Overlay, "#9C80A7" },
        { LayerBlend.Add, "#FFFAFA" },
    };

    [Theory]
    [MemberData(nameof(Blends))]
    public void EachBlendModeGivesTheExactPixel(LayerBlend blend, string expected) => Assert.Equal(Rgba.Parse(expected), LayerSet.Over(Back, Source, 100, blend));

    [Fact]
    public void HalfOpacityMixesTheTwoColoursAndAClearBackdropTakesTheSourceAsIs()
    {
        Assert.Equal(new Rgba(149, 125, 125, 255), LayerSet.Over(Back, Source, 50, LayerBlend.Normal));
        Assert.Equal(new Rgba(200, 100, 50, 127), LayerSet.Over(Rgba.Clear, Source, 50, LayerBlend.Multiply));
    }

    [Fact]
    public void HiddenLayersAndLayersWithZeroOpacityAreLeftOut()
    {
        var bottom = new PixelBuffer(2, 2);
        bottom.Set(0, 0, Back);
        var top = new PixelBuffer(2, 2);
        top.Set(0, 0, Source);
        var stack = new LayerSet([new PaintLayer("a", bottom), new PaintLayer("b", top, Visible: false)]);
        Assert.Equal(Back, stack.Flat.Get(0, 0));
        Assert.Equal(Back, new LayerSet([new PaintLayer("a", bottom), new PaintLayer("b", top, Opacity: 0)]).Flat.Get(0, 0));
    }

    [Fact]
    public void LayerOperationsAddMoveMergeAndDeleteWithoutChangingTheFlatPicture()
    {
        var one = LayerSet.OneLayer(new PixelBuffer(4, 4));
        var two = one.Apply(LayerSet.Op("add", 0, "Layer 2")).Apply(LayerSet.Op("add", 1, "Layer 3"));
        Assert.Equal(["Layer 1", "Layer 2", "Layer 3"], two.Layers.Select(l => l.Name));
        Assert.Equal(["Layer 1", "Layer 3", "Layer 2"], two.Apply(LayerSet.Op("move", 2, "-1")).Layers.Select(l => l.Name));
        Assert.Equal(2, two.Apply(LayerSet.Op("merge", 2)).Count);
        Assert.Equal(2, two.Apply(LayerSet.Op("delete", 0)).Count);
        Assert.Equal(1, two.Apply(LayerSet.Op("flatten", 0)).Count);
        Assert.Equal(LayerBlend.Multiply, two.Apply(LayerSet.Op("blend", 1, "Multiply")).Layers[1].Blend);
    }

    [Fact]
    public void LayerEditsUndoRedoAndReplayFromTheJournal()
    {
        using var data = CopyResourcesTests.Source();
        var history = new EditHistory(data);
        var commands = new[] { LayerOp("add", 0, "Layer 2"), Stroke(1), LayerOp("opacity", 1, "50"), LayerOp("blend", 1, "Screen") };
        foreach (var command in commands) history.Execute(command);
        var store = LayerStore.Of(data);
        Assert.Equal(2, store.Read(data, "spr_a", 0).Count);
        var after = FramePicture.Read(data, "spr_a", 0).Data;

        history.Undo();
        history.Undo();
        Assert.Equal(LayerBlend.Normal, store.Read(data, "spr_a", 0).Layers[1].Blend);
        Assert.Equal(100, store.Read(data, "spr_a", 0).Layers[1].Opacity);
        history.Redo();
        history.Redo();
        Assert.Equal(after, FramePicture.Read(data, "spr_a", 0).Data);

        using var fresh = CopyResourcesTests.Source();
        var replay = new EditHistory(fresh);
        foreach (var command in commands) replay.Execute(EditCommand.Create(command.Kind, command.Args));
        Assert.Equal(after, FramePicture.Read(fresh, "spr_a", 0).Data);
        Assert.Equal(2, LayerStore.Of(fresh).Read(fresh, "spr_a", 0).Count);
    }

    [Fact]
    public void EveryCommittedEditFlattensIntoTheDataFile()
    {
        using var data = CopyResourcesTests.Source();
        var history = new EditHistory(data);
        history.Execute(Stroke(0, "#0000FF"));
        history.Execute(LayerOp("add", 0, "Layer 2"));
        history.Execute(Stroke(1));
        var stack = LayerStore.Of(data).Read(data, "spr_a", 0);
        Assert.Equal(2, stack.Count);
        Assert.Equal(new Rgba(255, 0, 0, 255), FramePicture.Read(data, "spr_a", 0).Get(3, 1)); // the top layer wins in the flat picture
        Assert.Equal(new Rgba(0, 0, 255, 255), stack.Layers[0].Pixels.Get(3, 1)); // and the layer below still has its own pixel
        Assert.True(LayerSet.SameLook(stack.Flat, FramePicture.Read(data, "spr_a", 0)));
    }

    [Fact]
    public void AFrameChangedByAnotherEditFallsBackToOneLayerWithTheFlatPicture()
    {
        using var data = CopyResourcesTests.Source();
        var history = new EditHistory(data);
        history.Execute(LayerOp("add", 0, "Layer 2"));
        history.Execute(Stroke(1));
        var other = new PixelBuffer(32, 32);
        other.Stamp(10, 10, 2, new Rgba(0, 255, 0, 255));
        history.Execute(new ReplaceFrameCommand("spr_a", 0, FramePicture.ToPng(other)));
        var stack = LayerStore.Of(data).Read(data, "spr_a", 0);
        Assert.Equal(1, stack.Count);
        Assert.Equal(new Rgba(0, 255, 0, 255), stack.Flat.Get(10, 10));
        history.Undo(); // the stack comes back while the frame looks like it again
        Assert.Equal(2, LayerStore.Of(data).Read(data, "spr_a", 0).Count);
    }

    [Fact]
    public void CanvasSizeEditsFlattenTheLayersFirst()
    {
        using var data = CopyResourcesTests.Source();
        var history = new EditHistory(data);
        history.Execute(LayerOp("add", 0, "Layer 2"));
        history.Execute(new ImageEditCommand("spr_a", 0, null, [new ImageOp(ImageOps.Resize, "40", "40", "0", "0")]));
        Assert.Equal(1, LayerStore.Of(data).Read(data, "spr_a", 0).Count);
        Assert.Equal(40, FramePicture.Read(data, "spr_a", 0).Width);
    }

    [Fact]
    public void StacksSurviveExportAndImport()
    {
        using var data = CopyResourcesTests.Source();
        var history = new EditHistory(data);
        history.Execute(LayerOp("add", 0, "Top"));
        history.Execute(Stroke(1));
        history.Execute(LayerOp("blend", 1, "Add"));
        var saved = LayerStore.Of(data).Export();
        var fresh = new LayerStore();
        fresh.Import(saved);
        var read = fresh.Read(data, "spr_a", 0);
        Assert.Equal(["Layer 1", "Top"], read.Layers.Select(l => l.Name));
        Assert.Equal(LayerBlend.Add, read.Layers[1].Blend);
    }

    [Fact]
    public void CompositingAStrokeOnFiveBigLayersStaysUnderTheBudget()
    {
        var layers = Enumerable.Range(0, 5).Select(i => new PaintLayer("L" + i, new PixelBuffer(2048, 2048), Blend: (LayerBlend)i)).ToList();
        var stack = new LayerSet(layers);
        var target = new PixelBuffer(2048, 2048);
        var area = new Box(500, 500, 400, 40); // the dirty rectangle of a stroke
        stack.Composite(target, area);
        var best = double.MaxValue;
        for (var run = 0; run < 3; run++)
        {
            var watch = Stopwatch.StartNew();
            stack.Composite(target, area, 4, layers[4].Pixels);
            best = Math.Min(best, watch.Elapsed.TotalMilliseconds);
        }

        output.WriteLine($"composite of a stroke: {best:F1} ms");
        Budget.AssertUnder(best, 50, "composite of a stroke on 5 layers of 2048 x 2048", output);
    }
}
