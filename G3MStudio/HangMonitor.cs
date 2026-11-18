using System.Diagnostics;
using System.Globalization;
using Avalonia.Threading;
using G3MStudio.Core.Settings;
using G3MStudio.Core.Workspace;

namespace G3MStudio;

/// <summary>
/// Watches the UI thread from a thread of its own. When the UI thread does not answer for <see cref="Limit"/>, the cause is written to a text file in the crashes folder
/// of the user data (what the thread is doing, whether it spins or waits, memory) and one problem report goes to Sentry when reporting is on. A frozen window gives the user
/// nothing to look at, so this is the only record of what froze it. The window is never touched: the monitor only reads.
/// </summary>
public sealed class HangMonitor : IDisposable
{
    public const int Keep = 10;

    private readonly UserData _data;
    private readonly Action<string, IReadOnlyDictionary<string, string>> _send;
    private readonly int _uiThread = Environment.CurrentManagedThreadId;
    private readonly ManualResetEventSlim _stop = new();
    private long _pendingSince; // stopwatch ticks of the beat the UI thread has not answered yet; 0 when none is waiting
    private TimeSpan _cpuAtBeat;

    /// <summary>Created on the UI thread: that thread is the one watched.</summary>
    public HangMonitor(UserData data, TimeSpan limit, TimeSpan tick, Action<string, IReadOnlyDictionary<string, string>>? send = null)
    {
        (_data, Limit, Tick) = (data, limit, tick);
        _send = send ?? ((message, tags) => FailureReport.ProblemWithText("ui.hang", message, tags, "hang.txt", message));
        new Thread(Watch) { IsBackground = true, Name = "G3MStudio hang monitor" }.Start();
    }

    /// <summary>Starts the monitor for the running app and stops it when the app exits. Call it on the UI thread.</summary>
    public static void Start(Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop, UserData data)
    {
#pragma warning disable CA2000 // owned by the exit handler below
        var monitor = new HangMonitor(data, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(1));
#pragma warning restore CA2000
        desktop.Exit += (_, _) => monitor.Dispose();
    }

    public TimeSpan Limit { get; }

    public TimeSpan Tick { get; }

    /// <summary>The newest hang file written, or null while the UI thread never stopped answering.</summary>
    public string? LastFile { get; private set; }

    private void Beat() => Interlocked.Exchange(ref _pendingSince, 0);

    private void Watch()
    {
        try { Loop(); }
        catch (ObjectDisposedException)
        {
            return; // the monitor was disposed while the thread was waiting
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            FailureReport.Problem("ui.hang.monitor", e.GetType().Name, new Dictionary<string, string>()); // the watcher stops; it must never take the app down
        }
    }

    private void Loop()
    {
        var reported = false;
        var recoveryTries = 0;
        var lastLoop = Stopwatch.GetTimestamp();
        while (!_stop.IsSet)
        {
            // A gap far longer than a tick means this thread was frozen too (a sleeping laptop, a suspended process): the waiting beat proves nothing, so start again.
            var gap = Stopwatch.GetElapsedTime(lastLoop);
            lastLoop = Stopwatch.GetTimestamp();
            if (gap > Limit && !reported) Interlocked.Exchange(ref _pendingSince, 0);
            var since = Interlocked.Read(ref _pendingSince);
            if (since == 0)
            {
                if (reported && !AppendRecovery() && ++recoveryTries < 5)
                {
                    if (_stop.Wait(Tick)) return; // a file that is open elsewhere is tried again at the next tick
                    continue;
                }

                reported = false;
                recoveryTries = 0;
                using (var process = Process.GetCurrentProcess()) _cpuAtBeat = process.TotalProcessorTime;
                Interlocked.Exchange(ref _pendingSince, Stopwatch.GetTimestamp());
                Dispatcher.UIThread.Post(Beat, DispatcherPriority.Normal);
            }
            else if (!reported && Stopwatch.GetElapsedTime(since) >= Limit)
            {
                reported = true;
                Report(Stopwatch.GetElapsedTime(since));
            }

            if (_stop.Wait(Tick)) return;
        }
    }

