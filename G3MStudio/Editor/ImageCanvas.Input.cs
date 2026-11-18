using Avalonia;
using Avalonia.Input;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Editor;

public sealed partial class ImageCanvas
{
    private Point? _panFrom;
    private bool _drawing;
    private bool _secondary;
    private (int X, int Y) _start, _last;
    private (int X, int Y) _hoverCell;
    private KeyModifiers _modifiers;
    private PixelSelection? _before;
    private (int X, int Y)? _strokeEnd;
    private readonly List<(int X, int Y)> _points = [];
    private SmoothBrush? _brush;

    private Rgba Colour => _secondary ? Secondary : Primary;

    private bool Collecting => Tool is ImageTool.Polygon or ImageTool.Curve && _points.Count > 0;

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();
        var properties = e.GetCurrentPoint(this).Properties;
        // The middle button, Space with the left button and the hand tool all move the view and never touch the picture.
        if (properties.IsMiddleButtonPressed || ((_space || Tool == ImageTool.Hand) && properties.IsLeftButtonPressed))
        {
            _panFrom = e.GetPosition(this);
            e.Pointer.Capture(this);
            return;
        }

        if (!properties.IsLeftButtonPressed && !properties.IsRightButtonPressed) return;
        if (ResizeOn || _drawing) return; // another button during a canvas drag or a stroke neither draws nor ends it
        if (properties.IsLeftButtonPressed && BeginResize(e, e.GetPosition(this))) return;
        (_secondary, _modifiers) = (properties.IsRightButtonPressed, e.KeyModifiers);
        var cell = Cell(e.GetPosition(this));
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && Tool != ImageTool.Picker) PickAt(cell);
        else Begin(cell, e.ClickCount);
        if (_drawing) e.Pointer.Capture(this);
    }

    // The picker takes the colour on show: with layers that is the flat result, not the active layer alone (which can be empty or hidden there).
    private void PickAt(int x, int y)
    {
        if (!Picture.Contains(x, y)) return;
        if (Layers.IsPlain || Layers.Width != Picture.Width || Layers.Height != Picture.Height)
        {
            Picked?.Invoke(Picture.Get(x, y), _secondary);
            return;
        }

        if (_flat.Width != Picture.Width || _flat.Height != Picture.Height) _flat = new PixelBuffer(Picture.Width, Picture.Height);
        Layers.Composite(_flat, new Box(x, y, 1, 1), ActiveLayer, Picture);
        _flat.TakeDirty();
        Picked?.Invoke(_flat.Get(x, y), _secondary);
    }

    private void PickAt((int X, int Y) cell) => PickAt(cell.X, cell.Y);

    public event Action<(int X, int Y), Rgba>? TextRequested;

    private void Begin((int X, int Y) cell, int clicks)
    {
        if (Tool is not (ImageTool.Picker or ImageTool.Select or ImageTool.Lasso or ImageTool.Wand or ImageTool.Hand) && Refuse()) return;
        _before = State.Selection?.Copy();
        (_start, _last) = (cell, cell);
        switch (Tool)
        {
            case ImageTool.Picker: PickAt(cell); break;
            case ImageTool.Fill: Run(new ImageOp(ImageOps.Fill, ImageOp.Format(Colour), ImageOp.Format([cell]), ImageOp.Format(Tolerance), Contiguous ? "1" : "0")); break;
            case ImageTool.Replace: Run(new ImageOp(ImageOps.Replace, ImageOp.Format([cell]), ImageOp.Format(Colour), ImageOp.Format(Tolerance))); break;
            case ImageTool.Remove: Run(new ImageOp(ImageOps.Remove, ImageOp.Format([cell]), ImageOp.Format(Tolerance))); break;
            case ImageTool.Text: TextRequested?.Invoke(cell, Colour); break;
            case ImageTool.Wand: SetSelection(Combined(PixelSelection.Wand(Picture, cell.X, cell.Y, Tolerance, Contiguous))); break;
            case ImageTool.Pencil or ImageTool.Eraser: BeginStroke(cell); break;
            case ImageTool.Polygon or ImageTool.Curve: AddPoint(cell, clicks); break;
            default: _drawing = true; _points.Clear(); _points.Add(cell); break;
        }
    }

    private Rgba StrokeColour => Tool == ImageTool.Eraser ? Rgba.Clear : Colour;

    private void BeginStroke((int X, int Y) cell)
    {
        _drawing = true;
        _points.Clear();
        _brush = Smooth ? new SmoothBrush(Picture, StrokeColour, BrushSize) : null;
        // Shift draws a straight line from where the last stroke ended.
        if (_modifiers.HasFlag(KeyModifiers.Shift) && _strokeEnd is { } from)
        {
            _points.Add(from);
            DrawSegment(from, cell);
        }
        else DrawSegment(cell, cell);
        _points.Add(cell);
    }

    // The live stroke draws segment by segment with the same calls the replay makes (a stamp, then lines), so the pixels agree.
    private void DrawSegment((int X, int Y) from, (int X, int Y) to)
    {
        Picture.Clip = State.Selection;
        foreach (var copy in Core.Workspace.Symmetry.Copies([from, to], Picture.Width, Picture.Height, Symmetry))
        {
            if (_brush is { } brush) brush.Segment(copy[0].X + 0.5, copy[0].Y + 0.5, copy[1].X + 0.5, copy[1].Y + 0.5);
            else if (from == to) Picture.Stamp(copy[0].X, copy[0].Y, BrushSize, StrokeColour, Shape);
            else Picture.Line(copy[0].X, copy[0].Y, copy[1].X, copy[1].Y, BrushSize, StrokeColour, Shape);
        }

        Picture.Clip = null;
        Refresh();
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var at = e.GetPosition(this);
        if (_panFrom is { } from)
        {
            PanBy(at - from);
            _panFrom = at;
            return;
        }

        if (MoveResize(at, e.KeyModifiers)) return;
        _hoverCell = Cell(at);
        _modifiers = e.KeyModifiers;
        Hovered?.Invoke(_hoverCell);
        if (_drawing && Tool is ImageTool.Pencil or ImageTool.Eraser)
        {
            var next = _hoverCell;
            if (next != _last)
            {
                DrawSegment(_last, next);
                _points.Add(next);
                _last = next;
            }
        }
        else if (_drawing && Tool == ImageTool.Lasso && _points[^1] != _hoverCell) _points.Add(_hoverCell);

        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (ResizeOn)
        {
            // Only the left button that started the drag ends it; the capture goes after, so losing it does not cancel the drag first.
            if (e.InitialPressMouseButton != MouseButton.Left) return;
            EndResize(e.KeyModifiers);
            e.Pointer.Capture(null);
            return;
        }

        if (_panFrom is not null)
        {
            _panFrom = null;
            e.Pointer.Capture(null);
            return;
        }

        if (_drawing)
        {
            _drawing = false;
            Finish(Cell(e.GetPosition(this)));
            InvalidateVisual();
        }

        e.Pointer.Capture(null); // after the stroke is finished: losing the capture ends a stroke that is still on
    }

    // Shift adds the new shape to the selection the press started with, Alt cuts it out of that one.
    private PixelSelection Combined(PixelSelection made) =>
        _before is not { } before || before.Width != made.Width || before.Height != made.Height ? made
        : _modifiers.HasFlag(KeyModifiers.Shift) ? before.Union(made)
        : _modifiers.HasFlag(KeyModifiers.Alt) ? before.Subtract(made)
        : made;

    private void Finish((int X, int Y) end)
    {
        ReleaseShapePreview();
        switch (Tool)
        {
            case ImageTool.Pencil or ImageTool.Eraser:
                _strokeEnd = _points[^1];
                _brush = null;
                Emit(StrokeOps(_points));
                break;
            case ImageTool.Line: Run(StrokeOps([_start, end])); break;
            case ImageTool.Rectangle or ImageTool.Ellipse: RunShape(Corners(_start, end)); break;
            case ImageTool.Select: SetSelection(Combined(PixelSelection.Rectangle(Picture.Width, Picture.Height, _start.X, _start.Y, end.X, end.Y))); break;
            case ImageTool.Lasso: SetSelection(Combined(PixelSelection.Lasso(Picture.Width, Picture.Height, _points))); break;
            case ImageTool.Move when end != _start: Run(new ImageOp(ImageOps.Move, ImageOp.Format([(end.X - _start.X, end.Y - _start.Y)]))); break;
        }

        _points.Clear();
    }

    private ImageOp StrokeOp(IReadOnlyList<(int X, int Y)> points) => new(ImageOps.Stroke, ImageOp.Format(BrushSize), Shape == BrushShape.Round ? "1" : "0", ImageOp.Format(StrokeColour), ImageOp.Format(points));

    // A smooth stroke with mirrors is one op that carries every copy: the live stroke blends all copies through one brush, so the replay has to as well.
    private ImageOp[] StrokeOps(IReadOnlyList<(int X, int Y)> points)
    {
        var copies = Core.Workspace.Symmetry.Copies(points, Picture.Width, Picture.Height, Symmetry);
        if (!Smooth) return [.. copies.Select(StrokeOp)];
        return [new ImageOp(ImageOps.Smooth, ["line", ImageOp.Format(StrokeColour), "-", ImageOp.Format(BrushSize), .. copies.Select(ImageOp.Format)])];
    }

    /// <summary>The two corners of a rectangle or ellipse drag; Alt grows it from the middle, Shift keeps it square.</summary>
    private ((int X, int Y), (int X, int Y)) Corners((int X, int Y) from, (int X, int Y) to)
    {
        var (dx, dy) = (to.X - from.X, to.Y - from.Y);
        if (_modifiers.HasFlag(KeyModifiers.Shift)) (dx, dy) = (Math.Sign(dx) * Math.Max(Math.Abs(dx), Math.Abs(dy)), Math.Sign(dy) * Math.Max(Math.Abs(dx), Math.Abs(dy)));
        return _modifiers.HasFlag(KeyModifiers.Alt) ? ((from.X - dx, from.Y - dy), (from.X + dx, from.Y + dy)) : (from, (from.X + dx, from.Y + dy));
    }

    // A closed shape always has the outline in the primary colour and the fill in the secondary one, whichever mouse button draws it, so the two colours on the bar mean one thing.
    private (Rgba? Outline, Rgba? Fill) StyleColours() => Style switch { ShapeStyle.Filled => (null, Secondary), ShapeStyle.Both => (Primary, Secondary), _ => (Primary, null) };

    private void RunShape(((int X, int Y) A, (int X, int Y) B) box) => Run(ShapeOps(box));

    private void AddPoint((int X, int Y) cell, int clicks)
    {
        if (!(clicks >= 2 && _points.Count > 0)) _points.Add(cell);
        if (clicks >= 2 || (Tool == ImageTool.Curve && _points.Count == 3)) CommitPoints();
        InvalidateVisual();
    }

    /// <summary>Draws the polygon or curve of the clicked points (Enter or a double click).</summary>
    public void CommitPoints()
    {
        if (!Collecting) return;
        var points = _points.ToList();
        _points.Clear();
        if (Tool == ImageTool.Curve)
        {
            Run([.. Core.Workspace.Symmetry.Copies(points, Picture.Width, Picture.Height, Symmetry).Select(c => Smooth
                ? new ImageOp(ImageOps.Smooth, ImageOps.Curve, ImageOp.Format(Colour), "-", ImageOp.Format(BrushSize), ImageOp.Format(c))
                : new ImageOp(ImageOps.Curve, ImageOp.Format(BrushSize), Shape == BrushShape.Round ? "1" : "0", ImageOp.Format(Colour), ImageOp.Format(c)))]);
            return;
        }

        var (outline, fill) = StyleColours();
        Run([.. Core.Workspace.Symmetry.Copies(points, Picture.Width, Picture.Height, Symmetry).Select(c => Smooth
            ? new ImageOp(ImageOps.Smooth, ImageOps.Polygon, ImageOp.Format(outline), ImageOp.Format(fill), ImageOp.Format(BrushSize), ImageOp.Format(c))
            : new ImageOp(ImageOps.Polygon, ImageOp.Format(outline), ImageOp.Format(fill), ImageOp.Format(BrushSize), Shape == BrushShape.Round ? "1" : "0", ImageOp.Format(c)))]);
    }

    public void CancelPoints()
    {
        // The pixels of a pencil or eraser stroke are already on the picture, so Escape ends the stroke and reports it instead of dropping it.
        if (_drawing && Tool is ImageTool.Pencil or ImageTool.Eraser && _points.Count > 0)
        {
            _drawing = false;
            Finish(_last);
            InvalidateVisual();
            return;
        }

        _points.Clear();
        _drawing = false;
        ReleaseShapePreview();
        InvalidateVisual();
    }

    /// <summary>Runs the operations on the picture, shows the result and reports the edit.</summary>
    public void Run(params ImageOp[] ops)
    {
        if (Refuse()) return;
        var selection = State.Selection?.Copy();
        _before = selection; // Emit falls back to it, and a stale one from the last pointer press would journal a selection that is gone
        foreach (var op in ops) ImageOps.Run(State, op);
        _outline = null;
        Emit(ops, selection);
        SelectionChanged?.Invoke();
    }

    // Reports ops that are already applied (a live stroke), under the selection the stroke started with.
    private void Emit(IReadOnlyList<ImageOp> ops, PixelSelection? selection = null)
    {
        Refresh();
        Edited?.Invoke(new ImageEdit(selection ?? _before, ops, Picture.Copy(), ActiveLayer));
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        ClearHandleHover();
        Hovered?.Invoke(null);
        InvalidateVisual();
    }

    // Alt+Tab or another window taking the pointer mid-drag ends the drag without a resize, so a later click cannot commit it.
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        CancelResize();
        // Another window took the pointer mid-gesture: no button is down any more, so the stroke ends (a pencil stroke is kept) and the view stops following the mouse.
        _panFrom = null;
        if (_drawing) CancelPoints();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Space)
        {
            _space = true;
            UpdateCursor();
            e.Handled = true;
        }

        _modifiers = e.KeyModifiers;
        Handles.Modifiers(e.KeyModifiers);
        if (e.Key == Key.Escape && CancelResize())
        {
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter) CommitPoints();
        else if (e.Key == Key.Escape && (Collecting || _drawing)) CancelPoints();
        else if (e.Key is Key.Left or Key.Right or Key.Up or Key.Down && State.Selection is not null)
        {
            var step = e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? 8 : 1;
            Run(new ImageOp(ImageOps.Move, ImageOp.Format([(e.Key == Key.Left ? -step : e.Key == Key.Right ? step : 0, e.Key == Key.Up ? -step : e.Key == Key.Down ? step : 0)])));
        }
        else if (e.Key is Key.Left or Key.Right or Key.Up or Key.Down)
        {
            // No selection to nudge: the arrows scroll the view, so a keyboard user can reach the parts of a zoomed picture.
            var step = e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? 128 : 32;
            PanBy(new Vector(e.Key == Key.Left ? step : e.Key == Key.Right ? -step : 0, e.Key == Key.Up ? step : e.Key == Key.Down ? -step : 0));
        }
        else return;
        e.Handled = true;
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        if (e.Key == Key.Space)
        {
            _space = false;
            UpdateCursor();
        }

        _modifiers = e.KeyModifiers;
        Handles.Modifiers(e.KeyModifiers);
    }

    // The key-up of Space goes to whoever has the focus then, so a held Space must not stay on.
    private void ReleaseSpace() => HoldSpace(false);

    /// <summary>Space held or released for the hand grip; the editor calls this for a Space pressed while another control (a toolbar button) has the focus.</summary>
    internal void HoldSpace(bool held)
    {
        _space = held;
        UpdateCursor();
    }

    private void UpdateCursor() => Cursor = new Cursor(_space || Tool == ImageTool.Hand ? StandardCursorType.Hand : HandleSide is { } side ? SideCursor(side) : StandardCursorType.Cross);
}
