using Avalonia.Headless;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;
using G3MStudio.Start;

namespace G3MStudio.Tests;

public sealed class StartPageTests
{
    [Fact]
    public async Task AMissingRecentCardStaysClickableSoTheLostFileStepsCanStart()
    {
        using var dir = new TempDir();
        var recent = RecentFiles.Load(new UserData(dir.File("user")));
        var lost = dir.File("gone.win");
        recent.Add(lost, "gone", "1.0", DateTimeOffset.UtcNow);
        await HeadlessApp.Run(() =>
        {
            var page = new StartPage();
            page.Bind(recent);
            new Window { Content = page, Width = 800, Height = 600 }.Show();
            string? opened = null;
            page.OpenPath += path => opened = path;
            var card = page.GetVisualDescendants().OfType<Button>().Single(b => b.Tag as string == Path.GetFullPath(lost) && b.Content is not string);
            Assert.True(card.IsEnabled);
            card.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(Path.GetFullPath(lost), opened);
            return Task.CompletedTask;
        });
    }

    [Theory]
    [InlineData(1000, 3)]
    [InlineData(640, 2)]
    [InlineData(420, 1)]
    public async Task RecentCardsAreEqualLeftAlignedAndTheWholeCardOpensTheFile(int width, int columns)
    {
        using var dir = new TempDir();
        var recent = RecentFiles.Load(new UserData(dir.File("user")));
        var (here, pinned, lost) = (dir.File("a.win"), dir.File("b.win"), dir.File("gone.win"));
        await File.WriteAllTextAsync(here, "x", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(pinned, "x", TestContext.Current.CancellationToken);
        recent.Add(pinned, "pinned game", "2023.6.0.0", DateTimeOffset.UtcNow.AddHours(-2));
        recent.Add(here, "a", "1.0", DateTimeOffset.UtcNow.AddHours(-1));
        recent.Add(lost, "gone with a very long name that has to be trimmed on one line", "1.0", DateTimeOffset.UtcNow);
        recent.SetPinned(pinned, true);
        await HeadlessApp.Run(() =>
        {
            Loc.Current.SetLanguage("en");
            var page = new StartPage();
            page.Bind(recent);
            var window = new Window { Content = page, Width = width, Height = 800 };
            window.Show();
            try
            {
                for (var pass = 0; pass < 3; pass++) // the column count is set after the first layout and needs another
                {
                    Dispatcher.UIThread.RunJobs();
                    window.UpdateLayout();
                }

                var cards = page.GetVisualDescendants().OfType<Button>().Where(b => b.Classes.Contains("card")).ToList();
                Assert.Equal(3, cards.Count);
                Assert.Equal(Path.GetFullPath(pinned), cards[0].Tag); // pinned first
                Assert.Single(cards.Select(c => c.Bounds.Width).Distinct().Select(w => Math.Round(w)));
                Assert.Single(cards.Select(c => c.Bounds.Height).Distinct().Select(h => Math.Round(h)));
                Assert.Equal(columns, cards.Select(c => Math.Round(c.TranslatePoint(default, page)!.Value.X)).Distinct().Count());
                Assert.All(cards, c => Assert.True(c.Focusable && c.IsTabStop, "a card is reached with Tab"));
                foreach (var card in cards)
                {
                    Assert.Equal(card.Tag, ToolTip.GetTip(card)); // the full path
                    var grid = (Grid)card.Parent!;
                    var texts = grid.GetVisualDescendants().OfType<TextBlock>().Where(t => !t.GetVisualAncestors().OfType<Button>().Any()).ToList(); // the captions of Pin and Remove sit centred in their buttons
                    Assert.All(texts.Where(t => t.Text is { Length: > 0 }), t =>
                    {
                        Assert.NotEqual(Avalonia.Media.TextAlignment.Center, t.TextAlignment);
                        Assert.True(t.TranslatePoint(default, card)!.Value.X < 40, $"'{t.Text}' starts at the left of the card (the status after its icon)");
                    });
                    Assert.Equal(Avalonia.Media.TextTrimming.PathSegmentEllipsis, texts.Single(t => t.Text == (string)card.Tag!).TextTrimming);
                    var status = texts.Single(t => t.Text == Loc.Current["start.missing"]);
                    Assert.Equal(!File.Exists((string)card.Tag!), status.IsEffectivelyVisible);
                    if (status.IsEffectivelyVisible) Assert.Equal(((Avalonia.Media.ISolidColorBrush)Application.Current!.FindResource("Warning")!).Color, ((Avalonia.Media.ISolidColorBrush)status.Foreground!).Color);
                }

                // A click on the empty part of the card opens it; the Pin and Remove buttons keep their own click.
                Avalonia.Headless.AvaloniaHeadlessPlatform.ForceRenderTimerTick(2); // hit testing reads the composed frame
                var corner = cards[1].TranslatePoint(new Point(cards[1].Bounds.Width - 6, 6), window)!.Value;
                Assert.True(ReferenceEquals(cards[1], window.GetVisualAt(corner)?.GetSelfAndVisualAncestors().OfType<Button>().FirstOrDefault()), $"{corner} in window {window.Bounds} card {cards[1].Bounds} hit {window.GetVisualAt(corner)}");
                var pin = ((Grid)cards[1].Parent!).GetVisualDescendants().OfType<Button>().First(b => !b.Classes.Contains("card"));
                var pinSpot = pin.TranslatePoint(new Point(pin.Bounds.Width / 2, pin.Bounds.Height / 2), window)!.Value;
                Assert.Same(pin, window.GetVisualAt(pinSpot)?.GetSelfAndVisualAncestors().OfType<Button>().First());
            }
            finally { window.Close(); }
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task TheClearButtonSitsUnderTheRecentHeadingTheDropHintShowsOnlyWithoutRecentFilesAndTheRecentBlockSitsRightUnderTheTopBlock()
    {
        using var dir = new TempDir();
        var recent = RecentFiles.Load(new UserData(dir.File("user")));
        await HeadlessApp.Run(() =>
        {
            Loc.Current.SetLanguage("en");
            var page = new StartPage();
            page.Bind(recent);
            var window = new Window { Content = page, Width = 800, Height = 700 };
            window.Show();
            try
            {
                var scroll = page.GetVisualDescendants().OfType<ScrollViewer>().First();
                var column = scroll.GetVisualDescendants().OfType<StackPanel>().First(p => p.MaxWidth == 920);
                var heading = page.GetVisualDescendants().OfType<TextBlock>().First(t => t.Text == Loc.Current["start.recent"]);
                var clear = page.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "ClearButton");
                var drop = page.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "DropBox");
                void Settle() { Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); }
                void NoEmptyBand() => Assert.True(column.Bounds.Top < 24, $"empty band above the recent block: {column.Bounds} in {scroll.Viewport}");

                Settle();
                Assert.True(drop.IsVisible);
                Assert.False(clear.IsVisible);
                NoEmptyBand();

                recent.Add(dir.File("a.win"), "a", "1.0", DateTimeOffset.UtcNow);
                recent.Add(dir.File("b.win"), "b", "1.0", DateTimeOffset.UtcNow);
                Settle();
                var cards = page.GetVisualDescendants().OfType<ItemsControl>().Single(c => c.Name == "Cards");
                Assert.False(drop.IsVisible);
                Assert.True(clear.IsVisible);
                var headingBottom = heading.TranslatePoint(new Avalonia.Point(0, heading.Bounds.Height), page)!.Value.Y;
                var clearTop = clear.TranslatePoint(default, page)!.Value.Y;
                var cardsTop = cards.TranslatePoint(default, page)!.Value.Y;
                Assert.True(clearTop >= headingBottom - 0.5 && clearTop < headingBottom + 24, $"clear at {clearTop}, heading ends {headingBottom}");
                Assert.True(cardsTop >= clear.TranslatePoint(new Avalonia.Point(0, clear.Bounds.Height), page)!.Value.Y);
                NoEmptyBand();

                for (var i = 0; i < 40; i++) recent.Add(dir.File($"x{i}.win"), $"x{i}", "1.0", DateTimeOffset.UtcNow);
                Settle();
                Assert.True(scroll.Extent.Height > scroll.Viewport.Height);
                Assert.True(column.Bounds.Top >= 0, "a tall page starts at the top and scrolls");
            }
            finally { window.Close(); }
            return Task.CompletedTask;
        });
    }

    [Theory]
    [InlineData(1.0, 1100, 800)]
    [InlineData(1.25, 900, 700)]
    [InlineData(1.5, 700, 600)]
    [InlineData(2.0, 1600, 1000)]
    public async Task TheStartPageHasNoEmptyBandAndNoSidewaysScrollAtEveryScale(double scale, int width, int height)
    {
        using var dir = new TempDir();
        var recent = RecentFiles.Load(new UserData(dir.File("user")));
        recent.Add(dir.File("a.win"), "a", "1.0", DateTimeOffset.UtcNow);
        await HeadlessApp.Run(() =>
        {
            Loc.Current.SetLanguage("en");
            var page = new StartPage();
            page.Bind(recent);
            var window = new Window { Content = page, Width = width, Height = height };
            window.Show();
            try
            {
                window.SetRenderScaling(scale);
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                var scroll = page.GetVisualDescendants().OfType<ScrollViewer>().First();
                var column = scroll.GetVisualDescendants().OfType<StackPanel>().First(p => p.MaxWidth == 920);
                Assert.True(column.Bounds.Top < 24, $"empty band above the recent block: {column.Bounds}");
                Assert.True(scroll.Extent.Width <= scroll.Viewport.Width + 0.5, $"sideways scroll: {scroll.Extent} in {scroll.Viewport}");
            }
            finally { window.Close(); }
            return Task.CompletedTask;
        });
    }
}
