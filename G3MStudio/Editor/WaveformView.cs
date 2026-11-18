using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using G3MStudio.Core.Audio;
using G3MStudio.Theme;

namespace G3MStudio.Editor;

/// <summary>
/// The lowest and highest sample of audio per pixel column, in two lanes for stereo when the control is tall enough. A click asks to seek; pressing and dragging selects a region, the edges of a region can be dragged, its middle moves
/// the whole region, and Shift+click moves the nearer edge to the pointer.
/// The time axis zooms (Ctrl+wheel at the pointer, <see cref="Zoom"/>) and scrolls (the wheel, <see cref="Scroll"/>); the region, the playhead and the events stay fractions of the whole sound, and every pointer position is mapped
/// through the visible range, so selecting and seeking are exact at any zoom.
/// </summary>
public sealed class WaveformView : Control
{
    /// <summary>The control is at least this tall (pixels) for two lanes.</summary>
    public const double TwoLaneHeight = 100;

    private const double Slop = 4, Grab = 6; // pixels: a press that moves less is a click; an edge is grabbed this close
    internal static readonly Cursor Pick = new(StandardCursorType.Ibeam), Edge = new(StandardCursorType.SizeWestEast), Band = new(StandardCursorType.SizeAll);

    private bool _pressed, _dragging, _moving;
    private double _downX, _anchor; // where the press began; the fixed end of the region being dragged, as a fraction
    private (double Start, double End)? _before;
    private double _grab; // where in the region a move began, as a fraction of the sound

    private (double Start, double Span, int Columns, WavePeaks? Source, (float[] Min, float[] Max)[] Lanes)? _cache;

    public WaveformView()
    {
        Name = "Waveform";
        Height = 64;
        Focusable = true; // Escape needs the focus to cancel a selection
        Cursor = Pick;
        ClipToBounds = true; // a zoomed in view puts the region and the playhead far outside it
    }

    /// <summary>A plain click: the place as a fraction of the width, 0 to 1.</summary>
    public event Action<double>? SeekRequested;

    /// <summary>A drag selects or resizes the region (fractions of the width, live while dragging); null clears it (double click, or Escape when there was none).</summary>
    public event Action<(double Start, double End)?>? RegionDragged;

    /// <summary>The playing position as a fraction of the width; NaN draws no cursor.</summary>
    public double Playhead
    {
        get;
        set
        {
            if (field.Equals(value)) return;
            field = value;
            InvalidateVisual();
        }
    } = double.NaN;

