using Avalonia;
using Avalonia.Media.Imaging;
using G3MLib.DataFile.Models;
using G3MStudio.Editor;

namespace G3MStudio.Tests;

public sealed class TileArrangeTests
{
    [Fact]
    public async Task TheViewDrawsTheTilesInOtherColumnsAndLeavesTheTileSetAlone()
    {
        await HeadlessApp.Run(() =>
        {
#pragma warning disable CA2000 // a plain geometry holder for the test
            var set = new GameMakerBackground { GMS2TileWidth = 8, GMS2TileHeight = 8, GMS2TileColumns = 3, GMS2TileCount = 6, GMS2OutputBorderX = 0, GMS2OutputBorderY = 0 };
#pragma warning restore CA2000
            var view = TileArrange.ViewOf(set);
            view.GMS2TileColumns = 6;
            Assert.Equal(3u, set.GMS2TileColumns); // the real tile set keeps its own columns
            Assert.Equal(5, G3MStudio.Core.Workspace.TileGeometry.IndexAt(view, 45, 3));
            Assert.Equal((40, 0, 8, 8), G3MStudio.Core.Workspace.TileGeometry.RectOf(view, 5));

            using var source = new WriteableBitmap(new PixelSize(24, 16), new Vector(96, 96), Avalonia.Platform.PixelFormat.Bgra8888, Avalonia.Platform.AlphaFormat.Premul);
            var wide = TileArrange.Compose(source, set, 6);
            Assert.Equal((48d, 8d), (wide.Size.Width, wide.Size.Height));
            var narrow = TileArrange.Compose(source, set, 2);
            Assert.Equal((16d, 24d), (narrow.Size.Width, narrow.Size.Height));
            return Task.CompletedTask;
        });
    }
}
