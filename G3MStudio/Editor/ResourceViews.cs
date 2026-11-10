using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using G3MLib.DataFile.Models;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;
using G3MStudio.Rooms;

namespace G3MStudio.Editor;

/// <summary>The two-tone grid drawn behind pictures with transparent parts. One tiled brush fills the area, so the cost does not grow with its size.</summary>
public sealed class Chequer : Control
{
    /// <summary>The colours from Settings > Image (UTMT-SET-12); null keeps the theme colours. Used wherever a call gives none.</summary>
    public static IBrush? Light { get; set; }

    public static IBrush? Dark { get; set; }

    public static void Draw(DrawingContext context, Rect area, double cell, IBrush? light = null, IBrush? dark = null)
    {
        var pair = 2 * cell;
        var tile = new DrawingGroup
        {
            Children =
            {
                new GeometryDrawing { Brush = light ?? Light ?? G3MStudio.Theme.CanvasTheme.Bg, Geometry = new RectangleGeometry(new Rect(0, 0, pair, pair)) },
                new GeometryDrawing { Brush = dark ?? Dark ?? G3MStudio.Theme.CanvasTheme.Stage, Geometry = new GeometryGroup { Children = { new RectangleGeometry(new Rect(0, 0, cell, cell)), new RectangleGeometry(new Rect(cell, cell, cell, cell)) } } },
            },
        };
        context.FillRectangle(new DrawingBrush(tile) { TileMode = TileMode.Tile, DestinationRect = new RelativeRect(0, 0, pair, pair, RelativeUnit.Absolute) }, area);
    }

    public override void Render(DrawingContext context) => Draw(context, new Rect(Bounds.Size), 8);
}

/// <summary>
/// A texture page item (a tile set, a font sheet, an embedded image or a page item) on a chequer that shows what is transparent, with a zoom of 1x to 8x; it opens at the largest whole zoom that fits.
/// When the item sits inside a larger bounding box, the box is outlined and the picture is placed where the game draws it (UTMT-CTRL-03).
/// </summary>
public sealed class TextureItemView : DockPanel
{
    private readonly Image _image = new() { Stretch = Stretch.None, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top };
    private readonly Chequer _chequer = new() { HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top };
    private readonly Avalonia.Controls.Shapes.Rectangle _box = new() { Stroke = G3MStudio.Theme.CanvasTheme.Point, StrokeThickness = 2, IsHitTestVisible = false, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top };
    private (int X, int Y, int W, int H) _frame;
    private readonly Grid _grid = new();
    private readonly List<Control> _overlays = [];
    private readonly ScrollViewer _scroll = new() { HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, Background = G3MStudio.Theme.CanvasTheme.Bg };
    private readonly TextBlock _caption = new() { Classes = { "muted" }, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
    private readonly Avalonia.Controls.Shapes.Rectangle _mark = new() { Stroke = G3MStudio.Theme.CanvasTheme.Hover, StrokeThickness = 2, IsVisible = false, IsHitTestVisible = false, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top };
    private readonly Slider _zoomBar = new() { Minimum = 1, Maximum = 8, Value = 1, Width = 160, TickFrequency = 1, IsSnapToTickEnabled = true };

    // replace: asks the owner to replace the picture of the item (a picker and one undoable edit); true when it did. Null shows no Replace button.
    public TextureItemView(GameMakerTexturePageItem? item, TextureBitmaps textures, Func<Task<bool>>? replace = null)
    {
        Name = "TextureItemView";
        Height = 300;
        RenderOptions.SetBitmapInterpolationMode(_image, BitmapInterpolationMode.None);
        _zoomBar.ValueChanged += (_, e) => Zoom = e.NewValue;
        var export = new Button { Name = "ExportItem", Content = Loc.Current["textureItem.export"] };
        export.Click += async (_, _) => await ExportAsync(item);
        var bar = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 8, Children = { _zoomBar, export, _caption } };
        if (replace is not null)
        {
            var swap = new Button { Name = "ReplaceItem", Content = Loc.Current["textureItem.replace"] };
            swap.Click += async (_, _) =>
            {
                if (!await replace() || item?.TexturePage is not { } page) return;
                textures.Forget(page); // the page picture changed: decode it again
                await ShowAsync(item, textures);
            };
            bar.Children.Insert(2, swap);
        }

        SetDock(bar, Avalonia.Controls.Dock.Top);
        Children.Add(bar);
        foreach (var layer in new Control[] { _chequer, _image, _box, _mark }) _grid.Children.Add(layer);
        _scroll.Content = _grid;
        Children.Add(_scroll);
        _image.PointerPressed += (_, e) =>
        {
            if (_image.Source is null || !e.GetCurrentPoint(_image).Properties.IsLeftButtonPressed) return;
            var at = e.GetPosition(_image);
            if (BeginMarkDrag((int)(at.X / Zoom), (int)(at.Y / Zoom), at))
            {
                e.Pointer.Capture(_image);
                _scroll.Focus();
                return;
            }

            Picked?.Invoke((int)(at.X / Zoom), (int)(at.Y / Zoom));
        };
        _image.PointerMoved += (_, e) => DragMark(e.GetPosition(_image));
        _image.PointerReleased += (_, e) =>
        {
            if (_drag is null) return;
            EndMarkDrag(); // before the release of the capture, which would cancel the drag
            e.Pointer.Capture(null);
        };
        _image.PointerCaptureLost += (_, _) => CancelMarkDrag();
        _scroll.Focusable = true;
        _scroll.KeyDown += (_, e) => e.Handled = NudgeMark(e.Key, e.KeyModifiers.HasFlag(Avalonia.Input.KeyModifiers.Shift));
        Ready = ShowAsync(item, textures);
    }

