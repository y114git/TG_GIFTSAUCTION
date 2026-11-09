using G3MLib.DataFile;
using G3MLib.DataFile.Models;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

#pragma warning disable CA2000 // the data model owns what is added to it
public sealed class ImageImportTests
{
    private static PixelBuffer Solid(int w, int h, byte shade)
    {
        var p = new PixelBuffer(w, h);
        for (var i = 0; i < p.Data.Length; i += 4) (p.Data[i], p.Data[i + 1], p.Data[i + 2], p.Data[i + 3]) = (shade, 0, 0, 255);
        return p;
    }

    private static PixelBuffer Strip(int frames, int size)
    {
        var strip = new PixelBuffer(frames * size, size);
        for (var f = 0; f < frames; f++) strip.Place(Solid(size, size, (byte)(40 * (f + 1))), f * size, 0, false);
        return strip;
    }

    [Fact]
    public void TheStripNameAsksForAFrameCountAndTheGridMathMatches()
    {
        Assert.Equal(8, SheetSplit.StripCount("C:/art/hero_strip8.png"));
        Assert.Equal(12, SheetSplit.StripCount("walk_STRIP12.gif"));
        Assert.Null(SheetSplit.StripCount("hero_strip.png"));
        Assert.Null(SheetSplit.StripCount("strip8_hero.png"));
        Assert.Equal((16, 16), SheetSplit.SizeForCount(66, 34, 4, 2, 2, 2, 0, 0));
        Assert.Equal((3, 2), SheetSplit.GridFor(70, 34, 16, 16, 2, 2, 2, 0));
    }

    [Fact]
    public void ASheetIsCutRowByRowWithOffsetsAndGapsAndALimit()
    {
        var sheet = new PixelBuffer(2 + 2 * 8 + 2, 8);
        sheet.Place(Solid(8, 8, 50), 2, 0, false);
        sheet.Place(Solid(8, 8, 150), 12, 0, false);
        var frames = SheetSplit.Cut(sheet, new FrameGrid(8, 8, 2, 0, 2, 0));
        Assert.Equal(2, frames.Count);
        Assert.Equal((50, 150), (frames[0].Data[0], frames[1].Data[0]));
        Assert.Single(SheetSplit.Cut(sheet, new FrameGrid(8, 8, 2, 0, 2, 0), 1));
        Assert.Empty(SheetSplit.Cut(sheet, new FrameGrid(40, 8, 0, 0, 0, 0)));
    }

    [Fact]
    public void AnAnimatedGifGivesAllItsFramesAndAPictureGivesOne()
    {
        var gif = SpriteGif.Encode([Solid(6, 4, 30), Solid(6, 4, 90), Solid(6, 4, 200)], 10);
        var frames = SheetSplit.Read(gif);
        Assert.Equal(3, frames.Count);
        Assert.All(frames, f => Assert.Equal((6, 4), (f.Width, f.Height)));
        Assert.Single(SheetSplit.Read(FramePicture.ToPng(Solid(5, 5, 1))));
        Assert.Throws<InvalidDataException>(() => SheetSplit.Read([1, 2, 3, 4, 5]));
    }

    [Fact]
    public void ImportedFramesAppendOrReplaceUndoAndSurviveTheJournal()
    {
        using var data = TestData.Create(0);
        TestData.AddPage(data);
        SpriteTemplate.Create(data, "spr_strip");
        var history = new EditHistory(data);
        var frames = SheetSplit.Cut(Strip(4, 32), new FrameGrid(32, 32, 0, 0, 0, 0));
        var pages = data.EmbeddedTextures.Count;

        var append = FramesImportCommand.Build(data, "spr_strip", frames, false);
        history.Execute(append);
        var sprite = data.Sprites.Single();
        Assert.Equal(5, sprite.Textures.Count);
        Assert.Equal(pages + 4, data.EmbeddedTextures.Count);
        Assert.Equal(160, FramePicture.Read(data, "spr_strip", 4).Data[0]);
        history.Undo();
        Assert.Equal((1, pages), (sprite.Textures.Count, data.EmbeddedTextures.Count));
        history.Redo();
        Assert.Equal(5, sprite.Textures.Count);
        history.Undo();

        // A frame of another size needs the replace; the replace takes the new size and undo gives the old one back.
        var small = SheetSplit.Cut(Strip(3, 8), new FrameGrid(8, 8, 0, 0, 0, 0));
        Assert.Throws<InvalidDataException>(() => FramesImportCommand.Build(data, "spr_strip", small, false));
        Assert.Contains("limit", Assert.Throws<InvalidDataException>(() => FramesImportCommand.Build(data, "spr_strip", small, true, 10)).Message, StringComparison.Ordinal);
        var replace = FramesImportCommand.Build(data, "spr_strip", small, true);
        history.Execute(replace);
        Assert.Equal((3, 8u, 8u, 7), (sprite.Textures.Count, sprite.Width, sprite.Height, sprite.MarginRight));
        history.Undo();
        Assert.Equal((1, 32u, 32u, 31), (sprite.Textures.Count, sprite.Width, sprite.Height, sprite.MarginRight));

        var replayed = EditCommand.Create(replace.Kind, replace.Args);
        replayed.Apply(data);
        Assert.Equal((3, 8u), (sprite.Textures.Count, sprite.Width));
    }

