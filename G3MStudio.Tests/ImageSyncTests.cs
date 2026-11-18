using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using G3MStudio.Core.Workspace;
using G3MStudio.Editor;

#pragma warning disable CA2000 // sessions and windows made for a check only

namespace G3MStudio.Tests;

/// <summary>
/// The image editor of a sprite follows what happens to the sprite elsewhere: a change made behind it (the sprite editor, a second image editor, undo) is read again before
/// the next stroke is taken, a stroke drawn on an old picture is refused instead of overwriting the newer data, and the edits made in the editor can be applied, discarded or saved as a new sprite.
/// </summary>
public sealed class ImageSyncTests
{
    private static readonly Rgba Red = new(255, 0, 0, 255);
    private static readonly Rgba Blue = new(0, 0, 255, 255);

    private static async Task<Workspace> Open(TempDir dir)
    {
        var path = dir.File("data.win");
        using (var data = CopyResourcesTests.Source()) TestData.Write(path, data);
        var userData = new UserData(dir.File("user"));
        return await new WorkspaceService(userData, RecentFiles.Load(userData)).OpenAsync(path, cancellationToken: TestContext.Current.CancellationToken);
    }

    private static ImageEdit Stroke(ImageSession session, int x, Rgba colour)
    {
        var after = session.Read(0);
        after.Set(x, 1, colour);
        return new ImageEdit(null, [new ImageOp(ImageOps.Stroke, "1", "0", ImageOp.Format(colour), $"{x},1 {x},1")], after);
    }

    private static Rgba Pixel(Workspace workspace, int x) => FramePicture.Read(workspace.Data, "spr_a", 0).Get(x, 1);

    [Fact]
    public async Task AStrokeDrawnOnAnOldPictureIsRefusedAndTheNewerChangeStays()
    {
        using var dir = new TempDir();
        using var workspace = await Open(dir);
        using var first = new WorkspaceImageSession(workspace, "spr_a");
        using var second = new WorkspaceImageSession(workspace, "spr_a");
        _ = first.ReadLayers(0);
        second.Commit(0, Stroke(second, 3, Red)); // the second editor paints while the first still holds the old picture
        Assert.True(first.Behind);
        var old = Stroke(first, 9, Blue); // drawn by an editor that has not read the second editor change yet
        Assert.Throws<InvalidOperationException>(() => first.Commit(0, old));
        Assert.Equal(Red, Pixel(workspace, 3));
        Assert.Equal(Rgba.Clear, Pixel(workspace, 9));
        _ = first.ReadLayers(0);
        Assert.False(first.Behind);
        first.Commit(0, Stroke(first, 9, Blue));
        Assert.Equal((Red, Blue), (Pixel(workspace, 3), Pixel(workspace, 9)));
    }

