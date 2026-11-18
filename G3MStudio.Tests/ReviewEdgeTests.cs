using Avalonia.Controls;
using Avalonia;
using Avalonia.Controls.Presenters;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using G3MStudio.Core.Notifications;
using G3MStudio.Core.Workspace;
using G3MStudio.Editor;
using G3MStudio.Explorer;
using G3MStudio.Localization;
using G3MStudio.Notifications;
using G3MStudio.Theme;

namespace G3MStudio.Tests;

public sealed class ReviewEdgeTests
{
    private static readonly string[] Items = ["a", "b"];

    [Fact]
    public void ARangeAboveTheLimitIsNamedTooLongNotInvalid()
    {
        Loc.Current.SetLanguage("en");
        Assert.True(CharacterInput.IsTooLong("0-5000"));
        Assert.False(CharacterInput.IsTooLong("A-Z"));
        Assert.False(CharacterInput.IsTooLong("Z-A"));
        Assert.False(CharacterInput.IsTooLong("nonsense"));
        Assert.Contains("2048", CharacterHint.Text("0-5000", true), StringComparison.Ordinal);
        Assert.Equal(Loc.Current["fonts.charInvalid"], CharacterHint.Text("Z-A", true));
    }

    [Fact]
    public Task DeleteReportsATooLongRange() => HeadlessApp.Run(() =>
    {
        Loc.Current.SetLanguage("en");
        var workspace = ThumbnailTests.Generate();
        var font = workspace.Data.Fonts[0];
        var said = new List<string>();
        var view = new FontGlyphsView(font, workspace) { Notify = (_, text) => said.Add(text) };
        var before = font.Glyphs.Count;
        Assert.False(view.DeleteRange("0-5000"));
        Assert.Single(said);
        Assert.Contains("2048", said[0], StringComparison.Ordinal);
        Assert.Equal(before, font.Glyphs.Count);
    });

    [Fact]
    public async Task AHoverCardIsRebuiltAfterALanguageChange()
    {
        await CodeIntelUiTests.WithTab((tab, _, _) =>
        {
            Loc.Current.SetLanguage("en");
            try
            {
                var at = tab.Text.IndexOf("hp +", StringComparison.Ordinal);
                var english = tab.HoverTextAt(at);
                Loc.Current.SetLanguage("ru");
                Assert.NotEqual(english, tab.HoverTextAt(at));
            }
            finally { Loc.Current.SetLanguage("en"); }

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task ASelectionMovedUnderThePinnedHeadingIsScrolledBelowIt()
    {
        await HeadlessApp.Run(() =>
        {
            Loc.Current.SetLanguage("en");
            var view = new ExplorerView();
            var window = new Window { Width = 320, Height = 400, Content = view };
            window.Show();
            using var data = ExplorerModelTests.Generate(2000);
            view.Load("game.win", data);
            Dispatcher.UIThread.RunJobs();
            try
            {
                var list = view.GetVisualDescendants().OfType<ListBox>().Single(l => l.Name == "List");
                var sticky = view.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "Sticky");
                if (!view.Model.Rows.First(r => r.Key == "objects").Expanded) view.Model.Toggle("objects");
                Dispatcher.UIThread.RunJobs();
                var heading = view.Model.Rows.ToList().FindIndex(r => r is { Kind: ExplorerRowKind.Type, Key: "objects" });
                var scroll = list.GetVisualDescendants().OfType<ScrollViewer>().First();
                scroll.Offset = new Avalonia.Vector(0, 24 * (heading + 100));
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                Assert.True(sticky.IsVisible);
                // The row at the very top is the one the pinned heading covers.
                var top = list.GetRealizedContainers().OfType<ListBoxItem>().Where(c => c.TranslatePoint(default, list)!.Value.Y > -1).OrderBy(c => c.TranslatePoint(default, list)!.Value.Y).First();
                list.SelectedIndex = list.IndexFromContainer(top);
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                Assert.True(list.ContainerFromIndex(list.SelectedIndex)!.TranslatePoint(default, list)!.Value.Y >= sticky.Bounds.Height - 1);
            }
            finally { window.Close(); }

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task ExplorerRowsHaveNoBrushTransitionButOtherListsDo()
    {
        await HeadlessApp.Run(() =>
        {
            Motion.TimedInHeadless = true;
            try
            {
                Motion.SetLevel("Full");
                var view = new ExplorerView();
                var plain = new ListBox { ItemsSource = Items };
                var window = new Window { Width = 320, Height = 600, Content = new StackPanel { Children = { view, plain } } };
                window.Show();
                using var data = ExplorerModelTests.Generate(20);
                view.Load("game.win", data);
                Dispatcher.UIThread.RunJobs();
                static ContentPresenter Presenter(ListBox l) => l.GetRealizedContainers().First().GetVisualDescendants().OfType<ContentPresenter>().First(p => p.Name == "PART_ContentPresenter");
                var list = view.GetVisualDescendants().OfType<ListBox>().Single(l => l.Name == "List");
                Assert.True(Presenter(plain).Transitions?.Count > 0);
                Assert.True((Presenter(list).Transitions?.Count ?? 0) == 0);
                window.Close();
            }
            finally
            {
                Motion.CancelAll(); // runs started by the window must not outlive the test
                Motion.TimedInHeadless = false;
                Motion.SetLevel("Medium");
            }

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task ToggleButtonsAndMenuItemsFadeTheirHoverFillLikeButtons()
    {
        await HeadlessApp.Run(() =>
        {
            Motion.TimedInHeadless = true;
            try
            {
                Motion.SetLevel("Full");
                var toggle = new Avalonia.Controls.Primitives.ToggleButton { Content = "t" };
                var item = new MenuItem { Header = "m" };
                var window = new Window { Content = new StackPanel { Children = { toggle, new Menu { Items = { item } } } } };
                window.Show();
                Dispatcher.UIThread.RunJobs();
                Assert.True(toggle.GetVisualDescendants().OfType<ContentPresenter>().First(p => p.Name == "PART_ContentPresenter").Transitions?.Count > 0);
                Assert.True(item.GetVisualDescendants().OfType<Border>().First(b => b.Name == "PART_LayoutRoot").Transitions?.Count > 0);
                window.Close();
            }
            finally
            {
                Motion.CancelAll();
                Motion.TimedInHeadless = false;
                Motion.SetLevel("Medium");
            }

            return Task.CompletedTask;
        });
    }

    [Fact]
    public Task DismissKeepsFocusInsideTheList() => HeadlessApp.Run(() =>
    {
        Loc.Current.SetLanguage("en");
        var center = new NotificationCenter();
        var list = new NotificationList(center);
        var window = new Window { Content = list };
        window.Show();
        center.Post(NotificationLevel.Info, "a");
        center.Post(NotificationLevel.Info, "b");
        Dispatcher.UIThread.RunJobs();
        list.GetVisualDescendants().OfType<Button>().First(b => b.Tag as string == "dismiss").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
        var focused = window.FocusManager?.GetFocusedElement() as Avalonia.Visual;
        Assert.True(focused is not null && (ReferenceEquals(focused, list) || list.IsVisualAncestorOf(focused)));
        window.Close();
    });

    [Fact]
    public Task AListThatWasNeverShownIgnoresTheCenter() => HeadlessApp.Run(() =>
    {
        Loc.Current.SetLanguage("en");
        var center = new NotificationCenter();
        var list = new NotificationList(center);
        center.Post(NotificationLevel.Info, "unseen");
        Assert.DoesNotContain(list.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text == "unseen");
        var window = new Window { Content = list };
        window.Show();
        Assert.Contains(list.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text == "unseen");
        window.Close();
    });
}
