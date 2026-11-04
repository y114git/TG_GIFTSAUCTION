using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using G3MLib.DataFile;
using G3MLib.DataFile.Models;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;
using G3MStudio.Rooms;

namespace G3MStudio.Editor;

/// <summary>The two-tone grid drawn behind pictures with transparent parts. One tiled brush fills the area, so the cost does not grow with its size.</summary>
public sealed class Chequer : Control
{
    public static void Draw(DrawingContext context, Rect area, double cell)
    {
        var pair = 2 * cell;
        var tile = new DrawingGroup
        {
            Children =
            {
                new GeometryDrawing { Brush = G3MStudio.Theme.CanvasTheme.Bg, Geometry = new RectangleGeometry(new Rect(0, 0, pair, pair)) },
                new GeometryDrawing { Brush = G3MStudio.Theme.CanvasTheme.Stage, Geometry = new GeometryGroup { Children = { new RectangleGeometry(new Rect(0, 0, cell, cell)), new RectangleGeometry(new Rect(cell, cell, cell, cell)) } } },
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
    private readonly ScrollViewer _scroll = new() { HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, Background = G3MStudio.Theme.CanvasTheme.Bg };
    private readonly TextBlock _caption = new() { Classes = { "muted" }, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
    private readonly Avalonia.Controls.Shapes.Rectangle _mark = new() { Stroke = Brushes.Yellow, StrokeThickness = 2, IsVisible = false, IsHitTestVisible = false, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top };
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
        _scroll.Content = new Grid { Children = { _chequer, _image, _box, _mark } };
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

/// <summary>
/// A path drawn as a line through its points (a smooth path as a curve through them, a closed one joined back to the start), with the points marked.
/// With a workspace: a click on empty space adds a point at the end, a drag moves a point and Delete removes the selected point, each as one undo step.
/// </summary>
public sealed class PathView : Control
{
    private readonly GameMakerPath _path;
    private readonly Workspace? _workspace;
    private (Rect Box, double Scale)? _frame;
    private Point? _dragTo;

    public PathView(GameMakerPath path, Workspace? workspace = null)
    {
        (_path, _workspace) = (path, workspace);
        Name = "PathView";
        Height = 320;
        Focusable = workspace is not null;
        if (workspace is null) return;
        void Refresh() => InvalidateVisual();
        AttachedToVisualTree += (_, _) => workspace.History.Changed += Refresh;
        DetachedFromVisualTree += (_, _) => workspace.History.Changed -= Refresh;
    }

    /// <summary>The picked point, or -1.</summary>
    public int Selected { get; set { field = value; SelectionChanged?.Invoke(); InvalidateVisual(); } } = -1;

    public event Action? SelectionChanged;

    private string PathName => _path.Name.Content ?? "";

    private (Rect Box, double Scale) Frame()
    {
        if (_frame is { } kept) return kept;
        // Min and max by hand: Rect.Union ignores a rectangle without size, which every single point is.
        var box = new Rect(0, 0, 0, 0);
        if (_path.Points.Count > 0)
        {
            var (left, right) = (_path.Points.Min(p => p.X), _path.Points.Max(p => p.X));
            var (top, bottom) = (_path.Points.Min(p => p.Y), _path.Points.Max(p => p.Y));
            box = new Rect(left, top, right - left, bottom - top);
        }

        var scale = Math.Min((Bounds.Width - 24) / Math.Max(1, box.Width), (Bounds.Height - 24) / Math.Max(1, box.Height));
        return (box, double.IsFinite(scale) && scale > 0 ? Math.Min(scale, 8) : 1);
    }

    public Point ToScreen(Point path)
    {
        var (box, scale) = Frame();
        return new Point(12 + (path.X - box.X) * scale, 12 + (path.Y - box.Y) * scale);
    }

    public Point ToPath(Point screen)
    {
        var (box, scale) = Frame();
        return new Point((screen.X - 12) / scale + box.X, (screen.Y - 12) / scale + box.Y);
    }

    private int PointAt(Point screen)
    {
        for (var i = _path.Points.Count - 1; i >= 0; i--)
        {
            var at = ToScreen(new Point(_path.Points[i].X, _path.Points[i].Y));
            if ((at - screen).X is var dx && (at - screen).Y is var dy && dx * dx + dy * dy <= 100) return i;
        }

        return -1;
    }

    /// <summary>The index of the point that starts the segment within 8 pixels of <paramref name="screen"/>, or -1. The closing segment of a closed path counts as the last one.</summary>
    private int SegmentAt(Point screen)
    {
        var points = _path.Points.Select(p => ToScreen(new Point(p.X, p.Y))).ToList();
        var segments = _path.IsClosed ? points.Count : points.Count - 1;
        for (var i = 0; i < segments; i++)
        {
            var (a, b) = (points[i], points[(i + 1) % points.Count]);
            var (dx, dy) = (b.X - a.X, b.Y - a.Y);
            var length = dx * dx + dy * dy;
            var t = length == 0 ? 0 : Math.Clamp(((screen.X - a.X) * dx + (screen.Y - a.Y) * dy) / length, 0, 1);
            var (ex, ey) = (screen.X - (a.X + t * dx), screen.Y - (a.Y + t * dy));
            if (ex * ex + ey * ey <= 64) return i;
        }

        return -1;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (_workspace is null || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        Focus();
        var at = e.GetPosition(this);
        var hit = PointAt(at);
        if (hit < 0)
        {
            var added = ToPath(at);
            var after = SegmentAt(at);
            _workspace.Edit(new PathPointsCommand(PathName, PathPointsCommand.Add, after < 0 ? -1 : after + 1, (float)Math.Round(added.X), (float)Math.Round(added.Y)));
            Selected = after < 0 ? _path.Points.Count - 1 : after + 1;
        }
        else
        {
            Selected = hit;
            _frame = Frame();
            _dragTo = ToPath(at);
        }

        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_dragTo is null) return;
        _dragTo = ToPath(e.GetPosition(this));
        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_dragTo is not { } to || _workspace is null) return;
        (_dragTo, _frame) = (null, null);
        if (Selected >= 0 && Selected < _path.Points.Count && (Math.Round(to.X) != _path.Points[Selected].X || Math.Round(to.Y) != _path.Points[Selected].Y))
            _workspace.Edit(new PathPointsCommand(PathName, PathPointsCommand.Move, Selected, (float)Math.Round(to.X), (float)Math.Round(to.Y)));
        InvalidateVisual();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key != Key.Delete || _workspace is null || Selected < 0 || Selected >= _path.Points.Count || _path.Points.Count < 2) return;
        _workspace.Edit(new PathPointsCommand(PathName, PathPointsCommand.Delete, Selected));
        Selected = -1;
        InvalidateVisual();
        e.Handled = true;
    }

    public override void Render(DrawingContext context)
    {
        context.FillRectangle(G3MStudio.Theme.CanvasTheme.Bg, new Rect(Bounds.Size));
        var points = _path.Points.Select(p => new Point(p.X, p.Y)).ToList();
        if (_dragTo is { } to && Selected >= 0 && Selected < points.Count) points[Selected] = to;
        if (points.Count == 0) return;
        Point Map(Point p) => ToScreen(p);
        var line = new StreamGeometry();
        using (var g = line.Open())
        {
            g.BeginFigure(Map(points[0]), false);
            for (var i = 1; i < points.Count; i++)
            {
                if (_path.IsSmooth && i >= 1 && points.Count > 2)
                {
                    var mid = new Point((points[i - 1].X + points[i].X) / 2, (points[i - 1].Y + points[i].Y) / 2);
                    g.QuadraticBezierTo(Map(points[i - 1]), Map(mid));
                }

                g.LineTo(Map(points[i]));
            }

            g.EndFigure(_path.IsClosed);
        }

        context.DrawGeometry(null, new Pen(G3MStudio.Theme.CanvasTheme.Line, 2), line);
        for (var i = 0; i < points.Count; i++) context.DrawEllipse(i == Selected ? Brushes.White : G3MStudio.Theme.CanvasTheme.Point, null, Map(points[i]), i == Selected ? 5 : 3, i == Selected ? 5 : 3);
    }
}

/// <summary>A path with its canvas and a point list: pick a point in the list or on the canvas and type its X and Y.</summary>
public sealed class PathEditor : DockPanel
{
    private readonly GameMakerPath _path;
    private readonly Workspace? _workspace;
    private readonly ListBox _points = new() { Name = "PathPoints", MinWidth = 150 };
    private readonly NumericUpDown _x = new() { Name = "PathPointX", FormatString = "0", Minimum = -100000, Maximum = 100000 };
    private readonly NumericUpDown _y = new() { Name = "PathPointY", FormatString = "0", Minimum = -100000, Maximum = 100000 };
    private readonly NumericUpDown _speed = new() { Name = "PathPointSpeed", FormatString = "0", Minimum = 0, Maximum = 10000 };
    private readonly NumericUpDown _shiftX = new() { Name = "PathShiftX", FormatString = "0", Minimum = -100000, Maximum = 100000, Value = 0 };
    private readonly NumericUpDown _shiftY = new() { Name = "PathShiftY", FormatString = "0", Minimum = -100000, Maximum = 100000, Value = 0 };
    private bool _building;

    public PathEditor(GameMakerPath path, Workspace? workspace)
    {
        (_path, _workspace) = (path, workspace);
        Name = "PathEditor";
        View = new PathView(path, workspace);
        var moveAll = new Button { Name = "PathMoveAll", Content = Loc.Current["paths.moveAll"], IsEnabled = workspace is not null };
        moveAll.Click += (_, _) =>
        {
            if (_workspace is null || _shiftX.Value is not { } dx || _shiftY.Value is not { } dy || (dx == 0 && dy == 0)) return;
            _workspace.Edit(new PathPointsCommand(_path.Name.Content ?? "", PathPointsCommand.Shift, 0, (float)dx, (float)dy));
        };
        _pointFields.Children.Add(Labelled("X", _x));
        _pointFields.Children.Add(Labelled("Y", _y));
        _pointFields.Children.Add(Labelled(Loc.Current["paths.speed"], _speed));
        var side = new StackPanel { Width = 190, Margin = new Thickness(0, 0, 8, 0), Spacing = 4, Children = { _points, _pointFields, new Separator(), new TextBlock { Text = Loc.Current["paths.moveAll"], Classes = { "muted" } }, Labelled("X", _shiftX), Labelled("Y", _shiftY), moveAll } };
        SetDock(side, Avalonia.Controls.Dock.Left);
        Children.Add(side);
        Children.Add(View);
        _points.SelectionChanged += (_, _) =>
        {
            if (!_building) View.Selected = _points.SelectedIndex;
        };
        View.SelectionChanged += Show;
        _x.ValueChanged += (_, _) => Move();
        _y.ValueChanged += (_, _) => Move();
        _speed.ValueChanged += (_, _) => ChangeSpeed();
        if (workspace is not null)
        {
            workspace.History.Changed += Refresh;
            DetachedFromVisualTree += (_, _) => workspace.History.Changed -= Refresh;
        }

        Refresh();
    }

    public PathView View { get; }

    private static DockPanel Labelled(string text, Control box)
    {
        var label = new TextBlock { Text = text, MinWidth = 70, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) };
        DockPanel.SetDock(label, Avalonia.Controls.Dock.Left);
        return new DockPanel { Children = { label, box } };
    }

    private void ChangeSpeed()
    {
        if (_building || _workspace is null || View.Selected < 0 || View.Selected >= _path.Points.Count || _speed.Value is not { } percent) return;
        var factor = (float)percent / 100f;
        if (Math.Abs(_path.Points[View.Selected].Speed - factor) < 1e-6) return;
        _workspace.Edit(new PathPointsCommand(_path.Name.Content ?? "", PathPointsCommand.Speed, View.Selected, factor));
    }

    private void Refresh()
    {
        _building = true;
        _points.ItemsSource = _path.Points.Select((p, i) => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{i + 1}  {p.X:0.##}, {p.Y:0.##}")).ToList();
        _points.SelectedIndex = View.Selected < _path.Points.Count ? View.Selected : -1;
        _building = false;
        Show();
    }

    private readonly StackPanel _pointFields = new() { Name = "PathPointFields", Spacing = 4 };

    private void Show()
    {
        _building = true;
        var point = View.Selected >= 0 && View.Selected < _path.Points.Count ? _path.Points[View.Selected] : null;
        (_x.IsEnabled, _y.IsEnabled) = (point is not null && _workspace is not null, point is not null && _workspace is not null);
        _x.Value = point is null ? null : (decimal)Math.Round(point.X);
        _y.Value = point is null ? null : (decimal)Math.Round(point.Y);
        _speed.IsEnabled = point is not null && _workspace is not null;
        _pointFields.IsVisible = point is not null; // no point selected: no empty boxes
        _speed.Value = point is null ? null : (decimal)Math.Round(point.Speed * 100);
        _points.SelectedIndex = View.Selected < _path.Points.Count ? View.Selected : -1;
        _building = false;
    }

    private void Move()
    {
        if (_building || _workspace is null || View.Selected < 0 || View.Selected >= _path.Points.Count || _x.Value is not { } x || _y.Value is not { } y) return;
        var point = _path.Points[View.Selected];
        if (point.X == (float)x && point.Y == (float)y) return;
        _workspace.Edit(new PathPointsCommand(_path.Name.Content ?? "", PathPointsCommand.Move, View.Selected, (float)x, (float)y));
    }
}

/// <summary>The glyphs of a font as a list (code point, character, position on the sheet, size, shift, offset) with a filter that takes a character, a code like U+0041 or a number.</summary>
public sealed class FontGlyphsView : StackPanel
{
    private readonly GameMakerFont _font;
    private readonly Workspace? _workspace;
    private readonly TextBox _filter = new() { Name = "GlyphFilter" };
    private readonly TextBox _newCharacter = new() { Name = "GlyphNew", Width = 90 };
    private readonly ListBox _list = new() { Name = "FontGlyphs", MaxHeight = 150 };
    private readonly TextBlock _count = new() { Classes = { "muted" } };
    private readonly ContentControl _glyph = new() { Name = "GlyphProperties", MaxHeight = 260 };
    private readonly List<(ushort Character, string Text, int Index)> _rows = [];
    private readonly List<int> _shown = [];
    private readonly Button _replace = new() { Name = "FontReplace" };

    /// <summary>Called with the font's name when the user asks to draw it again from a font file; the window picks the file. Without it the button stays disabled.</summary>
    public Action<string>? ReplaceRequested
    {
        get;
        set
        {
            field = value;
            _replace.IsEnabled = value is not null;
        }
    }

    /// <summary>The picture of the font's texture, when the tab shows one: a picked glyph is outlined on it and a click on it picks a glyph.</summary>
    public TextureItemView? Sheet
    {
        get;
        init
        {
            field = value;
            if (value is { } sheet) sheet.Picked += (x, y) => PickAt(x, y);
        }
    }

    public FontGlyphsView(GameMakerFont font, Workspace? workspace = null)
    {
        (_font, _workspace) = (font, workspace);
        Name = "FontGlyphsView";
        Spacing = 4;
        _filter.PlaceholderText = Loc.Current["fonts.glyphFilter"];
        _filter.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty) Refresh();
        };
        Children.Add(_count);
        Children.Add(_filter);
        Children.Add(_list);
        Children.Add(_glyph);
        if (workspace is not null)
        {
            // Picking a glyph opens a property list of its fields; Add makes an empty glyph for a character and Remove deletes the picked one (one undo step each).
            _list.SelectionChanged += (_, _) => ShowOnSheet();
            _list.SelectionChanged += (_, _) =>
                _glyph.Content = _list.SelectedIndex is >= 0 and var row && row < _shown.Count
                    ? new StackPanel { Spacing = 6, Children = { new KerningView(font, _shown[row], workspace), new PropertyGridTab(workspace, "fonts", font.Name.Content ?? "", null, $"Glyphs[{_shown[row]}]") } }
                    : null;
            _newCharacter.PlaceholderText = Loc.Current["fonts.glyphNew"];
            var add = new Button { Name = "GlyphAdd", Content = Loc.Current["fonts.glyphAdd"] };
            add.Click += (_, _) => Add();
            var remove = new Button { Name = "GlyphRemove", Content = Loc.Current["timelines.remove"] };
            remove.Click += (_, _) =>
            {
                if (_list.SelectedIndex is >= 0 and var row && row < _shown.Count) workspace.Edit(new FontGlyphCommand(font.Name.Content ?? "", FontGlyphCommand.Remove, _shown[row]));
            };
            _replace.Content = Loc.Current["fonts.replace"];
            _replace.IsEnabled = ReplaceRequested is not null;
            Avalonia.Controls.ToolTip.SetTip(_replace, Loc.Current["fonts.replaceTip"]);
            _replace.Click += (_, _) => ReplaceRequested?.Invoke(font.Name.Content ?? "");
            Children.Add(new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 6, Children = { _newCharacter, add, remove, _replace } });
            AttachedToVisualTree += (_, _) => workspace.History.Changed += Reload;
            DetachedFromVisualTree += (_, _) => workspace.History.Changed -= Reload;
        }

        Reload();
    }

    public int Shown => _list.ItemCount;

    private void ShowOnSheet()
    {
        var glyph = _list.SelectedIndex is >= 0 and var row && row < _shown.Count ? _font.Glyphs[_shown[row]] : null;
        Sheet?.Highlight(glyph is null ? null : (glyph.SourceX, glyph.SourceY, glyph.SourceWidth, glyph.SourceHeight));
    }

    /// <summary>Picks the listed glyph whose rectangle holds a pixel of the sheet (as a click on the sheet does); false when none does.</summary>
    public bool PickAt(int x, int y)
    {
        for (var row = 0; row < _shown.Count; row++)
        {
            var g = _font.Glyphs[_shown[row]];
            if (x < g.SourceX || y < g.SourceY || x >= g.SourceX + g.SourceWidth || y >= g.SourceY + g.SourceHeight) continue;
            _list.SelectedIndex = row;
            return true;
        }

        return false;
    }

    /// <summary>The character a code such as <c>U+0041</c>, a number or a single character stands for, or null.</summary>
    internal static int? ParseCharacter(string text)
    {
        text = text.Trim();
        if (text.StartsWith("U+", StringComparison.OrdinalIgnoreCase) && int.TryParse(text[2..], System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var hex)) return hex;
        if (text.Length == 1) return text[0];
        return int.TryParse(text, System.Globalization.CultureInfo.InvariantCulture, out var number) ? number : null;
    }

    /// <summary>The characters of <c>A-Z</c> or <c>U+0020-U+007E</c> (at most 2048), or just the one character a single code stands for; null when the text is neither.</summary>
    public static IReadOnlyList<int>? ParseRange(string text)
    {
        var parts = text.Trim().Split('-', 2);
        if (parts.Length == 2 && parts[0].Length > 0 && parts[1].Length > 0 && ParseCharacter(parts[0]) is { } from && ParseCharacter(parts[1]) is { } to)
            return from <= to && to <= ushort.MaxValue && to - from < 2048 && from >= 0 ? [.. Enumerable.Range(from, to - from + 1)] : null;
        return ParseCharacter(text) is { } one and >= 0 and <= ushort.MaxValue ? [one] : null;
    }

    private void Add()
    {
        if (_workspace is null || ParseRange(_newCharacter.Text ?? "") is not { } codes) return;
        var font = _font.Name.Content ?? "";
        var added = codes.Where(code => _font.Glyphs.All(g => g.Character != code)).Select(code => (EditCommand)new FontGlyphCommand(font, FontGlyphCommand.Add, -1, (ushort)code)).ToList();
        if (added.Count == 1) _workspace.Edit(added[0]);
        else if (added.Count > 1) _workspace.Edit(new GroupCommand(added));
    }

    private void Reload()
    {
        _rows.Clear();
        _rows.AddRange(_font.Glyphs.Select((g, i) => (g.Character, string.Create(System.Globalization.CultureInfo.InvariantCulture,
            $"U+{g.Character:X4}  {(char.IsControl((char)g.Character) ? ' ' : (char)g.Character)}   {g.SourceX},{g.SourceY}  {g.SourceWidth}x{g.SourceHeight}  shift {g.Shift}  offset {g.Offset}"), i)));
        _count.Text = Loc.Current.Format("fonts.glyphCount", ("count", _rows.Count));
        Refresh();
    }

    private void Refresh()
    {
        var text = (_filter.Text ?? "").Trim();
        var code = ParseCharacter(text);
        var matching = _rows.Where(r => text.Length == 0 || r.Character == code).ToList();
        _shown.Clear();
        _shown.AddRange(matching.Select(r => r.Index));
        _list.ItemsSource = matching.Select(r => r.Text).ToList();
    }
}

/// <summary>The kerning pairs of one glyph: each is the next character and the shift added between the two; Set adds a pair or changes its shift, Remove deletes the picked one (UTMT-FNT-04).</summary>
public sealed class KerningView : StackPanel
{
    private readonly GameMakerFont _font;
    private readonly int _glyph;
    private readonly Workspace _workspace;
    private readonly ListBox _list = new() { Name = "KerningPairs", MaxHeight = 120 };
    private readonly TextBox _character = new() { Name = "KerningCharacter", Width = 110 };
    private readonly TextBox _shift = new() { Name = "KerningShift", Width = 80 };

