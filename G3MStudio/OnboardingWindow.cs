using G3MStudio.Theme;
using Avalonia.Controls;
using Avalonia.Layout;
using G3MStudio.Localization;

namespace G3MStudio;

/// <summary>What the welcome window changes in the settings. <paramref name="Associate"/> opens the data files with G3MStudio (true) or gives them back (false); the box is left out when it is null.</summary>
public sealed record OnboardingActions(Action<string> Pick, Action<string> Accent, Action<bool> Crash, Action<bool> Usage, Action Finish, Action<bool>? Associate = null, bool Associated = false, string? CurrentAccent = null);

/// <summary>
/// The welcome window of the first launch (design/onboarding.md steps 0 and 1, joined): a list of the languages, each in its own language, with the one
/// that matches the operating system tagged. Picking one switches the whole window at once; Get Started keeps it and Skip Setup keeps what is chosen now.
/// </summary>
public sealed class OnboardingWindow : Window
{
    private readonly ListBox _languages = new() { Name = "OnboardingLanguages", MinHeight = 140, MaxHeight = 260 };

    /// <summary>The accent colors offered on the welcome window: the G3M green first, then more that stay readable on the dark surfaces.</summary>
    public static readonly string[] Accents = ["#039D5B", "#2F81F7", "#8957E5", "#E5534B", "#D29922", "#F778BA", "#2DB5A3", "#F0883E", "#7EC850", "#6C7BF0", "#8B9BB4"];

    public OnboardingWindow(IReadOnlyList<KeyValuePair<string, string>> languages, string selected, string detected, bool crashReports, bool usageStats, OnboardingActions actions)
    {
        this.Tr(TitleProperty, "onboarding.title");
        Width = 520;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        CanResize = false;
        _languages.ItemsSource = languages.Select(l => l.Key == detected ? $"{l.Value} ({Loc.Current["onboarding.detected"]})" : l.Value).ToList();
        _languages.SelectedIndex = Math.Max(0, languages.ToList().FindIndex(l => l.Key == selected));
        _languages.SelectionChanged += (_, _) =>
        {
            if (_languages.SelectedIndex >= 0) actions.Pick(languages[_languages.SelectedIndex].Key);
        };
        var start = DialogFrame.Wire(this, new Button { Name = "OnboardingStart" }.Tr(ContentControl.ContentProperty, "onboarding.start"), () => Close(true), DialogRole.Primary);
        var skip = DialogFrame.Wire(this, new Button { Name = "OnboardingSkip" }.Tr(ContentControl.ContentProperty, "onboarding.skip"), () => Close(false), DialogRole.Normal);
        Closed += (_, _) => actions.Finish();
        var report = new CheckBox { Name = "OnboardingCrash", IsChecked = crashReports }.Tr(ContentControl.ContentProperty, "onboarding.crash");
        var usage = new CheckBox { Name = "OnboardingUsage", IsChecked = usageStats, IsEnabled = crashReports }.Tr(ContentControl.ContentProperty, "onboarding.usage");
        report.IsCheckedChanged += (_, _) =>
        {
            actions.Crash(report.IsChecked == true);
            usage.IsEnabled = report.IsChecked == true;
        };
        usage.IsCheckedChanged += (_, _) => actions.Usage(usage.IsChecked == true);
        var sent = new Button { Name = "OnboardingWhatIsSent", HorizontalAlignment = HorizontalAlignment.Left }.Tr(ContentControl.ContentProperty, "privacy.button");
        sent.Click += (_, _) => PrivacyInfoWindow.Open(this);
        var associate = new CheckBox { Name = "OnboardingAssociate", IsChecked = actions.Associated, IsVisible = actions.Associate is not null }.Tr(ContentControl.ContentProperty, "onboarding.associate");
        associate.IsCheckedChanged += (_, _) => actions.Associate?.Invoke(associate.IsChecked == true);
        var swatches = new StackPanel { Name = "OnboardingAccents", Orientation = Orientation.Horizontal, Spacing = 8 };
        var current = actions.CurrentAccent ?? Accents[0];
        void Ring()
        {
            foreach (var swatch in swatches.Children.OfType<Button>())
            {
                var on = string.Equals((string?)swatch.Tag, current, StringComparison.OrdinalIgnoreCase);
                swatch.BorderThickness = new Avalonia.Thickness(on ? 2 : 0);
                if (on) swatch.Bind(Border.BorderBrushProperty, swatch.GetResourceObservable("Text"));
            }
        }

        foreach (var hex in Accents)
        {
            var swatch = new Button { Name = "Accent_" + hex[1..], Width = 28, Height = 28, Padding = new Avalonia.Thickness(0), Tag = hex, Background = Avalonia.Media.SolidColorBrush.Parse(hex) };
            Avalonia.Controls.ToolTip.SetTip(swatch, hex);
            Avalonia.Automation.AutomationProperties.SetName(swatch, Loc.Current.Format("onboarding.accentNamed", ("hex", hex)));
            swatch.Click += (_, _) =>
            {
                current = hex;
                Ring();
                actions.Accent(hex);
            };
            swatches.Children.Add(swatch);
        }

        Ring();
        DialogFrame.Show(this, new StackPanel
        {
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
                usage,
                sent,
                associate,
                new TextBlock { Classes = { "muted" }, [HelpText.KindProperty] = HelpKind.Hint, TextWrapping = Avalonia.Media.TextWrapping.Wrap }.Tr(TextBlock.TextProperty, "onboarding.later"),
            },
        }, [skip, start], scroll: DialogScroll.Vertical);
    }
}
