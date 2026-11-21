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
/// The drawing surface of the image editor. It keeps a transform of picture pixels to screen (a zoom and the place of the picture, with no limit from the picture
/// size: zoom to the cursor, pan with the hand tool, space or the middle button, the wheel scrolls),
/// uploads only the changed rectangle of the picture to its bitmap, and turns what the mouse does into <see cref="ImageOp"/>s that run through <see cref="ImageOps"/>,
/// the same code the journal replays.
/// </summary>
public sealed partial class ImageCanvas : Control, IDisposable
{
#pragma warning disable CA2213 // freed by DeferredDispose after the pending render passes
    private WriteableBitmap _bitmap;
#pragma warning restore CA2213
    private PixelBuffer _shown;
    private PixelBuffer _flat;
    private bool _space;
    private bool _stackFlat;
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
        UpdateCursor();
        LostFocus += (_, _) => ReleaseSpace();
        Upload(true);
    }

    public ImageState State { get; }

    /// <summary>The layers of the frame as they were read; the picture of <see cref="ActiveLayer"/> in it is replaced by <see cref="Picture"/>, which is being drawn on.</summary>
    public LayerSet Layers { get; private set; }

    public int ActiveLayer { get; private set; }

    /// <summary>True while the active layer is locked: tools that change pixels do nothing.</summary>
    public bool Locked { get; set; }

    /// <summary>True while the active layer is hidden: what is drawn on it would not show, so the tools that change pixels refuse like on a locked layer.</summary>
    public bool Hidden { get; private set; }

    /// <summary>Raised with a language key when a tool is refused because the active layer is locked or hidden, so the editor can say why.</summary>
    public event Action<string>? Refused;

    /// <summary>True (after telling why) when the active layer takes no pixel edits.</summary>
    public bool Refuse()
    {
        if (!Locked && !Hidden) return false;
        Refused?.Invoke(Locked ? "image.locked" : "image.layerHidden");
        return true;
    }

    /// <summary>Anti-aliased brush, line and shapes.</summary>
    public bool Smooth { get; set; }

    public string TextFont { get; set; } = "";

    public int HandleSize { get; set; } = 3;

    public IBrush? GridBrush { get; set { field = value; InvalidateVisual(); } }

    public IBrush? Backdrop { get; set { field = value; InvalidateVisual(); } }

    public IBrush? ChequerLight { get; set { field = value; InvalidateVisual(); } }

    public IBrush? ChequerDark { get; set { field = value; InvalidateVisual(); } }

    public PixelBuffer Picture => State.Picture;

    public ImageTool Tool
    {
        get;
        set
        {
            field = value;
            UpdateCursor();
        }
    }

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
        // The picture below is replaced, so a stroke or a canvas drag that is on belongs to the old one (an undo mid-drag): it ends without an edit.
        (_drawing, _brush) = (false, null);
        _points.Clear();
        ReleaseShapePreview();
        CancelResize();
        Layers = layers;
        ActiveLayer = Math.Clamp(active, 0, layers.Count - 1);
        (Locked, Hidden) = (layers.Layers[ActiveLayer].Locked, !layers.Layers[ActiveLayer].Visible);
        State.Picture = layers.Layers[ActiveLayer].Pixels.Copy();
        _stackFlat = true;
        Upload(true);
        InvalidateVisual();
    }

    /// <summary>Uploads what changed since the last call: the whole picture after a new size, else only the dirty rectangle. With layers, that rectangle is composited first.</summary>
    public void Upload(bool all = false)
    {
        if (_disposed) return;
        if (!ReferenceEquals(_shown, State.Picture))
        {
            var resized = _shown.Width != State.Picture.Width || _shown.Height != State.Picture.Height;
            _bitmap.Dispose();
            _shown = State.Picture;
            _bitmap = NewBitmap(_shown);
            all = true;
            if (resized && _fitted) Fit();
            else if (resized && Zoom < MinZoom) ZoomTo(MinZoom); // a panned view of a large picture, now showing a small one: it would stay a speck
        }

        var area = _shown.TakeDirty();
        if (!all && area is null) return;
        var (x, y, w, h) = all ? (0, 0, _shown.Width, _shown.Height) : area ?? default;
        var source = _shown;
        var stackFlat = _stackFlat;
        _stackFlat = false;
        if (stackFlat && all && !Layers.IsPlain && Layers.Width == _shown.Width && Layers.Height == _shown.Height) source = Layers.Flat; // the picture is the layer as read, so the stack's own flat picture (made once, kept with the stack) is what shows
        else if (!Layers.IsPlain && Layers.Width == _shown.Width && Layers.Height == _shown.Height)
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

    private bool _disposed;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true; // the compositor may still draw this control once after its tab closed: Render then skips the bitmaps
        ReleaseShapePreview();
        Rooms.DeferredDispose.Release(_onion.Values.Append(_bitmap));
    }

    public void SetSelection(PixelSelection? selection)
    {
        State.Selection = selection is { IsEmpty: true } ? null : selection;
        _outline = null;
        SelectionChanged?.Invoke();
        InvalidateVisual();
    }

    /// <summary>The highest zoom: one pixel of the picture is 256 screen pixels. Only the part in view is drawn, so it costs the same for any picture size.</summary>
    public const double MaxZoom = 256;

    private const double MaxFitZoom = 64;

    // Whole and half-way steps of two: the buttons, keys and the wheel move along them, so a zoom in lands on whole pixels where it can.
    private static readonly double[] Steps = [.. Enumerable.Range(-8, 17).SelectMany(k => new[] { Math.Pow(2, k), 1.5 * Math.Pow(2, k) }).Where(z => z <= MaxZoom).Order()];

    private bool _fitted = true;
    private double _wheel;

    /// <summary>The smallest zoom: the longest side of the picture is still about 16 screen pixels (never below 1/256, never above 1).</summary>
    public double MinZoom => Math.Clamp(16.0 / Math.Max(1, Math.Max(Picture.Width, Picture.Height)), 1.0 / 256, 1);

    /// <summary>Where the top left corner of the picture is on screen.</summary>
    public Point Origin { get; private set; } = new(16, 16);

    /// <summary>The zoom as the status line shows it: "8x" from whole size up, a percent below.</summary>
    public string ZoomText => Zoom >= 1 ? $"{Zoom.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}x" : $"{(Zoom * 100).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}%";

    private Size ViewSize => Bounds.Width < 8 || Bounds.Height < 8 ? new Size(600, 400) : Bounds.Size;

    private void SetZoom(double zoom)
    {
        Zoom = zoom;
        // Zoomed in, a pixel is a hard-edged square; zoomed out, neighbouring pixels are averaged so thin lines do not drop out.
        RenderOptions.SetBitmapInterpolationMode(this, zoom < 1 ? BitmapInterpolationMode.MediumQuality : BitmapInterpolationMode.None);
    }

    private void ViewMoved(bool fitted)
    {
        _fitted = fitted;
        ViewChanged?.Invoke();
        InvalidateVisual();
    }

    /// <summary>Puts the whole picture in view, centred with a margin, at the largest zoom that fits (whole steps from 1 up, any fraction below).</summary>
    public void Fit() => Fit(ViewSize);

    private void Fit(Size size)
    {
        var margin = Math.Min(16, Math.Min(size.Width, size.Height) / 8);
        var fit = Math.Min((size.Width - 2 * margin) / Picture.Width, (size.Height - 2 * margin) / Picture.Height);
        SetZoom(Math.Clamp(fit >= 1 ? Math.Floor(fit) : fit, 1.0 / 256, MaxFitZoom));
        Origin = new Point((size.Width - Picture.Width * Zoom) / 2, (size.Height - Picture.Height * Zoom) / 2);
        ViewMoved(true);
    }

    /// <summary>Sets the zoom, keeping the picture point under <paramref name="around"/> (the middle of the view by default) where it is.</summary>
    public void ZoomTo(double zoom, Point? around = null)
    {
        var anchor = around ?? new Point(Bounds.Width / 2, Bounds.Height / 2);
        var before = ToPicture(anchor);
        SetZoom(Math.Clamp(zoom, Math.Min(MinZoom, Zoom), Math.Max(MaxZoom, Zoom)));
        Origin = new Point(anchor.X - before.X * Zoom, anchor.Y - before.Y * Zoom);
        ViewMoved(false);
    }

    public void ZoomBy(double factor, Point? around = null) => ZoomTo(Zoom * factor, around);

    /// <summary>One step in (positive) or out (negative) along the zoom steps.</summary>
    public void Step(int direction, Point? around = null) =>
        ZoomTo(direction > 0 ? Steps.FirstOrDefault(s => s > Zoom * 1.001, MaxZoom) : Steps.LastOrDefault(s => s < Zoom / 1.001, MinZoom), around);

    public void ActualSize()
    {
        SetZoom(1);
        Origin = new Point((ViewSize.Width - Picture.Width) / 2, (ViewSize.Height - Picture.Height) / 2);
        ViewMoved(false);
    }

    /// <summary>Moves the view over the picture by a screen distance; the picture itself is untouched.</summary>
    public void PanBy(Vector delta)
    {
        Origin += delta;
        ViewMoved(false);
    }

    /// <summary>The point of the picture (in pixels, fractions included) under a screen point of the canvas.</summary>
    public Point ToPicture(Point screen) => new((screen.X - Origin.X) / Zoom, (screen.Y - Origin.Y) / Zoom);

    /// <summary>The pixel under a screen point of the canvas; it can lie outside the picture.</summary>
    public (int X, int Y) Cell(Point screen)
    {
        var p = ToPicture(screen);
        return ((int)Math.Floor(p.X), (int)Math.Floor(p.Y));
    }

    private Point ToScreen(double x, double y) => new(Origin.X + x * Zoom, Origin.Y + y * Zoom);

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        if (_fitted || e.PreviousSize.Width < 8) Fit(e.NewSize.Width < 8 || e.NewSize.Height < 8 ? ViewSize : e.NewSize);
        else
        {
            // The middle of the view stays on the same picture point when the panels around the canvas are dragged.
            Origin += new Vector((e.NewSize.Width - e.PreviousSize.Width) / 2, (e.NewSize.Height - e.PreviousSize.Height) / 2);
            InvalidateVisual();
        }
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        e.Handled = true;
        var at = e.GetPosition(this);
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            // Whole notches step the zoom; the small deltas of a touch pad add up to one.
            _wheel += e.Delta.Y;
            for (; _wheel >= 1; _wheel--) Step(1, at);
            for (; _wheel <= -1; _wheel++) Step(-1, at);
        }
        else PanBy(e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? new Vector(e.Delta.Y * 48, 0) : new Vector(e.Delta.X * 48, e.Delta.Y * 48));

        _hoverCell = Cell(at);
        Hovered?.Invoke(_hoverCell);
    }
}
