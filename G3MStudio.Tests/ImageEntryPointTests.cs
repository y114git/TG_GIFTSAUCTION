using G3MLib.DataFile;
using G3MLib.DataFile.Models;
using G3MStudio.Core.Workspace;
using static G3MLib.DataFile.Models.GameMakerRoom;

#pragma warning disable CA2000 // the data model owns what is added to it

namespace G3MStudio.Tests;

/// <summary>The places that open the image editor and the targets and commands behind them.</summary>
public sealed class ImageEntryPointTests
{
    [Fact]
    public void ARoomLayerAnInstanceAnObjectAndAnElementNameThePictureTheyShow()
    {
        using var data = CopyResourcesTests.Source();
        var sprite = data.Sprites.First(s => s.Name.Content == "spr_a");
        var obj = data.GameObjects.First(o => o.Name.Content == "obj_a");
        Assert.Equal("sprites:spr_a", PictureRefs.KeyOf(obj));
        Assert.Equal("sprites:spr_a", PictureRefs.KeyOf(new GameObject { ObjectDefinition = obj }));
        Assert.Equal("sprites:spr_a", PictureRefs.KeyOf(new SpriteInstance { Sprite = sprite }));
        Assert.Equal("sprites:spr_a", PictureRefs.KeyOf(new Layer { Data = new Layer.LayerBackgroundData { Sprite = sprite } }));
        Assert.Null(PictureRefs.KeyOf(new Layer()));
        Assert.Null(PictureRefs.MaskKeyOf(obj));
        obj.TextureMaskId = sprite;
        Assert.Equal("sprites:spr_a", PictureRefs.MaskKeyOf(obj));

        var set = new GameMakerBackground { Name = data.Strings.MakeString("ts_a") };
        Assert.Equal("tileSets:ts_a", PictureRefs.KeyOf(new Layer { Data = new Layer.LayerTilesData { Background = set } }));
        Assert.NotNull(PictureTargets.For("tileSets", "ts_a"));
    }

    [Fact]
    public void AGlyphIsEditedInItsRectangleOnTheSheetAsOneUndoStep()
    {
        using var data = CopyResourcesTests.Source();
        var page = data.EmbeddedTextures[0];
        var item = new GameMakerTexturePageItem { TexturePage = page, SourceX = 64, SourceY = 64, SourceWidth = 16, SourceHeight = 16, TargetWidth = 16, TargetHeight = 16 };
        var font = new GameMakerFont { Name = data.Strings.MakeString("fnt_g"), Texture = item };
        font.Glyphs.Add(new GameMakerFont.Glyph { Character = 65, SourceX = 4, SourceY = 2, SourceWidth = 5, SourceHeight = 6 });
        data.Fonts.Add(font);
        data.TexturePageItems.Add(item);
        var target = (GlyphTarget)PictureTargets.For("fontGlyphs", "fnt_g#0")!;
        Assert.Equal((5, 6), target.Limit(data));
        var sheetBefore = new ItemTarget("fonts", "fnt_g").Read(data);
        var red = new Rgba(255, 0, 0, 255);
        var picture = target.Read(data);
        Assert.Equal((5, 6), (picture.Width, picture.Height));
        picture.Set(1, 1, red);
        var history = new EditHistory(data);
        history.Execute(target.Apply(data, picture, false));
        Assert.Equal(red, target.Read(data).Get(1, 1));
        Assert.Equal(red, new ItemTarget("fonts", "fnt_g").Read(data).Get(5, 3));
        Assert.Equal(sheetBefore.Get(0, 0), new ItemTarget("fonts", "fnt_g").Read(data).Get(0, 0));
        history.Undo();
        Assert.NotEqual(red, target.Read(data).Get(1, 1));
        Assert.Throws<InvalidOperationException>(() => target.Apply(data, new PixelBuffer(6, 6), false));
    }

    [Fact]
    public void ATextFileInTheIncludedFilesFolderIsWarnedAboutWrittenBackAndUndone()
    {
        using var dir = new TempDir();
        using var data = TestData.Create(0);
        var dataFile = dir.File("data.win");
        File.WriteAllText(dir.File("notes.txt"), "one");
        var history = new EditHistory(data);
        Assert.Throws<RiskyRefusalException>(() => history.Execute(new IncludedTextCommand(dataFile, "notes.txt", "two")));
        Assert.Equal("one", File.ReadAllText(dir.File("notes.txt")));
        var command = new IncludedTextCommand(dataFile, "notes.txt", "two", true);
        history.Execute(command);
        Assert.Equal("two", File.ReadAllText(dir.File("notes.txt")));
        history.Undo();
        Assert.Equal("one", File.ReadAllText(dir.File("notes.txt")));
        history.Redo();
        Assert.Equal("two", File.ReadAllText(dir.File("notes.txt")));
        Assert.IsType<IncludedTextCommand>(EditCommand.Create(IncludedTextCommand.Name, command.Args));
        Assert.True(IncludedTextCommand.IsText("a/b.JSON"));
        Assert.False(IncludedTextCommand.IsText("a.png"));
        File.WriteAllBytes(dir.File("a.png"), [1]);
        Assert.Throws<InvalidOperationException>(() => new EditHistory(data).Execute(new IncludedTextCommand(dataFile, "a.png", "x", true)));
    }

    [Fact]
    public void IncludedAudioAndEmbeddedAudioAreReplacedUndoably()
    {
        using var dir = new TempDir();
        using var data = TestData.Create(0);
        var dataFile = dir.File("data.win");
        File.WriteAllBytes(dir.File("hit.ogg"), [1, 2, 3]);
        var history = new EditHistory(data);
        history.Execute(new IncludedAudioCommand(dataFile, "hit.ogg", [9, 9], true));
        Assert.Equal([9, 9], File.ReadAllBytes(dir.File("hit.ogg")));
        history.Undo();
        Assert.Equal([1, 2, 3], File.ReadAllBytes(dir.File("hit.ogg")));
        Assert.Throws<InvalidOperationException>(() => new IncludedAudioCommand(dataFile, "hit.ogg", [], true).Apply(data));
        Assert.IsType<IncludedAudioCommand>(EditCommand.Create(IncludedAudioCommand.Name, new IncludedAudioCommand(dataFile, "hit.ogg", [4], true).Args));

        var entry = new GameMakerEmbeddedAudio { Name = data.Strings.MakeString("audio0"), Data = [1] };
        data.EmbeddedAudio.Add(entry);
        var command = new ReplaceEmbeddedAudioCommand("audio0", [5, 6]);
        history.Execute(command);
        Assert.Equal([5, 6], entry.Data);
        history.Undo();
        Assert.Equal([1], entry.Data);
        Assert.IsType<ReplaceEmbeddedAudioCommand>(EditCommand.Create(ReplaceEmbeddedAudioCommand.Name, command.Args));
    }
}
