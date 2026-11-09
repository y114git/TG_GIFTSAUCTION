using G3MLib.DataFile;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

public sealed class ItemExportTests
{
    [Fact]
    public async Task APageItemExportsAsAPngOfItsSize()
    {
        var folder = Corpus.Folder;
        GameMakerData? data = null;
        foreach (var file in Corpus.Files(folder))
        {
            data = await GameMakerIO.ReadAsync(file, cancellationToken: TestContext.Current.CancellationToken);
            if (data.Fonts?.Any(f => f.Texture is { SourceWidth: > 0 }) == true) break;
            data.Dispose();
            data = null;
        }

        Assert.False(data is null, "no game with a font that has a texture");
        using var _ = data;
        var item = data!.Fonts.First(f => f.Texture is { SourceWidth: > 0 }).Texture;
        var png = FramePicture.ItemToPng(item, "test");
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, png[..4]);
        using var image = new ImageMagick.MagickImage(png);
        Assert.Equal(item.SourceWidth, (int)image.Width);
    }
}
