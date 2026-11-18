using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using Avalonia.VisualTree;
using G3MStudio.Core.Settings;
using G3MStudio.Core.Workspace;
using G3MStudio.Explorer;
using G3MStudio.Localization;

namespace G3MStudio.Tests;

/// <summary>The size of the Explorer pictures: a smaller default, a Settings option, and the zoom slider scaling both (item 45).</summary>
public sealed class ExplorerThumbnailSizeTests
{
    [Fact]
    public void TheDefaultIsAboutOneAndAHalfToTwoTimesSmallerThanTheOldRowPicture()
    {
        // The old picture was the row of 26 px times 1.6, less a margin: 37.6 px.
        var old = 26 * 1.6 - 4;
        Assert.InRange(old / AppSettings.DefaultThumbnailSize, 1.5, 2.0);
        Assert.Equal(AppSettings.DefaultThumbnailSize, new AppSettings().ExplorerThumbnailSize);
    }

    [Fact]
    public void AFileFromBeforeTheOptionGetsTheDefaultAndAHandEditedValueIsClamped()
    {
        using var dir = new TempDir();
        var userData = new UserData(dir.File("user"));
        File.WriteAllText(userData.Combine("settings.json"), "{ \"codeFontSize\": 14 }");
        using (var old = new SettingsStore(userData)) Assert.Equal(AppSettings.DefaultThumbnailSize, old.Current.ExplorerThumbnailSize);
        File.WriteAllText(userData.Combine("settings.json"), "{ \"explorerThumbnailSize\": 4000 }");
        using (var big = new SettingsStore(userData)) Assert.Equal(96, big.Current.ExplorerThumbnailSize);
        File.WriteAllText(userData.Combine("settings.json"), "{ \"explorerThumbnailSize\": 2 }");
        using (var small = new SettingsStore(userData)) Assert.Equal(16, small.Current.ExplorerThumbnailSize);
    }

    private static Image ThumbOf(ListBox list) => ItemRow(list).GetVisualDescendants().OfType<Image>().First(x => x.Name == "Thumb");

    // The virtualizing panel realizes rows over several layout passes after a zoom, and how many it needs differs between machines.
    private static ListBoxItem ItemRow(ListBox list)
    {
        for (var pass = 0; pass < 20 && !list.GetRealizedContainers().OfType<ListBoxItem>().Any(i => i.DataContext is ExplorerRow { IsItem: true }); pass++)
        {
            Dispatcher.UIThread.RunJobs();
            list.UpdateLayout();
        }

        return list.GetRealizedContainers().OfType<ListBoxItem>().First(i => i.DataContext is ExplorerRow { IsItem: true });
    }

    // After a zoom or a toggle the list realizes its rows on later dispatcher jobs; a slow machine may need a few milliseconds, so wait for them instead of counting passes.
    private static async Task RowsRealized(ListBox list)
    {
        bool Ready()
        {
            Dispatcher.UIThread.RunJobs();
            list.UpdateLayout();
            return list.GetRealizedContainers().OfType<ListBoxItem>().Any(i => i.DataContext is ExplorerRow { IsItem: true });
        }

        // Rows are tall at a large zoom, so the first items may lie below the realized part of the list
        if (list.ItemsSource is System.Collections.IList rows)
            for (var at = 0; at < rows.Count; at++)
                if (rows[at] is ExplorerRow { IsItem: true } first)
                {
                    list.ScrollIntoView(first);
                    break;
                }

        for (var i = 0; i < 200 && !Ready(); i++) await Task.Delay(25);
        Assert.True(Ready(), $"no item row: {list.GetRealizedContainers().Count()} containers, {list.GetRealizedContainers().Count(c => c.DataContext is ExplorerRow)} rows, list {list.Bounds}, items {(list.ItemsSource as System.Collections.ICollection)?.Count}, kinds {string.Join(",", list.GetRealizedContainers().Take(6).Select(c => c.DataContext?.GetType().Name))}");
    }

    [Theory]
    [InlineData(22, 100, 26, 22)]
    [InlineData(22, 200, 52, 44)]
    [InlineData(22, 50, 13, 11)]
    [InlineData(48, 100, 52, 48)]
    [InlineData(16, 100, 26, 16)]
    public async Task TheRowIsAsTallAsThePictureAndAMarginAndTheZoomScalesBoth(int size, int zoom, double row, double picture)
    {
        await HeadlessApp.Run(async () =>
        {
            Loc.Current.SetLanguage("en");
            var view = new ExplorerView { ThumbnailSize = size };
            var window = new Window { Width = 400, Height = 600, Content = view };
            window.Show();
            using var data = ExplorerModelTests.Generate(40);
            view.Load("t.win", data);
            try
            {
                view.Model.Toggle("sprites");
                view.GetVisualDescendants().OfType<ToggleButton>().First(t => t.Name == "ThumbToggle").IsChecked = true;
                view.SetZoom(zoom);
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                var list = view.GetVisualDescendants().OfType<ListBox>().Single(l => l.Name == "List");
                await RowsRealized(list);
                Assert.Equal(row, ItemRow(list).Bounds.Height, 0.6);
                Assert.Equal(picture, ThumbOf(list).Bounds.Height, 0.6);
                // Without pictures the row is the theme row, scaled by the zoom, whatever the size is.
                view.GetVisualDescendants().OfType<ToggleButton>().First(t => t.Name == "ThumbToggle").IsChecked = false;
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                await RowsRealized(list);
                Assert.Equal(26 * zoom / 100.0, ItemRow(list).Bounds.Height, 0.6);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public async Task TheSettingsPageOptionSizesTheExplorerPicturesAtOnce()
    {
        using var dir = new TempDir();
        await HeadlessApp.Run(() =>
        {
            Loc.Current.SetLanguage("en");
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                Assert.Equal(AppSettings.DefaultThumbnailSize, window.Explorer.ThumbnailSize);
                window.Registry.Execute("settings.open");
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                var box = window.GetVisualDescendants().OfType<NumericUpDown>().Single(c => c.Name == "ExplorerThumbnailSize");
                Assert.Equal(AppSettings.DefaultThumbnailSize, (int)box.Value!);
                box.Value = 40;
                Assert.Equal(40, window.Workspaces.Settings.Current.ExplorerThumbnailSize);
                Assert.Equal(40, window.Explorer.ThumbnailSize);
            }
            finally { window.Close(); }
            return Task.CompletedTask;
        });
    }
}
