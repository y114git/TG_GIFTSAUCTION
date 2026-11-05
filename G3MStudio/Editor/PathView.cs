using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using G3MLib.DataFile.Models;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

namespace G3MStudio.Editor;
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

    /// <summary>Applies an edit that may be refused with a warning the owner can overrule; set by the window. Without it the edit is made plainly.</summary>
    public static Func<Workspace, Func<bool, EditCommand>, string, Task<bool>>? RiskyEdit { get; set; }

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
        if (e.Key != Key.Delete || _workspace is null || Selected < 0 || Selected >= _path.Points.Count) return;
        var (name, at) = (PathName, Selected);
        _ = RiskyEdit?.Invoke(_workspace, force => new PathPointsCommand(name, PathPointsCommand.Delete, at, force: force), name) ?? Task.FromResult(_workspace.Edit(new PathPointsCommand(name, PathPointsCommand.Delete, at)));
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