    public KerningView(GameMakerFont font, int glyph, Workspace workspace)
    {
        (_font, _glyph, _workspace) = (font, glyph, workspace);
        Name = "KerningView";
        Spacing = 4;
        _character.PlaceholderText = Loc.Current["fonts.kernCharacter"];
        _shift.PlaceholderText = Loc.Current["fonts.kernShift"];
        var set = new Button { Name = "KerningSet", Content = Loc.Current["fonts.kernSet"] };
        set.Click += (_, _) => Set();
        var remove = new Button { Name = "KerningRemove", Content = Loc.Current["timelines.remove"] };
        remove.Click += (_, _) =>
        {
            if (_list.SelectedIndex is >= 0 and var row && row < Pairs.Count) workspace.Edit(new FontKerningCommand(Font, glyph, FontKerningCommand.Remove, Pairs[row].Character));
        };
        Children.Add(new TextBlock { Text = Loc.Current["fonts.kerning"], Classes = { "muted" } });
        Children.Add(_list);
        Children.Add(new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 6, Children = { _character, _shift, set, remove } });
        AttachedToVisualTree += (_, _) => workspace.History.Changed += Reload;
        DetachedFromVisualTree += (_, _) => workspace.History.Changed -= Reload;
        Reload();
    }

    private string Font => _font.Name.Content ?? "";

