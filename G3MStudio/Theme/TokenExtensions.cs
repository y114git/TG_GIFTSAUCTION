using Avalonia;
using Avalonia.Controls;
using Avalonia.Automation;
using Avalonia.Controls.Documents;
using G3MStudio.Localization;

namespace G3MStudio.Theme;

public static class TokenExtensions
{
    /// <summary>Gives a control the text size of a theme token (FontSize.Caption to FontSize.Display), so it follows the density and the size settings instead of a fixed number.</summary>
    public static T TextSize<T>(this T control, string token) where T : Control
    {
        control.Bind(TextElement.FontSizeProperty, control.GetResourceObservable(token));
        return control;
    }

    /// <summary>Puts the cursor in a search or filter box with its text selected, so typing replaces the old query. Posted so a panel that was just shown has its parts by then.</summary>
    public static void FocusSelected(this TextBox box)
    {
        // A panel that is only now being shown may not take the focus yet: it is asked again for a few frames.
        var tries = 0;
        void Place()
        {
            if (box.Focus())
            {
                box.SelectAll();
                return;
            }

            if (++tries < 10) Avalonia.Threading.Dispatcher.UIThread.Post(Place, Avalonia.Threading.DispatcherPriority.Background);
        }

        Avalonia.Threading.Dispatcher.UIThread.Post(Place, Avalonia.Threading.DispatcherPriority.Loaded);
    }

    /// <summary>Keeps a property (MinHeight, Width) at a size token such as Control.Height or Row.Height.</summary>
    public static T BindSize<T>(this T control, AvaloniaProperty property, string token) where T : Control
    {
        control.Bind(property, control.GetResourceObservable(token));
        return control;
    }

    /// <summary>Names a control for screen readers and shows the same text as its tooltip (ux-standards section 12 rule 4); <paramref name="tip"/> replaces the tooltip when it adds a shortcut.</summary>
    public static T Labeled<T>(this T control, string locKey, string? tip = null) where T : Control
    {
        var name = Loc.Current[locKey];
        AutomationProperties.SetName(control, name);
        ToolTip.SetTip(control, tip ?? name);
        return control;
    }

    /// <summary>Takes the accessible name of an input from the text label next to it.</summary>
    public static T LabeledBy<T>(this T control, Control label) where T : Control
    {
        AutomationProperties.SetLabeledBy(control, label);
        return control;
    }

    /// <summary>Names one of several alike buttons for screen readers with its target, as "Remove: Track 2"; <paramref name="target"/> is the row label.</summary>
    public static T NamedFor<T>(this T control, string locKey, string target) where T : Control
    {
        AutomationProperties.SetName(control, $"{Loc.Current[locKey]}: {target}");
        return control;
    }

    /// <summary>The current value of a size token, or <paramref name="fallback"/> when the theme has none; for views that measure in code.</summary>
    public static double Size(this Control control, string token, double fallback) =>
        control.TryFindResource(token, control.ActualThemeVariant, out var found) && found is double value ? value : fallback;
}
