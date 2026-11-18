using G3MLib.DataFile;
using G3MLib.DataFile.Models;
using G3MStudio.Core.Workspace;
using G3MStudio.Editor;

#pragma warning disable CA2000 // the data owns the resources it is given
namespace G3MStudio.Tests;

/// <summary>The Advanced choices of the Resize dialog (page, growing, new page, largest side) reach tile sets, fonts and embedded images, and the editor applies them.</summary>
public sealed class PictureTargetFitTests
{
    private static GameMakerData WithItem(string type, string name)
    {
        var data = CopyResourcesTests.Source();
        var item = new GameMakerTexturePageItem { TexturePage = data.EmbeddedTextures[0], SourceX = 64, SourceY = 64, SourceWidth = 16, SourceHeight = 16, TargetWidth = 16, TargetHeight = 16 };
        data.TexturePageItems.Add(item);
        var title = data.Strings.MakeString(name);
        switch (type)
        {
            case "tileSets": data.Backgrounds.Add(new GameMakerBackground { Name = title, Texture = item }); break;
            case "fonts": data.Fonts.Add(new GameMakerFont { Name = title, Texture = item }); break;
            default: data.EmbeddedImages.Add(new GameMakerEmbeddedImage { Name = title, TextureEntry = item }); break;
        }

        return data;
    }

    [Theory]
    [InlineData("tileSets")]
    [InlineData("fonts")]
    [InlineData("embeddedImages")]
    public void TheAdvancedChoicesPlanAndPlaceThePictureOfThisTarget(string type)
    {
        using var data = WithItem(type, "pic_fit");
        var target = PictureTargets.For(type, "pic_fit")!;
        var wide = new PixelBuffer(3000, 8);
        var strict = new FitOptions(Grow: false, NewPage: false);

        Assert.NotEmpty(target.Plan(data, 3000, 8, null)!);
        Assert.Contains("not allowed", Assert.Throws<InvalidDataException>(() => target.Plan(data, 3000, 8, strict)).Message, StringComparison.Ordinal);
        // the edit carries the choices: the strict one is refused when it runs, the default one places the picture
        Assert.Throws<InvalidDataException>(() => new EditHistory(data).Execute(target.Apply(data, wide, true, (0, 0), strict))); // force: a tile grid warning is not what is tested here
        new EditHistory(data).Execute(target.Apply(data, wide, true, (0, 0), null));
        Assert.Equal((3000, 8), (target.Read(data).Width, target.Read(data).Height));
    }

    [Fact]
    public void TheSessionOffersThePagesAndAppliesWithTheChoicesOfTheLastResize()
    {
        using var data = WithItem("tileSets", "ts_fit");
        var target = PictureTargets.For("tileSets", "ts_fit")!;
        var session = new TargetImageSession(target, target.Read(data)) { Pages = WorkspaceImageSession.PagesOf(data), AutomaticMax = 2048, Planner = (w, h, fit) => target.Plan(data, w, h, fit) };
        Assert.NotEmpty(session.ResizeContext!.Pages!);
        var fit = new FitOptions(Grow: false);
        Assert.True(session.CommitResize(0, new ResizeSpec(false, 20, 20), false, null, fit));
        Assert.Equal(fit, session.Fit);
        var command = (TextureFitCommand)target.Apply(data, session.Current, true, session.Shift, session.Fit);
        Assert.Equal(fit, FitOptions.Parse(command.Args["fit"]));
    }
}
