using G3MStudio.Core.Workspace;

namespace G3MStudio.Core.Audio;

/// <summary>An output without a device: it keeps the play state and a clock that only <see cref="Advance"/> moves, so tests drive playback without any sound hardware.</summary>
public sealed class NullAudioOutput : IAudioOutput
{
    private double _length, _start, _end;
    private bool Paused { get; set; }

    public bool IsActive { get; private set; }

    public bool IsPlaying => IsActive && !Paused;

    public double Position { get; private set; }

    public double Volume { get; set; } = 1;

    public bool Loop { get; set; }

    public event Action? Ended;

    public void Load(PcmAudio clip)
    {
        _length = clip.Seconds;
        Stop();
    }

    public void Play(double start, double end)
    {
        _start = Math.Clamp(start, 0, _length);
        _end = end > _start ? Math.Min(end, _length) : _length;
        (Position, IsActive, Paused) = (_start, _end > _start, false);
    }

    public void Pause() => Paused = IsActive;

    public void Resume() => Paused = false;

    public void Stop() => (IsActive, Paused, Position) = (false, false, 0);

    public void Seek(double seconds)
    {
        if (IsActive) Position = Math.Clamp(seconds, _start, _end);
    }

    /// <summary>Moves the clock as a device would while playing.</summary>
    public void Advance(double seconds)
    {
        if (!IsPlaying) return;
        Position += seconds;
        if (Position < _end) return;
        if (Loop) Position = _start + ((Position - _start) % (_end - _start));
        else
        {
            Stop();
            Ended?.Invoke();
        }
    }

    public void Dispose() => Stop();
}
