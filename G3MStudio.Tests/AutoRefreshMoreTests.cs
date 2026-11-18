using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using G3MLib.DataFile.Models;
using G3MStudio.Core.Workspace;
using G3MStudio.Editor;
using G3MStudio.Rooms;

#pragma warning disable CA2000 // the data model owns the sprites it holds

namespace G3MStudio.Tests;

public sealed class AutoRefreshMoreTests
{
    private static Point HandlePoint(SpriteCanvas canvas, Window window, int sx, int sy)
    {
        var (w, h) = (canvas.PictureSize.Width * canvas.Zoom, canvas.PictureSize.Height * canvas.Zoom);
        const double off = 11; // handle half (5) plus the gap (6)
        var at = new Point(SpriteCanvas.Pad + (sx < 0 ? -off : sx > 0 ? w + off : w / 2), SpriteCanvas.Pad + (sy < 0 ? -off : sy > 0 ? h + off : h / 2));
        return canvas.TranslatePoint(at, window)!.Value;
    }

    private static void Hold(Window window, Point from, Vector by, RawInputModifiers modifiers, Action? midway = null, bool release = true)
    {
        window.MouseMove(from);
        window.MouseDown(from, MouseButton.Left, modifiers);
        window.MouseMove(from + by, modifiers | RawInputModifiers.LeftMouseButton);
        midway?.Invoke();
        if (release) window.MouseUp(from + by, MouseButton.Left, modifiers);
        Dispatcher.UIThread.RunJobs();
    }

    [Fact]
    public async Task TheSpritePreviewHandlesResizeTheCanvasStretchWithCtrlKeepAspectWithShiftAndEscCancels()
    {
        var (workspace, sprite, dir) = await AutoRefreshTests.OpenAsync();
        using (dir)
        using (workspace)
        {
            await HeadlessApp.Run(async () =>
            {
                using var textures = new TextureBitmaps();
                var tab = new SpriteEditorView(sprite, textures, workspace);
                var window = new Window { Content = tab, Width = 1100, Height = 900 };
                window.Show();
                await tab.Tools.Ready;
                await AutoRefreshTests.Settle();
                var canvas = tab.Tools.Canvas;
                var none = RawInputModifiers.None;
                var by = new Vector(8 * canvas.Zoom, 0);
                var right = HandlePoint(canvas, window, 1, 0);
                Assert.Equal((1, 0), canvas.HandleAt(window.TranslatePoint(right, canvas)!.Value));
                // The collision box handles inside never share a pixel with the outer ones.
                Assert.Null(canvas.HandleAt(new Point(SpriteCanvas.Pad + (canvas.Zoom * 32), SpriteCanvas.Pad + (canvas.Zoom * 16))));
                var before = workspace.History.Position;

                Hold(window, right, by, none, () => Assert.False(canvas.ResizeDrag!.Scale));
                await AutoRefreshTests.Settle(canvas.Ready);
                Assert.Equal((40, 32), ((int)sprite.Width, (int)sprite.Height));
                Assert.Equal(before + 1, workspace.History.Position);
                workspace.Undo();
                await AutoRefreshTests.Settle(canvas.Ready);
                Assert.Equal((32, 32), ((int)sprite.Width, (int)sprite.Height));

                Hold(window, right, by, RawInputModifiers.Control, () => Assert.True(canvas.ResizeDrag!.Scale));
                await AutoRefreshTests.Settle(canvas.Ready);
                Assert.Equal((40, 32), ((int)sprite.Width, (int)sprite.Height));
                Assert.Equal(before + 1, workspace.History.Position);
                workspace.Undo();
                await AutoRefreshTests.Settle(canvas.Ready);

                var corner = HandlePoint(canvas, window, 1, 1);
                Hold(window, corner, new Vector(16 * canvas.Zoom, 4 * canvas.Zoom), RawInputModifiers.Shift, () => Assert.Equal(canvas.ResizeDrag!.Width, canvas.ResizeDrag.Height));
                await AutoRefreshTests.Settle(canvas.Ready);
                Assert.Equal(48u, sprite.Width);
                Assert.Equal(sprite.Width, sprite.Height);
                workspace.Undo();
                await AutoRefreshTests.Settle(canvas.Ready);

                Hold(window, right, by, none, () =>
                {
                    Assert.NotNull(canvas.ResizeDrag);
                    window.KeyPress(Key.Escape, none, PhysicalKey.Escape, null);
                }, release: false);
                window.MouseUp(right + by, MouseButton.Left);
                Dispatcher.UIThread.RunJobs();
                Assert.Null(canvas.ResizeDrag);
                Assert.Equal(before, workspace.History.Position);
                Assert.Equal(32u, sprite.Width);
                window.Close();
            });
        }
    }

