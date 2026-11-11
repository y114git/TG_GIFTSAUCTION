using G3MLib.DataFile;
using G3MLib.DataFile.Models;
using G3MStudio.Core.Workspace;

#pragma warning disable CA2000 // the data model owns what is added to it

namespace G3MStudio.Tests;

public sealed class PictureTargetTests
{
    private static GameMakerData WithFont()
    {
        var data = CopyResourcesTests.Source();
        var page = data.EmbeddedTextures[0];
        var item = new GameMakerTexturePageItem { TexturePage = page, SourceX = 64, SourceY = 64, SourceWidth = 16, SourceHeight = 16, TargetWidth = 16, TargetHeight = 16 };
        data.Fonts.Add(new GameMakerFont { Name = data.Strings.MakeString("fnt_pic", out _), Texture = item });
        data.TexturePageItems.Add(item);
        return data;
    }

    private static PixelBuffer Dot(PixelBuffer picture)
    {
        var copy = picture.Copy();
        copy.Set(1, 1, new Rgba(255, 0, 0, 255));
        return copy;
    }

    // Applies the target's command through a history, then checks undo, redo and a command rebuilt from the journal text.
    private static void RoundTrip(GameMakerData data, IPictureTarget target)
    {
        var red = new Rgba(255, 0, 0, 255);
        var before = target.Read(data);
        var history = new EditHistory(data);
        var command = target.Apply(data, Dot(before), false);
        history.Execute(command);
        Assert.Equal(red, target.Read(data).Get(1, 1));
        history.Undo();
        Assert.NotEqual(red, target.Read(data).Get(1, 1));
        history.Redo();
        Assert.Equal(red, target.Read(data).Get(1, 1));
        history.Undo();
        var replay = EditCommand.Create(command.Kind, command.Args);
        new EditHistory(data).Execute(replay);
        Assert.Equal(red, target.Read(data).Get(1, 1));
    }

    [Fact]
    public void AFrameRoundTripsThroughApplyUndoRedoAndTheJournal()
    {
        using var data = CopyResourcesTests.Source();
        Assert.Null(new FrameTarget("spr_a", 0).Limit(data));
        RoundTrip(data, new FrameTarget("spr_a", 0));
    }

    [Fact]
    public void APageItemRoundTripsAndAPictureLargerThanItsRectangleIsRefusedUntilForced()
    {
        using var data = WithFont();
        var target = new ItemTarget("fonts", "fnt_pic");
        Assert.Equal((16, 16), target.Limit(data));
        RoundTrip(data, target);
        var big = new PixelBuffer(20, 16);
        Assert.Throws<RiskyRefusalException>(() => target.Apply(data, big, false));
        Assert.IsType<ReplaceItemCommand>(target.Apply(data, big, true));
        Assert.IsType<ItemTarget>(PictureTargets.For("texturePageItems", "x"));
        Assert.Null(PictureTargets.For("rooms", "x"));
    }

    [Fact]
    public void AWholePageRoundTripsAndAnotherSizeNeedsTheBypass()
    {
        using var data = CopyResourcesTests.Source();
        var target = new PageTarget("Texture 0");
        Assert.Equal((128, 128), target.Limit(data));
        RoundTrip(data, target);
        var wrong = new PixelBuffer(64, 64);
        Assert.Throws<RiskyRefusalException>(() => new EditHistory(data).Execute(target.Apply(data, wrong, false)));
    }

    [Fact]
    public void ACollisionMaskIsEditedAsABlackAndWhitePictureAtItsFixedSize()
    {
        using var data = CopyResourcesTests.Source();
        var sprite = data.Sprites.First(s => s.Name.Content == "spr_a");
        new EditHistory(data).Execute(new SpriteMaskCommand("spr_a", MaskMode.Manual, MaskShape.Precise, SpriteMaskCommand.BoxOf(sprite), 0));
        Assert.NotEmpty(sprite.CollisionMasks);
        var target = new MaskTarget("spr_a", 0);
        var entry = sprite.CollisionMasks[0];
        Assert.Equal((entry.Width, entry.Height), target.Limit(data));
        var picture = target.Read(data);
        var white = new Rgba(255, 255, 255, 255);
        var black = new Rgba(0, 0, 0, 255);
        var flipped = picture.Get(1, 1) == white ? black : white;
        var edited = picture.Copy();
        edited.Set(1, 1, flipped);
        var history = new EditHistory(data);
        var command = target.Apply(data, edited, false);
        history.Execute(command);
        Assert.Equal(flipped, target.Read(data).Get(1, 1));
        history.Undo();
        Assert.Equal(picture.Get(1, 1), target.Read(data).Get(1, 1));
        history.Redo();
        Assert.Equal(flipped, target.Read(data).Get(1, 1));
        Assert.Throws<InvalidOperationException>(() => target.Apply(data, new PixelBuffer(3, 3), false));
        Assert.IsType<MaskTarget>(PictureTargets.For("spriteMasks", "spr_a#0"));
    }

    [Fact]
    public void AnImageFileInTheIncludedFilesFolderIsEditedWarnedAboutAndWrittenBackUndoably()
    {
        using var dir = new TempDir();
        using var data = TestData.Create(0);
        var dataFile = dir.File("data.win");
        using (var blue = new ImageMagick.MagickImage(ImageMagick.MagickColors.Blue, 6, 4)) File.WriteAllBytes(dir.File("logo.png"), blue.ToByteArray(ImageMagick.MagickFormat.Png));
        var original = File.ReadAllBytes(dir.File("logo.png"));
        var target = new IncludedTarget(dataFile, "logo.png");
        Assert.Null(target.Limit(data));
        var picture = target.Read(data);
        Assert.Equal((6, 4), (picture.Width, picture.Height));
        var edited = picture.Copy();
        edited.Set(2, 1, new Rgba(255, 0, 0, 255));
        Assert.Throws<RiskyRefusalException>(() => new EditHistory(data).Execute(target.Apply(data, edited, false)));
        Assert.Equal(original, File.ReadAllBytes(dir.File("logo.png")));

        var history = new EditHistory(data);
        history.Execute(target.Apply(data, edited, true));
        Assert.Equal(new Rgba(255, 0, 0, 255), target.Read(data).Get(2, 1));
        history.Undo();
        Assert.Equal(original, File.ReadAllBytes(dir.File("logo.png")));
        history.Redo();
        Assert.Equal(new Rgba(255, 0, 0, 255), target.Read(data).Get(2, 1));
        Assert.IsType<IncludedPictureCommand>(EditCommand.Create(IncludedPictureCommand.Name, target.Apply(data, edited, true).Args));
        Assert.Null(PictureTargets.For("includedFiles", "notes.txt", 0, dataFile));
        Assert.IsType<IncludedTarget>(PictureTargets.For("includedFiles", "logo.png", 0, dataFile));
    }
}
