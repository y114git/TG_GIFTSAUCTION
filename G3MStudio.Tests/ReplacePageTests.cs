using G3MLib.DataFile;
using G3MStudio.Core.Workspace;
using ImageMagick;

namespace G3MStudio.Tests;

public sealed class ReplacePageTests
{
    [Fact]
    public async Task APageIsExportedChangedPutBackAndAWrongSizeIsRefused()
    {
        var folder = Environment.GetEnvironmentVariable("G3MSTUDIO_CORPUS_DIR");
        Assert.SkipWhen(string.IsNullOrEmpty(folder) || !Directory.Exists(folder), "G3MSTUDIO_CORPUS_DIR is not set");
        var file = Directory.EnumerateFiles(folder, "data.win", SearchOption.AllDirectories).OrderBy(f => new FileInfo(f).Length).First();
        using var data = await GameMakerIO.ReadAsync(file, cancellationToken: TestContext.Current.CancellationToken);
        var page = data.EmbeddedTextures.First(t => t.TextureData?.Image is { Width: > 8, Height: > 8 });
        var name = page.Name.Content!;
        var original = ReplacePageCommand.ReadPng(data, name);
        using (var image = new MagickImage(original)) Assert.Equal(page.TextureData!.Image.Width, (int)image.Width);

        byte[] edited;
        using (var image = new MagickImage(original))
        {
            using var dot = new MagickImage(MagickColors.Red, 1, 1);
            image.Composite(dot, 0, 0, CompositeOperator.Copy);
            edited = image.ToByteArray(MagickFormat.Png);
        }

        using (var check = new MagickImage(edited))
        {
            using var checkPixels = check.GetPixels();
            var sent = checkPixels.GetPixel(0, 0);
            Assert.True(sent.GetChannel(0) == 255 && sent.GetChannel(1) == 0, $"the edited picture itself holds {sent.GetChannel(0)},{sent.GetChannel(1)},{sent.GetChannel(2)} channels {sent.Channels}");
        }

        var command = EditCommand.Create(ReplacePageCommand.Name, new ReplacePageCommand(name, edited).Args);
        command.Apply(data);
        using (var image = new MagickImage(ReplacePageCommand.ReadPng(data, name)))
        {
            using var pixels = image.GetPixels();
            var pixel = pixels.GetPixel(0, 0);
            var got = (pixel.GetChannel(0), pixel.GetChannel(1), pixel.GetChannel(2));
            Assert.True(got == (255, 0, 0), $"{page.TextureData!.Image.Format} {page.TextureData.Image.Width}x{page.TextureData.Image.Height} {got}");
        }

        command.Revert(data);
        Assert.Equal(original.Length, ReplacePageCommand.ReadPng(data, name).Length);

        using var small = new MagickImage(MagickColors.Red, 4, 4);
        Assert.Throws<InvalidOperationException>(() => new ReplacePageCommand(name, small.ToByteArray(MagickFormat.Png)).Apply(data));
    }

    [Fact]
    public async Task ThePageMenuSavesThePictureAndReplacesItFromAFile()
    {
        var folder = Environment.GetEnvironmentVariable("G3MSTUDIO_CORPUS_DIR");
        Assert.SkipWhen(string.IsNullOrEmpty(folder) || !Directory.Exists(folder), "G3MSTUDIO_CORPUS_DIR is not set");
        var file = Directory.EnumerateFiles(folder, "data.win", SearchOption.AllDirectories).OrderBy(f => new FileInfo(f).Length).First();
        using var dir = new TempDir();
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                await window.OpenAsync(file);
                var workspace = window.Workspaces.Current!;
                var page = workspace.Data.EmbeddedTextures.First(t => t.TextureData?.Image is { Width: > 8 }).Name.Content!;
                window.Explorer.Select(ResourceTypes.KeyOf("embeddedTextures", page));
                var path = dir.File("page.png");
                window.FilePicker = (_, _) => Task.FromResult<string?>(path);
                Assert.True(window.Registry.Execute("tools.exportPage"));
                for (var i = 0; i < 100 && !File.Exists(path); i++) await Task.Delay(50);
                Assert.True(File.Exists(path));
                var before = workspace.History.Position;
                Assert.True(window.Registry.Execute("tools.replacePage"));
                for (var i = 0; i < 100 && workspace.History.Position == before; i++)
                {
                    await Task.Delay(50);
                    Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                }

                Assert.Equal(before + 1, workspace.History.Position);
            }
            finally
            {
                window.Close();
            }
        });
    }
}
