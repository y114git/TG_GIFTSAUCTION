using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Editor;

/// <summary>
/// The loudness of audio drawn as one bar per pixel column bucket.
/// shortcut: peaks are computed on the UI thread from the bytes already in memory; move to a pool thread if sounds of hundreds of MB show up.
/// </summary>
public sealed class WaveformView : Control
{
    public const int Buckets = 400;

    public WaveformView(float[] peaks)
    {
        Bars = peaks;
        Name = "Waveform";
        Height = 64;
    }

    /// <summary>A click or drag on the picture: the place as a fraction of the width, 0 to 1.</summary>
    public event Action<double>? SeekRequested;

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

    private float[] Bars { get; set; }

    public void SetPeaks(float[] peaks)
    {
        Bars = peaks;
        InvalidateVisual();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Request(e.GetPosition(this).X);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) Request(e.GetPosition(this).X);
    }

    private void Request(double x)
    {
        if (Bounds.Width > 0) SeekRequested?.Invoke(Math.Clamp(x / Bounds.Width, 0, 1));
    }

    /// <summary>Peak per bucket (0 to 1) of decoded audio, any channel count.</summary>
    public static float[] Peaks(PcmAudio audio, int buckets = Buckets)
    {
        var peaks = new float[buckets];
        for (var frame = 0; frame < audio.Frames; frame++)
        {
            var bucket = (int)((long)frame * buckets / audio.Frames);
            for (var c = 0; c < audio.Channels; c++) peaks[bucket] = Math.Max(peaks[bucket], Math.Abs(audio.Samples[frame * audio.Channels + c]));
        }

        return peaks;
    }

    /// <summary>Peak per bucket (0 to 1) of a PCM16 WAV, or null for any other data.</summary>
    public static float[]? Peaks(byte[] wav, int buckets = Buckets)
    {
        if (wav.Length < 44 || wav[0] != 'R' || wav[8] != 'W') return null;
        int channels = 0, bits = 0;
        for (var at = 12; at + 8 <= wav.Length;)
        {
            var size = BitConverter.ToUInt32(wav, at + 4);
            var id = System.Text.Encoding.ASCII.GetString(wav, at, 4);
            if (id == "fmt " && at + 24 <= wav.Length && BitConverter.ToUInt16(wav, at + 8) == 1)
                (channels, bits) = (BitConverter.ToUInt16(wav, at + 10), BitConverter.ToUInt16(wav, at + 22));
            else if (id == "data")
            {
                if (channels == 0 || bits != 16) return null;
                var count = (int)Math.Min(size, (uint)(wav.Length - at - 8)) / (2 * channels);
                if (count == 0) return null;
                var peaks = new float[buckets];
                for (var frame = 0; frame < count; frame++)
                {
                    var bucket = (int)((long)frame * buckets / count);
                    for (var c = 0; c < channels; c++)
                        peaks[bucket] = Math.Max(peaks[bucket], Math.Abs((int)BitConverter.ToInt16(wav, at + 8 + ((frame * channels) + c) * 2)) / 32768f);
                }

                return peaks;
            }

            at += 8 + (int)Math.Min(size + (size & 1), int.MaxValue - 8);
        }

        return null;
    }

    public override void Render(DrawingContext context)
    {
        var brush = Application.Current is { } app && app.TryGetResource("Text.Accent", app.ActualThemeVariant, out var found) && found is IBrush accent ? accent : Brushes.Gray;
        var (width, middle) = (Bounds.Width, Bounds.Height / 2);
        context.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size)); // the empty parts take clicks too
        if (Region is var (from, to))
        {
            using (context.PushOpacity(0.2)) context.FillRectangle(brush, new Rect(from * width, 0, Math.Max(1, (to - from) * width), Bounds.Height));
        }

        for (var i = 0; i < Bars.Length; i++)
        {
            var half = Math.Max(1, Bars[i] * middle);
            context.FillRectangle(brush, new Rect(i * width / Bars.Length, middle - half, Math.Max(1, width / Bars.Length - 0.5), half * 2));
        }

        if (!double.IsNaN(Playhead)) context.FillRectangle(Application.Current is { } a && a.TryGetResource("Text", a.ActualThemeVariant, out var primary) && primary is IBrush line ? line : Brushes.White, new Rect(Math.Clamp(Playhead, 0, 1) * width, 0, 2, Bounds.Height));
    }
}
