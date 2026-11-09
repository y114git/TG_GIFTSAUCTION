using G3MStudio.Core.Notifications;

namespace G3MStudio.Core.Workspace;

public enum MemoryLevel { Normal, High, Critical }

/// <summary>Bytes in use by the managed heap and by the process, and what the system still has free.</summary>
public readonly record struct MemorySample(long HeapBytes, long WorkingSetBytes, long SystemFreeBytes);

public interface IMemorySource
{
    MemorySample Read();
}

/// <summary>Reads the runtime. With a heap limit set, the system numbers the collector reports are relative to that limit, which is the budget anyway.</summary>
public sealed class GcMemorySource : IMemorySource
{
    public MemorySample Read()
    {
        var info = GC.GetGCMemoryInfo();
        return new MemorySample(GC.GetTotalMemory(false), Environment.WorkingSet, Math.Max(0, info.TotalAvailableMemoryBytes - info.MemoryLoadBytes));
    }
}

/// <summary>
/// The adaptive low-memory system. It polls a memory source, works out how much of the budget is used (the limit from Settings, or the free memory of the system
/// when there is none), and moves between Normal, High and Critical with hysteresis so a reading near a border does not flap. Moving up runs the release actions
/// registered for the levels crossed (rebuildable caches only, never edits or undo history) and trims the collector. Optional work checks <see cref="IsLow"/>.
/// </summary>
public sealed class MemoryGovernor : IDisposable
{
    public const double HighAt = 0.80, HighOff = 0.72, CriticalAt = 0.92, CriticalOff = 0.86;

    /// <summary>While the level stays up, the release actions run again every this many polls, because caches fill again.</summary>
    private const int RepeatEvery = 10;

    private readonly IMemorySource _source;
    private readonly TimeSpan _interval;
    private readonly Action<Action> _marshal;
    private readonly List<(MemoryLevel At, Action Release)> _releases = [];
    private readonly Lock _gate = new();
    private Timer? _timer;
    private int _held;

    /// <param name="source">Where the numbers come from; tests give a fake.</param>
    /// <param name="interval">Time between polls once started; default 2 seconds.</param>
    /// <param name="marshal">Runs the level-change event, for example on the UI thread; default is the polling thread.</param>
    public MemoryGovernor(IMemorySource source, TimeSpan? interval = null, Action<Action>? marshal = null)
    {
        _source = source;
        _interval = interval ?? TimeSpan.FromSeconds(2);
        _marshal = marshal ?? (a => a());
    }

    /// <summary>The budget in bytes from the Settings limit; 0 means none (all free memory).</summary>
    public long LimitBytes { get; set; }

    public MemoryLevel Level { get; private set; }

    /// <summary>Used share of the budget, 0 to 1 and a bit over; the larger of the heap against the limit and the process against the free memory.</summary>
    public double Pressure { get; private set; }

    public MemorySample Last { get; private set; }

    /// <summary>Critical: optional work (prefetch, previews, thumbnails, rename index) should not start.</summary>
    public bool IsLow => Level == MemoryLevel.Critical;

    /// <summary>Called after a trim; the default collects and compacts. Tests replace it.</summary>
    public Action Trim { get; set; } = TrimCollector;

    public event Action<MemoryLevel, MemoryLevel>? LevelChanged;

    /// <summary>Runs <paramref name="release"/> when the level reaches <paramref name="at"/> or higher. It runs on the polling thread and must be safe there.</summary>
    public void OnPressure(MemoryLevel at, Action release)
    {
        lock (_gate) _releases.Add((at, release));
    }

