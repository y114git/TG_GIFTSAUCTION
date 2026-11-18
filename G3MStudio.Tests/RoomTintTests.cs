using G3MLib.DataFile.Models;
using G3MStudio.Rooms;

namespace G3MStudio.Tests;

/// <summary>The blend colour of an instance, tile or background layer multiplies the picture; white or unset leaves it as it is.</summary>
public sealed class RoomTintTests
{
    private static (GameMakerRoom Room, GameMakerRoom.GameObject Instance) WhiteInstanceRoom(uint color)
    {
        var (room, _, instance) = RoomRenderTests.Room();
#pragma warning disable CA2000 // not disposed: the picture of the sprite lives in it
        var data = TestData.Create();
#pragma warning restore CA2000
        instance.ObjectDefinition.Sprite = RoomRenderTests.Sprite(RoomRenderTests.Picture(data, ImageMagick.MagickColors.White, 16));
        instance.Color = color;
        return (room, instance);
    }

    [Theory]
    [InlineData(0xFF00FF00u, 0, 255, 0)] // 0xAABBGGRR: green
    [InlineData(0xFF0000FFu, 255, 0, 0)]
    [InlineData(0xFF808080u, 128, 128, 128)]
    [InlineData(0xFFFFFFFFu, 255, 255, 255)]
    [InlineData(0u, 255, 255, 255)] // a model that never set a colour
    public async Task AnInstanceColourMultipliesItsPicture(uint color, int red, int green, int blue)
    {
        await HeadlessApp.Run(() =>
        {
            var (room, _) = WhiteInstanceRoom(color);
            using var scene = RoomScene.Build(room);
            var (r, g, b) = RoomRenderTests.Draw(new RoomView { Scene = scene }).At(30, 30);
            Assert.Equal((red, green, blue), (r, g, b));
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task InstancesOfTheSameColourShareOneCropAndAWhiteInstanceMakesNone()
    {
        await HeadlessApp.Run(() =>
        {
            var (room, instance) = WhiteInstanceRoom(0xFF00FF00);
            var twin = new GameMakerRoom.GameObject { InstanceID = 100002, X = 0, Y = 0, ScaleX = 1, ScaleY = 1, ObjectDefinition = instance.ObjectDefinition, Color = 0xFF00FF00 };
            room.Layers![0].InstancesData.Instances.Add(twin);
            using var tinted = RoomScene.Build(room);
            Assert.Single(tinted.Tinted);
            Assert.Equal(2, tinted.Items.Count(i => i.Element is GameMakerRoom.GameObject));

            instance.Color = twin.Color = 0xFFFFFFFF;
            using var plain = RoomScene.Build(room);
            Assert.Empty(plain.Tinted);
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task ABackgroundLayerColourTintsItsPictureAndAlphaStillFadesIt()
    {
        await HeadlessApp.Run(() =>
        {
            var (room, background, _) = RoomRenderTests.Room(tiled: false);
            background.BackgroundData.Color = 0xFF808080; // half grey
            using var scene = RoomScene.Build(room);
            Assert.Equal((128, 0, 0), RoomRenderTests.Draw(new RoomView { Scene = scene }).At(2, 2));
            Assert.Equal(1, scene.Items.Count(i => i.Element is null));
            return Task.CompletedTask;
        });
    }
}
