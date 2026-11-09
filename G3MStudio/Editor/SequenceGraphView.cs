using System.Collections;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using G3MLib.DataFile.Models;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

namespace G3MStudio.Editor;

/// <summary>A track of a sequence whose keyframes hold numbers (position, scale, rotation...), with the path of the track inside the sequence.</summary>
public sealed record GraphTrack(string Name, string Path, IList Keyframes, int Channels);

/// <summary>One keyframe value on the graph: list index, frame and number.</summary>
public sealed record GraphPoint(int Index, float Key, float Value);

/// <summary>
/// The graph mode of the sequence timeline: the number tracks, one at a time, as a line of value over frame. A point can be dragged (one edit for the
/// value, one more when the frame changes) or typed into the boxes under the graph. The dope sheet in <see cref="SequenceTimelineView"/> stays the
/// place for keyframe lengths, adding, splitting and deleting.
/// </summary>
public sealed class SequenceGraphView : DockPanel
{
    private readonly GameMakerSequence _sequence;
    private readonly Workspace? _workspace;
    private readonly ComboBox _tracks = new() { Name = "GraphTrack", MinWidth = 180 };
    private readonly ComboBox _channels = new() { Name = "GraphChannel", MinWidth = 80 };
    private readonly NumericUpDown _frame = new() { Name = "GraphPointFrame", FormatString = "0.###", Minimum = 0, Maximum = 10_000_000, Increment = 1 };
    private readonly NumericUpDown _value = new() { Name = "GraphPointValue", FormatString = "0.###", Minimum = -1_000_000, Maximum = 1_000_000, Increment = 0.1M };
    private readonly ContentControl _curve = new() { Name = "GraphCurve" };
    private bool _building;

