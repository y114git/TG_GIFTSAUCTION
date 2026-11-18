using System.Diagnostics;
using System.Globalization;
using G3MStudio.Core.Settings;

namespace G3MStudio;

/// <summary>A timed step that goes out as one Sentry transaction while usage statistics are on; the same object does nothing while they are off.</summary>
public sealed class TelemetrySpan : IDisposable
{
    internal static readonly TelemetrySpan None = new(null);

    private readonly ITransactionTracer? _transaction;

    internal TelemetrySpan(ITransactionTracer? transaction) => _transaction = transaction;

    /// <summary>A generic fact about the step (an editor kind, a size class); never a name or a path.</summary>
    public TelemetrySpan Tag(string key, string value)
    {
        _transaction?.SetTag(key, value);
        return this;
    }

    public TelemetrySpan Value(string name, double value, MeasurementUnit unit = default)
    {
        _transaction?.SetMeasurement(name, value, unit);
        return this;
    }

    public void Dispose() => _transaction?.Finish();
}

/// <summary>
/// Anonymous usage statistics (F-PRIV-1): timed steps as transactions, structured logs, and one summary per session of which commands and editors were used.
/// Everything is a count, a class, a duration or a name from the app's own lists. Nothing is created while crash reporting or the usage switch is off.
/// </summary>
public static class Telemetry
{
    /// <summary>The share of steps that are sent; the free plan allows far more transactions than a beta produces.</summary>
    public const double TracesSampleRate = 1.0;

    private static readonly Lock Gate = new();
    private static readonly Dictionary<string, int> Commands = [];
    private static readonly Dictionary<string, int> Editors = [];
    private static readonly Dictionary<string, string> Facts = [];
    private static HashSet<string> _knownCommands = [];
    private static int _files;
    private static bool _begun;
    private static (double Window, double? Ready)? _startup;
    private static DateTime _sessionStart = DateTime.UtcNow;

    // Only these keys leave: the values are settings classes and the answers of the welcome window.
    private static readonly HashSet<string> FactKeys = ["language", "theme", "scale", "undo-limit", "screen", "onboarding.language", "onboarding.accent", "onboarding.associate", "onboarding.crash", "onboarding.usage"];

    /// <summary>A step to time: <c>using var t = Telemetry.Measure("file.save", "file")</c>. A no-op object while usage statistics are off.</summary>
    public static TelemetrySpan Measure(string name, string op) => CrashReporting.UsageOn ? new TelemetrySpan(SentrySdk.StartTransaction(name, op)) : TelemetrySpan.None;

    /// <summary>The command ids of the app itself; a plugin command with a name of its own is never counted.</summary>
    public static void KnowCommands(IEnumerable<string> ids)
    {
        lock (Gate) _knownCommands = [.. ids];
    }

    public static void Command(string id)
    {
        if (!CrashReporting.UsageOn) return;
        lock (Gate)
        {
            if (_knownCommands.Contains(id)) Commands[id] = Commands.GetValueOrDefault(id) + 1;
        }
    }

    /// <summary>A tab of this kind came to the front (sprites, rooms, ...): feature adoption per editor.</summary>
    public static void Editor(string type)
    {
        if (!CrashReporting.UsageOn || !IsEditorClass(type)) return;
        lock (Gate) Editors[type] = Editors.GetValueOrDefault(type) + 1;
    }

    /// <summary>The editor kind as a tag value: letters only and short; anything else (a path, an id) becomes "other".</summary>
    public static string EditorClass(string type) => IsEditorClass(type) ? type : "other";

    private static bool IsEditorClass(string type) => type.Length is > 0 and <= 24 && type.All(char.IsAsciiLetter);

    public static void FileOpened()
    {
        if (CrashReporting.UsageOn) lock (Gate) _files++;
    }

    /// <summary>A setting value or a welcome window answer for the summary; keys outside the list are ignored.</summary>
    public static void Note(string key, string value)
    {
        if (!CrashReporting.UsageOn || !FactKeys.Contains(key)) return;
        lock (Gate) Facts[key] = CrashScrub.Clean(value.Length > 40 ? value[..40] : value);
    }

    public static double SinceProcessStart()
    {
        using var process = Process.GetCurrentProcess();
        return (DateTime.Now - process.StartTime).TotalMilliseconds;
    }

    /// <summary>Milliseconds from the start of the process until the window was shown, and until the start page was ready (null when a file was opened instead).</summary>
    public static void Startup(double windowMs, double? readyMs)
    {
        _startup = (windowMs, readyMs);
        Begin();
    }

    /// <summary>Sends what waits for reporting to start: the start-up time and the start of the session in the log, once per time usage goes on. Safe to call again; while usage is off it only re-arms.</summary>
    internal static void Begin()
    {
        if (!CrashReporting.UsageOn)
        {
            _begun = false;
            return;
        }

        if (!_begun)
        {
            _begun = true;
            lock (Gate) _sessionStart = DateTime.UtcNow;
            Log(SentryLogLevel.Info, "app started");
        }

        if (_startup is not { } startup) return;
        _startup = null;
        using var span = Measure("process.startup", "process.start");
        span.Value("process_to_window", startup.Window, MeasurementUnit.Duration.Millisecond);
        if (startup.Ready is { } ready) span.Value("process_to_start_page", ready, MeasurementUnit.Duration.Millisecond);
    }

    /// <summary>A structured log line: a fixed text and short facts. The texts of exceptions and names never go in here.</summary>
    public static void Log(SentryLogLevel level, string message, params (string Key, string Value)[] attributes)
    {
        if (!CrashReporting.UsageOn) return;
        void Set(SentryLog log)
        {
            foreach (var (key, value) in attributes) log.SetAttribute(key, CrashScrub.Clean(value));
        }

        var text = CrashScrub.Clean(message).Replace("{", "(", StringComparison.Ordinal).Replace("}", ")", StringComparison.Ordinal);
        switch (level)
        {
            case SentryLogLevel.Warning: SentrySdk.Logger.LogWarning(Set, text); break;
            case SentryLogLevel.Error: SentrySdk.Logger.LogError(Set, text); break;
            default: SentrySdk.Logger.LogInfo(Set, text); break;
        }
    }

    /// <summary>One log line for the whole session: what was used, how often, for how long. Counters start again afterwards.</summary>
    public static void SendSummary()
    {
        if (!CrashReporting.UsageOn) return;
        List<(string, string)> attributes;
        lock (Gate)
        {
            attributes =
            [
                ("session-minutes", Math.Round((DateTime.UtcNow - _sessionStart).TotalMinutes).ToString(CultureInfo.InvariantCulture)),
                ("files-opened", FailureReport.CountRange(_files)),
                ("commands", Join(Commands)),
                ("editors", Join(Editors)),
                .. Facts.Select(f => (f.Key, f.Value)),
            ];
            Commands.Clear();
            Editors.Clear();
            _files = 0;
        }

        Log(SentryLogLevel.Info, "session summary", [.. attributes]);
    }

    private static string Join(Dictionary<string, int> counts) => string.Join(',', counts.OrderBy(c => c.Key, StringComparer.Ordinal).Select(c => $"{c.Key}={c.Value}"));
}
