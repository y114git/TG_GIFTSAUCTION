using G3MStudio.Core.Audio;

namespace G3MStudio.Tests;

/// <summary>A microphone for tests: it delivers only what <see cref="Feed"/> or <see cref="Tone"/> put in while it is started, and can be made to fail, so nothing touches hardware.</summary>
public sealed class FakeAudioCapture : IAudioCapture
{
    private readonly Queue<short> _queue = new();
    private CaptureFailure? _fail;

    public bool IsCapturing { get; private set; }

    public bool Disposed { get; private set; }

    public int Starts { get; private set; }

    /// <summary>Runs inside <see cref="Start"/> once the device is started (a test blocks here to model a slow driver) and inside <see cref="Stop"/> before it stops (a test feeds the last samples here).</summary>
    public Action? OnStarted { get; set; }

    public Action? OnStopping { get; set; }

    public void Start()
    {
        ObjectDisposedException.ThrowIf(Disposed, this);
        if (_fail is { } failure) throw new CaptureException(failure);
        (IsCapturing, Starts) = (true, Starts + 1);
        OnStarted?.Invoke();
    }

    public void Stop()
    {
        OnStopping?.Invoke();
        IsCapturing = false;
    }

    /// <summary>Samples arriving from the microphone; they are dropped while the device is not started, as a stopped device would.</summary>
    public void Feed(params ReadOnlySpan<short> samples)
    {
        if (!IsCapturing) return;
        foreach (var sample in samples) _queue.Enqueue(sample);
    }

    /// <summary>A sine of <paramref name="hz"/> at <paramref name="amplitude"/> (0 to 1 of full scale) lasting <paramref name="seconds"/>.</summary>
    public void Tone(double seconds, double amplitude = 0.5, double hz = 440)
    {
        var frames = (int)Math.Round(seconds * IAudioCapture.SampleRate);
        var block = new short[frames];
        for (var i = 0; i < frames; i++) block[i] = (short)Math.Round(Math.Clamp(amplitude, 0, 1) * short.MaxValue * Math.Sin(2 * Math.PI * hz * i / IAudioCapture.SampleRate));
        Feed(block);
    }

    /// <summary>The next <see cref="Read"/> (and <see cref="Start"/>) fails with this reason, as an unplugged or refused device would.</summary>
    public void Fail(CaptureFailure failure) => _fail = failure;

    public int Read(Span<short> buffer)
    {
        if (_fail is { } failure) throw new CaptureException(failure);
        if (Disposed) throw new CaptureException(CaptureFailure.Lost);
        var count = Math.Min(buffer.Length, _queue.Count);
        for (var i = 0; i < count; i++) buffer[i] = _queue.Dequeue();
        return count;
    }

    public void Dispose() => (Disposed, IsCapturing) = (true, false);
}

/// <summary>A source for tests: a fixed list of names and the fake device each open returns, so the test can feed it.</summary>
public sealed class FakeCaptureSource : IAudioCaptureSource
{
    public List<string> Names { get; } = [];

    public string Default { get; set; } = "";

    public CaptureFailure? Problem { get; set; }

    public CaptureFailure? OpenFails { get; set; }

    public FakeAudioCapture? Device { get; private set; }

    /// <summary>The names that were asked for, in order (empty is the default).</summary>
    public List<string> Opened { get; } = [];

    /// <summary>Runs inside <see cref="List"/> (a test blocks here to model a slow driver).</summary>
    public Action? OnList { get; set; }

    public CaptureDevices List()
    {
        OnList?.Invoke();
        return new([.. Names], Default, Problem);
    }

    public IAudioCapture Open(string device)
    {
        if (OpenFails is { } failure) throw new CaptureException(failure);
        Opened.Add(device);
        return Device = new FakeAudioCapture();
    }
}
