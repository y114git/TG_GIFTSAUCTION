using G3MLib.DataFile;
using G3MLib.DataFile.Models;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

public sealed class ReplaceItemTests
{
    [Fact]
    public void ReplacingTheTextureOfAFontChangesThePageAndUndoRestoresIt()
    {
        using var data = TestData.Create(0);
        TestData.AddPage(data);
        var page = data.EmbeddedTextures[0];
#pragma warning disable CA2000 // the data model owns what is added to it
        var item = new GameMakerTexturePageItem { TexturePage = page, SourceX = 8, SourceY = 8, SourceWidth = 16, SourceHeight = 16, TargetWidth = 16, TargetHeight = 16 };
        data.Fonts.Add(new GameMakerFont { Name = data.Strings.MakeString("fnt_test", out _), Texture = item });
#pragma warning restore CA2000
        var before = page.TextureData.Image;
        using var red = new ImageMagick.MagickImage(ImageMagick.MagickColors.Red, 16, 16);
        var command = new ReplaceItemCommand("fonts", "fnt_test", red.ToByteArray(ImageMagick.MagickFormat.Png));

        command.Apply(data);
        Assert.NotSame(before, page.TextureData.Image);
        command.Revert(data);
        Assert.Same(before, page.TextureData.Image);

        var rebuilt = EditCommand.Create(command.Kind, command.Args);
        Assert.IsType<ReplaceItemCommand>(rebuilt);
        Assert.Equal(command.Args["picture"], rebuilt.Args["picture"]);
        Assert.Throws<InvalidOperationException>(() => new ReplaceItemCommand("sprites", "fnt_test", []).Apply(data));
        Assert.Throws<InvalidOperationException>(() => new ReplaceItemCommand("fonts", "missing", []).Apply(data));
    }

    [Fact]
    public void RedoPutsTheNewPictureBackUnderItsOwnSize()
    {
        using var data = TestData.Create(0);
        TestData.AddPage(data);
        var page = data.EmbeddedTextures[0];
#pragma warning disable CA2000 // the data model owns what is added to it
        var item = new GameMakerTexturePageItem { TexturePage = page, SourceX = 8, SourceY = 8, SourceWidth = 16, SourceHeight = 16, TargetWidth = 16, TargetHeight = 16 };
        data.Fonts.Add(new GameMakerFont { Name = data.Strings.MakeString("fnt_test", out _), Texture = item });
#pragma warning restore CA2000
        using var wide = new ImageMagick.MagickImage(ImageMagick.MagickColors.Red, 24, 12);
        var command = new ReplaceItemCommand("fonts", "fnt_test", wide.ToByteArray(ImageMagick.MagickFormat.Png));

        command.Apply(data);
        var size = (item.TargetWidth, item.TargetHeight);
        Assert.NotEqual((ushort)16, item.TargetWidth);
        command.Revert(data);
        Assert.Equal(((ushort)16, (ushort)16), (item.TargetWidth, item.TargetHeight));
        command.Apply(data);
        Assert.Equal(size, (item.TargetWidth, item.TargetHeight));
    }
}