    private IReadOnlyList<GameMakerFont.Glyph.GlyphKerning> Pairs => _glyph < _font.Glyphs.Count ? [.. _font.Glyphs[_glyph].Kerning] : [];

    public int Count => Pairs.Count;

    /// <summary>The same edit as the Set button with the two boxes filled in; false when the character or the shift is not a number in range.</summary>
    public bool Set(string character, string shift)
    {
        if (FontGlyphsView.ParseCharacter(character) is not (>= short.MinValue and <= ushort.MaxValue and var code) || !short.TryParse(shift, System.Globalization.CultureInfo.InvariantCulture, out var amount)) return false;
        _workspace.Edit(new FontKerningCommand(Font, _glyph, FontKerningCommand.Set, unchecked((short)code), amount));
        return true;
    }

    private void Set() => Set(_character.Text ?? "", _shift.Text ?? "");

    private void Reload() => _list.ItemsSource = Pairs.Select(k => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"U+{(ushort)k.Character:X4}  {(char.IsControl((char)k.Character) ? ' ' : (char)k.Character)}   shift {k.ShiftModifier}")).ToList();
}

/// <summary>The vertices of an object's physics shape as a list with X and Y boxes: Add puts a vertex at the end, Set moves the picked one, Remove deletes it (UTMT-OBJ-03).</summary>
public sealed class PhysicsVerticesView : StackPanel
{
    private readonly GameMakerGameObject _object;
    private readonly Workspace _workspace;
    private readonly ListBox _list = new() { Name = "PhysicsVertices", MaxHeight = 120 };
    private readonly TextBox _x = new() { Name = "VertexX", Width = 90 };
    private readonly TextBox _y = new() { Name = "VertexY", Width = 90 };