    [Fact]
    public async Task AnObjectPreviewAndAnExplorerPictureFollowASpriteEditAndUndo()
    {
        var (workspace, sprite, dir) = await AutoRefreshTests.OpenAsync();
        using (dir)
        using (workspace)
        {
            var obj = new GameMakerGameObject { Name = new GameMakerString("obj_auto"), Sprite = sprite };
            workspace.Data.GameObjects.Add(obj);
            await HeadlessApp.Run(async () =>
            {
                using var textures = new TextureBitmaps(workspace.Lock);
                var links = new ObjectLinksView(obj, workspace, _ => { }, _ => { }, textures);
                var window = new Window { Content = links, Width = 900, Height = 600 };
                window.Show();
                await AutoRefreshTests.Settle();
                Image Preview() => Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(links).OfType<Image>().Single(i => i.Name == "ObjectPreviewImage");
                var first = Preview().Source!.Size;
                using var service = ThumbnailTests.Shared(workspace, textures);
                var key = ResourceTypes.KeyOf("sprites", "spr_auto");
                var thumb = (await service.GetAsync(key))!.Size;

                using (var session = new WorkspaceImageSession(workspace, "spr_auto")) session.CommitResize(0, new ResizeSpec(false, 48, 40), true, null);
                await AutoRefreshTests.Settle();
                await AutoRefreshTests.Settle();
                Assert.Equal(48, Preview().Source!.Size.Width);
                Assert.Equal(48, (await service.GetAsync(key))!.Size.Width);

                workspace.Undo();
                await AutoRefreshTests.Settle();
                await AutoRefreshTests.Settle();
                Assert.Equal(first, Preview().Source!.Size);
                Assert.Equal(thumb, (await service.GetAsync(key))!.Size);
                window.Close();
            });
        }
    }

    private static WeakReference OpenTab(GameMakerSprite sprite, TextureBitmaps textures, Workspace workspace, Window window)
    {
        var tab = new SpriteEditorView(sprite, textures, workspace);
        window.Content = tab;
        return new WeakReference(tab);
    }

    [Fact]
    public async Task AHiddenSpriteTabCatchesUpWhenShown()
    {
        var (workspace, sprite, dir) = await AutoRefreshTests.OpenAsync();
        using (dir)
        using (workspace)
        {
            await HeadlessApp.Run(async () =>
            {
                using var textures = new TextureBitmaps();
                var window = new Window { Width = 1100, Height = 800 };
                OpenTab(sprite, textures, workspace, window);
                window.Show();
                await AutoRefreshTests.Settle();
                var tab = (SpriteEditorView)window.Content!;
                var canvas = tab.Tools.Canvas;
                var follow = tab.IsVisible;
                Assert.True(follow);

                window.Content = null; // a tab that is not on screen is detached and does nothing until shown
                using (var session = new WorkspaceImageSession(workspace, "spr_auto")) session.CommitResize(0, new ResizeSpec(false, 48, 40), true, null);
                await AutoRefreshTests.Settle(canvas.Ready);
                Assert.Equal((32, 32), canvas.PictureSize);
                window.Content = tab;
                await AutoRefreshTests.Settle(canvas.Ready);
                await AutoRefreshTests.Settle(tab.Tools.Canvas.Ready);
                Assert.Equal((48, 40), canvas.PictureSize);

                window.Close();
            });
        }
    }

