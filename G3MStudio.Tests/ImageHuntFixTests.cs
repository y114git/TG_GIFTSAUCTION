using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using G3MStudio.Core.Workspace;
using G3MStudio.Editor;

namespace G3MStudio.Tests;

/// <summary>Fixes found by reading the image editor: strokes broken by a second button or a lost capture, mirrored smooth strokes that replay differently, a skew that is too wide, a merge that changes the look.</summary>
public sealed class ImageHuntFixTests
{
    private static readonly Rgba Red = new(255, 0, 0, 255), Blue = new(0, 0, 255, 255);

    private static PixelBuffer Dot(int x, int y, Rgba colour)
    {
        var picture = new PixelBuffer(4, 4);
        picture.Set(x, y, colour);
        return picture;
    }

    [Theory]
    [InlineData(false, 100, LayerBlend.Normal)]
    [InlineData(true, 50, LayerBlend.Normal)]
    [InlineData(true, 100, LayerBlend.Multiply)]
    [InlineData(true, 70, LayerBlend.Screen)]
    public void MergingDownKeepsTheLookOfTheCanvas(bool visible, int opacity, LayerBlend blend)
    {
        var lower = new PaintLayer("Lower", Dot(0, 0, Red), visible, false, opacity, blend);
        var upper = new PaintLayer("Upper", Dot(1, 1, Blue));
        var stack = new LayerSet([lower, upper]);
        var merged = stack.Apply(LayerSet.Op("merge", 1));
        Assert.Equal(1, merged.Count);
        Assert.True(LayerSet.SameLook(stack.Flat, merged.Flat));
        var layer = merged.Layers[0];
        Assert.True(layer.IsPlain); // a hidden lower layer would hide the upper content too
        Assert.Equal(Blue, layer.Pixels.Get(1, 1));
    }

    [Fact]
    public void ASkewThatWouldBeWiderThanTheLimitIsRefusedBeforeAnythingIsAllocated()
    {
        var tall = new PixelBuffer(100, 5000);
        Assert.Throws<InvalidDataException>(() => tall.SkewedHorizontally(80));
        var state = new ImageState(tall);
        Assert.Throws<InvalidDataException>(() => ImageOps.Run(state, new ImageOp(ImageOps.Skew, "80")));
        Assert.Same(tall, state.Picture);
        Assert.True(new PixelBuffer(100, 40).SkewedHorizontally(20).Width > 100); // a small skew still works
    }

    private static Point Screen(ImageCanvas canvas, Window window, int x, int y) => canvas.TranslatePoint(new Point(canvas.Origin.X + (x + 0.5) * canvas.Zoom, canvas.Origin.Y + (y + 0.5) * canvas.Zoom), window)!.Value;

    private static ImageEditorView Open(out Window window, int size = 16)
    {
        var view = new ImageEditorView(new LocalImageSession(new PixelBuffer(size, size)));
        window = new Window { Width = 1100, Height = 700, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return view;
    }

    [Fact]
    public async Task AnotherButtonDuringAStrokeIsIgnoredAndTheWholeStrokeIsOneEditInOneColour()
    {
        await HeadlessApp.Run(() =>
        {
            using var view = Open(out var window);
            try
            {
                var canvas = view.Canvas;
                canvas.Primary = Red;
                canvas.Secondary = Blue;
                var edits = new List<ImageEdit>();
                canvas.Edited += edits.Add;
                window.MouseMove(Screen(canvas, window, 2, 2));
                window.MouseDown(Screen(canvas, window, 2, 2), MouseButton.Left);
                window.MouseMove(Screen(canvas, window, 5, 2), RawInputModifiers.LeftMouseButton);
                window.MouseDown(Screen(canvas, window, 5, 2), MouseButton.Right, RawInputModifiers.LeftMouseButton);
                window.MouseMove(Screen(canvas, window, 8, 2), RawInputModifiers.LeftMouseButton | RawInputModifiers.RightMouseButton);
                window.MouseUp(Screen(canvas, window, 8, 2), MouseButton.Right, RawInputModifiers.LeftMouseButton);
                window.MouseUp(Screen(canvas, window, 8, 2), MouseButton.Left);
                Dispatcher.UIThread.RunJobs();
                var edit = Assert.Single(edits);
                var op = Assert.Single(edit.Ops);
                Assert.Equal("#FF0000", op.Parts[2]);
                Assert.Equal("2,2 5,2 8,2", op.Parts[3]);
                Assert.Equal(Red, canvas.Picture.Get(3, 2));
                Assert.Equal(Red, canvas.Picture.Get(7, 2));
            }
            finally
            {
                window.Close();
            }

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task LosingTheCaptureMidStrokeEndsAndKeepsTheStrokeAndNothingPaintsWithoutAButton()
    {
        await HeadlessApp.Run(() =>
        {
            using var view = Open(out var window);
            try
            {
                var canvas = view.Canvas;
                var edits = new List<ImageEdit>();
                canvas.Edited += edits.Add;
                window.MouseMove(Screen(canvas, window, 2, 2));
                window.MouseDown(Screen(canvas, window, 2, 2), MouseButton.Left);
                window.MouseMove(Screen(canvas, window, 5, 2), RawInputModifiers.LeftMouseButton);
                canvas.RaiseEvent(new PointerCaptureLostEventArgs(canvas, new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true)));
                Assert.Single(edits); // the part drawn so far is journaled
                window.MouseMove(Screen(canvas, window, 9, 9));
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(Rgba.Clear, canvas.Picture.Get(9, 9));
                window.MouseUp(Screen(canvas, window, 9, 9), MouseButton.Left);
                Assert.Single(edits);
            }
            finally
            {
                window.Close();
            }

            return Task.CompletedTask;
        });
    }

    [Theory]
    [InlineData(SymmetryMode.Horizontal, 15)]
    [InlineData(SymmetryMode.Horizontal, 16)]
    [InlineData(SymmetryMode.Vertical, 15)]
    [InlineData(SymmetryMode.Horizontal | SymmetryMode.Vertical, 16)]
    public async Task AMirroredSmoothStrokeReplaysToTheSamePixelsAsTheLiveOne(SymmetryMode mirror, int size)
    {
        await HeadlessApp.Run(() =>
        {
            using var view = Open(out var window, size);
            try
            {
                var canvas = view.Canvas;
                canvas.Smooth = true;
                canvas.Symmetry = mirror;
                canvas.BrushSize = 4;
                canvas.Primary = new Rgba(200, 30, 30, 160);
                var edits = new List<ImageEdit>();
                canvas.Edited += edits.Add;
                var middle = size / 2;
                window.MouseMove(Screen(canvas, window, middle - 3, middle - 3));
                window.MouseDown(Screen(canvas, window, middle - 3, middle - 3), MouseButton.Left);
                window.MouseMove(Screen(canvas, window, middle, middle - 1), RawInputModifiers.LeftMouseButton);
                window.MouseMove(Screen(canvas, window, middle + 3, middle + 2), RawInputModifiers.LeftMouseButton);
                window.MouseUp(Screen(canvas, window, middle + 3, middle + 2), MouseButton.Left);
                Dispatcher.UIThread.RunJobs();
                var edit = Assert.Single(edits);
                var replay = new ImageState(new PixelBuffer(size, size));
                foreach (var op in edit.Ops) ImageOps.Run(replay, ImageOp.Parse(op.ToString()));
                Assert.Equal(canvas.Picture.Data, replay.Picture.Data);
            }
            finally
            {
                window.Close();
            }

            return Task.CompletedTask;
        });
    }
}
