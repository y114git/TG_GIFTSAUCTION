using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Editor;

public enum PaintTool { Pencil, Eraser, Fill, Pick, Line, Rectangle, FilledRectangle }

/// <summary>The picture of the sprite painter: draws the pixels large, takes the mouse for the tools, and keeps undo and redo of whole strokes.</summary>
public sealed class PixelCanvas : Control, IDisposable
{
    private WriteableBitmap _bitmap;
    private readonly Stack<byte[]> _undo = [];
    private readonly Stack<byte[]> _redo = [];
    private byte[]? _before;
    private (int X, int Y)? _start;
    private (int X, int Y) _last;
    private (int X, int Y)? _hover;

    public PixelCanvas(PixelBuffer picture)
    {
        Picture = picture;
        _bitmap = new WriteableBitmap(new PixelSize(picture.Width, picture.Height), new Vector(96, 96), PixelFormat.Rgba8888, AlphaFormat.Unpremul);
        RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.None);
        Cursor = new Cursor(StandardCursorType.Cross);
        Upload();
        Measure();
    }

    public PixelBuffer Picture { get; private set; }

    public PaintTool Tool { get; set; }

    public Rgba Color { get; set; } = new(255, 255, 255, 255);

    public int BrushSize { get; set; } = 1;

    public bool ShowGrid
    {
        get;
        set
        {
            field = value;
            InvalidateVisual();
        }
    } = true;

    public double Zoom
    {
        get;
        set
        {
            field = Math.Clamp(Math.Round(value), 1, 64);
            Measure();
            InvalidateVisual();
        }
    } = 8;

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    /// <summary>The picture or its undo state changed.</summary>
    public event Action? Changed;

    /// <summary>The picker took a color from the picture.</summary>
    public event Action<Rgba>? Picked;

    private void Measure()
    {
        Width = Picture.Width * Zoom;
        Height = Picture.Height * Zoom;
    }

    private void Upload()
    {
        using var frame = _bitmap.Lock();
        System.Runtime.InteropServices.Marshal.Copy(Picture.Data, 0, frame.Address, Picture.Data.Length);
    }

    private (int X, int Y) Cell(PointerEventArgs e)
    {
        var p = e.GetPosition(this);
        return ((int)Math.Floor(p.X / Zoom), (int)Math.Floor(p.Y / Zoom));
    }

    /// <summary>Puts another picture on the canvas (a resize); undo and redo start again from it.</summary>
    public void Replace(PixelBuffer next)
    {
        var old = _bitmap;
        Picture = next;
        _bitmap = new WriteableBitmap(new PixelSize(next.Width, next.Height), new Vector(96, 96), PixelFormat.Rgba8888, AlphaFormat.Unpremul);
        old.Dispose();
        _undo.Clear();
        _redo.Clear();
        Upload();
        Measure();
        InvalidateVisual();
        Changed?.Invoke();
    }

    public void Dispose() => _bitmap.Dispose();

    public void Undo()
    {
        if (_undo.Count == 0) return;
        _redo.Push(Picture.Snapshot());
        Picture.Restore(_undo.Pop());
        Refresh();
    }

    public void Redo()
    {
        if (_redo.Count == 0) return;
        _undo.Push(Picture.Snapshot());
        Picture.Restore(_redo.Pop());
        Refresh();
    }

    private void Refresh()
    {
        Upload();
        InvalidateVisual();
        Changed?.Invoke();
    }

    /// <summary>Keeps the picture from before the stroke as one undo step, but only when the stroke changed something.</summary>
    private void Commit()
    {
        if (_before is null) return;
        if (!_before.AsSpan().SequenceEqual(Picture.Data))
        {
            _undo.Push(_before);
            _redo.Clear();
        }

        _before = null;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        var cell = Cell(e);
        if (Tool == PaintTool.Pick)
        {
            if (Picture.Contains(cell.X, cell.Y)) Picked?.Invoke(Picture.Get(cell.X, cell.Y));
            return;
        }

        e.Pointer.Capture(this);
        _before = Picture.Snapshot();
        (_start, _last) = (cell, cell);
        switch (Tool)
        {
            case PaintTool.Pencil or PaintTool.Eraser:
                Picture.Stamp(cell.X, cell.Y, BrushSize, Paint());
                break;
            case PaintTool.Fill:
                Picture.Fill(cell.X, cell.Y, Color);
                _start = null;
                Commit();
                break;
        }

        Refresh();
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var cell = Cell(e);
        _hover = cell;
        if (_start is not null && Tool is PaintTool.Pencil or PaintTool.Eraser && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            Picture.Line(_last.X, _last.Y, cell.X, cell.Y, BrushSize, Paint());
            _last = cell;
            Upload();
        }

        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_start is not { } start) return;
        var (x, y) = Cell(e);
        if (Tool == PaintTool.Line) Picture.Line(start.X, start.Y, x, y, BrushSize, Color);
        else if (Tool is PaintTool.Rectangle or PaintTool.FilledRectangle) Picture.Rectangle(start.X, start.Y, x, y, Color, Tool == PaintTool.FilledRectangle);
        _start = null;
        Commit();
        e.Pointer.Capture(null);
        Refresh();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _hover = null;
        InvalidateVisual();
    }

    private Rgba Paint() => Tool == PaintTool.Eraser ? Rgba.Clear : Color;

    public override void Render(DrawingContext context)
    {
        var area = new Rect(0, 0, Picture.Width * Zoom, Picture.Height * Zoom);
        // A checkerboard in two theme colors shows what is transparent.
        var cell = Math.Max(Zoom, 8);
        Chequer.Draw(context, area, cell);

        context.DrawImage(_bitmap, new Rect(0, 0, Picture.Width, Picture.Height), area);
        if (ShowGrid && Zoom >= 6)
        {
            var pen = new Pen(G3MStudio.Theme.CanvasTheme.Soft(G3MStudio.Theme.CanvasTheme.Guide, 0.25), 1);
            for (var x = 1; x < Picture.Width; x++) context.DrawLine(pen, new Point(x * Zoom, 0), new Point(x * Zoom, area.Height));
            for (var y = 1; y < Picture.Height; y++) context.DrawLine(pen, new Point(0, y * Zoom), new Point(area.Width, y * Zoom));
        }

        // The shape being dragged, outlined until the button is released.
        if (_start is { } from && _hover is { } to && Tool is PaintTool.Line or PaintTool.Rectangle or PaintTool.FilledRectangle)
        {
            var outline = new Pen(G3MStudio.Theme.CanvasTheme.Point, 2);
            context.DrawRectangle(null, outline, new Rect(Math.Min(from.X, to.X) * Zoom, Math.Min(from.Y, to.Y) * Zoom, (Math.Abs(to.X - from.X) + 1) * Zoom, (Math.Abs(to.Y - from.Y) + 1) * Zoom));
        }
        else if (_hover is { } at && Picture.Contains(at.X, at.Y))
        {
            context.DrawRectangle(null, new Pen(G3MStudio.Theme.CanvasTheme.Line, 1), new Rect(at.X * Zoom, at.Y * Zoom, Zoom, Zoom));
        }
    }
}
