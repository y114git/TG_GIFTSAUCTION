using System.Numerics;
using G3MLib.DataFile;
using G3MLib.DataFile.Models;
using G3MLib.Modding.Integrity;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

/// <summary>
/// Resizing the frames of a sprite moves everything that is measured on the picture: the origin, the collision box and the masks. A stretch scales them, a canvas change
/// shifts them by the anchor; an automatic box is found again from the new pixels, a manual box and its mask keep their shape.
/// </summary>
public sealed class SpriteResizeBoxTests
{
    private static readonly Rgba Red = new(255, 0, 0, 255);

    // spr_a as a 40 x 40 sprite with a red 10 x 10 square at (10, 10) and the origin at (4, 6).
    private static (GameMakerData Data, EditHistory History, GameMakerSprite Sprite) Fixture(MaskMode mode)
    {
        var data = CopyResourcesTests.Source();
        var history = new EditHistory(data);
        var picture = new PixelBuffer(40, 40);
        for (var y = 10; y < 20; y++)
            for (var x = 10; x < 20; x++) picture.Set(x, y, Red);
        history.Execute(new TextureFitCommand("sprites", "spr_a", 0, FramePicture.ToPng(picture)));
        var sprite = data.Sprites.First(s => s.Name.Content == "spr_a");
        (sprite.Width, sprite.Height) = (40, 40);
        history.Execute(new SpriteOriginCommand("spr_a", 4, 6));
        history.Execute(new SpriteMaskCommand("spr_a", mode, MaskShape.Precise, new MaskBox(8, 8, 21, 21)));
        return (data, history, sprite);
    }

    private static int Bits(GameMakerSprite sprite) => sprite.CollisionMasks.Sum(m => m.Data.Sum(b => BitOperations.PopCount(b)));

    private static void AssertFileIsValid(GameMakerData data)
    {
        using var back = GameMakerIO.Read(new MemoryStream(TestData.Bytes(data)));
        var sprite = back.Sprites.First(s => s.Name.Content == "spr_a");
        var mine = data.Sprites.First(s => s.Name.Content == "spr_a");
        Assert.Equal((mine.Width, mine.Height, mine.MarginLeft, mine.MarginTop, mine.MarginRight, mine.MarginBottom, mine.OriginX, mine.OriginY),
            (sprite.Width, sprite.Height, sprite.MarginLeft, sprite.MarginTop, sprite.MarginRight, sprite.MarginBottom, sprite.OriginX, sprite.OriginY));
        Assert.Equal(mine.CollisionMasks.Select(m => (m.Width, m.Height, m.Data)), sprite.CollisionMasks.Select(m => (m.Width, m.Height, m.Data)));
        var check = DataIntegrityService.ValidateOnly(back);
        Assert.True(check.Success, string.Join("; ", check.Errors));
    }

    [Fact]
    public void AnAutomaticBoxFollowsAStretchOfTheFramesAndTheOriginIsScaled()
    {
        var (data, history, sprite) = Fixture(MaskMode.Automatic);
        using (data)
        {
            Assert.Equal((10, 10, 19, 19), (sprite.MarginLeft, sprite.MarginTop, sprite.MarginRight, sprite.MarginBottom));
            history.Execute(SpriteResize.Build(data, "spr_a", 0, new ResizeSpec(true, 80, 80), true, null));
            Assert.Equal((80u, 80u), (sprite.Width, sprite.Height));
            Assert.Equal((20, 20, 39, 39), (sprite.MarginLeft, sprite.MarginTop, sprite.MarginRight, sprite.MarginBottom)); // the right edge is the last scaled pixel, not 38
            Assert.Equal((8, 12), (sprite.OriginX, sprite.OriginY));
            Assert.Equal(400, Bits(sprite));
            AssertFileIsValid(data);
            history.Undo();
            Assert.Equal((40u, 10, 19, 4), (sprite.Width, sprite.MarginLeft, sprite.MarginRight, sprite.OriginX));
            Assert.Equal(100, Bits(sprite));
        }
    }

    [Fact]
    public void AnAutomaticBoxMovesWithTheAnchorOfACanvasChangeAndIsCutWhereTheCanvasShrinks()
    {
        var (data, history, sprite) = Fixture(MaskMode.Automatic);
        using (data)
        {
            history.Execute(SpriteResize.Build(data, "spr_a", 0, new ResizeSpec(false, 60, 50, 2, 2), true, null)); // bottom right: the picture moves by 20 and 10
            Assert.Equal((60u, 50u), (sprite.Width, sprite.Height));
            Assert.Equal((30, 20, 39, 29), (sprite.MarginLeft, sprite.MarginTop, sprite.MarginRight, sprite.MarginBottom));
            Assert.Equal((24, 16), (sprite.OriginX, sprite.OriginY));
            AssertFileIsValid(data);
            history.Undo();
            history.Execute(SpriteResize.Build(data, "spr_a", 0, new ResizeSpec(false, 15, 15, 0, 0), true, null)); // top left: only a 5 x 5 part of the square stays
            Assert.Equal((10, 10, 14, 14), (sprite.MarginLeft, sprite.MarginTop, sprite.MarginRight, sprite.MarginBottom));
            Assert.Equal(25, Bits(sprite));
            AssertFileIsValid(data);
        }
    }

