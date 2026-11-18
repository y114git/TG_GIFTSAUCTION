using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

public sealed class SmoothDrawingTests
{
    private static readonly Rgba Red = new(255, 0, 0, 255);

    private static int PartialPixels(PixelBuffer p) => Enumerable.Range(0, p.Width * p.Height).Count(i => p.Data[i * 4 + 3] is > 0 and < 255);

    [Fact]
    public void AHardDiagonalLineHasNoPartialPixelsAndASmoothOneHasSome()
    {
        var hard = new PixelBuffer(32, 32);
        hard.Line(2, 2, 29, 17, 3, Red, BrushShape.Round);
        var smooth = new PixelBuffer(32, 32);
        smooth.Smooth("line", [(2, 2), (29, 17)], Red, null, 3);
        Assert.Equal(0, PartialPixels(hard));
        Assert.True(PartialPixels(smooth) > 10);
        Assert.Equal(Red, smooth.Get(15, 9)); // the middle of the line is solid
    }

    [Fact]
    public void ASegmentedStrokeEqualsTheSamePathDrawnAtOnce()
    {
        var points = new (double, double)[] { (3.5, 3.5), (20.5, 9.5), (12.5, 25.5) };
        var live = new PixelBuffer(32, 32);
        var brush = new SmoothBrush(live, Red, 4);
        brush.Segment(3.5, 3.5, 20.5, 9.5);
        brush.Segment(20.5, 9.5, 12.5, 25.5);
        var once = new PixelBuffer(32, 32);
        new SmoothBrush(once, Red, 4).Path(points, false);
        Assert.Equal(once.Data, live.Data);
    }

    [Fact]
    public void SmoothShapesFillAndTheEraserFadesAlpha()
    {
        var picture = new PixelBuffer(24, 24);
        picture.Smooth("ellipse", [(2, 2), (21, 21)], null, Red, 1);
        Assert.Equal(Red, picture.Get(12, 12));
        Assert.Equal(0, picture.Get(0, 0).A);
        picture.Smooth("line", [(12, 12), (12, 12)], Rgba.Clear, null, 6);
        Assert.Equal(0, picture.Get(12, 12).A);
    }

    [Fact]
    public void TheSmoothOpReplaysToTheSamePixels()
    {
        var op = new ImageOp(ImageOps.Smooth, "polygon", "#0000FF", "#FFFF0080", "2", "3,3 20,5 10,20");
        var a = new ImageState(new PixelBuffer(24, 24));
        var b = new ImageState(new PixelBuffer(24, 24));
        ImageOps.Run(a, op);
        ImageOps.Run(b, ImageOp.Parse(op.ToString()));
        Assert.Equal(a.Picture.Data, b.Picture.Data);
        Assert.NotEqual(0, a.Picture.Get(10, 10).A);
    }
}