    [Fact]
    public void ResizeScalesEveryFrameTheBoxAndTheOriginInOneStepAndMovesTheGroup()
    {
        using var data = TestData.Create(0);
        TestData.AddPage(data);
        var sprite = SpriteTemplate.Create(data, "spr_size");
        sprite.OriginXWrapper = 8;
        data.TextureGroupInfo.Add(new GameMakerTextureGroupInfo { Name = data.Strings.MakeString("tg_other") });
        var history = new EditHistory(data);
        history.Execute(new FrameListCommand("spr_size", FrameListCommand.Duplicate, 0));

        Assert.Equal(new ImageOp(ImageOps.Scale, "64", "16").ToString(), new ResizeSpec(true, 64, 16).Op.ToString());
        Assert.Equal(50, ResizeSpec.Percent(100, 50));
        history.Execute(SpriteResize.Build(data, "spr_size", 0, new ResizeSpec(true, 64, 16), true, "tg_other"));
        Assert.Equal((64u, 16u, 62, 15, 16), (sprite.Width, sprite.Height, sprite.MarginRight, sprite.MarginBottom, sprite.OriginX));
        Assert.All(Enumerable.Range(0, 2), i => Assert.Equal((64, 16), (FramePicture.Read(data, "spr_size", i).Width, FramePicture.Read(data, "spr_size", i).Height)));
        Assert.Equal("tg_other", SpriteResize.GroupOf(data, "spr_size"));
        history.Undo();
        Assert.Equal((32u, 32u, 8), (sprite.Width, sprite.Height, sprite.OriginX));
        Assert.Equal(32, FramePicture.Read(data, "spr_size", 1).Width);
        Assert.NotEqual("tg_other", SpriteResize.GroupOf(data, "spr_size"));

        // The canvas change shifts by the anchor and keeps the pixels; one frame alone leaves the sprite size.
        history.Execute(SpriteResize.Build(data, "spr_size", 1, new ResizeSpec(false, 40, 32, 2, 1), false, null));
        Assert.Equal((32, 40), (FramePicture.Read(data, "spr_size", 0).Width, FramePicture.Read(data, "spr_size", 1).Width));
        Assert.Equal(32u, sprite.Width);
        Assert.Throws<InvalidDataException>(() => SpriteResize.Build(data, "spr_size", 0, new ResizeSpec(true, 0, 5), true, null));
    }

    [Fact]
    public void AnImageBecomesATileLayerOfMatchedTilesAndUnmatchedCellsAreReported()
    {
        using var data = TestData.Create(0);
        TestData.AddPage(data);
        // A tile set of four 8 by 8 tiles in a row: tile 0 empty, tiles 1 and 2 solid, tile 3 half clear.
        var sheet = new PixelBuffer(32, 8);
        sheet.Place(Solid(8, 8, 100), 8, 0, false);
        sheet.Place(Solid(8, 8, 200), 16, 0, false);
        sheet.Place(Solid(4, 8, 250), 24, 0, false);
        using var tiles = new ImageMagick.MagickImage(FramePicture.ToPng(sheet));
        var set = new GameMakerBackground { Name = data.Strings.MakeString("ts_match"), GMS2TileWidth = 8, GMS2TileHeight = 8, GMS2TileColumns = 4, GMS2TileCount = 4, GMS2OutputBorderX = 0, GMS2OutputBorderY = 0, Texture = SpriteTemplate.AddItem(data, tiles) };
        data.Backgrounds.Add(set);
        new ResourceListCommand("rooms", ResourceListCommand.New, "rm_img").Apply(data);
        var room = data.Rooms.Single(r => r.Name.Content == "rm_img");
        (room.Width, room.Height) = (24, 16);

        var picture = new PixelBuffer(24, 16);
        picture.Place(Solid(8, 8, 200), 0, 0, false); // tile 2
        picture.Place(Solid(8, 8, 100), 16, 0, false); // tile 1
        picture.Place(Solid(8, 8, 33), 8, 8, false); // matches nothing
        var match = ImageToTiles.Match(room, set, picture);
        Assert.Equal((2, 1, false), (match.Matched, match.Unmatched.Count, match.Cropped));
        Assert.Equal((1, 1), match.Unmatched[0]);
        Assert.Equal([2u, 0u, 1u], match.Cells[0]);

        var history = new EditHistory(data);
        var layers = room.Layers.Count;
        history.Execute(ImageToTiles.Build(data, "rm_img", "ts_match", ImageToTiles.FreeLayerName(room, "Tiles"), match));
        var made = room.Layers.Single(l => l.LayerName.Content == "Tiles").TilesData;
        Assert.Equal([2u, 0u, 1u], made.TileData[0]);
        Assert.Equal((3u, 2u), (made.TilesX, made.TilesY));
        history.Undo();
        Assert.Equal(layers, room.Layers.Count);
        Assert.Equal("Tiles_1", ImageToTiles.FreeLayerName(room, "Tiles") == "Tiles" ? "Tiles_1" : "x");
    }
}
