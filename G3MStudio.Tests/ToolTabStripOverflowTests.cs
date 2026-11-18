using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Dock.Avalonia.Controls;
using G3MStudio.Localization;

namespace G3MStudio.Tests;

/// <summary>A panel tab strip too narrow for its tabs (a long language in a narrow window) scrolls with arrows, so no tab is out of reach.</summary>
public sealed class ToolTabStripOverflowTests
{
    [Theory]
    [InlineData("ru", 900)]
    [InlineData("es", 760)]
    public async Task AToolTabStripThatOverflowsShowsItsScrollArrowAndReachesTheLastTab(string language, int width)
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir);
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                await window.OpenAsync(path);
                Loc.Current.SetLanguage(language);
                window.Width = width;
                window.Height = 800;
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                var overflowing = 0;
                foreach (var strip in window.DockHost.GetVisualDescendants().OfType<ToolTabStrip>())
                {
                    var scroll = strip.GetVisualDescendants().OfType<ScrollViewer>().First();
                    if (scroll.Extent.Width <= scroll.Viewport.Width + 0.5) continue;
                    overflowing++;
                    Assert.Contains(scroll.GetVisualDescendants().OfType<RepeatButton>(), b => b.IsVisible && b.Classes.Contains("tabScrollRight"));
                    scroll.Offset = new Vector(scroll.Extent.Width, 0);
                    window.UpdateLayout();
                    var last = strip.GetVisualDescendants().OfType<ToolTabStripItem>().Last();
                    Assert.True(last.TranslatePoint(new Point(last.Bounds.Width, 0), scroll)!.Value.X <= scroll.Bounds.Width + 1, "the last tab can be scrolled into view");
                }

                Assert.True(overflowing > 0, "the window is narrow enough for a strip to overflow");
            }
            finally { window.Close(); }
        });
    }
}