    public Task Ready { get; }

    /// <summary>A left click on the picture, as a pixel of the item.</summary>
    public event Action<int, int>? Picked;

    private (int X, int Y, int W, int H)? _marked;
    private ((int X, int Y, int W, int H) Start, Point From, bool Resize)? _drag;

    /// <summary>When true the outlined part can be dragged (its bottom right corner resizes it) and nudged with the arrow keys (Shift resizes); each gesture raises <see cref="MarkEdited"/> once.</summary>
    public bool MarkEditable { get; set; }

    /// <summary>The outlined part was moved or resized to this rectangle (pixels of the item).</summary>
    public event Action<(int X, int Y, int W, int H)>? MarkEdited;

    private bool BeginMarkDrag(int x, int y, Point at)
    {
        if (!MarkEditable || _marked is not { } m || x < m.X || y < m.Y || x >= m.X + m.W || y >= m.Y + m.H) return false;
        _drag = (m, at, x >= m.X + m.W - Math.Max(1, 6 / (int)Zoom) && y >= m.Y + m.H - Math.Max(1, 6 / (int)Zoom));
        return true;
    }

    private void DragMark(Point at)
    {
        if (_drag is not { } d) return;
        int dx = (int)Math.Round((at.X - d.From.X) / Zoom), dy = (int)Math.Round((at.Y - d.From.Y) / Zoom);
        _marked = ClampMark(d.Resize ? (d.Start.X, d.Start.Y, d.Start.W + dx, d.Start.H + dy) : (d.Start.X + dx, d.Start.Y + dy, d.Start.W, d.Start.H));
        PlaceMark();
    }

    // Glyph and item rectangles are stored as ushort: the position stays 0..65535 and the size 1..65535.
    internal static (int X, int Y, int W, int H) ClampMark((int X, int Y, int W, int H) r) =>
        (Math.Clamp(r.X, 0, ushort.MaxValue), Math.Clamp(r.Y, 0, ushort.MaxValue), Math.Clamp(r.W, 1, ushort.MaxValue), Math.Clamp(r.H, 1, ushort.MaxValue));

    private void CancelMarkDrag()
    {
        if (_drag is not { } d) return;
        _drag = null;
        _marked = d.Start;
        PlaceMark();
    }

    private void EndMarkDrag()
    {
        if (_drag is not { } d) return;
        _drag = null;
        if (_marked is { } now && now != d.Start) MarkEdited?.Invoke(now);
    }

    /// <summary>Moves the outlined part one pixel (or resizes it with <paramref name="resize"/>) for an arrow key; false when the key is not one or nothing is editable.</summary>
    public bool NudgeMark(Avalonia.Input.Key key, bool resize)
    {
        if (!MarkEditable || _marked is not { } m) return false;
        var (dx, dy) = key switch { Avalonia.Input.Key.Left => (-1, 0), Avalonia.Input.Key.Right => (1, 0), Avalonia.Input.Key.Up => (0, -1), Avalonia.Input.Key.Down => (0, 1), _ => (0, 0) };
        if (dx == 0 && dy == 0) return false;
        var next = ClampMark(resize ? (m.X, m.Y, m.W + dx, m.H + dy) : (m.X + dx, m.Y + dy, m.W, m.H));
        _marked = next;
        PlaceMark();
        MarkEdited?.Invoke(next);
        return true;
    }

