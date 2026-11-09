namespace G3MStudio.Core.Workspace;

/// <summary>
/// Writes the layer stacks of a <see cref="LayerStore"/> a short time after the last committed edit, on a pool thread, so a crash loses at most the last moment of work
/// and the UI never waits for the picture encoding. Each edit restarts the delay; a burst of edits makes one write.
/// </summary>
public sealed class LayerAutosave : IDisposable
{
    /// <summary>Quiet time after the last edit before a write starts.</summary>
    public static readonly TimeSpan DefaultDelay = TimeSpan.FromMilliseconds(500);

    private readonly LayerStore _store;
    private readonly Action<Dictionary<string, string>> _write;
    private readonly TimeSpan _delay;
    private readonly Timer _timer;
    private readonly Lock _gate = new();
    private bool _disposed, _writing, _dirty, _failing;
    private int _writes, _failures;

    public LayerAutosave(LayerStore store, Action<Dictionary<string, string>> write, TimeSpan delay)
    {
        (_store, _write, _delay) = (store, write, delay);
        _timer = new Timer(_ => Flush(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        store.Changed += OnChanged;
    }

    /// <summary>Raised on the writing thread when a write fails, once per run of failures (the next success re-arms it). A lost write is retried with the next edit.</summary>
    public event Action<Exception>? Failed;

    /// <summary>How many writes finished.</summary>
    public int Writes => Volatile.Read(ref _writes);

    /// <summary>How many writes failed.</summary>
    public int Failures => Volatile.Read(ref _failures);

    /// <summary>False stops writing (the setting is off); turning it on writes the current stacks once.</summary>
    public bool Enabled
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            if (value) OnChanged();
            else lock (_gate) if (!_disposed) _timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }
    } = true;

    private void OnChanged()
    {
        if (!Enabled) return;
        lock (_gate)
        {
            if (_disposed) return;
            _dirty = true;
            _timer.Change(_delay, Timeout.InfiniteTimeSpan);
        }
    }

    /// <summary>
    /// Writes now on the calling thread. The picture export and the write run outside the gate, so edits are never blocked; an edit that arrives meanwhile
    /// arms the timer again. A call while another write runs does nothing (that write re-arms the timer if edits came in).
    /// </summary>
    public void Flush()
    {
        lock (_gate)
        {
            if (_disposed || _writing) return;
            (_writing, _dirty) = (true, false);
        }

        try
        {
            _write(_store.Export());
            Interlocked.Increment(ref _writes);
            _failing = false;
        }
        catch (Exception e)
        {
            // A write must never take the process down (the export can fail with a picture library error too): count it and tell the owner once.
            Interlocked.Increment(ref _failures);
            if (!_failing)
            {
                _failing = true;
                try
                {
                    Failed?.Invoke(e);
                }
                catch (Exception)
                {
                    // a handler that fails is not worth a crash either
                }
            }
        }
        finally
        {
            lock (_gate)
            {
                _writing = false;
                if (_dirty && !_disposed && Enabled) _timer.Change(_delay, Timeout.InfiniteTimeSpan);
            }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            _store.Changed -= OnChanged;
            _timer.Dispose();
        }

        // an in-flight write finishes before the owner tears the folder down; bounded so a stuck write cannot hang the close
        SpinWait.SpinUntil(() => { lock (_gate) return !_writing; }, TimeSpan.FromSeconds(5));
    }
}
