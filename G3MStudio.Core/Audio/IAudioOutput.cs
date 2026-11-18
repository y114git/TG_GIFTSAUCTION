using G3MStudio.Core.Workspace;

namespace G3MStudio.Core.Audio;

/// <summary>
/// One audio clip played through a device. The clip is a decoded <see cref="PcmAudio"/>; a range of it plays once or in a loop. Implementations are safe to call from the UI thread
/// and never throw because a device is missing (they are not created then). Tests use <see cref="NullAudioOutput"/>, which never opens a device.
/// </summary>
public interface IAudioOutput : IDisposable
{
    /// <summary>Not paused and not stopped; false while paused.</summary>
    bool IsPlaying { get; }

    /// <summary>A clip is started and not stopped: playing or paused.</summary>
    bool IsActive { get; }

    /// <summary>Seconds from the start of the clip to the sample being heard.</summary>
    double Position { get; }

    /// <summary>0 to 1.</summary>
    double Volume { get; set; }

    /// <summary>At the end of the range, start again from its start. May change while playing.</summary>
    bool Loop { get; set; }

    void Load(PcmAudio clip);

    /// <summary>Plays the range from <paramref name="start"/> to <paramref name="end"/> seconds; an end at or before the start means the end of the clip. Stops what played before.</summary>
    void Play(double start, double end);

    /// <summary>
    /// Changes the range of the playing or paused clip without restarting it (the region was edited during playback); the arguments mean the same as in <see cref="Play"/>, and an empty range is the whole clip.
    /// A position inside the new range goes on; one outside it jumps to the start of the range, and with a loop the range wraps at its new end. Does nothing when no clip is active.
    /// </summary>
    void SetRange(double start, double end);

    void Pause();

    void Resume();

    /// <summary>Stops and forgets the position.</summary>
    void Stop();

    /// <summary>Continues from this time (inside the playing range) while playing or paused.</summary>
    void Seek(double seconds);

    /// <summary>The range reached its end without a loop. May run on another thread.</summary>
    event Action? Ended;
}
