using G3MLib.DataFile;
using G3MStudio.Core.Notifications;

namespace G3MStudio.Core.Settings;

/// <summary>Where the failures the app survives are sent when crash reporting is on (F-PRIV-1). The app sets it; Core code only reports to it.</summary>
public interface IFailureSink
{
    void Capture(Exception? exception, string area, string? message, IReadOnlyDictionary<string, string> tags);

    void Breadcrumb(string category, string message);

    /// <summary>Like <see cref="Capture"/>, with a text file that goes along (already scrubbed by the sink); a sink without attachments sends the report alone.</summary>
    void CaptureWithText(Exception? exception, string area, string? message, IReadOnlyDictionary<string, string> tags, string fileName, string text) => Capture(exception, area, message, tags);
}

/// <summary>
/// The one place Core and the editors report a failure from: an exception that was caught and handled, a data file that failed its own check, a read or write
/// that G3MLib refused. Nothing happens while no sink is set, which is whenever the switch in Settings is off. Tags carry sizes and versions, never names or paths.
/// </summary>
public static class FailureReport
{
    public static IFailureSink? Sink { get; set; }

    public static void Report(Exception exception, string area, IReadOnlyDictionary<string, string>? tags = null) => Send(sink => sink.Capture(exception, area, null, tags ?? Empty));

    /// <summary>A failure with a text that goes along as a small file (the hang report); the sink scrubs and caps it.</summary>
    public static void ProblemWithText(string area, string message, IReadOnlyDictionary<string, string> tags, string fileName, string text) => Send(sink => sink.CaptureWithText(null, area, message, tags, fileName, text));

    /// <summary>For an exception filter: reports the exception and answers false, so the catch clause never runs and the exception goes on as it did.</summary>
    public static bool Seen(Exception exception, string area)
    {
        Report(exception, area);
        return false;
    }

    /// <summary>A failure that has no exception, such as a written file that did not read back the same.</summary>
    public static void Problem(string area, string message, IReadOnlyDictionary<string, string>? tags = null) => Send(sink => sink.Capture(null, area, message, tags ?? Empty));

    /// <summary>A step of what the user was doing (a command name, a tab type): a trail for the next report. Never a resource name or a path.</summary>
    public static void Trail(string category, string step) => Send(sink => sink.Breadcrumb(category, step));

    /// <summary>An error the user was shown: only a trail for the next report. The text is not sent, since a formatted message may hold a name that is not quoted.</summary>
    public static void Notified(NotificationLevel level)
    {
        if (level == NotificationLevel.Error) Send(sink => sink.Breadcrumb("notification", "error shown"));
    }

    /// <summary>The size of the data file as a power-of-two range in MB, and the G3MLib version: enough to group reports, too coarse to name a game.</summary>
    public static IReadOnlyDictionary<string, string> FileTags(string path, GameMakerData? data = null)
    {
        var tags = new Dictionary<string, string> { ["g3mlib.version"] = G3MLibVersion };
        try
        {
            tags["file.size"] = SizeRange(new FileInfo(path).Length);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            tags["file.size"] = "unknown";
        }

        if (data?.GeneralInfo is { } info) tags["data.version"] = $"{info.Major}.{info.Minor}.{info.Release}.{info.Build}/bytecode {info.BytecodeVersion}";
        return tags;
    }

    public static string G3MLibVersion { get; } = typeof(GameMakerIO).Assembly.GetName().Version?.ToString() ?? "unknown";

    public static string SizeRange(long bytes)
    {
        if (bytes < 0) return "unknown";
        var low = 1L;
        while (low * 2 * 1048576L <= bytes) low *= 2;
        return bytes < 1048576L ? "<1MB" : $"{low}-{low * 2}MB";
    }

    /// <summary>A count as a coarse class (0, 1-9, 10-99, 100-999, 1k-9k, 10k+): enough to compare sessions, too coarse to name a game.</summary>
    public static string CountRange(long count) => count switch
    {
        < 0 => "unknown",
        0 => "0",
        < 10 => "1-9",
        < 100 => "10-99",
        < 1000 => "100-999",
        < 10000 => "1k-9k",
        _ => "10k+",
    };

    /// <summary>The kind of install id a report may carry: 32 lower-case hex digits and nothing else.</summary>
    public static string NewInstallId() => Guid.NewGuid().ToString("N");

    public static bool IsInstallId(string? id) => id is { Length: 32 } && id.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static readonly IReadOnlyDictionary<string, string> Empty = new Dictionary<string, string>();

    // Reporting must never be the reason a handled failure turns into an unhandled one.
    private static void Send(Action<IFailureSink> send)
    {
        if (Sink is not { } sink) return;
        try
        {
            send(sink);
        }
        catch (Exception)
        {
            // a sink that fails reports nothing
        }
    }
}
