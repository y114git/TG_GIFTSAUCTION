using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit;
using G3MStudio.Editor;
using Window = Avalonia.Controls.Window;

namespace G3MStudio.Tests;

public sealed class EditorZoomTests
{
    private static readonly int[] Sizes = [20, 28, 10];

    private static void Pump(Window window)
    {
        for (var i = 0; i < 5; i++)
        {
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
        }
    }

    [Fact]
    public async Task ZoomingTheCodeKeepsTheLineAtTheTopInPlace()
    {
        var before = EditorOptions.FontSize;
        await HeadlessApp.Run(() =>
        {
            var editor = new TextEditor { Text = string.Join('\n', Enumerable.Range(1, 800).Select(i => $"line {i}")) };
            var window = new Window { Width = 600, Height = 400, Content = editor };
            window.Show();
            try
            {
                EditorOptions.Set(13, false, true);
                EditorOptions.ApplyTo(editor, FontFamily.Default);
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                var view = editor.TextArea.TextView;
                for (var i = 0; i < 20 && editor.ViewportHeight <= 0; i++)
                {
                    window.UpdateLayout();
                    Dispatcher.UIThread.RunJobs();
                }

                Assert.True(editor.ViewportHeight > 0);
                editor.GetVisualDescendants().OfType<Avalonia.Controls.ScrollViewer>().First().Offset = new Avalonia.Vector(0, view.GetVisualTopByDocumentLine(300));
                Pump(window);
                Assert.True(view.GetDocumentLineByVisualTop(editor.VerticalOffset)?.LineNumber is >= 299 and <= 301, $"offset {editor.VerticalOffset} viewport {editor.ViewportHeight} visualTop300 {view.GetVisualTopByDocumentLine(300)} extent {editor.ExtentHeight} valid {view.VisualLinesValid}");

                foreach (var size in Sizes)
                {
                    EditorOptions.Set(size, false, true);
                    EditorOptions.ApplyTo(editor, FontFamily.Default);
                    Pump(window);
                    Assert.InRange(view.GetDocumentLineByVisualTop(editor.VerticalOffset)?.LineNumber ?? 0, 299, 301);
                }
            }
            finally
            {
                EditorOptions.Set(before, false, true);
                window.Close();
            }

            return Task.CompletedTask;
        });
    }
}
