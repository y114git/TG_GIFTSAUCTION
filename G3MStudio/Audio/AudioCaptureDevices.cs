using G3MStudio.Core.Audio;

namespace G3MStudio.Audio;

/// <summary>
/// The one microphone of the program: the recorder asks for it when Record is pressed, and at most one is open at a time (opening another closes the first). <see cref="Release"/> gives it back when the
/// recording stops or the dialog closes, and <see cref="Shutdown"/> when the program ends. Tests replace <see cref="Source"/> so no device is ever touched.
/// </summary>
public static class AudioCaptureDevices
{
    private static readonly Lock Gate = new();
    private static IAudioCapture? _open;

    /// <summary>Where microphones come from; the OpenAL one unless a test replaces it.</summary>
    public static IAudioCaptureSource Source { get; set; } = new OpenAlCaptureSource();

    /// <summary>The microphones now present; call off the UI thread.</summary>
    public static CaptureDevices List() => Source.List();

    /// <summary>Opens a microphone by name (empty is the default) and returns it, started by the caller; call off the UI thread. Throws <see cref="CaptureException"/>.</summary>
    public static IAudioCapture Open(string device)
    {
        lock (Gate)
        {
            _open?.Dispose();
            _open = null;
            return _open = Source.Open(device);
        }
    }

    /// <summary>Closes the device when it is still the open one.</summary>
    public static void Release(IAudioCapture? capture)
    {
        if (capture is null) return;
        lock (Gate)
        {
            if (ReferenceEquals(_open, capture)) _open = null;
            capture.Dispose();
        }
    }

    /// <summary>Closes whatever is open (the window closes).</summary>
    public static void Shutdown()
    {
        lock (Gate)
        {
            _open?.Dispose();
            _open = null;
        }
    }
}
