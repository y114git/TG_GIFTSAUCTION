using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.VisualTree;
using G3MLib.DataFile;
using G3MLib.DataFile.Models;
using G3MStudio.Core.Settings;
using G3MStudio.Core.Workspace;
using G3MStudio.Editor;
using G3MStudio.Rooms;

#pragma warning disable CA2000 // the data model owns the sprites it holds

namespace G3MStudio.Tests;

public sealed class SpriteStripTests
{
    private static async Task<(Workspace Workspace, GameMakerSprite Sprite, TempDir Dir)> OpenAsync()
    {
        var dir = new TempDir();
        string file;
        using (var data = TestData.Create(0))
        {
            TestData.AddPage(data);
            SpriteTemplate.Create(data, "spr_strip");
            file = TestData.Write(dir.File("data.win"), data);
        }

        var userData = new UserData(dir.File("user"));
        var service = new WorkspaceService(userData, RecentFiles.Load(userData));
        var workspace = await service.OpenAsync(file, cancellationToken: TestContext.Current.CancellationToken);
        return (workspace, workspace.Data.Sprites.First(s => s.Name.Content == "spr_strip"), dir);
    }

    [Fact]
    public async Task TheStripAddsDuplicatesDeletesAndSelectsFramesEachAsOneUndoableStep()
    {
        var (workspace, sprite, dir) = await OpenAsync();
        using (dir)
        using (workspace)
        {
            await HeadlessApp.Run(async () =>
            {
                using var textures = new TextureBitmaps();
                var view = new SpriteFramesView(sprite, textures, workspace.History, workspace);
                await view.Ready;
                Assert.Single(sprite.Textures);

                Assert.True(await view.AddAsync());
                Assert.Equal(2, sprite.Textures.Count);
                Assert.Equal([1], view.SelectedFrames);
                Assert.True(await view.DuplicateAsync());
                Assert.Equal(3, sprite.Textures.Count);
                Assert.Same(sprite.Textures[1].Texture, sprite.Textures[2].Texture);
                Assert.Equal(2, view.SelectedFrame);

                view.Select(0, KeyModifiers.None);
                view.Select(2, KeyModifiers.Shift);
                Assert.Equal([0, 1, 2], view.SelectedFrames);
                view.Select(1, KeyModifiers.Control);
                Assert.Equal([0, 2], view.SelectedFrames);

                Assert.True(await view.DeleteAsync());
                Assert.Single(sprite.Textures);
                workspace.Undo();
                Assert.Equal(3, sprite.Textures.Count);
                workspace.Undo(); // the duplicate
                workspace.Undo(); // the add
                Assert.Single(sprite.Textures);
            });
        }
    }

    [Fact]
    public async Task DraggingFramesReordersThemInOneStepAndUndoPutsThemBack()
    {
        var (workspace, sprite, dir) = await OpenAsync();
        using (dir)
        using (workspace)
        {
            await HeadlessApp.Run(async () =>
            {
                using var textures = new TextureBitmaps();
                var view = new SpriteFramesView(sprite, textures, workspace.History, workspace);
                await view.Ready;
                await view.AddAsync();
                await view.AddAsync();
                var (a, b, c) = (sprite.Textures[0].Texture, sprite.Textures[1].Texture, sprite.Textures[2].Texture);
                Assert.NotSame(a, b);

                view.Pick(0);
                Assert.True(await view.MoveSelectedAsync(3)); // the first frame goes last
                Assert.Same(b, sprite.Textures[0].Texture);
                Assert.Same(c, sprite.Textures[1].Texture);
                Assert.Same(a, sprite.Textures[2].Texture);
                Assert.Equal([2], view.SelectedFrames);

                workspace.Undo();
                Assert.Same(a, sprite.Textures[0].Texture);
                Assert.Same(c, sprite.Textures[2].Texture);
                workspace.Redo();
                Assert.Same(a, sprite.Textures[2].Texture);

                view.Select(0, KeyModifiers.None);
                view.Select(1, KeyModifiers.Shift);
                Assert.False(await view.MoveSelectedAsync(1)); // dropped where it already is
                var replay = EditCommand.Create(FrameReorderCommand.Name, new FrameReorderCommand("spr_strip", [1, 2, 0]).Args);
                Assert.IsType<FrameReorderCommand>(replay);
            });
        }
    }

    [Fact]
    public void MovingASelectionKeepsItsOrderAndAnImpossibleOrderIsRefused()
    {
        Assert.Equal([0, 1, 3, 2, 4], FrameReorderCommand.MoveTo(5, [1, 3], 1));
        Assert.Equal([2, 4, 0, 1, 3], FrameReorderCommand.MoveTo(5, [2, 4], 0));
        Assert.Equal([0, 1, 3, 2, 4], FrameReorderCommand.MoveTo(5, [2], 4));
        Assert.Equal([0, 1, 3, 4, 2], FrameReorderCommand.MoveTo(5, [2], 5));
        using var data = TestData.Create(0);
        TestData.AddPage(data);
        SpriteTemplate.Create(data, "spr_x");
        Assert.Throws<InvalidOperationException>(() => new FrameReorderCommand("spr_x", [1, 0]).Apply(data));
    }

