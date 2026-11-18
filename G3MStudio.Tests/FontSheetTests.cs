using G3MLib.DataFile;
using G3MLib.DataFile.Models;
using G3MStudio.Core.Workspace;

#pragma warning disable CA2000 // the data model owns what is added to it

namespace G3MStudio.Tests;

/// <summary>
/// A font sheet changes size like any other picture: its glyph rectangles are measured from the font's item, so they stay right where the item goes on the pages,
/// and a canvas change on the top or left side moves them along in the same undo step. A change that would cut a glyph or scale the sheet is refused.
/// </summary>
public sealed class FontSheetTests
{
    private static readonly Rgba Red = new(255, 0, 0, 255), Blue = new(0, 0, 255, 255);

    // A 16 x 16 sheet at (64, 64) of the 128 x 128 page with a red glyph at (0, 0) and a blue one at (8, 8), both 4 x 4, and an empty space glyph.
    private static GameMakerData WithGlyphs()
    {
        var data = CopyResourcesTests.Source();
        var page = data.EmbeddedTextures[0];
        var item = new GameMakerTexturePageItem { TexturePage = page, SourceX = 64, SourceY = 64, SourceWidth = 16, SourceHeight = 16, TargetWidth = 16, TargetHeight = 16, BoundingWidth = 16, BoundingHeight = 16 };
        var font = new GameMakerFont { Name = data.Strings.MakeString("fnt_pic", out _), Texture = item };
        font.Glyphs.Add(new GameMakerFont.Glyph { Character = 32 });
        font.Glyphs.Add(new GameMakerFont.Glyph { Character = 65, SourceX = 0, SourceY = 0, SourceWidth = 4, SourceHeight = 4 });
        font.Glyphs.Add(new GameMakerFont.Glyph { Character = 66, SourceX = 8, SourceY = 8, SourceWidth = 4, SourceHeight = 4 });
        data.Fonts.Add(font);
        data.TexturePageItems.Add(item);
        PagePixels.Write(page, [(64, 64, Solid(4, 4, Red)), (72, 72, Solid(4, 4, Blue))]);
        return data;
    }