    public PhysicsVerticesView(GameMakerGameObject obj, Workspace workspace)
    {
        (_object, _workspace) = (obj, workspace);
        Name = "PhysicsVerticesView";
        Spacing = 4;
        _x.PlaceholderText = "X";
        _y.PlaceholderText = "Y";
        var add = new Button { Name = "VertexAdd", Content = Loc.Current["object.vertexAdd"] };
        var set = new Button { Name = "VertexSet", Content = Loc.Current["object.vertexSet"] };
        var remove = new Button { Name = "VertexRemove", Content = Loc.Current["timelines.remove"] };
        add.Click += (_, _) => Edit(PhysicsVertexCommand.Add, -1);
        set.Click += (_, _) => Edit(PhysicsVertexCommand.Set, _list.SelectedIndex);
        remove.Click += (_, _) => Edit(PhysicsVertexCommand.Remove, _list.SelectedIndex, true);
        _list.SelectionChanged += (_, _) =>
        {
            if (_list.SelectedIndex is >= 0 and var row && row < _object.PhysicsVertices.Count) (_x.Text, _y.Text) = (Format(_object.PhysicsVertices[row].X), Format(_object.PhysicsVertices[row].Y));
        };
        Children.Add(new TextBlock { Text = Loc.Current["object.vertices"], Classes = { "muted" } });
        Children.Add(_list);
        Children.Add(new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 6, Children = { _x, _y, add, set, remove } });
        AttachedToVisualTree += (_, _) => workspace.History.Changed += Reload;
        DetachedFromVisualTree += (_, _) => workspace.History.Changed -= Reload;
        Reload();
    }

    public int Count => _object.PhysicsVertices.Count;

    private static string Format(float value) => value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>One edit from the boxes; a box that is not a number does nothing. Remove needs no numbers.</summary>
    public bool Edit(string operation, int index, bool needsNoNumbers = false)
    {
        var parsed = float.TryParse(_x.Text, System.Globalization.CultureInfo.InvariantCulture, out var x) & float.TryParse(_y.Text, System.Globalization.CultureInfo.InvariantCulture, out var y);
        if ((!parsed && !needsNoNumbers) || (operation != PhysicsVertexCommand.Add && (index < 0 || index >= Count))) return false;
        _workspace.Edit(new PhysicsVertexCommand(_object.Name.Content ?? "", operation, index, x, y));
        return true;
    }

    public void SetBoxes(string x, string y) => (_x.Text, _y.Text) = (x, y);

    private void Reload() => _list.ItemsSource = _object.PhysicsVertices.Select((v, i) => $"{i}:  {Format(v.X)}, {Format(v.Y)}").ToList();
}

