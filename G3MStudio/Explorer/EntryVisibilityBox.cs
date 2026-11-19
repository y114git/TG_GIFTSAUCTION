using Avalonia.Controls;
using Avalonia.Layout;
using G3MStudio.Core.Settings;
using G3MStudio.Localization;

namespace G3MStudio.Explorer;

// One box serves the filter popup and the Settings page, so both say the same words and stay in step.
public static class EntryVisibilityBox
{
    private static readonly EntryVisibility[] Choices = [EntryVisibility.Show, EntryVisibility.Dim, EntryVisibility.Hide];

    /// <summary>A label above its box, so a long label wraps at any text size instead of pushing the box out of a narrow popup.</summary>
    public static StackPanel Row(string name, string labelKey, EntryVisibility current, Action<EntryVisibility> changed)
    {
        var box = Create(name, current, changed);
        return new StackPanel { Spacing = 2, Children = { new TextBlock { Text = Loc.Current[labelKey], TextWrapping = Avalonia.Media.TextWrapping.Wrap }, box } };
    }

    public static ComboBox Create(string name, EntryVisibility current, Action<EntryVisibility> changed)
    {
        var box = new ComboBox
        {
            Name = name,
            ItemsSource = new[] { Loc.Current["explorer.visShow"], Loc.Current["explorer.visDim"], Loc.Current["explorer.visHide"] },
            SelectedIndex = Math.Max(0, Array.IndexOf(Choices, current)),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        box.SelectionChanged += (_, _) => { if (box.SelectedIndex >= 0) changed(Choices[box.SelectedIndex]); };
        return box;
    }
}
