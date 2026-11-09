using G3MStudio.Core.Audio;
using G3MStudio.Core.Settings;
using G3MStudio.Core.Workspace;
using Silk.NET.OpenAL;

namespace G3MStudio.Audio;

/// <summary>
/// Streams a clip to an OpenAL Soft device (the native library comes with the Silk.NET.OpenAL.Soft.Native package for all six platforms). A small pool of buffers is refilled
/// from a <see cref="PcmFeeder"/> by a timer, so a range, a loop and a seek need no more memory than a few buffers.
/// </summary>
internal sealed unsafe class OpenAlOutput : IAudioOutput
{
    private const int BufferCount = 4;

    private readonly ALContext _alc;
    private readonly AL _al;
    private readonly Device* _device;
    private readonly Context* _context;
    private readonly uint _source;
    private readonly uint[] _buffers = new uint[BufferCount];
    private readonly Stack<uint> _free = new();
    private readonly Queue<(uint Buffer, int Frames, long From)> _queued = new();
    private readonly int _bufferMs;
    private readonly Lock _gate = new();
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2213", Justification = "Disposed in Dispose under the lock.")]
    private readonly Timer _pump;
    private PcmAudio? _clip;
    private PcmFeeder? _feeder;
    private short[] _block = [];
    private bool _active, _paused, _drained, _disposed;

    private OpenAlOutput(ALContext alc, AL al, Device* device, Context* context, int bufferMs)
    {
        (_alc, _al) = (alc, al);
        _device = device;
        _context = context;
        _source = al.GenSource();
        for (var i = 0; i < BufferCount; i++) _free.Push(_buffers[i] = al.GenBuffer());
        _bufferMs = Math.Clamp(bufferMs, AudioOptions.MinBufferMs, AudioOptions.MaxBufferMs);
        _pump = new Timer(_ => Pump(), null, Timeout.Infinite, Timeout.Infinite);
    }

    public event Action? Ended;

    /// <summary>Names of the output devices, or an empty list when the library or a device is missing.</summary>
    public static IReadOnlyList<string> Devices()
    {
        if (Disabled) return [];
        try
        {
            using var alc = ALContext.GetApi(true);
            return ListDevices(alc);
        }
        catch (Exception e) when (e is DllNotFoundException or BadImageFormatException or EntryPointNotFoundException or NotSupportedException or InvalidOperationException or Silk.NET.Core.Loader.SymbolLoadingException)
        {
            return [];
        }
    }

    // The enumeration extension has its own Silk.NET package; the two calls it wraps are read directly: a list of names separated by NUL and ended by two NULs.
    private static List<string> ListDevices(ALContext alc)
    {
        const int DeviceSpecifier = 0x1005, AllDevicesSpecifier = 0x1013;
        var names = new List<string>();
        var getString = (delegate* unmanaged[Cdecl]<Device*, int, byte*>)alc.GetProcAddress(null, "alcGetString");
        if (getString == null) return names;
        var all = alc.IsExtensionPresent(null, "ALC_ENUMERATE_ALL_EXT");
        if (!all && !alc.IsExtensionPresent(null, "ALC_ENUMERATION_EXT")) return names;
        for (var at = getString(null, all ? AllDevicesSpecifier : DeviceSpecifier); at != null && *at != 0;)
        {
            var text = System.Runtime.InteropServices.Marshal.PtrToStringUTF8((IntPtr)at) ?? "";
            names.Add(text);
            at += System.Text.Encoding.UTF8.GetByteCount(text) + 1;
        }

        return names;
    }

    /// <summary>Opens a device (empty name: the default one) or returns null when there is none, the native library is missing or the environment variable G3MSTUDIO_NO_AUDIO is set.</summary>
    public static IAudioOutput? TryOpen(string device, int bufferMs)
    {
        if (Disabled) return null;
        ALContext? alc = null;
        AL? al = null;
        Device* opened = null;
        try
        {
            alc = ALContext.GetApi(true);
            al = AL.GetApi(true);
            opened = alc.OpenDevice(device.Length == 0 ? null : device);
            if (opened == null) return Fail(alc, al, null, null);
            var context = alc.CreateContext(opened, null);
            if (context == null || !alc.MakeContextCurrent(context)) return Fail(alc, al, opened, context);
            return new OpenAlOutput(alc, al, opened, context, bufferMs);
        }
        catch (Exception e) when (e is DllNotFoundException or BadImageFormatException or EntryPointNotFoundException or NotSupportedException or InvalidOperationException or Silk.NET.Core.Loader.SymbolLoadingException)
        {
            Fail(alc, al, opened, null);
            return null;
        }
    }

    /// <summary>True when the OpenAL native library loads (or the output is switched off by the environment, which is not a problem to report).</summary>
    public static bool LibraryPresent()
    {
        if (Disabled) return true;
        try
        {
            using var alc = ALContext.GetApi(true);
            return true;
        }
        catch (Exception e) when (e is DllNotFoundException or BadImageFormatException or EntryPointNotFoundException or NotSupportedException or Silk.NET.Core.Loader.SymbolLoadingException)
        {
            return false;
        }
    }

    private static bool Disabled => Environment.GetEnvironmentVariable("G3MSTUDIO_NO_AUDIO") is { Length: > 0 };

    private static IAudioOutput? Fail(ALContext? alc, AL? al, Device* device, Context* context)
    {
        if (alc is not null)
        {
            if (context != null) alc.DestroyContext(context);
            if (device != null) alc.CloseDevice(device);
        }

        al?.Dispose();
        alc?.Dispose();
        return null;
    }

