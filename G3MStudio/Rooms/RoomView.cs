using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using G3MStudio.Core.Workspace;
using static G3MLib.DataFile.Models.GameMakerRoom;

namespace G3MStudio.Rooms;

public enum RoomTool { None, Place, Paint, Erase, Fill, Rectangle, Pick }

/// <summary>
/// Draws a <see cref="RoomScene"/> and edits its object instances. Left-drag on empty space draws a marquee, middle-drag or Space+drag pans,
/// Ctrl+wheel zooms (6.25% to 3200%), clicking an instance selects it (Shift adds, Ctrl toggles), dragging a selected instance moves the
/// selection, arrow keys nudge it, and Delete, Ctrl+C, Ctrl+V, X and Y are reported as requests. The view never changes the model: it
/// reports one request per gesture so one gesture is one undo step. G toggles the grid and Shift+G snapping.
/// </summary>
public sealed class RoomView : Control
{
    private const double MinZoom = 0.0625;
    private const double MaxZoom = 32;

    private readonly HashSet<object> _selection = new(ReferenceEqualityComparer.Instance);
    private Point _offset;
    private double _zoom = 1;
    private Point? _panFrom;
    private bool _space;
    private bool _moving;
    private Point _gestureStart;
    private Vector _moveDelta;
    private Rect? _marquee;
    private Point _pointer;
    private List<(int X, int Y)>? _stroke;
    private GameObject? _paintSource;
    private List<Point> _paintPoints = [];
    private HashSet<(int X, int Y)> _strokeCells = [];

