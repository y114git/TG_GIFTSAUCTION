using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace G3MStudio.Tests;

/// <summary>Closing or replacing a file frees the decoded texture pages, but never while a view still draws one (an ObjectDisposedException in the compositor).</summary>
public sealed class TextureReleaseWhileShownTests
{
    private static async Task Pump()
    {
        for (var i = 0; i < 8; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick(2);
            await Task.Delay(20, TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task ABitmapReleasedWhileAnImageStillShowsItIsFreedOnlyAfterTheRenderPass()
    {
        using var dir = new TempDir();
        await HeadlessApp.Run(async () =>
        {
            using var probe = new SweepProbe();
            _ = Dispatcher.UIThread;
            var bitmap = new WriteableBitmap(new Avalonia.PixelSize(8, 8), new Avalonia.Vector(96, 96), Avalonia.Platform.PixelFormat.Bgra8888, Avalonia.Platform.AlphaFormat.Premul);
            var image = new Image { Source = bitmap, Width = 8, Height = 8 };
            var window = new Window { Content = image, Width = 40, Height = 40 };
            window.Show();
            try
            {
                await Pump();
                var order = new List<string>();
                G3MStudio.Rooms.DeferredDispose.Release(bitmap);
                Dispatcher.UIThread.Post(() => { order.Add("render"); image.InvalidateVisual(); }, DispatcherPriority.Render);
                Dispatcher.UIThread.Post(() => order.Add(IsDisposed(bitmap) ? "freed" : "alive"), DispatcherPriority.Render);
                AvaloniaHeadlessPlatform.ForceRenderTimerTick(3);
                Dispatcher.UIThread.RunJobs(DispatcherPriority.Render); // the pass the image is drawn in: the bitmap must still be there
                Assert.Equal(["render", "alive"], order);
                Assert.Empty(probe.Errors);

                image.Source = null; // the view lets go, then the release runs
                Dispatcher.UIThread.RunJobs();
                Assert.True(IsDisposed(bitmap));
                Assert.Empty(probe.Errors);
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static bool IsDisposed(Bitmap bitmap)
    {
        try { _ = bitmap.PixelSize; return false; }
        catch (ObjectDisposedException) { return true; }
    }

    [Theory]
    [InlineData("file.close")]
    [InlineData("reopen")]
    public async Task ATexturePageTabShownWhileTheFileIsClosedOrReplacedRendersWithoutAnErrorAndTheBitmapIsFreedAfterwards(string how)
    {
        using var dir = new TempDir();
        string path;
        using (var data = CopyResourcesTests.Source()) path = TestData.Write(dir.File("data.win"), data);
        await HeadlessApp.Run(async () =>
        {
            using var probe = new SweepProbe();
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                await window.OpenAsync(path);
                var page = window.Workspaces.Current!.Data.EmbeddedTextures[0].Name.Content!;
                window.Docking.OpenTab("resource", "embeddedTextures:" + page, false);
                await Pump();
                var preview = window.GetVisualDescendants().OfType<G3MStudio.Editor.TexturePagePreview>().Single();
                await preview.Ready;
                var image = preview.GetVisualDescendants().OfType<Image>().First(i => i.Name == "PageImage");
                var bitmap = Assert.IsAssignableFrom<Bitmap>(image.Source);
                Assert.True(bitmap.PixelSize.Width > 0);

                if (how == "file.close") Assert.True(window.Registry.Execute("file.close"));
                else await window.OpenAsync(path);
                await Pump();

                Assert.Empty(probe.Errors);
                Assert.Throws<ObjectDisposedException>(() => bitmap.PixelSize); // memory is still freed, after the render passes
            }
            finally
            {
                window.Close();
            }
        });
    }
}
