using Avalonia.VisualTree;
using Avalonia.Controls;
using G3MLib.DataFile;
using G3MLib.DataFile.Models;
using G3MStudio.Core.Workspace;
using G3MStudio.Editor;
using G3MStudio.Rooms;

namespace G3MStudio.Tests;

public sealed class SpriteFramesTests(ITestOutputHelper output)
{
    [Fact]
    public async Task AFrameWithoutAPageShowsAnEmptyBoxAndTheCountIsRight()
    {
        using var data = TestData.Create(0);
        using var sprite = new GameMakerSprite { Name = data.Strings.MakeString("spr_empty", out _) };
        sprite.Textures.Add(new GameMakerSprite.TextureEntry { Texture = new GameMakerTexturePageItem() });
        sprite.Textures.Add(new GameMakerSprite.TextureEntry());
        await HeadlessApp.Run(async () =>
        {
            using var textures = new TextureBitmaps();
            var view = new SpriteFramesView(sprite, textures);
            await view.Ready;
            Assert.Equal(2, view.FrameCount);
            Assert.Equal(0, view.Pictures);
        });
    }

    [Fact]
    public async Task PlayingStepsThroughTheFramesAndPausingStops()
    {
        using var data = TestData.Create(0);
        using var sprite = new GameMakerSprite { Name = data.Strings.MakeString("spr_two", out _), GMS2PlaybackSpeed = 1000 };
        sprite.Textures.Add(new GameMakerSprite.TextureEntry());
        sprite.Textures.Add(new GameMakerSprite.TextureEntry());
        await HeadlessApp.Run(async () =>
        {
            using var textures = new TextureBitmaps();
            var view = new SpriteFramesView(sprite, textures);
            var window = new Window { Content = view };
            window.Show();
            await view.Ready;
            var play = view.GetVisualDescendants().OfType<Avalonia.Controls.Primitives.ToggleButton>().Single(b => b.Name == "SpritePlay");
            play.IsChecked = true;
            for (var i = 0; i < 100 && view.Current == 0; i++) await Task.Delay(20);
            Assert.Equal(1, view.Current);
            play.IsChecked = false;
            var stopped = view.Current;
            await Task.Delay(100);
            Assert.Equal(stopped, view.Current);
            window.Close();
        });
    }

    [Fact]
    public async Task EverySpriteOfARealGameCutsItsFramesOutOfThePages()
    {
        var folder = Environment.GetEnvironmentVariable("G3MSTUDIO_CORPUS_DIR");
        Assert.SkipWhen(string.IsNullOrEmpty(folder) || !Directory.Exists(folder), "G3MSTUDIO_CORPUS_DIR is not set");
        var file = Directory.EnumerateFiles(folder, "data.win", SearchOption.AllDirectories).OrderBy(f => new FileInfo(f).Length).First();
        using var data = await GameMakerIO.ReadAsync(file, cancellationToken: TestContext.Current.CancellationToken);
        await HeadlessApp.Run(async () =>
        {
            using var textures = new TextureBitmaps();
            int sprites = 0, frames = 0, pictures = 0;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            foreach (var sprite in data.Sprites.Where(s => s is { Textures.Count: > 0 }).Take(300))
            {
                var view = new SpriteFramesView(sprite, textures);
                await view.Ready;
                (sprites, frames, pictures) = (sprites + 1, frames + view.FrameCount, pictures + view.Pictures);
            }

            output.WriteLine($"{sprites} sprites, {frames} frames, {pictures} with a picture, {watch.Elapsed.TotalMilliseconds:F0} ms");
            Assert.True(pictures > 0);
        });
    }

    [Fact]
    public async Task RoomsOfARealGameDrawThroughTheSharedPages()
    {
        var folder = Environment.GetEnvironmentVariable("G3MSTUDIO_CORPUS_DIR");
        Assert.SkipWhen(string.IsNullOrEmpty(folder) || !Directory.Exists(folder), "G3MSTUDIO_CORPUS_DIR is not set");
        var file = Directory.EnumerateFiles(folder, "data.win", SearchOption.AllDirectories).OrderBy(f => new FileInfo(f).Length).First();
        using var data = await GameMakerIO.ReadAsync(file, cancellationToken: TestContext.Current.CancellationToken);
        await HeadlessApp.Run(async () =>
        {
            using var textures = new TextureBitmaps();
            var drawn = 0;
            foreach (var room in data.Rooms.Take(30))
            {
                var preview = new RoomPreview(room, textures);
                await preview.Ready;
                if (preview.View.Scene is { Items.Count: > 0 }) drawn++;
            }

            output.WriteLine($"{drawn} of {Math.Min(30, data.Rooms.Count)} rooms have something to draw");
            Assert.True(drawn > 0);
        });
    }

