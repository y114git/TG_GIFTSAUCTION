using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using G3MLib.DataFile.Models;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;
using G3MStudio.Theme;

namespace G3MStudio.Editor;

/// <summary>
/// One channel of an animation curve as a graph: click empty space to add a point, drag a point to move it, drag a square handle (Bezier channels)
/// to bend the segment, Delete removes the picked point. Each finished drag is one undoable edit.
/// </summary>
public sealed class CurveGraph : Control
{
    private enum Grab { None, Point, HandleIn, HandleOut }

    private const double Pad = 36;

    private readonly GameMakerAnimationCurve _curve;
    private readonly Workspace? _workspace;
    private Grab _grab;
    private Point _to, _start;

    public CurveGraph(GameMakerAnimationCurve curve, Workspace? workspace)
    {
        (_curve, _workspace) = (curve, workspace);
        Name = "CurveGraph";
        Height = 280;
        Focusable = workspace is not null;
    }

    public int Channel { get; set { field = value; Selected = -1; InvalidateVisual(); } }

    public int Selected { get; set { field = value; SelectionChanged?.Invoke(); InvalidateVisual(); } } = -1;

    public event Action? SelectionChanged;

    private GameMakerAnimationCurve.Channel? Current => _curve.Channels.ElementAtOrDefault(Channel);

    private string CurveName => _curve.Name.Content ?? "";

    private bool IsBezier => Current?.Curve == GameMakerAnimationCurve.Channel.CurveType.Bezier;

    /// <summary>The visible range: the points with a margin, always at least 0 to 1 so an empty or flat channel has a frame.</summary>
    public (double MinX, double MaxX, double MinY, double MaxY) Range()
    {
        var points = Current?.Points ?? [];
        double minX = Math.Min(0, points.Select(p => (double)p.X).DefaultIfEmpty(0).Min()), maxX = Math.Max(1, points.Select(p => (double)p.X).DefaultIfEmpty(1).Max());
        double minY = Math.Min(0, points.Select(p => (double)p.Value).DefaultIfEmpty(0).Min()), maxY = Math.Max(1, points.Select(p => (double)p.Value).DefaultIfEmpty(1).Max());
        return (minX, maxX, minY, maxY);
    }

    public Point ToScreen(double x, double y)
    {
        var (minX, maxX, minY, maxY) = Range();
        return new Point(Pad + ((x - minX) / (maxX - minX) * Math.Max(1, Bounds.Width - (2 * Pad))), Bounds.Height - Pad - ((y - minY) / (maxY - minY) * Math.Max(1, Bounds.Height - (2 * Pad))));
    }

    public Point ToCurve(Point screen)
    {
        var (minX, maxX, minY, maxY) = Range();
        return new Point(minX + ((screen.X - Pad) / Math.Max(1, Bounds.Width - (2 * Pad)) * (maxX - minX)), minY + ((Bounds.Height - Pad - screen.Y) / Math.Max(1, Bounds.Height - (2 * Pad)) * (maxY - minY)));
    }

    private Point PointScreen(int i) => Current is { } c ? ToScreen(c.Points[i].X, c.Points[i].Value) : default;

    private Point HandleScreen(int i, bool outgoing)
    {
        if (Current is not { } c) return default;
        var p = c.Points[i];
        var at = PointScreen(i);
        var (hx, hy) = outgoing ? (p.BezierX1, p.BezierY1) : (p.BezierX0, p.BezierY0);
        var (minX, maxX, minY, maxY) = Range();
        var reach = new Vector(hx / (maxX - minX) * Math.Max(1, Bounds.Width - (2 * Pad)), -hy / (maxY - minY) * Math.Max(1, Bounds.Height - (2 * Pad)));
        return at + reach;
    }

    private static bool Near(Point a, Point b) => (a - b).X is var dx && (a - b).Y is var dy && (dx * dx) + (dy * dy) <= 81;