    [Fact]
    public async Task AFailedRefreshIsTriedAgainAndAHiddenAncestorCatchesUpWhenShown()
    {
        var (workspace, _, dir) = await AutoRefreshTests.OpenAsync();
        using (dir)
        using (workspace)
        {
            await HeadlessApp.Run(async () =>
            {
                var view = new Border();
                var parent = new Border { Child = view };
                var window = new Window { Content = parent, Width = 300, Height = 300 };
                window.Show();
                var (calls, fail) = (0, true);
                var follow = HistoryFollow.Of(view, workspace.History, () =>
                {
                    calls++;
                    if (fail) throw new InvalidOperationException("refresh failed");
                });
                var width = 20;
                void Edit()
                {
                    using var session = new WorkspaceImageSession(workspace, "spr_auto");
                    session.CommitResize(0, new ResizeSpec(false, ++width, 20), true, null);
                    Dispatcher.UIThread.RunJobs();
                }

                Edit();
                Assert.Equal(1, calls); // the throw did not escape, and the view is not marked as current
                fail = false;
                Edit();
                Assert.Equal(2, calls);

                parent.IsVisible = false; // an ancestor, not the view itself
                Edit();
                Assert.Equal(2, calls);
                parent.IsVisible = true;
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(3, calls);
                Assert.Equal(3, follow.Runs);
                window.Close();
                await Task.CompletedTask;
            });
        }
    }

    [Fact]
    public async Task TwoHundredOpenPreviewsRefreshOnceEachForOneChange()
    {
        var (workspace, sprite, dir) = await AutoRefreshTests.OpenAsync();
        using (dir)
        using (workspace)
        {
            await HeadlessApp.Run(async () =>
            {
                using var textures = new TextureBitmaps();
                var panel = new StackPanel();
                var follows = new List<HistoryFollow>();
                var canvases = new List<SpriteCanvas>();
                for (var i = 0; i < 200; i++)
                {
                    var canvas = new SpriteCanvas(sprite, textures);
                    canvases.Add(canvas);
                    panel.Children.Add(canvas);
                    follows.Add(HistoryFollow.Of(canvas, workspace.History, canvas.Refresh));
                }

                var window = new Window { Content = new ScrollViewer { Content = panel }, Width = 600, Height = 600 };
                window.Show();
                await AutoRefreshTests.Settle(canvases[0].Ready);
                var clock = System.Diagnostics.Stopwatch.StartNew();
                using (var session = new WorkspaceImageSession(workspace, "spr_auto")) session.CommitResize(0, new ResizeSpec(false, 48, 40), true, null);
                Dispatcher.UIThread.RunJobs();
                await Task.WhenAll(canvases.Select(c => c.Ready));
                Dispatcher.UIThread.RunJobs();
                clock.Stop();
                Assert.All(follows, f => Assert.Equal(1, f.Runs));
                Assert.All(canvases, c => Assert.Equal((48, 40), c.PictureSize));
                TestContext.Current.SendDiagnosticMessage($"BENCH 200 previews, one change: {clock.ElapsedMilliseconds} ms");
                Assert.True(clock.ElapsedMilliseconds < 3000, $"{clock.ElapsedMilliseconds} ms");
                window.Close();
            });
        }
    }

    [Fact]
    public async Task AClosedSpriteTabIsNotKeptAliveByTheHistoryAfterAChange()
    {
        var (workspace, sprite, dir) = await AutoRefreshTests.OpenAsync();
        using (dir)
        using (workspace)
        {
            await HeadlessApp.Run(async () =>
            {
                using var textures = new TextureBitmaps();
                var window = new Window { Width = 1100, Height = 800 };
                var weak = OpenTab(sprite, textures, workspace, window);
                window.Show();
                await AutoRefreshTests.Settle();
                using (var session = new WorkspaceImageSession(workspace, "spr_auto")) session.CommitResize(0, new ResizeSpec(false, 48, 40), true, null);
                await AutoRefreshTests.Settle();
                window.Content = null;
                await AutoRefreshTests.Settle();
                for (var i = 0; i < 5 && weak.IsAlive; i++)
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    await Task.Delay(20, TestContext.Current.CancellationToken);
                }

                Assert.False(weak.IsAlive);
                window.Close();
            });
        }
    }
}
