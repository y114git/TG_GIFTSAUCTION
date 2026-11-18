using G3MStudio.Core.Workspace;

namespace G3MStudio.Core.Audio;

/// <summary>
/// The samples of one recording, in memory: what the microphone delivers is multiplied by the gain, held inside 16 bit full scale (never wrapped) and appended in chunks, so a take grows without
/// ever copying what it already holds. There is no limit of time; the recorder stops when memory runs short. <see cref="Clipped"/> says that a sample reached full scale, so the recorder can
/// warn that the gain or the voice is too loud.
/// </summary>
public sealed class RecordingTake
{
    public const int SampleRate = IAudioCapture.SampleRate;

    /// <summary>Frames in one chunk (about three seconds, 256 KB), a power of two so a frame finds its chunk by a shift.</summary>
    public const int ChunkFrames = 1 << 17;

    /// <summary>The most frames a take can hold, as a 16 bit WAV of it must keep its size in 32 bits: about 6.7 hours.</summary>
    public const int MaxFrames = (int.MaxValue - 1024) / 2;

    public const double MaxGain = 4;

    private const int ChunkShift = 17;

    private readonly List<short[]> _chunks = [];
    private bool _released;

    public int Frames { get; private set; }

    /// <summary>The most frames the take holds; <see cref="MaxFrames"/> unless a test lowers it.</summary>
    public int LimitFrames { get; set; } = MaxFrames;

    public double Seconds => (double)Frames / SampleRate;

    /// <summary>Two bytes a frame while the samples are held, 0 after <see cref="Release"/>.</summary>
    public long Bytes => _released ? 0 : (long)Frames * sizeof(short);

    /// <summary>The limit is reached; nothing more is taken.</summary>
    public bool Full => Frames >= LimitFrames;

    /// <summary>A sample reached full scale since the take began or <see cref="ResetClipping"/>.</summary>
    public bool Clipped { get; private set; }

    /// <summary>The loudest sample of the last block, 0 to 1 of full scale (after the gain).</summary>
    public float Peak { get; private set; }

    /// <summary>Applies to the blocks that follow; 0 to <see cref="MaxGain"/>, and a value that is not a number counts as 1.</summary>
    public double Gain
    {
        get;
        set => field = double.IsFinite(value) ? Math.Clamp(value, 0, MaxGain) : 1;
    } = 1;

    /// <summary>Adds a block from the microphone and returns how many of its frames fit before the limit. A chunk that cannot be allocated throws <see cref="OutOfMemoryException"/> with the frames before it kept.</summary>
    public int Append(ReadOnlySpan<short> block)
    {
        var take = Math.Min(block.Length, LimitFrames - Frames);
        if (take <= 0) return 0;
        var peak = 0;
        for (var done = 0; done < take;)
        {
            var at = Frames & (ChunkFrames - 1);
            if (at == 0 && Frames >> ChunkShift == _chunks.Count) _chunks.Add(new short[ChunkFrames]);
            var chunk = _chunks[Frames >> ChunkShift];
            var count = Math.Min(take - done, ChunkFrames - at);
            for (var i = 0; i < count; i++)
            {
                var scaled = Math.Round(block[done + i] * Gain);
                if (scaled is >= short.MaxValue or <= short.MinValue) Clipped = true;
                var sample = (short)Math.Clamp(scaled, short.MinValue, short.MaxValue);
                chunk[at + i] = sample;
                peak = Math.Max(peak, Math.Abs((int)sample));
            }

            Frames += count;
            done += count;
        }

        Peak = peak / 32768f;
        return take;
    }

    public void ResetClipping() => Clipped = false;

    public void Clear()
    {
        (Frames, Peak, Clipped, _released) = (0, 0, false, false);
        _chunks.Clear();
    }

    /// <summary>The whole take as a clip. With <paramref name="release"/> each chunk is let go as soon as it is copied, so the take and the clip are not both whole in memory; the clip is made before any chunk is let go, so a refused allocation loses nothing.</summary>
    public PcmAudio ToAudio(bool release = false) => Copy(Frames, release);

    /// <summary>Frees the samples once <see cref="ToAudio"/> has made the clip: the counts stay, <see cref="Last"/> is empty, and only <see cref="Clear"/> makes the take usable again.</summary>
    public void Release()
    {
        _chunks.Clear();
        _released = true;
    }

    /// <summary>The last <paramref name="frames"/> frames (fewer when the take is shorter), for the live meter.</summary>
    public PcmAudio Last(int frames) => Copy(frames, false);

    private PcmAudio Copy(int frames, bool release)
    {
        var count = _released ? 0 : Math.Clamp(frames, 0, Frames);
        var samples = new float[count];
        var first = Frames - count;
        for (var done = 0; done < count;)
        {
            var frame = first + done;
            var chunk = _chunks[frame >> ChunkShift];
            var at = frame & (ChunkFrames - 1);
            var run = Math.Min(count - done, ChunkFrames - at);
            for (var i = 0; i < run; i++) samples[done + i] = chunk[at + i] / 32768f;
            done += run;
            if (release && at + run == ChunkFrames) _chunks[frame >> ChunkShift] = []; // the chunk is copied to its end
        }

        if (release) Release();
        return new PcmAudio(samples, 1, SampleRate);
    }
}
