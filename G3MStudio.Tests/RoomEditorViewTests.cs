using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using G3MLib.DataFile.Models;
using G3MStudio.Core.Workspace;
using G3MStudio.Rooms;

namespace G3MStudio.Tests;

/// <summary>The room canvas without a data file: a hand-made scene of rectangles drives the handles, tile tools and the large-room timings.</summary>
public sealed class RoomEditorViewTests(ITestOutputHelper output)
{
    private static WriteableBitmap Page() => new(new PixelSize(2, 2), new Vector(96, 96), Avalonia.Platform.PixelFormat.Bgra8888, Avalonia.Platform.AlphaFormat.Premul);

    private static RoomScene SceneOf(Bitmap page, params (GameMakerRoom.GameObject Instance, Rect Dest)[] items) => new()
    {
        Width = 1000,
        Height = 1000,
        Items = [.. items.Select(i => new RoomItem(page, new Rect(0, 0, 2, 2), i.Dest, i.Instance))],
    };

    [Fact]
    public async Task DraggingTheCornerHandleReportsOneScaleAndShiftKeepsTheRatio()
    {
        await HeadlessApp.Run(() =>
        {
            using var page = Page();
            var instance = new GameMakerRoom.GameObject { X = 100, Y = 100 };
            var view = new RoomView { Scene = SceneOf(page, (instance, new Rect(100, 100, 20, 10))) };
            var window = new Window { Content = view, Width = 600, Height = 400 };
            window.Show();
            view.Select([instance]);
            IReadOnlyList<(GameMakerRoom.GameObject Instance, Placement To)>? seen = null;
            view.Transformed += changes => seen = changes;
            Assert.Equal(new RoomBox(100, 100, 120, 110), view.SelectionBox());

            var corner = new Point(120, 110);
            window.MouseDown(corner, MouseButton.Left);
            window.MouseMove(new Point(140, 115), RawInputModifiers.Shift);
            window.MouseUp(new Point(140, 115), MouseButton.Left, RawInputModifiers.Shift);
            var to = Assert.Single(seen!).To;
            Assert.Equal(2, to.ScaleX, 6);
            Assert.Equal(to.ScaleX, to.ScaleY, 6);
            Assert.Equal((100, 100), (to.X, to.Y));
        });
    }

    [Fact]
    public async Task DraggingTheRotateHandleTurnsTheSelectionInFifteenDegreeSteps()
    {
        await HeadlessApp.Run(() =>
        {
            using var page = Page();
            var instance = new GameMakerRoom.GameObject { X = 100, Y = 100 };
            var view = new RoomView { Scene = SceneOf(page, (instance, new Rect(100, 100, 20, 20))) };
            var window = new Window { Content = view, Width = 600, Height = 400 };
            window.Show();
            view.Select([instance]);
            IReadOnlyList<(GameMakerRoom.GameObject Instance, Placement To)>? seen = null;
            view.Transformed += changes => seen = changes;

            var handle = new Point(110, 100 - 24);
            window.MouseDown(handle, MouseButton.Left);
            window.MouseMove(new Point(140, 110), RawInputModifiers.Shift);
            window.MouseUp(new Point(140, 110), MouseButton.Left, RawInputModifiers.Shift);
            Assert.Equal(270, Assert.Single(seen!).To.Rotation, 6);
        });
    }

    [Fact]
    public async Task TheLineToolPaintsTheCellsBetweenTwoCornersAndSelectMarksARectangle()
    {
        await HeadlessApp.Run(() =>
        {
            var view = new RoomView { Scene = new RoomScene { Width = 160, Height = 160, Items = [] } };
            var background = new GameMakerBackground { GMS2TileWidth = 16, GMS2TileHeight = 16 };
            var layer = new GameMakerRoom.Layer { LayerId = 1, Data = new GameMakerRoom.Layer.LayerTilesData { Background = background, TilesX = 10, TilesY = 10, TileData = [.. Enumerable.Range(0, 10).Select(_ => new uint[10])] } };
            view.TileLayer = layer;
            var window = new Window { Content = view, Width = 400, Height = 400 };
            window.Show();
            List<(int X, int Y)>? cells = null;
            view.TilesPainted += (_, _, c) => cells = [.. c];

            view.Tool = RoomTool.Line;
            window.MouseDown(new Point(8, 8), MouseButton.Left);
            window.MouseMove(new Point(56, 24), RawInputModifiers.LeftMouseButton);
            window.MouseUp(new Point(56, 24), MouseButton.Left);
            Assert.Equal(TileBlocks.Line((0, 0), (3, 1)), cells);

            view.Tool = RoomTool.Select;
            window.MouseDown(new Point(8, 8), MouseButton.Left);
            window.MouseMove(new Point(40, 40), RawInputModifiers.LeftMouseButton);
            window.MouseUp(new Point(40, 40), MouseButton.Left);
            Assert.Equal(9, cells!.Count);
        });
    }

