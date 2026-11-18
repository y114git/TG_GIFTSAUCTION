using G3MLib.DataFile;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

public sealed class FontKerningTests
{
    [Fact]
    public async Task APairIsAddedChangedAndRemovedAndEachStepUndoes()
    {
        var folder = Corpus.Folder;
        GameMakerData? data = null;
        foreach (var file in Corpus.Files(folder))
        {
            data = await GameMakerIO.ReadAsync(file, cancellationToken: TestContext.Current.CancellationToken);
            if (data.Fonts?.Any(f => f.Glyphs.Count > 1) == true) break;
            data.Dispose();
            data = null;
        }

        Assert.False(data is null, "no game with a font that has glyphs");
        using var _ = data;
        var font = data!.Fonts.First(f => f.Glyphs.Count > 1);
        var name = font.Name.Content!;
        var glyph = font.Glyphs[0];
        var before = glyph.Kerning.Count;
        const short next = 0x7E7E; // a character no glyph kerns against

        var add = new FontKerningCommand(name, 0, FontKerningCommand.Set, next, -3);
        add.Apply(data);
        Assert.Equal(before + 1, glyph.Kerning.Count);
        Assert.Equal(-3, glyph.Kerning.Single(k => k.Character == next).ShiftModifier);

        var change = new FontKerningCommand(name, 0, FontKerningCommand.Set, next, 5);
        change.Apply(data);
        Assert.Equal(5, glyph.Kerning.Single(k => k.Character == next).ShiftModifier);
        change.Revert(data);
        Assert.Equal(-3, glyph.Kerning.Single(k => k.Character == next).ShiftModifier);

        var remove = new FontKerningCommand(name, 0, FontKerningCommand.Remove, next);
        remove.Apply(data);
        Assert.Equal(before, glyph.Kerning.Count);
        remove.Revert(data);
        Assert.Equal(-3, glyph.Kerning.Single(k => k.Character == next).ShiftModifier);
        add.Revert(data);
        Assert.Equal(before, glyph.Kerning.Count);

        var again = (FontKerningCommand)EditCommand.Create(FontKerningCommand.Name, add.Args);
        Assert.Equal(add.Args, again.Args);
        Assert.Throws<InvalidOperationException>(() => new FontKerningCommand(name, 0, FontKerningCommand.Remove, next).Apply(data));
    }
}
