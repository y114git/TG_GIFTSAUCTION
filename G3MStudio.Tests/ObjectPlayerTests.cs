using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using G3MLib.DataFile;
using G3MStudio.Editor;
using G3MStudio.Rooms;

#pragma warning disable CA2000 // windows made for a check only

namespace G3MStudio.Tests;

/// <summary>The sprite preview of the object editor plays only when asked: Play, Pause, Stop, Loop and speed; it stops when hidden and is disabled for one frame.</summary>
public sealed class ObjectPlayerTests
{
    private static readonly string[] Controls = ["ObjectPlay", "ObjectStop", "ObjectLoop", "ObjectSpeed"];

    private static T Named<T>(Avalonia.Visual view, string name) where T : Control => view.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);

    private static Task Run(int frames, Func<GameMakerData, ObjectLinksView, Window, SpritePreviewPlayer, Task> body) =>
        RunIn(frames, (workspace, view, window, player) => body(workspace.Data, view, window, player));

    private static async Task RunIn(int frames, Func<G3MStudio.Core.Workspace.Workspace, ObjectLinksView, Window, SpritePreviewPlayer, Task> body)
    {
        var file = Corpus.Files(Corpus.Folder).First();
        await HeadlessApp.Run(async () =>
        {
            using var textures = new TextureBitmaps();
            using var workspace = await OpenWorkspace(file);
            var data = workspace.Data;
            var sprite = data.Sprites.First(s => s.Textures.Count > 0);
            while (sprite.Textures.Count < frames) sprite.Textures.Add(sprite.Textures[0]);
            while (sprite.Textures.Count > frames) sprite.Textures.RemoveAt(sprite.Textures.Count - 1);
            var obj = data.GameObjects.First(o => o.Sprite is null && o.ParentId is null);
            obj.Sprite = sprite;
            var view = new ObjectLinksView(obj, workspace, _ => { }, _ => { }, textures);
            var window = new Window { Content = view, Width = 700, Height = 400 };
            window.Show();
            Dispatcher.UIThread.RunJobs();
            await Named<SpritePreviewPlayer>(view, "ObjectPlayer").FirstShown;
            try { await body(workspace, view, window, Named<SpritePreviewPlayer>(view, "ObjectPlayer")); }
            finally { window.Close(); }
        });
    }

    private static async Task<G3MStudio.Core.Workspace.Workspace> OpenWorkspace(string file)
    {
        var dir = Path.Combine(Path.GetTempPath(), "g3m-player-" + Guid.NewGuid().ToString("N"));
        var userData = new G3MStudio.Core.Workspace.UserData(Path.Combine(dir, "user"));
        return await new G3MStudio.Core.Workspace.WorkspaceService(userData, G3MStudio.Core.Workspace.RecentFiles.Load(userData)).OpenAsync(file, null, TestContext.Current.CancellationToken);
    }

    [Fact]
    public Task NothingPlaysUntilPlayIsPressedAndPauseKeepsTheFrame() => Run(3, async (_, view, _, player) =>
    {
        Assert.False(player.IsPlaying);
        Assert.Equal(0, player.Frame);
        var image = Named<Image>(view, "ObjectPreviewImage");
        var first = image.Source;
        await player.PlayAsync();
        Assert.True(player.IsPlaying);
        Named<Button>(view, "ObjectPlay").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); // the same button pauses
        Assert.False(player.IsPlaying);
        player.Step();
        Assert.Equal(0, player.Frame); // a paused player ignores a tick
        await player.PlayAsync();
        player.Step();
        player.Step();
        Assert.Equal(2, player.Frame);
        player.Pause();
        Assert.Equal(2, player.Frame);
        Assert.NotSame(first, image.Source);
    });

    [Fact]
    public Task StopGoesBackToTheFirstFrameAndItsPicture() => Run(3, async (_, view, _, player) =>
    {
        var image = Named<Image>(view, "ObjectPreviewImage");
        var first = image.Source;
        await player.PlayAsync();
        player.Step();
        Assert.Equal(1, player.Frame);
        Named<Button>(view, "ObjectStop").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.False(player.IsPlaying);
        Assert.Equal(0, player.Frame);
        Assert.Same(first, image.Source);
    });

    [Fact]
    public Task ATrimmedFrameIsShownAtItsPlaceInTheBoundingBox() => Run(3, async (data, view, _, player) =>
    {
        var sprite = data.Sprites.First(s => s.Textures.Count > 0);
        var source = sprite.Textures[0].Texture!;
        var trimmed = new G3MLib.DataFile.Models.GameMakerTexturePageItem { TexturePage = source.TexturePage, SourceX = source.SourceX, SourceY = source.SourceY, SourceWidth = source.SourceWidth, SourceHeight = source.SourceHeight };
        (trimmed.TargetX, trimmed.TargetY, trimmed.TargetWidth, trimmed.TargetHeight) = (5, 3, source.SourceWidth, source.SourceHeight);
        (trimmed.BoundingWidth, trimmed.BoundingHeight) = ((ushort)(source.SourceWidth + 5), (ushort)(source.SourceHeight + 3));
        sprite.Textures[1] = new G3MLib.DataFile.Models.GameMakerSprite.TextureEntry { Texture = trimmed };
        await player.PlayAsync();
        player.Step();
        var shown = Assert.IsAssignableFrom<Avalonia.Media.Imaging.Bitmap>(Named<Image>(view, "ObjectPreviewImage").Source);
        Assert.Equal(new Avalonia.PixelSize(source.SourceWidth + 5, source.SourceHeight + 3), shown.PixelSize);
    });

    [Fact]
    public Task TheStillPictureOfATrimmedSpriteIsTheWholeBoundingBox() => Run(1, async (data, view, _, player) =>
    {
        var sprite = data.Sprites.First(s => s.Textures.Count > 0);
        var source = sprite.Textures[0].Texture!;
        var trimmed = new G3MLib.DataFile.Models.GameMakerTexturePageItem { TexturePage = source.TexturePage, SourceX = source.SourceX, SourceY = source.SourceY, SourceWidth = source.SourceWidth, SourceHeight = source.SourceHeight };
        (trimmed.TargetX, trimmed.TargetY, trimmed.TargetWidth, trimmed.TargetHeight) = (5, 3, source.SourceWidth, source.SourceHeight);
        (trimmed.BoundingWidth, trimmed.BoundingHeight) = ((ushort)(source.SourceWidth + 5), (ushort)(source.SourceHeight + 3));
        sprite.Textures[0] = new G3MLib.DataFile.Models.GameMakerSprite.TextureEntry { Texture = trimmed };
        var image = Named<Image>(view, "ObjectPreviewImage");
        player.Bind(sprite, image);
        player.ShowFirst();
        await player.FirstShown;
        var shown = Assert.IsAssignableFrom<Avalonia.Media.Imaging.Bitmap>(image.Source);
        Assert.Equal(new Avalonia.PixelSize(source.SourceWidth + 5, source.SourceHeight + 3), shown.PixelSize);
    });

    [Fact]
    public Task LoopWrapsAroundAndWithoutItThePlaybackEndsOnTheLastFrame() => Run(3, async (_, view, _, player) =>
    {
        await player.PlayAsync();
        player.Step();
        player.Step();
        player.Step();
        Assert.Equal(0, player.Frame);
        Assert.True(player.IsPlaying);
        Named<ToggleButton>(view, "ObjectLoop").IsChecked = false;
        Assert.False(player.Loop);
        player.Step();
        player.Step();
        Assert.Equal(2, player.Frame);
        player.Step();
        Assert.False(player.IsPlaying);
        Assert.Equal(2, player.Frame);
        await player.PlayAsync(); // from the end it starts over
        Assert.True(player.IsPlaying);
        Assert.Equal(0, player.Frame);
    });

    [Fact]
    public Task TheSpeedChoiceScalesTheTimerInterval() => Run(3, async (_, view, _, player) =>
    {
        await player.PlayAsync();
        var normal = player.Interval;
        Assert.Equal(1, player.SpeedFactor);
        Named<ComboBox>(view, "ObjectSpeed").SelectedIndex = 2;
        Assert.Equal(2, player.SpeedFactor);
        Assert.Equal(normal.TotalSeconds / 2, player.Interval.TotalSeconds, 3);
        Named<ComboBox>(view, "ObjectSpeed").SelectedIndex = 0;
        Assert.Equal(normal.TotalSeconds * 2, player.Interval.TotalSeconds, 3);
    });

    [Fact]
    public Task HidingThePreviewStopsThePlayback() => Run(3, async (_, _, _, player) =>
    {
        await player.PlayAsync();
        player.Step();
        Assert.True(player.IsPlaying);
        player.IsVisible = false;
        Dispatcher.UIThread.RunJobs();
        player.Step(); // the next tick notices
        Assert.False(player.IsPlaying);
        Assert.Equal(0, player.Frame);
    });

    [Fact]
    public Task APageThatDoesNotDecodeKeepsTheFirstFrameAndPlayTriesAgain() => Run(3, async (data, view, _, player) =>
    {
        var item = data.GameObjects.First(o => o.Sprite is { } s && s.Textures.Count == 3).Sprite!.Textures[0].Texture!;
        var page = item.TexturePage;
        var broken = new byte[24];
        (broken[19], broken[23]) = (4, 4); // a PNG header of a 4 x 4 picture with no pixels
        item.TexturePage = new G3MLib.DataFile.Models.GameMakerEmbeddedTexture { TextureData = new() { Image = G3MLib.DataFile.Util.GMImage.FromPng(broken) } };
        var image = Named<Image>(view, "ObjectPreviewImage");
        var first = image.Source;

        await player.PlayAsync();
        Assert.False(player.IsPlaying);
        Assert.Same(first, image.Source);
        Assert.True(Named<Button>(view, "ObjectPlay").IsEnabled);

        item.TexturePage = page;
        await player.PlayAsync();
        Assert.True(player.IsPlaying);
    });

    [Fact]
    public Task AnEditOfTheSpritePixelsShowsThePreviewAgain() => RunIn(3, async (workspace, view, _, player) =>
    {
        var sprite = workspace.Data.GameObjects.First(o => o.Sprite is { } s && s.Textures.Count == 3).Sprite!.Name.Content!;
        var image = Named<Image>(view, "ObjectPreviewImage");
        var red = G3MStudio.Core.Workspace.ImageOp.Format(new G3MStudio.Core.Workspace.Rgba(255, 0, 0, 255));
        workspace.Edit(new G3MStudio.Core.Workspace.ImageEditCommand(sprite, 0, null, [new G3MStudio.Core.Workspace.ImageOp(G3MStudio.Core.Workspace.ImageOps.Fill, red, G3MStudio.Core.Workspace.ImageOp.Format([(0, 0)]), "0", "1")]));
        Dispatcher.UIThread.RunJobs();
        Assert.NotSame(image, Named<Image>(view, "ObjectPreviewImage"));
        await player.PlayAsync();
        Assert.True(player.IsPlaying);
    });

    [Fact]
    public Task PlayingAgainFromTheEndShowsTheFirstFramesPicture() => Run(3, async (_, view, _, player) =>
    {
        Named<ToggleButton>(view, "ObjectLoop").IsChecked = false;
        await player.PlayAsync();
        player.Step();
        player.Step();
        player.Step(); // the playback ends on the last frame
        var image = Named<Image>(view, "ObjectPreviewImage");
        var last = image.Source;
        await player.PlayAsync();
        Assert.Equal(0, player.Frame);
        Assert.NotSame(last, image.Source);
    });

    [Fact]
    public Task AStopWhileThePagesLoadKeepsThePlayerStopped() => Run(3, async (_, _, _, player) =>
    {
        var playing = player.PlayAsync();
        player.Stop();
        await playing;
        Assert.False(player.IsPlaying);
    });

    [Fact]
    public Task ThePreviewIsSquareFromTheFirstLayout() => Run(3, (_, view, window, _) =>
    {
        window.UpdateLayout();
        var preview = Named<Border>(view, "ObjectPreview");
        Assert.False(double.IsNaN(preview.Height));
        Assert.Equal(preview.Bounds.Width, preview.Bounds.Height, 1);
        return Task.CompletedTask;
    });

    [Fact]
    public Task ASpriteWithOneFrameDisablesTheControlsWithAReason() => Run(1, (_, view, _, player) =>
    {
        foreach (var name in Controls)
        {
            var control = Named<Control>(view, name);
            Assert.False(control.IsEnabled);
            Assert.False(string.IsNullOrEmpty(ToolTip.GetTip(control) as string));
        }

        Assert.False(player.IsPlaying);
        return Task.CompletedTask;
    });
}
