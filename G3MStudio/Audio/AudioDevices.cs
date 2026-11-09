using G3MStudio.Core.Audio;
using G3MStudio.Core.Settings;

namespace G3MStudio.Audio;

/// <summary>A way to play sound inside Studio. <see cref="Open"/> returns null when it cannot play (the caller then uses the system player); a silent backend plays nothing at all.</summary>
public sealed record AudioBackend(string Id, string LabelKey, Func<string, int, IAudioOutput?> Open, Func<IReadOnlyList<string>> List, Func<bool> Available, bool Silent = false);

/// <summary>
/// The one audio output of the program. A player asks for it when it starts to play; a player that held it before is told to stop, so two tabs never play over each other.
/// When no device can be opened <see cref="Acquire"/> returns null and the caller falls back to the system player. Tests replace <see cref="Open"/> and <see cref="List"/> so no
/// device is ever touched.
/// </summary>
public static class AudioDevices
{
    private static readonly Lock Gate = new();
    private static IAudioOutput? _output;
    private static string _openedWith = "";
    private static bool _failed, _warned;
    private static object? _owner;
    private static Action? _takenAway;

    /// <summary>Opens a device by name (empty is the default) with a buffer length in milliseconds; null when there is none. This is the OpenAL backend; tests replace it.</summary>
    public static Func<string, int, IAudioOutput?> Open { get; set; } = OpenAlOutput.TryOpen;

    /// <summary>Output device names of the OpenAL backend for the Settings list, empty when OpenAL or a device is missing.</summary>
    public static Func<IReadOnlyList<string>> List { get; set; } = OpenAlOutput.Devices;

    /// <summary>Whether the OpenAL native library loads; tests replace it.</summary>
    public static Func<bool> LibraryPresent { get; set; } = OpenAlOutput.LibraryPresent;

    /// <summary>Runs once when a sound is asked for while the OpenAL library cannot load; the text says where to put the library. Sound then plays through the system player.</summary>
    public static event Action<string>? LibraryMissing;

    /// <summary>The backends the user can pick, in the order of the Settings list. Add one here to offer another.</summary>
    public static List<AudioBackend> Backends { get; } =
    [
        new(AudioOptions.OpenAl, "settings.backend.openal", (device, ms) => Open(device, ms), () => List(), () => LibraryPresent()),
        new(AudioOptions.SystemPlayer, "settings.backend.system", (_, _) => null, () => [], () => true),
        new(AudioOptions.Off, "settings.backend.off", (_, _) => null, () => [], () => true, true),
    ];

    /// <summary>The backend of the options; an id that is not registered (a setting from a newer build) is the OpenAL one.</summary>
    public static AudioBackend BackendOf(AudioOptions options) => Backends.Find(b => b.Id == options.Backend) ?? Backends[0];

    /// <summary>The file name the OpenAL library has on this system, next to the program.</summary>
    public static string LibraryFile => OperatingSystem.IsWindows() ? "soft_oal.dll" : OperatingSystem.IsMacOS() ? "libopenal.dylib" : "libopenal.so";

    /// <summary>The output for <paramref name="owner"/>, opened with the options on first use or when the device or buffer changed, or null without a device. <paramref name="takenAway"/> runs when another owner asks.</summary>
    public static IAudioOutput? Acquire(AudioOptions options, object owner, Action takenAway)
    {
        lock (Gate)
        {
            var backend = BackendOf(options);
            var key = backend.Id + "|" + options.Device + "|" + options.BufferMs;
            if (key != _openedWith)
            {
                CloseOutput();
                (_openedWith, _failed) = (key, false);
            }

            if (_output is null && !_failed)
            {
                _output = backend.Open(options.Device, options.BufferMs);
                _failed = _output is null;
                if (_failed && !backend.Available() && !_warned)
                {
                    _warned = true;
                    LibraryMissing?.Invoke(LibraryFile);
                }
            }

            if (_output is not null && !ReferenceEquals(_owner, owner))
            {
                _takenAway?.Invoke();
                (_owner, _takenAway) = (owner, takenAway);
            }

            return _output;
        }
    }

    /// <summary>The user chose no sound inside Studio: nothing plays and the system player is not used either.</summary>
    public static bool IsSilent(AudioOptions options) => BackendOf(options).Silent;

    /// <summary>Stops the output when <paramref name="owner"/> holds it.</summary>
    public static void Release(object owner)
    {
        lock (Gate)
        {
            if (!ReferenceEquals(_owner, owner)) return;
            _output?.Stop();
            (_owner, _takenAway) = (null, null);
        }
    }

    /// <summary>Closes the device (the window closes).</summary>
    public static void Shutdown()
    {
        lock (Gate)
        {
            CloseOutput();
            _warned = false;
        }
    }

    private static void CloseOutput()
    {
        _takenAway?.Invoke(); // the owner drops its reference before the output is gone
        _output?.Dispose();
        (_output, _owner, _takenAway, _openedWith) = (null, null, null, "");
    }
}
