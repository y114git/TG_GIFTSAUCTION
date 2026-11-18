using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using Avalonia.VisualTree;
using G3MStudio.Explorer;
using G3MStudio.Localization;

namespace G3MStudio.Tests;

/// <summary>The Explorer rows follow each other from the top down at every zoom value, also when the zoom changes while the list is scrolled (item 44).</summary>
public sealed class ExplorerZoomTests
{
    private static readonly string[] OpenTypes = ["sprites", "objects", "code"];
    private static readonly string[] DockedTypes = ["objects", "scripts"];
    private static readonly bool[] ThumbnailStates = [false, true];
    private static readonly double[] Offsets = [0.0, 3000.0, 3000.0];
    private static readonly int[] Jumps = [250, 50, 183, 61, 250, 100];

    private static T Named<T>(Control view, string name) where T : Control => view.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);

    // The realized rows must follow each other from the top down without a gap larger than rounding and without overlap, and no picture is taller than its row.
    private static void AssertRowsStack(ListBox list, string where)
    {
        var rows = list.GetRealizedContainers().OfType<ListBoxItem>().Select(c => (Index: list.IndexFromContainer(c), c.Bounds)).OrderBy(r => r.Index).ToList();
        Assert.True(rows.Count > 0, $"{where}: no row is realized, so there is nothing to check"); // an empty list would pass every loop below
        for (var i = 1; i < rows.Count; i++)
        {
            Assert.True(rows[i].Bounds.Y >= rows[i - 1].Bounds.Bottom - 0.5, $"{where}: row {rows[i].Index} at {rows[i].Bounds} overlaps row {rows[i - 1].Index} at {rows[i - 1].Bounds}");
            if (rows[i].Index == rows[i - 1].Index + 1) Assert.True(rows[i].Bounds.Y <= rows[i - 1].Bounds.Bottom + 0.5, $"{where}: gap before row {rows[i].Index}");
        }

        foreach (var item in list.GetRealizedContainers().OfType<ListBoxItem>())
            foreach (var thumb in item.GetVisualDescendants().OfType<Image>().Where(i => i.IsVisible))
                Assert.True(thumb.Bounds.Height <= item.Bounds.Height + 0.5, $"{where}: picture {thumb.Bounds} is taller than its row {item.Bounds}");
    }

    private static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
    }

    [Fact]
    public async Task EveryZoomValueKeepsTheRowsInOrderWithoutOverlapAlsoWhenChangedWhileScrolled()
    {
        await HeadlessApp.Run(() =>
        {
            Loc.Current.SetLanguage("en");
            var view = new ExplorerView();
            var window = new Window { Width = 320, Height = 600, Content = view };
            window.Show();
            using var data = ExplorerModelTests.Generate(2000);
            view.Load("game.win", data);
            try
            {
                var list = Named<ListBox>(view, "List");
                foreach (var key in OpenTypes)
                    if (!view.Model.Rows.First(r => r.Key == key).Expanded) view.Model.Toggle(key);
                Settle(window);
                var scroll = list.GetVisualDescendants().OfType<ScrollViewer>().First();
                foreach (var thumbnails in ThumbnailStates)
                {
                    Named<ToggleButton>(view, "ThumbToggle").IsChecked = thumbnails;
                    // Steps of 7 percent cover the odd values the slider reaches while dragged.
                    for (var percent = 50; percent <= 250; percent += 7)
                    {
                        foreach (var offset in Offsets)
                        {
                            view.SetZoom(percent);
                            Settle(window);
                            scroll.Offset = new Avalonia.Vector(0, offset * percent / 100.0);
                            Settle(window);
                            AssertRowsStack(list, $"thumbnails {thumbnails}, zoom {percent}, offset {offset}");
                        }
                    }

                    foreach (var percent in Jumps)
                    {
                        view.SetZoom(percent);
                        Settle(window);
                        AssertRowsStack(list, $"thumbnails {thumbnails}, jump to {percent}");
                    }
                }
            }
            finally { window.Close(); }
            return Task.CompletedTask;
        });
    }

    [Theory]
    [InlineData(100)]
    [InlineData(175)]
    [InlineData(250)]
    public async Task TheChevronSlotGrowsWithTheZoomSoTheGlyphNeverTouchesTheHeading(int percent)
    {
        await HeadlessApp.Run(() =>
        {
            Loc.Current.SetLanguage("en");
            var view = new ExplorerView();
            var window = new Window { Width = 320, Height = 600, Content = view };
            window.Show();
            using var data = ExplorerModelTests.Generate(50);
            view.Load("game.win", data);
            try
            {
                view.SetZoom(percent);
                Settle(window);
                var list = Named<ListBox>(view, "List");
                var chevrons = list.GetVisualDescendants().OfType<TextBlock>().Where(t => t.Name == "RowChevron" && !string.IsNullOrEmpty(t.Text)).ToList();
                Assert.NotEmpty(chevrons);
                foreach (var chevron in chevrons)
                {
                    var glyph = new TextBlock { Text = chevron.Text, FontSize = chevron.FontSize, FontFamily = chevron.FontFamily, FontWeight = chevron.FontWeight };
                    glyph.Measure(Avalonia.Size.Infinity);
                    Assert.True(chevron.Bounds.Width - glyph.DesiredSize.Width >= 1.5, $"zoom {percent}: slot {chevron.Bounds.Width} for a glyph {glyph.DesiredSize.Width} wide");
                }
            }
            finally { window.Close(); }
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task AFocusedRowScrolledAwayIsDressedAgainWhenTheZoomOrTheThumbnailsChange()
    {
        await HeadlessApp.Run(() =>
        {
            Loc.Current.SetLanguage("en");
            var view = new ExplorerView();
            var window = new Window { Width = 320, Height = 600, Content = view };
            window.Show();
            using var data = ExplorerModelTests.Generate(2000);
            view.Load("game.win", data);
            try
            {
                var list = Named<ListBox>(view, "List");
                if (!view.Model.Rows.First(r => r.Key == "sprites").Expanded) view.Model.Toggle("sprites");
                Settle(window);
                var scroll = list.GetVisualDescendants().OfType<ScrollViewer>().First();
                Assert.True(list.ContainerFromIndex(5)?.Focus());
                scroll.Offset = new Avalonia.Vector(0, 3000);
                Settle(window);
                Named<ToggleButton>(view, "ThumbToggle").IsChecked = true;
                view.SetZoom(200);
                Settle(window);
                scroll.Offset = new Avalonia.Vector(0, 0);
                Settle(window);
                var heights = list.GetVisualDescendants().OfType<ListBoxItem>().Select(i => i.MinHeight).Distinct().ToList();
                Assert.Single(heights);
            }
            finally { window.Close(); }
            return Task.CompletedTask;
        });
    }

    [Theory]
    [InlineData(125, 1.0)]
    [InlineData(150, 1.5)]
    [InlineData(175, 1.75)]
    public async Task TheDockedExplorerAtAScaledUiKeepsItsRowsInOrderWhileTheZoomMoves(int uiScale, double renderScale)
    {
        using var dir = new TempDir();
        await HeadlessApp.Run(async () =>
        {
            Loc.Current.SetLanguage("en");
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                await window.OpenAsync(TestData.WriteSample(dir, objects: 600, withCode: true));
                window.Workspaces.Settings.Update(x => x with { UiScale = uiScale });
                Avalonia.Headless.HeadlessWindowExtensions.SetRenderScaling(window, renderScale);
                Settle(window);
                var view = window.Explorer;
                var list = Named<ListBox>(view, "List");
                foreach (var key in DockedTypes)
                    if (!view.Model.Rows.First(r => r.Key == key).Expanded) view.Model.Toggle(key);
                Named<ToggleButton>(view, "ThumbToggle").IsChecked = true;
                Settle(window);
                var scroll = list.GetVisualDescendants().OfType<ScrollViewer>().First();
                for (var percent = 50; percent <= 250; percent += 13)
                {
                    view.SetZoom(percent);
                    window.UpdateLayout(); // the layout pass right after the change, as the slider does it
                    AssertRowsStack(list, $"scale {uiScale}, zoom {percent}");
                    ExplorerDockedLayoutTests.AssertSettled(window, $"scale {uiScale}, zoom {percent}");
                    scroll.Offset = new Avalonia.Vector(0, 40 * percent);
                    Settle(window);
                    AssertRowsStack(list, $"scale {uiScale}, zoom {percent}, scrolled");
                }
            }
            finally { window.Workspaces.Close(); window.Close(); }
        });
    }
}