    public RoomView()
    {
        Focusable = true;
        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);
        RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.None);
    }

    public RoomScene? Scene { get; set { field = value; _offset = default; InvalidateVisual(); } }

    /// <summary>Top-left corner of the visible area in room pixels.</summary>
    public Point Offset { get => _offset; set { _offset = value; ViewChanged?.Invoke(); InvalidateVisual(); } }

    public double Zoom { get => _zoom; set { _zoom = Math.Clamp(value, MinZoom, MaxZoom); ViewChanged?.Invoke(); InvalidateVisual(); } }

    /// <summary>Raised when the scroll or zoom changed, so rulers can redraw.</summary>
    public event Action? ViewChanged;

    /// <summary>Guide lines in room pixels: x positions of vertical guides and y positions of horizontal ones. Objects dragged near one snap to it (within 8 screen pixels).</summary>
    public List<double> VerticalGuides { get; } = [];

    public List<double> HorizontalGuides { get; } = [];

    private (bool Horizontal, double Position)? _guidePreview;

    public void AddGuide(bool horizontal, double position)
    {
        (horizontal ? HorizontalGuides : VerticalGuides).Add(Math.Round(position));
        InvalidateVisual();
    }

    /// <summary>Shows a guide being dragged out of a ruler; null removes the preview.</summary>
    public void PreviewGuide(bool horizontal, double? position)
    {
        _guidePreview = position is { } p ? (horizontal, p) : null;
        InvalidateVisual();
    }

    /// <summary>Removes the guide nearest to a point when it is within 4 screen pixels.</summary>
    public bool RemoveGuideAt(Point room)
    {
        var reach = 4 / _zoom;
        if (VerticalGuides.FirstOrDefault(g => Math.Abs(g - room.X) <= reach, double.NaN) is var v && !double.IsNaN(v)) VerticalGuides.Remove(v);
        else if (HorizontalGuides.FirstOrDefault(g => Math.Abs(g - room.Y) <= reach, double.NaN) is var h && !double.IsNaN(h)) HorizontalGuides.Remove(h);
        else return false;
        InvalidateVisual();
        return true;
    }

    public bool ShowGrid { get; set { field = value; InvalidateVisual(); OptionsChanged?.Invoke(); } }

    public bool SnapToGrid { get; set { field = value; OptionsChanged?.Invoke(); } }

    /// <summary>Grid, snap or the view outlines were switched on or off (by a key or a toolbar button).</summary>
    public event Action? OptionsChanged;

    /// <summary>The camera rectangles of the enabled views, drawn as outlines while ShowViews is on.</summary>
    public IReadOnlyList<Rect> Views { get; set { field = value; InvalidateVisual(); } } = [];

    public bool ShowViews { get; set { field = value; InvalidateVisual(); OptionsChanged?.Invoke(); } }

    public double GridWidth { get; set { field = value > 0 ? value : 16; InvalidateVisual(); } } = 16;

    public double GridHeight { get; set { field = value > 0 ? value : 16; InvalidateVisual(); } } = 16;

    public int DrawnItems { get; private set; }

    /// <summary>The tile layer that painting changes; with <see cref="Tool"/> set the pointer paints instead of selecting.</summary>
    public Layer? TileLayer { get; set; }

    public RoomTool Tool { get; set; }

    /// <summary>A finished stroke: the layer, the tool and the cells it touched (a fill reports only its start cell).</summary>
    public event Action<Layer, RoomTool, IReadOnlyList<(int X, int Y)>>? TilesPainted;

    /// <summary>The cell of the tile layer under a point in room pixels, or null outside the layer or when it has no tile set.</summary>
    public (int X, int Y)? TileCell(Point room)
    {
        if (TileLayer is not { TilesData: { Background: { } set, TileData: { } rows } } layer || set.GMS2TileWidth == 0 || set.GMS2TileHeight == 0) return null;
        var x = (int)Math.Floor((room.X - layer.XOffset) / set.GMS2TileWidth);
        var y = (int)Math.Floor((room.Y - layer.YOffset) / set.GMS2TileHeight);
        return y >= 0 && y < rows.Length && x >= 0 && x < rows[y].Length ? (x, y) : null;
    }

    public IReadOnlyCollection<object> Selection => _selection;

    /// <summary>Elements of locked layers: they are drawn but cannot be picked.</summary>
    public HashSet<object> Locked { get; } = new(ReferenceEqualityComparer.Instance);

    /// <summary>The selected instances and how far to move them in room pixels (already snapped).</summary>
    public event Action<IReadOnlyList<object>, int, int>? Moved;

    public event Action<IReadOnlyList<object>>? DeleteRequested;

    public event Action<IReadOnlyList<object>>? CopyRequested;

    /// <summary>Alt+drag over an instance: the instance and the places (room pixels, snapped when snapping is on) to put a copy of it, not counting its own place.</summary>
    public event Action<GameObject, IReadOnlyList<Point>>? CopiesPainted;

    /// <summary>Where to paste, in room pixels (snapped when snapping is on).</summary>
    public event Action<Point>? PasteRequested;

    /// <summary>The selection and whether to mirror it horizontally (otherwise vertically).</summary>
    public event Action<IReadOnlyList<object>, bool>? FlipRequested;

    public event Action? SelectionChanged;

    /// <summary>The pointer is over the canvas: the room position, the position snapped to the grid, and the element under it.</summary>
    public event Action<Point, Point, object?>? Hovered;

    /// <summary>A resource was dropped from the Explorer: its tab key and the room position of the drop.</summary>
    public event Action<string, Point>? ResourceDropped;

    /// <summary>The Place tool was clicked: the room position, snapped to the grid when snapping is on.</summary>
    public event Action<Point>? PlaceRequested;

    private void OnDragOver(object? sender, DragEventArgs e) => e.DragEffects = e.DataTransfer.TryGetValue(ResourceDrag.Format) is not null ? DragDropEffects.Copy : DragDropEffects.None;

    private void OnDrop(object? sender, DragEventArgs e)
    {
        if (e.DataTransfer.TryGetValue(ResourceDrag.Format) is { } key) ResourceDropped?.Invoke(key, PaintPoint(ToRoom(e.GetPosition(this))));
    }

    public void Select(IEnumerable<object> instances)
    {
        _selection.Clear();
        foreach (var instance in instances) _selection.Add(instance);
        SelectionChanged?.Invoke();
        InvalidateVisual();
    }

    public object? HitTest(Point room)
    {
        if (Scene is null) return null;
        foreach (var marker in Scene.Markers)
            if (marker.Bounds.Contains(room) && !Locked.Contains(marker.Element)) return marker.Element;
        for (var i = Scene.Items.Count - 1; i >= 0; i--)
            if (Scene.Items[i].Element is { } element && !Locked.Contains(element) && Scene.Items[i].Dest.Contains(room)) return element;
        return null;
    }

    private double Align(double value, double cell, List<double> guides)
    {
        var reach = 8 / _zoom;
        var near = guides.OrderBy(g => Math.Abs(g - value)).Select(g => (double?)g).FirstOrDefault();
        return near is { } guide && Math.Abs(guide - value) <= reach ? guide : Snap(value, cell);
    }

    public Point ToRoom(Point screen) => new(screen.X / _zoom + _offset.X, screen.Y / _zoom + _offset.Y);

    /// <summary>Fits the whole room in the view, or shows it at 100% when it is small.</summary>
    public void FitRoom()
    {
        if (Scene is null || Bounds.Width <= 0 || Bounds.Height <= 0) return;
        Zoom = Math.Min(1, Math.Min(Bounds.Width / Math.Max(1, Scene.Width), Bounds.Height / Math.Max(1, Scene.Height)));
        Offset = default;
    }

    private Point PaintPoint(Point room) => new(Math.Round(Snap(room.X, GridWidth)), Math.Round(Snap(room.Y, GridHeight)));

    private double Snap(double value, double cell) => SnapToGrid ? Math.Round(value / cell) * cell : value;

    private (int X, int Y) SnappedDelta(object anchor, Vector raw)
    {
        var (x, y) = (RoomElements.X(anchor), RoomElements.Y(anchor));
        return ((int)Math.Round(Align(x + raw.X, GridWidth, VerticalGuides) - x), (int)Math.Round(Align(y + raw.Y, GridHeight, HorizontalGuides) - y));
    }

    /// <summary>Draws the whole room at 100% (visible layers only, no selection or grid) and writes it as a PNG. Rooms over 16384 pixels on a side are refused.</summary>
    public void SavePng(Stream stream)
    {
        if (Scene is not { } scene) throw new InvalidOperationException("The room has nothing to draw.");
        if (scene.Width > 16384 || scene.Height > 16384) throw new InvalidOperationException("The room is larger than 16384 pixels on a side.");
        using var target = new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize(Math.Max(1, (int)scene.Width), Math.Max(1, (int)scene.Height)));
        using (var context = target.CreateDrawingContext(true))
        {
            context.FillRectangle(Brushes.Black, new Rect(0, 0, scene.Width, scene.Height));
            foreach (var item in scene.Items) DrawItem(context, item, item.Dest);
        }

        target.Save(stream, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
    }

    private static void DrawItem(DrawingContext context, RoomItem item, Rect dest)
    {
        if (item.Flags == 0)
        {
            context.DrawImage(item.Page, item.Source, dest);
            return;
        }

        var centre = dest.Center;
        var matrix = Matrix.CreateTranslation(-centre.X, -centre.Y);
        if ((item.Flags & 1) != 0) matrix *= Matrix.CreateScale(-1, 1);
        if ((item.Flags & 2) != 0) matrix *= Matrix.CreateScale(1, -1);
        if ((item.Flags & 4) != 0) matrix *= Matrix.CreateRotation(Math.PI / 2);
        using var transform = context.PushTransform(matrix * Matrix.CreateTranslation(centre.X, centre.Y));
        context.DrawImage(item.Page, item.Source, dest);
    }

    public override void Render(DrawingContext context)
    {
        context.FillRectangle(Brushes.Black, new Rect(Bounds.Size));
        DrawnItems = 0;
        if (Scene is null) return;
        var view = new Rect(_offset, new Size(Bounds.Width / _zoom, Bounds.Height / _zoom));
        using var clip = context.PushClip(new Rect(Bounds.Size));
        using var move = context.PushTransform(Matrix.CreateTranslation(-_offset.X, -_offset.Y) * Matrix.CreateScale(_zoom, _zoom));
        var outline = new Pen(Brushes.DeepSkyBlue, 1 / _zoom);
        var delta = _moving && _selection.Count > 0 ? MoveDelta() : default;
        foreach (var item in Scene.Items)
        {
            var selected = item.Element is not null && _selection.Contains(item.Element);
            var dest = selected && _moving ? item.Dest.Translate(delta) : item.Dest;
            if (!dest.Intersects(view)) continue;
            DrawItem(context, item, dest);
            if (selected) context.DrawRectangle(null, outline, dest);
            DrawnItems++;
        }

        var markerPen = new Pen(Brushes.Orange, 1 / _zoom) { DashStyle = DashStyle.Dash };
        foreach (var marker in Scene.Markers)
        {
            var selected = _selection.Contains(marker.Element);
            var bounds = selected && _moving ? marker.Bounds.Translate(delta) : marker.Bounds;
            if (!bounds.Intersects(view)) continue;
            context.DrawRectangle(null, selected ? outline : markerPen, bounds);
            var text = new FormattedText(marker.Label, System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, Typeface.Default, 11 / _zoom, Brushes.White);
            context.DrawText(text, bounds.TopLeft);
        }

        if (_paintSource is not null)
            foreach (var place in _paintPoints)
                context.DrawEllipse(new SolidColorBrush(Colors.DeepSkyBlue, 0.5), null, place, 3 / _zoom, 3 / _zoom);

        var guidePen = new Pen(Brushes.Cyan, 1 / _zoom);
        foreach (var x in VerticalGuides) context.DrawLine(guidePen, new Point(x, view.Top), new Point(x, view.Bottom));
        foreach (var y in HorizontalGuides) context.DrawLine(guidePen, new Point(view.Left, y), new Point(view.Right, y));
        if (_guidePreview is { } preview)
            context.DrawLine(guidePen, preview.Horizontal ? new Point(view.Left, preview.Position) : new Point(preview.Position, view.Top), preview.Horizontal ? new Point(view.Right, preview.Position) : new Point(preview.Position, view.Bottom));

        if (ShowGrid) DrawGrid(context, view);
        if (ShowViews)
        {
            var viewPen = new Pen(Brushes.Orange, 2 / _zoom, DashStyle.Dash);
            foreach (var camera in Views) context.DrawRectangle(null, viewPen, camera);
        }

        if (_stroke is not null && TileLayer is { TilesData.Background: { } set } tileLayer)
            foreach (var (x, y) in _stroke)
                context.DrawRectangle(new SolidColorBrush(Colors.DeepSkyBlue, 0.35), null, new Rect(tileLayer.XOffset + x * set.GMS2TileWidth, tileLayer.YOffset + y * set.GMS2TileHeight, set.GMS2TileWidth, set.GMS2TileHeight));
        if (_marquee is { } marquee) context.DrawRectangle(null, outline, marquee);
    }

    private void DrawGrid(DrawingContext context, Rect view)
    {
        // Lines closer than 3 screen pixels are only noise, and each one costs a draw call.
        if (GridWidth * _zoom < 3 || GridHeight * _zoom < 3) return;
        var pen = new Pen(new SolidColorBrush(Colors.White, 0.25), 1 / _zoom);
        var right = Math.Min(view.Right, Scene?.Width ?? view.Right);
        var bottom = Math.Min(view.Bottom, Scene?.Height ?? view.Bottom);
        for (var x = Math.Max(0, Math.Floor(view.Left / GridWidth) * GridWidth); x <= right; x += GridWidth)
            context.DrawLine(pen, new Point(x, Math.Max(0, view.Top)), new Point(x, bottom));
        for (var y = Math.Max(0, Math.Floor(view.Top / GridHeight) * GridHeight); y <= bottom; y += GridHeight)
            context.DrawLine(pen, new Point(Math.Max(0, view.Left), y), new Point(right, y));
    }

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
        if (_paintSource is not null)
        {
            var next = PaintPoint(ToRoom(at));
            if (next != _paintPoints[^1]) _paintPoints.Add(next);
            InvalidateVisual();
        }
        else if (_stroke is not null)
        {
            if (TileCell(ToRoom(at)) is { } cell)
            {
                if (Tool == RoomTool.Rectangle)
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
            var at = e.GetPosition(this);
            var room = ToRoom(at);
            _zoom = Math.Clamp(_zoom * (e.Delta.Y > 0 ? 1.25 : 0.8), MinZoom, MaxZoom);
            Offset = new Point(room.X - at.X / _zoom, room.Y - at.Y / _zoom);
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
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
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
                PasteRequested?.Invoke(new Point(Snap(room.X, GridWidth), Snap(room.Y, GridHeight)));
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
                Zoom = _zoom * 1.25;
                break;
            case Key.OemMinus or Key.Subtract when ctrl:
                Zoom = _zoom * 0.8;
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