    [Fact]
    public async Task AFloatingBlockIsDroppedOnTheCellUnderTheClick()
    {
        await HeadlessApp.Run(() =>
        {
            var view = new RoomView { Scene = new RoomScene { Width = 160, Height = 160, Items = [] } };
            var background = new GameMakerBackground { GMS2TileWidth = 16, GMS2TileHeight = 16 };
            view.TileLayer = new GameMakerRoom.Layer { Data = new GameMakerRoom.Layer.LayerTilesData { Background = background, TilesX = 10, TilesY = 10, TileData = [.. Enumerable.Range(0, 10).Select(_ => new uint[10])] } };
            var window = new Window { Content = view, Width = 400, Height = 400 };
            window.Show();
            (TileBlock Block, int X, int Y)? dropped = null;
            view.FloatingDropped += (b, x, y) => dropped = (b, x, y);
            view.StartFloating(new TileBlock(1, 1, [5]));
            window.MouseDown(new Point(50, 70), MouseButton.Left);
            Assert.Equal(5u, dropped!.Value.Block.Cells[0]);
            Assert.Equal((3, 4), (dropped.Value.X, dropped.Value.Y));
            Assert.Null(view.Floating);
        });
    }

    [Fact]
    public async Task ZoomAtKeepsThePointUnderTheCursor()
    {
        await HeadlessApp.Run(() =>
        {
            var view = new RoomView { Zoom = 1, Offset = new Point(100, 50) };
            var before = view.ToRoom(new Point(200, 120));
            view.ZoomAt(new Point(200, 120), 1.25);
            Assert.Equal(before.X, view.ToRoom(new Point(200, 120)).X, 6);
            Assert.Equal(before.Y, view.ToRoom(new Point(200, 120)).Y, 6);
            Assert.Equal(1.25, view.Zoom, 6);
        });
    }

    [Fact]
    public async Task ABigRoomKeepsSelectionBoxHitTestAndAlignInsideTheBudget()
    {
        await HeadlessApp.Run(() =>
        {
            using var page = Page();
            var instances = Enumerable.Range(0, 200_000).Select(i => new GameMakerRoom.GameObject { X = i % 2000 * 16, Y = i / 2000 * 16, InstanceID = (uint)i }).ToList();
            var scene = new RoomScene
            {
                Width = 32000,
                Height = 3200,
                Items = [.. instances.Select(i => new RoomItem(page, new Rect(0, 0, 2, 2), new Rect(i.X, i.Y, 16, 16), i))],
            };
            var view = new RoomView { Scene = scene };
            var watch = Stopwatch.StartNew();
            _ = scene.BoundsOf(instances[0]);
            var index = watch.Elapsed.TotalMilliseconds;
            view.Select(instances.Take(10_000));
            watch.Restart();
            var box = view.SelectionBox();
            var selectionBox = watch.Elapsed.TotalMilliseconds;
            watch.Restart();
            _ = view.HitTest(new Point(100, 100));
            var hit = watch.Elapsed.TotalMilliseconds;
            watch.Restart();
            var moves = RoomAlign.Align([.. instances.Take(10_000).Select(i => scene.BoundsOf(i)!.Value).Select(r => new RoomBox(r.Left, r.Top, r.Right, r.Bottom))], AlignMode.Left);
            var align = watch.Elapsed.TotalMilliseconds;
            output.WriteLine($"200,000 items: index {index:F1} ms, selection box of 10,000 {selectionBox:F1} ms, hit test {hit:F1} ms, align 10,000 {align:F1} ms");
            Assert.NotNull(box);
            Assert.Equal(10_000, moves.Length);
            Budget.AssertUnder(selectionBox, 50, "selection box", output);
            Budget.AssertUnder(hit, 50, "hit test", output);
            Budget.AssertUnder(align, 50, "align", output);
            Budget.AssertUnder(index, 300, "scene bounds index", output);
        });
    }
}
