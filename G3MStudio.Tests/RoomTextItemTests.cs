using Avalonia;
using G3MLib.DataFile.Models;
using G3MStudio.Rooms;

namespace G3MStudio.Tests;

/// <summary>Text items are drawn as their text and can be picked where the text is; a text item without text is a labelled box.</summary>
public sealed class RoomTextItemTests
{
#pragma warning disable CA2000 // model objects owned by the room of the test
    private static (GameMakerRoom Room, GameMakerRoom.TextItemInstance Item) TextRoom(string? text, uint color = 0xFFFFFFFF)
    {
        var item = new GameMakerRoom.TextItemInstance
        {
            Name = new GameMakerString("txt"),
            X = 8,
            Y = 8,
            ScaleX = 1,
            ScaleY = 1,
            Color = color,
            Text = text is null ? null! : new GameMakerString(text),
        };
        var layer = new GameMakerRoom.Layer
        {
            LayerName = new GameMakerString("Text"),
            LayerType = GameMakerRoom.LayerType.Assets,
            LayerDepth = 0,
            IsVisible = true,
            Data = new GameMakerRoom.Layer.LayerAssetsData { TextItems = [item] },
        };
        return (new GameMakerRoom { Width = 64, Height = 64, Layers = [layer] }, item);
    }

    [Fact]
    public async Task ATextItemIsDrawnWithItsColourAndPicked()
    {
        await HeadlessApp.Run(() =>
        {
            var (room, item) = TextRoom("MMMM", 0xFF0000FF); // 0xAABBGGRR: red text
            using var scene = RoomScene.Build(room);
            var marker = Assert.Single(scene.Markers);
            Assert.NotNull(marker.Text);
            Assert.Same(item, marker.Element);
            Assert.True(marker.Bounds.Width > 8 && marker.Bounds.Height > 4, marker.Bounds.ToString());
            Assert.Equal(8, marker.Bounds.X);
            var picture = RoomRenderTests.Draw(new RoomView { Scene = scene });
            Assert.Contains(picture, p => p.R > 128 && p.G < 64 && p.B < 64);
            Assert.DoesNotContain(picture, p => p.B > 128); // nothing blue: the colour is not read in the wrong order
            Assert.Same(item, new RoomView { Scene = scene }.HitTest(new Point(marker.Bounds.X + 2, marker.Bounds.Y + 2)));
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task ATextItemWithoutTextIsALabelledBox()
    {
        await HeadlessApp.Run(() =>
        {
            var (room, item) = TextRoom(null);
            using var scene = RoomScene.Build(room);
            var marker = Assert.Single(scene.Markers);
            Assert.Null(marker.Text);
            Assert.NotNull(marker.Caption);
            Assert.Equal("text", marker.Label);
            Assert.Equal(new Rect(-8, -8, 32, 32), marker.Bounds);
            Assert.Same(item, marker.Element);
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task AMovingTextItemKeepsItsLayoutAndOnlyShiftsTheMatrix()
    {
        await HeadlessApp.Run(() =>
        {
            var (room, _) = TextRoom("M");
            using var scene = RoomScene.Build(room);
            var text = scene.Markers[0].Text!;
            Assert.Equal(new Point(18, 13), text.At(new Vector(10, 5)).Transform(default));
            return Task.CompletedTask;
        });
    }
#pragma warning restore CA2000
}
