using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using G3MStudio.Core.Workspace;
using G3MStudio.Editor;

namespace G3MStudio.Tests;

/// <summary>Closed shapes in the image editor: an outline leaves the inside alone, the preview is the final pixels, the colours and the style agree with the tool in hand.</summary>
public sealed class ImageShapeStyleTests
{
    private static readonly Rgba Red = new(255, 0, 0, 255), Blue = new(0, 0, 255, 255);

    private static PixelBuffer Draw(string kind, int box, int size, bool smooth, ShapeStyle? style = null)
    {
        var picture = new PixelBuffer(box + 8, box + 8);
        var (a, b) = (4, 3 + box);
        Rgba? outline = style == ShapeStyle.Filled ? null : Red;
        Rgba? fill = style is ShapeStyle.Filled or ShapeStyle.Both ? Blue : null;
        var (o, f, n) = (ImageOp.Format(outline), ImageOp.Format(fill), ImageOp.Format(size));
        var corners = ImageOp.Format([(a, a), (b, b)]);
        var ring = ImageOp.Format([(a, a), (b, a), (b, b), (a, b)]);
        ImageOp op = (kind, smooth) switch
        {
            (ImageOps.Polygon, true) => new(ImageOps.Smooth, ImageOps.Polygon, o, f, n, ring),
            (ImageOps.Polygon, false) => new(ImageOps.Polygon, o, f, n, "0", ring),
            (_, true) => new(ImageOps.Smooth, kind, o, f, n, corners),
            _ => new(kind, o, f, n, corners),
        };
        ImageOps.Run(new ImageState(picture), op);
        return picture;
    }

    [Theory]
    [InlineData(ImageOps.Rect, false)]
    [InlineData(ImageOps.Rect, true)]
    [InlineData(ImageOps.Ellipse, false)]
    [InlineData(ImageOps.Ellipse, true)]
    [InlineData(ImageOps.Polygon, false)]
    [InlineData(ImageOps.Polygon, true)]
    public void AnOutlineLeavesTheInsideUntouchedForEveryBrushSizeAndBox(string kind, bool smooth)
    {
        foreach (var box in new[] { 3, 4, 5, 6, 7, 9, 12, 20, 33 })
            for (var size = 1; size <= 8; size++)
            {
                var picture = Draw(kind, box, size, smooth);
                // The thickness is cut so that an inside stays; a box of 1 or 2 pixels has none and is solid.
                var thick = kind == ImageOps.Polygon ? size : (int)PixelBuffer.OutlineSize(size, box, box);
                var margin = thick + 1; // one more than the stroke: the soft edge of a smooth stroke and the stamp of a polygon corner
                if (box <= 2 * margin) continue;
                var (inner, c) = (new List<(int, int)>(), 4);
                for (var y = c + margin; y < c + box - margin; y++)
                    for (var x = c + margin; x < c + box - margin; x++)
                        if (kind != ImageOps.Ellipse || Math.Pow(x + 0.5 - (c + box / 2.0), 2) + Math.Pow(y + 0.5 - (c + box / 2.0), 2) < Math.Pow(box / 2.0 - margin - 1, 2)) inner.Add((x, y));
                Assert.All(inner, p => Assert.True(picture.Get(p.Item1, p.Item2).A == 0, $"{kind} smooth={smooth} box={box} size={size} at {p}"));
                if (kind != ImageOps.Polygon) Assert.True(picture.Get(4, 4 + box / 2).A > 0 || kind == ImageOps.Ellipse, $"{kind} {box} {size} has an edge");
            }
    }

    [Fact]
    public void ABoxTooSmallForAnInsideIsSolidAndOneThatCanHaveOneKeepsIt()
    {
        Assert.Equal(1, PixelBuffer.OutlineSize(5, 3, 3));
        Assert.Equal(1, PixelBuffer.OutlineSize(5, 2, 9));
        Assert.Equal(3, PixelBuffer.OutlineSize(5, 7, 40));
        Assert.Equal(2, PixelBuffer.OutlineSize(2, 40, 40));
        var small = Draw(ImageOps.Rect, 2, 4, false);
        Assert.All<(int, int)>([(4, 4), (5, 4), (4, 5), (5, 5)], p => Assert.Equal(Red, small.Get(p.Item1, p.Item2)));
        var seven = Draw(ImageOps.Rect, 7, 6, false);
        Assert.Equal(Rgba.Clear, seven.Get(7, 7)); // the single pixel in the middle stays clear
        Assert.Equal(Red, seven.Get(6, 7));
    }

