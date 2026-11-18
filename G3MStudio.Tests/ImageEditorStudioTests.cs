using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using G3MStudio.Core.Commands;
using G3MStudio.Core.Workspace;
using G3MStudio.Editor;

namespace G3MStudio.Tests;

public sealed class ImageEditorStudioTests
{
    private static readonly Rgba Red = new(255, 0, 0, 255);

    [Fact]
    public async Task TextRendersPixelsInTheToolColourAndSmoothTextHasSoftEdges()
    {
        await HeadlessApp.Run(() =>
        {
            var hard = TextRaster.Render("Hi", "", 20, Red, false) ?? throw new InvalidOperationException("no text picture");
            var soft = TextRaster.Render("Hi", "", 20, Red, true) ?? throw new InvalidOperationException("no text picture");
            static int[] alphas(PixelBuffer p) => [.. Enumerable.Range(0, p.Width * p.Height).Select(i => (int)p.Data[i * 4 + 3])];
            Assert.Contains(alphas(hard), a => a == 255);
            Assert.DoesNotContain(alphas(hard), a => a is > 0 and < 255);
            Assert.Contains(alphas(soft), a => a is > 0 and < 255);
            Assert.True(Enumerable.Range(0, hard.Width * hard.Height).Where(i => hard.Data[i * 4 + 3] == 255).All(i => hard.Get(i % hard.Width, i / hard.Width) == Red));
            Assert.Null(TextRaster.Render("", "", 20, Red, false));
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task TheTextToolStampsTheTypedTextAsOneEdit()
    {
        await HeadlessApp.Run(() =>
        {
            var session = new LocalImageSession(new PixelBuffer(64, 32));
            using var view = new ImageEditorView(session);
            var window = new Window { Width = 1200, Height = 700, Content = view };
            window.Show();
            try
            {
                view.GetVisualDescendants().OfType<TextBox>().First(t => t.Name == "ImageText").Text = "Hi";
                view.SetColour(Red, false);
                view.Run("img.tool.text");
                Assert.Equal(ImageTool.Text, view.Tool);
                view.PlaceText((2, 2), Red);
                Assert.Contains(Enumerable.Range(0, 64 * 32), i => view.Canvas.Picture.Data[i * 4 + 3] == 255);
                Assert.True(session.CanUndo);
            }
            finally
            {
                window.Close();
            }

            return Task.CompletedTask;
        });
    }

    [Fact]
    public void EveryImageCommandIsInTheRegistryAndCanBeRebound()
    {
        var registry = AppCommands.Create(new FakeShell());
        Assert.True(AppCommands.ImageCommands.Count >= 60);
        foreach (var (id, shortcut) in AppCommands.ImageCommands)
        {
            var command = registry.Get(id);
            Assert.Equal(AppCommands.ImageTab, command.When);
            Assert.Equal(shortcut, command.Shortcut);
            registry.SetShortcut(id, "Ctrl+Alt+F9");
            Assert.Equal("Ctrl+Alt+F9", registry.Get(id).Shortcut);
            registry.SetShortcut(id, shortcut);
        }

        Assert.Equal(AppCommands.ImageCommands.Select(c => c.Id).Distinct().Count(), AppCommands.ImageCommands.Count);
        Assert.Contains(ImageTools.All, t => AppCommands.ImageCommands.Any(c => c.Id == ImageTools.CommandOf(t)));
        Assert.All(ImageTools.All, t => Assert.Contains(AppCommands.ImageCommands, c => c.Id == ImageTools.CommandOf(t)));
    }

    [Fact]
    public async Task ReboundKeysReachTheEditorAndTheOldKeyStopsWorking()
    {
        await HeadlessApp.Run(() =>
        {
            var registry = AppCommands.Create(new FakeShell());
            registry.SetShortcut("img.tool.eraser", "K");
            using var view = new ImageEditorView(new LocalImageSession(new PixelBuffer(8, 8))) { Bindings = id => registry.Find(id)?.Shortcut };
            var window = new Window { Width = 1100, Height = 700, Content = view };
            window.Show();
            try
            {
                view.Canvas.Focus();
                window.KeyPress(Key.K, RawInputModifiers.None, PhysicalKey.None, null);
                Assert.Equal(ImageTool.Eraser, view.Tool);
                window.KeyPress(Key.D, RawInputModifiers.None, PhysicalKey.None, null);
                Assert.Equal(ImageTool.Pencil, view.Tool);
                window.KeyPress(Key.E, RawInputModifiers.None, PhysicalKey.None, null);
                Assert.Equal(ImageTool.Pencil, view.Tool); // E no longer chooses the eraser
            }
            finally
            {
                window.Close();
            }

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task AnOptionsRecordSetsTheEditorDefaults()
    {
        await HeadlessApp.Run(() =>
        {
            var options = new G3MStudio.Core.Settings.ImageOptions { BrushSize = 5, Smooth = true, Tolerance = 20, GridCell = 8, HandleSize = 6, GridColour = "#FF0000", OnionFrames = 2 };
            using var view = new ImageEditorView(new LocalImageSession(new PixelBuffer(8, 8)), 0, options);
            Assert.Equal(5, view.Canvas.BrushSize);
            Assert.True(view.Canvas.Smooth);
            Assert.Equal(20, view.Canvas.Tolerance);
            Assert.Equal(8, view.Canvas.GridCell);
            Assert.Equal(6, view.Canvas.HandleSize);
            Assert.NotNull(view.Canvas.GridBrush);
            return Task.CompletedTask;
        });
    }

    [Fact]
    public void ImageOptionsAreSavedClampedAndLoadedAgain()
    {
        using var dir = new TempDir();
        var userData = new UserData(dir.File("user"));
        using (var store = new G3MStudio.Core.Settings.SettingsStore(userData)) store.Update(s => s with { Image = s.Image with { BrushSize = 999, Smooth = true, GridColour = "#112233", CheckerLight = "bad" } });
        using var again = new G3MStudio.Core.Settings.SettingsStore(userData);
        Assert.Equal(256, again.Current.Image.BrushSize);
        Assert.True(again.Current.Image.Smooth);
        Assert.Equal("#112233", again.Current.Image.GridColour);
        Assert.Equal("", again.Current.Image.CheckerLight);
    }

    [Fact]
    public async Task LayersInTheTabAreUndoableAndTheFileKeepsTheFlatResult()
    {
        using var dir = new TempDir();
        var path = dir.File("data.win");
        using (var data = CopyResourcesTests.Source()) TestData.Write(path, data);
        var userData = new UserData(dir.File("user"));
        using var workspace = await new WorkspaceService(userData, RecentFiles.Load(userData)).OpenAsync(path, cancellationToken: TestContext.Current.CancellationToken);
        await HeadlessApp.Run(() =>
        {
            using var view = new ImageEditorView(new WorkspaceImageSession(workspace, "spr_a"));
            var window = new Window { Width = 1200, Height = 900, Content = view };
            window.Show();
            try
            {
                Assert.Single(view.GetVisualDescendants().OfType<Border>(), b => b.Name?.StartsWith("LayerRow_", StringComparison.Ordinal) == true);
                Assert.True(view.Run("img.layerAdd"));
                Assert.Equal(1, view.ActiveLayer);
                Assert.Equal(2, view.GetVisualDescendants().OfType<Border>().Count(b => b.Name?.StartsWith("LayerRow_", StringComparison.Ordinal) == true));
                view.Canvas.Run(new ImageOp(ImageOps.Stroke, "1", "0", "#FF0000", "1,1 5,1"));
                Assert.Equal(Red, FramePicture.Read(workspace.Data, "spr_a", 0).Get(3, 1));
                Assert.Equal(2, workspace.History.Position);

                view.Run("img.layerLock");
                view.Canvas.Run(new ImageOp(ImageOps.Stroke, "1", "0", "#00FF00", "1,3 5,3"));
                Assert.Equal(Rgba.Clear, view.Canvas.Picture.Get(3, 3)); // a locked layer takes no drawing
                view.Run("img.layerLock");

                view.Run("img.layerMergeDown");
                Assert.Equal(1, view.Canvas.Layers.Count);
                Assert.Equal(Red, FramePicture.Read(workspace.Data, "spr_a", 0).Get(3, 1));
                view.Undo();
                Assert.Equal(2, view.Canvas.Layers.Count);

                // Another edit of the frame drops the stack: one layer with the flat picture.
                workspace.Edit(new ReplaceFrameCommand("spr_a", 0, FramePicture.ToPng(new PixelBuffer(32, 32))));
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.Equal(1, view.Canvas.Layers.Count);
                Assert.Equal(Rgba.Clear, FramePicture.Read(workspace.Data, "spr_a", 0).Get(3, 1));
            }
            finally
            {
                window.Close();
            }

            return Task.CompletedTask;
        });
    }
}
