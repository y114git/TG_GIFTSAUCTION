namespace G3MStudio.Core.Settings;

/// <summary>Playback of sounds inside Studio (Settings > Sound), read when a sound view or the audio editor starts to play.</summary>
public sealed record AudioOptions
{
    public const int MinBufferMs = 20, MaxBufferMs = 500;

    /// <summary>The waveform height (pixels) the user can drag a player to.</summary>
    public const int MinWaveformHeight = 40, MaxWaveformHeight = 800;

    /// <summary>Ids of the built in backends; the app keeps a registry so another can be added (see AudioDevices).</summary>
    public const string OpenAl = "openal", SystemPlayer = "system", Off = "off";

    /// <summary>Which backend plays sounds inside Studio: OpenAL Soft (in process), the system player, or none.</summary>
    public string Backend { get; init; } = OpenAl;

    /// <summary>Name of the output device; empty is the system default.</summary>
    public string Device { get; init; } = "";

    /// <summary>Name of the microphone the recorder uses; empty is the system default. A name that is no longer present falls back to the default without being forgotten.</summary>
    public string CaptureDevice { get; init; } = "";

    /// <summary>Start volume of a player, percent.</summary>
    public int Volume { get; init; } = 100;

    /// <summary>Length of one streaming buffer in milliseconds: shorter reacts faster to seeking, longer survives a busy machine.</summary>
    public int BufferMs { get; init; } = 100;

    /// <summary>Show the level meter and spectrum under the waveform; the toggle in the player changes it.</summary>
    public bool Visualizer { get; init; } = true;

    /// <summary>The waveform height the user dragged a player to, in pixels; 0 keeps the built in height. Every player opens at it.</summary>
    public int WaveformHeight { get; init; }

    internal AudioOptions Clamped() => this with
    {
        Backend = string.IsNullOrWhiteSpace(Backend) ? OpenAl : Backend,
        Device = Device ?? "",
        CaptureDevice = CaptureDevice ?? "",
        Volume = Math.Clamp(Volume, 0, 100),
        BufferMs = Math.Clamp(BufferMs, MinBufferMs, MaxBufferMs),
        WaveformHeight = WaveformHeight <= 0 ? 0 : Math.Clamp(WaveformHeight, MinWaveformHeight, MaxWaveformHeight),
    };
}