    private (Grab, int) Hit(Point at)
    {
        for (var i = (Current?.Points.Count ?? 0) - 1; i >= 0; i--)
        {
            if (IsBezier && i == Selected && Near(HandleScreen(i, false), at)) return (Grab.HandleIn, i);
            if (IsBezier && i == Selected && Near(HandleScreen(i, true), at)) return (Grab.HandleOut, i);
            if (Near(PointScreen(i), at)) return (Grab.Point, i);
        }

        return (Grab.None, -1);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (_workspace is null || Current is null || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        Focus();
        var at = e.GetPosition(this);
        var (grab, hit) = Hit(at);
        if (grab == Grab.None)
        {
            var added = ToCurve(at);
            _workspace.Edit(new CurvePointCommand(CurveName, Channel, CurvePointCommand.Add, -1, new CurveValue((float)Math.Round(added.X, 3), (float)Math.Round(added.Y, 3))));
            Selected = CurveOrder.SortedIndex(Current.Points.Select(p => p.X), (float)Math.Round(added.X, 3)) - 1;
        }
        else
        {
            (_grab, Selected, _to, _start) = (grab, hit, at, at);
            e.Pointer.Capture(this);
        }

        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_grab == Grab.None) return;
        _to = e.GetPosition(this);
        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_grab == Grab.None || _workspace is null || Current is null || Selected < 0 || Selected >= Current.Points.Count) return;
        var (grab, point, target) = (_grab, Current.Points[Selected], ToCurve(e.GetPosition(this)));
        _grab = Grab.None;
        e.Pointer.Capture(null);
        if (Math.Abs(e.GetPosition(this).X - _start.X) < 3 && Math.Abs(e.GetPosition(this).Y - _start.Y) < 3)
        {
            InvalidateVisual(); // a click only selects
            return;
        }

        if (grab == Grab.Point)
        {
            _workspace.Edit(new CurvePointCommand(CurveName, Channel, CurvePointCommand.Move, Selected, new CurveValue((float)Math.Round(target.X, 3), (float)Math.Round(target.Y, 3))));
            Selected = CurveOrder.SortedIndex(Current.Points.Select(p => p.X), (float)Math.Round(target.X, 3)) - 1;
        }
        else
        {
            var h = new[] { point.BezierX0, point.BezierY0, point.BezierX1, point.BezierY1 };
            var offset = (float)(target.X - point.X);
            var rise = (float)(target.Y - point.Value);
            if (grab == Grab.HandleIn) (h[0], h[1]) = (offset, rise);
            else (h[2], h[3]) = (offset, rise);
            _workspace.Edit(new CurvePointCommand(CurveName, Channel, CurvePointCommand.Handles, Selected, new CurveValue(Handles: h)));
        }

        InvalidateVisual();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key != Key.Delete || _workspace is null || Current is null || Selected < 0 || Selected >= Current.Points.Count) return;
        _workspace.Edit(new CurvePointCommand(CurveName, Channel, CurvePointCommand.Delete, Selected));
        Selected = -1;
    }

    public override void Render(DrawingContext context)
    {
        context.FillRectangle(CanvasTheme.Bg, new Rect(Bounds.Size));
        if (Current is not { } channel) return;
        var (minX, maxX, minY, maxY) = Range();
        var grid = new Pen(CanvasTheme.Soft(CanvasTheme.Handle, 0.1), 1);
        for (var q = 0; q <= 4; q++)
        {
            context.DrawLine(grid, ToScreen(minX, minY + ((maxY - minY) * q / 4)), ToScreen(maxX, minY + ((maxY - minY) * q / 4)));
            context.DrawLine(grid, ToScreen(minX + ((maxX - minX) * q / 4), minY), ToScreen(minX + ((maxX - minX) * q / 4), maxY));
        }

        var shown = channel.Points.Select((p, i) => i == Selected && _grab == Grab.Point ? ToCurve(_to) : new Point(p.X, p.Value)).ToList();
        var line = new Pen(CanvasTheme.Line, 2);
        for (var i = 1; i < shown.Count; i++) context.DrawLine(line, ToScreen(shown[i - 1].X, shown[i - 1].Y), ToScreen(shown[i].X, shown[i].Y));
        for (var i = 0; i < shown.Count; i++)
        {
            var at = ToScreen(shown[i].X, shown[i].Y);
            context.DrawEllipse(i == Selected ? CanvasTheme.Handle : CanvasTheme.Point, null, at, i == Selected ? 5 : 4, i == Selected ? 5 : 4);
            if (!IsBezier || i != Selected) continue;
            foreach (var outgoing in new[] { false, true })
            {
                var handle = HandleScreen(i, outgoing);
                context.DrawLine(grid, at, handle);
                context.DrawRectangle(CanvasTheme.Handle, new Pen(Brushes.Black, 1), new Rect(handle.X - 4, handle.Y - 4, 8, 8));
            }
        }
    }
}

