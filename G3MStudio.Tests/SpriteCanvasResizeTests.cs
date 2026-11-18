using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using G3MLib.DataFile;
using G3MStudio.Core.Workspace;
using G3MStudio.Editor;

#pragma warning disable CA2000 // the windows are closed by the test
namespace G3MStudio.Tests;

/// <summary>Resize canvas keeps every pixel at 1:1 (only moved by the anchor), whatever entry opened the dialog and whatever options were kept.</summary>
public sealed class SpriteCanvasResizeTests
{
    private static PixelBuffer Pattern(int w, int h)
    {
        var p = new PixelBuffer(w, h);
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++) p.Set(x, y, new Rgba((byte)(x * 4 + 1), (byte)(y * 3 + 1), (byte)((x ^ y) + 1), 255));
        return p;
    }

    private static (GameMakerData Data, EditHistory History) Fixture(int w, int h)
    {
        var data = CopyResourcesTests.Source();
        var history = new EditHistory(data);
        history.Execute(new TextureFitCommand("sprites", "spr_a", 0, FramePicture.ToPng(Pattern(w, h))));
        return (data, history);
    }

    private static void AssertAt(PixelBuffer source, PixelBuffer result, int dx, int dy)
    {
        for (var y = 0; y < source.Height; y++)
            for (var x = 0; x < source.Width; x++)
            {
                var (rx, ry) = (x + dx, y + dy);
                if (rx < 0 || ry < 0 || rx >= result.Width || ry >= result.Height) continue;
                Assert.Equal(source.Get(x, y), result.Get(rx, ry));
            }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CanvasModeKeepsThePixelsAtOneToOne(bool allFrames)
    {
        var (data, history) = Fixture(55, 75);
        using (data)
        {
            var source = Pattern(55, 75);
            history.Execute(SpriteResize.Build(data, "spr_a", 0, new ResizeSpec(false, 80, 100, 0, 0), allFrames, null));
            var grown = FramePicture.Read(data, "spr_a", 0);
            Assert.Equal((80, 100), (grown.Width, grown.Height));
            AssertAt(source, grown, 0, 0);
            Assert.Equal(0, grown.Get(79, 99).A);
            history.Execute(SpriteResize.Build(data, "spr_a", 0, new ResizeSpec(false, 30, 40, 2, 2), allFrames, null));
            var cut = FramePicture.Read(data, "spr_a", 0);
            Assert.Equal((30, 40), (cut.Width, cut.Height));
            AssertAt(source, cut, 30 - 80, 40 - 100); // bottom right of the 80 x 100 canvas, where the picture sat at 0, 0
        }
    }

    private static async Task<Workspace> Open(TempDir dir, int w, int h)
    {
        var path = dir.File("data.win");
        using (var data = CopyResourcesTests.Source()) TestData.Write(path, data);
        var userData = new UserData(dir.File("user"));
        var workspace = await new WorkspaceService(userData, RecentFiles.Load(userData)).OpenAsync(path, cancellationToken: TestContext.Current.CancellationToken);
        workspace.Edit(new TextureFitCommand("sprites", "spr_a", 0, FramePicture.ToPng(Pattern(w, h))));
        workspace.Edit(new FrameListCommand("spr_a", FrameListCommand.Duplicate, 0));
        return workspace;
    }

    private static T Field<T>(Window window, string name) where T : Control => window.GetLogicalDescendants().OfType<T>().Single(c => c.Name == name);

    // The image editor on the real workspace session: the menu entry opens the dialog, the test sets it up and presses OK.
    private static async Task Drive(Workspace workspace, string command, Action<ResizeImageWindow> setup)
    {
        using var session = new WorkspaceImageSession(workspace, "spr_a");
        using var view = new ImageEditorView(session);
        var host = new Window { Width = 1000, Height = 700, Content = view };
        host.Show();
        List<ResizeImageWindow> opened = [];
        List<Exception> errors = [];
        view.ResizeOpened += dialog =>
        {
            try { setup(dialog); } catch (Exception e) { errors.Add(e); }
            Avalonia.Threading.Dispatcher.UIThread.Post(() => Field<Button>(dialog, "ResizeOk").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)));
            opened.Add(dialog);
        };
        view.Run(command);
        // the dialog opens, shows, takes the click and closes; the edit is committed right after it closes
        for (var i = 0; i < 250 && opened.Count == 0; i++) await Task.Delay(20);
        for (var i = 0; i < 250 && !opened[0].IsVisible; i++) await Task.Delay(20);
        for (var i = 0; i < 250 && opened[0].IsVisible; i++) await Task.Delay(20);
        await Task.Delay(100);
        host.Close();
        if (errors.Count > 0) throw new InvalidOperationException("dialog setup failed", errors[0]);
    }

    private static void AssertCanvas(Workspace workspace, PixelBuffer source, int width, int height)
    {
        for (var frame = 0; frame < 2; frame++)
        {
            PixelBuffer result;
            using (workspace.Lock.Read()) result = FramePicture.Read(workspace.Data, "spr_a", frame);
            Assert.Equal((width, height), (result.Width, result.Height));
            AssertAt(source, result, 0, 0);
        }
    }

    [Fact]
    public async Task TheCanvasMenuEntryAndTheDialogInCanvasModeKeepThePixels()
    {
        using var dir = new TempDir();
        using var workspace = await Open(dir, 55, 75);
        var source = Pattern(55, 75);
        await HeadlessApp.Run(async () =>
        {
            ResizeChoices.Current = new ResizeChoices();
            await Drive(workspace, "img.resize", d =>
            {
                Assert.Equal(1, Field<ComboBox>(d, "ResizeMode").SelectedIndex);
                Field<RadioButton>(d, "ResizeScopeAll").IsChecked = true; // every frame is a deliberate choice
                Field<CheckBox>(d, "ResizeKeep").IsChecked = false;
                Field<NumericUpDown>(d, "ResizeWidth").Value = 80;
                Field<NumericUpDown>(d, "ResizeHeight").Value = 100;
            });
            AssertCanvas(workspace, source, 80, 100);

            // an earlier Resize image choice kept in the options does not turn the canvas entry into a stretch
            workspace.Undo();
            ResizeChoices.Current = new ResizeChoices { Scale = true, Smooth = true };
            await Drive(workspace, "img.resize", d =>
            {
                Field<RadioButton>(d, "ResizeScopeAll").IsChecked = true; // every frame is a deliberate choice
                Field<CheckBox>(d, "ResizeKeep").IsChecked = false;
                Field<NumericUpDown>(d, "ResizeWidth").Value = 80;
                Field<NumericUpDown>(d, "ResizeHeight").Value = 100;
            });
            AssertCanvas(workspace, source, 80, 100);
            ResizeChoices.Current = new ResizeChoices();
        });
    }
}
