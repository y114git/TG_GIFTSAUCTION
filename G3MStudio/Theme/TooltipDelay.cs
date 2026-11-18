using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;

namespace G3MStudio.Theme;

/// <summary>Gives every tooltip in the application the same show delay.</summary>
public static class TooltipDelay
{
    private static Style? _style;
    private static int _ms = -1;
    private static Application? _owner; // the style belongs to one application; a later one (a new test session) must get its own

    public static void Apply(int ms)
    {
        if (Application.Current is not { } app) return;
        if (!ReferenceEquals(_owner, app)) (_owner, _style) = (app, null);
        if (ms == _ms && _style is not null) return;
        _ms = ms;
        if (_style is not null) app.Styles.Remove(_style);
        _style = new Style(x => x.Is<Control>()) { Setters = { new Setter(ToolTip.ShowDelayProperty, ms) } };
        app.Styles.Add(_style);
    }
}
