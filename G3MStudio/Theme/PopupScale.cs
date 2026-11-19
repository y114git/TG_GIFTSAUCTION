using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace G3MStudio.Theme;

/// <summary>
/// Popups (menus, context menus, combo box lists, tooltips, flyouts, hover cards, completion) live outside the visual tree that the interface scale transforms, so they would
/// open at 100%. Every Popup takes the scale of the control it is placed against (Avalonia's InheritsTransform), which follows the scale live and counts a scaled dialog once.
/// A popup placed against a top level itself (the completion description) has no transform to inherit, so it takes the scale of that window's content.
/// </summary>
public static class PopupScale
{
    private static bool _following;

    /// <summary>Turns it on for every Popup, before any opens: the child is always set before the popup is opened.</summary>
    public static void Follow()
    {
        if (_following) return;
        _following = true;
        Popup.ChildProperty.Changed.AddClassHandler<Popup>((popup, _) =>
        {
            if (popup.InheritsTransform) return;
            popup.InheritsTransform = true;
            popup.Opened += OnOpened;
        });
    }

    /// <summary>The interface scale a popup placed against <paramref name="target"/> opens at; a size limit for the popup is the window size divided by this.</summary>
    public static double Of(TopLevel? target) => target switch
    {
        Window { Content: LayoutTransformControl { LayoutTransform: { } transform } } => transform.Value.M11,
        PopupRoot { Transform: { } transform } => transform.Value.M11, // the description of a completion list sits against the list's own window
        _ => 1,
    };

    private static void OnOpened(object? sender, EventArgs e)
    {
        if (sender is not Popup { Child: { } child, PlacementTarget: TopLevel target }) return;
        var scale = Of(target);
        if (scale == 1) return;
        // The host is a window of its own, or a layer of the window for a popup that stays inside it.
        if (TopLevel.GetTopLevel(child) is PopupRoot root) root.Transform = new ScaleTransform(scale, scale);
        else if (child.FindAncestorOfType<OverlayPopupHost>() is { } overlay) overlay.Transform = new ScaleTransform(scale, scale);
    }
}
