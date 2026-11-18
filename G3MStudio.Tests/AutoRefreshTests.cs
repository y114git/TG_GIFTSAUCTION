using Avalonia.Controls;
using Avalonia.Threading;
using G3MLib.DataFile.Models;
using G3MStudio.Core.Workspace;
using G3MStudio.Editor;
using G3MStudio.Rooms;

#pragma warning disable CA2000 // the data model owns the sprites it holds

namespace G3MStudio.Tests;

/// <summary>A view that shows data another editor, command, undo or redo can change follows it by itself: no reopening, no user action.</summary>
public sealed class AutoRefreshTests
{
    internal static async Task<(Workspace Workspace, GameMakerSprite Sprite, TempDir Dir)> OpenAsync(string name = "spr_auto")
    {
        var dir = new TempDir();
        string file;
        using (var data = TestData.Create(0))
        {
            TestData.AddPage(data);
            SpriteTemplate.Create(data, name);
            file = TestData.Write(dir.File("data.win"), data);
        }

        var userData = new UserData(dir.File("user"));
        var service = new WorkspaceService(userData, RecentFiles.Load(userData));
        var workspace = await service.OpenAsync(file, cancellationToken: TestContext.Current.CancellationToken);
        return (workspace, workspace.Data.Sprites.First(s => s.Name.Content == name), dir);
    }

    internal static async Task Settle(Task? ready = null)
    {
        for (var i = 0; i < 4; i++)
        {
            Dispatcher.UIThread.RunJobs();
            if (ready is not null) await ready;
            await Task.Delay(20, TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task ACanvasResizeInTheImageEditorReachesTheOpenSpriteTabAndUndoAndRedoFollow()
    {
        var (workspace, sprite, dir) = await OpenAsync();
        using (dir)
        using (workspace)
        {
            await HeadlessApp.Run(async () =>
            {
                using var textures = new TextureBitmaps();
                var tab = new SpriteEditorView(sprite, textures, workspace);
                var window = new Window { Content = tab, Width = 1100, Height = 800 };
                window.Show();
                await tab.Tools.Ready;
                await Settle();
                var canvas = tab.Tools.Canvas;
                Assert.Equal((32, 32), canvas.PictureSize);
                var before = canvas.PictureBitmap;

                using var session = new WorkspaceImageSession(workspace, "spr_auto");
                session.CommitResize(0, new ResizeSpec(false, 48, 40), true, null);
                await Settle(canvas.Ready);
                Assert.Equal((48, 40), canvas.PictureSize);
                Assert.Equal((uint)(48 * canvas.Zoom + 1 + 2 * SpriteCanvas.Pad), (uint)canvas.Width);
                Assert.NotSame(before, canvas.PictureBitmap);
                Assert.Equal("0  48x40", tab.Frames.FrameCaption(0));
                Assert.Equal(48u, sprite.Width);

                workspace.Undo();
                await Settle(canvas.Ready);
                Assert.Equal((32, 32), canvas.PictureSize);
                workspace.Redo();
                await Settle(canvas.Ready);
                Assert.Equal((48, 40), canvas.PictureSize);
                window.Close();
            });
        }
    }
}
