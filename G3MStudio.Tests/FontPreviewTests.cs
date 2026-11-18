using G3MLib.DataFile.Models;
using G3MStudio.Core.Commands;
using G3MStudio.Editor;

namespace G3MStudio.Tests;

public sealed class FontPreviewTests
{
    private static GameMakerFont Font()
    {
        var font = new GameMakerFont();
        font.Glyphs.Add(new GameMakerFont.Glyph { Character = 'A', SourceX = 10, SourceY = 20, SourceWidth = 6, SourceHeight = 8, Shift = 7, Offset = 1 });
        font.Glyphs.Add(new GameMakerFont.Glyph { Character = 'B', SourceX = 30, SourceY = 20, SourceWidth = 5, SourceHeight = 10, Shift = 6, Offset = 0 });
        return font;
    }

    [Fact]
    public void TextIsPlacedByShiftAndOffsetWithMissingCharactersSkippedAndNewLinesBelow()
    {
        using var font = Font();
        var placed = FontPreview.Layout(font, "AxB\nA", out var size);
        Assert.Equal(3, placed.Count);
        Assert.Equal(new Avalonia.Rect(10, 20, 6, 8), placed[0].Source);
        Assert.Equal(new Avalonia.Rect(1, 0, 6, 8), placed[0].Target); // offset 1, no advance for the missing x
        Assert.Equal(7, placed[1].Target.X); // after the shift of A
        Assert.Equal(new Avalonia.Point(1, 10), placed[2].Target.TopLeft); // second line is the tallest glyph (10) lower
        Assert.Equal(20, size.Height);
    }

    [Fact]
    public void ALongTextWrapsAtTheWidthAndANegativeOffsetMovesTheTextRight()
    {
        using var font = Font();
        var placed = FontPreview.Layout(font, new string('A', 20), out var size, 50);
        Assert.True(placed.Max(p => p.Target.Right) <= 50, $"{size}"); // the shift after the last glyph may reach past the width
        Assert.True(size.Height > 10);

        font.Glyphs[0].Offset = -3;
        var shifted = FontPreview.Layout(font, "AB", out var shiftedSize);
        Assert.Equal(0, shifted[0].Target.X);
        Assert.True(shifted.All(p => p.Target.X >= 0 && p.Target.Right <= shiftedSize.Width));
    }

    [Fact]
    public void TheSoundMenuIsBoundToTheSoundContext()
    {
        Assert.Equal(AppCommands.SoundTab, AppCommands.MenuContexts["menu.sound"]);
    }
}
