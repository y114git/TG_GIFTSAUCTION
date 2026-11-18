using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.LogicalTree;
using G3MLib.DataFile;
using G3MLib.DataFile.Models;
using G3MStudio.Core.Workspace;
using G3MStudio.Editor;
using G3MStudio.Rooms;

namespace G3MStudio.Tests;

/// <summary>Drawing and gesture faults of the room canvas found by reading: turned bounds, mirrored items, lost capture, arrow steps, focus, zoom, tiny scales.</summary>
public sealed class RoomViewFixTests
{
#pragma warning disable CA2000 // model objects owned by the data model of the test
    private static readonly (byte, byte, byte) Green = (0, 255, 0), Blue = (0, 0, 255);

    // The room of RoomRenderTests with the instance's 16 pixel sprite drawn green on the left half and blue on the right half.
    private static (GameMakerRoom Room, GameMakerRoom.GameObject Instance) HalvesRoom()
    {
        var (room, _, instance) = RoomRenderTests.Room();
        var data = TestData.Create();
        using var canvas = new ImageMagick.MagickImage(ImageMagick.MagickColors.Lime, 16, 16);
        canvas.Draw(new ImageMagick.Drawing.DrawableFillColor(ImageMagick.MagickColors.Blue), new ImageMagick.Drawing.DrawableRectangle(8, 0, 15, 15));
        var page = new GameMakerEmbeddedTexture
        {
            TextureData = new GameMakerEmbeddedTexture.TexData { Image = G3MLib.DataFile.Util.GMImage.FromMagickImage(canvas).ConvertToPng() },
            TextureWidth = 16,
            TextureHeight = 16,
            Name = new GameMakerString("halves"),
        };
        data.EmbeddedTextures.Add(page);
        var item = new GameMakerTexturePageItem { SourceWidth = 16, SourceHeight = 16, TargetWidth = 16, TargetHeight = 16, BoundingWidth = 16, BoundingHeight = 16, TexturePage = page };
        instance.ObjectDefinition.Sprite = RoomRenderTests.Sprite(item);
        return (room, instance);
    }

