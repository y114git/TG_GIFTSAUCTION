using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;

namespace G3MStudio.Theme;

public static class TokenExtensions
{
    /// <summary>Gives a control the text size of a theme token (FontSize.Caption to FontSize.Display), so it follows the density and the size settings instead of a fixed number.</summary>
    public static T TextSize<T>(this T control, string token) where T : Control
    {
        control.Bind(TextElement.FontSizeProperty, control.GetResourceObservable(token));
        return control;
    }

    /// <summary>Keeps a property (MinHeight, Width) at a size token such as Control.Height or Row.Height.</summary>
    public static T BindSize<T>(this T control, AvaloniaProperty property, string token) where T : Control
    {
        control.Bind(property, control.GetResourceObservable(token));
        return control;
    }

    /// <summary>The current value of a size token, or <paramref name="fallback"/> when the theme has none; for views that measure in code.</summary>
    public static double Size(this Control control, string token, double fallback) =>
        control.TryFindResource(token, control.ActualThemeVariant, out var found) && found is double value ? value : fallback;
}