    public SequenceGraphView(GameMakerSequence sequence, Workspace? workspace)
    {
        (_sequence, _workspace) = (sequence, workspace);
        Name = "SequenceGraph";
        Surface = new GraphSurface(this);
        var top = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { _tracks, _channels } };
        SetDock(top, Avalonia.Controls.Dock.Top);
        var fields = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { new TextBlock { Text = Loc.Current["editors.sequence.graphFrame"], VerticalAlignment = VerticalAlignment.Center }, _frame, new TextBlock { Text = Loc.Current["editors.curve.value"], VerticalAlignment = VerticalAlignment.Center }, _value } };
        SetDock(fields, Avalonia.Controls.Dock.Bottom);
        SetDock(_curve, Avalonia.Controls.Dock.Bottom);
        Children.Add(top);
        Children.Add(_curve);
        Children.Add(fields);
        Children.Add(Surface);
        _tracks.SelectionChanged += (_, _) => Reset();
        _channels.SelectionChanged += (_, _) => Reset();
        _frame.ValueChanged += (_, _) => Type();
        _value.ValueChanged += (_, _) => Type();
        if (workspace is not null)
        {
            AttachedToVisualTree += (_, _) => workspace.History.Changed += Refresh;
            DetachedFromVisualTree += (_, _) => workspace.History.Changed -= Refresh;
        }

        Refresh();
    }

    internal GraphSurface Surface { get; }

    public IReadOnlyList<GraphTrack> Tracks { get; private set; } = [];

    public int Selected { get; set; } = -1;

    public GraphTrack? Current => Tracks.ElementAtOrDefault(Math.Max(0, _tracks.SelectedIndex));

    /// <summary>The number tracks of a sequence, nested ones included; a track counts when its keyframes carry a float value (the real-number tracks).</summary>
    public static List<GraphTrack> Collect(GameMakerSequence sequence)
    {
        var found = new List<GraphTrack>();
        Walk(sequence.Tracks, "", found);
        return found;
    }

    private static void Walk(IList<GameMakerSequence.Track>? tracks, string prefix, List<GraphTrack> found)
    {
        for (var i = 0; i < (tracks?.Count ?? 0); i++)
        {
            var track = tracks![i];
            var path = $"{prefix}Tracks[{i}]";
            if (track.Keyframes?.GetType().GetProperty("List")?.GetValue(track.Keyframes) is IList { Count: > 0 } list && Channels(list[0]) is { Count: > 0 } channels && channels[0] is { } first && ValueOf(first) is not null)
                found.Add(new GraphTrack(track.Name?.Content ?? path, path + ".Keyframes.List", list, channels.Count));
            Walk(track.Tracks, path + ".", found);
        }
    }

    private static IList? Channels(object? keyframe) => keyframe?.GetType().GetProperty("Channels")?.GetValue(keyframe) as IList;

    private static float? ValueOf(object channel) => channel.GetType().GetProperty("Value")?.GetValue(channel) is { } data ? data.GetType().GetProperty("Value")?.GetValue(data) as float? : null;

    private static float KeyOf(object keyframe) => keyframe.GetType().GetProperty("Key")?.GetValue(keyframe) as float? ?? 0;

    /// <summary>The points of one channel of a track, in list order.</summary>
    public static List<GraphPoint> Points(GraphTrack track, int channel)
    {
        var points = new List<GraphPoint>();
        for (var i = 0; i < track.Keyframes.Count; i++)
        {
            var key = track.Keyframes[i];
            if (key is not null && Channels(key)?.Cast<object>().ElementAtOrDefault(channel) is { } c && ValueOf(c) is { } value && float.IsFinite(value)) points.Add(new GraphPoint(i, KeyOf(key), value));
        }

        return points;
    }

    public int Channel => Math.Max(0, _channels.SelectedIndex);

    public List<GraphPoint> CurrentPoints => Current is { } track ? Points(track, Channel) : [];

    private void Reset()
    {
        if (_building) return;
        Selected = -1;
        Refresh();
    }

    private void Refresh()
    {
        _building = true;
        var wasTrack = _tracks.SelectedIndex;
        Tracks = Collect(_sequence);
        _tracks.ItemsSource = Tracks.Select(t => t.Name).ToList();
        _tracks.SelectedIndex = Tracks.Count == 0 ? -1 : Math.Clamp(wasTrack, 0, Tracks.Count - 1);
        var wasChannel = _channels.SelectedIndex;
        _channels.ItemsSource = Enumerable.Range(0, Current?.Channels ?? 0).Select(c => c.ToString(CultureInfo.InvariantCulture)).ToList();
        _channels.SelectedIndex = Current is null ? -1 : Math.Clamp(wasChannel, 0, Current.Channels - 1);
        var point = CurrentPoints.FirstOrDefault(p => p.Index == Selected);
        var has = point is not null && Selected >= 0;
        (_frame.IsEnabled, _value.IsEnabled) = (has && _workspace is not null, has && _workspace is not null);
        _frame.Value = point is not null && has ? (decimal)point.Key : null;
        _value.Value = point is not null && has ? (decimal)point.Value : null;
        _curve.Content = point is not null && has ? CurveOf(point.Index) : null;
        _building = false;
        Surface.InvalidateVisual();
    }

    /// <summary>A keyframe stays between its neighbours in the list, because the list must stay sorted by frame.</summary>
    public static float ClampKey(List<GraphPoint> points, int index, float key)
    {
        var low = points.Where(p => p.Index < index).Select(p => p.Key).DefaultIfEmpty(0).Max();
        var high = points.Where(p => p.Index > index).Select(p => p.Key).DefaultIfEmpty(float.MaxValue).Min();
        return Math.Clamp(key, low, high);
    }

    // The curve a keyframe eases along: an embedded one opens as a property list of its points, a linked one is named.
    private Control? CurveOf(int index)
    {
        if (_workspace is null || Current is null || Current.Keyframes[index] is not { } key || Channels(key)?.Cast<object>().ElementAtOrDefault(Channel) is not { } channel) return null;
        var data = channel.GetType().GetProperty("Value")?.GetValue(channel);
        if (data?.GetType().GetProperty("EmbeddedAnimCurve")?.GetValue(data) is GameMakerAnimationCurve && data.GetType().GetProperty("IsCurveEmbedded")?.GetValue(data) is true)
            return ParticleEmittersView.Expandable(Loc.Current["editors.sequence.keyCurve"], _workspace, "sequences", _sequence.Name.Content ?? "", PointPath(index, $"Channels[{Channel}].Value.EmbeddedAnimCurve"));
        return data?.GetType().GetProperty("AssetAnimCurve")?.GetValue(data) is { } asset && asset.GetType().GetProperty("Resource")?.GetValue(asset) is GameMakerAnimationCurve linked
            ? new TextBlock { Text = Loc.Current.Format("editors.sequence.linkedCurve", ("name", linked.Name.Content ?? "")), Classes = { "muted" } }
            : null;
    }

    private string PointPath(int index, string field) => $"{Current?.Path}[{index}].{field}";

    /// <summary>Sets the frame and number of a keyframe, each as one undoable edit; nothing is written for a part that did not change.</summary>
    public bool Commit(int index, float key, float value)
    {
        if (_workspace is null || Current is null || CurrentPoints.FirstOrDefault(p => p.Index == index) is not { } old) return false;
        var name = _sequence.Name.Content ?? "";
        key = ClampKey(CurrentPoints, index, key);
        static string Text(float v) => v.ToString("R", CultureInfo.InvariantCulture);
        if (old.Value != value) _workspace.Edit(new SetPropertyCommand("sequences", name, PointPath(index, $"Channels[{Channel}].Value.Value"), Text(value)));
        if (old.Key != key) _workspace.Edit(new SetPropertyCommand("sequences", name, PointPath(index, "Key"), Text(key)));
        return true;
    }

    private void Type()
    {
        if (_building || Selected < 0 || _frame.Value is not { } frame || _value.Value is not { } value) return;
        Commit(Selected, (float)frame, (float)value);
    }

    internal sealed class GraphSurface : Control
    {
        private const double Pad = 36;
        private readonly SequenceGraphView _owner;
        private bool _dragging;
        private Point _start;

        public GraphSurface(SequenceGraphView owner)
        {
            _owner = owner;
            Height = 240;
            MinWidth = 300;
        }

        private static (float MinX, float MaxX, float MinY, float MaxY) Range(List<GraphPoint> points)
        {
            var (minX, maxX) = (points.Min(p => p.Key), points.Max(p => p.Key));
            var (minY, maxY) = (points.Min(p => p.Value), points.Max(p => p.Value));
            return (minX, maxX - minX < 1e-6f ? minX + 1 : maxX, minY, maxY - minY < 1e-6f ? minY + 1 : maxY);
        }

        private Point Map(GraphPoint p, (float MinX, float MaxX, float MinY, float MaxY) r) =>
            new(Pad + ((p.Key - r.MinX) / (r.MaxX - r.MinX) * Math.Max(1, Bounds.Width - (2 * Pad))), Bounds.Height - Pad - ((p.Value - r.MinY) / (r.MaxY - r.MinY) * Math.Max(1, Bounds.Height - (2 * Pad))));

        public override void Render(DrawingContext context)
        {
            context.FillRectangle(G3MStudio.Theme.CanvasTheme.Bg, new Rect(Bounds.Size));
            var points = _owner.CurrentPoints;
            if (points.Count == 0) return;
            var range = Range(points);
            var line = new Pen(G3MStudio.Theme.CanvasTheme.Line, 2);
            var ordered = points.OrderBy(p => p.Key).ToList();
            for (var i = 1; i < ordered.Count; i++) context.DrawLine(line, Map(ordered[i - 1], range), Map(ordered[i], range));
            foreach (var p in points) context.DrawEllipse(p.Index == _owner.Selected ? G3MStudio.Theme.CanvasTheme.Point : G3MStudio.Theme.CanvasTheme.Line, null, Map(p, range), 4, 4);
        }

        private int Hit(Point at)
        {
            var points = _owner.CurrentPoints;
            if (points.Count == 0) return -1;
            var range = Range(points);
            return points.Where(p => Math.Abs(Map(p, range).X - at.X) < 8 && Math.Abs(Map(p, range).Y - at.Y) < 8).Select(p => p.Index).DefaultIfEmpty(-1).First();
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            _start = e.GetPosition(this);
            _owner.Selected = Hit(_start);
            _dragging = _owner.Selected >= 0 && _owner._workspace is not null;
            if (_dragging) e.Pointer.Capture(this);
            _owner.Refresh();
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            if (!_dragging) return;
            _dragging = false;
            e.Pointer.Capture(null);
            if (Math.Abs(e.GetPosition(this).X - _start.X) < 3 && Math.Abs(e.GetPosition(this).Y - _start.Y) < 3) return; // a click only selects
            var points = _owner.CurrentPoints;
            if (points.FirstOrDefault(p => p.Index == _owner.Selected) is not { } picked) return;
            var (minX, maxX, minY, maxY) = Range(points);
            var at = e.GetPosition(this);
            var width = Math.Max(1, Bounds.Width - (2 * Pad));
            var height = Math.Max(1, Bounds.Height - (2 * Pad));
            var key = (float)Math.Max(0, Math.Round(minX + ((at.X - Pad) / width * (maxX - minX))));
            var value = (float)(minY + ((Bounds.Height - Pad - at.Y) / height * (maxY - minY)));
            _owner.Commit(picked.Index, key, (float)Math.Round(value, 3));
        }
    }
}
