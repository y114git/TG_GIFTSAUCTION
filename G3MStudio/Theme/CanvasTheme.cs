using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace G3MStudio.Theme;

/// <summary>The theme colors of the views that draw themselves (rooms, paths, curves, timelines, previews), read at draw time so a changed color shows at once.</summary>
public static class CanvasTheme
{
    public static IBrush Bg => Get("Canvas.Bg");
    public static IBrush Stage => Get("Canvas.Stage");
    public static IBrush Line => Get("Canvas.Line");
    public static IBrush Point => Get("Canvas.Point");
    public static IBrush Guide => Get("Canvas.Guide");
    public static IBrush Handle => Get("Canvas.Handle");
    public static IBrush Hover => Get("Canvas.Hover");
    public static IBrush Muted => Get("Canvas.Muted");
    public static IBrush Selection => Get("Selection.Bg");

    /// <summary>The same brush at a lower opacity, for fills over a picture.</summary>
    public static IBrush Soft(IBrush brush, double opacity) => brush is ISolidColorBrush solid ? new SolidColorBrush(solid.Color, opacity) : brush;

    private static IBrush Get(string key) => Application.Current is { } app && app.TryGetResource(key, app.ActualThemeVariant, out var found) && found is IBrush brush ? brush : Brushes.Gray;

    /// <summary>Draws every open window again; the custom drawn views do not follow a resource change by themselves.</summary>
    public static void Redraw()
    {
        if (Application.Current?.ApplicationLifetime is not Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime lifetime) return;
        foreach (var window in lifetime.Windows)
            foreach (var visual in window.GetVisualDescendants().OfType<Control>()) visual.InvalidateVisual();
    }
}
