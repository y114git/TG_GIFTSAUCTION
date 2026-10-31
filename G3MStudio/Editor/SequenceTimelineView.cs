using System.Collections;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using G3MLib.DataFile.Models;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

namespace G3MStudio.Editor;

/// <summary>
/// The keyframes of a sequence on a frame ruler, one row per track (nested tracks indented), the moments and the broadcast messages; a keyframe is
/// a bar from its key to its key plus its length. With a workspace a bar can be dragged to another frame, which is one undoable edit of its key.
/// Lengths, adding and removing keyframes and their values are edited in the property list of the track.
/// </summary>
public sealed class SequenceTimelineView : Control
{
    private const double LabelWidth = 170, RowHeight = 22, RulerHeight = 24, MaxFrames = 10_000_000;

    private readonly GameMakerSequence _sequence;
    private readonly Workspace? _workspace;
    private (int Row, int Frame, double Offset)? _drag;
    private double _pressedAt;

    public SequenceTimelineView(GameMakerSequence sequence, Workspace? workspace = null)
    {
        (_sequence, _workspace) = (sequence, workspace);
        Name = "SequenceTimeline";
        Rows = [];
        Build();
        ClipToBounds = true;
        if (workspace is null) return;
        AttachedToVisualTree += (_, _) => workspace.History.Changed += Refresh;
        DetachedFromVisualTree += (_, _) => workspace.History.Changed -= Refresh;
    }

    public IReadOnlyList<TimelineRow> Rows { get; private set; }

    /// <summary>The frame of the playhead, drawn as a line across the rows.</summary>
    public double Playhead
    {
        get;
        set
        {
            field = value;
            InvalidateVisual();
        }
    }

    /// <summary>Raised with the frame under a click on the ruler.</summary>
    public event Action<double>? PlayheadRequested;

    /// <summary>The length shown on the ruler, in frames.</summary>
    public double Length { get; private set; }

    private void Refresh()
    {
        Build();
        InvalidateVisual();
    }

    private void Build()
    {
        var rows = new List<TimelineRow>();
        if (Frames(_sequence.Moments) is { Count: > 0 } moments) rows.Add(new TimelineRow(0, Loc.Current["sequences.moments"], "Moments", moments));
        if (Frames(_sequence.BroadcastMessages) is { Count: > 0 } messages) rows.Add(new TimelineRow(0, Loc.Current["sequences.broadcast"], "BroadcastMessages", messages));
        var tracks = _sequence.Tracks ?? [];
        for (var i = 0; i < tracks.Count; i++) AddTrack(rows, tracks[i], 0, $"Tracks[{i}]");
        Rows = rows;
        var end = rows.SelectMany(r => r.Frames).Select(f => f.Key + f.Length).DefaultIfEmpty(0).Max();
        // The file is untrusted: a length that is not a finite number would make the ruler loop forever.
        var shown = _sequence.Length > 0 ? _sequence.Length : end;
        Length = double.IsFinite(shown) ? Math.Clamp(shown, 1, MaxFrames) : 1;
        Height = RulerHeight + (Math.Clamp(rows.Count, 1, 60) * RowHeight);
    }

    private static void AddTrack(List<TimelineRow> rows, GameMakerSequence.Track track, int depth, string path)
    {
        rows.Add(new TimelineRow(depth, track.Name?.Content ?? "", path + ".Keyframes.List", Frames(track.Keyframes is { } keyframes ? keyframes.GetType().GetProperty("List")?.GetValue(keyframes) : null)));
        var children = track.Tracks ?? [];
        for (var i = 0; i < children.Count; i++) AddTrack(rows, children[i], depth + 1, $"{path}.Tracks[{i}]");
    }

    /// <summary>The key, length and list index of every keyframe in a list of <c>Keyframe&lt;T&gt;</c>; the data type differs per track, so they are read by name.</summary>
    private static List<TimelineFrame> Frames(object? list)
    {
        var frames = new List<TimelineFrame>();
        if (list is not IEnumerable items) return frames;
        var index = -1;
        foreach (var item in items)
        {
            index++;
            if (item is null) continue;
            var type = item.GetType();
            if (type.GetProperty("Key")?.GetValue(item) is float key && float.IsFinite(key))
                frames.Add(new TimelineFrame(key, type.GetProperty("Length")?.GetValue(item) is float length && float.IsFinite(length) ? length : 0, index));
        }

        return frames;
    }

