using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Avalonia.Controls.Primitives;
using G3MStudio.Core.Settings;
using G3MStudio.Core.Workspace;
using G3MStudio.Explorer;
using G3MStudio.Localization;

namespace G3MStudio.Tests;

/// <summary>Text (4.5:1) and icons (3:1) of the Explorer rows keep their contrast on the plain, hovered, pressed and selected fill of the row, for every accent.</summary>
public sealed class ExplorerRowContrastTests
{
    // "S" marks a selected row; the other parts are pseudo-classes set on the row.
    private static readonly string[] States = ["", ":pointerover", ":pressed", "S", "S:pointerover", "S:pressed"];
    private static readonly string[] Flags = [":pointerover", ":pressed"];

    public static readonly TheoryData<string> Accents = [.. OnboardingWindow.Accents];

    private static Color Over(Color top, Color back)
    {
        var a = top.A / 255.0;
        static byte Mix(byte t, byte b, double a) => (byte)Math.Round((t * a) + (b * (1 - a)));
        return Color.FromRgb(Mix(top.R, back.R, a), Mix(top.G, back.G, a), Mix(top.B, back.B, a));
    }

    private static string Hex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

    private static Color Token(string key) =>
        Application.Current is { } app && app.TryGetResource(key, app.ActualThemeVariant, out var found) && found is ISolidColorBrush brush ? brush.Color : throw new InvalidOperationException(key);

    private static Color? IconColour(Image image) =>
        image is { IsVisible: true, Source: DrawingImage { Drawing: DrawingGroup group } } && group.Children.OfType<GeometryDrawing>().FirstOrDefault()?.Brush is ISolidColorBrush brush ? brush.Color : null;

    [Theory]
    [MemberData(nameof(Accents))]
    public Task ExplorerRowsKeepTheirContrastInEveryState(string accent) => HeadlessApp.Run(async () =>
    {
        Loc.Current.SetLanguage("en");
        var view = new ExplorerView();
        var window = new Window { Width = 360, Height = 500, Content = view };
        window.Show();
        try
        {
            MainWindow.ApplyColors(new Dictionary<string, string> { [ThemeColors.Accent] = accent });
            view.GetVisualDescendants().OfType<ToggleButton>().First(t => t.Name == "ThumbToggle").IsChecked = true;
            G3MStudio.Theme.FluentForwards.Apply(); // the Fluent row brushes are copies of the tokens
            using var data = ExplorerModelTests.Generate(40);
            view.Load("game.win", data);
            foreach (var type in view.Model.Rows.Where(r => r.Kind == ExplorerRowKind.Type).Select(r => r.Key).Take(2).ToList())
                if (!view.Model.Rows.First(r => r.Key == type).Expanded) view.Model.Toggle(type);
            for (var i = 0; i < 4; i++)
            {
                Dispatcher.UIThread.RunJobs();
                await Task.Delay(20);
            }

            var list = view.GetVisualDescendants().OfType<ListBox>().First(l => l.Name == "List");
            var items = list.GetVisualDescendants().OfType<ListBoxItem>().Where(i => i.Bounds.Height > 0).ToList();
            Assert.True(items.Count > 4);
            var failures = new HashSet<string>();
            foreach (var item in items.Take(8))
            {
                var row = (ExplorerRow)item.DataContext!;
                foreach (var state in States)
                {
                    list.SelectedItem = state.StartsWith('S') ? row : null;
                    foreach (var flag in Flags) ((IPseudoClasses)item.Classes).Set(flag, state.Contains(flag, StringComparison.Ordinal));
                    Dispatcher.UIThread.RunJobs();
                    var presenter = item.GetVisualDescendants().OfType<ContentPresenter>().First(p => p.Name == "PART_ContentPresenter");
                    var fill = presenter.Background is ISolidColorBrush b ? Over(b.Color, Token("Bg.Surface")) : Token("Bg.Surface");
                    foreach (var text in item.GetVisualDescendants().OfType<TextBlock>().Where(t => !string.IsNullOrEmpty(t.Text) && t.IsVisible))
                        if (text.Foreground is ISolidColorBrush fg && ThemeColors.Contrast(Hex(Over(fg.Color, fill)), Hex(fill)) is var ratio && ratio < 4.5)
                            failures.Add($"text '{text.Text}' in state '{state}' {ratio:0.0}:1 ({Hex(fg.Color)} on {Hex(fill)})");
                    foreach (var image in item.GetVisualDescendants().OfType<Image>())
                        if (IconColour(image) is { } icon && ThemeColors.Contrast(Hex(icon), Hex(fill)) is var iconRatio && iconRatio < 3)
                            failures.Add($"icon of '{row.Key}' in state '{state}' {iconRatio:0.0}:1 ({Hex(icon)} on {Hex(fill)})");
                }

                foreach (var flag in Flags) ((IPseudoClasses)item.Classes).Set(flag, false);
            }

            Assert.True(failures.Count == 0, accent + " sel " + Hex(Token("Selection.Bg")) + "\n" + string.Join("\n", failures.Take(12)));
        }
        finally
        {
            window.Close();
            MainWindow.ApplyColors(new Dictionary<string, string>());
        }
    });
}
