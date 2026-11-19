using G3MStudio.Rooms;

namespace G3MStudio.Tests;

public sealed class TextureBitmapsBudgetTests
{
    [Fact]
    public async Task PagesPastTheBudgetLeaveTheCacheOldestFirst()
    {
        using var data = TestData.Create(1, false);
        for (var i = 0; i < 3; i++) TestData.AddPage(data);
        var pages = data.EmbeddedTextures;
        await HeadlessApp.Run(async () =>
        {
            // Each 128 by 128 page is 64 KB of pixels; the budget holds two.
            using var textures = new TextureBitmaps(null, 2 * 128 * 128 * 4 + 1);
            var first = await textures.GetAsync(pages[0]);
            await textures.GetAsync(pages[1]);
            await textures.GetAsync(pages[2]);
            Assert.Equal(2, textures.Cached);
            Assert.Same(await textures.GetAsync(pages[2]), await textures.GetAsync(pages[2]));
            Assert.NotSame(first, await textures.GetAsync(pages[0])); // it was dropped and is decoded again
        });
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static async Task<WeakReference> DecodeThenTrim(TextureBitmaps textures, G3MLib.DataFile.Models.GameMakerEmbeddedTexture page)
    {
        var weak = new WeakReference(await textures.GetAsync(page));
        textures.Trim();
        return weak;
    }

    [Fact]
    public async Task TrimmedPagesCanBeCollectedWhenNoViewDrawsThem()
    {
        using var data = TestData.Create(1, false);
        TestData.AddPage(data);
        var page = data.EmbeddedTextures[0];
        await HeadlessApp.Run(async () =>
        {
            using var textures = new TextureBitmaps();
            var weak = await DecodeThenTrim(textures, page);
            for (var i = 0; i < 3; i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                await Task.Delay(20);
            }

            Assert.False(weak.IsAlive, "a trimmed page is still reachable");
        });
    }
}
