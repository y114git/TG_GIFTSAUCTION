using Avalonia;

namespace G3MStudio.Theme;

/// <summary>
/// Fluent's control keys point at our tokens (ButtonBackground at Bg.Surface). A XAML StaticResource takes the token's value once, at load, so a color or size
/// changed later never reached those keys and the controls kept the old look. This table is the one place that lists them; <see cref="Apply"/> copies the
/// current token values into the keys at start and whenever the settings are applied.
/// </summary>
public static class FluentForwards
{
    private static readonly (string Key, string Token)[] Table =
    [
        ("SystemControlHighlightListAccentLowBrush", "Selection.Bg"),
        ("SystemControlHighlightListAccentMediumBrush", "Selection.Bg"),
        ("SystemControlHighlightListAccentHighBrush", "Selection.Bg"),
        ("SystemControlHighlightAltListAccentLowBrush", "Selection.Bg"),
        ("SystemControlHighlightAltListAccentMediumBrush", "Selection.Bg"),
        ("SystemControlHighlightAltListAccentHighBrush", "Selection.Bg"),
        ("ComboBoxItemBackgroundSelected", "Selection.Bg"),
        ("ComboBoxItemBackgroundSelectedPointerOver", "Selection.Bg"),
        ("ComboBoxItemBackgroundSelectedPressed", "Selection.Bg"),
        ("TreeViewItemBackgroundSelected", "Selection.Bg"),
        ("TreeViewItemBackgroundSelectedPointerOver", "Selection.Bg"),
        ("TreeViewItemBackgroundSelectedPressed", "Selection.Bg"),
        ("ButtonBorderThemeThickness", "Border.Width"),
        ("ToggleButtonBorderThemeThickness", "Border.Width"),
        ("CheckBoxBorderThemeThickness", "Border.Width"),
        ("TextControlBorderThemeThickness", "Border.Width"),
        ("TextControlBorderThemeThicknessFocused", "Border.Width"),
        ("ComboBoxBorderThemeThickness", "Border.Width"),
        ("ComboBoxDropdownBorderThickness", "Border.Width"),
        ("ListBoxBorderThemeThickness", "Border.Width"),
        ("TreeViewItemBorderThemeThickness", "Border.Width"),
        ("ToolTipBorderThemeThickness", "Border.Width"),
        ("MenuFlyoutPresenterBorderThemeThickness", "Border.Width"),
        ("FlyoutBorderThemeThickness", "Border.Width"),
        ("ProgressBarBorderThemeThickness", "Border.Width"),
        ("SliderBorderThemeThickness", "Border.Width"),
        ("SplitButtonBorderThemeThickness", "Border.Width"),
        ("HyperlinkButtonBorderThemeThickness", "Border.Width"),
        ("ExpanderHeaderBorderThickness", "Border.Width"),
        ("ExpanderChevronBorderThickness", "Border.Width"),
        ("ExpanderContentDownBorderThickness", "Border.Width"),
        ("ExpanderContentLeftBorderThickness", "Border.Width"),
        ("ExpanderContentRightBorderThickness", "Border.Width"),
        ("ExpanderContentUpBorderThickness", "Border.Width"),
        ("GroupBoxBorderThickness", "Border.Width"),
        ("AutoCompleteListBorderThemeThickness", "Border.Width"),
        ("CalendarDatePickerBorderThemeThickness", "Border.Width"),
        ("TimePickerBorderThemeThickness", "Border.Width"),
        ("ToggleSwitchOuterBorderStrokeThickness", "Border.Width"),
        ("DateTimeFlyoutBorderThickness", "Border.Width"),
        ("DateTimeFlyoutButtonBorderThickness", "Border.Width"),
        ("ControlCornerRadius", "Radius.Control"),
        ("OverlayCornerRadius", "Radius.Panel"),
        ("ButtonBackground", "Bg.Surface"),
        ("ButtonBackgroundPointerOver", "Hover"),
        ("ButtonBackgroundPressed", "Pressed"),
        ("ButtonBackgroundDisabled", "Disabled.Bg"),
        ("ButtonBorderBrush", "Border"),
        ("ButtonBorderBrushPointerOver", "Border"),
        ("ButtonBorderBrushPressed", "Text"),
        ("ButtonBorderBrushDisabled", "Disabled.Border"),
        ("ButtonForeground", "Text"),
        ("ButtonForegroundPointerOver", "Text"),
        ("ButtonForegroundPressed", "Text"),
        ("ButtonForegroundDisabled", "Disabled.Text"),
        ("ToggleButtonBackground", "Bg.Surface"),
        ("ToggleButtonBackgroundPointerOver", "Hover"),
        ("ToggleButtonBackgroundPressed", "Pressed"),
        ("ToggleButtonBorderBrush", "Border"),
        ("ToggleButtonBorderBrushPointerOver", "Border"),
        ("ToggleButtonForeground", "Text"),
        ("ToggleButtonForegroundPointerOver", "Text"),
        ("ToggleButtonForegroundPressed", "Text"),
        ("TextControlBackground", "Bg.Sunken"),
        ("TextControlBackgroundPointerOver", "Bg.Sunken"),
        ("TextControlBackgroundFocused", "Bg.Sunken"),
        ("TextControlBackgroundDisabled", "Disabled.Bg"),
        ("TextControlBorderBrush", "Border"),
        ("TextControlBorderBrushPointerOver", "Border"),
        ("TextControlBorderBrushFocused", "Select"),
        ("TextControlBorderBrushDisabled", "Disabled.Border"),
        ("TextControlForeground", "Text"),
        ("ComboBoxBackground", "Bg.Surface"),
        ("ComboBoxBackgroundPointerOver", "Hover"),
        ("ComboBoxBackgroundPressed", "Pressed"),
        ("ComboBoxBorderBrush", "Border"),
        ("ComboBoxBorderBrushPointerOver", "Border"),
        ("ComboBoxBorderBrushPressed", "Text"),
        ("ComboBoxDropDownBackground", "Bg.Overlay"),
        ("ComboBoxDropDownBorderBrush", "Border"),
        ("ToolTipBackground", "Bg.Overlay"),
        ("ToolTipBorderBrush", "Border"),
        ("ToolTipForeground", "Text"),
        ("MenuFlyoutPresenterBackground", "Bg.Overlay"),
        ("MenuFlyoutPresenterBorderBrush", "Border"),
        ("FlyoutPresenterBackground", "Bg.Overlay"),
    ];

    /// <summary>Number of keys that follow a token.</summary>
    public static int Count => Table.Length;

    public static void Apply()
    {
        if (Application.Current is not { Resources: { } resources } app) return;
        foreach (var (key, token) in Table)
        {
            if (!app.TryGetResource(token, app.ActualThemeVariant, out var value) || value is null) continue;
            if (resources.TryGetValue(key, out var current) && Equals(current, value)) continue;
            resources[key] = value;
        }
    }
}
