using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using G3MStudio.Core.Workspace;
using G3MStudio.Explorer;
using G3MStudio.Localization;

namespace G3MStudio.Tests;

public sealed class ExplorerStickyTests
{
    private static T Named<T>(ExplorerView view, string name) where T : Control => view.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);

    private static Window Open(ExplorerView view, int total)
    {
        Loc.Current.SetLanguage("en");
        var window = new Window { Width = 320, Height = 400, Content = view };
        window.Show();
        using var data = ExplorerModelTests.Generate(total);
        view.Load("game.win", data);
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    [Fact]
    public async Task ScrollingAnOpenCategoryPinsItsHeadingAndAClickFoldsIt()
    {
        await HeadlessApp.Run(() =>
        {
            var view = new ExplorerView();
            var window = Open(view, 2000);
            try
            {
                var list = Named<ListBox>(view, "List");
                var sticky = Named<Border>(view, "Sticky");
                Assert.False(sticky.IsVisible);
                if (!view.Model.Rows.First(r => r.Key == "objects").Expanded) view.Model.Toggle("objects");
                Dispatcher.UIThread.RunJobs();
                var heading = view.Model.Rows.ToList().FindIndex(r => r is { Kind: ExplorerRowKind.Type, Key: "objects" });
                var scroll = list.GetVisualDescendants().OfType<ScrollViewer>().First();
                scroll.Offset = new Avalonia.Vector(0, 24 * (heading + 100));
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                Assert.True(sticky.IsVisible);
                Assert.Equal("objects", view.StickyRow?.Key);
                Assert.Equal("Objects", Named<TextBlock>(view, "StickyText").Text);

                var at = sticky.TranslatePoint(new Avalonia.Point(40, 10), window)!.Value;
                Avalonia.Headless.HeadlessWindowExtensions.MouseDown(window, at, MouseButton.Left);
                Avalonia.Headless.HeadlessWindowExtensions.MouseUp(window, at, MouseButton.Left);
                Dispatcher.UIThread.RunJobs();
                Assert.False(view.Model.Rows.First(r => r.Key == "objects").Expanded);
                Assert.DoesNotContain(view.Model.Rows, r => r is { IsItem: true, Key: "objects" });
            }
            finally { window.Close(); }
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task ScrollingInsideAFolderPinsTheFolderAndASubfolderPinsItself()
    {
        await HeadlessApp.Run(() =>
        {
            var view = new ExplorerView();
            var window = Open(view, 2000);
            try
            {
                var (organizer, top) = Organizer.Empty.AddFolder("Heroes", null);
                (organizer, var sub) = organizer.AddFolder("Bosses", top);
                organizer = organizer.MoveTo(Enumerable.Range(0, 60).Select(i => ResourceTypes.KeyOf("objects", "obj_" + i)), top);
                organizer = organizer.MoveTo(Enumerable.Range(60, 60).Select(i => ResourceTypes.KeyOf("objects", "obj_" + i)), sub);
                view.Model.Organizer = organizer with { FolderView = true };
                Dispatcher.UIThread.RunJobs();
                if (!view.Model.Rows.First(r => r.Key == "objects").Expanded) view.Model.Toggle("objects");
                view.Model.OpenAll(view.Model.Rows.First(r => r.Key == "objects"));
                Dispatcher.UIThread.RunJobs();
                var rows = view.Model.Rows.ToList();
                var list = Named<ListBox>(view, "List");
                var scroll = list.GetVisualDescendants().OfType<ScrollViewer>().First();
                var heroes = rows.FindIndex(r => r.Kind == ExplorerRowKind.Folder && r.Text == "Heroes");
                var bosses = rows.FindIndex(r => r.Kind == ExplorerRowKind.Folder && r.Text == "Bosses");
                Assert.True(heroes >= 0 && bosses > heroes);

                scroll.Offset = new Avalonia.Vector(0, 24 * (heroes + 100));
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                Assert.Equal("Heroes", view.StickyRow?.Text);

                scroll.Offset = new Avalonia.Vector(0, 24 * (bosses + 20));
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                Assert.Equal("Bosses", view.StickyRow?.Text);
            }
            finally { window.Close(); }
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task ARightClickOnThePinnedHeadingOpensItsMenuWithoutMovingTheList()
    {
        await HeadlessApp.Run(() =>
        {
            var view = new ExplorerView();
            var window = Open(view, 2000);
            try
            {
                var list = Named<ListBox>(view, "List");
                if (!view.Model.Rows.First(r => r.Key == "objects").Expanded) view.Model.Toggle("objects");
                Dispatcher.UIThread.RunJobs();
                var heading = view.Model.Rows.ToList().FindIndex(r => r is { Kind: ExplorerRowKind.Type, Key: "objects" });
                var scroll = list.GetVisualDescendants().OfType<ScrollViewer>().First();
                scroll.Offset = new Avalonia.Vector(0, 24 * (heading + 100));
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                var offset = scroll.Offset;
                ExplorerRow? asked = null;
                view.MenuRequested += (row, _) => asked = row;
                var at = Named<Border>(view, "Sticky").TranslatePoint(new Avalonia.Point(40, 10), window)!.Value;
                Avalonia.Headless.HeadlessWindowExtensions.MouseDown(window, at, MouseButton.Right);
                Avalonia.Headless.HeadlessWindowExtensions.MouseUp(window, at, MouseButton.Right);
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                Assert.Equal((ExplorerRowKind.Type, "objects"), (asked?.Kind, asked?.Key));
                Assert.Equal(offset, scroll.Offset);
                Assert.Same(asked, list.SelectedItem); // selected for the menu's commands

                // The next row picked is followed again.
                var last = list.ItemCount - 1;
                list.SelectedIndex = last;
                for (var pass = 0; pass < 3; pass++)
                {
                    Dispatcher.UIThread.RunJobs();
                    window.UpdateLayout();
                }

                Assert.NotNull(list.ContainerFromIndex(last));
            }
            finally { window.Close(); }
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task ARightClickOnThePinnedHeadingDoesNotScrollBackToItWhenItIsJustAbove()
    {
        await HeadlessApp.Run(async () =>
        {
            var view = new ExplorerView();
            var window = Open(view, 2000);
            try
            {
                var list = Named<ListBox>(view, "List");
                if (!view.Model.Rows.First(r => r.Key == "objects").Expanded) view.Model.Toggle("objects");
                Dispatcher.UIThread.RunJobs();
                var heading = view.Model.Rows.ToList().FindIndex(r => r is { Kind: ExplorerRowKind.Type, Key: "objects" });
                var scroll = list.GetVisualDescendants().OfType<ScrollViewer>().First();
                var row = list.GetRealizedContainers().First().Bounds.Height;
                scroll.Offset = new Avalonia.Vector(0, row * (heading + 0.5)); // the heading row is half above the top edge, so it is realized
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                var offset = scroll.Offset;
                var at = Named<Border>(view, "Sticky").TranslatePoint(new Avalonia.Point(40, 10), window)!.Value;
                window.MouseDown(at, MouseButton.Right);
                window.MouseUp(at, MouseButton.Right);
                for (var i = 0; i < 10; i++)
                {
                    await Task.Delay(30, TestContext.Current.CancellationToken);
                    Dispatcher.UIThread.RunJobs();
                    window.UpdateLayout();
                }

                Assert.Equal((ExplorerRowKind.Type, "objects"), ((list.SelectedItem as ExplorerRow)?.Kind, (list.SelectedItem as ExplorerRow)?.Key));
                Assert.Equal(offset, scroll.Offset);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public async Task ARowSelectedFromAboveIsNotLeftUnderThePinnedHeading()
    {
        await HeadlessApp.Run(async () =>
        {
            var view = new ExplorerView();
            var window = Open(view, 2000);
            try
            {
                var list = Named<ListBox>(view, "List");
                var sticky = Named<Border>(view, "Sticky");
                if (!view.Model.Rows.First(r => r.Key == "objects").Expanded) view.Model.Toggle("objects");
                Dispatcher.UIThread.RunJobs();
                var scroll = list.GetVisualDescendants().OfType<ScrollViewer>().First();
                var at = view.Model.Rows.ToList().FindIndex(r => r is { IsItem: true, Key: "objects" } && r.Text == "obj_300");
                scroll.Offset = new Avalonia.Vector(0, 26 * (at + 2)); // the row is two rows above the top edge, close enough to be in the list already
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                view.Select("objects:obj_300");
                for (var i = 0; i < 10; i++)
                {
                    await Task.Delay(30, TestContext.Current.CancellationToken);
                    Dispatcher.UIThread.RunJobs();
                    window.UpdateLayout();
                }

                Assert.True(sticky.IsVisible);
                var top = list.ContainerFromIndex(list.SelectedIndex)!.TranslatePoint(default, list)!.Value.Y;
                Assert.True(top >= sticky.Bounds.Height - 1, $"row top {top}, heading {sticky.Bounds.Height}");
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public async Task AScrollByTheUserIsNotMovedBackByTheChecksOfTheLastReveal()
    {
        await HeadlessApp.Run(async () =>
        {
            var view = new ExplorerView();
            var window = Open(view, 2000);
            try
            {
                var list = Named<ListBox>(view, "List");
                if (!view.Model.Rows.First(r => r.Key == "objects").Expanded) view.Model.Toggle("objects");
                Dispatcher.UIThread.RunJobs();
                var scroll = list.GetVisualDescendants().OfType<ScrollViewer>().First();
                var at = view.Model.Rows.ToList().FindIndex(r => r is { IsItem: true, Key: "objects" } && r.Text == "obj_300");
                scroll.Offset = new Avalonia.Vector(0, 26 * (at + 2));
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                view.Select("objects:obj_300");
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                window.MouseWheel(new Point(100, 200), new Vector(0, -1)); // the user scrolls down: the selected row goes under the heading
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                var offset = scroll.Offset.Y;
                for (var i = 0; i < 12; i++)
                {
                    await Task.Delay(30, TestContext.Current.CancellationToken);
                    Dispatcher.UIThread.RunJobs();
                    window.UpdateLayout();
                }

                Assert.Equal(offset, scroll.Offset.Y);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public async Task TheSelectionCountShowsWhileRowsAreSelectedAndHidesAtZero()
    {
        await HeadlessApp.Run(() =>
        {
            var view = new ExplorerView();
            var window = Open(view, 40);
            try
            {
                var list = Named<ListBox>(view, "List");
                var text = Named<TextBlock>(view, "SelectedCount");
                view.Model.Toggle("objects");
                Dispatcher.UIThread.RunJobs();
                Assert.False(text.IsVisible);
                list.SelectedItems!.Add(list.Items.OfType<ExplorerRow>().First(r => r.IsItem));
                list.SelectedItems.Add(list.Items.OfType<ExplorerRow>().Last(r => r.IsItem));
                Assert.True(text.IsVisible);
                Assert.Equal("2 selected", text.Text);
                view.OptionsFolded = true; // the strip keeps the count
                Assert.True(text.IsEffectivelyVisible);
                list.SelectedItems.Clear();
                Assert.False(text.IsVisible);
            }
            finally { window.Close(); }
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task EveryCategoryHeadingShowsItsTypeIconWhenThumbnailsAreOn()
    {
        await HeadlessApp.Run(() =>
        {
            var view = new ExplorerView();
            var window = Open(view, 40);
            try
            {
                Named<ToggleButton>(view, "ThumbToggle").IsChecked = true;
                Dispatcher.UIThread.RunJobs();
                var list = Named<ListBox>(view, "List");
                var seen = 0;
                foreach (var container in list.GetRealizedContainers())
                {
                    if (container.DataContext is not ExplorerRow { Kind: ExplorerRowKind.Type } row) continue;
                    var thumb = container.GetVisualDescendants().OfType<Image>().Single(i => i.Name == "Thumb");
                    Assert.True(thumb.IsVisible, row.Key);
                    Assert.IsType<Avalonia.Media.DrawingImage>(thumb.Source);
                    seen++;
                }

                Assert.True(seen > 0);
            }
            finally { window.Close(); }
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task ThePinnedHeadingIsTheOneOwningTheRowAtTheTopAtEveryScrollOffset()
    {
        await HeadlessApp.Run(() =>
        {
            var view = new ExplorerView();
            var window = Open(view, 2000);
            try
            {
                var list = Named<ListBox>(view, "List");
                var sticky = Named<Border>(view, "Sticky");
                foreach (var key in view.Model.Rows.Where(r => r.Kind == ExplorerRowKind.Type && !r.Expanded).Select(r => r.Key).ToList()) view.Model.Toggle(key);
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                var scroll = list.GetVisualDescendants().OfType<ScrollViewer>().First();
                var rows = list.Items.OfType<ExplorerRow>().ToList();
                var bad = new List<string>();
                static bool Heading(ExplorerRow r) => r.Kind is ExplorerRowKind.Type or ExplorerRowKind.Group;
                for (double y = 0; y < scroll.Extent.Height - scroll.Viewport.Height; y += 7)
                {
                    scroll.Offset = new Avalonia.Vector(0, y);
                    Dispatcher.UIThread.RunJobs();
                    window.UpdateLayout();
                    // The row at the top edge decides: its heading is pinned, unless it is a heading flush at the top.
                    var found = list.GetRealizedContainers().Select(c => (Index: list.IndexFromContainer(c), c.Bounds)).Where(t => t.Bounds.Bottom > y).OrderBy(t => t.Index).Take(1).ToList();
                    if (found.Count == 0) continue;
                    var top = found[0].Index;
                    var owner = rows.FindLastIndex(top, Heading);
                    var flush = owner == top && found[0].Bounds.Y - y >= -0.01;
                    var expected = owner < 0 || flush ? null : rows[owner].Key;
                    var shown = sticky.IsVisible ? view.StickyRow?.Key : null;
                    if (expected != shown) bad.Add($"{y}: top row {top} ({rows[top].Text}) expects {expected ?? "none"}, shows {shown ?? "none"} vis={sticky.IsVisible} sr={view.StickyRow?.Key} off={scroll.Offset.Y}");
                    // A pinned copy never reaches over the next heading.
                    var next = rows.FindIndex(top + 1, Heading);
                    if (shown is not null && next >= 0 && list.ContainerFromIndex(next) is { } below && sticky.TranslatePoint(new Avalonia.Point(0, sticky.Bounds.Height), list)!.Value.Y > below.TranslatePoint(default, list)!.Value.Y + 1)
                        bad.Add($"{y}: the copy of {shown} reaches over {rows[next].Text}");
                }

                Assert.True(bad.Count == 0, string.Join("; ", bad.Take(8)));
            }
            finally { window.Close(); }
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task AFilterTypedAfterScrollingDeepShowsItsMatchesFromTheTop()
    {
        await HeadlessApp.Run(() =>
        {
            var view = new ExplorerView();
            var window = Open(view, 2000);
            try
            {
                var list = Named<ListBox>(view, "List");
                var scroll = list.GetVisualDescendants().OfType<ScrollViewer>().First();
                foreach (var key in view.Model.Rows.Where(r => r.Kind == ExplorerRowKind.Type && !r.Expanded).Select(r => r.Key).ToList()) view.Model.Toggle(key);
                Dispatcher.UIThread.RunJobs();
                scroll.Offset = new Avalonia.Vector(0, scroll.Extent.Height - scroll.Viewport.Height);
                Dispatcher.UIThread.RunJobs();
                Assert.True(scroll.Offset.Y > 1000);
                Named<TextBox>(view, "FilterBox").Text = "obj_300";
                view.FlushFilter();
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(0, scroll.Offset.Y);
                Assert.NotEmpty(list.GetRealizedContainers());
            }
            finally { window.Close(); }
            return Task.CompletedTask;
        });
    }

    [Theory]
    [InlineData(50, false)]
    [InlineData(100, false)]
    [InlineData(100, true)]
    [InlineData(150, true)]
    [InlineData(250, false)]
    [InlineData(250, true)]
    public async Task ThePinnedHeadingSitsFlushAtTheTopAndIsAsTallAsTheRowsAtEveryZoom(double zoom, bool icons)
    {
        await HeadlessApp.Run(() =>
        {
            var view = new ExplorerView();
            var window = Open(view, 2000);
            try
            {
                Named<ToggleButton>(view, "ThumbToggle").IsChecked = icons;
                view.SetZoom(zoom);
                var list = Named<ListBox>(view, "List");
                var sticky = Named<Border>(view, "Sticky");
                if (!view.Model.Rows.First(r => r.Key == "objects").Expanded) view.Model.Toggle("objects");
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                var scroll = list.GetVisualDescendants().OfType<ScrollViewer>().First();
                var heading = view.Model.Rows.ToList().FindIndex(r => r is { Kind: ExplorerRowKind.Type, Key: "objects" });
                scroll.Offset = new Avalonia.Vector(0, list.GetRealizedContainers().First().Bounds.Height * (heading + 100));
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                Assert.True(sticky.IsVisible);
                Assert.Equal(0, sticky.TranslatePoint(default, list)!.Value.Y, 0.5); // no strip of the rows above shows over the pinned heading
                Assert.Equal(0, sticky.Margin.Top);
                var rowHeight = list.GetRealizedContainers().Max(c => c.Bounds.Height);
                Assert.True(sticky.Bounds.Height >= rowHeight - 0.5, $"pinned {sticky.Bounds.Height}, rows {rowHeight}");
            }
            finally { window.Close(); }
            return Task.CompletedTask;
        });
    }
}
