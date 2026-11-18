using G3MStudio.Core.Workspace;

namespace G3MStudio.Core.Audio;

/// <summary>
/// What the visualizer shows for a position of a decoded clip: the level of each channel and the spectrum around the position. It reads the samples the player is about to play, so it
/// needs no capture from the device and works the same with the device-less output of the tests. Every value is finite; a NaN sample counts as silence.
/// </summary>
public static class AudioAnalysis
{
    public const int FftSize = 2048;

    /// <summary>The quietest level the displays show, in decibels; anything lower is the bottom.</summary>
    public const double FloorDb = -80;

    private static readonly float[] Window = Fft.Hann(FftSize);

    /// <summary>A level between 0 and 1 mapped to a unit scale: 0 at <see cref="FloorDb"/> and below, 1 at full scale.</summary>
    public static float Unit(double linear) => linear > 0 ? (float)Math.Clamp((20 * Math.Log10(linear) - FloorDb) / -FloorDb, 0, 1) : 0;

    /// <summary>Peak and RMS (0 to 1, linear) of the left and right channel in the <paramref name="window"/> seconds around <paramref name="position"/>. A mono clip fills both; more than two channels give the first two.</summary>
    public static (float Peak, float Rms)[] Levels(PcmAudio clip, double position, double window = 0.05)
    {
        var result = new (float, float)[2];
        var (from, to) = Range(clip, position, Math.Max(1, (int)(window * clip.SampleRate)));
        if (to <= from) return result;
        for (var channel = 0; channel < 2; channel++)
        {
            var source = Math.Min(channel, clip.Channels - 1);
            double peak = 0, sum = 0;
            for (var frame = from; frame < to; frame++)
            {
                var sample = Sample(clip, frame, source);
                peak = Math.Max(peak, Math.Abs(sample));
                sum += sample * sample;
            }

            result[channel] = ((float)peak, (float)Math.Sqrt(sum / (to - from)));
        }

        return result;
    }

    /// <summary>The amplitude (0 to 1, a full scale sine is 1) of each of the <see cref="FftSize"/> / 2 frequency bins of the channels mixed, in the <see cref="FftSize"/> samples around <paramref name="position"/>; bin k is k times the sample rate over the size.</summary>
    public static float[] Bins(PcmAudio clip, double position)
    {
        var re = new float[FftSize];
        var im = new float[FftSize];
        var first = (long)Math.Round(position * clip.SampleRate) - FftSize / 2;
        double gain = 0;
        for (var i = 0; i < FftSize; i++)
        {
            gain += Window[i];
            var frame = first + i;
            if (frame < 0 || frame >= clip.Frames) continue; // past the ends is silence
            double mix = 0;
            for (var channel = 0; channel < clip.Channels; channel++) mix += Sample(clip, frame, channel);
            re[i] = (float)(mix / clip.Channels) * Window[i];
        }

        Fft.Transform(re, im);
        return [.. Enumerable.Range(0, FftSize / 2).Select(k => (float)Math.Min(1, 2 * Math.Sqrt((double)re[k] * re[k] + (double)im[k] * im[k]) / gain))];
    }

    /// <summary>The spectrum as <paramref name="bars"/> bars on a logarithmic frequency axis from <paramref name="lowHz"/> up to the top of the range, each the loudest bin of its band on the <see cref="Unit"/> decibel scale.</summary>
    public static float[] Spectrum(PcmAudio clip, double position, int bars, double lowHz = 40, double highHz = 16000)
    {
        var result = new float[Math.Max(0, bars)];
        if (result.Length == 0 || clip.Frames == 0) return result;
        var bins = Bins(clip, position);
        highHz = Math.Min(highHz, clip.SampleRate / 2.0);
        var binWidth = (double)clip.SampleRate / FftSize;
        for (var bar = 0; bar < result.Length; bar++)
        {
            double lo = lowHz * Math.Pow(highHz / lowHz, (double)bar / result.Length), hi = lowHz * Math.Pow(highHz / lowHz, (double)(bar + 1) / result.Length);
            var first = Math.Clamp((int)Math.Round(lo / binWidth), 1, bins.Length - 1);
            var last = Math.Clamp((int)Math.Round(hi / binWidth) - 1, first, bins.Length - 1); // a band narrower than a bin still takes the bin it falls in
            var loudest = 0f;
            for (var k = first; k <= last; k++) loudest = Math.Max(loudest, bins[k]);
            result[bar] = Unit(loudest);
        }

        return result;
    }

    private static (long From, long To) Range(PcmAudio clip, double position, int frames)
    {
        var from = (long)Math.Round(position * clip.SampleRate) - frames / 2;
        return (Math.Clamp(from, 0, clip.Frames), Math.Clamp(from + frames, 0, clip.Frames));
    }

    private static double Sample(PcmAudio clip, long frame, int channel)
    {
        var sample = clip.Samples[frame * clip.Channels + channel];
        return float.IsFinite(sample) ? Math.Clamp(sample, -1, 1) : 0;
    }
}

/// <summary>
/// What the visualizer draws, with the fall of a real meter: a value jumps up to a louder target at once and sinks back at a fixed speed, so a stopped or paused sound ends at exactly
/// zero after a short time and nothing stays lit. Values are on the 0 to 1 unit scale.
/// </summary>
public sealed class VisualizerState
{
    /// <summary>Units per second a value falls by; a full bar is gone in about 0.7 s.</summary>
    public const double Fall = 1.4;

    public float[] Bars { get; private set; } = [];

    /// <summary>Left and right: the peak and the RMS level.</summary>
    public float[] Peak { get; } = new float[2];

    public float[] Rms { get; } = new float[2];

    /// <summary>Every value is zero.</summary>
    public bool Settled => Bars.All(b => b == 0) && Peak.All(p => p == 0) && Rms.All(r => r == 0);

    /// <summary>Moves the values by <paramref name="seconds"/>: up to the given targets when there are some (null while nothing plays), down at <see cref="Fall"/> otherwise.</summary>
    public void Update(double seconds, int bars, (float Peak, float Rms)[]? levels, float[]? spectrum)
    {
        if (Bars.Length != bars) Bars = new float[Math.Max(0, bars)];
        var drop = (float)(Fall * Math.Clamp(double.IsFinite(seconds) ? seconds : 0, 0, 0.25));
        for (var i = 0; i < Bars.Length; i++) Bars[i] = Move(Bars[i], spectrum is not null && i < spectrum.Length ? spectrum[i] : 0, drop);
        for (var i = 0; i < 2; i++)
        {
            Peak[i] = Move(Peak[i], levels is { Length: > 1 } ? AudioAnalysis.Unit(levels[i].Peak) : 0, drop);
            Rms[i] = Move(Rms[i], levels is { Length: > 1 } ? AudioAnalysis.Unit(levels[i].Rms) : 0, drop);
        }
    }

    private static float Move(float value, float target, float drop)
    {
        target = float.IsFinite(target) ? Math.Clamp(target, 0, 1) : 0;
        var next = Math.Max(target, value - drop);
        return next < 0.002f ? 0 : next;
    }
}
