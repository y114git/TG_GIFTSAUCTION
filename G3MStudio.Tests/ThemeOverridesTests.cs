using Avalonia;
using Avalonia.Media;
using G3MStudio.Theme;

namespace G3MStudio.Tests;

public sealed class ThemeOverridesTests
{
    [Fact]
    public async Task AValueIsReadInTheKindOfTheTokenAndABadOneIsIgnored()
    {
        // Avalonia objects must be made on the UI thread of the headless host, or the creating thread becomes the UI thread for every later test.
        await HeadlessApp.Run(() =>
        {
            var color = Assert.IsType<SolidColorBrush>(ThemeOverrides.Parse(new SolidColorBrush(Colors.Red), "#80112233"));
            Assert.Equal(Color.Parse("#80112233"), color.Color);
            Assert.Null(ThemeOverrides.Parse(new SolidColorBrush(Colors.Red), "not a color"));
            Assert.Equal(15.0, ThemeOverrides.Parse(13.0, "15"));
            Assert.Null(ThemeOverrides.Parse(13.0, "-4"));
            Assert.Null(ThemeOverrides.Parse(13.0, "5000"));
            Assert.Equal(new Thickness(3), ThemeOverrides.Parse(new Thickness(2), "3"));
            Assert.Equal(new CornerRadius(10), ThemeOverrides.Parse(new CornerRadius(7), "10"));
            Assert.Equal("Fira Code", Assert.IsType<FontFamily>(ThemeOverrides.Parse(FontFamily.Default, "Fira Code")).Name);
            Assert.Null(ThemeOverrides.Parse("a string token", "x"));
            Assert.Null(ThemeOverrides.Parse(13.0, "  "));
        });
    }

    [Fact]
    public async Task AnOverrideReachesTheThemeAtOnceAndIsTakenBackWhenItIsRemoved()
    {
        using var dir = new TempDir();
        await HeadlessApp.Run(() =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                var app = Application.Current!;
                CornerRadius Radius() => app.TryGetResource("Radius.Window", app.ActualThemeVariant, out var v) && v is CornerRadius r ? r : default;
                var original = Radius();
                window.Workspaces.Settings.Update(x => x with { Overrides = new Dictionary<string, string> { ["Radius.Window"] = "12", ["No.Such.Token"] = "1", ["Radius.Panel"] = "oops" } });
                Assert.Equal(new CornerRadius(12), Radius());
                window.Workspaces.Settings.Update(x => x with { Overrides = new Dictionary<string, string>() });
                Assert.Equal(original, Radius());
            }
            finally { window.Close(); }
        });
    }
}
