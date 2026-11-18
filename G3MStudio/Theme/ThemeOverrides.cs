using System.Globalization;
using Avalonia;
using Avalonia.Media;

namespace G3MStudio.Theme;

/// <summary>
/// The advanced overrides of settings.json (<c>"overrides": { "Bg.Overlay": "#E61B1B1B", "Radius.Window": "10", "Font.Code": "Fira Code" }</c>): any token of the theme can be given
/// a value of the kind it already has (a color with or without transparency, a number, a thickness, a corner radius or a font family). A token the theme does not have, or a value
/// that does not read, is ignored, so a typo never breaks the window.
/// </summary>
public static class ThemeOverrides
{
    /// <summary>The value for a token, read in the kind of <paramref name="template"/> (the token's current value), or null when it does not read.</summary>
    public static object? Parse(object? template, string text)
    {
        text = text.Trim();
        if (text.Length == 0) return null;
        return template switch
        {
            SolidColorBrush => Color.TryParse(text, out var color) ? new SolidColorBrush(color) : null,
            double => Number(text, 1000) is { } n ? n : null,
            Thickness => Number(text, 20) is { } t ? new Thickness(t) : null,
            CornerRadius => Number(text, 50) is { } r ? new CornerRadius(r) : null,
            FontFamily => new FontFamily(text),
            _ => null,
        };
    }

    private static double? Number(string text, double max) => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && value >= 0 && value <= max ? value : null;
}
