using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using G3MStudio.Core.Settings;

#pragma warning disable CA2000 // windows made for a check only

namespace G3MStudio.Tests;

/// <summary>
/// Text and icons keep 4.5:1 (3:1 for large text and icons) against the fill they sit on, for every accent the onboarding offers: the selected, hovered, pressed, primary and
/// toggled surfaces are drawn with the accent, so a bright or dark accent must not take the readability away.
/// </summary>
public sealed class AccentContrastTests
{
    private static readonly string[] Surfaces = ["Bg.Window", "Bg.Surface", "Bg.Sunken", "Bg.Raised", "Bg.Overlay"], Statuses = ["Danger", "Warning", "Success", "Info"], ListItems = ["one", "two"];

    public static readonly TheoryData<string> Accents = [.. OnboardingWindow.Accents];

    private static Color Token(string key) =>
        Application.Current is { } app && app.TryGetResource(key, app.ActualThemeVariant, out var found) && found is ISolidColorBrush brush ? brush.Color : throw new InvalidOperationException(key);

    private static string Hex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

    private static double Ratio(Color a, Color b) => ThemeColors.Contrast(Hex(a), Hex(b));

    private static async Task Settle()
    {
        for (var i = 0; i < 3; i++)
        {
            await Task.Delay(30);
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static void Apply(string accent)
    {
        MainWindow.ApplyColors(new Dictionary<string, string> { [ThemeColors.Accent] = accent });
        G3MStudio.Theme.FluentForwards.Apply(); // the Fluent row and button brushes are copies of the tokens
    }

    /// <summary>The fill and the text colour a control paints in its presenter, as the template resolves them.</summary>
    private static (Color Fill, Color Text) Painted(Control control)
    {
        // A tab or a list row paints on the control, a button on the presenter of its template.
        var presenter = control.GetVisualDescendants().OfType<ContentPresenter>().FirstOrDefault(p => p.Background is ISolidColorBrush);
        var fill = (ISolidColorBrush)(presenter?.Background ?? ((TemplatedControl)control).Background)!;
        var text = presenter?.Foreground as ISolidColorBrush ?? (ISolidColorBrush)((TemplatedControl)control).Foreground!;
        return (fill.Color, text.Color);
    }

    [Theory]
    [MemberData(nameof(Accents))]
    public Task TokensKeepTheirTextReadableOnTheAccent(string accent) => HeadlessApp.Run(() =>
    {
        try
        {
            Apply(accent);
            var failures = new List<string>();
            void Check(string what, Color text, Color fill, double min = 4.5)
            {
                var ratio = Ratio(text, fill);
                if (ratio < min) failures.Add($"{what}: {ratio:0.0}:1 ({Hex(text)} on {Hex(fill)})");
            }

            var (onAccent, light, muted, accentText, placeholder) = (Token("Text.OnAccent"), Token("Text"), Token("Text.Muted"), Token("Text.Accent"), Token("Text.Placeholder"));
            Check("selection", onAccent, Token("Selection.Bg"));
            Check("accent fill", onAccent, Token("Accent.Fill"));
            Check("primary", light, Token("Accent.Primary"));
            Check("primary hover", light, Token("Accent.PrimaryHover"));
            Check("hover", light, Token("Hover"));
            Check("pressed", light, Token("Pressed"));
            foreach (var surface in Surfaces)
            {
                Check("accent text on " + surface, accentText, Token(surface));
                Check("muted text on " + surface, muted, Token(surface));
                Check("placeholder on " + surface, placeholder, Token(surface));
                if (Ratio(placeholder, Token(surface)) >= Ratio(muted, Token(surface))) failures.Add("placeholder is not dimmer than muted text on " + surface);
                Check("text on " + surface, light, Token(surface));
                foreach (var status in Statuses) Check(status + " on " + surface, Token(status), Token(surface));
            }

            Check("danger button", onAccent, Token("Danger.Fill"));
            Check("danger button hover", onAccent, Token("Danger"));
            Check("disabled", Token("Disabled.Text"), Token("Disabled.Bg"), 3);
            if (Ratio(Token("Disabled.Text"), Token("Bg.Surface")) >= Ratio(placeholder, Token("Bg.Surface"))) failures.Add("disabled text is not dimmer than the placeholder");
            Assert.True(failures.Count == 0, accent + "\n" + string.Join("\n", failures));
        }
        finally { MainWindow.ApplyColors(new Dictionary<string, string>()); }

        return Task.CompletedTask;
    });

    [Theory]
    [MemberData(nameof(Accents))]
    public Task SurfacesDrawnWithTheAccentKeepTheirTextReadable(string accent) => HeadlessApp.Run(async () =>
    {
        var toggle = new ToggleButton { Content = "Toggle", IsChecked = true };
        var primary = new Button { Content = "Primary", Classes = { "primary" } };
        var active = new Button { Content = new TextBlock { Text = "0", Classes = { "muted" } }, Classes = { "active" } };
        var plain = new Button { Content = "Plain" };
        var tab = new TabControl { ItemsSource = new TabItem[] { new() { Header = "Chosen", Content = "x" }, new() { Header = "Other", Content = "y" } }, SelectedIndex = 0 };
        var list = new ListBox { ItemsSource = ListItems, SelectedIndex = 0 };
        var window = new Window { Content = new StackPanel { Children = { toggle, primary, active, plain, tab, list } }, Width = 400, Height = 500 };
        window.Show();
        try
        {
            Apply(accent);
            await Settle();
            var failures = new List<string>();
            void Check(string what, Control control, double min = 4.5)
            {
                var (fill, text) = Painted(control);
                var ratio = Ratio(text, fill);
                if (ratio < min) failures.Add($"{what}: {ratio:0.0}:1 ({Hex(text)} on {Hex(fill)})");
            }

            Check("checked toggle", toggle);
            Check("primary button", primary);
            Check("current frame button", active);
            Check("chosen tab", tab.GetVisualDescendants().OfType<TabItem>().First(t => t.IsSelected));
            Check("selected row", list.GetVisualDescendants().OfType<ListBoxItem>().First(i => i.IsSelected));
            var caption = active.GetVisualDescendants().OfType<TextBlock>().First();
            var (captionFill, _) = Painted(active);
            var captionRatio = Ratio(((ISolidColorBrush)caption.Foreground!).Color, captionFill);
            if (captionRatio < 4.5) failures.Add($"current frame caption: {captionRatio:0.0}:1");
            Assert.True(failures.Count == 0, accent + "\n" + string.Join("\n", failures));
        }
        finally
        {
            window.Close();
            MainWindow.ApplyColors(new Dictionary<string, string>());
        }
    });
}