/// <summary>The tile layout of a tile set in one line: tile size, columns, count, the border the exporter adds around each tile and the separation between tiles (UTMT-BG-05). Follows edits.</summary>
public sealed class TileSetInfoView : TextBlock
{
    public TileSetInfoView(GameMakerBackground background, Workspace? workspace = null)
    {
        Name = "TileSetInfo";
        Classes.Add("muted");
        Margin = new Thickness(0, 4);
        Text = Describe(background);
        if (workspace is null) return;
        void Reload() => Text = Describe(background);
        AttachedToVisualTree += (_, _) => workspace.History.Changed += Reload;
        DetachedFromVisualTree += (_, _) => workspace.History.Changed -= Reload;
    }

    public static string Describe(GameMakerBackground background) => Loc.Current.Format("tilesets.info",
        ("width", background.GMS2TileWidth), ("height", background.GMS2TileHeight), ("columns", background.GMS2TileColumns), ("count", background.GMS2TileCount),
        ("borderX", background.GMS2OutputBorderX), ("borderY", background.GMS2OutputBorderY), ("separationX", background.GMS2TileSeparationX), ("separationY", background.GMS2TileSeparationY));
}

/// <summary>The emitters of a particle system, one line each: name, on or off, emit mode and count, shape and region size.</summary>
public sealed class ParticleEmittersView : StackPanel
{
    private readonly GameMakerParticleSystem _system;
    private readonly Workspace? _workspace;

    public ParticleEmittersView(GameMakerParticleSystem system, Workspace? workspace = null)
    {
        (_system, _workspace) = (system, workspace);
        Name = "ParticleEmitters";
        Lines = [];
        if (workspace is not null)
        {
            AttachedToVisualTree += (_, _) => workspace.History.Changed += Rebuild;
            DetachedFromVisualTree += (_, _) => workspace.History.Changed -= Rebuild;
        }

        Rebuild();
    }

    public IReadOnlyList<string> Lines { get; private set; }

    private void Rebuild()
    {
        Lines = [.. (_system.Emitters ?? []).Select(r => r.Resource).OfType<GameMakerParticleSystemEmitter>().Select(e => string.Create(System.Globalization.CultureInfo.InvariantCulture,
            $"{e.Name?.Content}  {(e.Enabled ? "on" : "off")}  {e.Mode} x{e.EmitCount}  {e.Shape} {e.RegionX:0.##} x {e.RegionY:0.##}"))];
        if (_workspace is not { } workspace)
        {
            ItemsSource = Lines;
            return;
        }

        // With a workspace: each emitter opens into its property list and has a Remove button; Add makes a new emitter with default settings.
        var name = _system.Name.Content ?? "";
        var items = Lines.Select((line, i) =>
        {
            var remove = new Button { Name = "EmitterRemove_" + i, Content = Loc.Current["timelines.remove"] };
            remove.Click += (_, _) => workspace.Edit(new ParticleEmitterCommand(name, ParticleEmitterCommand.Remove, i));
            remove.Margin = new Avalonia.Thickness(12, 0, 40, 0); // clear of the expander arrow at the right edge
            var header = new DockPanel { LastChildFill = true, Children = { remove, new TextBlock { Text = line, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center } } };
            DockPanel.SetDock(remove, Avalonia.Controls.Dock.Right);
            var expander = Expandable(line, workspace, "particleSystems", name, $"Emitters[{i}].Resource");
            expander.Header = header;
            return (object)expander;
        }).ToList();
        var add = new Button { Name = "EmitterAdd", Content = Loc.Current["particles.add"] };
        add.Click += (_, _) => workspace.Edit(new ParticleEmitterCommand(name, ParticleEmitterCommand.Add));
        items.Add(add);
        ItemsSource = items;
    }

    /// <summary>A header line that opens the property list of one part of a resource, built when first opened.</summary>
    internal static Expander Expandable(string header, Workspace workspace, string type, string name, string subPath)
    {
        var expander = new Expander { Header = header, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch, HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Stretch };
        expander.Expanded += (_, _) => expander.Content ??= new PropertyGridTab(workspace, type, name, null, subPath) { MaxHeight = 320 };
        return expander;
    }

    // The list is built from plain controls: an items control in a docked header got no size in the tab.
    private IEnumerable<object> ItemsSource
    {
        set
        {
            Children.Clear();
            foreach (var item in value) Children.Add(item as Control ?? new TextBlock { Text = item.ToString() });
        }
    }
}
/// <summary>The tracks of a sequence as an indented tree (name, kind), after a line with its length, speed and moment and message counts.</summary>
public sealed class SequenceTracksView : StackPanel
{
    private readonly GameMakerSequence _sequence;
    private readonly Workspace? _workspace;