    public void Start()
    {
        lock (_gate) _timer ??= new Timer(_ => Poll(), null, _interval, _interval);
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _timer?.Dispose();
            _timer = null;
        }
    }

    /// <summary>Reads the source once and reacts. The timer calls it; tests call it directly.</summary>
    public void Poll()
    {
        MemoryLevel before, after;
        lock (_gate)
        {
            Last = _source.Read();
            Pressure = Compute(Last, LimitBytes);
            before = Level;
            after = Next(before, Pressure);
            Level = after;
            if (after > before)
            {
                _held = 0;
                Release(r => r.At > before && r.At <= after);
            }
            else if (after != MemoryLevel.Normal && after == before && ++_held >= RepeatEvery)
            {
                _held = 0;
                Release(r => r.At <= after);
            }
        }

        if (after != before) _marshal(() => LevelChanged?.Invoke(before, after));
    }

    /// <summary>Frees everything that can be rebuilt, whatever the level; used before the second try of an operation that ran out of memory.</summary>
    public void Relieve()
    {
        lock (_gate) Release(_ => true);
    }

    private void Release(Func<(MemoryLevel At, Action Release), bool> pick)
    {
        foreach (var item in _releases.Where(pick).ToList())
        {
            try
            {
                item.Release();
            }
            catch (Exception e) when (e is not OutOfMemoryException) // runs on a timer thread, where any escape ends the process
            {
                // A cache that cannot be released now is released at the next round.
            }
        }

        Trim();
    }

    public static double Compute(MemorySample sample, long limitBytes)
    {
        var heap = limitBytes > 0 ? (double)sample.HeapBytes / limitBytes : 0;
        var total = sample.WorkingSetBytes + sample.SystemFreeBytes;
        var system = total > 0 ? (double)sample.WorkingSetBytes / total : 0;
        return Math.Max(heap, system);
    }

    /// <summary>Hysteresis: the borders to fall are lower than the borders to rise, so a reading that hovers near one does not flip the mode every poll.</summary>
    public static MemoryLevel Next(MemoryLevel current, double pressure) => current switch
    {
        MemoryLevel.Critical when pressure >= CriticalOff => MemoryLevel.Critical,
        MemoryLevel.Critical or MemoryLevel.High when pressure >= CriticalAt => MemoryLevel.Critical,
        MemoryLevel.Critical or MemoryLevel.High when pressure >= HighOff => MemoryLevel.High,
        MemoryLevel.Normal when pressure >= CriticalAt => MemoryLevel.Critical,
        MemoryLevel.Normal when pressure >= HighAt => MemoryLevel.High,
        _ => MemoryLevel.Normal,
    };

    private static void TrimCollector()
    {
        System.Runtime.GCSettings.LargeObjectHeapCompactionMode = System.Runtime.GCLargeObjectHeapCompactionMode.CompactOnce;
        GC.Collect(2, GCCollectionMode.Forced, true, true);
    }
}

/// <summary>Posts one warning when the level becomes Critical and takes it back when the pressure drops below it.</summary>
public sealed class LowMemoryNotice
{
    private readonly NotificationCenter _center;
    private readonly Func<string> _text;
    private Notification? _shown;

    public LowMemoryNotice(MemoryGovernor governor, NotificationCenter center, Func<string> text)
    {
        (_center, _text) = (center, text);
        governor.LevelChanged += (_, now) =>
        {
            if (now == MemoryLevel.Critical && _shown is null) _shown = _center.Post(NotificationLevel.Warning, _text());
            else if (now != MemoryLevel.Critical && _shown is not null)
            {
                _center.Dismiss(_shown.Id);
                _shown = null;
            }
        };
    }
}

public static class MemoryLimit
{
    private static ulong _applied;

    /// <summary>
    /// Sets the heap hard limit of the running collector (0 = none) with GC.RefreshMemoryLimit. Returns null when it took effect, or the runtime's reason when it
    /// refused (the limit is invalid or below what is already committed); the caller still uses the number as its budget.
    /// </summary>
    public static string? Apply(int megabytes, Action? refresh = null)
    {
        var bytes = (ulong)Math.Max(0, megabytes) * 1048576UL;
        if (bytes == _applied) return null; // nothing was ever set: the default user never touches the collector
        try
        {
            AppContext.SetData("GCHeapHardLimit", bytes);
            (refresh ?? GC.RefreshMemoryLimit)();
            _applied = bytes;
            return null;
        }
        catch (Exception e) when (e is InvalidOperationException or ArgumentException)
        {
            return e.Message;
        }
    }

    /// <summary>
    /// Runs <paramref name="action"/>; when it runs out of memory, <paramref name="relieve"/> frees what it can and the action runs once more.
    /// A second failure is thrown for the caller to report.
    /// </summary>
    public static async Task<T> RetryOnOutOfMemoryAsync<T>(Func<Task<T>> action, Action relieve)
    {
        try
        {
            return await action();
        }
        catch (OutOfMemoryException)
        {
            relieve();
            return await action();
        }
    }

    public static T RetryOnOutOfMemory<T>(Func<T> action, Action relieve)
    {
        try
        {
            return action();
        }
        catch (OutOfMemoryException)
        {
            relieve();
            return action();
        }
    }
}
