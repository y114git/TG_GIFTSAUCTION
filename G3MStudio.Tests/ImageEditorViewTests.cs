using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using G3MStudio.Core.Workspace;
using G3MStudio.Editor;

namespace G3MStudio.Tests;

public sealed class ImageEditorViewTests
{
    private static readonly Rgba Red = new(255, 0, 0, 255);

    [Fact]
    public async Task TheViewHasEveryToolAndAnEditIsOneUndoStepOfTheSession()
    {
        await HeadlessApp.Run(() =>
        {
            var session = new LocalImageSession(new PixelBuffer(8, 8));
            using var view = new ImageEditorView(session);
            var window = new Window { Width = 1100, Height = 700, Content = view };
            window.Show();
            try
            {
                Assert.Equal(Enum.GetValues<ImageTool>().Length, view.GetVisualDescendants().OfType<ToggleButton>().Count(b => b.Name?.StartsWith("Tool_", StringComparison.Ordinal) == true));
                view.SetColour(Red, false);
                view.Canvas.Run(new ImageOp(ImageOps.Stroke, "1", "0", "#FF0000", "0,0 7,0"));
                Assert.Equal(Red, view.Canvas.Picture.Get(4, 0));
                Assert.True(session.CanUndo);
                view.Undo();
                Assert.Equal(Rgba.Clear, view.Canvas.Picture.Get(4, 0));
                view.Redo();
                Assert.Equal(Red, view.Canvas.Picture.Get(4, 0));
            }
            finally
            {
                window.Close();
            }

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task KeysChooseToolsAndShapeStylesLikeGameMaker()
    {
        await HeadlessApp.Run(() =>
        {
            using var view = new ImageEditorView(new LocalImageSession(new PixelBuffer(8, 8)));
            var window = new Window { Width = 1100, Height = 700, Content = view };
            window.Show();
            try
            {
                view.Canvas.Focus();
                foreach (var (key, shift, tool) in new[] { (Key.E, RawInputModifiers.None, ImageTool.Eraser), (Key.F, RawInputModifiers.None, ImageTool.Fill), (Key.L, RawInputModifiers.None, ImageTool.Line), (Key.R, RawInputModifiers.Shift, ImageTool.Rectangle), (Key.C, RawInputModifiers.None, ImageTool.Ellipse), (Key.W, RawInputModifiers.None, ImageTool.Wand), (Key.O, RawInputModifiers.None, ImageTool.Picker), (Key.D, RawInputModifiers.None, ImageTool.Pencil) })
                {
                    window.KeyPress(key, shift, PhysicalKey.None, null);
                    Assert.Equal(tool, view.Tool);
                }

                window.KeyPress(Key.R, RawInputModifiers.Shift, PhysicalKey.None, null);
                Assert.Equal(ShapeStyle.Filled, view.Canvas.Style);
                window.KeyPress(Key.X, RawInputModifiers.None, PhysicalKey.None, null);
                Assert.Equal(new Rgba(255, 255, 255, 255), view.Canvas.Primary);
            }
            finally
            {
                window.Close();
            }

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task APaintedFrameInTheTabIsOneWorkspaceEditAndUndoAndTheJournalRestoreIt()
    {
        using var dir = new TempDir();
        var path = dir.File("data.win");
        using (var data = CopyResourcesTests.Source()) TestData.Write(path, data);
        var userData = new UserData(dir.File("user"));
        var workspace = await new WorkspaceService(userData, RecentFiles.Load(userData)).OpenAsync(path, cancellationToken: TestContext.Current.CancellationToken);
        using var session = new WorkspaceImageSession(workspace, "spr_a");
        session.Commit(0, new ImageEdit(null, [new ImageOp(ImageOps.Stroke, "1", "0", "#FF0000", "1,1 5,1")], ReadWith(session)));
        Assert.Equal(Red, FramePicture.Read(workspace.Data, "spr_a", 0).Get(3, 1));
        Assert.Equal(1, workspace.History.Position);
        session.Undo();
        Assert.Equal(Rgba.Clear, FramePicture.Read(workspace.Data, "spr_a", 0).Get(3, 1));
        session.Redo();
        workspace.Dispose();

        // After a crash (no save) the journal replays the same operations on the file.
        using var again = await new WorkspaceService(userData, RecentFiles.Load(userData)).OpenAsync(path, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(1, again.RecoveredEdits);
        Assert.Equal(Red, FramePicture.Read(again.Data, "spr_a", 0).Get(3, 1));
    }

    private static PixelBuffer ReadWith(ImageSession session)
    {
        var picture = session.Read(0);
        picture.Line(1, 1, 5, 1, 1, Red);
        return picture;
    }
}