    private static PixelBuffer Solid(int width, int height, Rgba colour)
    {
        var picture = new PixelBuffer(width, height);
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++) picture.Set(x, y, colour);
        return picture;
    }

    private static void AssertGlyphs(GameMakerData data)
    {
        Assert.Equal(Red, new GlyphTarget("fnt_pic", 1).Read(data).Get(3, 3));
        Assert.Equal(Blue, new GlyphTarget("fnt_pic", 2).Read(data).Get(0, 0));
    }

    [Fact]
    public void AWiderCanvasOnTheTopLeftMovesTheGlyphRectanglesAlongAndUndoPutsThemBack()
    {
        using var data = WithGlyphs();
        var target = new ItemTarget("fonts", "fnt_pic");
        Assert.Null(target.Limit(data));
        var sheet = target.Read(data);
        var op = new ImageOp(ImageOps.Resize, "24", "20", "2", "2");
        var shift = FontSheet.Follow((0, 0), sheet, null, [op]);
        Assert.Equal((8, 4), shift);
        var command = target.Apply(data, ImageEditCommand.Run(sheet, null, [op]), false, shift);
        var history = new EditHistory(data);
        history.Execute(command);
        var font = data.Fonts.First(f => f.Name.Content == "fnt_pic");
        Assert.Equal((24, 20), (font.Texture.SourceWidth, font.Texture.SourceHeight));
        Assert.Equal((8, 4), (font.Glyphs[1].SourceX, font.Glyphs[1].SourceY));
        Assert.Equal((0, 0), (font.Glyphs[0].SourceX, font.Glyphs[0].SourceY)); // the empty glyph stays put
        AssertGlyphs(data);
        history.Undo();
        Assert.Equal((16, 16, 0, 0), (font.Texture.SourceWidth, font.Texture.SourceHeight, font.Glyphs[1].SourceX, font.Glyphs[1].SourceY));
        AssertGlyphs(data);
        history.Redo();
        Assert.Equal((8, 4), (font.Glyphs[1].SourceX, font.Glyphs[1].SourceY));
        AssertGlyphs(data);

        // The journal replays the same edit on a fresh copy.
        using var fresh = WithGlyphs();
        new EditHistory(fresh).Execute(EditCommand.Create(command.Kind, command.Args));
        AssertGlyphs(fresh);
        Assert.Equal((8, 4), (fresh.Fonts.First(f => f.Name.Content == "fnt_pic").Glyphs[1].SourceX, fresh.Fonts.First(f => f.Name.Content == "fnt_pic").Glyphs[1].SourceY));
    }

    [Fact]
    public void ALargerSheetFindsRoomOnThePagesAndTheGlyphsStillReadTheirPixels()
    {
        using var data = WithGlyphs();
        var target = new ItemTarget("fonts", "fnt_pic");
        var grown = ImageEditCommand.Run(target.Read(data), null, [new ImageOp(ImageOps.Resize, "100", "40", "0", "0")]);
        Assert.NotNull(target.Plan(data, 100, 40));
        var command = target.Apply(data, grown, false, (0, 0));
        Assert.IsType<TextureFitCommand>(command);
        new EditHistory(data).Execute(command);
        var item = data.Fonts.First(f => f.Name.Content == "fnt_pic").Texture;
        Assert.Equal((100, 40), (item.SourceWidth, item.SourceHeight));
        Assert.NotEqual((64, 64), (item.SourceX, item.SourceY)); // 100 wide does not fit at x 64 of a 128 page
        AssertGlyphs(data);
    }

    [Fact]
    public void ACutGlyphAndAScaledSheetAreRefusedWithTheReason()
    {
        using var data = WithGlyphs();
        var target = new ItemTarget("fonts", "fnt_pic");
        var sheet = target.Read(data);
        var cut = ImageEditCommand.Run(sheet, null, [new ImageOp(ImageOps.Resize, "10", "10", "0", "0")]);
        Assert.Contains("Glyph 2", Assert.Throws<InvalidOperationException>(() => target.Apply(data, cut, false, (0, 0))).Message, StringComparison.Ordinal);
        var scale = new ImageOp(ImageOps.Scale, "32", "32");
        Assert.Null(FontSheet.Follow((0, 0), sheet, null, [scale]));
        Assert.Null(FontSheet.Follow((0, 0), sheet, null, [new ImageOp(ImageOps.Rotate, "45")])); // a whole-sheet rotation grows the canvas and leaves the glyph rectangles behind
        Assert.Contains("scaled", Assert.Throws<InvalidOperationException>(() => target.Apply(data, ImageEditCommand.Run(sheet, null, [scale]), false, null)).Message, StringComparison.Ordinal);
        // a crop of a selection moves the sheet's corner to the selection's corner
        var selection = PixelSelection.Rectangle(16, 16, 0, 0, 13, 13);
        Assert.Equal((0, 0), FontSheet.Follow((0, 0), sheet, selection, [new ImageOp(ImageOps.Crop)]));
    }

    [Fact]
    public void AResizeWithASelectionStillMovesTheGlyphsLikeTheWholeCanvas()
    {
        var sheet = new PixelBuffer(16, 16);
        var selection = PixelSelection.Rectangle(16, 16, 0, 0, 3, 3);
        var grow = new ImageOp(ImageOps.Resize, "20", "24", "2", "2"); // anchored bottom right: the old canvas moves by (4, 8)
        Assert.Equal((4, 8), FontSheet.Follow((0, 0), sheet, selection, [grow]));
        // the resize dropped the selection, so a scale after it changes the whole sheet
        Assert.Null(FontSheet.Follow((0, 0), sheet, selection, [grow, new ImageOp(ImageOps.Scale, "8", "8")]));
    }

    [Fact]
    public void ATrimAfterAnotherOpIsRefusedSinceItIsMeasuredOnThePictureBefore()
    {
        var sheet = new PixelBuffer(16, 16);
        sheet.Set(5, 5, new Rgba(255, 0, 0, 255));
        var trim = new ImageOp(ImageOps.Trim);
        Assert.Equal((-5, -5), FontSheet.Follow((0, 0), sheet, null, [trim]));
        Assert.Null(FontSheet.Follow((0, 0), sheet, null, [new ImageOp(ImageOps.Resize, "20", "20", "0", "0"), trim]));
    }
}
