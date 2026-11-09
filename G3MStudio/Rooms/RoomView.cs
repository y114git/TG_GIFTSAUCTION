using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using G3MStudio.Core.Workspace;
using static G3MLib.DataFile.Models.GameMakerRoom;

namespace G3MStudio.Rooms;

public enum RoomTool { None, Place, Paint, Erase, Fill, Rectangle, Pick, Line, Select }

/// <summary>
/// Draws a <see cref="RoomScene"/> and edits its object instances. Left-drag on empty space draws a marquee, middle-drag or Space+drag pans,
/// Ctrl+wheel zooms (6.25% to 3200%), clicking an instance selects it (Shift adds, Ctrl toggles), dragging a selected instance moves the
/// selection, arrow keys nudge it, and Delete, Ctrl+C, Ctrl+V, X and Y are reported as requests. The view never changes the model: it
/// reports one request per gesture so one gesture is one undo step. G toggles the grid and Shift+G snapping.
/// </summary>
public sealed partial class RoomView : Control
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

    /// <summary>Ctrl+D: the selection and the offset (one grid step) at which to put the copies.</summary>
    public event Action<IReadOnlyList<object>, Point>? DuplicateRequested;

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

    /// <summary>True while P is held: a click picks elements of locked layers too (select from any layer).</summary>
    public bool IgnoreLocks { get; set; }

    private bool IsLocked(object element) => !IgnoreLocks && Locked.Contains(element);

    public object? HitTest(Point room)
    {
        if (Scene is null) return null;
        foreach (var marker in Scene.Markers)
            if (marker.Bounds.Contains(room) && !IsLocked(marker.Element)) return marker.Element;
        for (var i = Scene.Items.Count - 1; i >= 0; i--)
            if (Scene.Items[i].Element is { } element && !IsLocked(element) && Scene.Items[i].Dest.Contains(room)) return element;
        return null;
    }

    private double Align(double value, double cell, List<double> guides)
    {
        var reach = 8 / _zoom;
        var near = guides.OrderBy(g => Math.Abs(g - value)).Select(g => (double?)g).FirstOrDefault();
        return near is { } guide && Math.Abs(guide - value) <= reach ? guide : Snap(value, cell);
    }

    /// <summary>Zooms by a factor and keeps the room point under <paramref name="screen"/> where it is.</summary>
    public void ZoomAt(Point screen, double factor)
    {
        var room = ToRoom(screen);
        _zoom = Math.Clamp(_zoom * factor, MinZoom, MaxZoom);
        Offset = new Point(room.X - screen.X / _zoom, room.Y - screen.Y / _zoom);
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
}
