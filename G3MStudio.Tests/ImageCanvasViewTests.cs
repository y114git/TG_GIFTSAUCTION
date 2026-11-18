using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using G3MStudio.Core.Workspace;
using G3MStudio.Editor;

namespace G3MStudio.Tests;

/// <summary>The view of the image canvas is a zoom and a pan over an endless background: Fit, zoom limits, pan gestures and exact pointer to pixel mapping.</summary>
public sealed class ImageCanvasViewTests
{
    private static (Window Window, ImageCanvas Canvas) Show(int pictureWidth, int pictureHeight, double width, double height)
    {
        var canvas = new ImageCanvas(new ImageState(new PixelBuffer(pictureWidth, pictureHeight)));
        var window = new Window { Width = width, Height = height, Content = canvas };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        return (window, canvas);
    }

    private static Rect Picture(ImageCanvas canvas) => new(canvas.Origin.X, canvas.Origin.Y, canvas.Picture.Width * canvas.Zoom, canvas.Picture.Height * canvas.Zoom);

    [Theory]
    [InlineData(2048, 1024, 600, 400)]
    [InlineData(2048, 1024, 900, 500)]
    [InlineData(2048, 1024, 300, 200)]
    [InlineData(2048, 1024, 3000, 2000)]
    [InlineData(4096, 4096, 800, 600)]
    [InlineData(64, 64, 700, 500)]
    [InlineData(1, 300, 500, 400)]
    public async Task FitShowsTheWholePictureCentredInsideTheViewportWithAMargin(int pw, int ph, int width, int height)
    {
        await HeadlessApp.Run(() =>
        {
            var (window, canvas) = Show(pw, ph, width, height);
            try
            {
                // Nobody called Fit: the first layout did.
                var view = canvas.Bounds;
                var rect = Picture(canvas);
                Assert.True(rect.Left >= 0 && rect.Top >= 0 && rect.Right <= view.Width + 1e-6 && rect.Bottom <= view.Height + 1e-6, $"{rect} not inside {view}");
                Assert.True(Math.Min(rect.Left, rect.Top) > 0, "no margin");
                Assert.InRange(Math.Abs(rect.Center.X - view.Width / 2), 0, 1e-6);
                Assert.InRange(Math.Abs(rect.Center.Y - view.Height / 2), 0, 1e-6);
                // The largest zoom that fits: whole steps from 1 up.
                if (canvas.Zoom >= 1) Assert.Equal(Math.Floor(canvas.Zoom), canvas.Zoom);
                canvas.PanBy(new Vector(500, -300));
                canvas.ZoomTo(canvas.Zoom * 3);
                canvas.Fit();
                Assert.Equal(rect, Picture(canvas));
            }
            finally { window.Close(); }

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task FitFollowsAResizeOfTheViewportUntilTheUserMovesTheView()
    {
        await HeadlessApp.Run(() =>
        {
            var (window, canvas) = Show(2048, 1024, 600, 400);
            try
            {
                var small = canvas.Zoom;
                window.Width = 1400;
                window.Height = 900;
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                Assert.True(canvas.Zoom > small);
                var rect = Picture(canvas);
                Assert.True(rect.Left >= 0 && rect.Right <= canvas.Bounds.Width && rect.Top >= 0 && rect.Bottom <= canvas.Bounds.Height);

                // After a pan the picture keeps its zoom and the middle of the view stays on the same picture point.
                canvas.PanBy(new Vector(40, 10));
                var (zoom, middle) = (canvas.Zoom, canvas.ToPicture(new Point(canvas.Bounds.Width / 2, canvas.Bounds.Height / 2)));
                window.Width = 1000;
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                Assert.Equal(zoom, canvas.Zoom);
                var after = canvas.ToPicture(new Point(canvas.Bounds.Width / 2, canvas.Bounds.Height / 2));
                Assert.InRange(Math.Abs(after.X - middle.X), 0, 1e-6);
                Assert.InRange(Math.Abs(after.Y - middle.Y), 0, 1e-6);
            }
            finally { window.Close(); }

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task ASmallerPictureInAPannedViewRaisesTheZoomToItsOwnMinimum()
    {
        await HeadlessApp.Run(() =>
        {
            var (window, canvas) = Show(4096, 4096, 800, 600);
            try
            {
                for (var i = 0; i < 40; i++) canvas.Step(-1); // not fitted any more, at the lowest zoom of a 4096 picture
                Assert.Equal(1.0 / 256, canvas.Zoom);
                canvas.State.Picture = new PixelBuffer(64, 64);
                canvas.Refresh();
                Assert.Equal(canvas.MinZoom, canvas.Zoom);
                Assert.True(canvas.Zoom >= 0.25, "the 64 pixel picture is at least 16 screen pixels wide");
            }
            finally { window.Close(); }

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task ZoomGoesFarBelowTheOldLimitOfOneAndFarAboveIt()
    {
        await HeadlessApp.Run(() =>
        {
            var (window, canvas) = Show(2048, 1024, 900, 600);
            try
            {
                for (var i = 0; i < 40; i++) canvas.Step(-1);
                Assert.Equal(canvas.MinZoom, canvas.Zoom);
                Assert.Equal(1.0 / 128, canvas.Zoom); // the long side of the picture is 16 pixels
                Assert.Equal("0.78%", canvas.ZoomText);
                canvas.ZoomBy(0.5);
                Assert.Equal(canvas.MinZoom, canvas.Zoom); // no further
                var halved = 0;
                canvas.Fit();
                canvas.ZoomTo(1);
                for (var z = canvas.Zoom; z > 1.0 / 64; z = canvas.Zoom)
                {
                    canvas.Step(-1);
                    Assert.True(canvas.Zoom < z);
                    halved++;
                }

                Assert.True(halved >= 6);
                for (var i = 0; i < 60; i++) canvas.Step(1);
                Assert.Equal(ImageCanvas.MaxZoom, canvas.Zoom);
                Assert.Equal("256x", canvas.ZoomText);
            }
            finally { window.Close(); }

            return Task.CompletedTask;
        });
    }

    [Theory]
    [InlineData(1.0 / 8, 0, 0)]
    [InlineData(0.3, 55, -20)]
    [InlineData(1, -700, 300)]
    [InlineData(2.5, 120, 60)]
    [InlineData(8, -1000, -500)]
    [InlineData(37, 300, 900)]
    public async Task APointerPositionMapsToTheSamePixelAtAnyZoomAndPan(double zoom, double panX, double panY)
    {
        await HeadlessApp.Run(() =>
        {
            var (window, canvas) = Show(2048, 1024, 800, 500);
            try
            {
                canvas.ZoomTo(zoom);
                canvas.PanBy(new Vector(panX, panY));
                foreach (var (px, py) in new[] { (0, 0), (1, 1), (17, 3), (1023, 511), (2047, 1023), (-3, 4000) })
                {
                    // The middle of the pixel and its two corners that belong to it.
                    var origin = canvas.Origin;
                    var middle = new Point(origin.X + (px + 0.5) * canvas.Zoom, origin.Y + (py + 0.5) * canvas.Zoom);
                    Assert.Equal((px, py), canvas.Cell(middle));
                    Assert.Equal((px, py), canvas.Cell(new Point(origin.X + px * canvas.Zoom + 1e-6, origin.Y + py * canvas.Zoom + 1e-6)));
                    Assert.Equal((px - 1, py - 1), canvas.Cell(new Point(origin.X + px * canvas.Zoom - 1e-6, origin.Y + py * canvas.Zoom - 1e-6)));
                    var back = canvas.ToPicture(middle);
                    Assert.InRange(Math.Abs(back.X - (px + 0.5)), 0, 1e-9 * Math.Max(1, Math.Abs(px)) + 1e-6);
                }
            }
            finally { window.Close(); }

            return Task.CompletedTask;
        });
    }

    [Theory]
    [InlineData(1.0 / 4, 30, 10)]
    [InlineData(3, -400, -100)]
    [InlineData(12.5, -2000, -900)]
    public async Task ThePencilPaintsTheExactPixelUnderThePointerAtAnyZoomAndPan(double zoom, double panX, double panY)
    {
        await HeadlessApp.Run(() =>
        {
            var (window, canvas) = Show(2048, 1024, 800, 500);
            try
            {
                var painted = new List<(int X, int Y)>();
                canvas.ZoomTo(zoom);
                canvas.PanBy(new Vector(panX, panY));
                canvas.Tool = ImageTool.Pencil;
                // Pixels that are in view at this zoom and pan, taken from the transform itself.
                var targets = new List<(int X, int Y)>();
                for (var screen = new Point(100, 80); targets.Count < 3; screen += new Vector(223, 91))
                {
                    var cell = canvas.Cell(screen);
                    if (canvas.Picture.Contains(cell.X, cell.Y)) targets.Add(cell);
                    if (screen.X > 780) screen = new Point(40, screen.Y + 120);
                    if (screen.Y > 480) break;
                }

                Assert.NotEmpty(targets);
                foreach (var (x, y) in targets)
                {
                    var at = new Point(canvas.Origin.X + (x + 0.5) * canvas.Zoom, canvas.Origin.Y + (y + 0.5) * canvas.Zoom);
                    window.MouseDown(at, MouseButton.Left);
                    window.MouseUp(at, MouseButton.Left);
                    Assert.Equal(new Rgba(0, 0, 0, 255), canvas.Picture.Get(x, y));
                    painted.Add((x, y));
                }

                // Nothing else was touched.
                var count = 0;
                for (var y = 0; y < canvas.Picture.Height; y++)
                    for (var x = 0; x < canvas.Picture.Width; x++)
                        if (canvas.Picture.Get(x, y).A != 0) count++;
                Assert.Equal(painted.Distinct().Count(), count);
            }
            finally { window.Close(); }

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task TheHandToolTheMiddleButtonAndSpaceMoveTheViewAndNeverThePicture()
    {
        await HeadlessApp.Run(() =>
        {
            var (window, canvas) = Show(256, 128, 800, 500);
            try
            {
                var edits = 0;
                canvas.Edited += _ => edits++;
                canvas.Focus();
                var start = new Point(200, 200);

                canvas.Tool = ImageTool.Hand;
                var before = canvas.Origin;
                window.MouseDown(start, MouseButton.Left);
                window.MouseMove(start + new Vector(30, 12));
                window.MouseMove(start + new Vector(50, -20));
                window.MouseUp(start + new Vector(50, -20), MouseButton.Left);
                Assert.Equal(before + new Vector(50, -20), canvas.Origin);

                canvas.Tool = ImageTool.Pencil;
                before = canvas.Origin;
                window.MouseDown(start, MouseButton.Middle);
                window.MouseMove(start + new Vector(-70, 25));
                window.MouseUp(start + new Vector(-70, 25), MouseButton.Middle);
                Assert.Equal(before + new Vector(-70, 25), canvas.Origin);

                before = canvas.Origin;
                window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
                window.MouseDown(start, MouseButton.Left);
                window.MouseMove(start + new Vector(15, 15));
                window.MouseUp(start + new Vector(15, 15), MouseButton.Left);
                window.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
                Assert.Equal(before + new Vector(15, 15), canvas.Origin);

                Assert.Equal(0, edits);
                Assert.All(canvas.Picture.Data, b => Assert.Equal(0, b));

                // Space is released: a plain drag draws again.
                var (cx, cy) = canvas.Cell(start);
                window.MouseDown(start, MouseButton.Left);
                window.MouseUp(start, MouseButton.Left);
                Assert.Equal(1, edits);
                Assert.Equal(255, canvas.Picture.Get(cx, cy).A);
            }
            finally { window.Close(); }

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task CtrlWheelZoomsAtThePointerAndTheWheelScrolls()
    {
        await HeadlessApp.Run(() =>
        {
            var (window, canvas) = Show(2048, 1024, 800, 500);
            try
            {
                var pointer = new Point(300, 120);
                var under = canvas.ToPicture(pointer);
                var zoom = canvas.Zoom;
                window.MouseWheel(pointer, new Vector(0, 1), RawInputModifiers.Control);
                Assert.True(canvas.Zoom > zoom);
                var now = canvas.ToPicture(pointer);
                Assert.InRange(Math.Abs(now.X - under.X), 0, 1e-6);
                Assert.InRange(Math.Abs(now.Y - under.Y), 0, 1e-6);
                window.MouseWheel(pointer, new Vector(0, -1), RawInputModifiers.Control);
                window.MouseWheel(pointer, new Vector(0, -1), RawInputModifiers.Control);
                Assert.True(canvas.Zoom < zoom);

                var origin = canvas.Origin;
                window.MouseWheel(pointer, new Vector(0, 1), RawInputModifiers.None);
                Assert.Equal(origin.Y + 48, canvas.Origin.Y);
                Assert.Equal(origin.X, canvas.Origin.X);
                window.MouseWheel(pointer, new Vector(0, 1), RawInputModifiers.Shift);
                Assert.Equal(origin.X + 48, canvas.Origin.X);
            }
            finally { window.Close(); }

            return Task.CompletedTask;
        });
    }

    // The colour of one pixel of what the canvas draws.
    private static (int R, int G, int B) PixelOf(RenderTargetBitmap rendered, int x, int y)
    {
        using var stream = new MemoryStream();
        rendered.Save(stream, new PngBitmapEncoderOptions());
        using var image = new ImageMagick.MagickImage(stream.ToArray());
        using var pixels = image.GetPixels();
        var p = pixels.GetPixel(x, y).ToArray();
        int Byte(int channel) => (int)Math.Round(p[channel] * 255.0 / ImageMagick.Quantum.Max);
        return (Byte(0), Byte(1), Byte(2));
    }

    [Theory]
    [InlineData(2.5)]
    [InlineData(1.5)]
    [InlineData(0.3)]
    public async Task FractionalZoomsDrawTheEdgeOfThePictureAndItsPixelsInPlace(double zoom)
    {
        await HeadlessApp.Run(() =>
        {
            var (window, canvas) = Show(40, 30, 300, 200);
            try
            {
                var blue = new Rgba(0, 0, 255, 255);
                for (var y = 0; y < 30; y++)
                    for (var x = 0; x < 40; x++) canvas.Picture.Set(x, y, blue);
                canvas.Refresh();
                canvas.ShowGrid = false;
                canvas.CanResize = false; // the resize handles sit on the edge and would hide the line under test
                canvas.ZoomTo(zoom, new Point(0, 0));
                canvas.PanBy(new Vector(40, 30) - (Vector)canvas.Origin);
                using var rendered = new RenderTargetBitmap(new PixelSize(300, 200));
                rendered.Render(canvas);
                var inside = PixelOf(rendered, (int)(40 + 5 * zoom), (int)(30 + 5 * zoom));
                Assert.True(inside.B > 200 && inside.R < 40, $"inside is {inside}");
                var outside = PixelOf(rendered, 10, 10);
                Assert.True(outside.B < 100, $"outside is {outside}");
                // The border is a light line just around the picture.
                var edge = PixelOf(rendered, 39, (int)(30 + 3 * zoom));
                Assert.True(edge.R > 60 && edge.G > 60, $"edge is {edge}");
            }
            finally { window.Close(); }

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task A4096PictureFitsZoomsAndRendersWithinBudgetAtEveryZoom()
    {
        await HeadlessApp.Run(() =>
        {
            var (window, canvas) = Show(4096, 4096, 900, 700);
            try
            {
                canvas.TilePreview = true;
                using var rendered = new RenderTargetBitmap(new PixelSize(900, 700));
                var watch = System.Diagnostics.Stopwatch.StartNew();
                foreach (var zoom in new[] { 1.0 / 256, 0.2, 1, 5, 64, 256 })
                {
                    canvas.ZoomTo(zoom, new Point(450, 350));
                    for (var i = 0; i < 3; i++) rendered.Render(canvas);
                }

                Assert.True(watch.ElapsedMilliseconds < 10000, $"{watch.ElapsedMilliseconds} ms");
            }
            finally { window.Close(); }

            return Task.CompletedTask;
        });
    }
}
