using G3MStudio.Core.Workspace;

namespace G3MStudio.Core.Audio;

/// <summary>
/// The lowest and highest sample of decoded audio per pixel column, for any visible range. A table of min and max per block of <see cref="Block"/> frames is built once (off the UI thread),
/// so a column of a zoomed out view reads a few blocks instead of its samples and a ten minute sound draws as fast as a short one; a column of a zoomed in view reads its samples directly.
/// </summary>
public sealed class WavePeaks
{
    public const int Block = 256;

    private readonly PcmAudio _clip;
    private readonly float[][] _min, _max; // per lane, per block

    public WavePeaks(PcmAudio clip)
    {
        _clip = clip;
        Lanes = Math.Clamp(clip.Channels, 1, 2);
        var blocks = (clip.Frames + Block - 1) / Block;
        _min = new float[Lanes][];
        _max = new float[Lanes][];
        for (var lane = 0; lane < Lanes; lane++)
        {
            (_min[lane], _max[lane]) = (new float[blocks], new float[blocks]);
            for (var block = 0; block < blocks; block++)
                (_min[lane][block], _max[lane][block]) = Range(lane, (long)block * Block, Math.Min(clip.Frames, (long)(block + 1) * Block));
        }
    }

    /// <summary>1 for mono, 2 for stereo or more (the first two channels).</summary>
    public int Lanes { get; }

    public int Frames => _clip.Frames;

    /// <summary>Lowest and highest sample (-1 to 1) of each of <paramref name="columns"/> equal slices of the range that starts at <paramref name="start"/> and is <paramref name="span"/> long, both as fractions of the sound; a slice with no audio is 0 and 0.</summary>
    public (float[] Min, float[] Max)[] Columns(double start, double span, int columns)
    {
        var result = new (float[] Min, float[] Max)[Lanes];
        for (var lane = 0; lane < Lanes; lane++) result[lane] = (new float[Math.Max(0, columns)], new float[Math.Max(0, columns)]);
        if (columns <= 0 || Frames == 0 || !(span > 0)) return result;
        double first = Math.Clamp(start, 0, 1) * Frames, length = Math.Min(span, 1) * Frames;
        for (var i = 0; i < columns; i++)
        {
            var from = (long)Math.Floor(first + length * i / columns);
            var to = Math.Max(from + 1, (long)Math.Ceiling(first + length * (i + 1) / columns));
            (from, to) = (Math.Clamp(from, 0, Frames), Math.Clamp(to, 0, Frames));
            if (to <= from) continue;
            for (var lane = 0; lane < Lanes; lane++)
            {
                if (to - from < 2 * Block) (result[lane].Min[i], result[lane].Max[i]) = Range(lane, from, to);
                else
                {
                    // Whole blocks inside the slice come from the table; the partial blocks at either end are read sample by sample, so no sample outside [from, to) is counted.
                    long firstBlock = (from + Block - 1) / Block, lastBlock = to / Block;
                    float low = float.MaxValue, high = float.MinValue;
                    Extend(lane, from, firstBlock * Block, ref low, ref high);
                    Extend(lane, lastBlock * Block, to, ref low, ref high);
                    for (var block = (int)firstBlock; block < lastBlock; block++)
                        (low, high) = (Math.Min(low, _min[lane][block]), Math.Max(high, _max[lane][block]));
                    (result[lane].Min[i], result[lane].Max[i]) = low > high ? (0, 0) : (Math.Clamp(low, -1, 1), Math.Clamp(high, -1, 1));
                }
            }
        }

        return result;
    }

    // A NaN sample fails both comparisons and is skipped; the rest is clipped to full scale.
    private (float Min, float Max) Range(int lane, long from, long to)
    {
        float low = float.MaxValue, high = float.MinValue;
        Extend(lane, from, to, ref low, ref high);
        return low > high ? (0, 0) : (Math.Clamp(low, -1, 1), Math.Clamp(high, -1, 1));
    }

    private void Extend(int lane, long from, long to, ref float low, ref float high)
    {
        for (var frame = from; frame < to; frame++)
        {
            var sample = _clip.Samples[frame * _clip.Channels + lane];
            if (sample < low) low = sample;
            if (sample > high) high = sample;
        }
    }
}
