using G3MStudio.Core.Workspace;

namespace G3MStudio.Core.Audio;

/// <summary>
/// Cuts a range of decoded audio into blocks of 16 bit samples with at most two channels (more channels are mixed down to the first two), wrapping at the end of the range
/// while the loop is on. A device output feeds its buffers from this; the logic has no device in it, so tests cover it.
/// </summary>
public sealed class PcmFeeder(PcmAudio clip)
{
    private long _start, _end;

    /// <summary>1 or 2.</summary>
    public int Channels { get; } = Math.Min(2, Math.Max(1, clip.Channels));

    public int SampleRate => clip.SampleRate;

    /// <summary>The frame, counted from the clip start, that the next block starts at.</summary>
    public long Next { get; private set; }

    public bool Loop { get; set; }

    /// <summary>Starts the range in seconds; an end at or before the start means the end of the clip.</summary>
    public void Begin(double start, double end)
    {
        _start = Math.Clamp((long)(start * clip.SampleRate), 0, clip.Frames);
        _end = end > start ? Math.Clamp((long)(end * clip.SampleRate), _start, clip.Frames) : clip.Frames;
        Next = _start;
    }

    /// <summary>A frame counted without the wrap of a loop (a block can run past the range end) as the frame heard: back at the range start after the end when looping, else the range end.</summary>
    public long Wrap(long frame) => frame < _end || _end <= _start ? frame : Loop ? _start + ((frame - _start) % (_end - _start)) : _end;

    /// <summary>Moves the next block to a time inside the range.</summary>
    public void Seek(double seconds) => Next = Math.Clamp((long)(seconds * clip.SampleRate), _start, Math.Max(_start, _end - 1));

    /// <summary>Fills up to <paramref name="frames"/> frames and returns how many were written and the frame the block starts at. 0 frames means the range is over (with a loop only when the range is empty).</summary>
    public (int Frames, long From) Read(short[] block, int frames)
    {
        var from = Next;
        if (_end <= _start) return (0, from);
        var written = 0;
        while (written < frames)
        {
            if (Next >= _end)
            {
                if (!Loop) break;
                Next = _start;
            }

            var take = (int)Math.Min(frames - written, _end - Next);
            for (var i = 0; i < take; i++)
            {
                for (var c = 0; c < Channels; c++)
                    block[((written + i) * Channels) + c] = (short)Math.Clamp((int)Math.Round(clip.Samples[((Next + i) * clip.Channels) + c] * 32767f), short.MinValue, short.MaxValue);
            }

            written += take;
            Next += take;
        }

        return (written, from);
    }
}
