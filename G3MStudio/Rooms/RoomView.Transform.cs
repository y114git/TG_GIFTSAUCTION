using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using G3MStudio.Core.Workspace;
using static G3MLib.DataFile.Models.GameMakerRoom;

namespace G3MStudio.Rooms;

/// <summary>Scale and rotate handles around the selected instances. A drag previews the result and reports one request on release, so it is one undo step.</summary>
public sealed partial class RoomView
{
    private const double HandleSize = 8;
    private const double RotateReach = 24;
    private const double AngleStep = 15;

    private Handle _handle;
    private RoomBox _boxStart;
    private double _angleStart;
    private double _turn;
    private Matrix? _transform;
    private (double Fx, double Fy, double Ax, double Ay)? _scaling;
    private List<(GameObject Instance, Placement Start)> _starts = [];

    public event Action<IReadOnlyList<(GameObject Instance, Placement To)>>? Transformed;

    /// <summary>The bounds of the selected instances in room pixels, or null when no instance is selected.</summary>
    public RoomBox? SelectionBox()
    {
        if (Scene is null) return null;
        double left = double.MaxValue, top = double.MaxValue, right = double.MinValue, bottom = double.MinValue;
        var any = false;
        foreach (var instance in _selection)
        {
            if (instance is not GameObject || Scene.BoundsOf(instance) is not { } r) continue;
            (left, top, right, bottom, any) = (Math.Min(left, r.Left), Math.Min(top, r.Top), Math.Max(right, r.Right), Math.Max(bottom, r.Bottom), true);
        }

        return any ? new RoomBox(left, top, right, bottom) : null;
    }

    private IEnumerable<(Handle Handle, Point At)> HandlePoints(RoomBox b)
    {
        yield return (Handle.TopLeft, new Point(b.Left, b.Top));
        yield return (Handle.Top, new Point(b.CenterX, b.Top));
        yield return (Handle.TopRight, new Point(b.Right, b.Top));
        yield return (Handle.Right, new Point(b.Right, b.CenterY));
        yield return (Handle.BottomRight, new Point(b.Right, b.Bottom));
        yield return (Handle.Bottom, new Point(b.CenterX, b.Bottom));
        yield return (Handle.BottomLeft, new Point(b.Left, b.Bottom));
        yield return (Handle.Left, new Point(b.Left, b.CenterY));
        yield return (Handle.Rotate, new Point(b.CenterX, b.Top - RotateReach / _zoom));
    }

    private Handle HandleAt(Point room)
    {
        if (Tool != RoomTool.None || SelectionBox() is not { } box) return Handle.None;
        var reach = HandleSize / _zoom;
        var near = HandlePoints(box).Where(h => Math.Abs(room.X - h.At.X) <= reach && Math.Abs(room.Y - h.At.Y) <= reach)
            .OrderBy(h => (room.X - h.At.X) * (room.X - h.At.X) + (room.Y - h.At.Y) * (room.Y - h.At.Y)).ToList();
        return near.Count > 0 ? near[0].Handle : Handle.None;
    }

    private bool BeginTransform(Point room)
    {
        var handle = HandleAt(room);
        if (handle == Handle.None || SelectionBox() is not { } box) return false;
        (_handle, _boxStart) = (handle, box);
        _starts = [.. _selection.OfType<GameObject>().Select(i => (i, new Placement(i.X, i.Y, i.ScaleX, i.ScaleY, i.Rotation)))];
        _angleStart = RoomTransform.AngleOf(box.CenterX, box.CenterY, room.X, room.Y);
        return true;
    }

    private void DragTransform(Point room, KeyModifiers modifiers)
    {
        var (shift, alt) = (modifiers.HasFlag(KeyModifiers.Shift), modifiers.HasFlag(KeyModifiers.Alt));
        if (_handle == Handle.Rotate)
        {
            var (cx, cy) = (_boxStart.CenterX, _boxStart.CenterY);
            var raw = RoomTransform.AngleOf(cx, cy, room.X, room.Y) - _angleStart;
            _turn = SnapToGrid || shift ? RoomTransform.SnapAngle(raw, AngleStep) : Math.Round(raw);
            _scaling = null;
            _transform = Matrix.CreateTranslation(-cx, -cy) * Matrix.CreateRotation(-_turn * Math.PI / 180) * Matrix.CreateTranslation(cx, cy);
        }
        else
        {
            var f = RoomTransform.Factors(_boxStart, _handle, Snap(room.X, GridWidth), Snap(room.Y, GridHeight), shift, alt);
            _scaling = f;
            _transform = Matrix.CreateTranslation(-f.AnchorX, -f.AnchorY) * Matrix.CreateScale(f.Fx, f.Fy) * Matrix.CreateTranslation(f.AnchorX, f.AnchorY);
        }

        InvalidateVisual();
    }

    private void EndTransform()
    {
        var result = new List<(GameObject Instance, Placement To)>();
        foreach (var (instance, start) in _starts)
        {
            var to = _handle == Handle.Rotate
                ? RoomTransform.Rotate(start, _boxStart.CenterX, _boxStart.CenterY, _turn)
                : _scaling is { } s ? RoomTransform.Scale(start, s.Ax, s.Ay, s.Fx, s.Fy) : start;
            if (to != start) result.Add((instance, to));
        }

        (_handle, _transform, _scaling, _turn) = (Handle.None, null, null, 0);
        if (result.Count > 0) Transformed?.Invoke(result);
        InvalidateVisual();
    }

    private void DrawHandles(DrawingContext context)
    {
        if (_moving || SelectionBox() is not { } box) return;
        var pen = new Pen(G3MStudio.Theme.CanvasTheme.Line, 2 / _zoom);
        var size = HandleSize / _zoom;
        if (_transform is { } matrix)
        {
            using var preview = context.PushTransform(matrix);
            context.DrawRectangle(null, pen, new Rect(box.Left, box.Top, box.Width, box.Height));
            return;
        }

        context.DrawLine(pen, new Point(box.CenterX, box.Top), new Point(box.CenterX, box.Top - RotateReach / _zoom));
        foreach (var (handle, at) in HandlePoints(box))
        {
            if (handle == Handle.Rotate) context.DrawEllipse(Brushes.White, pen, at, size / 2, size / 2);
            else context.DrawRectangle(Brushes.White, pen, new Rect(at.X - size / 2, at.Y - size / 2, size, size));
        }
    }
}
