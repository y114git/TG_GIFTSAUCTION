using G3MLib.DataFile;

namespace G3MStudio.Tests;

public sealed class GlyphPickTests
{
    [Fact]
    public async Task AClickOnTheSheetPicksAGlyphThatHoldsThePixel()
    {
        var folder = Corpus.Folder;
        foreach (var file in Directory.EnumerateFiles(folder, "data.win", SearchOption.AllDirectories))
        {
            using var data = await GameMakerIO.ReadAsync(file, cancellationToken: TestContext.Current.CancellationToken);
            var font = data.Fonts.FirstOrDefault(f => f.Glyphs.Any(g => g.SourceWidth > 0 && g.SourceHeight > 0));
            if (font is null) continue;
            await HeadlessApp.Run(() =>
            {
                var glyph = font.Glyphs.First(g => g.SourceWidth > 0 && g.SourceHeight > 0);
                var view = new Editor.FontGlyphsView(font);
                Assert.True(view.PickAt(glyph.SourceX, glyph.SourceY));
                Assert.False(view.PickAt(-1, -1));
                return Task.CompletedTask;
            });
            return;
        }

        Assert.Fail("No font with a sized glyph in the corpus.");
    }
}