    [Fact]
    public void AManualBoxAndItsMaskScaleAndShiftWithThePicture()
    {
        var (data, history, sprite) = Fixture(MaskMode.Manual);
        using (data)
        {
            Assert.Equal(100, Bits(sprite));
            var command = SpriteResize.Build(data, "spr_a", 0, new ResizeSpec(true, 80, 80), true, null);
            history.Execute(command);
            Assert.Equal((16, 16, 43, 43), (sprite.MarginLeft, sprite.MarginTop, sprite.MarginRight, sprite.MarginBottom));
            Assert.Equal(400, Bits(sprite)); // each solid pixel became 2 x 2
            AssertFileIsValid(data);
            history.Undo();
            Assert.Equal((8, 8, 21, 21), (sprite.MarginLeft, sprite.MarginTop, sprite.MarginRight, sprite.MarginBottom));
            Assert.Equal(100, Bits(sprite));
            history.Redo();
            history.Undo();
            history.Execute(SpriteResize.Build(data, "spr_a", 0, new ResizeSpec(false, 50, 50, 1, 1), true, null)); // middle: shifts by 5
            Assert.Equal((13, 13, 26, 26), (sprite.MarginLeft, sprite.MarginTop, sprite.MarginRight, sprite.MarginBottom));
            Assert.Equal(100, Bits(sprite));
            AssertFileIsValid(data);
        }
    }

    [Fact]
    public void ABoxThatLeavesAShrunkCanvasKeepsAtLeastOnePixelInsideIt()
    {
        var (data, history, sprite) = Fixture(MaskMode.Manual);
        using (data)
        {
            history.Execute(SpriteResize.Build(data, "spr_a", 0, new ResizeSpec(false, 6, 6, 0, 0), true, null));
            Assert.True(sprite.MarginLeft <= sprite.MarginRight && sprite.MarginRight < 6 && sprite.MarginBottom < 6);
            AssertFileIsValid(data);
        }
    }

    [Theory]
    [InlineData(MaskMode.Automatic)]
    [InlineData(MaskMode.Full)]
    [InlineData(MaskMode.Manual)]
    public void AStretchWithoutScalingTheBoxLeavesTheOriginAndTheMarginsAlone(MaskMode mode)
    {
        var (data, history, sprite) = Fixture(mode);
        using (data)
        {
            var before = (sprite.MarginLeft, sprite.MarginTop, sprite.MarginRight, sprite.MarginBottom, sprite.OriginX, sprite.OriginY);
            history.Execute(SpriteResize.Build(data, "spr_a", 0, new ResizeSpec(true, 80, 80) { ScaleBox = false }, true, null));
            Assert.Equal((80u, 80u), (sprite.Width, sprite.Height)); // the frames are stretched, the sprite has the new size
            Assert.Equal((uint)mode, sprite.BBoxMode);
            Assert.Equal(before, (sprite.MarginLeft, sprite.MarginTop, sprite.MarginRight, sprite.MarginBottom, sprite.OriginX, sprite.OriginY));
            AssertFileIsValid(data);
            history.Undo();
            Assert.Equal((uint)mode, sprite.BBoxMode);
            Assert.Equal((40u, before), (sprite.Width, (sprite.MarginLeft, sprite.MarginTop, sprite.MarginRight, sprite.MarginBottom, sprite.OriginX, sprite.OriginY)));
        }
    }

    [Fact]
    public void AStretchThatScalesTheBoxStillMovesTheOriginAndTheMargins()
    {
        var (data, history, sprite) = Fixture(MaskMode.Manual);
        using (data)
        {
            history.Execute(SpriteResize.Build(data, "spr_a", 0, new ResizeSpec(true, 80, 80) { ScaleBox = true }, true, null));
            Assert.Equal((16, 16, 43, 43, 8, 12), (sprite.MarginLeft, sprite.MarginTop, sprite.MarginRight, sprite.MarginBottom, sprite.OriginX, sprite.OriginY));
        }
    }

    [Fact]
    public void TheMaskResizeEditReplaysFromItsArguments()
    {
        var (data, _, _) = Fixture(MaskMode.Manual);
        using (data)
        {
            var edit = new MaskResizeCommand("spr_a", 40, 40, true, 0, 0);
            var replayed = (MaskResizeCommand)EditCommand.Create(edit.Kind, edit.Args);
            Assert.Equal(edit.Args, replayed.Args);
        }
    }

    [Fact]
    public void AResizeFillsTheNewAreaWithTheChosenColourAndTheMethodChangesTheEdges()
    {
        var picture = new PixelBuffer(2, 1);
        picture.Set(0, 0, new Rgba(0, 0, 0, 255));
        picture.Set(1, 0, new Rgba(255, 255, 255, 255));
        var grown = picture.Resized(4, 3, 1, 1, new Rgba(0, 0, 255, 255));
        Assert.Equal(new Rgba(0, 0, 255, 255), grown.Get(0, 0));
        Assert.Equal(new Rgba(255, 255, 255, 255), grown.Get(2, 1)); // the pixels stay where the anchor puts them
        Assert.Equal(Rgba.Clear, picture.Resized(4, 3, 1, 1).Get(0, 0));
        var hard = picture.Scaled(4, 1);
        Assert.Equal([0, 0, 255, 255], [hard.Get(0, 0).R, hard.Get(1, 0).R, hard.Get(2, 0).R, hard.Get(3, 0).R]);
        var smooth = picture.Scaled(4, 1, true);
        Assert.InRange(smooth.Get(1, 0).R, 1, 254);
        Assert.Equal((byte)255, smooth.Get(1, 0).A);
        var op = new ResizeSpec(true, 4, 1) { Smooth = true }.Op;
        Assert.Equal("scale;4;1;smooth", op.ToString());
        Assert.Equal("resize;4;3;1;1;#0000FF", new ResizeSpec(false, 4, 3, 1, 1) { Fill = new Rgba(0, 0, 255, 255) }.Op.ToString());
    }
}