    /// <summary>A tick step of 1, 2, 5 or 10 times a power of ten that leaves at least 48 pixels between ticks, so a ruler never has more than a few dozen ticks.</summary>
    public static double Step(double pixelsPerFrame)
    {
        var wanted = 48 / Math.Max(pixelsPerFrame, 1e-9);
        if (wanted <= 1) return 1;
        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(wanted)));
        foreach (var factor in new[] { 1.0, 2, 5, 10 })
            if (factor * magnitude >= wanted) return factor * magnitude;
        return 10 * magnitude;
    }

    private double Scale => Math.Max(1, Bounds.Width - LabelWidth - 12) / Length;

    /// <summary>Moves a keyframe by whole frames (never before frame 0) as one undoable edit of its key. Returns false when nothing changed or the edit was refused.</summary>
    public bool Move(int row, int frame, int frames)
    {
        if (_workspace is null || row < 0 || row >= Rows.Count || frames == 0) return false;
        var target = Rows[row].Frames.FirstOrDefault(f => f.Index == frame);
        if (target is null) return false;
        var key = Math.Max(0, (int)Math.Round(target.Key) + frames);
        return _workspace.Edit(new SetPropertyCommand("sequences", _sequence.Name.Content ?? "", $"{Rows[row].Path}[{frame}].Key", key.ToString(CultureInfo.InvariantCulture)));
    }

    private (int Row, int Frame)? HitTest(Point at)
    {
        var scale = Scale;
        for (var i = 0; i < Rows.Count; i++)
        {
            var top = RulerHeight + (i * RowHeight);
            if (at.Y < top || at.Y >= top + RowHeight) continue;
            foreach (var frame in Rows[i].Frames)
                if (at.X >= LabelWidth + (frame.Key * scale) - 2 && at.X <= LabelWidth + (frame.Key * scale) + Math.Max(4, frame.Length * scale) + 2) return (i, frame.Index);
        }

        return null;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        var at = e.GetPosition(this);
        if (at.Y < RulerHeight && at.X >= LabelWidth)
        {
            PlayheadRequested?.Invoke(Math.Round((at.X - LabelWidth) / Scale));
            return;
        }

        if (_workspace is null) return;
        if (HitTest(at) is not var (row, frame)) return;
        _pressedAt = at.X;
        _drag = (row, frame, 0);
        e.Pointer.Capture(this);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var at = e.GetPosition(this);
        if (_drag is { } drag)
        {
            _drag = drag with { Offset = at.X - _pressedAt };
            InvalidateVisual();
        }

        Cursor = _workspace is not null && (_drag is not null || HitTest(at) is not null) ? new Cursor(StandardCursorType.SizeWestEast) : null;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_drag is not { } drag) return;
        _drag = null;
        e.Pointer.Capture(null);
        _ = Move(drag.Row, drag.Frame, (int)Math.Round(drag.Offset / Scale));
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        var accent = Application.Current is { } app && app.TryGetResource("Text.Accent", app.ActualThemeVariant, out var found) && found is IBrush brush ? brush : Brushes.Gray;
        var text = Brushes.Gray;
        context.FillRectangle(new SolidColorBrush(Color.FromRgb(30, 30, 34)), new Rect(Bounds.Size));
        var scale = Scale;
        static FormattedText Label(string value, IBrush color) => new(value, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Typeface.Default, 11, color);
        var grid = new Pen(new SolidColorBrush(Colors.White, 0.08), 1);
        for (var frame = 0.0; frame <= Length; frame += Step(scale))
        {
            var x = LabelWidth + (frame * scale);
            if (x > Bounds.Width) break;
            context.DrawLine(grid, new Point(x, RulerHeight - 4), new Point(x, Bounds.Height));
            context.DrawText(Label(frame.ToString("0", CultureInfo.InvariantCulture), text), new Point(x + 3, 4));
        }

        var playhead = LabelWidth + (Playhead * scale);
        context.DrawLine(new Pen(accent, 1.5), new Point(playhead, 0), new Point(playhead, Bounds.Height));
        for (var i = 0; i < Rows.Count && RulerHeight + ((i + 1) * RowHeight) <= Bounds.Height + RowHeight; i++)
        {
            var (row, top) = (Rows[i], RulerHeight + (i * RowHeight));
            context.DrawLine(grid, new Point(0, top), new Point(Bounds.Width, top));
            var label = Label(row.Name, Brushes.White);
            label.MaxTextWidth = Math.Max(1, LabelWidth - 12 - (row.Depth * 12));
            label.MaxLineCount = 1;
            context.DrawText(label, new Point(6 + (row.Depth * 12), top + 4));
            foreach (var frame in row.Frames)
            {
                // The bar being dragged follows the pointer, snapped to whole frames; the edit is made on release.
                var shift = _drag is { } drag && drag.Row == i && drag.Frame == frame.Index ? Math.Round(drag.Offset / scale) * scale : 0;
                var left = Math.Max(LabelWidth, LabelWidth + (frame.Key * scale) + shift);
                context.DrawRectangle(new SolidColorBrush(((ISolidColorBrush)accent).Color, 0.45), new Pen(accent, 1), new Rect(left, top + 4, Math.Max(4, frame.Length * scale), RowHeight - 8), 2, 2);
            }
        }
    }
}

/// <summary>One row of the timeline: a track, the moments or the broadcast messages, with the property path of its keyframe list.</summary>
public sealed record TimelineRow(int Depth, string Name, string Path, IReadOnlyList<TimelineFrame> Frames);

/// <summary>A keyframe: its key and length in frames and its index in the list (the index is kept when a bad keyframe is skipped).</summary>
public sealed record TimelineFrame(float Key, float Length, int Index);