/// <summary>The animation curve editor: a channel list and curve type, the graph and a table of the points of the channel with typed X and Value.</summary>
public sealed class CurveEditor : DockPanel
{
    private static readonly string[] TypeKeys = ["editors.curve.linear", "editors.curve.smooth", "editors.curve.bezier"];

    private readonly GameMakerAnimationCurve _curve;
    private readonly Workspace? _workspace;
    private readonly ComboBox _channels = new() { Name = "CurveChannel", MinWidth = 140 };
    private readonly ComboBox _type = new() { Name = "CurveType", MinWidth = 120 };
    private readonly ListBox _points = new() { Name = "CurvePoints", MinWidth = 160, MaxHeight = 220 };
    private readonly NumericUpDown _x = new() { Name = "CurvePointX", FormatString = "0.###", Minimum = -1000000, Maximum = 1000000, Increment = 0.1M };
    private readonly NumericUpDown _value = new() { Name = "CurvePointValue", FormatString = "0.###", Minimum = -1000000, Maximum = 1000000, Increment = 0.1M };
    private bool _building;

    // Same order as CurvePresets.Names.
    private static readonly string[] PresetKeys = ["editors.curve.presetLinear", "editors.curve.presetEaseIn", "editors.curve.presetEaseOut", "editors.curve.presetEaseInOut", "editors.curve.presetStep", "editors.curve.presetBounce", "editors.curve.presetBezierEaseIn", "editors.curve.presetBezierEaseOut", "editors.curve.presetBezierEaseInOut"];