    /// <summary>The shaded part (fractions of the width), or null.</summary>
    public (double Start, double End)? Region
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            InvalidateVisual();
        }
    }

    /// <summary>The edge of the region the arrow keys move: the one last grabbed or dragged, the end until then.</summary>
    public bool EndEdge { get; private set; } = true;

    /// <summary>The audio drawn; null draws a flat line (the audio is not decoded yet).</summary>
    public WavePeaks? Source
    {
        get;
        set
        {
            field = value;
            _cache = null;
            InvalidateVisual();
        }
    }

    /// <summary>2 when a stereo sound is drawn in two lanes (the control is at least <see cref="TwoLaneHeight"/> tall), else 1.</summary>
    public int LaneCount => Source is { Lanes: 2 } && Bounds.Height >= TwoLaneHeight ? 2 : 1;

    /// <summary>The visible range as fractions of the sound: where it starts and how much of the sound it covers (1 shows all).</summary>
    public double ViewStart { get; private set; }

    public double ViewSpan { get; private set; } = 1;

    /// <summary>Raised when the visible range changes, by the user or by code; the owner keeps its scroll bar in step.</summary>
    public event Action? ViewChanged;

    /// <summary>The smallest range that can be shown: about 16 frames of the sound.</summary>
    public double MinSpan => Source is { Frames: > 16 } source ? 16.0 / source.Frames : 1;

    /// <summary>Shows a range, kept inside the sound and between <see cref="MinSpan"/> and 1.</summary>
    public void SetView(double start, double span)
    {
        span = Math.Clamp(double.IsNaN(span) ? 1 : span, MinSpan, 1);
        start = Math.Clamp(double.IsNaN(start) ? 0 : start, 0, 1 - span);
        if (start == ViewStart && span == ViewSpan) return;
        (ViewStart, ViewSpan) = (start, span);
        InvalidateVisual();
        ViewChanged?.Invoke();
    }

    /// <summary>Zooms in (a factor above 1) or out by <paramref name="factor"/> so the sound position <paramref name="anchor"/> stays where it is on the screen.</summary>
    public void Zoom(double factor, double anchor)
    {
        var span = Math.Clamp(ViewSpan / factor, MinSpan, 1);
        anchor = Math.Clamp(anchor, ViewStart, ViewStart + ViewSpan);
        SetView(anchor - (anchor - ViewStart) / ViewSpan * span, span);
    }

    /// <summary>Scrolls by a share of the visible range.</summary>
    public void Scroll(double share) => SetView(ViewStart + share * ViewSpan, ViewSpan);

    /// <summary>Scrolls just far enough, and no further than a page, to show a position (the cursor of a playing sound).</summary>
    public void Reveal(double fraction)
    {
        if (fraction >= ViewStart && fraction <= ViewStart + ViewSpan) return;
        SetView(fraction - ViewSpan * 0.1, ViewSpan);
    }

    /// <summary>The place of a pixel column as a fraction of the sound (not clamped: a drag outside the control reaches beyond the visible range).</summary>
    public double FractionAt(double x) => Bounds.Width > 0 ? ViewStart + x / Bounds.Width * ViewSpan : ViewStart;

    /// <summary>The inverse of <see cref="FractionAt"/>: the pixel column of a fraction of the sound, outside 0 to the width for a place that is out of view.</summary>
    public double XOf(double fraction) => (fraction - ViewStart) / ViewSpan * Bounds.Width;

    private bool NearEdge(double x) => Region is var (from, to) && Math.Min(Math.Abs(x - XOf(from)), Math.Abs(x - XOf(to))) <= Grab;

    // A region that is the whole sound (the audio editor starts with one) has no middle to move: a drag there selects.
    private bool InBand(double x) => Region is var (from, to) && to - from < 1 && x > XOf(from) && x < XOf(to);

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var point = e.GetCurrentPoint(this);
        if (!point.Properties.IsLeftButtonPressed || Bounds.Width <= 0) return;
        Focus();
        e.Handled = true;
        if (e.ClickCount > 1)
        {
            RegionDragged?.Invoke(null);
            return;
        }

        (_pressed, _dragging, _moving, _downX, _before) = (true, false, false, point.Position.X, Region);
        _anchor = Math.Clamp(FractionAt(_downX), 0, 1);
        if (Region is var (from, to))
        {
            var startNearer = Math.Abs(_downX - XOf(from)) <= Math.Abs(_downX - XOf(to));
            var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
            if (shift || NearEdge(_downX))
            {
                (_anchor, EndEdge) = (startNearer ? to : from, !startNearer); // the edge grabbed moves, the other stays
                _dragging = shift; // Shift+click moves the edge at once, and is not a seek
            }
            else if (InBand(_downX) && !e.KeyModifiers.HasFlag(KeyModifiers.Control)) (_moving, _grab) = (true, FractionAt(_downX)); // Ctrl+drag selects a new region inside the old one
        }

        e.Pointer.Capture(this);
        if (_dragging) Drag(_downX);
    }

    private void Drag(double x)
    {
        var here = Math.Clamp(FractionAt(x), 0, 1);
        if (_moving && _before is var (from, to))
        {
            var start = Math.Clamp(from + FractionAt(x) - _grab, 0, 1 - (to - from));
            Region = (start, start + to - from);
        }
        else
        {
            Region = (Math.Min(_anchor, here), Math.Max(_anchor, here));
            EndEdge = here >= _anchor;
        }

        RegionDragged?.Invoke(Region);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var x = e.GetPosition(this).X;
        if (!_pressed)
        {
            Cursor = NearEdge(x) ? Edge : InBand(x) ? Band : Pick;
            return;
        }

        if (!_dragging && Math.Abs(x - _downX) < Slop) return;
        _dragging = true;
        Drag(x);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        var click = _pressed && !_dragging;
        _pressed = _dragging = _moving = false;
        if (e.Pointer.Captured == this) e.Pointer.Capture(null);
        if (click && Bounds.Width > 0) SeekRequested?.Invoke(Math.Clamp(FractionAt(e.GetPosition(this).X), 0, 1));
    }

    // Ctrl+wheel zooms at the pointer; the plain wheel (or a sideways one) scrolls while zoomed in and is left to the page otherwise.
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
            Zoom(Math.Pow(1.25, e.Delta.Y), FractionAt(e.GetPosition(this).X));
        else if (ViewSpan < 1) Scroll(-(e.Delta.X != 0 ? e.Delta.X : e.Delta.Y) * 0.1);
        else return;
        e.Handled = true;
    }

    // The window lost the pointer (focus moved away mid drag): there is no gesture left for Escape to cancel.
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _pressed = _dragging = _moving = false;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key != Key.Escape || !_pressed) return;
        var moved = Region != _before;
        (_pressed, _dragging, _moving, e.Handled, Region) = (false, false, false, true, _before); // the selection is dropped and the region comes back as it was
        if (moved) RegionDragged?.Invoke(_before); // a press without a drag changed nothing, and must not overwrite the fields
    }

    public override void Render(DrawingContext context)
    {
        var brush = CanvasTheme.Get("Text.Accent");
        var (width, height) = (Bounds.Width, Bounds.Height);
        context.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size)); // the empty parts take clicks too
        if (Region is var (from, to))
        {
            var (left, right) = (XOf(from), XOf(to));
            if (right >= 0 && left <= width)
                using (context.PushOpacity(0.2)) context.FillRectangle(brush, new Rect(left, 0, Math.Max(1, right - left), height));
            var grip = Math.Min(24, height / 2);
            foreach (var edge in new[] { left, right })
            {
                if (edge < -3 || edge > width + 3) continue; // an edge outside the visible range
                context.FillRectangle(brush, new Rect(edge - 1, 0, 2, height));
                context.FillRectangle(brush, new Rect(edge - 3, height / 2 - grip / 2, 6, grip), 2);
            }
        }

        var columns = (int)Math.Ceiling(width);
        if (Source is null || columns <= 0) context.FillRectangle(brush, new Rect(0, height / 2 - 0.5, width, 1));
        else
        {
            if (_cache is not { } hit || hit.Start != ViewStart || hit.Span != ViewSpan || hit.Columns != columns || !ReferenceEquals(hit.Source, Source))
                _cache = (ViewStart, ViewSpan, columns, Source, Source.Columns(ViewStart, ViewSpan, columns));
            var lanes = _cache.Value.Lanes;
            var count = LaneCount; // stereo in two lanes when there is room, else both channels in one
            var split = count == 2;
            for (var lane = 0; lane < count; lane++)
            {
                double top = height * lane / count, laneHeight = height / count, middle = top + laneHeight / 2;
                for (var i = 0; i < columns; i++)
                {
                    float low = lanes[lane].Min[i], high = lanes[lane].Max[i];
                    if (!split && lanes.Length == 2) (low, high) = (Math.Min(low, lanes[1].Min[i]), Math.Max(high, lanes[1].Max[i]));
                    double y1 = middle - high * laneHeight / 2, y2 = middle - low * laneHeight / 2;
                    context.FillRectangle(brush, new Rect(i, Math.Min(y1, y2 - 1), 1, Math.Max(1, y2 - y1)));
                }

                if (split && lane == 0) using (context.PushOpacity(0.3)) context.FillRectangle(brush, new Rect(0, top + laneHeight - 0.5, width, 1));
            }
        }

        if (!double.IsNaN(Playhead) && XOf(Playhead) is var x && x >= -1 && x <= width + 1)
            context.FillRectangle(CanvasTheme.Get("Text", Brushes.White), new Rect(x - 1, 0, 2, height));
    }
}
