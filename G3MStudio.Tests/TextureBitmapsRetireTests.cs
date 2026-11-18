using G3MStudio.Rooms;

namespace G3MStudio.Tests;

public sealed class TextureBitmapsRetireTests
{
    [Fact]
    public async Task AReplacedPageBitmapStaysUsableWhileAViewMayStillDrawItAndGoesWithTheStore()
    {
        using var data = CopyResourcesTests.Source();
        var page = data.EmbeddedTextures[0];
        await HeadlessApp.Run(async () =>
        {
            using var textures = new TextureBitmaps();
            var first = (await textures.GetAsync(page))!;
            textures.Forget(page);
            var second = (await textures.GetAsync(page))!;
            Assert.True(first.PixelSize.Width > 0); // retired once: a view may still show it
            textures.Forget(page);
            var third = (await textures.GetAsync(page))!;
            Assert.True(first.PixelSize.Width > 0); // retired twice: a view that has not refreshed yet can still draw it
            Assert.True(second.PixelSize.Width > 0);
            Assert.True(third.PixelSize.Width > 0);
            textures.Dispose();
            Assert.Throws<ObjectDisposedException>(() => third.PixelSize);
        });
    }
}
