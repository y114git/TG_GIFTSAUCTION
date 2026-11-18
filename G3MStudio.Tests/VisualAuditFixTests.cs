using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using G3MStudio.Localization;
using G3MStudio.Theme;

namespace G3MStudio.Tests;

/// <summary>Small fixes of the visual audit: swatch frames, readable headings, the dialog frame rhythm, the Strings window, the Settings navigation, the welcome window.</summary>
public sealed class VisualAuditFixTests
{
    [Fact]
    public async Task ASettingsColorSwatchHasAFrameSoDarkColorsShowAgainstThePage()
    {
        using var dir = new TempDir();
        await HeadlessApp.Run(async () =>
        {
            var window = await PanelChromeTests.Shown(dir, TestData.WriteSample(dir), "en", 100, 1600, 900);
            try
            {
                window.Registry.Execute("settings.open");
                await PanelChromeTests.Settle(window);
                var swatch = window.GetVisualDescendants().OfType<Border>().First(b => b.Name?.StartsWith("ColorSwatch_", StringComparison.Ordinal) == true);
                Assert.Equal(new Thickness(1), swatch.BorderThickness);
                Assert.NotNull(swatch.BorderBrush);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public async Task TheTransferHeadingsAreReadableAndNotSelectable()
    {
        await HeadlessApp.Run(async () =>
        {
            var window = new TransferWindow();
            window.Show();
            await PanelChromeTests.Settle(window);
            var headings = window.GetVisualDescendants().OfType<ListBoxItem>().Where(i => i.Tag is null).ToList();
            Assert.NotEmpty(headings);
            foreach (var heading in headings)
            {
                Assert.True(heading.IsEnabled && !heading.IsHitTestVisible && !heading.Focusable);
                Assert.Contains("muted", heading.GetVisualDescendants().OfType<TextBlock>().Single().Classes);
            }

            window.Close();
        });
    }

    [Fact]
    public async Task TheDialogFrameKeepsSixteenAboveTheButtonsAndWrapsThemToTheRight()
    {
        await HeadlessApp.Run(async () =>
        {
            var window = new Window { Width = 260, Height = 300 };
            var buttons = Enumerable.Range(0, 3).Select(i => new Button { Content = "Button number " + i, Width = 120 }).ToList();
            window.Content = DialogFrame.Build(new TextBlock { Name = "Body", Text = "text" }, buttons);
            window.Show();
            await PanelChromeTests.Settle(window);
            var row = window.GetVisualDescendants().OfType<WrapPanel>().Single(p => p.Name == "DialogActions");
            var body = window.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "Body");
            Assert.True(row.TranslatePoint(default, window)!.Value.Y - body.TranslatePoint(new Point(0, body.Bounds.Height), window)!.Value.Y >= 16 - 0.5);
            // Three 120 px buttons do not fit one line of 228 px: the lines wrap, and every line ends at the same right edge.
            var rights = buttons.Select(b => Math.Round(b.TranslatePoint(new Point(b.Bounds.Width, 0), window)!.Value.X)).Distinct().ToList();
            Assert.Single(rights);
            window.Close();
        });
    }

    [Fact]
    public async Task TheStringsWindowLabelsItsTextBoxAndDoesNotKeepATallEmptyBox()
    {
        using var dir = new TempDir();
        await HeadlessApp.Run(async () =>
        {
            var main = await PanelChromeTests.Shown(dir, TestData.WriteSample(dir), "en", 100, 1600, 900);
            try
            {
                var window = new StringsWindow(main.Workspaces.Current!);
                window.Show();
                await PanelChromeTests.Settle(main);
                var text = window.GetVisualDescendants().OfType<TextBox>().Single(t => t.Name == "StringText");
                var parent = Assert.IsAssignableFrom<Panel>(text.GetVisualParent());
                var label = Assert.IsType<TextBlock>(parent.Children[parent.Children.IndexOf(text) - 1]);
                Assert.Equal(Loc.Current["strings.text"], label.Text);
                Assert.True(text.MinHeight <= 60 && text.MaxHeight <= 160);
                window.Close();
            }
            finally { main.Close(); }
        });
    }

    [Fact]
    public async Task TheSettingsNavigationIsCappedAndCutsALongNameWithATip()
    {
        using var dir = new TempDir();
        await HeadlessApp.Run(async () =>
        {
            var window = await PanelChromeTests.Shown(dir, TestData.WriteSample(dir), "ru", 200, 960, 540);
            try
            {
                window.Registry.Execute("settings.open");
                await PanelChromeTests.Settle(window);
                var nav = window.GetVisualDescendants().OfType<ListBox>().First(l => l.Name == "SettingsNav");
                Assert.True(nav.Bounds.Width <= nav.MaxWidth + 0.5 && nav.MaxWidth < 220, $"nav is {nav.Bounds.Width:0} wide");
                var label = nav.GetVisualDescendants().OfType<TextBlock>().First();
                Assert.Equal(Avalonia.Media.TextTrimming.CharacterEllipsis, label.TextTrimming);
                Assert.NotNull(ToolTip.GetTip(label));
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public async Task TheWelcomeWindowHasOneSpaceBeforeTheDetectedTagAndRingsTheCurrentAccent()
    {
        await HeadlessApp.Run(async () =>
        {
            var window = new OnboardingWindow([.. Loc.Current.Languages], "en", "ru", true, true, new OnboardingActions(_ => { }, _ => { }, _ => { }, _ => { }, () => { }));
            window.Show();
            await PanelChromeTests.Settle(window);
            var list = window.GetVisualDescendants().OfType<ListBox>().Single(l => l.Name == "OnboardingLanguages");
            Assert.DoesNotContain("  ", list.ItemsSource!.Cast<string>().Single(t => t.Contains(Loc.Current["onboarding.detected"], StringComparison.Ordinal)), StringComparison.Ordinal);
            var swatches = window.GetVisualDescendants().OfType<Button>().Where(b => b.Name?.StartsWith("Accent_", StringComparison.Ordinal) == true).ToList();
            Assert.Equal(2, swatches[0].BorderThickness.Left);
            Assert.All(swatches.Skip(1), s => Assert.Equal(0, s.BorderThickness.Left));
            window.Close();
        });
    }
}
