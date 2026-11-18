using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using G3MStudio.Core.Audio;
using G3MStudio.Core.Workspace;
using G3MStudio.Theme;

namespace G3MStudio.Editor;

/// <summary>
/// A level meter (peak and RMS of the left and right channel) beside a spectrum on a logarithmic frequency axis, both in decibels. The player's timer calls <see cref="Step"/> with the clip and
/// the playing position; the control reads the samples there, so it needs no device. After a stop or a pause the values fall to zero and the control stops drawing.
/// </summary>
public sealed class AudioVisualizer : Control
{
    private const double MeterWidth = 14, Gap = 4, BarPitch = 6; // pixels
    private const int MinBars = 4, MaxBars = 64;

    public AudioVisualizer()
    {
        Name = "AudioVisualizer";
        Height = 72;
        MinWidth = 60;
        IsHitTestVisible = false;
    }

    /// <summary>The state being drawn, for tests.</summary>
    internal VisualizerState State { get; } = new();

    /// <summary>Every value is zero: nothing is drawn but the empty scale, and the timer has no reason to call <see cref="Step"/>.</summary>
    public bool Settled => State.Settled;

    /// <summary>How many spectrum bars fit the width now.</summary>
    internal int BarCount => (int)Math.Clamp((Bounds.Width - 2 * MeterWidth - 3 * Gap) / BarPitch, MinBars, MaxBars);

    /// <summary>Moves the display by <paramref name="seconds"/>: while <paramref name="playing"/> to what the clip holds at <paramref name="position"/>, else toward zero. Nothing is analysed while the control is hidden or its tab is not in front.</summary>
    public void Step(PcmAudio? clip, double position, bool playing, double seconds)
    {
        var live = playing && IsEffectivelyVisible ? clip : null;
        var bars = BarCount;
        State.Update(seconds, bars, live is null ? null : AudioAnalysis.Levels(live, position), live is null ? null : AudioAnalysis.Spectrum(live, position, bars));
        if (IsEffectivelyVisible) InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        var accent = CanvasTheme.Get("Text.Accent");
        var (width, height) = (Bounds.Width, Bounds.Height);
        if (width <= 0 || height <= 0) return;
        using (context.PushOpacity(0.18))
        {
            // the dB scale: a line every 20 dB across both displays
            for (var db = 0; db >= AudioAnalysis.FloorDb; db -= 20) context.FillRectangle(accent, new Rect(0, Y(AudioAnalysis.Unit(Math.Pow(10, db / 20.0)), height), width, 1));
        }

        for (var channel = 0; channel < 2; channel++)
        {
            var x = channel * (MeterWidth + Gap);
            using (context.PushOpacity(0.5)) context.FillRectangle(accent, Bar(x, MeterWidth, State.Rms[channel], height));
            context.FillRectangle(accent, new Rect(x, Y(State.Peak[channel], height), MeterWidth, 2));
        }

        var left = 2 * (MeterWidth + Gap) + Gap;
        var count = State.Bars.Length;
        if (count == 0) return;
        var pitch = (width - left) / count;
        for (var i = 0; i < count; i++) context.FillRectangle(accent, Bar(left + i * pitch, Math.Max(1, pitch - 1.5), State.Bars[i], height));
    }

    private static double Y(float unit, double height) => height - 1 - unit * (height - 1);

    private static Rect Bar(double x, double width, float unit, double height) => new(x, Y(unit, height), width, Math.Max(0, height - Y(unit, height)));

}
