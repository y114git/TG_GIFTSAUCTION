using G3MStudio.Core.Workspace;

namespace G3MStudio.Core.Audio;

/// <summary>
/// Cuts a range of decoded audio into blocks of 16 bit samples with at most two channels (more channels are mixed down to the first two), wrapping at the end of the range
/// while the loop is on. A device output feeds its buffers from this; the logic has no device in it, so tests cover it.
/// </summary>
public sealed partial class PcmFeeder(PcmAudio clip)
{
    private readonly Lock _gate = new(); // the range changes on the UI thread while a pump thread reads blocks
    private long _start, _end, _next;

    /// <summary>1 or 2.</summary>
    public int Channels { get; } = Math.Min(2, Math.Max(1, clip.Channels));

    public int SampleRate => clip.SampleRate;

    /// <summary>The frame, counted from the clip start, that the next block starts at.</summary>
    public long Next
    {
        get { lock (_gate) return _next; }
    }

    public bool Loop { get; set; }

    /// <summary>Fills up to <paramref name="frames"/> frames and returns how many were written and the frame the block starts at. 0 frames means the range is over (with a loop only when the range is empty).</summary>
    public (int Frames, long From) Read(short[] block, int frames)
    {
        lock (_gate)
        {
            var from = _next;
            if (_end <= _start) return (0, from);
            if (_next < _start) _next = from = _start; // the range start moved past the next block
            var written = 0;
            while (written < frames)
            {
                if (_next >= _end)
                {
                    if (!Loop) break;
                    _next = _start;
                }

                var take = (int)Math.Min(frames - written, _end - _next);
                for (var i = 0; i < take; i++)
                {
                    for (var c = 0; c < Channels; c++)
                        block[((written + i) * Channels) + c] = (short)Math.Clamp((int)Math.Round(clip.Samples[((_next + i) * clip.Channels) + c] * 32767f), short.MinValue, short.MaxValue);
                }

                written += take;
                _next += take;
            }

            return (written, from);
        }
    }
}
