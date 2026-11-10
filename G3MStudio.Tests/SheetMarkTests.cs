using Avalonia.Input;
using G3MStudio.Editor;
using G3MStudio.Rooms;

namespace G3MStudio.Tests;

public sealed class SheetMarkTests
{
    [Fact]
    public async Task ArrowKeysMoveTheOutlineAndShiftResizesItWithOneReportPerKey()
    {
        await HeadlessApp.Run(() =>
        {
            using var textures = new TextureBitmaps();
            var sheet = new TextureItemView(null, textures) { MarkEditable = true };
            var seen = new List<(int X, int Y, int W, int H)>();
            sheet.MarkEdited += seen.Add;
            Assert.False(sheet.NudgeMark(Key.Right, false)); // nothing outlined yet
            sheet.Highlight((5, 5, 10, 10));
            Assert.True(sheet.NudgeMark(Key.Right, false));
            Assert.True(sheet.NudgeMark(Key.Down, true));
            Assert.False(sheet.NudgeMark(Key.A, false));
            Assert.Equal([(6, 5, 10, 10), (6, 5, 10, 11)], seen);
            sheet.MarkEditable = false;
            Assert.False(sheet.NudgeMark(Key.Left, false));
            return Task.CompletedTask;
        });
    }
}