    [Fact]
    public async Task ATurnedInstanceHasTheBoundsOfItsTurnedPicture()
    {
        await HeadlessApp.Run(() =>
        {
            var (room, instance) = HalvesRoom();
            instance.Rotation = 90; // turned about 24, 24: the square covers x 24 to 40 and y 8 to 24
            using var scene = RoomScene.Build(room);
            var bounds = scene.BoundsOf(instance)!.Value;
            Assert.Equal((24d, 8d, 16d, 16d), (Math.Round(bounds.X, 6), Math.Round(bounds.Y, 6), Math.Round(bounds.Width, 6), Math.Round(bounds.Height, 6)));
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task AMirroredInstanceIsDrawnMirroredHitAndKeepsUprightBounds()
    {
        await HeadlessApp.Run(() =>
        {
            var (room, instance) = HalvesRoom();
            instance.X = 40; // a scale of -1 draws the 16 pixels to the left of the position
            instance.ScaleX = -1;
            using var scene = RoomScene.Build(room);
            var item = scene.Items.Single(i => ReferenceEquals(i.Element, instance));
            Assert.Equal(new Rect(24, 24, 16, 16), item.Dest);
            Assert.Equal(1, item.Flags);
            Assert.Equal(new Rect(24, 24, 16, 16), scene.BoundsOf(instance));
            var view = new RoomView { Scene = scene };
            Assert.Same(instance, view.HitTest(new Point(30, 30)));
            var picture = RoomRenderTests.Draw(view);
            Assert.Equal(Blue, picture.At(26, 30)); // the blue right half of the picture now faces left
            Assert.Equal(Green, picture.At(36, 30));
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task LosingTheCaptureMidMoveDropsTheMoveAndTheNextMarqueeWorks()
    {
        await HeadlessApp.Run(() =>
        {
            var (room, instance) = HalvesRoom();
            using var scene = RoomScene.Build(room);
            var view = new RoomView { Scene = scene, IgnoreLocks = true };
            var window = new Window { Content = view, Width = 64, Height = 64 };
            window.Show();
            var moves = new List<(int, int)>();
            view.Moved += (_, x, y) => moves.Add((x, y));

            window.MouseDown(new Point(30, 30), MouseButton.Left);
            window.MouseMove(new Point(50, 50));
            view.RaiseEvent(new PointerCaptureLostEventArgs(view, new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true)));
            Assert.False(view.IgnoreLocks);
            window.MouseUp(new Point(50, 50), MouseButton.Left);
            Assert.Empty(moves);

            view.Select([]);
            window.MouseDown(new Point(2, 2), MouseButton.Left);
            window.MouseMove(new Point(60, 60));
            window.MouseUp(new Point(60, 60), MouseButton.Left);
            Assert.Same(instance, Assert.Single(view.Selection));
        });
    }

    [Fact]
    public async Task ArrowKeysMoveAtLeastOnePixelOnAFractionalGrid()
    {
        await HeadlessApp.Run(() =>
        {
            var (room, instance) = HalvesRoom();
            using var scene = RoomScene.Build(room);
            var view = new RoomView { Scene = scene, SnapToGrid = true, GridWidth = 0.5, GridHeight = 0.5 };
            var window = new Window { Content = view, Width = 64, Height = 64 };
            window.Show();
            view.Select([instance]);
            view.Focus();
            var moves = new List<(int, int)>();
            view.Moved += (_, x, y) => moves.Add((x, y));
            window.KeyPress(Key.Right, RawInputModifiers.None, PhysicalKey.ArrowRight, null);
            Assert.Equal((1, 0), Assert.Single(moves));
        });
    }

    [Fact]
    public async Task FocusSelectionCentresAndZoomsToTheSelectedElementAndZoomCentreKeepsTheMiddle()
    {
        await HeadlessApp.Run(() =>
        {
            var (room, instance) = HalvesRoom();
            using var scene = RoomScene.Build(room);
            var view = new RoomView { Scene = scene };
            var window = new Window { Content = view, Width = 640, Height = 480 };
            window.Show();
            window.UpdateLayout();
            view.FocusSelection(); // nothing selected: unchanged
            Assert.Equal(1, view.Zoom);
            view.Select([instance]);
            view.FocusSelection();
            Assert.Equal(8, view.Zoom);
            var middle = view.ToRoom(new Point(view.Bounds.Width / 2, view.Bounds.Height / 2));
            Assert.Equal((32d, 32d), (Math.Round(middle.X), Math.Round(middle.Y)));
            view.ZoomCentre(0.5);
            var after = view.ToRoom(new Point(view.Bounds.Width / 2, view.Bounds.Height / 2));
            Assert.Equal((Math.Round(middle.X), Math.Round(middle.Y)), (Math.Round(after.X), Math.Round(after.Y)));
            return Task.CompletedTask;
        });
    }

    [Fact]
    public void RepeatedShrinkingStopsAtOnePercentAndKeepsTheMirror()
    {
        var p = new Placement(10, 10, 1, -1, 0);
        for (var i = 0; i < 10; i++) p = RoomTransform.Scale(p, 0, 0, 0.01, 0.01);
        Assert.Equal((0.01, -0.01), (p.ScaleX, p.ScaleY));
    }

    [Fact]
    public async Task AFrameBeyondTheTileCountIsRefused()
    {
        using var data = TestData.Create();
        var set = new GameMakerBackground { Name = data.Strings.MakeString("ts", out _), GMS2TileWidth = 16, GMS2TileHeight = 16, GMS2TileCount = 4, GMS2ItemsPerTileCount = 1 };
        for (var i = 0; i < 4; i++) set.GMS2TileIds.Add(new GameMakerBackground.TileID { ID = (uint)i });
        data.Backgrounds.Add(set);
        Assert.Throws<InvalidOperationException>(() => new TileAnimationCommand("ts", TileAnimationCommand.Frames, 0, [1, 999]).Apply(data));
        new TileAnimationCommand("ts", TileAnimationCommand.Frames, 0, [1, 3]).Apply(data);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task AnEmptyTileSetPaintsNothingAndTheObjectListFollowsNewObjects()
    {
        using var dir = new TempDir();
        var userData = new UserData(dir.File("user"));
        using var workspace = await new WorkspaceService(userData, RecentFiles.Load(userData)).OpenAsync(TestData.WriteSample(dir, objects: 1), cancellationToken: TestContext.Current.CancellationToken);
        await HeadlessApp.Run(async () =>
        {
            using var textures = new TextureBitmaps();
            var palette = new TilePalette(new GameMakerBackground { GMS2TileCount = 0, GMS2TileWidth = 16, GMS2TileHeight = 16 }, textures);
            Assert.Equal(0, palette.SelectedIndex);

            var inspector = new InstanceInspector(workspace, "none");
            var box = inspector.GetLogicalDescendants().OfType<ComboBox>().Single();
            Assert.Single((IEnumerable<string>)box.ItemsSource!);
            workspace.Data.GameObjects.Add(new GameMakerGameObject { Name = workspace.Data.Strings.MakeString("obj_new", out _) });
            inspector.RefreshObjects();
            Assert.Equal(2, ((IEnumerable<string>)box.ItemsSource!).Count());
            await Task.CompletedTask;
        });
    }
#pragma warning restore CA2000
}