    public bool IsActive
    {
        get { lock (_gate) return _active; }
    }

    public bool IsPlaying
    {
        get { lock (_gate) return _active && !_paused; }
    }

    public double Volume
    {
        get;
        set
        {
            field = Math.Clamp(value, 0, 1);
            lock (_gate)
            {
                if (_disposed) return;
                Use();
                _al.SetSourceProperty(_source, SourceFloat.Gain, (float)field);
            }
        }
    } = 1;

    public bool Loop
    {
        get;
        set
        {
            field = value;
            lock (_gate)
            {
                if (_feeder is null) return;
                _feeder.Loop = value;
                _drained = false; // a loop switched on near the end has more to play
            }
        }
    }

    public double Position
    {
        get
        {
            lock (_gate)
            {
                if (!_active || _feeder is null || _clip is null) return 0;
                Use();
                _al.GetSourceProperty(_source, GetSourceInteger.SampleOffset, out int offset);
                foreach (var (_, frames, from) in _queued)
                {
                    if (offset < frames) return _feeder.Wrap(from + offset) / (double)_clip.SampleRate;
                    offset -= frames;
                }

                return _feeder.Wrap(_feeder.Next) / (double)_clip.SampleRate;
            }
        }
    }

    public void Load(PcmAudio clip)
    {
        lock (_gate)
        {
            if (_disposed) return;
            Halt();
            _clip = clip;
            _feeder = new PcmFeeder(clip) { Loop = Loop };
            _block = new short[(int)((long)clip.SampleRate * _bufferMs / 1000) * _feeder.Channels];
        }
    }

    public void Play(double start, double end)
    {
        lock (_gate)
        {
            if (_disposed || _feeder is null) return;
            Halt();
            _feeder.Begin(start, end);
            Start();
        }
    }

    public void Pause()
    {
        lock (_gate)
        {
            if (!_active) return;
            Use();
            _al.SourcePause(_source);
            _paused = true;
        }
    }

    public void Resume()
    {
        lock (_gate)
        {
            if (!_active || !_paused) return;
            Use();
            _paused = false;
            _al.SourcePlay(_source);
        }
    }

    public void Stop()
    {
        lock (_gate)
        {
            if (!_disposed) Halt();
        }
    }

    public void Seek(double seconds)
    {
        lock (_gate)
        {
            if (!_active || _feeder is null) return;
            var paused = _paused;
            Halt();
            _feeder.Seek(seconds);
            Start();
            if (paused) Pause();
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
                Halt(); // changes the timer, so the timer is disposed after it
                Use();
                _al.DeleteSource(_source);
                _al.DeleteBuffers(_buffers);
                _alc.MakeContextCurrent(null);
                _alc.DestroyContext(_context);
            }
            finally
            {
                try { _alc.CloseDevice(_device); }
                finally
                {
                    _pump.Dispose();
                    _al.Dispose();
                    _alc.Dispose();
                }
            }
        }
    }

    // Every call that touches the source makes the context of this output current first, so two outputs never mix up.
    private void Use() => _alc.MakeContextCurrent(_context);

    // Called with the lock held.
    private void Halt()
    {
        _pump.Change(Timeout.Infinite, Timeout.Infinite);
        _active = _paused = _drained = false;
        Use();
        _al.SourceStop(_source);
        _al.GetSourceProperty(_source, GetSourceInteger.BuffersQueued, out int queued);
        if (queued > 0)
        {
            var taken = new uint[queued];
            fixed (uint* at = taken) _al.SourceUnqueueBuffers(_source, queued, at);
        }

        while (_queued.Count > 0) _free.Push(_queued.Dequeue().Buffer);
        _al.SetSourceProperty(_source, SourceFloat.Gain, (float)Volume);
    }

    private void Start()
    {
        Use();
        (_active, _paused, _drained) = (true, false, false);
        Fill();
        if (_queued.Count == 0)
        {
            _active = false;
            return;
        }

        _al.SourcePlay(_source);
        _pump.Change(10, 10);
    }

    private void Fill()
    {
        if (_feeder is null || _clip is null) return;
        var format = _feeder.Channels == 1 ? BufferFormat.Mono16 : BufferFormat.Stereo16;
        while (_free.Count > 0 && !_drained)
        {
            var (frames, from) = _feeder.Read(_block, _block.Length / _feeder.Channels);
            if (frames == 0)
            {
                _drained = true;
                break;
            }

            var buffer = _free.Pop();
            fixed (short* data = _block) _al.BufferData(buffer, format, data, frames * _feeder.Channels * sizeof(short), _clip.SampleRate);
            _al.SourceQueueBuffers(_source, 1, &buffer);
            _queued.Enqueue((buffer, frames, from));
        }
    }

    private void Pump()
    {
        var ended = false;
        lock (_gate)
        {
            if (!_active || _paused) return;
            Use();
            _al.GetSourceProperty(_source, GetSourceInteger.BuffersProcessed, out int processed);
            for (; processed > 0; processed--)
            {
                uint buffer;
                _al.SourceUnqueueBuffers(_source, 1, &buffer);
                _free.Push(_queued.Dequeue().Buffer);
            }

            Fill();
            if (_queued.Count == 0)
            {
                Halt();
                ended = true;
            }
            else
            {
                _al.GetSourceProperty(_source, GetSourceInteger.SourceState, out int state);
                if ((SourceState)state != SourceState.Playing) _al.SourcePlay(_source); // the device ran dry before the refill
            }
        }

        if (ended) Ended?.Invoke();
    }
}
