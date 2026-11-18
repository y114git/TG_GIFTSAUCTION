using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace G3MStudio.Tests;

public sealed class ExplorerFoldTests
{
    [Fact]
    public async Task FoldingTheOptionsBarFreesItsHeightForTheList()
    {
        await HeadlessApp.Run(() =>
        {
            var view = new Explorer.ExplorerView();
            var window = new Window { Width = 320, Height = 600, Content = view };
            window.Show();
            try
            {
                Dispatcher.UIThread.RunJobs();
                var list = view.GetVisualDescendants().OfType<ListBox>().Single(l => l.Name == "List");
                var fold = view.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "OptionsFold");
                var row = view.GetVisualDescendants().OfType<Control>().Single(c => c.Name == "OptionsRow");
                var before = list.Bounds.Height;
                Assert.True(row.IsVisible);
                Assert.True(fold.Focusable && fold.Bounds is { Width: >= 24, Height: >= 24 });
                var shown = Avalonia.Automation.AutomationProperties.GetName(fold);

                fold.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                Assert.True(view.OptionsFolded);
                Assert.False(row.IsVisible);
                Assert.True(list.Bounds.Height > before);
                Assert.NotEqual(shown, Avalonia.Automation.AutomationProperties.GetName(fold));
                Assert.True(fold.IsEffectivelyVisible && fold.Bounds.Height >= 24); // the strip with the unfold chevron stays

                fold.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                Assert.False(view.OptionsFolded);
                Assert.True(row.IsVisible);
                Assert.Equal(before, list.Bounds.Height, 1);
            }
            finally { window.Close(); }
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task TheFoldedStateIsRememberedAndRestoredOnStart()
    {
        using var dir = new TempDir();
        await HeadlessApp.Run(() =>
        {
            var first = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                Assert.False(first.Workspaces.Settings.Current.ExplorerOptionsFolded);
                first.Explorer.OptionsFolded = true;
                Assert.True(first.Workspaces.Settings.Current.ExplorerOptionsFolded);
            }
            finally { first.Close(); }

            var second = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                Dispatcher.UIThread.RunJobs();
                Assert.True(second.Explorer.OptionsFolded);
            }
            finally { second.Close(); }
            return Task.CompletedTask;
        });
    }

    [Theory]
    [InlineData(100, 1.0)]
    [InlineData(125, 1.0)]
    [InlineData(150, 1.5)]
    public async Task ClickingTheFoldArrowAtItsNewPlaceRightAfterFoldingFoldsAgainWithoutMovingFirst(int uiScale, double renderScale)
    {
        using var dir = new TempDir();
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                await window.OpenAsync(TestData.WriteSample(dir));
                window.Workspaces.Settings.Update(x => x with { UiScale = uiScale });
                Avalonia.Headless.HeadlessWindowExtensions.SetRenderScaling(window, renderScale);
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                var fold = window.Explorer.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "OptionsFold");
                Point Centre() => fold.TranslatePoint(new Point(fold.Bounds.Width / 2, fold.Bounds.Height / 2), window)!.Value;
                void Click()
                {
                    var at = Centre();
                    window.MouseDown(at, Avalonia.Input.MouseButton.Left);
                    window.MouseUp(at, Avalonia.Input.MouseButton.Left);
                }

                var folded = window.Explorer.OptionsFolded;
                for (var i = 0; i < 4; i++)
                {
                    Click(); // no mouse move in between: the next click is where the arrow is now
                    Assert.Equal(!folded, window.Explorer.OptionsFolded);
                    folded = !folded;
                }
            }
            finally { window.Close(); }
        });
    }
}