    public SequenceTracksView(GameMakerSequence sequence, Workspace? workspace = null)
    {
        (_sequence, _workspace) = (sequence, workspace);
        Name = "SequenceTracks";
        Lines = [];
        if (workspace is not null)
        {
            AttachedToVisualTree += (_, _) => workspace.History.Changed += Rebuild;
            DetachedFromVisualTree += (_, _) => workspace.History.Changed -= Rebuild;
        }

        Rebuild();
    }

    private void Rebuild()
    {
        var (sequence, workspace) = (_sequence, _workspace);
        var lines = new List<string>();
        foreach (var track in sequence.Tracks ?? []) Add(lines, track, 0);
        Lines = lines;
        var name = sequence.Name.Content ?? "";
        if (workspace is null)
        {
            ItemsSource = lines;
            return;
        }

        ItemsSource = ((sequence.Tracks ?? []).Select((track, i) =>
        {
            var expander = ParticleEmittersView.Expandable(track.Name?.Content + "  " + track.ModelName?.Content, workspace, "sequences", name, $"Tracks[{i}]");
            var remove = new Button { Name = "TrackRemove_" + i, Content = Loc.Current["timelines.remove"] };
            remove.Click += (_, _) => workspace.Edit(new SequenceTrackCommand(name, SequenceTrackCommand.Remove, i));
            var header = new DockPanel { Children = { new TextBlock { Text = track.Name?.Content + "  " + track.ModelName?.Content, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center }, remove } };
            DockPanel.SetDock(remove, Avalonia.Controls.Dock.Right);
            expander.Header = header;
            return (object)expander;
        }).Append(AddGroupButton(workspace, name)));
    }

    private static Button AddGroupButton(Workspace workspace, string sequence)
    {
        var add = new Button { Name = "TrackAddGroup", Content = Loc.Current["sequences.addGroup"] };
        add.Click += (_, _) => workspace.Edit(new SequenceTrackCommand(sequence, SequenceTrackCommand.AddGroup, -1, Loc.Current["sequences.newGroup"]));
        return add;
    }

    public IReadOnlyList<string> Lines { get; private set; }

    private static void Add(List<string> lines, GameMakerSequence.Track track, int depth)
    {
        lines.Add(new string(' ', depth * 3) + track.Name?.Content + "  " + track.ModelName?.Content);
        foreach (var child in track.Tracks ?? []) Add(lines, child, depth + 1);
    }

    // The list is built from plain controls: an items control in a docked header got no size in the tab.
    private IEnumerable<object> ItemsSource
    {
        set
        {
            Children.Clear();
            foreach (var item in value) Children.Add(item as Control ?? new TextBlock { Text = item.ToString() });
        }
    }
}
/// <summary>One step box and a Remove button per moment of a timeline, in file order, and an Add Moment button. Every change is one undo step.</summary>
public sealed class TimelineStepsView : StackPanel
{
    private readonly GameMakerTimeline _timeline;
    private readonly Workspace _workspace;
    private bool _filling;

    private readonly Action<string> _open;

    public TimelineStepsView(GameMakerTimeline timeline, Workspace workspace, Action<string> open)
    {
        (_timeline, _workspace, _open) = (timeline, workspace, open);
        Name = "TimelineSteps";
        Spacing = 2;
        AttachedToVisualTree += (_, _) => workspace.History.Changed += Fill;
        DetachedFromVisualTree += (_, _) => workspace.History.Changed -= Fill;
        Fill();
    }

    private string TimelineName => _timeline.Name.Content ?? "";

    private void Fill()
    {
        _filling = true;
        Children.Clear();
        for (var i = 0; i < _timeline.Moments.Count; i++)
        {
            var (index, moment) = (i, _timeline.Moments[i]);
            var box = new NumericUpDown { Name = "MomentStep_" + i, Minimum = 0, Maximum = 1_000_000, FormatString = "0", Value = moment.Step };
            box.ValueChanged += (_, e) =>
            {
                if (!_filling && e.NewValue is { } value && (uint)value != moment.Step) _workspace.Edit(new SetPropertyCommand("timelines", TimelineName, $"Moments[{index}].Step", ((uint)value).ToString(System.Globalization.CultureInfo.InvariantCulture)));
            };
            var remove = new Button { Name = "MomentRemove_" + i, Content = Loc.Current["timelines.remove"] };
            remove.Click += (_, _) => _workspace.Edit(new TimelineMomentCommand(TimelineName, TimelineMomentCommand.Remove, index));
            var label = new TextBlock { Text = Loc.Current.Format("timelines.moment", ("number", i + 1)), VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
            DockPanel.SetDock(label, Avalonia.Controls.Dock.Left);
            DockPanel.SetDock(remove, Avalonia.Controls.Dock.Right);
            Children.Add(new DockPanel { Children = { label, remove, box } });

            // The actions of the moment: each runs a code entry; Add action takes the name of an existing code entry.
            for (var a = 0; a < (moment.Event?.Count ?? 0); a++)
            {
                var actionIndex = a;
                var removeAction = new Button { Name = $"ActionRemove_{i}_{a}", Content = Loc.Current["timelines.remove"] };
                removeAction.Click += (_, _) => _workspace.Edit(new TimelineActionCommand(TimelineName, TimelineActionCommand.Remove, index, actionIndex));
                var code = moment.Event?[a].CodeId?.Name?.Content ?? "?";
                // The name opens the code entry the action runs.
                var codeName = new Button { Content = code, Classes = { "link" }, Margin = new Thickness(24, 0, 8, 0), HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Left };
                codeName.Click += (_, _) => _open(ResourceTypes.KeyOf("code", code));
                DockPanel.SetDock(removeAction, Avalonia.Controls.Dock.Right);
                Children.Add(new DockPanel { Children = { removeAction, codeName } });
            }

            var newCode = new TextBox { Name = "ActionCode_" + i, PlaceholderText = Loc.Current["timelines.actionCode"], Margin = new Thickness(24, 0, 8, 0) };
            var addAction = new Button { Name = "ActionAdd_" + i, Content = Loc.Current["timelines.addAction"] };
            addAction.Click += (_, _) =>
            {
                if (newCode.Text is { Length: > 0 } text && _workspace.Data.Code.Any(c => c.Name.Content == text)) _workspace.Edit(new TimelineActionCommand(TimelineName, TimelineActionCommand.Add, index, -1, text));
            };
            DockPanel.SetDock(addAction, Avalonia.Controls.Dock.Right);
            Children.Add(new DockPanel { Children = { addAction, newCode } });
        }

        var add = new Button { Name = "MomentAdd", Content = Loc.Current["timelines.add"] };
        add.Click += (_, _) => _workspace.Edit(new TimelineMomentCommand(TimelineName, TimelineMomentCommand.Add, -1, _timeline.Moments.Select(m => m.Step + 1).DefaultIfEmpty(0u).Max()));
        Children.Add(add);
        _filling = false;
    }
}

/// <summary>The channels of an animation curve drawn as lines through their points, one colour per channel, with a legend.</summary>
public sealed class AnimationCurveView : Control
{
    private static readonly IBrush[] Colors = [Brushes.DeepSkyBlue, Brushes.Orange, Brushes.LightGreen, Brushes.Violet, Brushes.Khaki, Brushes.Salmon];
    private readonly GameMakerAnimationCurve _curve;

