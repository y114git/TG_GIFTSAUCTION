using G3MStudio.Core.Workspace;
using ImageMagick;
using Avalonia.Threading;

namespace G3MStudio.Tests;

public sealed class FileDropTests
{
    [Fact]
    public async Task APictureOnASpriteTabReplacesAFrameAndElsewhereMakesASprite()
    {
        var corpus = Environment.GetEnvironmentVariable("G3MSTUDIO_CORPUS_DIR");
        Assert.SkipWhen(string.IsNullOrEmpty(corpus) || !Directory.Exists(corpus), "G3MSTUDIO_CORPUS_DIR is not set");
        var file = Directory.EnumerateFiles(corpus, "data.win", SearchOption.AllDirectories).OrderBy(f => new FileInfo(f).Length).First();
        using var dir = new TempDir();
        var picture = dir.File("drop.png");
        using (var image = new MagickImage(MagickColors.Blue, 16, 16)) await image.WriteAsync(picture, MagickFormat.Png, TestContext.Current.CancellationToken);
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                window.Show();
                await window.OpenAsync(file);
                var workspace = window.Workspaces.Current!;
                var sprites = workspace.Data.Sprites.Count;
                var name = workspace.Data.Sprites.First(s => s.Textures.Count > 0 && s.Textures[0].Texture is not null).Name.Content!;
                window.Docking.OpenTab("resource", ResourceTypes.KeyOf("sprites", name), false);
                for (var i = 0; i < 10; i++) { await Task.Delay(100); Dispatcher.UIThread.RunJobs(); }

                var version = workspace.History.Version;
                window.OpenDropped([picture]);
                for (var i = 0; i < 50 && workspace.History.Version == version; i++) { await Task.Delay(100); Dispatcher.UIThread.RunJobs(); }
                Assert.NotEqual(version, workspace.History.Version);
                Assert.Equal(sprites, workspace.Data.Sprites.Count); // the frame was replaced, no sprite was added

                window.Docking.CloseTab(window.Docking.ActiveTabId!);
                window.OpenDropped([picture]);
                Assert.Equal(sprites + 1, workspace.Data.Sprites.Count);
            }
            finally { window.Close(); }
        });
    }
}
