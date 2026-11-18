using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using G3MLib.DataFile;
using G3MLib.DataFile.Models;
using G3MStudio.Editor;
using G3MStudio.Rooms;

namespace G3MStudio.Tests;

/// <summary>The room canvas draws what the game draws: background layers (colour, tiled sprite), instances, and the two ways to hide things.</summary>
public sealed class RoomRenderTests
{
#pragma warning disable CA2000 // model objects owned by the data model of the test
    private const int Side = 64;

    internal static GameMakerTexturePageItem Picture(GameMakerData data, ImageMagick.MagickColor color, int size)
    {
        using var canvas = new ImageMagick.MagickImage(color, (uint)size, (uint)size);
        var page = new GameMakerEmbeddedTexture
        {
            TextureData = new GameMakerEmbeddedTexture.TexData { Image = G3MLib.DataFile.Util.GMImage.FromMagickImage(canvas).ConvertToPng() },
            TextureWidth = size,
            TextureHeight = size,
            Name = new GameMakerString("page"),
        };
        data.EmbeddedTextures.Add(page);
        return new GameMakerTexturePageItem { SourceWidth = (ushort)size, SourceHeight = (ushort)size, TargetWidth = (ushort)size, TargetHeight = (ushort)size, BoundingWidth = (ushort)size, BoundingHeight = (ushort)size, TexturePage = page };
    }

    internal static GameMakerSprite Sprite(GameMakerTexturePageItem item)
    {
        var sprite = new GameMakerSprite { Width = item.SourceWidth, Height = item.SourceHeight };
        sprite.Textures.Add(new GameMakerSprite.TextureEntry { Texture = item });
        return sprite;
    }

    // A 64 x 64 room: a red 16 pixel sprite tiled both ways on a background layer (deepest), a blue 16 pixel instance at 24, 24 above it.
    internal static (GameMakerRoom Room, GameMakerRoom.Layer Background, GameMakerRoom.GameObject Instance) Room(bool tiled = true)
    {
        var data = TestData.Create(); // not disposed: the pictures of the sprites live in it
        var red = Sprite(Picture(data, ImageMagick.MagickColors.Red, 16));
        var blue = Sprite(Picture(data, ImageMagick.MagickColors.Blue, 16));
        var background = new GameMakerRoom.Layer
        {
            LayerName = new GameMakerString("Compatibility_Background"),
            LayerType = GameMakerRoom.LayerType.Background,
            LayerDepth = 100,
            IsVisible = true,
            Data = new GameMakerRoom.Layer.LayerBackgroundData { Visible = true, Sprite = red, TiledHorizontally = tiled, TiledVertically = tiled, Color = 0xFFFFFFFF },
        };
        var instance = new GameMakerRoom.GameObject { InstanceID = 100001, X = 24, Y = 24, ScaleX = 1, ScaleY = 1, ObjectDefinition = new GameMakerGameObject { Name = new GameMakerString("obj"), Sprite = blue } };
        var instances = new GameMakerRoom.Layer
        {
            LayerName = new GameMakerString("Instances"),
            LayerType = GameMakerRoom.LayerType.Instances,
            LayerDepth = 0,
            IsVisible = true,
            Data = new GameMakerRoom.Layer.LayerInstancesData { Instances = [instance] },
        };
        return (new GameMakerRoom { Width = Side, Height = Side, Layers = [instances, background] }, background, instance);
    }

    // The bitmap keeps the platform's byte order: Bgra on Windows and Linux, Rgba on macOS.
    internal static (byte R, byte G, byte B)[] Rgb(byte[] pixels, bool bgra) =>
        [.. Enumerable.Range(0, pixels.Length / 4).Select(i => bgra ? (pixels[i * 4 + 2], pixels[i * 4 + 1], pixels[i * 4]) : (pixels[i * 4], pixels[i * 4 + 1], pixels[i * 4 + 2]))];

    internal static (byte R, byte G, byte B)[] Draw(RoomView view)
    {
        var window = new Window { Content = view, Width = Side, Height = Side, SizeToContent = SizeToContent.Manual };
        window.Show();
        try
        {
            window.UpdateLayout();
            using var target = new RenderTargetBitmap(new PixelSize(Side, Side));
            target.Render(view);
            var pixels = new byte[Side * Side * 4];
            var pin = System.Runtime.InteropServices.GCHandle.Alloc(pixels, System.Runtime.InteropServices.GCHandleType.Pinned);
            try { target.CopyPixels(new PixelRect(0, 0, Side, Side), pin.AddrOfPinnedObject(), pixels.Length, Side * 4); }
            finally { pin.Free(); }
            return Rgb(pixels, target.Format == PixelFormat.Bgra8888);
        }
        finally
        {
            window.Close();
        }
    }

    [Fact]
    public void ThePixelReaderFollowsTheByteOrderOfTheBitmap()
    {
        Assert.Equal([((byte)255, (byte)0, (byte)0)], Rgb([0, 0, 255, 255], bgra: true));
        Assert.Equal([((byte)255, (byte)0, (byte)0)], Rgb([255, 0, 0, 255], bgra: false));
    }