    public AnimationCurveView(GameMakerAnimationCurve curve)
    {
        _curve = curve;
        Name = "AnimationCurveView";
        Height = 260;
    }

    public override void Render(DrawingContext context)
    {
        context.FillRectangle(G3MStudio.Theme.CanvasTheme.Bg, new Rect(Bounds.Size));
        var all = _curve.Channels.SelectMany(c => c.Points).ToList();
        if (all.Count == 0) return;
        var (minX, maxX) = (all.Min(p => p.X), all.Max(p => p.X));
        var (minY, maxY) = (all.Min(p => p.Value), all.Max(p => p.Value));
        double Span(float low, float high) => high - low < 1e-6 ? 1 : high - low;
        var (spanX, spanY) = (Span(minX, maxX), Span(minY, maxY));
        Point Map(float x, float y) => new(44 + (x - minX) / spanX * Math.Max(1, Bounds.Width - 68), Bounds.Height - 24 - (y - minY) / spanY * Math.Max(1, Bounds.Height - 48));
        var axis = new Pen(new SolidColorBrush(Avalonia.Media.Colors.White, 0.25), 1);
        var grid = new Pen(new SolidColorBrush(Avalonia.Media.Colors.White, 0.08), 1);
        for (var q = 1; q < 4; q++)
        {
            context.DrawLine(grid, Map(minX, minY + (float)(spanY * q / 4)), Map(maxX, minY + (float)(spanY * q / 4)));
            context.DrawLine(grid, Map(minX + (float)(spanX * q / 4), minY), Map(minX + (float)(spanX * q / 4), maxY));
        }

        context.DrawLine(axis, Map(minX, minY), Map(maxX, minY));
        context.DrawLine(axis, Map(minX, minY), Map(minX, maxY));
        void Label(float value, Point at)
        {
            var text = new FormattedText(value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture), System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Typeface.Default, 11, Brushes.Gray);
            context.DrawText(text, at);
        }

        Label(maxY, new Point(2, Map(minX, maxY).Y - 7));
        Label(minY, new Point(2, Map(minX, minY).Y - 7));
        Label(minX, new Point(Map(minX, minY).X - 4, Bounds.Height - 16));
        Label(maxX, new Point(Map(maxX, minY).X - 12, Bounds.Height - 16));
        for (var c = 0; c < _curve.Channels.Count; c++)
        {
            var brush = Colors[c % Colors.Length];
            var points = _curve.Channels[c].Points.OrderBy(p => p.X).ToList();
            for (var i = 1; i < points.Count; i++) context.DrawLine(new Pen(brush, 2), Map(points[i - 1].X, points[i - 1].Value), Map(points[i].X, points[i].Value));
            foreach (var p in points) context.DrawEllipse(brush, null, Map(p.X, p.Value), 3, 3);
            var label = new FormattedText(_curve.Channels[c].Name?.Content ?? "", System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Typeface.Default, 12, brush);
            context.DrawText(label, new Point(48 + c * 110, 4));
        }
    }
}

/// <summary>The shader sources of a shader, one tab per language the shader holds, each in a code view with line numbers. With a workspace a changed source is saved with its Apply button as one undo step.</summary>
public sealed class ShaderSourceView : TabControl
{
    public ShaderSourceView(GameMakerShader shader, Workspace? workspace = null)
    {
        Name = "ShaderSources";
        Height = 360;
        Margin = new Thickness(0, 0, 0, 8); // keeps the editor off the property filter below
        (string Title, string Property, string? Text)[] all =
        [
            ("GLSL ES vertex", nameof(GameMakerShader.GLSL_ES_Vertex), shader.GLSL_ES_Vertex?.Content), ("GLSL ES fragment", nameof(GameMakerShader.GLSL_ES_Fragment), shader.GLSL_ES_Fragment?.Content),
            ("GLSL vertex", nameof(GameMakerShader.GLSL_Vertex), shader.GLSL_Vertex?.Content), ("GLSL fragment", nameof(GameMakerShader.GLSL_Fragment), shader.GLSL_Fragment?.Content),
            ("HLSL9 vertex", nameof(GameMakerShader.HLSL9_Vertex), shader.HLSL9_Vertex?.Content), ("HLSL9 fragment", nameof(GameMakerShader.HLSL9_Fragment), shader.HLSL9_Fragment?.Content),
        ];
        var present = all.Where(s => !string.IsNullOrWhiteSpace(s.Text)).ToList();
        Sources = [.. present.Select(s => (s.Title, Text: s.Text ?? string.Empty))];
        foreach (var (title, property, text) in present)
        {
            // The GML colors also suit GLSL comments, numbers and the shared keywords; shader types stay plain.
            var box = GmlColorizer.CreateEditor(text ?? string.Empty, workspace is null, out var colorizer);
            box.Name = "Shader_" + property;
            box.TextChanged += (_, _) =>
            {
                colorizer.Tokenize(box.Text);
                box.TextArea.TextView.Redraw();
            };
            var page = new DockPanel();
            if (workspace is not null)
            {
                var apply = new Button { Name = "ShaderApply_" + property, Content = Loc.Current["strings.apply"], Margin = new Thickness(0, 4) };
                apply.Click += (_, _) =>
                {
                    if (box.Text is { } changed && changed != PropertyModel.Read(shader, property)) workspace.Edit(new SetPropertyCommand("shaders", shader.Name.Content ?? "", property, changed));
                };
                DockPanel.SetDock(apply, Avalonia.Controls.Dock.Top);
                page.Children.Add(apply);
            }

            page.Children.Add(box);
            Items.Add(new TabItem { Header = title, Content = page, FontSize = 12, Padding = new Thickness(8, 4) }); // snug, so the six tabs stay on one row
        }
    }