    /// <summary>Outlines a part of the picture (pixels of the item), or nothing for null.</summary>
    public void Highlight((int X, int Y, int W, int H)? area)
    {
        _marked = area;
        PlaceMark();
        if (area is not { } a) return;
        // Bring the outlined part to the middle of the view once the layout has the new mark.
        Avalonia.Threading.Dispatcher.UIThread.Post(() => _scroll.Offset = new Vector(
            Math.Max(0, (a.X + a.W / 2.0) * Zoom - _scroll.Viewport.Width / 2),
            Math.Max(0, (a.Y + a.H / 2.0) * Zoom - _scroll.Viewport.Height / 2)));
    }

    private void PlaceMark()
    {
        _mark.IsVisible = _marked is not null;
        if (_marked is not { } a) return;
        _mark.Margin = new Thickness((a.X + _frame.X) * Zoom, (a.Y + _frame.Y) * Zoom, 0, 0);
        _mark.Width = a.W * Zoom;
        _mark.Height = a.H * Zoom;
    }

    /// <summary>Asks for a file and writes the item as a PNG (UTMT-TEX-02); the caption says where it went or why not.</summary>
    private async Task ExportAsync(GameMakerTexturePageItem? item)
    {
        if (item is null || TopLevel.GetTopLevel(this) is not { } top) return;
        try
        {
            var target = await top.StorageProvider.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions { Title = Loc.Current["textureItem.export"], DefaultExtension = "png", SuggestedFileName = "texture.png" });
            if (target?.TryGetLocalPath() is not { } path) return;
            var png = await Task.Run(() => FramePicture.ItemToPng(item, "texture"));
            await File.WriteAllBytesAsync(path, png);
            _caption.Text = Loc.Current.Format("textureItem.saved", ("path", path));
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            _caption.Text = Loc.Current.Format("textureItem.failed", ("reason", e.Message));
        }
    }

    /// <summary>Puts a control over the picture; it is sized and placed like the picture at every zoom, and draws in picture pixels times <see cref="Zoom"/>.</summary>
    public void AddOverlay(Control overlay)
    {
        _overlays.Add(overlay);
        overlay.IsHitTestVisible = false;
        overlay.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left;
        overlay.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top;
        _grid.Children.Add(overlay);
        ApplyZoom();
    }

    public (int Width, int Height)? PictureSize => _image.Source is { } source ? ((int)source.Size.Width, (int)source.Size.Height) : null;

    public double Zoom
    {
        get;
        set
        {
            field = Math.Clamp(value, 1, 8);
            ApplyZoom();
        }
    } = 1;

    private void ApplyZoom()
    {
        if (_image.Source is not { } source) return;
        _image.Width = source.Size.Width * Zoom;
        _image.Height = source.Size.Height * Zoom;
        _image.Stretch = Stretch.Fill;
        _image.Margin = new Thickness(_frame.X * Zoom, _frame.Y * Zoom, 0, 0);
        foreach (var overlay in _overlays)
        {
            (overlay.Margin, overlay.Width, overlay.Height) = (_image.Margin, _image.Width, _image.Height);
            overlay.InvalidateVisual();
        }
        _chequer.Width = _box.Width = Math.Min(_frame.W * Zoom, MaxBox);
        _chequer.Height = _box.Height = Math.Min(_frame.H * Zoom, MaxBox);
        PlaceMark();
    }

    /// <summary>The largest chequer and box in pixels per side; a file can claim a box of 65535 and the picture itself stays scrollable.</summary>
    public const double MaxBox = 8192;

    /// <summary>Where the picture sits inside its bounding box, and the box itself; a box smaller than the picture grows to hold it.</summary>
    public static (int X, int Y, int W, int H) FrameOf(GameMakerTexturePageItem item) =>
        (item.TargetX, item.TargetY, Math.Max(item.BoundingWidth, item.TargetX + item.SourceWidth), Math.Max(item.BoundingHeight, item.TargetY + item.SourceHeight));

    private async Task ShowAsync(GameMakerTexturePageItem? item, TextureBitmaps textures)
    {
        if (item is not { TexturePage: { } page, SourceWidth: > 0, SourceHeight: > 0 } || await textures.GetAsync(page) is not { } bitmap)
        {
            // No picture (a tile set or font without a texture): the zoom bar, the export button and the empty frame would only take room.
            _caption.Text = "-";
            IsVisible = false;
            return;
        }

        _frame = FrameOf(item);
        _image.Source = new CroppedBitmap(bitmap, new PixelRect(item.SourceX, item.SourceY, item.SourceWidth, item.SourceHeight));
        _caption.Text = $"{item.SourceWidth} x {item.SourceHeight}";
        // Start at the largest whole scale that still fits the view, so a small sheet is readable without touching the slider.
        _zoomBar.Value = Math.Clamp(Math.Floor(260.0 / Math.Max(item.SourceWidth, item.SourceHeight)), 1, 8);
        ApplyZoom();
    }
}
