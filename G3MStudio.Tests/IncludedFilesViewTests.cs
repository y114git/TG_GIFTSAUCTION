using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using G3MStudio.Core.Workspace;
using G3MStudio.Editor;
using Window = Avalonia.Controls.Window;

namespace G3MStudio.Tests;

public sealed class IncludedFilesViewTests
{
    [Fact]
    public async Task ScrollingTheIncludedFilesListRecyclesRowsWithoutBuildingForNoFile()
    {
        await HeadlessApp.Run(() =>
        {
            var files = Enumerable.Range(0, 400).Select(i => new IncludedFile($"lang/file{i}.json", 1024 * i, new DateTime(2026, 1, 1))).ToList();
            var list = new ListBox { ItemsSource = files, ItemTemplate = IncludedFilesView.RowTemplate(), Height = 120, Width = 500 };
            var window = new Window { Width = 520, Height = 160, Content = list };
            window.Show();
            try
            {
                Dispatcher.UIThread.RunJobs();
                var scroll = list.GetVisualDescendants().OfType<ScrollViewer>().First();
                for (var step = 0; step < 40; step++)
                {
                    scroll.Offset = new Avalonia.Vector(0, step * 150);
                    window.UpdateLayout();
                    Dispatcher.UIThread.RunJobs();
                }

                Assert.True(scroll.Offset.Y > 0);
                Assert.NotNull(IncludedFilesView.RowTemplate().Build(null));
            }
            finally
            {
                window.Close();
            }

            return Task.CompletedTask;
        });
    }
}