    [Fact]
    public async Task PlaybackStepsAndLoopsOrStopsAtTheLastFrame()
    {
        using var data = TestData.Create(0);
        using var sprite = new GameMakerSprite { Name = data.Strings.MakeString("spr_three", out _), GMS2PlaybackSpeed = 12 };
        for (var i = 0; i < 3; i++) sprite.Textures.Add(new GameMakerSprite.TextureEntry());
        await HeadlessApp.Run(async () =>
        {
            using var textures = new TextureBitmaps();
            var view = new SpriteFramesView(sprite, textures);
            var window = new Window { Content = view };
            window.Show();
            await view.Ready;
            var play = view.GetVisualDescendants().OfType<ToggleButton>().Single(b => b.Name == "SpritePlay");
            var loop = view.GetVisualDescendants().OfType<ToggleButton>().Single(b => b.Name == "SpriteLoop");
            var speed = view.GetVisualDescendants().OfType<NumericUpDown>().Single(b => b.Name == "SpriteSpeed");
            Assert.Equal(12m, speed.Value);

            view.Step();
            view.Step();
            Assert.Equal(2, view.Current);
            view.Step();
            Assert.Equal(0, view.Current); // loops

            loop.IsChecked = false;
            play.IsChecked = true;
            view.Pick(2);
            view.Step();
            Assert.Equal(2, view.Current);
            Assert.False(play.IsChecked); // stopped on the last frame
            window.Close();
        });
    }

    [Fact]
    public async Task TheSpriteSidePanelScrollsWhenTheTabIsShort()
    {
        var (workspace, sprite, dir) = await OpenAsync();
        using (dir)
        using (workspace)
        {
            await HeadlessApp.Run(async () =>
            {
                using var textures = new TextureBitmaps();
                var editor = new SpriteEditorView(sprite, textures, workspace);
                var tab = new PropertyGridTab(workspace, "sprites", "spr_strip", editor);
                var window = new Window { Content = tab, Width = 1100, Height = 400 };
                window.Show();
                await editor.Tools.Ready;
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.True(editor.IsWide);
                var scroll = editor.GetVisualDescendants().OfType<ScrollViewer>().Single(v => v.Name == "SpriteToolsScroll");
                Assert.True(scroll.Extent.Height > scroll.Viewport.Height, $"extent {scroll.Extent.Height} viewport {scroll.Viewport.Height}");
                scroll.Offset = new Avalonia.Vector(0, scroll.Extent.Height);
                Assert.True(scroll.Offset.Y > 0, "the side panel did not scroll");
                window.Close();
            });
        }
    }

    [Fact]
    public async Task AnEditStaysOnItsFrameWhenYouMoveToAnotherFrameInsideTheEditor()
    {
        var (workspace, sprite, dir) = await OpenAsync();
        using (dir)
        using (workspace)
        {
            await HeadlessApp.Run(() =>
            {
                using var session = new WorkspaceImageSession(workspace, "spr_strip");
                Assert.True(session.ChangeFrames(FrameListCommand.Duplicate, 0));
                using var view = new ImageEditorView(session, 0, new ImageOptions());
                var window = new Window { Content = view, Width = 1200, Height = 700 };
                window.Show();
                try
                {
                    var red = new Rgba(255, 0, 0, 255);
                    Assert.Equal(["ImageFrame0", "ImageFrame1"], view.GetVisualDescendants().OfType<Button>().Where(b => b.Name?.StartsWith("ImageFrame", StringComparison.Ordinal) == true && b.Name.Length == 11).Select(b => b.Name).Order());
                    view.Canvas.Run(new ImageOp(ImageOps.Stroke, "1", "0", "#FF0000", "1,1 5,1"));
                    view.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "ImageFrame1").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                    Assert.Equal(1, view.Frame);
                    Assert.NotEqual(red, view.Canvas.Picture.Get(3, 1));
                    view.ShowFrame(0);
                    Assert.Equal(red, view.Canvas.Picture.Get(3, 1)); // kept on its frame, not lost
                    Assert.True(session.CanUndo);
                }
                finally
                {
                    window.Close();
                }

                return Task.CompletedTask;
            });
        }
    }

    [Fact]
    public async Task AnEditorOnAPictureTargetAppliesAtTheEndAndTracksUnappliedEdits()
    {
        await HeadlessApp.Run(() =>
        {
            var session = new TargetImageSession(new PageTarget("Texture 0"), new PixelBuffer(16, 16));
            using var view = new ImageEditorView(session);
            var window = new Window { Content = view, Width = 1200, Height = 700 };
            window.Show();
            try
            {
                var apply = view.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "ImageApply");
                var asked = 0;
                view.ApplyRequested += () => asked++;
                Assert.False(session.Dirty);
                Assert.False(apply.IsEnabled);
                view.Canvas.Run(new ImageOp(ImageOps.Stroke, "1", "0", "#FF0000", "1,1 5,1"));
                Assert.True(session.Dirty);
                Assert.True(apply.IsEnabled);
                apply.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                Assert.Equal(1, asked);
                view.Undo();
                Assert.False(session.Dirty); // back at what was applied
                view.Redo();
                Assert.True(session.Dirty);
                session.MarkApplied();
                Assert.False(session.Dirty);
                Assert.False(apply.IsEnabled);
            }
            finally
            {
                window.Close();
            }

            return Task.CompletedTask;
        });
    }
}
