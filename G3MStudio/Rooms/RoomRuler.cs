using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Layout;

namespace G3MStudio.Rooms;

/// <summary>A ruler along one edge of a <see cref="RoomView"/>: ticks and numbers in room pixels that follow its zoom and scroll. Pressing it and releasing over the room adds a guide line.</summary>
public sealed class RoomRuler : Control
{
    public const double Thickness = 20;

    private readonly RoomView _view;
    private readonly Orientation _orientation;
    private double? _dragging;

    public RoomRuler(RoomView view, Orientation orientation)
    {
        (_view, _orientation) = (view, orientation);
        Name = orientation == Orientation.Horizontal ? "RulerTop" : "RulerLeft";
        if (orientation == Orientation.Horizontal) Height = Thickness;
        else Width = Thickness;
        view.ViewChanged += InvalidateVisual;
    }

    public override void Render(DrawingContext context)
    {
        context.FillRectangle(new SolidColorBrush(Color.FromRgb(30, 30, 34)), new Rect(Bounds.Size));
        var horizontal = _orientation == Orientation.Horizontal;
        var length = horizontal ? Bounds.Width : Bounds.Height;
        var start = horizontal ? _view.Offset.X : _view.Offset.Y;
        var step = NiceStep(60 / _view.Zoom);
        var pen = new Pen(Brushes.Gray, 1);
        for (var value = Math.Floor(start / step) * step; (value - start) * _view.Zoom <= length; value += step)
        {
            var at = (value - start) * _view.Zoom;
            if (at < 0) continue;
            var label = new FormattedText(value.ToString("0", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Typeface.Default, 9, Brushes.Gray);
            if (horizontal)
            {
                context.DrawLine(pen, new Point(at, Thickness - 8), new Point(at, Thickness));
                context.DrawText(label, new Point(at + 2, 0));
            }
            else
            {
                context.DrawLine(pen, new Point(Thickness - 8, at), new Point(Thickness, at));
                context.DrawText(label, new Point(0, at + 2));
            }
        }
    }

    /// <summary>1, 2, 5 times a power of ten that is at least <paramref name="minimum"/>.</summary>
    public static double NiceStep(double minimum)
    {
        var power = Math.Pow(10, Math.Floor(Math.Log10(Math.Max(minimum, 1e-6))));
        foreach (var factor in new[] { 1.0, 2.0, 5.0, 10.0 })
            if (factor * power >= minimum) return factor * power;
        return 10 * power;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        _dragging = Along(e);
        e.Pointer.Capture(this);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        if (_dragging is null) return;
        _dragging = Along(e);
        _view.PreviewGuide(_orientation == Orientation.Horizontal, _dragging);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        if (_dragging is not null)
        {
            // A drag from the top ruler makes a horizontal line (a y position), so the position comes from the other axis.
            var position = _orientation == Orientation.Horizontal ? _view.ToRoom(e.GetPosition(_view)).Y : _view.ToRoom(e.GetPosition(_view)).X;
            var inside = _orientation == Orientation.Horizontal ? e.GetPosition(_view).Y > 0 : e.GetPosition(_view).X > 0;
            _view.PreviewGuide(_orientation == Orientation.Horizontal, null);
            if (inside) _view.AddGuide(_orientation == Orientation.Horizontal, position);
        }

        _dragging = null;
        e.Pointer.Capture(null);
    }

    private double Along(PointerEventArgs e) => _orientation == Orientation.Horizontal ? _view.ToRoom(e.GetPosition(_view)).Y : _view.ToRoom(e.GetPosition(_view)).X;
}
