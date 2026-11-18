using G3MLib.DataFile.Models;
using G3MStudio.Rooms;

namespace G3MStudio.Tests;

/// <summary>A sprite with nine-slice on keeps its corners and stretches the rest when it is drawn at a scale.</summary>
public sealed class RoomNineSliceTests
{
    private static readonly (byte, byte, byte) Green = (0, 255, 0), Blue = (0, 0, 255), Red = (255, 0, 0);

    // A 16 pixel green sprite with a blue middle (4 to 12), nine-slice borders of 4, drawn at 4, 4 with a scale of 3 (48 pixels: green corners 4 wide, blue middle 40 wide).
    private static (GameMakerRoom Room, GameMakerRoom.GameObject Instance, GameMakerSprite.NineSlice Slice) SlicedRoom()
    {
        var (room, _, instance) = RoomRenderTests.Room();
#pragma warning disable CA2000 // not disposed: the picture of the sprite lives in the data
        var data = TestData.Create();
        using var canvas = new ImageMagick.MagickImage(ImageMagick.MagickColors.Lime, 16, 16);
#pragma warning restore CA2000
        canvas.Draw(new ImageMagick.Drawing.DrawableFillColor(ImageMagick.MagickColors.Blue), new ImageMagick.Drawing.DrawableRectangle(4, 4, 11, 11), new ImageMagick.Drawing.DrawableRectangle(4, 0, 7, 3)); // the left half of the top edge is blue too
        var page = new GameMakerEmbeddedTexture
        {
            TextureData = new GameMakerEmbeddedTexture.TexData { Image = G3MLib.DataFile.Util.GMImage.FromMagickImage(canvas).ConvertToPng() },
            TextureWidth = 16,
            TextureHeight = 16,
            Name = new GameMakerString("sliced"),
        };
        data.EmbeddedTextures.Add(page);
        var item = new GameMakerTexturePageItem { SourceWidth = 16, SourceHeight = 16, TargetWidth = 16, TargetHeight = 16, BoundingWidth = 16, BoundingHeight = 16, TexturePage = page };
        var slice = new GameMakerSprite.NineSlice { Left = 4, Top = 4, Right = 4, Bottom = 4, Enabled = true };
        var sprite = RoomRenderTests.Sprite(item);
        sprite.V3NineSlice = slice;
        instance.ObjectDefinition.Sprite = sprite;
        (instance.X, instance.Y, instance.ScaleX, instance.ScaleY) = (4, 4, 3, 3);
        return (room, instance, slice);
    }

    [Fact]
    public async Task TheCornersKeepTheirSizeAndTheMiddleStretches()
    {
        await HeadlessApp.Run(() =>
        {
            var (room, instance, _) = SlicedRoom();
            using var scene = RoomScene.Build(room);
            var picture = RoomRenderTests.Draw(new RoomView { Scene = scene });
            Assert.Equal(Green, picture.At(5, 5)); // inside the 4 pixel corner
            Assert.Equal(Blue, picture.At(10, 10)); // a plain scaled sprite is still green here
            Assert.Equal(Blue, picture.At(45, 45)); // the middle runs to 4 pixels before the far edge
            Assert.Equal(Green, picture.At(50, 50));
            Assert.Equal(9, scene.Items.Count(i => ReferenceEquals(i.Element, instance)));
            Assert.Equal(new Avalonia.Rect(4, 4, 48, 48), scene.BoundsOf(instance));
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task AHiddenMiddleLeavesAHoleAndANormalScaleIsOnePiece()
    {
        await HeadlessApp.Run(() =>
        {
            var (room, instance, slice) = SlicedRoom();
            slice.TileModes = [GameMakerSprite.NineSlice.TileMode.Stretch, GameMakerSprite.NineSlice.TileMode.Stretch, GameMakerSprite.NineSlice.TileMode.Stretch, GameMakerSprite.NineSlice.TileMode.Stretch, GameMakerSprite.NineSlice.TileMode.Hide];
            using var holed = RoomScene.Build(room);
            Assert.Equal(Red, RoomRenderTests.Draw(new RoomView { Scene = holed }).At(10, 10)); // the red background shows through
            Assert.Equal(8, holed.Items.Count(i => ReferenceEquals(i.Element, instance)));

            (instance.ScaleX, instance.ScaleY) = (1, 1);
            using var plain = RoomScene.Build(room);
            Assert.Single(plain.Items, i => ReferenceEquals(i.Element, instance));
            return Task.CompletedTask;
        });
    }

    // The top edge is 8 source pixels wide with a blue left half. At a scale of 3.25 it is 44 pixels wide from x 8: five whole copies and a half one.
    // The probes are the blue half of copy 0, the second half of copy 0 (x 13), the blue half of copy 1 (x 17) and the cut copy 5 (x 49).
    private static async Task<(int Items, (byte, byte, byte)[] Probes)> TopEdge(GameMakerSprite.NineSlice.TileMode mode)
    {
        var result = (0, Array.Empty<(byte, byte, byte)>());
        await HeadlessApp.Run(() =>
        {
            var (room, instance, slice) = SlicedRoom();
            instance.ScaleX = 3.25f;
            var stretch = GameMakerSprite.NineSlice.TileMode.Stretch;
            slice.TileModes = [stretch, mode, stretch, stretch, stretch];
            using var scene = RoomScene.Build(room);
            var picture = RoomRenderTests.Draw(new RoomView { Scene = scene });
            result = (scene.Items.Count, new[] { picture.At(9, 5), picture.At(13, 5), picture.At(17, 5), picture.At(49, 5) });
            return Task.CompletedTask;
        });
        return result;
    }

    [Fact]
    public async Task RepeatTilesTheEdgeAndCutsTheLastCopyShort()
    {
        var (items, probes) = await TopEdge(GameMakerSprite.NineSlice.TileMode.Repeat);
        Assert.Equal([Blue, Green, Blue, Blue], probes); // a stretched edge is blue at x 13
        Assert.True(items < 20); // the copies are grids, not one item each
    }

    [Fact]
    public async Task MirrorFlipsEverySecondCopyAndItsCutCopyShowsTheFarEnd()
    {
        var (_, probes) = await TopEdge(GameMakerSprite.NineSlice.TileMode.Mirror);
        Assert.Equal([Blue, Green, Green, Green], probes); // copy 1 is flipped (green first), copy 5 is flipped and cut to the green far end
    }

    [Fact]
    public async Task BlankRepeatLeavesTheRemainderEmpty()
    {
        var (_, probes) = await TopEdge(GameMakerSprite.NineSlice.TileMode.BlankRepeat);
        Assert.Equal([Blue, Green, Blue, Red], probes); // the red background shows through
    }
}
