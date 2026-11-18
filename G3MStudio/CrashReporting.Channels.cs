using System.Globalization;
using System.Runtime.InteropServices;
using G3MStudio.Core.Settings;

namespace G3MStudio;

// The channels beyond errors: transactions, logs, feedback, attachments and the install id. Each one is cleaned here before the SDK sends it.
public static partial class CrashReporting
{
#if DEBUG
    private const bool Debug = true;
#else
    private const bool Debug = false;
#endif

    /// <summary>The environment a report belongs to: dev for a debug build, beta for a pre-release version (1.2.0-beta.1), release for the rest.</summary>
    internal static string EnvironmentFor(string version, bool debug) => debug ? "dev" : version.Contains('-', StringComparison.Ordinal) ? "beta" : "release";

    /// <summary>The random id of this install, or null before one was made. It is the Sentry user id and a tag, nothing else about the person.</summary>
    public static string? InstallId { get; private set; }

    /// <summary>Takes a new install id (null keeps the current one); a running session uses it at once.</summary>
    public static void SetInstallId(string? id)
    {
        if (id is null || !FailureReport.IsInstallId(id)) return;
        InstallId = id;
        if (!Active) return;
        SentrySdk.ConfigureScope(scope =>
        {
            scope.User = new SentryUser { Id = id };
            scope.SetTag("install-id", id);
        });
    }

    private static string? CleanOrNull(string? text) => text is null ? null : Clean(text);

    private static SentryTransaction? ScrubTransaction(SentryTransaction t, SentryHint hint)
    {
        if (!UsageOn) return null;
        t.User = new SentryUser { Id = InstallId };
        t.Contexts.Device.Name = null;
        foreach (var (key, value) in t.Tags.ToList()) if (!VersionTags.Contains(key)) t.SetTag(key, Clean(value));
        foreach (var (key, value) in t.Data.ToList()) if (value is string text) t.SetData(key, Clean(text));
        foreach (var span in t.Spans)
        {
            span.Description = CleanOrNull(span.Description);
            foreach (var (key, value) in span.Tags.ToList()) span.SetTag(key, Clean(value));
            foreach (var (key, value) in span.Data.ToList()) if (value is string text) span.SetData(key, Clean(text));
        }

        t.Request = new SentryRequest();
        return t;
    }

    // The text and the attributes of a log are cleaned where the log is made (Telemetry.Log); here only the switch is checked again.
    private static SentryLog? ScrubLog(SentryLog log) => UsageOn ? log : null;

    // The text is cleaned before it is sent (CrashScrub.Feedback), so what is left here is the rest of the report.
    private static SentryEvent? ScrubFeedback(SentryEvent e, SentryHint hint)
    {
        e.ServerName = null;
        e.User = e.User?.Id is null ? new SentryUser() : new SentryUser { Id = InstallId };
        e.Contexts.Device.Name = null;
        foreach (var (key, value) in e.Tags.ToList()) if (!VersionTags.Contains(key)) e.SetTag(key, Clean(value));
        return e;
    }

    /// <summary>The text of a small file that goes with a report: line by line cleaned, at most 16000 characters.</summary>
    internal static string AttachmentText(string text)
    {
        var cleaned = string.Join('\n', text.ReplaceLineEndings("\n").Split('\n').Select(Clean));
        return cleaned.Length > 16000 ? cleaned[..16000] : cleaned;
    }

    /// <summary>Sends the feedback the user wrote. The text is cleaned of paths, addresses, keys and prefixed names; the contact goes only when given; without diagnostics the report carries no tags or trail.</summary>
    public static CaptureFeedbackResult SendFeedback(string message, string? contact, bool diagnostics)
    {
        if (!Active) return CaptureFeedbackResult.DisabledHub;
        var feedback = new SentryFeedback(CrashScrub.Feedback(message.Trim()), string.IsNullOrWhiteSpace(contact) ? null : contact.Trim()[..Math.Min(contact.Trim().Length, 200)], null, null, null, null);
        SentrySdk.CaptureFeedback(feedback, out var result, scope =>
        {
            scope.SetTag("area", "feedback");
            if (diagnostics) return;
            scope.User = new SentryUser();
            foreach (var key in scope.Tags.Keys.ToList()) scope.UnsetTag(key);
            scope.ClearBreadcrumbs();
            scope.SetTag("area", "feedback");
        });
        return result;
    }

    /// <summary>The facts the Copy Diagnostics command puts on the clipboard: versions, install id, system, memory class and the names (no folders) of the newest crash and hang files.</summary>
    public static string DiagnosticsText(string? installId, string crashFolder)
    {
        var files = new List<string>();
        try
        {
            if (Directory.Exists(crashFolder)) files.AddRange(Directory.EnumerateFiles(crashFolder, "*.txt").Select(f => Path.GetFileName(f)).Order(StringComparer.Ordinal).TakeLast(5));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // an unreadable folder is left out
        }

        return string.Join('\n',
            $"G3MStudio {AppVersion} ({EnvironmentFor(AppVersion, Debug)})",
            $"G3MLib {FailureReport.G3MLibVersion}",
            $"Install ID: {installId ?? "none"}",
            $"{RuntimeInformation.FrameworkDescription}, {RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture})",
            $"Memory: {FailureReport.SizeRange(GC.GetGCMemoryInfo().TotalAvailableMemoryBytes)}, cores: {Environment.ProcessorCount.ToString(CultureInfo.InvariantCulture)}",
            $"Crash reports: {(Active ? "on" : "off")}, usage statistics: {(UsageOn ? "on" : "off")}",
            "Recent crash and hang files: " + (files.Count == 0 ? "none" : string.Join(", ", files)));
    }

    /// <summary>The program is closing: the usage of the session goes out as one line and the session ends as exited.</summary>
    public static void Shutdown()
    {
        try
        {
            Telemetry.SendSummary();
            if (_session is not null && SentrySdk.IsSessionActive) SentrySdk.EndSession(SessionEndStatus.Exited);
            Stop();
        }
        catch (Exception)
        {
            // closing must never fail because of a report
        }
    }
}
