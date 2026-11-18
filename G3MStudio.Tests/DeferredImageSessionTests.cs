using G3MStudio.Core.Workspace;
using G3MStudio.Editor;

#pragma warning disable CA2000 // sessions made for a check only

namespace G3MStudio.Tests;

/// <summary>The image editor keeps the edits of a sprite to itself until Apply writes them, all together, as one step of the workspace history.</summary>
public sealed class DeferredImageSessionTests
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

    private static ImageEdit Stroke(ImageSession session, int frame, int x, Rgba colour)
    {
        var after = session.Read(frame);
        after.Set(x, 1, colour);
        return new ImageEdit(null, [new ImageOp(ImageOps.Stroke, "1", "0", ImageOp.Format(colour), $"{x},1 {x},1")], after);
    }

    private static Rgba Pixel(Workspace workspace, int frame, int x) => FramePicture.Read(workspace.Data, "spr_a", frame).Get(x, 1);

    [Fact]
    public async Task LocalEditsStayOutOfTheWorkspaceUntilApplyAndApplyIsOneStepThatUndoTakesBack()
    {
        using var dir = new TempDir();
        using var workspace = await Open(dir);
        using var session = new DeferredImageSession(workspace, "spr_a");
        Assert.True(session.ChangeFrames(FrameListCommand.Duplicate, 0)); // two frames
        var position = workspace.History.Position;

        session.Commit(0, Stroke(session, 0, 3, Red));
        session.Commit(1, Stroke(session, 1, 5, Blue));
        session.Commit(0, Stroke(session, 0, 4, Red));

        Assert.True(session.Pending);
        Assert.Equal(position, workspace.History.Position);
        Assert.Equal(Rgba.Clear, Pixel(workspace, 0, 3));
        Assert.Equal(Rgba.Clear, Pixel(workspace, 1, 5));
        Assert.Equal((Red, Red, Blue), (session.Read(0).Get(3, 1), session.Read(0).Get(4, 1), session.Read(1).Get(5, 1)));

        Assert.True(session.Apply());
        Assert.False(session.Pending);
        Assert.Equal(position + 1, workspace.History.Position);
        Assert.Equal((Red, Red, Blue), (Pixel(workspace, 0, 3), Pixel(workspace, 0, 4), Pixel(workspace, 1, 5)));

        workspace.Undo();
        Assert.Equal(position, workspace.History.Position);
        Assert.Equal((Rgba.Clear, Rgba.Clear, Rgba.Clear), (Pixel(workspace, 0, 3), Pixel(workspace, 0, 4), Pixel(workspace, 1, 5)));
    }

    [Fact]
    public async Task LocalUndoRedoAndDiscardNeverTouchTheWorkspaceHistory()
    {
        using var dir = new TempDir();
        using var workspace = await Open(dir);
        using var session = new DeferredImageSession(workspace, "spr_a");
        var position = workspace.History.Position;
        session.Commit(0, Stroke(session, 0, 3, Red));
        session.Commit(0, Stroke(session, 0, 4, Red));

        session.Undo();
        Assert.Equal((Red, Rgba.Clear), (session.Read(0).Get(3, 1), session.Read(0).Get(4, 1)));
        Assert.True(session.CanRedo);
        session.Redo();
        Assert.Equal(Red, session.Read(0).Get(4, 1));

        Assert.True(session.Discard());
        Assert.False(session.Pending);
        Assert.Equal(Rgba.Clear, session.Read(0).Get(3, 1));
        Assert.Equal(position, workspace.History.Position);
        Assert.Equal(Rgba.Clear, Pixel(workspace, 0, 3));
    }

    [Fact]
    public async Task AFrameChangedElsewhereIsNotOverwrittenUntilTheUserSaysSo()
    {
        using var dir = new TempDir();
        using var workspace = await Open(dir);
        using var session = new DeferredImageSession(workspace, "spr_a");
        session.Commit(0, Stroke(session, 0, 3, Red));
        using (var other = new WorkspaceImageSession(workspace, "spr_a")) other.Commit(0, Stroke(other, 0, 9, Blue));

        Assert.True(session.Conflict);
        var position = workspace.History.Position;
        Assert.False(session.Apply());
        Assert.True(session.Pending); // the local edit is kept
        Assert.Equal(position, workspace.History.Position);

        Assert.True(session.Apply(true));
        Assert.Equal(Red, Pixel(workspace, 0, 3));
        Assert.Equal(Rgba.Clear, Pixel(workspace, 0, 9)); // the local picture of the frame won
    }

    [Fact]
    public async Task AStructuralChangeAppliesThePendingEditsFirstAsTheirOwnStep()
    {
        using var dir = new TempDir();
        using var workspace = await Open(dir);
        using var session = new DeferredImageSession(workspace, "spr_a");
        var position = workspace.History.Position;
        session.Commit(0, Stroke(session, 0, 3, Red));

        Assert.True(session.ChangeFrames(FrameListCommand.Duplicate, 0));

        Assert.Equal(position + 2, workspace.History.Position); // the edits, then the frame
        Assert.Equal("image.appliedFirst", session.TakeNotice());
        Assert.Null(session.TakeNotice());
        Assert.False(session.Pending);
        Assert.Equal((2, Red), (session.FrameCount, Pixel(workspace, 0, 3)));
    }

    [Fact]
    public async Task AStructuralChangeIsRefusedWhileTheEditedFrameChangedElsewhere()
    {
        using var dir = new TempDir();
        using var workspace = await Open(dir);
        using var session = new DeferredImageSession(workspace, "spr_a");
        session.Commit(0, Stroke(session, 0, 3, Red));
        using (var other = new WorkspaceImageSession(workspace, "spr_a")) other.Commit(0, Stroke(other, 0, 9, Blue));

        Assert.Throws<InvalidOperationException>(() => session.ChangeFrames(FrameListCommand.Duplicate, 0));
        Assert.True(session.Pending);
        Assert.Equal(1, session.FrameCount);
    }

    [Fact]
    public async Task ALayerLockIsLocalAndLeavesThePixelsAlone()
    {
        using var dir = new TempDir();
        using var workspace = await Open(dir);
        using var session = new DeferredImageSession(workspace, "spr_a");
        var before = Pixel(workspace, 0, 3);
        Assert.True(session.CommitLayers(0, LayerSet.Op("add", 0, "Top")));
        Assert.True(session.CommitLayers(0, LayerSet.Op("lock", 0, "1")));
        Assert.True(session.ReadLayers(0).Layers[0].Locked);
        Assert.True(session.Pending);

        Assert.True(session.Apply());
        Assert.Equal(before, Pixel(workspace, 0, 3));
        Assert.True(session.ReadLayers(0).Layers[0].Locked);
    }

    [Fact]
    public async Task ARedoOfTheWorkspaceDoesNotRunWhileLocalEditsArePending()
    {
        using var dir = new TempDir();
        using var workspace = await Open(dir);
        using var session = new DeferredImageSession(workspace, "spr_a");
        session.Commit(0, Stroke(session, 0, 3, Red));
        Assert.True(session.Apply());
        workspace.Undo(); // the applied step is undone, so the workspace has a step to redo
        Assert.True(workspace.History.CanRedo);

        session.Commit(0, Stroke(session, 0, 5, Blue));
        Assert.False(session.CanRedo);
        var position = workspace.History.Position;
        session.Redo();
        Assert.Equal(position, workspace.History.Position);
        Assert.True(session.Pending);
    }

    [Fact]
    public async Task ASpriteDeletedOrRenamedUnderPendingEditsKeepsThemAndNothingIsWrittenBlind()
    {
        foreach (var rename in new[] { false, true })
        {
            using var dir = new TempDir();
            using var workspace = await Open(dir);
            using var session = new DeferredImageSession(workspace, "spr_a");
            session.Commit(0, Stroke(session, 0, 3, Red));
            workspace.Edit(rename ? new RenameResourceCommand("sprites", "spr_a", "spr_renamed") : new ResourceListCommand("sprites", ResourceListCommand.Delete, "spr_a", true));
            var position = workspace.History.Position;

            Assert.Equal(0, session.FrameCount);
            Assert.True(session.Pending);
            Assert.True(session.Conflict);
            Assert.False(session.Apply()); // the user is asked first
            Assert.Equal(position, workspace.History.Position);
            var refused = Record.Exception(() => session.Apply(true)); // the sprite is gone: refused, not half written
            Assert.True(refused is InvalidOperationException or InvalidDataException, refused?.GetType().Name); // the kinds the editor shows as a message
            Assert.True(session.Pending);
            Assert.Equal(position, workspace.History.Position);
            Assert.True(session.Discard());
            Assert.False(session.Pending);
        }
    }

    [Fact]
    public async Task ALockedSpriteRefusesTheApplyAsAnInvalidOperationAndKeepsTheEdits()
    {
        using var dir = new TempDir();
        using var workspace = await Open(dir);
        using var session = new DeferredImageSession(workspace, "spr_a");
        session.Commit(0, Stroke(session, 0, 3, Red));
        workspace.Locks.Set(ResourceTypes.KeyOf("sprites", "spr_a"), true);
        Assert.Throws<ResourceLockedException>(() => session.Apply()); // the window shows this as a message and stops the save
        Assert.True(session.Pending);
        Assert.Equal(Rgba.Clear, Pixel(workspace, 0, 3));
    }

    [Fact]
    public async Task AnUndoInTheShellUnderPendingEditsMakesThemAConflictInsteadOfBeingLost()
    {
        using var dir = new TempDir();
        using var workspace = await Open(dir);
        using (var other = new WorkspaceImageSession(workspace, "spr_a")) other.Commit(0, Stroke(other, 0, 9, Blue));
        using var session = new DeferredImageSession(workspace, "spr_a");
        session.Commit(0, Stroke(session, 0, 3, Red));
        workspace.Undo();
        Assert.True(session.Conflict);
        Assert.True(session.Pending);
        Assert.Equal(Red, session.Read(0).Get(3, 1)); // the local picture still shows
        Assert.True(session.Apply(true));
        Assert.Equal(Red, Pixel(workspace, 0, 3));
    }

    [Fact]
    public async Task AHundredFramesApplyAsOneStepAndOneByOneFramesTakeAStroke()
    {
        using var dir = new TempDir();
        using var workspace = await Open(dir);
        workspace.Edit(new TextureFitCommand("sprites", "spr_a", 0, FramePicture.ToPng(new PixelBuffer(1, 1))));
        using var session = new DeferredImageSession(workspace, "spr_a");
        for (var i = 1; i < 100; i++) Assert.True(session.ChangeFrames(FrameListCommand.Duplicate, 0));
        Assert.Equal(100, session.FrameCount);
        var position = workspace.History.Position;
        for (var f = 0; f < 100; f++)
        {
            var after = session.Read(f);
            after.Set(0, 0, Red);
            session.Commit(f, new ImageEdit(null, [new ImageOp(ImageOps.Stroke, "1", "0", ImageOp.Format(Red), "0,0 0,0")], after));
        }

        Assert.False(session.Conflict);
        Assert.True(session.Apply());
        Assert.Equal(position + 1, workspace.History.Position);
        Assert.Equal(Red, FramePicture.Read(workspace.Data, "spr_a", 99).Get(0, 0));
    }

    [Fact]
    public async Task AllFramesResizeOfFramesWithDifferentSizesKeepsEachFramesPixelsAndAppliesEditsFirst()
    {
        using var dir = new TempDir();
        using var workspace = await Open(dir);
        var big = new PixelBuffer(8, 6);
        big.Set(2, 1, Blue);
        workspace.Edit(new TextureFitCommand("sprites", "spr_a", 0, FramePicture.ToPng(big)));
        using var session = new DeferredImageSession(workspace, "spr_a");
        Assert.True(session.ChangeFrames(FrameListCommand.Duplicate, 0));
        workspace.Edit(new TextureFitCommand("sprites", "spr_a", 1, FramePicture.ToPng(new PixelBuffer(3, 2))));
        session.Commit(0, Stroke(session, 0, 4, Red));

        Assert.True(session.CommitResize(0, new ResizeSpec(false, 10, 10, 0, 0), true, null));
        Assert.Equal("image.appliedFirst", session.TakeNotice());
        Assert.False(session.Pending);
        foreach (var frame in new[] { 0, 1 })
        {
            var picture = FramePicture.Read(workspace.Data, "spr_a", frame);
            Assert.Equal((10, 10), (picture.Width, picture.Height));
        }

        var first = FramePicture.Read(workspace.Data, "spr_a", 0);
        Assert.Equal((Blue, Red), (first.Get(2, 1), first.Get(4, 1)));
    }

    [Fact]
    public async Task AWorkspaceUndoDropsTheLocalRedoSoRedoTakesTheWorkspaceStepBack()
    {
        using var dir = new TempDir();
        using var workspace = await Open(dir);
        using var session = new DeferredImageSession(workspace, "spr_a");
        session.Commit(0, Stroke(session, 0, 3, Red));
        Assert.True(session.Apply());
        session.Commit(0, Stroke(session, 0, 4, Blue));
        session.Undo(); // the local stroke goes to the local redo list
        session.Undo(); // nothing local: the applied step is undone

        Assert.Equal(Rgba.Clear, Pixel(workspace, 0, 3));
        session.Redo();

        Assert.Equal(Red, Pixel(workspace, 0, 3));
        Assert.False(session.Pending);
        Assert.Equal(Rgba.Clear, session.Read(0).Get(4, 1));
    }

    [Fact]
    public async Task ARefusedResizeLeavesNoStaleAppliedFirstNotice()
    {
        using var dir = new TempDir();
        using var workspace = await Open(dir);
        using var session = new DeferredImageSession(workspace, "spr_a");
        session.Commit(0, Stroke(session, 0, 3, Red));

        Assert.Throws<InvalidDataException>(() => session.CommitResize(0, new ResizeSpec(false, 5000, 5000, 0, 0), true, null));

        Assert.Null(session.TakeNotice());
    }

    [Fact]
    public void TheImageEditsAreDeferredByDefault() => Assert.False(new G3MStudio.Core.Settings.ImageOptions().ApplyImmediately);
}
