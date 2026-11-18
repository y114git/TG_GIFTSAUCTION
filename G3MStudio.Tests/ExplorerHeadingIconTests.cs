using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using Avalonia.VisualTree;
using G3MStudio.Core.Workspace;
using G3MStudio.Explorer;
using G3MStudio.Localization;

namespace G3MStudio.Tests;

public sealed class ExplorerHeadingIconTests
{
    [Fact]
    public async Task WithThumbnailsOnEveryGroupAndTypeHeadingAndTheStickyHeadingShowAnIcon()
    {
        await HeadlessApp.Run(() =>
        {
            Loc.Current.SetLanguage("en");
            var view = new ExplorerView();
            var window = new Window { Width = 320, Height = 3000, Content = view };
            window.Show();
            try
            {
                using var data = ExplorerModelTests.Generate(2000);
                view.Load("game.win", data);
                view.GetVisualDescendants().OfType<ToggleButton>().First(t => t.Name == "ThumbToggle").IsChecked = true;
                // Open the groups (Quick and data), so the headings under them are listed too.
                foreach (var key in view.Model.Rows.Where(r => r.Kind == ExplorerRowKind.Type && r.Expanded).Select(r => r.Key).ToList()) view.Model.Toggle(key);
                foreach (var key in view.Model.Rows.Where(r => r.Kind == ExplorerRowKind.Group && !r.Expanded).Select(r => r.Key).ToList()) view.Model.Toggle(key);
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();

                var headings = view.Model.Rows.Where(r => r.Kind is ExplorerRowKind.Group or ExplorerRowKind.Type).ToList();
                Assert.Contains(headings, r => r.Kind == ExplorerRowKind.Group);
                Assert.True(headings.Count > 6);
                var shown = view.GetVisualDescendants().OfType<ListBoxItem>().Where(i => i.DataContext is ExplorerRow { Kind: ExplorerRowKind.Group or ExplorerRowKind.Type }).ToList();
                Assert.Equal(headings.Count, shown.Count);
                foreach (var item in shown)
                {
                    var thumb = item.GetVisualDescendants().OfType<Image>().First(x => x.Name == "Thumb");
                    Assert.True(thumb.IsVisible && thumb.Source is not null, $"no icon on the heading {((ExplorerRow)item.DataContext!).Key}");
                }

                window.Height = 400;
                if (!view.Model.Rows.First(r => r.Key == "objects").Expanded) view.Model.Toggle("objects");
                Dispatcher.UIThread.RunJobs();
                var sticky = view.GetVisualDescendants().OfType<Image>().First(x => x.Name == "StickyThumb");
                var list = view.GetVisualDescendants().OfType<ListBox>().First(x => x.Name == "List");
                var objects = view.Model.Rows.ToList().FindIndex(r => r is { Kind: ExplorerRowKind.Type, Key: "objects" });
                list.GetVisualDescendants().OfType<ScrollViewer>().First().Offset = new Avalonia.Vector(0, 24 * (objects + 100));
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                Assert.NotNull(view.StickyRow);
                Assert.True(sticky.IsVisible && sticky.Source is not null);
            }
            finally { window.Close(); }

            return Task.CompletedTask;
        });
    }
}
