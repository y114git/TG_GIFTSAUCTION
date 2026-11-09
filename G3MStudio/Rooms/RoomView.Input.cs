using Avalonia;
using Avalonia.Input;
using G3MStudio.Core.Workspace;
using static G3MLib.DataFile.Models.GameMakerRoom;

namespace G3MStudio.Rooms;

public sealed partial class RoomView
{
    private Vector MoveDelta()
    {
        var anchor = _selection.First();
        var (x, y) = SnappedDelta(anchor, _moveDelta);
        return new Vector(x, y);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        Focus();
        var at = e.GetPosition(this);
        var point = e.GetCurrentPoint(this).Properties;
        if (point.IsRightButtonPressed && RemoveGuideAt(ToRoom(at))) return;
        if (Tool == RoomTool.Place && !_space && point.IsLeftButtonPressed)
        {
            PlaceRequested?.Invoke(PaintPoint(ToRoom(at)));
            return;
        }

        if (point.IsLeftButtonPressed && !_space && Floating is not null)
        {
            DropFloating(ToRoom(at));
            return;
        }

        if (point.IsLeftButtonPressed && !_space && BeginTransform(ToRoom(at)))
        {
            e.Pointer.Capture(this);
            return;
        }

        if (Tool is not (RoomTool.None or RoomTool.Place) && !_space && point.IsLeftButtonPressed && TileCell(ToRoom(at)) is { } first)
        {
            (_stroke, _strokeCells) = ([first], [first]);
            InvalidateVisual();
            e.Pointer.Capture(this);
            return;
        }

        if (point.IsMiddleButtonPressed || _space)
        {
            _panFrom = at / _zoom + _offset;
        }
        else
        {
            var hit = HitTest(ToRoom(at));
            if (hit is GameObject source && e.KeyModifiers.HasFlag(KeyModifiers.Alt))
            {
                (_paintSource, _paintPoints) = (source, [PaintPoint(ToRoom(at))]);
                InvalidateVisual();
                e.Pointer.Capture(this);
                return;
            }

            var add = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
            var toggle = e.KeyModifiers.HasFlag(KeyModifiers.Control);
            if (hit is not null)
            {
                if (toggle && !_selection.Add(hit)) _selection.Remove(hit);
                else if (!toggle && !_selection.Contains(hit))
                {
                    if (!add) _selection.Clear();
                    _selection.Add(hit);
                }

                _moving = _selection.Contains(hit) && !toggle;
            }
            else if (!add && !toggle)
            {
                _selection.Clear();
            }

            _gestureStart = at;
            _moveDelta = default;
            if (hit is null) _marquee = new Rect(ToRoom(at), default(Size));
        }

        SelectionChanged?.Invoke();
        InvalidateVisual();
        e.Pointer.Capture(this);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        var at = e.GetPosition(this);
        _pointer = at;
        var here = ToRoom(at);
        Hovered?.Invoke(here, PaintPoint(here), HitTest(here));
        if (Floating is not null && CellAt(here) is { } over)
        {
            _floatAt = over;
            InvalidateVisual();
        }

        if (_handle != Handle.None)
        {
            DragTransform(here, e.KeyModifiers);
        }
        else if (_paintSource is not null)
        {
            var next = PaintPoint(ToRoom(at));
            if (next != _paintPoints[^1]) _paintPoints.Add(next);
            InvalidateVisual();
        }
        else if (_stroke is not null)
        {
            if (TileCell(ToRoom(at)) is { } cell)
            {
                if (Tool == RoomTool.Line)
                {
                    _stroke = TileBlocks.Line(_stroke[0], cell);
                }
                else if (Tool is RoomTool.Rectangle or RoomTool.Select)
                {
                    var (a, b) = (_stroke[0], cell);
                    _stroke = [.. Enumerable.Range(Math.Min(a.X, b.X), Math.Abs(a.X - b.X) + 1).SelectMany(x => Enumerable.Range(Math.Min(a.Y, b.Y), Math.Abs(a.Y - b.Y) + 1).Select(y => (x, y)))];
                    _stroke.Remove(a);
                    _stroke.Insert(0, a);
                }
                else if (Tool is not (RoomTool.Fill or RoomTool.Pick) && _strokeCells.Add(cell))
                {
                    _stroke.Add(cell);
                }
            }

            InvalidateVisual();
        }
        else if (_panFrom is { } from) Offset = from - at / _zoom;
        else if (_moving)
        {
            _moveDelta = (at - _gestureStart) / _zoom;
            InvalidateVisual();
        }
        else if (_marquee is { } marquee)
        {
            _marquee = new Rect(marquee.TopLeft, ToRoom(at)).Normalize();
            InvalidateVisual();
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        if (_handle != Handle.None)
        {
            EndTransform();
            e.Pointer.Capture(null);
            return;
        }

        if (_paintSource is { } painted)
        {
            var origin = new Point(painted.X, painted.Y);
            var places = _paintPoints.Where(p => p != origin).Distinct().ToList();
            _paintSource = null;
            _paintPoints = [];
            if (places.Count > 0) CopiesPainted?.Invoke(painted, places);
            e.Pointer.Capture(null);
            InvalidateVisual();
            return;
        }

        if (_stroke is { } stroke)
        {
            _stroke = null;
            if (TileLayer is { } layer) TilesPainted?.Invoke(layer, Tool, stroke);
            e.Pointer.Capture(null);
            InvalidateVisual();
            return;
        }

        if (_moving && _selection.Count > 0)
        {
            var (dx, dy) = SnappedDelta(_selection.First(), _moveDelta);
            if (dx != 0 || dy != 0) Moved?.Invoke([.. _selection], dx, dy);
        }
        else if (_marquee is { } box && Scene is not null)
        {
            foreach (var item in Scene.Items)
                if (item.Element is { } element && !Locked.Contains(element) && item.Dest.Intersects(box)) _selection.Add(element);
            foreach (var marker in Scene?.Markers ?? [])
                if (!Locked.Contains(marker.Element) && marker.Bounds.Intersects(box)) _selection.Add(marker.Element);
        }

        SelectionChanged?.Invoke();
        (_moving, _moveDelta, _marquee, _panFrom) = (false, default, null, null);
        e.Pointer.Capture(null);
        InvalidateVisual();
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            ZoomAt(e.GetPosition(this), e.Delta.Y > 0 ? 1.25 : 0.8);
        }
        else if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            Offset = new Point(_offset.X - e.Delta.Y * 40 / _zoom, _offset.Y);
        }
        else
        {
            Offset = new Point(_offset.X, _offset.Y - e.Delta.Y * 40 / _zoom);
        }