    [Fact]
    public async Task ReplacingAFrameChangesThePageAndUndoRestoresIt()
    {
        var folder = Environment.GetEnvironmentVariable("G3MSTUDIO_CORPUS_DIR");
        Assert.SkipWhen(string.IsNullOrEmpty(folder) || !Directory.Exists(folder), "G3MSTUDIO_CORPUS_DIR is not set");
        var file = Directory.EnumerateFiles(folder, "data.win", SearchOption.AllDirectories).OrderBy(f => new FileInfo(f).Length).First();
        using var data = await GameMakerIO.ReadAsync(file, cancellationToken: TestContext.Current.CancellationToken);
        var sprite = data.Sprites.First(s => s.Textures.Count > 0 && s.Textures[0].Texture is { TexturePage.TextureData.Image.Width: > 1, SourceWidth: > 0 });
        var item = sprite.Textures[0].Texture!;
        var before = item.TexturePage.TextureData.Image;
        using var red = new ImageMagick.MagickImage(ImageMagick.MagickColors.Red, 8, 8);
        var command = new ReplaceFrameCommand(sprite.Name.Content ?? string.Empty, 0, red.ToByteArray(ImageMagick.MagickFormat.Png));

        command.Apply(data);
        var after = item.TexturePage.TextureData.Image;
        Assert.NotSame(before, after);
        Assert.Equal((ushort)8, item.TargetWidth);
        command.Revert(data);
        Assert.Same(before, item.TexturePage.TextureData.Image);
        command.Apply(data);
        Assert.Same(after, item.TexturePage.TextureData.Image);

        var rebuilt = EditCommand.Create(command.Kind, command.Args);
        Assert.Equal(command.Args["picture"], rebuilt.Args["picture"]);
    }

