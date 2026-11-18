using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using G3MStudio.Core.Workspace;
using G3MStudio.Editor;
using G3MStudio.Rooms;

#pragma warning disable CA2000 // sessions and windows made for a check only
namespace G3MStudio.Tests;

/// <summary>The second hunt in the image editor: drags that end with the picture, locked and hidden layers, the picker, the undo cap and the sprite canvas grabs.</summary>
public sealed class ImageToolBehaviorTests
{
    private static readonly Rgba Red = new(255, 0, 0, 255);

    private static Point Screen(ImageCanvas canvas, Window window, int x, int y) => canvas.TranslatePoint(new Point(canvas.Origin.X + (x + 0.5) * canvas.Zoom, canvas.Origin.Y + (y + 0.5) * canvas.Zoom), window)!.Value;

    private static ImageEditorView Open(out Window window, int size = 16)
    {
        var view = new ImageEditorView(new LocalImageSession(new PixelBuffer(size, size)));
        window = new Window { Width = 1100, Height = 700, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return view;
    }

    private static LayerSet Stack(bool locked = false, bool visible = true)
    {
        var below = new PixelBuffer(16, 16);
        below.Set(1, 1, Red);
        return new LayerSet([new PaintLayer("Below", below), new PaintLayer("Top", new PixelBuffer(16, 16), visible, locked, 100, LayerBlend.Normal)]);
    }

    [Fact]
    public async Task AnUndoMidStrokeEndsTheStrokeAndItsRestIsNotJournaledOnTheReloadedPicture()
    {
        await HeadlessApp.Run(() =>
        {
            using var view = Open(out var window);
            try
            {
                var canvas = view.Canvas;
                canvas.Smooth = true;
                var edits = new List<ImageEdit>();
                canvas.Edited += edits.Add;
                window.MouseMove(Screen(canvas, window, 1, 1));
                window.MouseDown(Screen(canvas, window, 1, 1), MouseButton.Left);
                window.MouseUp(Screen(canvas, window, 1, 1), MouseButton.Left);
                Assert.Single(edits);
                window.MouseDown(Screen(canvas, window, 4, 4), MouseButton.Left);
                window.MouseMove(Screen(canvas, window, 7, 4), RawInputModifiers.LeftMouseButton);
                view.Undo(); // the first stroke goes, the picture is read again under the second
                window.MouseMove(Screen(canvas, window, 10, 4), RawInputModifiers.LeftMouseButton);
                window.MouseUp(Screen(canvas, window, 10, 4), MouseButton.Left);
                Dispatcher.UIThread.RunJobs();
                Assert.Single(edits); // the second stroke ended with the reload, no edit of its own
                Assert.Equal(Rgba.Clear, canvas.Picture.Get(10, 4));
                Assert.Equal(Rgba.Clear, canvas.Picture.Get(1, 1));
            }
            finally
            {
                window.Close();
            }

            return Task.CompletedTask;
        });
    }

    [Theory]
    [InlineData(true, false, "image.locked")]
    [InlineData(false, false, "image.layerHidden")]
    public async Task ALockedOrHiddenLayerRefusesEveryPixelEditAndSaysWhy(bool locked, bool visible, string key)
    {
        await HeadlessApp.Run(() =>
        {
            using var view = Open(out var window);
            try
            {
                var canvas = view.Canvas;
                canvas.SetLayers(Stack(locked, visible), 1);
                var (edits, said) = (new List<ImageEdit>(), new List<string>());
                canvas.Edited += edits.Add;
                canvas.Refused += said.Add;
                canvas.Run(new ImageOp(ImageOps.Clear));
                canvas.Run(new ImageOp(ImageOps.Fill, ImageOp.Format(Red), ImageOp.Format([(2, 2)]), ImageOp.Format(0), "1"));
                canvas.Run(new ImageOp(ImageOps.Flip, "h"));
                window.MouseMove(Screen(canvas, window, 3, 3));
                window.MouseDown(Screen(canvas, window, 3, 3), MouseButton.Left);
                window.MouseMove(Screen(canvas, window, 6, 3), RawInputModifiers.LeftMouseButton);
                window.MouseUp(Screen(canvas, window, 6, 3), MouseButton.Left);
                Assert.Empty(edits);
                Assert.Equal(Rgba.Clear, canvas.Picture.Get(4, 3));
                Assert.All(said, k => Assert.Equal(key, k));
                Assert.True(said.Count >= 4);
            }
            finally
            {
                window.Close();
            }

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task ACutOrPasteOnALockedLayerChangesNothingAndKeepsTheTool()
    {
        await HeadlessApp.Run(() =>
        {
            using var view = Open(out var window);
            try
            {
                view.CopySelection(false); // something to paste
                var canvas = view.Canvas;
                canvas.SetLayers(Stack(locked: true), 1);
                var said = new List<string>();
                canvas.Refused += said.Add;
                view.CopySelection(true);
                view.PasteClipboard();
                Assert.Equal(["image.locked", "image.locked"], said);
                Assert.Equal(ImageTool.Pencil, view.Tool); // a refused paste does not switch to the Move tool
            }
            finally
            {
                window.Close();
            }

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task ThePickerTakesTheColourOnShowNotTheActiveLayerAlone()
    {
        await HeadlessApp.Run(() =>
        {
            using var view = Open(out var window);
            try
            {
                var canvas = view.Canvas;
                canvas.SetLayers(Stack(), 1); // the red pixel is on the layer below, the active layer is empty there
                view.SetTool(ImageTool.Picker);
                window.MouseMove(Screen(canvas, window, 1, 1));
                window.MouseDown(Screen(canvas, window, 1, 1), MouseButton.Left);
                window.MouseUp(Screen(canvas, window, 1, 1), MouseButton.Left);
                Assert.Equal(Red, canvas.Primary);
            }
            finally
            {
                window.Close();
            }

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task ABothShapeStyleSurvivesTheShapeShortcutAndAFillIsStillNoStaleFill()
    {
        await HeadlessApp.Run(() =>
        {
            using var view = Open(out var window);
            try
            {
                view.Run("img.tool.rectangleFilled");
                Assert.Equal(ShapeStyle.Filled, view.Canvas.Style);
                view.Run("img.tool.rectangle");
                Assert.Equal(ShapeStyle.Outline, view.Canvas.Style);
                view.SetStyle(ShapeStyle.Both);
                view.Run("img.tool.ellipse");
                Assert.Equal(ShapeStyle.Both, view.Canvas.Style);
            }
            finally
            {
                window.Close();
            }

            return Task.CompletedTask;
        });
    }

    [Fact]
    public void TheUndoStepsOfAPictureWindowAreCappedByCountAndByMemory()
    {
        var session = new LocalImageSession(new PixelBuffer(4, 4));
        for (var i = 0; i < LocalImageSession.MaxUndoSteps + 30; i++) session.Commit(0, new ImageEdit(null, [], new PixelBuffer(4, 4)));
        var steps = 0;
        for (; session.CanUndo; steps++) session.Undo();
        Assert.Equal(LocalImageSession.MaxUndoSteps, steps);

        // Huge pictures keep fewer steps: the same big picture committed again and again counts each time.
        var big = new PixelBuffer(1024, 1024);
        var heavy = new LocalImageSession(big);
        for (var i = 0; i < 100; i++) heavy.Commit(0, new ImageEdit(null, [], big));
        steps = 0;
        for (; heavy.CanUndo; steps++) heavy.Undo();
        Assert.InRange(steps, 1, (int)(LocalImageSession.MaxUndoBytes / big.Data.Length));
    }

    // The box blur as it was written first: every neighbour of every pixel read one by one.
    private static PixelBuffer SlowBlur(PixelBuffer picture, int size, PixelSelection? clip)
    {
        var result = picture.Copy();
        for (var y = 0; y < picture.Height; y++)
            for (var x = 0; x < picture.Width; x++)
            {
                if (picture.Get(x, y).A == 0 || (clip is not null && !clip.Contains(x, y))) continue;
                double r = 0, g = 0, b = 0, a = 0, count = 0;
                for (var dy = -size; dy <= size; dy++)
                    for (var dx = -size; dx <= size; dx++)
                    {
                        if (!picture.Contains(x + dx, y + dy)) continue;
                        var c = picture.Get(x + dx, y + dy);
                        (r, g, b, a, count) = (r + c.R * c.A, g + c.G * c.A, b + c.B * c.A, a + c.A, count + 1);
                    }

                result.Set(x, y, a == 0 ? Rgba.Clear : new Rgba((byte)(r / a), (byte)(g / a), (byte)(b / a), (byte)(a / count)));
            }

        return result;
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(3, false)]
    [InlineData(8, false)]
    [InlineData(2, true)]
    public void TheRunningSumBlurGivesTheSamePixelsAsAddingUpEveryNeighbour(int radius, bool clipped)
    {
        static byte Mix(int x, int y, int k) => (byte)((x * 37 + y * 91 + k * 53 + x * y * 7) % 256);
        var picture = new PixelBuffer(19, 11);
        for (var y = 0; y < 11; y++)
            for (var x = 0; x < 19; x++) picture.Set(x, y, Mix(x, y, 0) % 4 == 0 ? Rgba.Clear : new Rgba(Mix(x, y, 1), Mix(x, y, 2), Mix(x, y, 3), (byte)(1 + Mix(x, y, 4) % 255)));
        var clip = clipped ? PixelSelection.Rectangle(19, 11, 3, 2, 12, 8) : null;
        var expected = SlowBlur(picture, radius, clip);
        picture.Clip = clip;
        picture.Blur(radius);
        Assert.Equal(expected.Data, picture.Data);
    }

    private static async Task Settle(Func<Task> body) => await HeadlessApp.Run(body);

    private static (Point Origin, Func<double, double, Point> At) Grid(SpriteCanvas canvas) => (default, (x, y) => new Point(SpriteCanvas.Pad + x * canvas.Zoom, SpriteCanvas.Pad + y * canvas.Zoom));

    [Theory]
    [InlineData("escape")]
    [InlineData("capture")]
    public async Task EscapeOrALostCaptureEndsAnOriginDragWithoutMovingTheOrigin(string how)
    {
        var (workspace, sprite, dir) = await AutoRefreshTests.OpenAsync();
        using (dir)
        using (workspace)
        {
            await Settle(() =>
            {
                using var textures = new TextureBitmaps();
                var canvas = new SpriteCanvas(sprite, textures);
                var window = new Window { Content = canvas, Width = 900, Height = 900 };
                window.Show();
                try
                {
                    var changes = new List<(int, int)>();
                    canvas.OriginChanged += (x, y) => changes.Add((x, y));
                    var (_, at) = Grid(canvas);
                    var (ox, oy) = (sprite.OriginX + 0.5, sprite.OriginY + 0.5);
                    window.MouseDown(at(ox, oy), MouseButton.Left);
                    window.MouseMove(at(ox + 3, oy), RawInputModifiers.LeftMouseButton);
                    if (how == "escape") window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
                    else canvas.RaiseEvent(new PointerCaptureLostEventArgs(canvas, new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true)));
                    window.MouseMove(at(ox + 6, oy), RawInputModifiers.None);
                    window.MouseUp(at(ox + 6, oy), MouseButton.Left);
                    Assert.Empty(changes);
                }
                finally
                {
                    window.Close();
                }

                return Task.CompletedTask;
            });
        }
    }

    [Fact]
    public async Task AnotherButtonDuringAnOriginDragNeitherEndsNorRestartsIt()
    {
        var (workspace, sprite, dir) = await AutoRefreshTests.OpenAsync();
        using (dir)
        using (workspace)
        {
            await Settle(() =>
            {
                using var textures = new TextureBitmaps();
                var canvas = new SpriteCanvas(sprite, textures);
                var window = new Window { Content = canvas, Width = 900, Height = 900 };
                window.Show();
                try
                {
                    var changes = new List<(int, int)>();
                    canvas.OriginChanged += (x, y) => changes.Add((x, y));
                    var (_, at) = Grid(canvas);
                    var (ox, oy) = (sprite.OriginX + 0.5, sprite.OriginY + 0.5);
                    window.MouseDown(at(ox, oy), MouseButton.Left);
                    window.MouseMove(at(ox + 3, oy), RawInputModifiers.LeftMouseButton);
                    window.MouseDown(at(ox + 3, oy), MouseButton.Right, RawInputModifiers.LeftMouseButton);
                    window.MouseUp(at(ox + 3, oy), MouseButton.Right, RawInputModifiers.LeftMouseButton);
                    Assert.Empty(changes); // the right button's release did not commit the drag
                    window.MouseUp(at(ox + 3, oy), MouseButton.Left);
                    Assert.Equal([(sprite.OriginX + 3, sprite.OriginY)], changes);
                }
                finally
                {
                    window.Close();
                }

                return Task.CompletedTask;
            });
        }
    }
}