    public CurveEditor(GameMakerAnimationCurve curve, Workspace? workspace)
    {
        (_curve, _workspace) = (curve, workspace);
        Name = "CurveEditor";
        Graph = new CurveGraph(curve, workspace);
        _type.ItemsSource = TypeKeys.Select(k => Loc.Current[k]).ToList();
        var top = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new Thickness(0, 0, 0, 6), Children = { new TextBlock { Text = Loc.Current["editors.curve.channel"], VerticalAlignment = VerticalAlignment.Center }, _channels, _type } };
        SetDock(top, Avalonia.Controls.Dock.Top);
        Children.Add(top);
        var add = new Button { Name = "CurvePointAdd", Content = Loc.Current["editors.curve.add"], IsEnabled = workspace is not null };
        var delete = new Button { Name = "CurvePointDelete", Content = Loc.Current["editors.curve.delete"], IsEnabled = workspace is not null };
        var presets = new ComboBox { Name = "CurvePreset", MinWidth = 140, IsEnabled = workspace is not null, PlaceholderText = Loc.Current["editors.curve.preset"], ItemsSource = PresetKeys.Select(k => Loc.Current[k]).ToList() };
        presets.SelectionChanged += (_, _) =>
        {
            if (presets.SelectedIndex < 0 || _building || _workspace is null || Current is null) return;
            _workspace.Edit(new CurvePointCommand(CurveName, Graph.Channel, CurvePointCommand.Preset, 0, new CurveValue(presets.SelectedIndex)));
            presets.SelectedIndex = -1;
        };
        top.Children.Add(presets);
        var side = new StackPanel { Width = 200, Spacing = 4, Margin = new Thickness(0, 0, 8, 0), Children = { _points, Labelled("X", _x), Labelled(Loc.Current["editors.curve.value"], _value), new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { add, delete } } } };
        SetDock(side, Avalonia.Controls.Dock.Left);
        Children.Add(side);
        Children.Add(Graph);
        _channels.SelectionChanged += (_, _) =>
        {
            if (_building) return; // Refresh sets the list itself, which raises this again
            Graph.Channel = Math.Max(0, _channels.SelectedIndex);
            Refresh();
        };
        _type.SelectionChanged += (_, _) => ChangeType();
        _points.SelectionChanged += (_, _) =>
        {
            if (!_building) Graph.Selected = _points.SelectedIndex;
        };
        Graph.SelectionChanged += Refresh;
        _x.ValueChanged += (_, _) => Move();
        _value.ValueChanged += (_, _) => Move();
        add.Click += (_, _) => AddPoint();
        delete.Click += (_, _) => Delete();
        if (workspace is not null)
        {
            AttachedToVisualTree += (_, _) => workspace.History.Changed += Refresh;
            DetachedFromVisualTree += (_, _) => workspace.History.Changed -= Refresh;
        }

        Refresh();
    }

    public CurveGraph Graph { get; }

    private string CurveName => _curve.Name.Content ?? "";

    private static DockPanel Labelled(string text, Control box)
    {
        var label = new TextBlock { Text = text, MinWidth = 60, VerticalAlignment = VerticalAlignment.Center };
        DockPanel.SetDock(label, Avalonia.Controls.Dock.Left);
        return new DockPanel { Children = { label, box } };
    }

    private GameMakerAnimationCurve.Channel? Current => _curve.Channels.ElementAtOrDefault(Graph.Channel);

    private void AddPoint()
    {
        if (_workspace is null || Current is null) return;
        var last = Current.Points.LastOrDefault();
        _workspace.Edit(new CurvePointCommand(CurveName, Graph.Channel, CurvePointCommand.Add, -1, new CurveValue((last?.X ?? 0) + 1, last?.Value ?? 0)));
        Graph.Selected = Current.Points.Count - 1;
    }

    private void Delete()
    {
        if (_workspace is null || Current is null || Graph.Selected < 0 || Graph.Selected >= Current.Points.Count) return;
        _workspace.Edit(new CurvePointCommand(CurveName, Graph.Channel, CurvePointCommand.Delete, Graph.Selected));
        Graph.Selected = -1;
    }

    private void ChangeType()
    {
        if (_building || _workspace is null || Current is null || _type.SelectedIndex < 0 || (int)Current.Curve == _type.SelectedIndex) return;
        _workspace.Edit(new CurvePointCommand(CurveName, Graph.Channel, CurvePointCommand.Type, 0, new CurveValue(_type.SelectedIndex)));
    }

    private void Move()
    {
        if (_building || _workspace is null || Current is null || Graph.Selected < 0 || Graph.Selected >= Current.Points.Count || _x.Value is not { } x || _value.Value is not { } v) return;
        var p = Current.Points[Graph.Selected];
        if (p.X == (float)x && p.Value == (float)v) return;
        _workspace.Edit(new CurvePointCommand(CurveName, Graph.Channel, CurvePointCommand.Move, Graph.Selected, new CurveValue((float)x, (float)v)));
        Graph.Selected = CurveOrder.SortedIndex(Current.Points.Select(q => q.X), (float)x) - 1;
    }

    private void Refresh()
    {
        _building = true;
        _channels.ItemsSource = _curve.Channels.Select((c, i) => c.Name?.Content ?? i.ToString(CultureInfo.InvariantCulture)).ToList();
        _channels.SelectedIndex = Math.Min(Graph.Channel, _curve.Channels.Count - 1);
        _type.SelectedIndex = Current is null ? -1 : (int)Math.Min((uint)Current.Curve, 2u);
        _type.IsEnabled = _workspace is not null && Current is not null;
        _points.ItemsSource = (Current?.Points ?? []).Select((p, i) => string.Create(CultureInfo.InvariantCulture, $"{i + 1}  {p.X:0.###}, {p.Value:0.###}")).ToList();
        var selected = Current is { } c && Graph.Selected >= 0 && Graph.Selected < c.Points.Count ? c.Points[Graph.Selected] : null;
        _points.SelectedIndex = selected is null ? -1 : Graph.Selected;
        (_x.IsEnabled, _value.IsEnabled) = (selected is not null && _workspace is not null, selected is not null && _workspace is not null);
        _x.Value = selected is null ? null : (decimal)selected.X;
        _value.Value = selected is null ? null : (decimal)selected.Value;
        _building = false;
        Graph.InvalidateVisual();
    }
}
