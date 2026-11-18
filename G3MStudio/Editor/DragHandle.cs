using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace G3MStudio.Editor;

/// <summary>A thin bar under a control that sets the control's height when dragged. The height is kept per key until the app closes, so the next tab of the same kind opens as it was left.</summary>
public static class DragHandle
{
    private static readonly Dictionary<string, double> Sizes = [];
    public const double MinHeight = 40;

    /// <summary>The height last dragged for a key, or null.</summary>
    public static double? Remembered(string key) => Sizes.TryGetValue(key, out var h) ? h : null;

    /// <summary>Gives <paramref name="target"/> its remembered height and returns the bar to put right under it; <paramref name="dragged"/> gets the height when a drag ends (for a setting that outlives the app).</summary>
    public static Border For(Control target, string key, Action<double>? dragged = null)
    {
        if (Remembered(key) is { } saved) target.Height = saved;
        var bar = new Border
        {
            Name = "ResizeHandle_" + key,
            Height = 8,
            Background = Brushes.Transparent,
            Cursor = new Cursor(StandardCursorType.SizeNorthSouth),
            Child = new Border { Height = 2, Margin = new Thickness(40, 3), CornerRadius = new CornerRadius(1), Background = Brushes.Gray, Opacity = 0.5 },
        };
        ToolTip.SetTip(bar, G3MStudio.Localization.Loc.Current["fonts.dragResize"]);
        double startY = 0, startHeight = 0;
        bool dragging = false, moved = false;
        bar.PointerPressed += (_, e) =>
        {
            if (TopLevel.GetTopLevel(bar) is not { } top || !e.GetCurrentPoint(bar).Properties.IsLeftButtonPressed) return;
            (dragging, moved, startY, startHeight) = (true, false, e.GetPosition(top).Y, target.Bounds.Height);
            e.Pointer.Capture(bar);
            e.Handled = true;
        };
        void Finish()
        {
            dragging = false;
            if (moved) dragged?.Invoke(Sizes[key]);
            moved = false;
        }

        bar.PointerMoved += (_, e) =>
        {
            if (!dragging) return;
            // A button released outside the window sends no release: a move without the left button ends the drag.
            if (!e.GetCurrentPoint(bar).Properties.IsLeftButtonPressed) Finish();
            else if (TopLevel.GetTopLevel(bar) is { } top)
            {
                Resize(target, key, startHeight + e.GetPosition(top).Y - startY);
                moved = true;
            }
        };
        bar.PointerReleased += (_, e) =>
        {
            e.Pointer.Capture(null);
            Finish();
        };
        bar.PointerCaptureLost += (_, _) => Finish();
        return bar;
    }

    /// <summary>The control with its drag bar under it, as one child for a parent that is not a vertical stack.</summary>
    public static StackPanel Under(Control target, string key) => new() { Children = { target, For(target, key) } };

    /// <summary>Sets and remembers a height (never below <see cref="MinHeight"/>).</summary>
    public static void Resize(Control target, string key, double height)
    {
        target.Height = Sizes[key] = Math.Max(MinHeight, height);
    }
}
