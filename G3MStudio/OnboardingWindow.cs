using G3MStudio.Theme;
using Avalonia.Controls;
using Avalonia.Layout;
using G3MStudio.Localization;

namespace G3MStudio;

/// <summary>What the welcome window changes in the settings.</summary>
public sealed record OnboardingActions(Action<string> Pick, Action<string> Accent, Action<bool> Crash, Action Finish);

/// <summary>
/// The welcome window of the first launch (design/onboarding.md steps 0 and 1, joined): a list of the languages, each in its own language, with the one
/// that matches the operating system tagged. Picking one switches the whole window at once; Get Started keeps it and Skip Setup keeps what is chosen now.
/// </summary>
public sealed class OnboardingWindow : Window
{
    private readonly ListBox _languages = new() { Name = "OnboardingLanguages", MinHeight = 220 };

    /// <summary>The accent colors offered on the welcome window: the G3M green first, then a few that stay readable on the dark surfaces.</summary>
    public static readonly string[] Accents = ["#039D5B", "#2F81F7", "#8957E5", "#E5534B", "#D29922", "#F778BA"];

    public OnboardingWindow(IReadOnlyList<KeyValuePair<string, string>> languages, string selected, string detected, bool crashReports, OnboardingActions actions)
    {
        this.Tr(TitleProperty, "onboarding.title");
        Width = 520;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        CanResize = false;
        _languages.ItemsSource = languages.Select(l => l.Key == detected ? $"{l.Value}  ({Loc.Current["onboarding.detected"]})" : l.Value).ToList();
        _languages.SelectedIndex = Math.Max(0, languages.ToList().FindIndex(l => l.Key == selected));
        _languages.SelectionChanged += (_, _) =>
        {
            if (_languages.SelectedIndex >= 0) actions.Pick(languages[_languages.SelectedIndex].Key);
        };
        var start = new Button { Name = "OnboardingStart", Classes = { "primary" } }.Tr(ContentControl.ContentProperty, "onboarding.start");
        var skip = new Button { Name = "OnboardingSkip" }.Tr(ContentControl.ContentProperty, "onboarding.skip");
        start.Click += (_, _) => Close(true);
        skip.Click += (_, _) => Close(false);
        Closed += (_, _) => actions.Finish();
        var report = new CheckBox { Name = "OnboardingCrash", IsChecked = crashReports }.Tr(ContentControl.ContentProperty, "onboarding.crash");
        report.IsCheckedChanged += (_, _) => actions.Crash(report.IsChecked == true);
        var swatches = new StackPanel { Name = "OnboardingAccents", Orientation = Orientation.Horizontal, Spacing = 8 };
        foreach (var hex in Accents)
        {
            var swatch = new Button { Name = "Accent_" + hex[1..], Width = 28, Height = 28, Padding = new Avalonia.Thickness(0), Background = Avalonia.Media.SolidColorBrush.Parse(hex) };
            Avalonia.Controls.ToolTip.SetTip(swatch, hex);
            Avalonia.Automation.AutomationProperties.SetName(swatch, Loc.Current.Format("onboarding.accentNamed", ("hex", hex)));
            swatch.Click += (_, _) => actions.Accent(hex);
            swatches.Children.Add(swatch);
        }

        Content = new StackPanel
        {
            Margin = new Avalonia.Thickness(24),
            Spacing = 12,
            Children =
            {
                new TextBlock().TextSize("FontSize.Display").Tr(TextBlock.TextProperty, "onboarding.heading"),
                new TextBlock { TextWrapping = Avalonia.Media.TextWrapping.Wrap }.Tr(TextBlock.TextProperty, "onboarding.body"),
                new TextBlock { Classes = { "accent" } }.Tr(TextBlock.TextProperty, "onboarding.language"),
                _languages,
                new TextBlock { Classes = { "accent" } }.Tr(TextBlock.TextProperty, "onboarding.accent"),
                swatches,
                report,
                new TextBlock { Classes = { "muted" }, TextWrapping = Avalonia.Media.TextWrapping.Wrap }.Tr(TextBlock.TextProperty, "onboarding.later"),
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Children = { skip, start } },
            },
        };
    }
}
