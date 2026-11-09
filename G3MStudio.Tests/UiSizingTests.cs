using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;
using G3MStudio.Core.Settings;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

public sealed class UiSizingTests
{
    [Theory]
    [InlineData(1920, 1080, 1.0, 0, 100)]
    [InlineData(3840, 2160, 1.5, 0, 115)]
    [InlineData(3840, 2160, 1.0, 0, 150)]
    [InlineData(2560, 1440, 1.0, 0, 115)]
    [InlineData(1366, 768, 1.0, 0, 85)]
    [InlineData(800, 480, 1.0, 0, 80)]
    [InlineData(7680, 4320, 1.0, 0, 200)]
    [InlineData(3840, 2160, 1.0, 1000, 105)]
    [InlineData(0, 0, 0, 0, 100)]
    public void AutoScaleFollowsTheScreen(int width, int height, double scaling, double window, int expected) =>
        Assert.Equal(expected, UiSizing.AutoScale(new ScreenInfo(width, height, scaling, window)));

    [Fact]
    public void ScreensOfTheSamePhysicalSizeGetComparableScales()
    {
        var hd = UiSizing.AutoScale(new ScreenInfo(1920, 1080, 1.0));
        var uhd = UiSizing.AutoScale(new ScreenInfo(3840, 2160, 1.5));
        Assert.InRange(Math.Abs(hd - uhd), 0, 20);
    }

    [Fact]
    public void ZeroMeansAutoAndAManualValueIsKeptInRange()
    {
        var screen = new ScreenInfo(3840, 2160, 1.0);
        Assert.Equal(150, UiSizing.Effective(0, screen));
        Assert.Equal(120, UiSizing.Effective(120, screen));
        Assert.Equal(200, UiSizing.Effective(900, screen));
        Assert.Equal((1632d, 918d), UiSizing.WindowSize(1920, 1080, 1.0));
    }

    [Fact]
    public void ADensitySetsTheSizesTogetherAndAnOwnSizeWins()
    {
        var none = new Dictionary<string, int>();
        Assert.Equal(28, ThemeSizes.Value(none, UiSizing.Compact, "Control.Height"));
        Assert.Equal(30, ThemeSizes.Value(none, UiSizing.Comfortable, "Control.Height"));
        Assert.Equal(36, ThemeSizes.Value(none, UiSizing.Spacious, "Control.Height"));
        Assert.Equal(40, ThemeSizes.Value(new Dictionary<string, int> { ["Control.Height"] = 40 }, UiSizing.Compact, "Control.Height"));
        Assert.Equal(30, ThemeSizes.Value(none, "Nonsense", "Control.Height"));
        Assert.True(ThemeSizes.IsDefault(none, UiSizing.Comfortable, "Row.Height"));
        Assert.False(ThemeSizes.IsDefault(none, UiSizing.Compact, "Row.Height"));
        // No control may fall under the 28 px hit target, whatever the file says.
        Assert.Equal(28, ThemeSizes.Clamp(new Dictionary<string, int> { ["Control.Height"] = 5 })["Control.Height"]);
    }

    [Fact]
    public void SettingsKeepScaleDensitySizesAndDialogSizesThroughTheFile()
    {
        using var dir = new TempDir();
        var userData = new UserData(dir.File("user"));
        using (var store = new SettingsStore(userData))
        {
            Assert.Equal(0, store.Current.UiScale);
            Assert.Equal(UiSizing.Comfortable, store.Current.Density);
            store.Update(x => x with { UiScale = 135, Density = UiSizing.Spacious, Sizes = new Dictionary<string, int> { ["Tab.Height"] = 40, ["Scrollbar.Width"] = 99 }, DialogSizes = new Dictionary<string, DialogSize> { ["AboutWindow"] = new(500, 400) } });
        }

        using var again = new SettingsStore(userData);
        Assert.Equal((135, UiSizing.Spacious), (again.Current.UiScale, again.Current.Density));
        Assert.Equal(40, again.Current.Sizes["Tab.Height"]);
        Assert.Equal(24, again.Current.Sizes["Scrollbar.Width"]);
        Assert.Equal(new DialogSize(500, 400), again.Current.DialogSizes["AboutWindow"]);
    }

    private static object? Token(string key) => Application.Current!.TryGetResource(key, Application.Current.ActualThemeVariant, out var value) ? value : null;

    [Fact]
    public async Task DensityAndSizeSettingsReachTheThemeTokens()
    {
        using var dir = new TempDir();
        await HeadlessApp.Run(() =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                Assert.Equal((30d, 26d, new Thickness(12)), (Token("Control.Height"), Token("Row.Height"), (Thickness)Token("Panel.Padding")!));
                window.Workspaces.Settings.Update(x => x with { Density = UiSizing.Compact });
                Assert.Equal((28d, 22d, 28d), (Token("Control.Height"), Token("Row.Height"), Token("Tab.Height")));
                Assert.Equal(new Thickness(8), Token("Panel.Padding"));
                window.Workspaces.Settings.Update(x => x with { Density = UiSizing.Spacious, Sizes = new Dictionary<string, int> { ["Icon.Size"] = 24 } });
                Assert.Equal((36d, 24d, 14d), (Token("Control.Height"), Token("Icon.Size"), Token("FontSize.Body")));
                window.Workspaces.Settings.Update(x => x with { Density = UiSizing.Comfortable, Sizes = new Dictionary<string, int>() });
                Assert.Equal((30d, 16d, 13d), (Token("Control.Height"), Token("Icon.Size"), Token("FontSize.Body")));
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public async Task NoControlOfTheMainWindowIsSmallerThanTheHitTarget()
    {
        using var dir = new TempDir();
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                window.Registry.Execute("settings.open");
                foreach (var density in UiSizing.Densities)
                {
                    window.Workspaces.Settings.Update(x => x with { Density = density, UiScale = 100 });
                    await Task.Delay(100);
                    Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                    window.UpdateLayout();
                    var shown = window.GetVisualDescendants().OfType<Control>().Where(c => c is Button or ComboBox or TextBox or NumericUpDown or ToggleButton && c.IsEffectivelyVisible && c.Bounds.Height > 0 && c.Name?.StartsWith("PART_", StringComparison.Ordinal) != true).ToList();
                    Assert.NotEmpty(shown);
                    var small = shown.Where(c => c.Bounds.Height < 28)
                        .Select(c => $"{c.GetType().Name}:{c.Name}:{c.Bounds.Height}").ToList();
                    Assert.True(small.Count == 0, density + ": " + string.Join(", ", small));
                }
            }
            finally { window.Close(); }
        });
    }
}