        e.Handled = true;
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        if (e.Key == Key.Space) _space = false;
        if (e.Key == Key.P) IgnoreLocks = false;
    }

    /// <summary>A key pressed without Ctrl while a tile layer is chosen: the editor turns it into a tool or a flag change and returns true when it used it (UTMT-KEY-06).</summary>
    public event Func<Key, bool>? TileKeyPressed;

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.P && e.KeyModifiers == KeyModifiers.None && TileLayer is null) IgnoreLocks = true;
        if (TileKeyOperation(e))
        {
            e.Handled = true;
            return;
        }

        if (TileLayer is not null && !e.KeyModifiers.HasFlag(KeyModifiers.Control) && !e.KeyModifiers.HasFlag(KeyModifiers.Alt) && TileKeyPressed?.Invoke(e.Key) == true)
        {
            e.Handled = true;
            return;
        }

        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        IReadOnlyList<object> selected = [.. _selection];
        var step = (SnapToGrid ? (e.Key is Key.Left or Key.Right ? GridWidth : GridHeight) : 1) * (shift ? 8 : 1);
        switch (e.Key)
        {
            case Key.Space:
                _space = true;
                break;
            case Key.Left when selected.Count > 0:
                Moved?.Invoke(selected, -(int)step, 0);
                break;
            case Key.Right when selected.Count > 0:
                Moved?.Invoke(selected, (int)step, 0);
                break;
            case Key.Up when selected.Count > 0:
                Moved?.Invoke(selected, 0, -(int)step);
                break;
            case Key.Down when selected.Count > 0:
                Moved?.Invoke(selected, 0, (int)step);
                break;
            case Key.Delete when selected.Count > 0:
                DeleteRequested?.Invoke(selected);
                break;
            case Key.C when ctrl && selected.Count > 0:
                CopyRequested?.Invoke(selected);
                break;
            case Key.V when ctrl:
                var room = ToRoom(_pointer);
                PasteRequested?.Invoke(shift ? new Point(Math.Round(room.X), Math.Round(room.Y)) : new Point(Snap(room.X, GridWidth), Snap(room.Y, GridHeight)));
                break;
            case Key.D when ctrl && selected.Count > 0:
                DuplicateRequested?.Invoke(selected, new Point(SnapToGrid ? GridWidth : 16, SnapToGrid ? GridHeight : 16));
                break;
            case Key.A when ctrl && Scene is not null:
                Select(Scene.Items.Select(i => i.Element).OfType<object>().Concat(Scene.Markers.Select(m => m.Element)));
                break;
            case Key.X when !ctrl && selected.Count > 0:
                FlipRequested?.Invoke(selected, true);
                break;
            case Key.Y when !ctrl && selected.Count > 0:
                FlipRequested?.Invoke(selected, false);
                break;
            case Key.G when shift:
                SnapToGrid = !SnapToGrid;
                break;
            case Key.G:
                ShowGrid = !ShowGrid;
                break;
            case Key.V when !ctrl:
                ShowViews = !ShowViews;
                break;
            case Key.Escape:
                Select([]);
                break;
            case Key.OemPlus or Key.Add when ctrl:
                ZoomAt(new Point(Bounds.Width / 2, Bounds.Height / 2), 1.25);
                break;
            case Key.OemMinus or Key.Subtract when ctrl:
                ZoomAt(new Point(Bounds.Width / 2, Bounds.Height / 2), 0.8);
                break;
            case Key.D0 or Key.NumPad0 when ctrl && shift:
                Zoom = 1;
                break;
            case Key.D0 or Key.NumPad0 when ctrl:
                FitRoom();
                break;
            default:
                return;
        }

        e.Handled = true;
    }
}