    private void Report(TimeSpan stuck)
    {
        try { WriteReport(stuck); }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException or NotSupportedException or System.Security.SecurityException or IOException or UnauthorizedAccessException)
        {
            _send("The UI thread did not answer; the report could not be completed: " + e.GetType().Name, new Dictionary<string, string> { ["hang"] = "unknown" });
        }
    }

    private void WriteReport(TimeSpan stuck)
    {
        using var process = Process.GetCurrentProcess();
        var cpu = process.TotalProcessorTime - _cpuAtBeat;
        var (stack, osThread) = Stack(_uiThread);
        var state = osThread == 0 ? "unknown (the thread could not be identified)" : Spinning(process, osThread) ? "spinning (the thread is busy)" : "waiting (a lock, a task or I/O that does not finish)";
        var text = string.Create(CultureInfo.InvariantCulture,
            $"The UI thread did not answer for {stuck.TotalSeconds:0.#} s: {state}.\nProcessor time in that window: {cpu.TotalSeconds:0.#} s. Memory: {process.WorkingSet64 / 1048576} MB. Threads: {process.Threads.Count}. GCs: {GC.CollectionCount(0)}/{GC.CollectionCount(1)}/{GC.CollectionCount(2)}.\n\nUI thread:\n{stack}\n");
        LastFile = WriteFile(text);
        _send(text.Length > 3500 ? text[..3500] : text, new Dictionary<string, string> { ["hang"] = state.Split(' ')[0] });
        Telemetry.Log(SentryLogLevel.Warning, "ui hang", ("state", state.Split(' ')[0]), ("seconds", ((int)stuck.TotalSeconds).ToString(CultureInfo.InvariantCulture)));
    }

    private bool AppendRecovery()
    {
        try
        {
            if (LastFile is not null) File.AppendAllText(LastFile, $"{Environment.NewLine}The UI thread answered again at {DateTime.UtcNow:O}.{Environment.NewLine}");
            Telemetry.Log(SentryLogLevel.Info, "ui hang recovered");
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException or System.Security.SecurityException)
        {
            return false;
        }
    }

    private string? WriteFile(string text)
    {
        try
        {
            var folder = Directory.CreateDirectory(_data.Combine("crashes")).FullName;
            var path = Path.Combine(folder, $"hang-{DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture)}-{Guid.NewGuid().ToString("N")[..4]}.txt");
            File.WriteAllText(path, $"G3MStudio {typeof(HangMonitor).Assembly.GetName().Version}\n{Environment.OSVersion}\n{DateTime.UtcNow:O}\n\n{text}");
            foreach (var old in Directory.EnumerateFiles(folder, "hang-*.txt").Order(StringComparer.Ordinal).SkipLast(Keep)) File.Delete(old);
            return path;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException or System.Security.SecurityException)
        {
            return null;
        }
    }

    /// <summary>Whether the thread used processor time during a short window now: a busy thread spins, one that does not waits. The processor time of the whole process says nothing about one thread.</summary>
    private static bool Spinning(Process process, uint osThread)
    {
        TimeSpan Used()
        {
            try { return process.Threads.Cast<ProcessThread>().FirstOrDefault(t => t.Id == osThread)?.TotalProcessorTime ?? TimeSpan.Zero; }
            catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException)
            {
                return TimeSpan.Zero; // the thread ended while it was read
            }
        }

        process.Refresh();
        var before = Used();
        Thread.Sleep(400);
        process.Refresh();
        return Used() - before > TimeSpan.FromMilliseconds(120);
    }

    /// <summary>The managed stack of the UI thread, read from a snapshot of this process (Windows). Elsewhere, or when the snapshot fails, the reason instead of a stack.</summary>
    private static (string Text, uint OsThread) Stack(int managedThreadId)
    {
        if (!OperatingSystem.IsWindows()) return ("(the stack can be read on Windows only)", 0);
        try
        {
            using var target = Microsoft.Diagnostics.Runtime.DataTarget.CreateSnapshotAndAttach(Environment.ProcessId);
            var runtime = target.ClrVersions.FirstOrDefault()?.CreateRuntime();
            var thread = runtime?.Threads.FirstOrDefault(t => t.ManagedThreadId == managedThreadId);
            return thread is null ? ("(the UI thread was not found in the snapshot)", 0) : (string.Join('\n', thread.EnumerateStackTrace().Take(60).Select(f => "  at " + f)), thread.OSThreadId);
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            return ($"(the stack could not be read: {e.GetType().Name})", 0);
        }
    }

    public void Dispose()
    {
        _stop.Set();
        _stop.Dispose();
    }
}
