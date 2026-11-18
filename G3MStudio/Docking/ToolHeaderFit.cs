using System.Globalization;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using Dock.Avalonia.Controls;

namespace G3MStudio.Docking;

/// <summary>
/// A panel header gives its title all the room it needs: when the title does not fit beside the buttons (a narrow panel, a long translation), the pin and
/// menu buttons are hidden. The panel menu stays reachable with a right click on the header.
/// </summary>
internal static class ToolHeaderFit
{
    // Loaded fires again after every re-attach; a header is wired once.
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ToolChromeControl, object> Wired = [];

    /// <summary>True when the header was wired now, false when it already was (or its parts are not there yet).</summary>
    public static bool Watch(ToolChromeControl chrome)
    {
        if (Wired.TryGetValue(chrome, out _)) return false;
        var parts = chrome.GetVisualDescendants().OfType<Control>().ToList();
        if (parts.Find(c => c.Name == "PART_Grip") is not Grid grip || parts.Find(c => c.Name == "PART_Title") is not TextBlock title
            || parts.Find(c => c.Name == "PART_PinButton") is not Button pin || parts.Find(c => c.Name == "PART_MenuButton") is not Button menu || menu.GetVisualParent() is not StackPanel stack) return false;
        Wired.Add(chrome, chrome);
        double full = 0;
        void Fit()
        {
            if (grip.Bounds.Width <= 0 || string.IsNullOrEmpty(title.Text)) return;
            if (pin.IsVisible && menu.IsVisible) full = stack.Bounds.Width;
            if (full == 0) return;
            var text = new FormattedText(title.Text, CultureInfo.CurrentCulture, title.FlowDirection, new Typeface(title.FontFamily, title.FontStyle, title.FontWeight), title.FontSize, null).Width;
            // Besides the title and the three buttons the row holds its margins (16), the grip dots (16) and the title margin (8).
            menu.IsVisible = pin.IsVisible = grip.Bounds.Width - 40 - full >= text;
        }

        grip.SizeChanged += (_, _) => Fit();
        title.PropertyChanged += (_, e) => { if (e.Property == TextBlock.TextProperty) Fit(); };
        grip.AddHandler(InputElement.PointerPressedEvent, (_, e) =>
        {
            if (menu.IsVisible || !e.GetCurrentPoint(grip).Properties.IsRightButtonPressed || menu.Flyout is not { } flyout) return;
            menu.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, menu)); // the menu prepares itself on this click
            flyout.ShowAt(grip);
        }, RoutingStrategies.Tunnel);
        Fit();
        return true;
    }
}
