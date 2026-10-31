using Avalonia.Controls;
using Avalonia.Layout;
using G3MStudio.Localization;

namespace G3MStudio;

/// <summary>
/// The welcome window of the first launch (design/onboarding.md steps 0 and 1, joined): a list of the languages, each in its own language, with the one
/// that matches the operating system tagged. Picking one switches the whole window at once; Get Started keeps it and Skip Setup keeps what is chosen now.
/// </summary>
public sealed class OnboardingWindow : Window
{
    private readonly ListBox _languages = new() { Name = "OnboardingLanguages", MinHeight = 220 };

    public OnboardingWindow(IReadOnlyList<KeyValuePair<string, string>> languages, string selected, string detected, Action<string> pick, Action finish)
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
            if (_languages.SelectedIndex >= 0) pick(languages[_languages.SelectedIndex].Key);
        };
        var start = new Button { Name = "OnboardingStart", Classes = { "primary" } }.Tr(ContentControl.ContentProperty, "onboarding.start");
        var skip = new Button { Name = "OnboardingSkip" }.Tr(ContentControl.ContentProperty, "onboarding.skip");
        start.Click += (_, _) => Close(true);
        skip.Click += (_, _) => Close(false);
        Closed += (_, _) => finish();
        Content = new StackPanel
        {
            Margin = new Avalonia.Thickness(24),
            Spacing = 12,
            Children =
            {
                new TextBlock { FontSize = 20 }.Tr(TextBlock.TextProperty, "onboarding.heading"),
                new TextBlock { TextWrapping = Avalonia.Media.TextWrapping.Wrap }.Tr(TextBlock.TextProperty, "onboarding.body"),
                new TextBlock { Classes = { "accent" } }.Tr(TextBlock.TextProperty, "onboarding.language"),
                _languages,
                new TextBlock { Classes = { "muted" }, TextWrapping = Avalonia.Media.TextWrapping.Wrap }.Tr(TextBlock.TextProperty, "onboarding.later"),
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Children = { skip, start } },
            },
        };
    }
}
