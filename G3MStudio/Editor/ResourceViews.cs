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
    public static void Draw(DrawingContext context, Rect area, double cell, IBrush? light = null, IBrush? dark = null)
    {
        var pair = 2 * cell;
        var tile = new DrawingGroup
        {
            Children =
            {
                new GeometryDrawing { Brush = light ?? G3MStudio.Theme.CanvasTheme.Bg, Geometry = new RectangleGeometry(new Rect(0, 0, pair, pair)) },
                new GeometryDrawing { Brush = dark ?? G3MStudio.Theme.CanvasTheme.Stage, Geometry = new GeometryGroup { Children = { new RectangleGeometry(new Rect(0, 0, cell, cell)), new RectangleGeometry(new Rect(cell, cell, cell, cell)) } } },
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

    public TextureItemView(GameMakerTexturePageItem? item, TextureBitmaps textures)
    {
        Name = "TextureItemView";
        Height = 300;
        RenderOptions.SetBitmapInterpolationMode(_image, BitmapInterpolationMode.None);
        _zoomBar.ValueChanged += (_, e) => Zoom = e.NewValue;
        var export = new Button { Name = "ExportItem", Content = Loc.Current["textureItem.export"] };
        export.Click += async (_, _) => await ExportAsync(item);
        var bar = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 8, Children = { _zoomBar, export, _caption } };
        SetDock(bar, Avalonia.Controls.Dock.Top);
        Children.Add(bar);
        foreach (var layer in new Control[] { _chequer, _image, _box, _mark }) _grid.Children.Add(layer);
        _scroll.Content = _grid;
        Children.Add(_scroll);
        _image.PointerPressed += (_, e) =>
        {
            if (_image.Source is null || !e.GetCurrentPoint(_image).Properties.IsLeftButtonPressed) return;
            var at = e.GetPosition(_image);
            Picked?.Invoke((int)(at.X / Zoom), (int)(at.Y / Zoom));
        };
        Ready = ShowAsync(item, textures);
    }

    public Task Ready { get; }

    /// <summary>A left click on the picture, as a pixel of the item.</summary>
    public event Action<int, int>? Picked;

    private (int X, int Y, int W, int H)? _marked;

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
