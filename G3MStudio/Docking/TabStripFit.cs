using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Dock.Avalonia.Controls;

namespace G3MStudio.Docking;

/// <summary>
/// A tab strip that scrolls (a narrow panel, many tabs, a long language) shows the chosen tab whole, and a tab the strip edge cuts loses its close button, so
/// no half label is drawn with a lone cross (class <see cref="CutClass"/>, see Styles.axaml).
/// </summary>
internal static class TabStripFit
{
    public const string CutClass = "cut";

    // Loaded fires again after every re-attach; a strip is wired once.
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<TemplatedControl, object> Wired = [];

    /// <summary>True when the strip was wired now, false when it already was (or has no scroll area yet).</summary>
    public static bool Watch(TemplatedControl strip)
    {
        if (Wired.TryGetValue(strip, out _) || strip.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault() is not { } scroll) return false;
        Wired.Add(strip, strip);
        scroll.ScrollChanged += (_, _) => Mark(strip, scroll);
        if (strip is SelectingItemsControl selecting)
            selecting.SelectionChanged += (_, _) => Dispatcher.UIThread.Post(() =>
            {
                if (selecting.SelectedItem is { } chosen && selecting.ContainerFromItem(chosen) is { } container) container.BringIntoView();
            }, DispatcherPriority.Loaded);
        return true;
    }

    private static void Mark(Visual strip, ScrollViewer scroll)
    {
        // The arrows sit beside the scrolled area, so the viewport is exactly what the tabs may use.
        if (scroll.Presenter is not { } presenter || scroll.Viewport.Width <= 0) return;
        foreach (var item in strip.GetVisualDescendants().OfType<ContentControl>().Where(c => c is DocumentTabStripItem or ToolTabStripItem))
        {
            if (item.TranslatePoint(default, presenter) is not { } at) continue;
            item.Classes.Set(CutClass, at.X < -0.5 || at.X + item.Bounds.Width > presenter.Bounds.Width + 0.5);
        }
    }
}
