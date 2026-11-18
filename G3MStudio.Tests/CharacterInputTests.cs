#pragma warning disable CA2000 // the workspace made for a lookup only
using G3MStudio.Core.Workspace;
using G3MStudio.Editor;

namespace G3MStudio.Tests;

public sealed class CharacterInputTests
{
    [Theory]
    [InlineData("Z", 90)]
    [InlineData("9", 57)]
    [InlineData("90", 90)]
    [InlineData("U+005A", 90)]
    [InlineData("u+5a", 90)]
    [InlineData("0x5A", 90)]
    [InlineData("  Ж ", 0x416)]
    [InlineData("あ", 0x3042)]
    [InlineData("\U0001F600", 0x1F600)]
    [InlineData("U+1F600", 0x1F600)]
    public void ACharacterIsReadFromEveryForm(string text, int code) => Assert.Equal(code, CharacterInput.ParseCharacter(text));

    [Theory]
    [InlineData("")]
    [InlineData("ab")]
    [InlineData("-5")]
    [InlineData("U+110000")]
    [InlineData("U+D800")]
    [InlineData("0x")]
    [InlineData("\ud83d")]
    public void NonsenseIsNotACharacter(string text) => Assert.Null(CharacterInput.ParseCharacter(text));

    [Fact]
    public void RangesTakeEveryForm()
    {
        Assert.Equal(Enumerable.Range(65, 26), CharacterInput.ParseRange("A-Z"));
        Assert.Equal(Enumerable.Range('a', 26), CharacterInput.ParseRange("a..z"));
        Assert.Equal(Enumerable.Range(0x410, 64), CharacterInput.ParseRange("U+0410-U+044F"));
        Assert.Equal(Enumerable.Range(0x41, 3), CharacterInput.ParseRange("0x41 - 67"));
        Assert.Equal([0x1F600, 0x1F601], CharacterInput.ParseRange("\U0001F600-\U0001F601"));
        Assert.Equal([0x3042, 0x3043], CharacterInput.ParseRange("あ..ぃ"));
        Assert.Equal([45], CharacterInput.ParseRange("-"));
        Assert.Equal(Enumerable.Range(45, 82), CharacterInput.ParseRange("--~"));
        Assert.Null(CharacterInput.ParseRange("Z-A"));
        Assert.Null(CharacterInput.ParseRange("A-"));
        Assert.Null(CharacterInput.ParseRange("0-5000"));
        Assert.NotNull(CharacterInput.ParseRange("0-5000", 0x10000));
    }

    [Fact]
    public void ADescriptionNamesTheCharacterCodeAndNumber()
    {
        Assert.Equal("Z  U+005A  90", CharacterInput.Describe('Z'));
        Assert.Equal("\U0001F600  U+1F600  128512", CharacterInput.Describe(0x1F600));
        Assert.Equal("  U+0000  0", CharacterInput.Describe(0));
    }

    [Fact]
    public void DistinctCharactersCountASurrogatePairOnceAndSkipControls()
    {
        Assert.Equal(['a', 'b', 0x1F600, 0x416], CharacterInput.DistinctCharacters("aab\n\U0001F600Ж\U0001F600"));
    }

    [Fact]
    public void PresetsHoldTheirScript()
    {
        Assert.All(CharacterInput.PresetNames, n => Assert.NotEmpty(CharacterInput.Preset(n)));
        Assert.Contains(0x416, CharacterInput.Preset("Cyrillic"));
        Assert.Contains(0x3B1, CharacterInput.Preset("Greek"));
        Assert.Contains(0x3042, CharacterInput.Preset("Hiragana"));
        Assert.Contains(0x30AB, CharacterInput.Preset("Katakana"));
        Assert.Contains(0x4E2D, CharacterInput.Preset("Cjk"));
        Assert.Contains(0xAC00, CharacterInput.Preset("Hangul"));
        Assert.Contains(0xE9, CharacterInput.Preset("LatinExtended"));
        Assert.Equal(95, CharacterInput.Preset("Normal").Count);
    }

    [Fact]
    public async Task AGlyphIsAddedByTypedCharacterRangeOrText()
    {
        await HeadlessApp.Run(() =>
        {
            var workspace = ThumbnailTests.Generate();
            var font = workspace.Data.Fonts[0];
            var view = new FontGlyphsView(font, workspace);
            var before = font.Glyphs.Count;
            view.AddCodes(CharacterInput.ParseRange("Ж")!);
            Assert.Equal(before + 1, font.Glyphs.Count);
            view.AddCodes(CharacterInput.ParseRange("U+0410-U+0412")!);
            Assert.Equal(before + 4, font.Glyphs.Count);
            view.AddFromText("あいあ😀");
            Assert.Contains(font.Glyphs, g => g.Character == 0x3042);
            Assert.Contains(font.Glyphs, g => g.Character == 0x3044);
            Assert.DoesNotContain(font.Glyphs, g => g.Character > 0xFFFF);
            Assert.Equal(before + 6, font.Glyphs.Count); // two kana added; the emoji is skipped
        });
    }

    [Fact]
    public async Task ACharacterTypedIntoTheNumberFieldIsConverted()
    {
        await HeadlessApp.Run(() =>
        {
            var workspace = ThumbnailTests.Generate();
            var font = workspace.Data.Fonts[0];
            var name = font.Name.Content!;
            new FontGlyphsView(font, workspace).AddCodes([0x416]);
            string Path() => $"Glyphs[{font.Glyphs.FindIndex(g => g.Character is 0x416 or 0x2603 or 92)}].Character";
            workspace.Edit(new SetPropertyCommand("fonts", name, Path(), "☃"));
            Assert.Contains(font.Glyphs, g => g.Character == 0x2603);
            workspace.Edit(new SetPropertyCommand("fonts", name, Path(), "92"));
            Assert.Contains(font.Glyphs, g => g.Character == 92);
            Assert.Throws<InvalidOperationException>(() => workspace.Edit(new SetPropertyCommand("fonts", name, Path(), "nonsense")));
        });
    }
}
