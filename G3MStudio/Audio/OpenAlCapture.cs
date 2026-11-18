using G3MStudio.Core.Audio;
using Silk.NET.OpenAL;

namespace G3MStudio.Audio;

/// <summary>
/// The microphones through OpenAL Soft capture (the same native library as the output). The capture calls are read from the library directly, as the device list of <see cref="OpenAlOutput"/> is;
/// the device keeps one second of samples, so a recorder that polls every few milliseconds never loses any.
/// </summary>
internal sealed unsafe class OpenAlCaptureSource : IAudioCaptureSource
{
    private const int CaptureSpecifier = 0x310, CaptureDefaultSpecifier = 0x311;

    public CaptureDevices List()
    {
        if (OpenAlOutput.Disabled) return new([], "", CaptureFailure.NoDevice);
        try
        {
            using var alc = ALContext.GetApi(true);
            var getString = (delegate* unmanaged[Cdecl]<Device*, int, byte*>)alc.GetProcAddress(null, "alcGetString");
            List<string> names = getString == null || !alc.IsExtensionPresent(null, "ALC_ENUMERATION_EXT") ? [] : OpenAlOutput.Names(getString, CaptureSpecifier);
            var first = getString == null ? null : getString(null, CaptureDefaultSpecifier);
            var fallback = first == null ? "" : System.Runtime.InteropServices.Marshal.PtrToStringUTF8((IntPtr)first) ?? "";
            return new(names, fallback, MicrophoneAccess.IsDenied() ? CaptureFailure.AccessDenied : names.Count == 0 ? CaptureFailure.NoDevice : null);
        }
        catch (Exception e) when (IsLibraryProblem(e))
        {
            return new([], "", CaptureFailure.LibraryMissing);
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000", Justification = "The device owns the context once it is returned; every other path disposes it in the finally.")]
    public IAudioCapture Open(string device)
    {
        if (OpenAlOutput.Disabled) throw new CaptureException(CaptureFailure.NoDevice);
        if (MicrophoneAccess.IsDenied()) throw new CaptureException(CaptureFailure.AccessDenied); // the system would hand a desktop program silence, which is worse than a message
        ALContext? alc = null;
        var handedOver = false;
        try
        {
            alc = ALContext.GetApi(true);
            var opened = OpenAlCapture.TryOpen(alc, device);
            if (opened is not null)
            {
                handedOver = true; // the device owns the context now
                return opened;
            }
        }
        catch (Exception e) when (IsLibraryProblem(e))
        {
            throw new CaptureException(CaptureFailure.LibraryMissing);
        }
        finally
        {
            if (!handedOver) alc?.Dispose(); // also when a symbol was missing or anything else was thrown
        }

        throw new CaptureException(OpenFailure(List()));
    }

    /// <summary>Why a refused open failed: the privacy block the list reports wins, then no microphone at all, else another program holds it.</summary>
    internal static CaptureFailure OpenFailure(CaptureDevices list) => list.Problem == CaptureFailure.AccessDenied ? CaptureFailure.AccessDenied : list.Names.Count == 0 ? CaptureFailure.NoDevice : CaptureFailure.Busy;

    private static bool IsLibraryProblem(Exception e) => e is DllNotFoundException or BadImageFormatException or EntryPointNotFoundException or NotSupportedException or InvalidOperationException or Silk.NET.Core.Loader.SymbolLoadingException;
}

internal sealed unsafe class OpenAlCapture : IAudioCapture
{
    private const int FormatMono16 = 0x1101, CaptureSamplesQuery = 0x312, ConnectedQuery = 0x313;
    private static readonly string[] Calls = ["alcCaptureStart", "alcCaptureStop", "alcCaptureCloseDevice", "alcCaptureSamples", "alcGetIntegerv", "alcGetError"];

    private readonly ALContext _alc;
    private readonly Device* _device;
    private readonly delegate* unmanaged[Cdecl]<Device*, void> _start, _stop;
    private readonly delegate* unmanaged[Cdecl]<Device*, byte> _close;
    private readonly delegate* unmanaged[Cdecl]<Device*, void*, int, void> _samples;
    private readonly delegate* unmanaged[Cdecl]<Device*, int, int, int*, void> _query;
    private readonly delegate* unmanaged[Cdecl]<Device*, int> _error;
    private readonly Lock _gate = new();
    private bool _disposed;

    private OpenAlCapture(ALContext alc, Device* device)
    {
        _alc = alc;
        _device = device;
        _start = (delegate* unmanaged[Cdecl]<Device*, void>)alc.GetProcAddress(null, "alcCaptureStart");
        _stop = (delegate* unmanaged[Cdecl]<Device*, void>)alc.GetProcAddress(null, "alcCaptureStop");
        _close = (delegate* unmanaged[Cdecl]<Device*, byte>)alc.GetProcAddress(null, "alcCaptureCloseDevice");
        _samples = (delegate* unmanaged[Cdecl]<Device*, void*, int, void>)alc.GetProcAddress(null, "alcCaptureSamples");
        _query = (delegate* unmanaged[Cdecl]<Device*, int, int, int*, void>)alc.GetProcAddress(null, "alcGetIntegerv");
        _error = (delegate* unmanaged[Cdecl]<Device*, int>)alc.GetProcAddress(null, "alcGetError");
    }

    public bool IsCapturing { get; private set; }

    /// <summary>The open device, or null when the driver refused it. The library keeps its own copy of the name, so the bytes only live for the call.</summary>
    public static OpenAlCapture? TryOpen(ALContext alc, string name)
    {
        var open = (delegate* unmanaged[Cdecl]<byte*, uint, int, int, Device*>)alc.GetProcAddress(null, "alcCaptureOpenDevice");
        if (open == null || Calls.Any(name => alc.GetProcAddress(null, name) == null)) throw new CaptureException(CaptureFailure.LibraryMissing);
        var bytes = System.Text.Encoding.UTF8.GetBytes(name + "\0");
        Device* device;
        fixed (byte* text = bytes) device = open(name.Length == 0 ? null : text, IAudioCapture.SampleRate, FormatMono16, IAudioCapture.SampleRate);
        return device == null ? null : new OpenAlCapture(alc, device);
    }

    public void Start()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _error(_device); // reading the error clears it, so an old one is not taken for this call's
            _start(_device);
            if (_error(_device) != 0) throw new CaptureException(CaptureFailure.Busy); // the library does not throw: it sets an error and records nothing
            IsCapturing = true;
        }
    }

    public void Stop()
    {
        lock (_gate)
        {
            if (_disposed || !IsCapturing) return;
            _stop(_device); // what arrived before the stop stays readable
            IsCapturing = false;
        }
    }

    public int Read(Span<short> buffer)
    {
        lock (_gate)
        {
            if (_disposed) throw new CaptureException(CaptureFailure.Lost); // another open took the device: silence would look like a recording
            var values = stackalloc int[2] { 1, 0 }; // the 1 stays when a library lacks ALC_EXT_DISCONNECT
            _query(_device, ConnectedQuery, 1, values);
            if (values[0] == 0) throw new CaptureException(CaptureFailure.Lost);
            _query(_device, CaptureSamplesQuery, 1, values + 1);
            var count = Math.Min(values[1], buffer.Length);
            if (count <= 0) return 0;
            _error(_device);
            fixed (short* data = buffer) _samples(_device, data, count);
            if (_error(_device) != 0) throw new CaptureException(CaptureFailure.Lost);
            return count;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            try
            {
                if (IsCapturing) _stop(_device);
                IsCapturing = false;
            }
            finally
            {
                try { _close(_device); }
                finally { _alc.Dispose(); }
            }
        }
    }
}
