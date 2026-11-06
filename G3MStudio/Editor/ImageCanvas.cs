using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Editor;

/// <summary>An edit the canvas made: the operations (already applied to the picture), the selection they ran under, and the picture they left.</summary>
public sealed record ImageEdit(PixelSelection? Selection, IReadOnlyList<ImageOp> Ops, PixelBuffer After);

/// <summary>
/// The drawing surface of the image editor. It keeps a transform of picture pixels to screen (zoom to the cursor, pan with space or the middle button),
/// uploads only the changed rectangle of the picture to its bitmap, and turns what the mouse does into <see cref="ImageOp"/>s that run through <see cref="ImageOps"/>,
/// the same code the journal replays.
/// </summary>
public sealed partial class ImageCanvas : Control, IDisposable
{
    private WriteableBitmap _bitmap;
    private PixelBuffer _shown;
    private Point _origin = new(16, 16);
    private bool _space;
    private readonly Dictionary<PixelBuffer, WriteableBitmap> _onion = [];

    public ImageCanvas(ImageState state)
    {
        State = state;
        _shown = state.Picture;
        _bitmap = NewBitmap(state.Picture);
        Focusable = true;
        ClipToBounds = true;
        RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.None);
        Cursor = new Cursor(StandardCursorType.Cross);
        Upload(true);
    }

    public ImageState State { get; }

    public PixelBuffer Picture => State.Picture;

    public ImageTool Tool { get; set; }

    public Rgba Primary { get; set; } = new(0, 0, 0, 255);

    public Rgba Secondary { get; set; } = new(255, 255, 255, 255);

    public int BrushSize { get; set; } = 1;

    public BrushShape Shape { get; set; } = BrushShape.Square;

    public ShapeStyle Style { get; set; } = ShapeStyle.Outline;

    public int Tolerance { get; set; }

    public bool Contiguous { get; set; } = true;

    public SymmetryMode Symmetry { get; set { field = value; InvalidateVisual(); } }

    public bool ShowGrid { get; set { field = value; InvalidateVisual(); } } = true;

    public int GridCell { get; set { field = Math.Max(0, value); InvalidateVisual(); } } = 16;

    public bool TilePreview { get; set { field = value; InvalidateVisual(); } }

    /// <summary>Pictures of the frames before and after, drawn faint under the picture.</summary>
    public IReadOnlyList<PixelBuffer> Onion
    {
        get;
        set
        {
            field = value;
            foreach (var bitmap in _onion.Values) bitmap.Dispose();
            _onion.Clear();
            InvalidateVisual();
        }
    } = [];

    public double OnionOpacity { get; set; } = 0.35;

    public double Zoom { get; private set; } = 8;

    public event Action<ImageEdit>? Edited;

    public event Action<Rgba, bool>? Picked;

    public event Action<(int X, int Y)?>? Hovered;

    public event Action? ViewChanged;

    public event Action? SelectionChanged;

    private static WriteableBitmap NewBitmap(PixelBuffer picture) => new(new PixelSize(picture.Width, picture.Height), new Vector(96, 96), PixelFormat.Rgba8888, AlphaFormat.Unpremul);

    /// <summary>Uploads what changed since the last call: the whole picture after a new size, else only the dirty rectangle.</summary>
    public void Upload(bool all = false)
    {
        if (!ReferenceEquals(_shown, State.Picture))
        {
            _bitmap.Dispose();
            _shown = State.Picture;
            _bitmap = NewBitmap(_shown);
            all = true;
        }

        var area = _shown.TakeDirty();
        if (!all && area is null) return;
        var (x, y, w, h) = all ? (0, 0, _shown.Width, _shown.Height) : area ?? default;
        using var frame = _bitmap.Lock();
        for (var row = y; row < y + h; row++) Marshal.Copy(_shown.Data, (row * _shown.Width + x) * 4, frame.Address + row * frame.RowBytes + x * 4, w * 4);
    }

    /// <summary>Shows the changes made to the picture: uploads the dirty part, then redraws.</summary>
    public void Refresh()
    {
        Upload();
        InvalidateVisual();
    }

    public void Dispose()
    {
        _bitmap.Dispose();
        foreach (var bitmap in _onion.Values) bitmap.Dispose();
    }

    public void SetSelection(PixelSelection? selection)
    {
        State.Selection = selection is { IsEmpty: true } ? null : selection;
        _outline = null;
        SelectionChanged?.Invoke();
        InvalidateVisual();
    }

    /// <summary>Puts the whole picture in view at the largest zoom that fits (at least 1).</summary>
    public void Fit()
    {
        var size = Bounds.Size;
        if (size.Width < 8) size = new Size(600, 400);
        Zoom = Math.Clamp(Math.Floor(Math.Min((size.Width - 32) / Picture.Width, (size.Height - 32) / Picture.Height)), 1, 64);
        _origin = new Point((size.Width - Picture.Width * Zoom) / 2, (size.Height - Picture.Height * Zoom) / 2);
        ViewChanged?.Invoke();
        InvalidateVisual();
    }

    public void ZoomBy(double factor, Point? around = null)
    {
        var anchor = around ?? new Point(Bounds.Width / 2, Bounds.Height / 2);
        var before = ToPicture(anchor);
        Zoom = Math.Clamp(factor > 1 ? Math.Ceiling(Zoom * factor) : Math.Floor(Zoom * factor), 1, 64);
        // Keep the pixel under the cursor where it was.
        _origin = new Point(anchor.X - before.X * Zoom, anchor.Y - before.Y * Zoom);
        ViewChanged?.Invoke();
        InvalidateVisual();
    }

    public void ActualSize()
    {
        Zoom = 1;
        _origin = new Point((Bounds.Width - Picture.Width) / 2, (Bounds.Height - Picture.Height) / 2);
        ViewChanged?.Invoke();
        InvalidateVisual();
    }

    private Point ToPicture(Point screen) => new((screen.X - _origin.X) / Zoom, (screen.Y - _origin.Y) / Zoom);

    private (int X, int Y) Cell(Point screen)
    {
        var p = ToPicture(screen);
        return ((int)Math.Floor(p.X), (int)Math.Floor(p.Y));
    }

    private Point ToScreen(double x, double y) => new(_origin.X + x * Zoom, _origin.Y + y * Zoom);

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        if (e.PreviousSize.Width < 8 && e.NewSize.Width >= 8) Fit();
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        ZoomBy(e.Delta.Y > 0 ? 1.25 : 0.8, e.GetPosition(this));
        e.Handled = true;
    }
}
