using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using G3MLib.DataFile;
using G3MStudio.Core.Workspace;
using G3MStudio.Editor;
using G3MStudio.Localization;

namespace G3MStudio.Tests;

/// <summary>
/// The canvas size changes by dragging the handles on the picture's edge: whole pixels, Shift keeps the aspect, Escape cancels, the far side stays put, the
/// status line shows the texture page line while dragging, and the release is one undoable edit (every frame of a sprite, or the picture of a page item).
/// </summary>
public sealed class ImageCanvasHandleTests
{
    private static readonly Rgba Red = new(255, 0, 0, 255);

    [Fact]
    public void TheDraggedSizeSnapsToWholePixelsKeepsTheAspectWithShiftAndStaysInRange()
    {
        Assert.Equal((26, 8), ImageCanvas.DragSize((16, 8), (1, 0), new Vector(9.6, 3), false));
        Assert.Equal((26, 8), ImageCanvas.DragSize((16, 8), (-1, 0), new Vector(-9.6, 3), false)); // the left edge grows to the left
        Assert.Equal((16, 4), ImageCanvas.DragSize((16, 8), (0, 1), new Vector(50, -4), false));
        Assert.Equal((32, 16), ImageCanvas.DragSize((16, 8), (1, 1), new Vector(16, 2), true)); // the side that changed most leads
        Assert.Equal((32, 16), ImageCanvas.DragSize((16, 8), (0, 1), new Vector(0, 8), true)); // an edge takes the other side along
        Assert.Equal((1, 8), ImageCanvas.DragSize((16, 8), (1, 0), new Vector(-100, 0), false));
        Assert.Equal((ResizeSpec.MaxSide, 8), ImageCanvas.DragSize((16, 8), (1, 0), new Vector(1e6, 0), false));
        Assert.Equal((ResizeSpec.MaxSide, ResizeSpec.MaxSide / 2), ImageCanvas.DragSize((16, 8), (0, 1), new Vector(0, 1e6), true)); // the aspect holds at the largest size
    }

    // A point of the canvas in window coordinates: the middle of the given side of the picture, just outside its edge.
    private static Point Edge(ImageCanvas canvas, Window window, int sx, int sy)
    {
        var (w, h) = (canvas.Picture.Width * canvas.Zoom, canvas.Picture.Height * canvas.Zoom);
        var at = new Point(canvas.Origin.X + (sx < 0 ? -4 : sx > 0 ? w + 4 : w / 2), canvas.Origin.Y + (sy < 0 ? -4 : sy > 0 ? h + 4 : h / 2));
        return canvas.TranslatePoint(at, window)!.Value;
    }

    private static void Drag(Window window, Point from, Vector by, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.MouseMove(from);
        window.MouseDown(from, MouseButton.Left, modifiers);
        window.MouseMove(from + by, modifiers | RawInputModifiers.LeftMouseButton);
        window.MouseUp(from + by, MouseButton.Left, modifiers);
        Dispatcher.UIThread.RunJobs();
    }

    [Fact]
    public async Task DraggingAnEdgeResizesTheCanvasAsOneUndoStepAndEscapeCancels()
    {
        await HeadlessApp.Run(async () =>
        {
            var picture = new PixelBuffer(16, 8);
            picture.Set(0, 0, Red);
            var session = new LocalImageSession(picture);
            using var view = new ImageEditorView(session);
            var window = new Window { Width = 1100, Height = 700, Content = view };
            window.Show();
            try
            {
                await LayoutAudit.Settle();
                var canvas = view.Canvas;
                Assert.True(canvas.CanResize);
                // The hint shown over a handle must not wrap the bottom bar and move the handle away from the pointer.
                var before = Edge(canvas, window, 1, 0);
                window.MouseMove(before);
                Dispatcher.UIThread.RunJobs();
                Assert.True(canvas.OverHandle);
                Assert.Equal(before, Edge(canvas, window, 1, 0));
                Drag(window, Edge(canvas, window, 1, 0), new Vector(3 * canvas.Zoom, 0));
                Assert.Equal((19, 8), (canvas.Picture.Width, canvas.Picture.Height));
                Assert.Equal(Red, canvas.Picture.Get(0, 0)); // the right edge moved, the pixels stayed
                view.Undo();
                Assert.Equal((16, 8), (canvas.Picture.Width, canvas.Picture.Height));

                Drag(window, Edge(canvas, window, -1, 0), new Vector(-2 * canvas.Zoom, 0));
                Assert.Equal((18, 8), (canvas.Picture.Width, canvas.Picture.Height));
                Assert.Equal(Red, canvas.Picture.Get(2, 0)); // the left edge grew, the right side stayed put

                // Escape during the drag: no edit.
                var from = Edge(canvas, window, 0, 1);
                window.MouseMove(from);
                window.MouseDown(from, MouseButton.Left);
                window.MouseMove(from + new Vector(0, 5 * canvas.Zoom), RawInputModifiers.LeftMouseButton);
                Assert.Equal(13, canvas.ResizeDrag?.Height);
                window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
                window.MouseUp(from + new Vector(0, 5 * canvas.Zoom), MouseButton.Left);
                Dispatcher.UIThread.RunJobs();
                Assert.Null(canvas.ResizeDrag);
                Assert.Equal((18, 8), (canvas.Picture.Width, canvas.Picture.Height));
            }
            finally
            {
                window.Close();
            }
        });
    }

