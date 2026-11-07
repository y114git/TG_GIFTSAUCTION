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
}
