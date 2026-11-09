using System.Diagnostics;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

public sealed class ImageEditCommandTests(ITestOutputHelper output)
{
    private static readonly Rgba Red = new(255, 0, 0, 255);

    private static ImageEditCommand Stroke() => new("spr_a", 0, null, [new ImageOp(ImageOps.Stroke, "1", "0", "#FF0000", "1,1 5,1")]);

    [Fact]
    public void OneEditChangesTheFrameUndoAndRedoMoveItBackAndForth()
    {
        using var data = CopyResourcesTests.Source();
        var history = new EditHistory(data);
        history.Execute(Stroke());
        Assert.Equal(Red, FramePicture.Read(data, "spr_a", 0).Get(3, 1));
        history.Undo();
        Assert.Equal(Rgba.Clear, FramePicture.Read(data, "spr_a", 0).Get(3, 1));
        history.Redo();
        Assert.Equal(Red, FramePicture.Read(data, "spr_a", 0).Get(3, 1));
    }

    [Fact]
    public void TheLayerOfAnEditSurvivesTheJournal()
    {
        var command = new ImageEditCommand("spr_a", 0, null, [new ImageOp(ImageOps.Stroke, "1", "0", "#FF0000", "1,1 5,1")], null, 2);
        Assert.Equal("2", command.Args["layer"]);
        Assert.Equal("2", EditCommand.Create(ImageEditCommand.Name, command.Args).Args["layer"]);
    }

    [Fact]
    public void ACommandRebuiltFromTheJournalMakesTheSamePixels()
    {
        using var data = CopyResourcesTests.Source();
        var command = new ImageEditCommand("spr_a", 0, PixelSelection.Rectangle(32, 32, 0, 0, 3, 3), [new ImageOp(ImageOps.Stroke, "1", "0", "#FF0000", "1,1 5,1"), new ImageOp(ImageOps.Flip, "h")]);
        new EditHistory(data).Execute(command);

        using var fresh = CopyResourcesTests.Source();
        new EditHistory(fresh).Execute(EditCommand.Create(command.Kind, command.Args));
        Assert.Equal(FramePicture.Read(data, "spr_a", 0).Data, FramePicture.Read(fresh, "spr_a", 0).Data);
        Assert.Equal(Red, FramePicture.Read(fresh, "spr_a", 0).Get(2, 1)); // the stroke was clipped to four columns, then mirrored inside them
    }

    [Fact]
    public void TheLiveEditorCanHandOverItsResultAndAResizeGivesTheFrameItsOwnPicture()
    {
        using var data = CopyResourcesTests.Source();
        var history = new EditHistory(data);
        var live = new PixelBuffer(40, 40);
        live.Stamp(0, 0, 1, Red);
        history.Execute(new ImageEditCommand("spr_a", 0, null, [new ImageOp(ImageOps.Resize, "40", "40", "0", "0")], live));
        var read = FramePicture.Read(data, "spr_a", 0);
        Assert.Equal((40, 40), (read.Width, read.Height));
        Assert.Equal(Red, read.Get(0, 0));
        history.Undo();
        Assert.Equal(32, FramePicture.Read(data, "spr_a", 0).Width);
    }

    [Fact]
    public void BigPicturesStayUnderTheBudgetForFillLineAndSelectionMove()
    {
        var state = new ImageState(new PixelBuffer(2048, 2048));
        ImageOps.Run(state, new ImageOp(ImageOps.Fill, "#FF0000", "5,5", "0", "1")); // warms the code paths
        Measure(state, "fill", new ImageOp(ImageOps.Fill, "#0000FF", "5,5", "0", "1"));
        Measure(state, "line", new ImageOp(ImageOps.Stroke, "3", "1", "#00FF00", "0,0 2047,2047"));
        state.Selection = PixelSelection.Rectangle(2048, 2048, 100, 100, 1900, 1900);
        ImageOps.Run(state, new ImageOp(ImageOps.Move, "1,1"));
        Measure(state, "selection move", new ImageOp(ImageOps.Move, "5,5"));
    }

    // The best of three runs: a parallel test run and the JIT add noise that is not the cost of the operation.
    private void Measure(ImageState state, string name, ImageOp op)
    {
        var best = double.MaxValue;
        for (var run = 0; run < 3; run++)
        {
            var watch = Stopwatch.StartNew();
            ImageOps.Run(state, op);
            best = Math.Min(best, watch.Elapsed.TotalMilliseconds);
        }

        output.WriteLine($"{name}: {best:F1} ms");
        Budget.AssertUnder(best, 50, name, output);
    }
}