    private static readonly (byte, byte, byte) Red = (255, 0, 0), Blue = (0, 0, 255), Black = (0, 0, 0);

    [Fact]
    public async Task ATiledBackgroundLayerCoversTheRoomAndTheInstanceDrawsAboveIt()
    {
        await HeadlessApp.Run(() =>
        {
            var (room, _, _) = Room();
            using var scene = RoomScene.Build(room);
            var picture = Draw(new RoomView { Scene = scene });
            Assert.Equal(Red, picture.At(2, 2));
            Assert.Equal(Red, picture.At(61, 61)); // the last tile of the repeat, both directions
            Assert.Equal(Red, picture.At(40, 10));
            Assert.Equal(Blue, picture.At(30, 30));
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task ATiledBackgroundIsOneItemWhateverTheRoomSizeAndAZeroSizedRoomDrawsNothing()
    {
        await HeadlessApp.Run(() =>
        {
            var (room, background, _) = Room();
            (room.Width, room.Height) = (4096, 4096); // 65,536 copies of the 16 pixel picture, more than a copy list could hold
            using var scene = RoomScene.Build(room);
            var tiled = Assert.Single(scene.Items, i => i.Element is null);
            Assert.Equal((256, 256), (tiled.Grid!.Value.Columns, tiled.Grid.Value.Rows));
            Assert.Equal((4096d, 4096d), (tiled.Extent.Right, tiled.Extent.Bottom));

            (room.Width, room.Height) = (0, 0);
            background.BackgroundData.Stretch = true;
            using var empty = RoomScene.Build(room);
            Assert.DoesNotContain(empty.Items, i => i.Element is null);
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task ARotatedInstanceIsHitAndCulledWhereItIsDrawn()
    {
        await HeadlessApp.Run(() =>
        {
            var (room, _, instance) = Room();
            instance.Rotation = 90; // counterclockwise about 24, 24: the square now covers x 24 to 40 and y 8 to 24
            using var scene = RoomScene.Build(room);
            var view = new RoomView { Scene = scene };
            Assert.Same(instance, view.HitTest(new Point(30, 16)));
            Assert.Null(view.HitTest(new Point(30, 30)));
            var item = scene.Items.Single(i => ReferenceEquals(i.Element, instance));
            Assert.Equal((24d, 8d, 16d, 16d), (item.Seen(item.Dest).X, item.Seen(item.Dest).Y, item.Seen(item.Dest).Width, item.Seen(item.Dest).Height), new Tolerance());
            return Task.CompletedTask;
        });
    }

    private sealed class Tolerance : IEqualityComparer<(double, double, double, double)>
    {
        public bool Equals((double, double, double, double) a, (double, double, double, double) b) => Math.Abs(a.Item1 - b.Item1) + Math.Abs(a.Item2 - b.Item2) + Math.Abs(a.Item3 - b.Item3) + Math.Abs(a.Item4 - b.Item4) < 1e-6;

        public int GetHashCode((double, double, double, double) value) => 0;
    }

    [Fact]
    public async Task AnUntiledBackgroundLayerDrawsOnceAtTheLayerOffset()
    {
        await HeadlessApp.Run(() =>
        {
            var (room, background, _) = Room(tiled: false);
            background.XOffset = 32;
            using var scene = RoomScene.Build(room);
            var picture = Draw(new RoomView { Scene = scene });
            Assert.Equal(Black, picture.At(2, 2));
            Assert.Equal(Red, picture.At(36, 4));
            Assert.Equal(Black, picture.At(52, 4)); // 32 + 16 is past the single copy
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task AColourBackgroundLayerFillsTheRoom()
    {
        await HeadlessApp.Run(() =>
        {
            var (room, background, _) = Room();
            background.BackgroundData.Sprite = null!;
            background.BackgroundData.Color = 0xFF00FF00; // 0xAABBGGRR: green
            using var scene = RoomScene.Build(room);
            Assert.Equal(0x00FF00u, scene.FillColor);
            Assert.Equal((0, 255, 0), Draw(new RoomView { Scene = scene }).At(2, 2));
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task TheLayerEyeHidesEverythingOnTheLayerAndTheInstanceBoxOnlyThatInstance()
    {
        await HeadlessApp.Run(() =>
        {
            var (room, background, instance) = Room();
            using var scene = RoomScene.Build(room);
            var view = new RoomView { Scene = scene };
            view.SetHidden([instance]);
            var hiddenInstance = Draw(view);
            Assert.Equal(Red, hiddenInstance.At(30, 30)); // the tile under it shows, the layer is untouched
            Assert.Equal(Red, hiddenInstance.At(2, 2));
            Assert.Null(view.HitTest(new Point(30, 30)));
            view.Select([instance]); // the row of an unticked element is not a selection of something that is not drawn
            Assert.Empty(view.Selection);
            view.SetHidden([]);
            Assert.Same(instance, view.HitTest(new Point(30, 30)));

            background.IsVisible = false; // the eye of the layer
            using var withoutBackground = RoomScene.Build(room);
            view = new RoomView { Scene = withoutBackground };
            var picture = Draw(view);
            Assert.Equal(Black, picture.At(2, 2));
            Assert.Equal(Blue, picture.At(30, 30)); // the instance box is still ticked
            view = new RoomView { Scene = withoutBackground };
            view.SetHidden([instance]);
            Assert.Equal(Black, Draw(view).At(30, 30)); // both off: nothing left
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task AnInstanceRowIsTickedByDefaultAndTheBoxIsNotTheSelection()
    {
        await HeadlessApp.Run(() =>
        {
            var (room, _, instance) = Room();
            var list = new RoomElementList(room);
            var window = new Window { Content = list, Width = 400, Height = 300 };
            window.Show();
            try
            {
                list.Show(null);
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                var box = list.GetVisualDescendants().OfType<CheckBox>().Single(c => c.Name == "ElementCheck");
                Assert.True(box.IsChecked);
                list.Mark([instance]); // selecting does not tick or clear it
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                Assert.True(list.GetVisualDescendants().OfType<CheckBox>().Single(c => c.Name == "ElementCheck").IsChecked);

                var changed = 0;
                list.HiddenChanged += () => changed++;
                list.GetVisualDescendants().OfType<CheckBox>().Single(c => c.Name == "ElementCheck").IsChecked = false;
                Assert.Same(instance, Assert.Single(list.Hidden));
                list.SetAllShown(true);
                Assert.Empty(list.Hidden);
                list.SetAllShown(false);
                Assert.Same(instance, Assert.Single(list.Hidden));
                Assert.Equal(3, changed);
            }
            finally
            {
                window.Close();
            }

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task APlainClickPicksOneRowAndTheModeHasNoToggleFlag()
    {
        await HeadlessApp.Run(() =>
        {
            var (room, _, _) = Room();
            var layer = room.Layers[0];
            for (var i = 0; i < 3; i++) layer.InstancesData.Instances.Add(new GameMakerRoom.GameObject { InstanceID = (uint)(200 + i), X = i * 20, ObjectDefinition = layer.InstancesData.Instances[0].ObjectDefinition });
            var list = new RoomElementList(room);
            var window = new Window { Content = list, Width = 400, Height = 300 };
            window.Show();
            try
            {
                list.Show(layer);
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                Point Row(int index)
                {
                    var item = list.GetVisualDescendants().OfType<ListBoxItem>().ElementAt(index);
                    return item.TranslatePoint(new Point(item.Bounds.Width - 6, item.Bounds.Height / 2), window)!.Value;
                }

                HeadlessWindowExtensions.MouseDown(window, Row(0), MouseButton.Left);
                HeadlessWindowExtensions.MouseUp(window, Row(0), MouseButton.Left);
                HeadlessWindowExtensions.MouseDown(window, Row(1), MouseButton.Left);
                HeadlessWindowExtensions.MouseUp(window, Row(1), MouseButton.Left);
                Assert.Single(list.SelectedItems!); // a plain click does not add to the first
                Assert.Equal(SelectionMode.Multiple, list.SelectionMode); // no Toggle flag: Ctrl+click adds or removes a row, Shift+click takes a range
            }
            finally
            {
                window.Close();
            }

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task APlainClickOnOneOfSeveralSelectedInstancesNarrowsTheCanvasSelection()
    {
        await HeadlessApp.Run(() =>
        {
            var (room, _, instance) = Room();
            var second = new GameMakerRoom.GameObject { InstanceID = 100002, X = 0, Y = 0, ScaleX = 1, ScaleY = 1, ObjectDefinition = instance.ObjectDefinition };
            room.Layers[0].InstancesData.Instances.Add(second);
            using var scene = RoomScene.Build(room);
            var view = new RoomView { Scene = scene };
            var window = new Window { Content = view, Width = Side, Height = Side };
            window.Show();
            try
            {
                window.UpdateLayout();
                view.Select([instance, second]);
                HeadlessWindowExtensions.MouseDown(window, new Point(30, 30), MouseButton.Left);
                HeadlessWindowExtensions.MouseUp(window, new Point(30, 30), MouseButton.Left);
                Assert.Same(instance, Assert.Single(view.Selection));
                HeadlessWindowExtensions.MouseDown(window, new Point(5, 5), MouseButton.Left, RawInputModifiers.Control);
                HeadlessWindowExtensions.MouseUp(window, new Point(5, 5), MouseButton.Left, RawInputModifiers.Control);
                Assert.Equal(2, view.Selection.Count);
            }
            finally
            {
                window.Close();
            }

            return Task.CompletedTask;
        });
    }
#pragma warning restore CA2000
}

internal static class RoomPixels
{
    public static (byte R, byte G, byte B) At(this (byte R, byte G, byte B)[] picture, int x, int y) => picture[y * 64 + x];
}
