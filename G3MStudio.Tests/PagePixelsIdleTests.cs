using G3MLib.DataFile.Util;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

/// <summary>Pages the image editor keeps as raw pixels go back to their format once idle or past the cap, with the same pixels, and undo still works after it.</summary>
public sealed class PagePixelsIdleTests
{
    private static readonly Rgba Red = new(255, 0, 0, 255);

    [Fact]
    public void AnIdlePageIsEncodedBackWithTheSamePixelsAndAnEditStillUndoes()
    {
        using var data = CopyResourcesTests.Source();
        var history = new EditHistory(data);
        var item = data.Sprites.First(s => s.Name.Content == "spr_a").Textures[0].Texture!;
        var page = item.TexturePage;
        Assert.Empty(PagePixels.Idle(data, TimeSpan.Zero, 0)); // nothing raw yet

        var edit = new ImageEditCommand("spr_a", 0, null, [new ImageOp(ImageOps.Fill, ImageOp.Format(Red), ImageOp.Format([(0, 0)]), "0", "1")]);
        history.Execute(edit);
        Assert.Equal(GMImage.ImageFormat.RawBgra, page.TextureData.Image.Format);
        Assert.Empty(PagePixels.Idle(data, TimeSpan.FromHours(1), PagePixels.RawCap)); // just written, under the cap

        var idle = Assert.Single(PagePixels.Idle(data, TimeSpan.Zero, PagePixels.RawCap));
        var before = FramePicture.Read(data, "spr_a", 0).Data;
        Assert.True(PagePixels.PutBack(idle, PagePixels.Encode(idle)));
        Assert.NotEqual(GMImage.ImageFormat.RawBgra, page.TextureData.Image.Format);
        Assert.Equal(before, FramePicture.Read(data, "spr_a", 0).Data);
        Assert.Empty(PagePixels.Idle(data, TimeSpan.Zero, 0)); // encoded pages are not raw any more

        history.Undo();
        Assert.Equal(Rgba.Clear, FramePicture.Read(data, "spr_a", 0).Get(0, 0));
        history.Redo();
        Assert.Equal(Red, FramePicture.Read(data, "spr_a", 0).Get(0, 0));
    }

    [Fact]
    public async Task APageDecodedForReadingIsDroppedWhenItsFileCloses()
    {
        using var dir = new TempDir();
        using (var source = CopyResourcesTests.Source()) TestData.Write(dir.File("data.win"), source);
        var userData = new UserData(dir.File("user"));
        var workspace = await new WorkspaceService(userData, RecentFiles.Load(userData)).OpenAsync(dir.File("data.win"), cancellationToken: TestContext.Current.CancellationToken);
        var stored = workspace.Data.EmbeddedTextures[0].TextureData.Image;
        Assert.NotEqual(GMImage.ImageFormat.RawBgra, stored.Format);
        PagePixels.Read(workspace.Data.EmbeddedTextures[0], 0, 0, 1, 1);
        Assert.True(PagePixels.IsDecoded(stored));
        workspace.Dispose();
        Assert.False(PagePixels.IsDecoded(stored));
    }

    [Fact]
    public void AMaskReadsThePageThroughTheSharedDecodeCache()
    {
        using var data = CopyResourcesTests.Source();
        var sprite = data.Sprites.First(s => s.Name.Content == "spr_a");
        var stored = sprite.Textures[0].Texture!.TexturePage.TextureData.Image;
        Assert.NotEqual(GMImage.ImageFormat.RawBgra, stored.Format);
        SpriteSolids.Of(sprite, 0);
        Assert.True(PagePixels.IsDecoded(stored));
    }

    [Fact]
    public void PastTheCapTheOldestPageGoesFirstAndAPageWrittenSinceIsNotSwapped()
    {
        using var data = CopyResourcesTests.Source();
        var (first, second) = (data.EmbeddedTextures[0], data.EmbeddedTextures[1]);
        var dot = new PixelBuffer(1, 1);
        dot.Set(0, 0, Red);
        PagePixels.Write(first, [(0, 0, dot)]);
        PagePixels.Write(second, [(0, 0, dot)]);
        static long Bytes(G3MLib.DataFile.Models.GameMakerEmbeddedTexture p) => (long)p.TextureData.Image.Width * p.TextureData.Image.Height * 4;

        var picked = Assert.Single(PagePixels.Idle(data, TimeSpan.FromHours(1), Bytes(first) + Bytes(second) - 1));
        Assert.Same(first, picked.Page);
        Assert.Equal(2, PagePixels.Idle(data, TimeSpan.FromHours(1), 0).Count);

        var encoded = PagePixels.Encode(picked);
        PagePixels.Write(first, [(1, 1, dot)]); // an edit while the encode ran
        Assert.False(PagePixels.PutBack(picked, encoded));
        Assert.Equal(GMImage.ImageFormat.RawBgra, first.TextureData.Image.Format);
        Assert.Equal(Red, PagePixels.Read(first, 1, 1, 1, 1).Get(0, 0));
    }

    [Fact]
    public async Task TheTimedSwapWaitsForABackgroundReaderWithoutHoldingTheUiThread()
    {
        using var dir = new TempDir();
        var path = dir.File("data.win");
        using (var source = CopyResourcesTests.Source()) TestData.Write(path, source);
        var userData = new UserData(dir.File("user"));
        using var workspace = await new WorkspaceService(userData, RecentFiles.Load(userData)).OpenAsync(path, cancellationToken: TestContext.Current.CancellationToken);
        var page = workspace.Data.EmbeddedTextures[0];
        var dot = new PixelBuffer(1, 1);
        dot.Set(0, 0, Red);
        PagePixels.Write(page, [(0, 0, dot)]);
        await HeadlessApp.Run(async () =>
        {
            using var held = new ManualResetEventSlim();
            var reader = Task.Run(() =>
            {
                using (workspace.Lock.Read())
                {
                    held.Set();
                    Thread.Sleep(1500); // a long search or export
                }
            });
            held.Wait();
            var encoding = MainWindow.EncodeIdlePagesAsync(workspace, TimeSpan.Zero, 0);
            var slowest = TimeSpan.Zero;
            while (!encoding.IsCompleted)
            {
                // A pass of the dispatcher, then a short wait: a swap that blocks the UI thread makes one of them last as long as the reader.
                var pass = System.Diagnostics.Stopwatch.StartNew();
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                await Task.Delay(10);
                slowest = pass.Elapsed > slowest ? pass.Elapsed : slowest;
            }

            await encoding;
            await reader;
            Assert.True(slowest < TimeSpan.FromMilliseconds(1000), $"the UI thread was held for {slowest.TotalMilliseconds:0} ms");
            Assert.NotEqual(GMImage.ImageFormat.RawBgra, page.TextureData.Image.Format);
        });
    }
}