    [Fact]
    public void FilledShapesReachTheBoxEdgeSmoothOrNot()
    {
        var plain = Draw(ImageOps.Rect, 6, 3, false, ShapeStyle.Filled);
        var soft = Draw(ImageOps.Rect, 6, 3, true, ShapeStyle.Filled);
        Assert.Equal(Blue, plain.Get(4, 4));
        Assert.Equal(Blue, soft.Get(4, 4));
        Assert.Equal(Blue, soft.Get(9, 9));
    }

    private static T Named<T>(Visual view, string name) where T : Control => view.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);

    [Theory]
    [InlineData(ShapeStyle.Outline, false, false)]
    [InlineData(ShapeStyle.Outline, true, false)]
    [InlineData(ShapeStyle.Filled, false, false)]
    [InlineData(ShapeStyle.Both, false, false)]
    [InlineData(ShapeStyle.Both, true, true)]
    [InlineData(ShapeStyle.Outline, true, true)]
    public async Task ThePreviewIsTheFinalPixelsAndTheStyleDecidesWhichColourIsWhere(ShapeStyle style, bool smooth, bool mirror)
    {
        await HeadlessApp.Run(() =>
        {
            foreach (var tool in new[] { ImageTool.Rectangle, ImageTool.Ellipse })
                for (var size = 1; size <= 5; size++)
                    foreach (var box in new[] { ((3, 3), (4, 12)), ((2, 2), (14, 6)), ((20, 20), (6, 9)), ((5, 5), (5, 5)) })
                    {
                        using var canvas = new ImageCanvas(new ImageState(new PixelBuffer(24, 24)))
                        {
                            Tool = tool,
                            Style = style,
                            BrushSize = size,
                            Smooth = smooth,
                            Primary = Red,
                            Secondary = Blue,
                            Symmetry = mirror ? SymmetryMode.Horizontal : SymmetryMode.None,
                        };
                        var preview = canvas.ShapePreview(box)!.Snapshot();
                        canvas.Run(canvas.ShapeOps(box));
                        Assert.Equal(preview, canvas.Picture.Snapshot()); // the picture was blank, so the overlay is the result
                        var colours = canvas.Picture.Data.Chunk(4).Select(p => new Rgba(p[0], p[1], p[2], p[3])).Where(c => c.A == 255).Distinct().ToList();
                        if (style == ShapeStyle.Outline) Assert.DoesNotContain(Blue, colours);
                        if (style == ShapeStyle.Filled) Assert.DoesNotContain(Red, colours);
                        if (style == ShapeStyle.Both && !smooth) Assert.Contains(Red, colours);
                    }

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task ThePreviewFollowsTheDragAndLeavesNoTraceOfTheLastBox()
    {
        await HeadlessApp.Run(() =>
        {
            using var canvas = new ImageCanvas(new ImageState(new PixelBuffer(30, 30))) { Tool = ImageTool.Rectangle, Style = ShapeStyle.Filled, Primary = Red, Secondary = Blue };
            Assert.NotNull(canvas.ShapePreview(((1, 1), (20, 20))));
            var next = canvas.ShapePreview(((5, 5), (8, 8)))!;
            Assert.Equal(Rgba.Clear, next.Get(1, 1));
            Assert.Equal(Rgba.Clear, next.Get(20, 20));
            Assert.Equal(Blue, next.Get(6, 6));
            Assert.Equal(16, Enumerable.Range(0, 900).Count(i => next.Get(i % 30, i / 30).A != 0));
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task ToolboxButtonsKeysAndTheStyleBoxAlwaysAgreeAndTheBarNamesTheColoursOfTheStyle()
    {
        await HeadlessApp.Run(() =>
        {
            using var view = new ImageEditorView(new LocalImageSession(new PixelBuffer(16, 16)));
            var window = new Window { Width = 1300, Height = 700, Content = view };
            window.Show();
            try
            {
                var combo = Named<ComboBox>(view, "ImageStyle");
                Assert.True(view.Run("img.tool.rectangleFilled"));
                Assert.Equal((ShapeStyle.Filled, 1), (view.Canvas.Style, combo.SelectedIndex));
                Named<ToggleButton>(view, "Tool_Rectangle").RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
                Assert.Equal((ShapeStyle.Outline, 0), (view.Canvas.Style, combo.SelectedIndex)); // the plain tool is an outline, not a stale fill
                Assert.True(view.Run("img.tool.ellipseFilled"));
                Named<ToggleButton>(view, "Tool_Polygon").RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
                Assert.Equal((ImageTool.Polygon, ShapeStyle.Outline, 0), (view.Tool, view.Canvas.Style, combo.SelectedIndex));

                var (outline, fill) = ((StackPanel)Named<Border>(view, "OutlineSwatch").GetVisualParent()!, (StackPanel)Named<Border>(view, "FillSwatch").GetVisualParent()!);
                Assert.True(outline.IsVisible);
                Assert.False(fill.IsVisible);
                combo.SelectedIndex = 1;
                Assert.False(outline.IsVisible);
                Assert.True(fill.IsVisible);
                combo.SelectedIndex = 2;
                Assert.True(outline.IsVisible && fill.IsVisible);
                view.SetColour(Red, false);
                view.SetColour(Blue, true);
                Assert.Equal(Red, ((ISolidColorBrush)Named<Border>(view, "OutlineSwatch").Background!).Color is var o ? new Rgba(o.R, o.G, o.B, o.A) : default);
                Assert.Equal(Blue, ((ISolidColorBrush)Named<Border>(view, "FillSwatch").Background!).Color is var f ? new Rgba(f.R, f.G, f.B, f.A) : default);
                Assert.Equal("Outline colour", Avalonia.Automation.AutomationProperties.GetName(Named<Border>(view, "PrimarySwatch")));
                view.SetTool(ImageTool.Pencil);
                Assert.Equal("Primary colour", Avalonia.Automation.AutomationProperties.GetName(Named<Border>(view, "PrimarySwatch")));
                Assert.False(combo.IsEffectivelyVisible); // no style box for a brush
            }
            finally
            {
                window.Close();
            }

            return Task.CompletedTask;
        });
    }

    // A point of the canvas in window coordinates, just outside the given side of the picture.
    private static Point Edge(ImageCanvas canvas, Window window, int sx, int sy)
    {
        var (w, h) = (canvas.Picture.Width * canvas.Zoom, canvas.Picture.Height * canvas.Zoom);
        var at = new Point(canvas.Origin.X + (sx < 0 ? -4 : sx > 0 ? w + 4 : w / 2), canvas.Origin.Y + (sy < 0 ? -4 : sy > 0 ? h + 4 : h / 2));
        return canvas.TranslatePoint(at, window)!.Value;
    }

    [Theory]
    [InlineData(1200, 700)]
    [InlineData(420, 500)]
    public async Task HoveringTheResizeHandlesNeverMovesAnythingOnTheScreen(int width, int height)
    {
        await HeadlessApp.Run(() =>
        {
            using var view = new ImageEditorView(new LocalImageSession(new PixelBuffer(16, 8)));
            var window = new Window { Width = width, Height = height, Content = view };
            window.Show();
            try
            {
                Dispatcher.UIThread.RunJobs();
                var canvas = view.Canvas;
                var line = Named<TextBlock>(view, "ImageStatus").GetVisualParent()!;
                (Rect, Point, double, double) State() => (canvas.Bounds, canvas.Origin, line.Bounds.Height, canvas.Zoom);
                var before = State();
                var away = canvas.TranslatePoint(new Point(canvas.Bounds.Width / 2, 6), window)!.Value;
                var hovered = 0;
                canvas.HandleHovered += over => hovered += over ? 1 : 0;
                for (var round = 0; round < 50; round++)
                    foreach (var (sx, sy) in new[] { (-1, -1), (0, -1), (1, -1), (-1, 0), (1, 0), (-1, 1), (0, 1), (1, 1) })
                    {
                        window.MouseMove(Edge(canvas, window, sx, sy));
                        Dispatcher.UIThread.RunJobs();
                        Assert.True(canvas.OverHandle);
                        Assert.Equal(before, State());
                        window.MouseMove(away);
                        Dispatcher.UIThread.RunJobs();
                        Assert.Equal(before, State());
                    }

                Assert.Equal(400, hovered);
            }
            finally
            {
                window.Close();
            }

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task TheIconsAreCentredAndTheUndoAndRedoArrowsMirrorEachOther()
    {
        await HeadlessApp.Run(() =>
        {
            Icons();
            return Task.CompletedTask;
        });
    }

    private static void Icons()
    {
        static Rect Box(string data) => Geometry.Parse(data).Bounds;
        var star = Box(ImageTools.WandStar);
        Assert.Equal(23, star.Center.X + star.Center.Y, 0.01); // the star sits on the axis of the wand body, which runs along x + y = 23
        var body = Box("M2 19l6-6 2 2-6 6z");
        Assert.Equal(23, body.Center.X + body.Center.Y, 0.01);
        var lasso = Box(ImageTools.Icon(ImageTool.Lasso));
        Assert.Equal(12, lasso.Center.X, 0.01);
        Assert.InRange(lasso.Center.Y, 11.5, 12.8);
        var (undo, redo) = (Box(ImageTools.UndoIcon), Box(ImageTools.RedoIcon));
        Assert.Equal(24 - undo.Right, redo.Left, 0.01);
        Assert.Equal(undo.Top, redo.Top, 0.01);
        Assert.Equal(undo.Height, redo.Height, 0.01);
        Assert.True(undo.Height >= 14 && undo.Width >= 14); // large enough to read at 16 pixels
    }
}
