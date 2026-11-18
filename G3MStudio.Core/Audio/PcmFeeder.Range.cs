namespace G3MStudio.Core.Audio;

/// <summary>The range of a <see cref="PcmFeeder"/>: set when a block starts, changed while it is read, and the frame heard inside it.</summary>
public sealed partial class PcmFeeder
{
    /// <summary>Starts the range in seconds; an end at or before the start means the end of the clip.</summary>
    public void Begin(double start, double end)
    {
        lock (_gate)
        {
            (_start, _end) = Frames(start, end);
            _next = _start;
        }
    }

    /// <summary>The range in frames, counted from the clip start.</summary>
    public (long Start, long End) Range
    {
        get { lock (_gate) return (_start, _end); }
    }

    /// <summary>Changes the range while it is being read (a region edited during playback), in seconds like <see cref="Begin"/>; the next block stays where it is. Returns false when the range did not change.</summary>
    public bool SetRange(double start, double end)
    {
        lock (_gate)
        {
            var range = Frames(start, end);
            if (range == (_start, _end)) return false;
            (_start, _end) = range;
            return true;
        }
    }

    private (long Start, long End) Frames(double start, double end)
    {
        var from = Math.Clamp((long)(start * clip.SampleRate), 0, clip.Frames);
        return (from, end > start ? Math.Clamp((long)(end * clip.SampleRate), from, clip.Frames) : clip.Frames);
    }

    /// <summary>A frame counted without the wrap of a loop (a block can run past the range end) as the frame heard: back at the range start after the end when looping, else the range end.</summary>
    public long Wrap(long frame)
    {
        lock (_gate) return frame < _end || _end <= _start ? frame : Loop ? _start + ((frame - _start) % (_end - _start)) : _end;
    }

    /// <summary>Moves the next block to a time inside the range.</summary>
    public void Seek(double seconds)
    {
        lock (_gate) _next = Math.Clamp((long)(seconds * clip.SampleRate), _start, Math.Max(_start, _end - 1));
    }
}
