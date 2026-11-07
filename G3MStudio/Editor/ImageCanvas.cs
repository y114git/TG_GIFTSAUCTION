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
public sealed record ImageEdit(PixelSelection? Selection, IReadOnlyList<ImageOp> Ops, PixelBuffer After, int Layer = -1);

/// <summary>
/// The drawing surface of the image editor. It keeps a transform of picture pixels to screen (zoom to the cursor, pan with space or the middle button),
/// uploads only the changed rectangle of the picture to its bitmap, and turns what the mouse does into <see cref="ImageOp"/>s that run through <see cref="ImageOps"/>,
/// the same code the journal replays.
/// </summary>
public sealed partial class ImageCanvas : Control, IDisposable
{
    private WriteableBitmap _bitmap;
    private PixelBuffer _shown;
    private PixelBuffer _flat;
    private Point _origin = new(16, 16);
    private bool _space;
    private readonly Dictionary<PixelBuffer, WriteableBitmap> _onion = [];

    public ImageCanvas(ImageState state)
    {
        State = state;
        _shown = state.Picture;
        _flat = new PixelBuffer(1, 1);
        Layers = LayerSet.OneLayer(state.Picture);
        _bitmap = NewBitmap(state.Picture);
        Focusable = true;
        ClipToBounds = true;
        RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.None);
        Cursor = new Cursor(StandardCursorType.Cross);
        Upload(true);
    }

    public ImageState State { get; }

    /// <summary>The layers of the frame as they were read; the picture of <see cref="ActiveLayer"/> in it is replaced by <see cref="Picture"/>, which is being drawn on.</summary>
    public LayerSet Layers { get; private set; }

    public int ActiveLayer { get; private set; }

    /// <summary>True while the active layer is locked: tools that change pixels do nothing.</summary>
    public bool Locked { get; set; }

    /// <summary>Anti-aliased brush, line and shapes.</summary>
    public bool Smooth { get; set; }

    public string TextFont { get; set; } = "";

    public int HandleSize { get; set; } = 3;

    public IBrush? GridBrush { get; set { field = value; InvalidateVisual(); } }

    public IBrush? Backdrop { get; set { field = value; InvalidateVisual(); } }

    public IBrush? ChequerLight { get; set { field = value; InvalidateVisual(); } }

    public IBrush? ChequerDark { get; set { field = value; InvalidateVisual(); } }

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

    /// <summary>Shows a frame's layers, drawing on the given one. The picture to draw on is a copy of that layer.</summary>
    public void SetLayers(LayerSet layers, int active)
    {
        Layers = layers;
        ActiveLayer = Math.Clamp(active, 0, layers.Count - 1);
        Locked = layers.Layers[ActiveLayer].Locked;
        State.Picture = layers.Layers[ActiveLayer].Pixels.Copy();
        Upload(true);
        InvalidateVisual();
    }

    /// <summary>Uploads what changed since the last call: the whole picture after a new size, else only the dirty rectangle. With layers, that rectangle is composited first.</summary>
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
        var source = _shown;
        if (!Layers.IsPlain && Layers.Width == _shown.Width && Layers.Height == _shown.Height)
        {
            if (_flat.Width != _shown.Width || _flat.Height != _shown.Height) (_flat, x, y, w, h) = (new PixelBuffer(_shown.Width, _shown.Height), 0, 0, _shown.Width, _shown.Height);
            Layers.Composite(_flat, new Box(x, y, w, h), ActiveLayer, _shown);
            _flat.TakeDirty();
            source = _flat;
        }

        using var frame = _bitmap.Lock();
        for (var row = y; row < y + h; row++) Marshal.Copy(source.Data, (row * source.Width + x) * 4, frame.Address + row * frame.RowBytes + x * 4, w * 4);
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
