using System.Collections;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using G3MLib.DataFile.Models;
using G3MStudio.Localization;

namespace G3MStudio.Editor;

/// <summary>
/// The keyframes of a sequence on a frame ruler, one row per track (nested tracks indented), the moments and the broadcast messages. Read-only:
/// a keyframe is a bar from its key to its key plus its length. Keyframes are edited in the property list of their track.
/// </summary>
public sealed class SequenceTimelineView : Control
{
    private const double LabelWidth = 170, RowHeight = 22, RulerHeight = 24, MaxFrames = 10_000_000;

    public SequenceTimelineView(GameMakerSequence sequence)
    {
        Name = "SequenceTimeline";
        var rows = new List<TimelineRow>();
        if (Frames(sequence.Moments) is { Count: > 0 } moments) rows.Add(new TimelineRow(0, Loc.Current["sequences.moments"], moments));
        if (Frames(sequence.BroadcastMessages) is { Count: > 0 } messages) rows.Add(new TimelineRow(0, Loc.Current["sequences.broadcast"], messages));
        foreach (var track in sequence.Tracks ?? []) AddTrack(rows, track, 0);
        Rows = rows;
        var end = rows.SelectMany(r => r.Frames).Select(f => f.Key + f.Length).DefaultIfEmpty(0).Max();
        // The file is untrusted: a length that is not a finite number would make the ruler loop forever.
        var shown = sequence.Length > 0 ? sequence.Length : end;
        Length = double.IsFinite(shown) ? Math.Clamp(shown, 1, MaxFrames) : 1;
        Height = RulerHeight + Math.Clamp(rows.Count, 1, 60) * RowHeight;
        ClipToBounds = true;
    }

    public IReadOnlyList<TimelineRow> Rows { get; }

    /// <summary>The length shown on the ruler, in frames.</summary>
    public double Length { get; }

    private static void AddTrack(List<TimelineRow> rows, GameMakerSequence.Track track, int depth)
    {
        rows.Add(new TimelineRow(depth, track.Name?.Content ?? "", Frames(track.Keyframes is { } keyframes ? keyframes.GetType().GetProperty("List")?.GetValue(keyframes) : null)));
        foreach (var child in track.Tracks ?? []) AddTrack(rows, child, depth + 1);
    }

    /// <summary>The key and length of every keyframe in a list of <c>Keyframe&lt;T&gt;</c>; the data type differs per track, so they are read by name.</summary>
    private static List<(float Key, float Length)> Frames(object? list)
    {
        var frames = new List<(float, float)>();
        if (list is not IEnumerable items) return frames;
        foreach (var item in items)
        {
            if (item is null) continue;
            var type = item.GetType();
            if (type.GetProperty("Key")?.GetValue(item) is float key && float.IsFinite(key))
                frames.Add((key, type.GetProperty("Length")?.GetValue(item) is float length && float.IsFinite(length) ? length : 0));
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

    public override void Render(DrawingContext context)
    {
        var accent = Application.Current is { } app && app.TryGetResource("Text.Accent", app.ActualThemeVariant, out var found) && found is IBrush brush ? brush : Brushes.Gray;
        var text = Brushes.Gray;
        context.FillRectangle(new SolidColorBrush(Color.FromRgb(30, 30, 34)), new Rect(Bounds.Size));
        var width = Math.Max(1, Bounds.Width - LabelWidth - 12);
        var scale = width / Length;
        static FormattedText Label(string value, IBrush color) => new(value, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Typeface.Default, 11, color);
        var grid = new Pen(new SolidColorBrush(Colors.White, 0.08), 1);
        for (var frame = 0.0; frame <= Length; frame += Step(scale))
        {
            var x = LabelWidth + frame * scale;
            if (x > Bounds.Width) break;
            context.DrawLine(grid, new Point(x, RulerHeight - 4), new Point(x, Bounds.Height));
            context.DrawText(Label(frame.ToString("0", CultureInfo.InvariantCulture), text), new Point(x + 3, 4));
        }

        for (var i = 0; i < Rows.Count && RulerHeight + (i + 1) * RowHeight <= Bounds.Height + RowHeight; i++)
        {
            var (row, top) = (Rows[i], RulerHeight + i * RowHeight);
            context.DrawLine(grid, new Point(0, top), new Point(Bounds.Width, top));
            var label = Label(row.Name, Brushes.White);
            label.MaxTextWidth = Math.Max(1, LabelWidth - 12 - row.Depth * 12);
            label.MaxLineCount = 1;
            context.DrawText(label, new Point(6 + row.Depth * 12, top + 4));
            foreach (var (key, length) in row.Frames)
                context.DrawRectangle(new SolidColorBrush(((ISolidColorBrush)accent).Color, 0.45), new Pen(accent, 1), new Rect(LabelWidth + key * scale, top + 4, Math.Max(4, length * scale), RowHeight - 8), 2, 2);
        }
    }
}

/// <summary>One row of the timeline: a track, the moments or the broadcast messages, with the key and length of each keyframe in frames.</summary>
public sealed record TimelineRow(int Depth, string Name, IReadOnlyList<(float Key, float Length)> Frames);
