using Avalonia.Threading;
using Avalonia.VisualTree;
using Dock.Avalonia.Controls;
using Dock.Model.Core;

namespace G3MStudio.Tests;

/// <summary>A tool tab whose strip panel is out of step with the list (the macOS fuzz run) closes without an exception on the UI thread.</summary>
public sealed class ToolTabCloseTests
{
    [Fact]
    public async Task ClosingAToolTabWhoseStripPanelLostItsChildrenDoesNotThrow()
    {
        using var dir = new TempDir();
        await HeadlessApp.Run(async () =>
        {
            using var probe = new SweepProbe();
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                await window.OpenAsync(Path.Combine(Corpus.Fixtures, "deltarune", "data.win"));
                await CommandSweep.Pump(3);
                var strip = window.GetVisualDescendants().OfType<ToolTabStrip>().First(s => s.ItemsPanelRoot is { Children.Count: >= 2 });
                var tool = strip.Items.Cast<object>().OfType<IDockable>().Last();
                strip.ItemsPanelRoot!.Children.Clear(); // the panel no longer matches the list

                var ex = Record.Exception(() => window.Docking.Factory.CloseDockable(tool));

                Assert.Null(ex);
                await CommandSweep.Pump(3);
                Dispatcher.UIThread.RunJobs();
                Assert.Empty(probe.Errors);
            }
            finally
            {
                window.Close();
            }
        });
    }
}
