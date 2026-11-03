using Avalonia.VisualTree;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

public sealed class PixelBufferTests
{
    private static readonly Rgba Red = new(255, 0, 0, 255);

    [Fact]
    public void ColorsParseFromHexWithOrWithoutAlpha()
    {
        Assert.Equal(new Rgba(1, 2, 3, 255), Rgba.Parse("#010203"));
        Assert.Equal(new Rgba(1, 2, 3, 128), Rgba.Parse("#01020380"));
        Assert.Null(Rgba.Parse("010203"));
        Assert.Null(Rgba.Parse("#12345"));
        Assert.Equal("#010203", new Rgba(1, 2, 3, 255).ToString());
    }

    [Fact]
    public void ALineHasNoGapsAndAStampStaysInside()
    {
        var picture = new PixelBuffer(10, 6);
        picture.Line(0, 0, 9, 5, 1, Red);
        for (var x = 0; x < 10; x++) Assert.Contains(Enumerable.Range(0, 6), y => picture.Get(x, y) == Red);
        picture.Stamp(9, 5, 3, Red); // half of it is outside: no exception, nothing wraps to the next row
        Assert.Equal(Rgba.Clear, picture.Get(0, 5));
    }

    [Fact]
    public void FillStopsAtADifferentColorAndAnEraseClears()
    {
        var picture = new PixelBuffer(5, 5);
        picture.Rectangle(0, 2, 4, 2, Red, true);
        picture.Fill(0, 0, new Rgba(0, 0, 255, 255));
        Assert.Equal(new Rgba(0, 0, 255, 255), picture.Get(4, 1));
        Assert.Equal(Rgba.Clear, picture.Get(0, 3)); // the other side of the red wall
        picture.Stamp(2, 2, 1, Rgba.Clear);
        Assert.Equal(Rgba.Clear, picture.Get(2, 2));
    }

    [Fact]
    public void ARectangleOutlineIsHollowAndASnapshotRestores()
    {
        var picture = new PixelBuffer(6, 6);
        var before = picture.Snapshot();
        picture.Rectangle(1, 1, 4, 4, Red, false);
        Assert.Equal(Red, picture.Get(1, 1));
        Assert.Equal(Rgba.Clear, picture.Get(2, 2));
        picture.Restore(before);
        Assert.Equal(Rgba.Clear, picture.Get(1, 1));
    }

    [Fact]
    public async Task APaintedFrameIsWrittenBackAndReadAgainPixelForPixel()
    {
        var folder = Environment.GetEnvironmentVariable("G3MSTUDIO_CORPUS_DIR");
        Assert.SkipWhen(string.IsNullOrEmpty(folder) || !Directory.Exists(folder), "G3MSTUDIO_CORPUS_DIR is not set");
        var file = Directory.EnumerateFiles(folder, "data.win", SearchOption.AllDirectories).OrderBy(f => new FileInfo(f).Length).First();
        using var data = await G3MLib.DataFile.GameMakerIO.ReadAsync(file, cancellationToken: TestContext.Current.CancellationToken);
        var sprite = data.Sprites.First(s => s.Textures.Count > 0 && s.Textures[0].Texture is { SourceWidth: > 3, SourceHeight: > 3 } t && t.SourceWidth == t.TargetWidth && t.SourceHeight == t.TargetHeight);
        var name = sprite.Name.Content!;
        var picture = FramePicture.Read(data, name, 0);
        picture.Stamp(1, 1, 2, Red);
        picture.Stamp(picture.Width - 1, picture.Height - 1, 1, new Rgba(0, 255, 0, 255));
        var command = new ReplaceFrameCommand(name, 0, FramePicture.ToPng(picture));
        command.Apply(data);
        var again = FramePicture.Read(data, name, 0);
        Assert.Equal((picture.Width, picture.Height), (again.Width, again.Height));
        Assert.Equal(Red, again.Get(1, 1));
        Assert.Equal(new Rgba(0, 255, 0, 255), again.Get(picture.Width - 1, picture.Height - 1));
        command.Revert(data);
        Assert.NotEqual(Red, FramePicture.Read(data, name, 0).Get(1, 1));
    }

