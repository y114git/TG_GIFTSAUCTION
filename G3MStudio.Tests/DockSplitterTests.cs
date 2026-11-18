using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Dock.Avalonia.Controls;
using Dock.Controls.ProportionalStackPanel;
using G3MStudio.Docking;

namespace G3MStudio.Tests;

public sealed class DockSplitterTests
{
    [Fact]
    public Task DockSplittersAreSixPixelsWideAndTurnGreenUnderThePointer() => HeadlessApp.Run(() =>
    {
        var dock = new StudioDock(editor: tab => new TextBlock { Text = tab.Key });
        var control = new DockControl();
        dock.Attach(control);
        var window = new Window { Width = 1000, Height = 700, Content = control };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        try
        {
            var splitters = window.GetVisualDescendants().OfType<ProportionalStackPanelSplitter>().ToList();
            Assert.Equal(3, splitters.Count); // left, right and bottom
            Assert.All(splitters, s => Assert.True(s.Thickness >= 6));
            var splitter = splitters[0];
            var rest = ((ISolidColorBrush)splitter.Background!).Color;
            window.MouseMove(splitter.TranslatePoint(new Point(splitter.Bounds.Width / 2, splitter.Bounds.Height / 2), window)!.Value);
            Dispatcher.UIThread.RunJobs();
            Assert.NotEqual(rest, ((ISolidColorBrush)splitter.Background!).Color);
        }
        finally
        {
            window.Close();
        }
    });

    [Fact]
    public Task APreviewCanvasHasADragHandleThatResizesIt() => HeadlessApp.Run(() =>
    {
        var system = new G3MLib.DataFile.Models.GameMakerParticleSystem();
        var view = new G3MStudio.Editor.ParticlePreviewView(system);
        var window = new Window { Width = 600, Height = 600, Content = view };
        window.Show();
        var canvas = view.Children[0];
        var handle = view.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "ResizeHandle_preview.particles");
        var before = canvas.Height;
        G3MStudio.Editor.DragHandle.Resize(canvas, "preview.particles", before + 50);
        Assert.Equal(before + 50, canvas.Height);
        Assert.NotNull(handle);
        window.Close();
    });
}