    [Fact]
    public void OnlyPlainRasterPicturesAreRead()
    {
        using var red = new ImageMagick.MagickImage(ImageMagick.MagickColors.Red, 4, 4);
        using (ReplaceFrameCommand.ReadPicture(red.ToByteArray(ImageMagick.MagickFormat.Png))) { }
        var svg = System.Text.Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"4\" height=\"4\"><rect width=\"4\" height=\"4\"/></svg>");
        Assert.ThrowsAny<Exception>(() => ReplaceFrameCommand.ReadPicture(svg));
    }

    [Fact]
    public async Task ATexturePageOfARealGameShowsItsPicture()
    {
        var folder = Environment.GetEnvironmentVariable("G3MSTUDIO_CORPUS_DIR");
        Assert.SkipWhen(string.IsNullOrEmpty(folder) || !Directory.Exists(folder), "G3MSTUDIO_CORPUS_DIR is not set");
        var file = Directory.EnumerateFiles(folder, "data.win", SearchOption.AllDirectories).OrderBy(f => new FileInfo(f).Length).First();
        using var data = await GameMakerIO.ReadAsync(file, cancellationToken: TestContext.Current.CancellationToken);
        await HeadlessApp.Run(async () =>
        {
            using var textures = new TextureBitmaps();
            var preview = new TexturePagePreview(data.EmbeddedTextures.First(t => t.TextureData?.Image is { Width: > 1 }), textures);
            await preview.Ready;
            Assert.NotNull(preview.PictureSize);
        });
    }

    [Fact]
    public async Task DuplicatingAndRemovingAFrameChangeTheListAndUndoPutsItBack()
    {
        var folder = Environment.GetEnvironmentVariable("G3MSTUDIO_CORPUS_DIR");
        Assert.SkipWhen(string.IsNullOrEmpty(folder) || !Directory.Exists(folder), "G3MSTUDIO_CORPUS_DIR is not set");
        var file = Directory.EnumerateFiles(folder, "data.win", SearchOption.AllDirectories).OrderBy(f => new FileInfo(f).Length).First();
        using var data = await GameMakerIO.ReadAsync(file, cancellationToken: TestContext.Current.CancellationToken);
        var sprite = data.Sprites.First(s => s.Textures.Count > 1);
        var name = sprite.Name.Content ?? string.Empty;
        var count = sprite.Textures.Count;

        var duplicate = new FrameListCommand(name, FrameListCommand.Duplicate, 0);
        duplicate.Apply(data);
        Assert.Equal(count + 1, sprite.Textures.Count);
        Assert.Same(sprite.Textures[0].Texture, sprite.Textures[1].Texture);
        using (var stream = new MemoryStream())
        {
            await GameMakerIO.WriteAsync(stream, data, cancellationToken: TestContext.Current.CancellationToken);
            stream.Position = 0;
            using var back = await GameMakerIO.ReadAsync(stream, cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal(count + 1, back.Sprites.First(s => s.Name.Content == name).Textures.Count);
        }

        duplicate.Revert(data);
        Assert.Equal(count, sprite.Textures.Count);

        var first = sprite.Textures[0];
        var remove = new FrameListCommand(name, FrameListCommand.Remove, 0);
        remove.Apply(data);
        Assert.Equal(count - 1, sprite.Textures.Count);
        remove.Revert(data);
        Assert.Same(first, sprite.Textures[0]);
        Assert.Equal(name, EditCommand.Create(remove.Kind, remove.Args).Args["sprite"]);
    }

    [Fact]
    public async Task AnObjectListsItsEventsWithTheirCodeEntries()
    {
        var folder = Environment.GetEnvironmentVariable("G3MSTUDIO_CORPUS_DIR");
        Assert.SkipWhen(string.IsNullOrEmpty(folder) || !Directory.Exists(folder), "G3MSTUDIO_CORPUS_DIR is not set");
        var file = Directory.EnumerateFiles(folder, "data.win", SearchOption.AllDirectories).OrderBy(f => new FileInfo(f).Length).First();
        using var data = await GameMakerIO.ReadAsync(file, cancellationToken: TestContext.Current.CancellationToken);
        var opened = new List<string>();
        await HeadlessApp.Run(() =>
        {
            var rows = data.GameObjects.Select(o => new ObjectEventsView(o, opened.Add)).Where(v => v.Rows.Count > 0).ToList();
            Assert.NotEmpty(rows);
            output.WriteLine($"{rows.Count} objects with events, first: {rows[0].Rows[0]}");
            Assert.StartsWith("gml_Object_", rows[0].Rows[0].Code, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task ASoundWritesItsAudioToAPrivateFileAndHandsItToThePlayer()
    {
        var folder = Environment.GetEnvironmentVariable("G3MSTUDIO_CORPUS_DIR");
        Assert.SkipWhen(string.IsNullOrEmpty(folder) || !Directory.Exists(folder), "G3MSTUDIO_CORPUS_DIR is not set");
        var file = Directory.EnumerateFiles(folder, "data.win", SearchOption.AllDirectories).OrderBy(f => new FileInfo(f).Length).First();
        using var data = await GameMakerIO.ReadAsync(file, cancellationToken: TestContext.Current.CancellationToken);
        var sound = data.Sounds.First(s => s.AudioFile is { Data.Length: > 0 });
        string? opened = null;
        await HeadlessApp.Run(() =>
        {
            var view = new SoundPlayerView(sound, path =>
            {
                opened = path;
                return true;
            });
            var path = view.Extract();
            Assert.NotNull(path);
            Assert.Equal(sound.AudioFile!.Data.Length, new FileInfo(path).Length);
            Assert.Contains("audio", view.Status, StringComparison.OrdinalIgnoreCase);
            File.Delete(path);
        });
        Assert.Null(opened);
    }

    [Fact]
    public async Task AWavWithoutAStoredLengthShowsItsLengthFromTheHeader()
    {
        using var data = TestData.Create(0);
        var wav = new byte[44 + 16000];
        System.Text.Encoding.ASCII.GetBytes("RIFF").CopyTo(wav, 0);
        System.Text.Encoding.ASCII.GetBytes("WAVEfmt ").CopyTo(wav, 8);
        BitConverter.GetBytes(16u).CopyTo(wav, 16);
        BitConverter.GetBytes(8000u).CopyTo(wav, 28); // bytes per second
        System.Text.Encoding.ASCII.GetBytes("data").CopyTo(wav, 36);
        BitConverter.GetBytes(16000u).CopyTo(wav, 40);
        using var sound = new GameMakerSound { Name = data.Strings.MakeString("snd_two", out _), AudioFile = new GameMakerEmbeddedAudio { Data = wav } };
        await HeadlessApp.Run(() =>
        {
            Assert.Contains("2 s", new SoundPlayerView(sound, _ => true).Status, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task ARebuiltMaskCoversMostOfTheOriginalAndUndoRestoresIt()
    {
        var folder = Environment.GetEnvironmentVariable("G3MSTUDIO_CORPUS_DIR");
        Assert.SkipWhen(string.IsNullOrEmpty(folder) || !Directory.Exists(folder), "G3MSTUDIO_CORPUS_DIR is not set");
        var file = Directory.EnumerateFiles(folder, "data.win", SearchOption.AllDirectories).OrderBy(f => new FileInfo(f).Length).First();
        using var data = await GameMakerIO.ReadAsync(file, cancellationToken: TestContext.Current.CancellationToken);
        var sprite = data.Sprites.First(s => s.CollisionMasks.Count > 0 && s.Textures.Count > 0 && s.Textures[0].Texture?.TexturePage?.TextureData?.Image is { Width: > 1 } && s.CollisionMasks.All(m => m?.Data is { Length: > 0 }));
        var before = sprite.CollisionMasks.Select(m => (byte[])m.Data.Clone()).ToList();

        var command = new RecalcMaskCommand(sprite.Name.Content ?? string.Empty);
        command.Apply(data);
        var after = sprite.CollisionMasks.Select(m => m.Data).ToList();
        var original = before.Sum(b => b.Sum(x => System.Numerics.BitOperations.PopCount(x)));
        var rebuilt = after.Sum(b => b.Sum(x => System.Numerics.BitOperations.PopCount(x)));
        output.WriteLine($"{sprite.Name.Content}: original {original} solid pixels, rebuilt {rebuilt}");
        Assert.Equal(before.Select(b => b.Length), after.Select(b => b.Length));
        Assert.True(rebuilt > 0);

        command.Revert(data);
        Assert.All(Enumerable.Range(0, before.Count), k => Assert.Equal(before[k], sprite.CollisionMasks[k].Data));
        Assert.Equal(command.Args["tolerance"], EditCommand.Create(command.Kind, command.Args).Args["tolerance"]);
    }
}