    [Fact]
    public async Task ThePainterAppliesOneUndoableEditToTheSprite()
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
                var sprite = workspace.Data.Sprites.First(s => s.Textures.Count > 0 && s.Textures[0].Texture is { SourceWidth: > 3, SourceHeight: > 3 } t && t.SourceWidth == t.TargetWidth && t.SourceHeight == t.TargetHeight);
                var name = sprite.Name.Content!;
                var key = ResourceTypes.KeyOf("sprites", name);
                window.Docking.OpenTab("resource", key, false);
                for (var i = 0; i < 20; i++)
                {
                    await Task.Delay(100);
                    Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                }

                Editor.PixelPainterWindow? painter = null;
                window.PainterOpened += p => painter = p;
                Assert.True(window.Registry.Execute("edit.paintFrame"));
                for (var i = 0; i < 100 && painter is null; i++)
                {
                    await Task.Delay(50);
                    Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                }

                Assert.NotNull(painter);
                painter.Canvas.Picture.Stamp(0, 0, 1, Red);
                painter.GetVisualDescendants().OfType<Avalonia.Controls.Button>().Single(b => b.Name == "PaintApply").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
                Assert.Equal(Red, FramePicture.Read(workspace.Data, name, 0).Get(0, 0));
                workspace.History.Undo();
                Assert.NotEqual(Red, FramePicture.Read(workspace.Data, name, 0).Get(0, 0));
            }
            finally
            {
                foreach (var open in window.OwnedWindows.ToList()) open.Close();
                window.Close();
            }
        });
    }

    [Fact]
    public void AStrokeThatChangesNothingIsNotAnUndoStep()
    {
        // Filling an area that already has the color changes no pixel: the painter keeps no snapshot for it.
        var picture = new PixelBuffer(4, 4);
        var before = picture.Snapshot();
        picture.Fill(0, 0, Rgba.Clear);
        Assert.True(before.AsSpan().SequenceEqual(picture.Data));
    }

    [Fact]
    public async Task APaintedFrameThatSharesItsPictureGetsItsOwnAndTheOtherFrameStaysAsItWas()
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
                var sprite = workspace.Data.Sprites.First(x => x.Textures.Count > 0 && x.Textures[0].Texture is { SourceWidth: > 3, SourceHeight: > 3 } t && t.SourceWidth == t.TargetWidth && t.SourceHeight == t.TargetHeight);
                var name = sprite.Name.Content!;
                var original = FramePicture.Read(workspace.Data, name, 0);
                sprite.Textures.Insert(1, new G3MLib.DataFile.Models.GameMakerSprite.TextureEntry { Texture = sprite.Textures[0].Texture }); // a duplicated frame: one picture, two frames
                Assert.Same(sprite.Textures[0].Texture, sprite.Textures[1].Texture);
                var painted = FramePicture.Read(workspace.Data, name, 1);
                painted.Stamp(0, 0, 1, Red);
                var pages = workspace.Data.EmbeddedTextures.Count;
                workspace.Edit(new G3MStudio.Core.Workspace.FrameOwnPictureCommand(name, 1, FramePicture.ToPng(painted)));
                Assert.NotSame(sprite.Textures[0].Texture, sprite.Textures[1].Texture);
                Assert.Equal(Red, FramePicture.Read(workspace.Data, name, 1).Get(0, 0));
                Assert.Equal(original.Get(0, 0), FramePicture.Read(workspace.Data, name, 0).Get(0, 0));
                workspace.History.Undo();
                Assert.Same(sprite.Textures[0].Texture, sprite.Textures[1].Texture);
                Assert.Equal(pages, workspace.Data.EmbeddedTextures.Count);
            }
            finally
            {
                window.Close();
            }
        });
    }
}
