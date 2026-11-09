using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace G3MStudio.Tests;

public sealed class SelectionContrastTests
{
    private static readonly string[] Items = ["a", "b"];

    private static double Luminance(Color c)
    {
        static double Channel(byte v)
        {
            var s = v / 255.0;
            return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }

        return (0.2126 * Channel(c.R)) + (0.7152 * Channel(c.G)) + (0.0722 * Channel(c.B));
    }

    private static double Ratio(Color a, Color b)
    {
        var (hi, lo) = (Math.Max(Luminance(a), Luminance(b)), Math.Min(Luminance(a), Luminance(b)));
        return (hi + 0.05) / (lo + 0.05);
    }

    private static Color Token(string key) =>
        Application.Current is { } app && app.TryGetResource(key, app.ActualThemeVariant, out var found) && found is ISolidColorBrush brush ? brush.Color : throw new InvalidOperationException(key);

    // The shell ships one theme (Dark); add its light sibling here when it exists.
    [Fact]
    public Task SelectedAndPrimaryFillsKeepAtLeastFourPointFiveToOneWithTheirText() => HeadlessApp.Run(() =>
    {
        Assert.True(Ratio(Token("Text.OnAccent"), Token("Selection.Bg")) >= 4.5);
        Assert.True(Ratio(Token("Text.OnAccent"), Token("Accent.Fill")) >= 4.5);
        return Task.CompletedTask;
    });

    [Fact]
    public Task ASelectedListRowIsDrawnWithTheDarkTextOnTheSelectionFill() => HeadlessApp.Run(async () =>
    {
        var list = new ListBox { ItemsSource = Items, SelectedIndex = 0 };
        var window = new Window { Content = list, Width = 200, Height = 200 };
        window.Show();
        await Task.Delay(100);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        var item = list.GetVisualDescendants().OfType<ListBoxItem>().First(i => i.IsSelected);
        var presenter = item.GetVisualDescendants().OfType<ContentPresenter>().First(p => p.Name == "PART_ContentPresenter");
        var fill = ((ISolidColorBrush)presenter.Background!).Color;
        var text = ((ISolidColorBrush)presenter.Foreground!).Color;
        Assert.Equal(Token("Selection.Bg"), fill);
        Assert.True(Ratio(text, fill) >= 4.5, $"{text} on {fill}");
        window.Close();
    });
}
