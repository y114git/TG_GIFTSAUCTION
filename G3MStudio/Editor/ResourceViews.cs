using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using G3MLib.DataFile.Models;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;
using G3MStudio.Rooms;

namespace G3MStudio.Editor;

/// <summary>A texture page item (a tile set, a font sheet or an embedded image) on a dark chequer, with a zoom of 1x to 8x; it opens at the largest whole zoom that fits.</summary>
public sealed class TextureItemView : DockPanel
{
    private readonly Image _image = new() { Stretch = Stretch.None };
    private readonly TextBlock _caption = new() { Classes = { "muted" } };
    private readonly Slider _zoomBar = new() { Minimum = 1, Maximum = 8, Value = 1, Width = 160, TickFrequency = 1, IsSnapToTickEnabled = true };

    public TextureItemView(GameMakerTexturePageItem? item, TextureBitmaps textures)
    {
        Name = "TextureItemView";
        Height = 300;
        RenderOptions.SetBitmapInterpolationMode(_image, BitmapInterpolationMode.None);
        _zoomBar.ValueChanged += (_, e) => Zoom = e.NewValue;
        var bar = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 8, Children = { _zoomBar, _caption } };
        SetDock(bar, Avalonia.Controls.Dock.Top);
        Children.Add(bar);
        Children.Add(new ScrollViewer { HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, Background = new SolidColorBrush(Color.FromRgb(40, 40, 46)), Content = _image });
        Ready = ShowAsync(item, textures);
    }

    public Task Ready { get; }

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
    }

    private async Task ShowAsync(GameMakerTexturePageItem? item, TextureBitmaps textures)
    {
        if (item is not { TexturePage: { } page, SourceWidth: > 0, SourceHeight: > 0 } || await textures.GetAsync(page) is not { } bitmap)
        {
            _caption.Text = "-";
            return;
        }

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
        context.FillRectangle(new SolidColorBrush(Color.FromRgb(30, 30, 34)), new Rect(Bounds.Size));
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

        context.DrawGeometry(null, new Pen(Brushes.DeepSkyBlue, 2), line);
        for (var i = 0; i < points.Count; i++) context.DrawEllipse(i == Selected ? Brushes.White : Brushes.Orange, null, Map(points[i]), i == Selected ? 5 : 3, i == Selected ? 5 : 3);
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
        var side = new StackPanel { Width = 190, Margin = new Thickness(0, 0, 8, 0), Spacing = 4, Children = { _points, Labelled("X", _x), Labelled("Y", _y), Labelled(Loc.Current["paths.speed"], _speed), new Separator(), new TextBlock { Text = Loc.Current["paths.moveAll"], Classes = { "muted" } }, Labelled("X", _shiftX), Labelled("Y", _shiftY), moveAll } };
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

    private void Show()
    {
        _building = true;
        var point = View.Selected >= 0 && View.Selected < _path.Points.Count ? _path.Points[View.Selected] : null;
        (_x.IsEnabled, _y.IsEnabled) = (point is not null && _workspace is not null, point is not null && _workspace is not null);
        _x.Value = point is null ? null : (decimal)Math.Round(point.X);
        _y.Value = point is null ? null : (decimal)Math.Round(point.Y);
        _speed.IsEnabled = point is not null && _workspace is not null;
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
    private readonly ListBox _list = new() { Name = "FontGlyphs", MaxHeight = 220 };
    private readonly TextBlock _count = new() { Classes = { "muted" } };
    private readonly ContentControl _glyph = new() { Name = "GlyphProperties", MaxHeight = 260 };
    private readonly List<(ushort Character, string Text, int Index)> _rows = [];
    private readonly List<int> _shown = [];

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
            _list.SelectionChanged += (_, _) =>
                _glyph.Content = _list.SelectedIndex is >= 0 and var row && row < _shown.Count ? new PropertyGridTab(workspace, "fonts", font.Name.Content ?? "", null, $"Glyphs[{_shown[row]}]") : null;
            _newCharacter.PlaceholderText = Loc.Current["fonts.glyphNew"];
            var add = new Button { Name = "GlyphAdd", Content = Loc.Current["fonts.glyphAdd"] };
            add.Click += (_, _) => Add();
            var remove = new Button { Name = "GlyphRemove", Content = Loc.Current["timelines.remove"] };
            remove.Click += (_, _) =>
            {
                if (_list.SelectedIndex is >= 0 and var row && row < _shown.Count) workspace.Edit(new FontGlyphCommand(font.Name.Content ?? "", FontGlyphCommand.Remove, _shown[row]));
            };
            Children.Add(new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 6, Children = { _newCharacter, add, remove } });
            AttachedToVisualTree += (_, _) => workspace.History.Changed += Reload;
            DetachedFromVisualTree += (_, _) => workspace.History.Changed -= Reload;
        }

        Reload();
    }

    public int Shown => _list.ItemCount;

    /// <summary>The character a code such as <c>U+0041</c>, a number or a single character stands for, or null.</summary>
    internal static int? ParseCharacter(string text)
    {
        text = text.Trim();
        if (text.StartsWith("U+", StringComparison.OrdinalIgnoreCase) && int.TryParse(text[2..], System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var hex)) return hex;
        if (text.Length == 1) return text[0];
        return int.TryParse(text, System.Globalization.CultureInfo.InvariantCulture, out var number) ? number : null;
    }

    private void Add()
    {
        if (_workspace is null || ParseCharacter(_newCharacter.Text ?? "") is not { } code || code is < 0 or > ushort.MaxValue) return;
        if (_font.Glyphs.Any(g => g.Character == code)) return;
        _workspace.Edit(new FontGlyphCommand(_font.Name.Content ?? "", FontGlyphCommand.Add, -1, (ushort)code));
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
            var header = new DockPanel { Children = { new TextBlock { Text = line, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center }, remove } };
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
        context.FillRectangle(new SolidColorBrush(Color.FromRgb(30, 30, 34)), new Rect(Bounds.Size));
        var all = _curve.Channels.SelectMany(c => c.Points).ToList();
        if (all.Count == 0) return;
        var (minX, maxX) = (all.Min(p => p.X), all.Max(p => p.X));
        var (minY, maxY) = (all.Min(p => p.Value), all.Max(p => p.Value));
        double Span(float low, float high) => high - low < 1e-6 ? 1 : high - low;
        var (spanX, spanY) = (Span(minX, maxX), Span(minY, maxY));
        Point Map(float x, float y) => new(24 + (x - minX) / spanX * Math.Max(1, Bounds.Width - 48), Bounds.Height - 24 - (y - minY) / spanY * Math.Max(1, Bounds.Height - 48));
        var axis = new Pen(new SolidColorBrush(Avalonia.Media.Colors.White, 0.25), 1);
        context.DrawLine(axis, Map(minX, minY), Map(maxX, minY));
        context.DrawLine(axis, Map(minX, minY), Map(minX, maxY));
        for (var c = 0; c < _curve.Channels.Count; c++)
        {
            var brush = Colors[c % Colors.Length];
            var points = _curve.Channels[c].Points.OrderBy(p => p.X).ToList();
            for (var i = 1; i < points.Count; i++) context.DrawLine(new Pen(brush, 2), Map(points[i - 1].X, points[i - 1].Value), Map(points[i].X, points[i].Value));
            foreach (var p in points) context.DrawEllipse(brush, null, Map(p.X, p.Value), 3, 3);
            var label = new FormattedText(_curve.Channels[c].Name?.Content ?? "", System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Typeface.Default, 12, brush);
            context.DrawText(label, new Point(28 + c * 110, 4));
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
            Items.Add(new TabItem { Header = title, Content = page, FontSize = 13 });
        }
    }

    /// <summary>The shader fields these tabs edit; the property list leaves them out so a source is not shown twice.</summary>
    public static readonly string[] SourceProperties = [nameof(GameMakerShader.GLSL_ES_Vertex), nameof(GameMakerShader.GLSL_ES_Fragment), nameof(GameMakerShader.GLSL_Vertex), nameof(GameMakerShader.GLSL_Fragment), nameof(GameMakerShader.HLSL9_Vertex), nameof(GameMakerShader.HLSL9_Fragment)];

    protected override Type StyleKeyOverride => typeof(TabControl);

    public IReadOnlyList<(string Title, string Text)> Sources { get; }
}