    [Fact]
    public async Task AResizeFromTheSpriteEditorReachesTheOpenImageEditorAndANewStrokeKeepsTheNewSize()
    {
        using var dir = new TempDir();
        using var workspace = await Open(dir);
        await HeadlessApp.Run(async () =>
        {
            using var view = new ImageEditorView(new WorkspaceImageSession(workspace, "spr_a"));
            var window = new Window { Width = 1100, Height = 700, Content = view };
            window.Show();
            try
            {
                await LayoutAudit.Settle();
                using (var sprite = new WorkspaceImageSession(workspace, "spr_a")) // the sprite editor's Resize dialog works through a session of its own
                    Assert.True(sprite.CommitResize(0, new ResizeSpec(false, 48, 40), true, null));
                // Before the editor reloads, a stroke on its old 32 by 32 picture must not be written over the 48 by 40 sprite.
                view.Canvas.Run(new ImageOp(ImageOps.Stroke, "1", "0", "#FF0000", "2,1 2,1"));
                Assert.Equal((48, 40), (FramePicture.Read(workspace.Data, "spr_a", 0).Width, FramePicture.Read(workspace.Data, "spr_a", 0).Height));
                Assert.Equal(Rgba.Clear, Pixel(workspace, 2));
                Dispatcher.UIThread.RunJobs();
                Assert.Equal((48, 40), (view.Canvas.Picture.Width, view.Canvas.Picture.Height));
                view.Canvas.Run(new ImageOp(ImageOps.Stroke, "1", "0", "#FF0000", "2,1 2,1"));
                Assert.Equal(Red, Pixel(workspace, 2));
                Assert.Equal(48, FramePicture.Read(workspace.Data, "spr_a", 0).Width);
                // undo and redo from either side keep the editor and the data together
                workspace.Undo();
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(Rgba.Clear, view.Canvas.Picture.Get(2, 1));
                view.Undo();
                Dispatcher.UIThread.RunJobs();
                Assert.Equal((32, 32), (view.Canvas.Picture.Width, view.Canvas.Picture.Height));
                view.Redo();
                Dispatcher.UIThread.RunJobs();
                Assert.Equal((48, 40), (view.Canvas.Picture.Width, view.Canvas.Picture.Height));
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public async Task AFrameChangeOnAnEditorThatIsBehindIsRefusedAndTheEditorReloads()
    {
        using var dir = new TempDir();
        using var workspace = await Open(dir);
        await HeadlessApp.Run(async () =>
        {
            using var view = new ImageEditorView(new WorkspaceImageSession(workspace, "spr_a"));
            var window = new Window { Width = 1100, Height = 700, Content = view };
            window.Show();
            try
            {
                await LayoutAudit.Settle();
                var frames = workspace.Data.Sprites.First(s => s.Name.Content == "spr_a").Textures.Count;
                using (var sprite = new WorkspaceImageSession(workspace, "spr_a"))
                    Assert.True(sprite.CommitResize(0, new ResizeSpec(false, 48, 40), true, null));
                view.Run("img.duplicateFrame"); // the editor still holds the old picture: nothing is written, the editor reads the sprite again
                Assert.Equal(frames, workspace.Data.Sprites.First(s => s.Name.Content == "spr_a").Textures.Count);
                Dispatcher.UIThread.RunJobs();
                Assert.Equal((48, 40), (view.Canvas.Picture.Width, view.Canvas.Picture.Height));
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public async Task AnUnrelatedEditAfterAcceptIsNotThisEditorsPendingEdit()
    {
        using var dir = new TempDir();
        using var workspace = await Open(dir);
        using var session = new WorkspaceImageSession(workspace, "spr_a");
        session.Commit(0, Stroke(session, 4, Red));
        session.Accept();
        using (var other = new WorkspaceImageSession(workspace, "spr_a"))
            other.Commit(0, Stroke(other, 6, Blue));

        Assert.False(session.Pending);
    }

    [Fact]
    public async Task EditsWaitForApplyAndDiscardTakesBackOnlyTheEditorsOwnEdits()
    {
        using var dir = new TempDir();
        using var workspace = await Open(dir);
        using var session = new WorkspaceImageSession(workspace, "spr_a");
        Assert.False(session.Pending);
        session.Commit(0, Stroke(session, 3, Red));
        Assert.True(session.Pending);
        Assert.True(session.Discard());
        Assert.False(session.Pending);
        Assert.Equal(Rgba.Clear, Pixel(workspace, 3));

        session.Commit(0, Stroke(session, 4, Red));
        session.Accept();
        Assert.False(session.Pending);
        Assert.Equal(Red, Pixel(workspace, 4));

        session.Commit(0, Stroke(session, 5, Red));
        using (var other = new WorkspaceImageSession(workspace, "spr_a"))
        {
            _ = session.ReadLayers(0);
            other.Commit(0, Stroke(other, 6, Blue));
        }

        Assert.False(session.Discard()); // another edit sits on top: undo would take it too
        Assert.Equal((Red, Blue), (Pixel(workspace, 5), Pixel(workspace, 6)));
    }

    [Fact]
    public async Task SaveAsNewMakesTheSpriteInOneUndoStepWithTheFrameSize()
    {
        using var dir = new TempDir();
        using var workspace = await Open(dir);
        using var session = new WorkspaceImageSession(workspace, "spr_a");
        session.Commit(0, Stroke(session, 3, Red));
        var before = workspace.History.Position;
        var pictures = session.Pictures();
        EditCommand command;
        using (workspace.Lock.Read()) command = ResourceTemplates.NewSprite(workspace.Data, "spr_copy", pictures);
        workspace.Edit(command);
        Assert.Equal(before + 1, workspace.History.Position);
        var made = workspace.Data.Sprites.First(s => s.Name.Content == "spr_copy");
        Assert.Equal((32u, 32u), (made.Width, made.Height));
        Assert.Equal(Red, FramePicture.Read(workspace.Data, "spr_copy", 0).Get(3, 1));
        Assert.Equal(Red, Pixel(workspace, 3)); // the original stays
        workspace.Undo();
        Assert.DoesNotContain(workspace.Data.Sprites, s => s.Name.Content == "spr_copy");
    }

    [Fact]
    public async Task TheTabShowsUnappliedEditsAndClosingAsksApplyOrDiscard()
    {
        using var dir = new TempDir();
        var path = dir.File("data.win");
        using (var data = CopyResourcesTests.Source()) TestData.Write(path, data);
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                await window.OpenAsync(path);
                var workspace = window.Workspaces.Current!;
                window.EditImageKey("sprites:spr_a");
                Dispatcher.UIThread.RunJobs();
                var view = window.GetVisualDescendants().OfType<ImageEditorView>().Single();
                var tab = window.Docking.Tabs.Tabs.Single(t => t.Kind == "image");
                Assert.False(view.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "ImageApply").IsEnabled);
                view.Canvas.Run(new ImageOp(ImageOps.Stroke, "1", "0", "#FF0000", "3,1 3,1"));
                Assert.True(view.HasPending);
                Assert.True(window.Docking.DocumentOf(tab.Id)!.IsModified);
                Assert.True(view.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "ImageApply").IsEnabled);

                var asked = 0;
                window.ImageChangesAsker = _ =>
                {
                    asked++;
                    return Task.FromResult(asked == 1 ? CloseChoice.Cancel : CloseChoice.DoItLater);
                };
                window.Docking.CloseTab(tab.Id);
                Dispatcher.UIThread.RunJobs();
                await Task.Delay(50, TestContext.Current.CancellationToken);
                Dispatcher.UIThread.RunJobs();
                Assert.Contains(window.Docking.Tabs.Tabs, t => t.Id == tab.Id); // Cancel keeps the tab and the edit
                Assert.Equal(Rgba.Clear, Pixel(workspace, 3)); // the edit waits in the editor, the sprite is untouched
                Assert.True(view.HasPending);

                window.Docking.CloseTab(tab.Id);
                for (var i = 0; i < 20 && window.Docking.Tabs.Tabs.Any(t => t.Id == tab.Id); i++)
                {
                    await Task.Delay(50, TestContext.Current.CancellationToken);
                    Dispatcher.UIThread.RunJobs();
                }

                Assert.DoesNotContain(window.Docking.Tabs.Tabs, t => t.Id == tab.Id);
                Assert.Equal(Rgba.Clear, Pixel(workspace, 3)); // Discard took the stroke back
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public async Task ClosingTheTabAndChoosingApplyWritesTheEditsAsOneStep()
    {
        using var dir = new TempDir();
        var path = dir.File("data.win");
        using (var data = CopyResourcesTests.Source()) TestData.Write(path, data);
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                await window.OpenAsync(path);
                var workspace = window.Workspaces.Current!;
                window.EditImageKey("sprites:spr_a");
                Dispatcher.UIThread.RunJobs();
                var view = window.GetVisualDescendants().OfType<ImageEditorView>().Single();
                var tab = window.Docking.Tabs.Tabs.Single(t => t.Kind == "image");
                var position = workspace.History.Position;
                view.Canvas.Run(new ImageOp(ImageOps.Stroke, "1", "0", "#FF0000", "3,1 3,1"));
                Assert.Equal(position, workspace.History.Position);
                window.ImageChangesAsker = _ => Task.FromResult(CloseChoice.Save);
                window.Docking.CloseTab(tab.Id);
                for (var i = 0; i < 20 && window.Docking.Tabs.Tabs.Any(t => t.Id == tab.Id); i++)
                {
                    await Task.Delay(50, TestContext.Current.CancellationToken);
                    Dispatcher.UIThread.RunJobs();
                }

                Assert.DoesNotContain(window.Docking.Tabs.Tabs, t => t.Id == tab.Id);
                Assert.Equal((position + 1, Red), (workspace.History.Position, Pixel(workspace, 3)));
            }
            finally { window.Close(); }
        });
    }
}