    /// <summary>The shader fields these tabs edit; the property list leaves them out so a source is not shown twice.</summary>
    public static readonly string[] SourceProperties = [nameof(GameMakerShader.GLSL_ES_Vertex), nameof(GameMakerShader.GLSL_ES_Fragment), nameof(GameMakerShader.GLSL_Vertex), nameof(GameMakerShader.GLSL_Fragment), nameof(GameMakerShader.HLSL9_Vertex), nameof(GameMakerShader.HLSL9_Fragment)];

    protected override Type StyleKeyOverride => typeof(TabControl);

    public IReadOnlyList<(string Title, string Text)> Sources { get; }
}

/// <summary>The code the game runs when it starts and when it ends, as two lists with a box to pick a code entry: Add puts it at the end, Remove takes the picked one out, a double click opens it (UTMT-AST-13, UTMT-AST-14).</summary>
public sealed class EntryScriptsView : StackPanel
{
    private readonly GameMakerData _data;
    private readonly Workspace _workspace;
    private readonly Action<string> _open;
    private readonly List<(string List, ListBox Box)> _lists = [];

    public EntryScriptsView(Workspace workspace, Action<string> open)
    {
        (_workspace, _data, _open) = (workspace, workspace.Data, open);
        Name = "EntryScriptsView";
        Spacing = 8;
        foreach (var (list, title) in new[] { (EntryScriptsCommand.GlobalInit, "entryScripts.globalInit"), (EntryScriptsCommand.GameEnd, "entryScripts.gameEnd") })
        {
            if (_data.GlobalInitScripts is null && list == EntryScriptsCommand.GlobalInit || _data.GameEndScripts is null && list == EntryScriptsCommand.GameEnd) continue;
            var box = new ListBox { Name = "Scripts_" + list, MaxHeight = 120 };
            box.DoubleTapped += (_, _) => { if (box.SelectedItem is string code) _open(ResourceTypes.KeyOf("code", code)); };
            var pick = new AutoCompleteBox { Name = "ScriptPick_" + list, Width = 240, FilterMode = AutoCompleteFilterMode.Contains, ItemsSource = _data.Code.Select(c => c.Name.Content ?? "").ToList() };
            var add = new Button { Name = "ScriptAdd_" + list, Content = Loc.Current["entryScripts.add"] };
            add.Click += (_, _) => Add(list, pick.Text ?? "");
            var remove = new Button { Name = "ScriptRemove_" + list, Content = Loc.Current["timelines.remove"] };
            remove.Click += (_, _) => { if (box.SelectedItem is string code) Remove(list, code); };
            Children.Add(new TextBlock { Text = Loc.Current[title], Classes = { "muted" } });
            Children.Add(box);
            Children.Add(new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 6, Children = { pick, add, remove } });
            _lists.Add((list, box));
        }

        AttachedToVisualTree += (_, _) =>
        {
            workspace.History.Changed += Reload;
            Reload();
        };
        DetachedFromVisualTree += (_, _) => workspace.History.Changed -= Reload;
        Reload();
    }

    public IReadOnlyList<string> Codes(string list) => [.. EntryScriptsCommand.ListOf(_data, list).Select(EntryScriptsCommand.Describe)];

    /// <summary>The same edit as the Add button; false when no code entry has that name or it is already listed.</summary>
    public bool Add(string list, string code)
    {
        if (!_data.Code.Any(c => c.Name.Content == code) || Codes(list).Contains(code)) return false;
        _workspace.Edit(new EntryScriptsCommand(list, EntryScriptsCommand.Add, code));
        return true;
    }

    public void Remove(string list, string code)
    {
        // The list can have changed since it was drawn (an undo elsewhere); a stale row is then just dropped from the view.
        if (Codes(list).Contains(code)) _workspace.Edit(new EntryScriptsCommand(list, EntryScriptsCommand.Remove, code));
        else Reload();
    }

    private void Reload()
    {
        foreach (var (list, box) in _lists) box.ItemsSource = Codes(list);
    }
}

/// <summary>
/// A tile set's texture beside the list of its tile ids: a click on a tile picks its row, a picked row outlines its tile (UTMT-BG-03, UTMT-BG-04). The ids themselves are edited in the property list.
/// </summary>
public sealed class TilePickerView : StackPanel
{
    private readonly GameMakerBackground _background;
    private readonly ListBox _list = new() { Name = "TileIds", MaxHeight = 160 };

    public TilePickerView(GameMakerBackground background, TextureBitmaps textures, Workspace workspace)
    {
        _background = background;
        Name = "TilePickerView";
        Spacing = 4;
        Texture = new TextureItemView(background.Texture, textures);
        Texture.Picked += (x, y) => Pick(TileGeometry.IndexAt(_background, x, y));
        _list.SelectionChanged += (_, _) => Texture.Highlight(_list.SelectedIndex is >= 0 and var i ? TileGeometry.RectOf(_background, i) : null);
        Children.Add(new TileSetInfoView(background, workspace));
        Children.Add(Texture);
        Children.Add(_list);
        var find = new Button { Name = "FindTile", Content = Loc.Current["tileSet.findRooms"], IsEnabled = false };
        find.Click += (_, _) => { if (Selected >= 0 || _picked >= 0) FindRequested?.Invoke(_picked); };
        _findButton = find;
        Children.Add(find);
        AttachedToVisualTree += (_, _) => workspace.History.Changed += Reload;
        DetachedFromVisualTree += (_, _) => workspace.History.Changed -= Reload;
        Reload();
    }

    public TextureItemView Texture { get; }

    private readonly Button _findButton;
    private int _picked = -1;

    /// <summary>Raised with the picked tile index when the user asks which rooms use that tile.</summary>
    public event Action<int>? FindRequested;

    /// <summary>Outlines a tile index (as a click on the picture does) and selects its row when the tile set lists ids; -1 clears both.</summary>
    public void Pick(int index)
    {
        _picked = index;
        _findButton.IsEnabled = index >= 0;
        _list.SelectedIndex = index >= 0 && index < _background.GMS2TileIds.Count ? index : -1;
        // Many tile sets list no ids at all; the tile under the click is outlined either way.
        Texture.Highlight(index >= 0 ? TileGeometry.RectOf(_background, index) : null);
    }

    public int Selected => _list.SelectedIndex;

    private void Reload()
    {
        _list.IsVisible = _background.GMS2TileIds.Count > 0;
        _list.ItemsSource = _background.GMS2TileIds.Select((t, i) => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{i}: {t.ID}")).ToList();
    }
}
