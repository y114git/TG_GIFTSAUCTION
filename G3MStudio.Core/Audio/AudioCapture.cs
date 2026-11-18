namespace G3MStudio.Core.Audio;

/// <summary>Why a microphone could not be listed, opened or read. Each one has its own message in the recorder.</summary>
public enum CaptureFailure
{
    LibraryMissing,
    NoDevice,

    /// <summary>The microphone exists but could not be opened (another program holds it, or the driver refused).</summary>
    Busy,

    /// <summary>The operating system privacy setting blocks it; opening would only deliver silence.</summary>
    AccessDenied,
    Lost,
}

/// <summary>Raised by a capture source or device with the reason; the recorder turns it into one message and never lets it escape.</summary>
public sealed class CaptureException(CaptureFailure failure) : Exception($"Audio capture failed: {failure}.")
{
    public CaptureFailure Failure { get; } = failure;
}

/// <summary>The microphones of a source: their names, the name of the default one and why the list may be empty or unusable (null when it is fine).</summary>
public sealed record CaptureDevices(IReadOnlyList<string> Names, string Default, CaptureFailure? Problem = null);

/// <summary>
/// One open microphone delivering mono 16 bit PCM at <see cref="SampleRate"/> Hz. It records nothing until <see cref="Start"/>; <see cref="Read"/> never blocks and returns what arrived since
/// the last call. Dispose releases the device. Tests use a fake one that never touches hardware.
/// </summary>
public interface IAudioCapture : IDisposable
{
    public const int SampleRate = 44100;

    /// <summary>Started and not stopped.</summary>
    bool IsCapturing { get; }

    void Start();

    /// <summary>Stops delivering; what arrived before the stop can still be read.</summary>
    void Stop();

    /// <summary>Copies the samples that arrived since the last call into <paramref name="buffer"/> and returns how many (0 when none). Throws <see cref="CaptureException"/> with <see cref="CaptureFailure.Lost"/> when the device went away or was disposed (a disposed device that read as silence would look like a recording).</summary>
    int Read(Span<short> buffer);
}

/// <summary>Where microphones come from. Both calls may take a while (a driver is asked), so the app calls them off the UI thread.</summary>
public interface IAudioCaptureSource
{
    /// <summary>The microphones now present; never throws.</summary>
    CaptureDevices List();

    /// <summary>Opens a microphone by name (empty is the system default). Throws <see cref="CaptureException"/> when it cannot.</summary>
    IAudioCapture Open(string device);
}
