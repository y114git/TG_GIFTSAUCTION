using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using G3MStudio.Core.Settings;
using G3MStudio.Editor;

namespace G3MStudio.Tests;

public sealed partial class ThemeHueTests
{
    private static readonly string[] Sheets = ["Styles.axaml", "Controls.axaml", "Glyphs.axaml", "FluentOverrides.axaml"];
    private static readonly string[] StateTokens = ["Hover", "Pressed", "Selection.Bg", "Selection.Soft", "Accent.Fill", "Border"];
    private static readonly string[] Shades = ["SystemAccentColorLight", "SystemAccentColorDark"];

    [GeneratedRegex("#([0-9A-Fa-f]{6})\\b|#[0-9A-Fa-f]{2}([0-9A-Fa-f]{6})\\b")]
    private static partial Regex Hex();

    private static bool IsBlue(Color c)
    {
        var (hue, saturation) = (c.ToHsv().H, c.ToHsv().S);
        return saturation > 0.15 && hue is > 175 and < 265;
    }

    private static string Read(string name)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "G3MStudio", "Theme"))) dir = dir.Parent;
        return File.ReadAllText(Path.Combine(dir!.FullName, "G3MStudio", "Theme", name));
    }

    [Fact]
    public void NoThemeSheetHardCodesABlueAccent()
    {
        foreach (var sheet in Sheets)
            foreach (Match m in Hex().Matches(Read(sheet)))
                Assert.False(IsBlue(Color.Parse("#" + (m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value))), $"{sheet} has a blue colour {m.Value}");
    }

    [Fact]
    public Task StateTokensAndFluentAccentShadesAreGreen() => HeadlessApp.Run(() =>
    {
        var app = Application.Current!;
        foreach (var key in StateTokens)
            Assert.True(app.TryGetResource(key, app.ActualThemeVariant, out var brush) && !IsBlue(((ISolidColorBrush)brush!).Color), key);
        for (var i = 1; i <= 3; i++)
            foreach (var shade in Shades)
                Assert.True(app.TryGetResource(shade + i, app.ActualThemeVariant, out var color) && !IsBlue((Color)color!), shade + i);
        return Task.CompletedTask;
    });

    [Fact]
    public void TheSoftSelectionFollowsTheSelectionColour() =>
        Assert.Contains(ThemeColors.Expand("Selection.Bg", "#112233"), t => t is ("Selection.Soft", "#66112233"));

    [Fact]
    public async Task TheCodeGutterDrawsChevronsInPlaceOfTheLibraryMarkers()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir, withCode: true);
        var userData = new G3MStudio.Core.Workspace.UserData(dir.File("user"));
        using var workspace = await new G3MStudio.Core.Workspace.WorkspaceService(userData, G3MStudio.Core.Workspace.RecentFiles.Load(userData)).OpenAsync(path, null, TestContext.Current.CancellationToken);
        await HeadlessApp.Run(async () =>
        {
            var tab = new CodeEditorTab(workspace, "gml_Script_probe");
            var window = new Window { Content = tab, Width = 900, Height = 600 };
            window.Show();
            await tab.Ready;
            tab.Editor.Text = "if (a) {\n  b = 1;\n}\n";
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            var margins = tab.Editor.TextArea.LeftMargins.OfType<AvaloniaEdit.Folding.FoldingMargin>().ToList();
            Assert.Single(margins);
            Assert.IsType<ChevronFoldingMargin>(margins[0]);
            window.Close();
        });
    }
}