    // A canvas alone in a window, zoomed so the handles are far apart; the drop handler says whether the size was applied.
    private static (ImageCanvas Canvas, Window Window) Lone(Func<ResizeSpec, bool> drop)
    {
        var canvas = new ImageCanvas(new ImageState(new PixelBuffer(16, 8)));
        canvas.ResizeDropped += drop;
        var window = new Window { Width = 800, Height = 600, Content = canvas };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        canvas.ZoomTo(8);
        return (canvas, window);
    }

    [Fact]
    public async Task CtrlPressedOrReleasedWithoutMovingChangesTheDragAndTheReleaseModifiersCount()
    {
        await HeadlessApp.Run(() =>
        {
            ResizeSpec? dropped = null;
            var (canvas, window) = Lone(spec =>
            {
                dropped = spec;
                return true;
            });
            try
            {
                static KeyEventArgs Press(Avalonia.Interactivity.RoutedEvent route, Key key, KeyModifiers modifiers) => new() { RoutedEvent = route, Key = key, KeyModifiers = modifiers };
                var from = Edge(canvas, window, 1, 0);
                window.MouseMove(from);
                window.MouseDown(from, MouseButton.Left);
                window.MouseMove(from + new Vector(16, 0), RawInputModifiers.LeftMouseButton);
                Assert.False(canvas.ResizeDrag!.Scale);
                canvas.RaiseEvent(Press(InputElement.KeyDownEvent, Key.LeftCtrl, KeyModifiers.Control));
                Assert.True(canvas.ResizeDrag!.Scale); // no pointer move came
                canvas.RaiseEvent(Press(InputElement.KeyUpEvent, Key.LeftCtrl, KeyModifiers.None));
                Assert.False(canvas.ResizeDrag!.Scale);
                window.MouseUp(from + new Vector(16, 0), MouseButton.Left, RawInputModifiers.Control); // held at the release
                Dispatcher.UIThread.RunJobs();
                Assert.True(dropped!.Scale);
            }
            finally { window.Close(); }

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task OnlyTheHandlesGrabSoASelectionCanStartJustOutsideThePicture()
    {
        await HeadlessApp.Run(() =>
        {
            var (canvas, window) = Lone(_ => true);
            try
            {
                canvas.Tool = ImageTool.Select;
                // Just outside the left edge, a quarter of the way down: between the corner and middle handles.
                var from = canvas.TranslatePoint(canvas.Origin + new Vector(-4, 16), window)!.Value;
                window.MouseMove(from);
                window.MouseDown(from, MouseButton.Left);
                Assert.Null(canvas.ResizeDrag);
                Assert.False(canvas.OverHandle);
                window.MouseMove(from + new Vector(40, 30), RawInputModifiers.LeftMouseButton);
                window.MouseUp(from + new Vector(40, 30), MouseButton.Left);
                Dispatcher.UIThread.RunJobs();
                Assert.NotNull(canvas.State.Selection);
                Assert.Equal((16, 8), (canvas.Picture.Width, canvas.Picture.Height));
            }
            finally { window.Close(); }

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task APressOnAPixelInsideThePictureNeverGrabsAHandle()
    {
        await HeadlessApp.Run(() =>
        {
            var (canvas, window) = Lone(_ => true);
            try
            {
                var (w, h) = (canvas.Picture.Width * canvas.Zoom, canvas.Picture.Height * canvas.Zoom);
                Assert.Null(canvas.HandleAt(canvas.Origin + new Vector(w - 1, h / 2))); // the last column, next to the right handle
                Assert.Null(canvas.HandleAt(canvas.Origin + new Vector(1, 1))); // the first pixel, next to the corner handle
                Assert.Equal((1, 0), canvas.HandleAt(canvas.Origin + new Vector(w + 4, h / 2))); // just outside still grabs
            }
            finally { window.Close(); }

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task TheRightButtonDoesNotEndADragAndARefusedSizeLeavesThePictureWhereItWas()
    {
        await HeadlessApp.Run(() =>
        {
            var drops = 0;
            var (canvas, window) = Lone(_ =>
            {
                drops++;
                return false;
            });
            try
            {
                var origin = canvas.Origin;
                var from = Edge(canvas, window, -1, 0);
                window.MouseMove(from);
                window.MouseDown(from, MouseButton.Left);
                window.MouseMove(from - new Vector(16, 0), RawInputModifiers.LeftMouseButton);
                window.MouseDown(from - new Vector(16, 0), MouseButton.Right, RawInputModifiers.LeftMouseButton);
                window.MouseUp(from - new Vector(16, 0), MouseButton.Right, RawInputModifiers.LeftMouseButton);
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(0, drops);
                Assert.Equal(18, canvas.ResizeDrag?.Width);

                window.MouseUp(from - new Vector(16, 0), MouseButton.Left);
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(1, drops);
                Assert.Null(canvas.ResizeDrag);
                Assert.Equal(origin, canvas.Origin); // the size was refused, so the picture does not jump

                // Leaving the canvas clears the hover, so the status hint and the cursor do not stay on.
                window.MouseMove(Edge(canvas, window, 1, 0));
                Assert.True(canvas.OverHandle);
                window.MouseMove(new Point(-50, -50));
                Dispatcher.UIThread.RunJobs();
                Assert.False(canvas.OverHandle);
            }
            finally { window.Close(); }

            return Task.CompletedTask;
        });
    }

    private static async Task<Workspace> Open(TempDir dir)
    {
        var path = dir.File("data.win");
        using (var data = CopyResourcesTests.Source()) TestData.Write(path, data);
        var userData = new UserData(dir.File("user"));
        return await new WorkspaceService(userData, RecentFiles.Load(userData)).OpenAsync(path, cancellationToken: TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task DraggingASpriteCanvasShowsTheTexturePageLineAndResizesEveryFrameAsOneEdit()
    {
        using var dir = new TempDir();
        using var workspace = await Open(dir);
        await HeadlessApp.Run(async () =>
        {
            using var view = new ImageEditorView(new WorkspaceImageSession(workspace, "spr_a"));
            var window = new Window { Width = 1100, Height = 700, Content = view };
            window.Show();
            try
            {
                await LayoutAudit.Settle();
                var canvas = view.Canvas;
                canvas.ZoomTo(2); // the drag stays inside the window
                var from = Edge(canvas, window, 1, 0);
                var by = new Vector(Math.Ceiling(68 * canvas.Zoom), 0);
                window.MouseMove(from);
                window.MouseDown(from, MouseButton.Left);
                window.MouseMove(from + by, RawInputModifiers.LeftMouseButton);
                Dispatcher.UIThread.RunJobs();
                var spec = canvas.ResizeDrag!;
                Assert.Equal((100, 32), (spec.Width, spec.Height));
                IReadOnlyList<FitStep> steps;
                using (workspace.Lock.Read()) steps = TextureFit.Plan(workspace.Data, [new FitRequest(workspace.Data.Sprites.First(s => s.Name.Content == "spr_a").Textures[0].Texture!, 100, 32)]);
                Assert.Equal($"   {Loc.Current["pages.fitLabel"]}: {FitText.Describe(steps).Replace(Environment.NewLine, " ", StringComparison.Ordinal)}", view.DragFitText);
                window.MouseUp(from + by, MouseButton.Left);
                Dispatcher.UIThread.RunJobs();

                var sprite = workspace.Data.Sprites.First(s => s.Name.Content == "spr_a");
                Assert.Equal((100u, 32u), (sprite.Width, sprite.Height));
                Assert.Equal((100, 32), (canvas.Picture.Width, canvas.Picture.Height));
                workspace.Undo();
                Assert.Equal((32u, 32u), (sprite.Width, sprite.Height));
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task ADragOnAPictureOfAnExternalPageSaysWhyAndLeavesEverythingAsItWas()
    {
        using var dir = new TempDir();
        using var workspace = await Open(dir);
        var sprite = workspace.Data.Sprites.First(s => s.Name.Content == "spr_a");
        sprite.Textures[0].Texture!.TexturePage.TextureExternal = true;
        await HeadlessApp.Run(async () =>
        {
            using var view = new ImageEditorView(new WorkspaceImageSession(workspace, "spr_a"));
            var window = new Window { Width = 1100, Height = 700, Content = view };
            window.Show();
            try
            {
                await LayoutAudit.Settle();
                var canvas = view.Canvas;
                canvas.ZoomTo(2);
                var from = Edge(canvas, window, 1, 0);
                var by = new Vector(Math.Ceiling(68 * canvas.Zoom), 0);
                window.MouseMove(from);
                window.MouseDown(from, MouseButton.Left);
                window.MouseMove(from + by, RawInputModifiers.LeftMouseButton);
                Dispatcher.UIThread.RunJobs();
                Assert.Contains("stored outside the data file", view.DragFitText, StringComparison.Ordinal); // said before the release
                window.MouseUp(from + by, MouseButton.Left);
                Dispatcher.UIThread.RunJobs();

                Assert.Equal((32, 32), (canvas.Picture.Width, canvas.Picture.Height));
                Assert.False(workspace.History.CanUndo);
                Assert.Equal((32u, 32u), (sprite.Width, sprite.Height));
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void AnAppliedScaleBecomesTheNewReferenceForGlyphShifts()
    {
        var session = new TargetImageSession(new ItemTarget("fonts", "fnt_pic"), new PixelBuffer(16, 16));
        Assert.True(session.CommitResize(0, new ResizeSpec(true, 32, 32), false, null));
        Assert.Null(session.Shift); // a scale moves the glyphs in a way no shift describes
        session.MarkApplied();
        Assert.Equal((0, 0), session.Shift);
        Assert.True(session.CommitResize(0, new ResizeSpec(false, 35, 32, 2, 0), false, null)); // grown on the left
        Assert.Equal((3, 0), session.Shift);
    }

    [Fact]
    public async Task AFontSheetDraggedOnTheLeftMovesItsGlyphsWhenApplied()
    {
        using var data = CopyResourcesTests.Source();
        var page = data.EmbeddedTextures[0];
        var item = new G3MLib.DataFile.Models.GameMakerTexturePageItem { TexturePage = page, SourceX = 64, SourceY = 64, SourceWidth = 16, SourceHeight = 16, TargetWidth = 16, TargetHeight = 16, BoundingWidth = 16, BoundingHeight = 16 };
#pragma warning disable CA2000 // the data model owns what is added to it
        var font = new G3MLib.DataFile.Models.GameMakerFont { Name = data.Strings.MakeString("fnt_pic", out _), Texture = item };
        font.Glyphs.Add(new G3MLib.DataFile.Models.GameMakerFont.Glyph { Character = 65, SourceX = 2, SourceY = 2, SourceWidth = 4, SourceHeight = 4 });
#pragma warning restore CA2000
        data.Fonts.Add(font);
        data.TexturePageItems.Add(item);
        var target = new ItemTarget("fonts", "fnt_pic");
        await HeadlessApp.Run(async () =>
        {
            var session = new TargetImageSession(target, target.Read(data)) { Planner = (w, h, _) => target.Plan(data, w, h) };
            using var view = new ImageEditorView(session);
            var window = new Window { Width = 1100, Height = 700, Content = view };
            window.Show();
            try
            {
                await LayoutAudit.Settle();
                view.Canvas.ZoomTo(2); // a 16 pixel sheet fits at a zoom that puts its corner out of view
                Drag(window, Edge(view.Canvas, window, -1, -1), new Vector(-6 * view.Canvas.Zoom, -1 * view.Canvas.Zoom));
                Assert.Equal((22, 17), (view.Canvas.Picture.Width, view.Canvas.Picture.Height));
                Assert.Equal((6, 1), session.Shift);
                new EditHistory(data).Execute(target.Apply(data, session.Current, false, session.Shift));
                Assert.Equal((8, 3), (font.Glyphs[0].SourceX, font.Glyphs[0].SourceY));
                Assert.Equal((22, 17), (font.Texture.SourceWidth, font.Texture.SourceHeight));
            }
            finally
            {
                window.Close();
            }
        });
    }
    [Fact]
    public async Task ADragAppliesToEveryFrameOnlyWithTheToggleOrTheAllFramesKey()
    {
        await HeadlessApp.Run(() =>
        {
            ResizeChoices.Current = new ResizeChoices();
            var drops = new List<ResizeSpec>();
            var (canvas, window) = Lone(spec =>
            {
                drops.Add(spec);
                return true;
            });
            try
            {
                var from = Edge(canvas, window, 1, 0);
                Drag(window, from, new Vector(16, 0));
                canvas.AllFrames = true;
                Drag(window, Edge(canvas, window, 1, 0), new Vector(16, 0));
                canvas.AllFrames = false;
                Drag(window, Edge(canvas, window, 1, 0), new Vector(16, 0), RawInputModifiers.Alt);
                Assert.Equal([false, true, true], drops.Select(d => d.AllFrames));
            }
            finally
            {
                window.Close();
            }
        });

    }
}
