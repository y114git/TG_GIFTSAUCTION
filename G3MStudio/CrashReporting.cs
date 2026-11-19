using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using Avalonia.Threading;
using G3MStudio.Core.Settings;
using G3MStudio.Core.Workspace;
using Sentry.Extensibility;
using Sentry.Protocol;

namespace G3MStudio;

/// <summary>
/// Crash reporting through Sentry (F-PRIV-1). The key is baked in by the release build (AssemblyMetadata "SentryDsn"); a build without one, the
/// switch off in Settings, or any failure in Sentry leaves the app exactly as it is. A report carries the exception with its full text, the stack, the app, G3MLib and system facts, the
/// area, tab and commands in use, a few settings and the format facts of the data file; every personal part (user, machine, IP address, e-mail, key, path
/// folders and file names, resource names) is replaced by <see cref="CrashScrub"/>, and the server is told nothing about the person.
/// Reached from here: an unhandled exception on any thread (the SDK's own handler), a task nobody observed (the SDK's), an unhandled exception on the
/// UI thread (<see cref="OnUiException"/>), and every failure the app survives (<see cref="FailureReport"/>).
/// </summary>
public static partial class CrashReporting
{
    private static IDisposable? _session;
    private static string? _cacheFolder;

    private static readonly IFailureSink Sink = new SentrySink();

    public static string? Dsn => Assembly.GetExecutingAssembly().GetCustomAttributes<AssemblyMetadataAttribute>().FirstOrDefault(a => a.Key == "SentryDsn")?.Value is { Length: > 0 } dsn ? dsn : null;

    /// <summary>True while events are being sent, which is when the setting is on and the key works.</summary>
    public static bool Active => _session is not null;

    /// <summary>The app version, without the build metadata after a plus sign.</summary>
    public static string AppVersion { get; } = (typeof(CrashReporting).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? typeof(CrashReporting).Assembly.GetName().Version?.ToString() ?? "unknown").Split('+')[0];

    /// <summary>Nothing is sent before the start page has been shown and closed; after that the crash report switch decides.</summary>
    public static bool ShouldReport(AppSettings settings) => settings.CrashReports && settings.OnboardingDone;

    /// <summary>True while usage statistics (transactions, logs, the session summary) may be sent: crash reporting is on and so is the usage switch.</summary>
    public static bool UsageOn { get; private set; }

    /// <summary>Starts or stops the reporting to match the settings; safe to call again whenever a setting changes, a running session is kept. A transport is for tests, which send nothing anywhere.</summary>
    public static void Apply(bool enabled, string? key = null, ITransport? transport = null, bool usage = true, string? installId = null)
    {
        InitFailure = null;
        SetInstallId(installId);
        try
        {
            if (!enabled || (key ?? Dsn) is not { } dsn)
            {
                Stop(clearCache: true);
                return;
            }

            UsageOn = usage;

            if (_session is not null)
            {
                Telemetry.Begin();
                return;
            }

            _session = SentrySdk.Init(options => Configure(options, dsn, transport));
            // an unusable key leaves the SDK switched off without an exception; say so, so a missing report can be explained
            if (!SentrySdk.IsEnabled) throw new InvalidOperationException("The crash report key was not accepted.");
            SentrySdk.ConfigureScope(scope =>
            {
                foreach (var (key, value) in SystemFacts()) scope.SetTag(key, value);
                scope.User = new SentryUser { Id = InstallId };
                if (InstallId is { } id) scope.SetTag("install-id", id);
            });
            FailureReport.Sink = Sink;
            Dispatcher.UIThread.UnhandledException += OnUiException;
            Telemetry.Begin();
        }
        catch (Exception e)
        {
            // Reporting must never be the reason the app fails.
            InitFailure = e;
            Stop();
        }
    }

    /// <summary>What the machine and the app are, without a name: versions, build, language, size classes of memory and processor.</summary>
    private static IEnumerable<KeyValuePair<string, string>> SystemFacts()
    {
        yield return new("app-version", AppVersion);
        yield return new("g3mlib.version", FailureReport.G3MLibVersion);
        yield return new("os", RuntimeInformation.OSDescription);
        yield return new("os-arch", RuntimeInformation.OSArchitecture.ToString());
        yield return new("runtime", RuntimeInformation.FrameworkDescription);
#if DEBUG
        yield return new("build", "Debug");
#else
        yield return new("build", "Release");
#endif
        yield return new("culture", CultureInfo.CurrentCulture.Name);
        yield return new("cpu-cores", Environment.ProcessorCount.ToString(CultureInfo.InvariantCulture));
        yield return new("memory", FailureReport.SizeRange(GC.GetGCMemoryInfo().TotalAvailableMemoryBytes));
    }

    /// <summary>A setting or a screen fact that helps to reproduce a failure (language, interface scale, theme, display scale): a flag, never a name or a path.</summary>
    public static void Describe(string key, string value)
    {
        if (Active) SentrySdk.ConfigureScope(scope => scope.SetTag(key, value));
    }

    /// <summary>The kind of tab in front (sprites, rooms, ...): a tag for the next report and a step of the trail.</summary>
    public static void TabChanged(string? type)
    {
        if (type is null) return;
        Describe("tab", type);
        Telemetry.Editor(type);
        FailureReport.Trail("tab", type);
    }

    // Stopping sends nothing more: the session ends as exited only in Shutdown. A user who switched reporting off also loses what still waits in the SDK's cache folder.
    private static void Stop(bool clearCache = false)
    {
        UsageOn = false;
        Telemetry.Begin();
        FailureReport.Sink = null;
        Dispatcher.UIThread.UnhandledException -= OnUiException;
        _session?.Dispose();
        _session = null;
        if (!clearCache || _cacheFolder is not { } folder) return;
        try
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // a locked cache is left for the next start
        }
    }

    private static void Configure(SentryOptions options, string dsn, ITransport? transport)
    {
        options.Dsn = dsn;
        options.Release = "g3mstudio@" + AppVersion;
        options.Environment = EnvironmentFor(AppVersion, Debug);
        options.SendDefaultPii = false;
        options.AutoSessionTracking = true; // release health: a session ends as exited, crashed or (found at the next start, from the cache folder) abnormal
        options.TracesSampleRate = Telemetry.TracesSampleRate;
        options.MaxAttachmentSize = 32 * 1024;
        options.MaxBreadcrumbs = 40;
        options.ServerName = null; // no machine name
        // a report that could not be sent (offline, firewall) waits in this folder and goes out at the next start
        if (transport is null)
        {
            options.CacheDirectoryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "G3MStudio");
            _cacheFolder = Path.Combine(options.CacheDirectoryPath, "Sentry");
            options.InitCacheFlushTimeout = TimeSpan.FromSeconds(3);
        }

        options.AttachStacktrace = true;
        options.IsGlobalModeEnabled = true; // the tags set once reach events from every thread, which includes the background workers
        if (transport is not null) options.Transport = transport;
        options.SetBeforeSend(Scrub);
        options.SetBeforeBreadcrumb(ScrubBreadcrumb);
        options.SetBeforeSendTransaction(ScrubTransaction);
        options.SetBeforeSendLog(ScrubLog);
        options.SetBeforeSendFeedback(ScrubFeedback);
    }

    /// <summary>Why reporting could not start, kept so a missing report can be explained.</summary>
    public static Exception? InitFailure { get; private set; }

    // An unhandled exception on the UI thread ends the app, so it is sent before that. A locked-resource refusal is shown to the user by the window, not a failure.
    private static void OnUiException(object? sender, DispatcherUnhandledExceptionEventArgs e)
    {
        if (e.Handled || e.Exception is ResourceLockedException || DiskFull.Is(e.Exception)) return;
        try
        {
            e.Exception.Data[Mechanism.HandledKey] = false;
            e.Exception.Data[Mechanism.MechanismKey] = "Avalonia.UIThread.UnhandledException";
            SentrySdk.CaptureException(e.Exception, scope => scope.SetTag("area", "ui.dispatcher"));
            SentrySdk.EndSession(SessionEndStatus.Crashed);
            SentrySdk.FlushAsync(TimeSpan.FromSeconds(2)).Wait(TimeSpan.FromSeconds(3));
        }
        catch (Exception)
        {
            // a report that fails must not hide the crash it was about
        }
    }

    private static string Clean(string? text) => CrashScrub.Clean(text);

    private static SentryEvent? Scrub(SentryEvent e, SentryHint hint)
    {
        e.ServerName = null;
        e.User = new SentryUser { Id = InstallId };
        e.Contexts.Device.Name = null;
        e.SetExtra("scrubbed", true);
        e.Message = e.Message is { } m ? new SentryMessage { Message = Clean(m.Message), Formatted = Clean(m.Formatted) } : null;
        foreach (var exception in e.SentryExceptions ?? [])
        {
            exception.Value = CrashScrub.Exception(exception.Value);
            ScrubFrames(exception.Stacktrace);
        }

        foreach (var thread in e.SentryThreads ?? []) ScrubFrames(thread.Stacktrace);
        // the loaded assemblies are listed with the full path of the install folder, which can sit in a user folder
        foreach (var image in e.DebugImages ?? [])
        {
            image.CodeFile = image.CodeFile is { } code ? CrashScrub.Text(code) : null;
            image.DebugFile = image.DebugFile is { } debug ? CrashScrub.Text(debug) : null;
        }

        // the version tags are numbers that look like addresses; every other tag is cleaned
        foreach (var (key, value) in e.Tags.ToList()) if (!VersionTags.Contains(key)) e.SetTag(key, Clean(value));
        foreach (var (key, value) in e.Extra.ToList()) if (value is string text) e.SetExtra(key, Clean(text));
        return e;
    }

    private static void ScrubFrames(SentryStackTrace? trace)
    {
        foreach (var frame in trace?.Frames ?? [])
        {
            frame.AbsolutePath = frame.AbsolutePath is { } absolute ? CrashScrub.Text(absolute) : null;
            frame.FileName = frame.FileName is { } file ? CrashScrub.Text(file) : null;
        }
    }

    private static readonly HashSet<string> VersionTags = ["app-version", "g3mlib.version", "data.version", "runtime", "os", "install-id"];

    private static Breadcrumb? ScrubBreadcrumb(Breadcrumb b) => new(Clean(b.Message), b.Type ?? "default", null, b.Category, b.Level);

    /// <summary>Sends a failure the app survived; nothing happens while reporting is off.</summary>
    private sealed class SentrySink : IFailureSink
    {
        public void Capture(Exception? exception, string area, string? message, IReadOnlyDictionary<string, string> tags) => Send(exception, area, message, tags, null);

        public void CaptureWithText(Exception? exception, string area, string? message, IReadOnlyDictionary<string, string> tags, string fileName, string text) => Send(exception, area, message, tags, (fileName, text));

        private static void Send(Exception? exception, string area, string? message, IReadOnlyDictionary<string, string> tags, (string Name, string Text)? file)
        {
            if (!Active) return;
            void Configure(Scope scope)
            {
                scope.SetTag("area", area);
                scope.SetTag("handled", "true");
                foreach (var (key, value) in tags) scope.SetTag(key, value);
                if (file is { } f) scope.AddAttachment(System.Text.Encoding.UTF8.GetBytes(AttachmentText(f.Text)), Path.GetFileName(Clean(f.Name)) is { Length: > 0 } name ? name : "report.txt", AttachmentType.Default, "text/plain");
            }

            if (exception is not null) SentrySdk.CaptureException(exception, Configure);
            else SentrySdk.CaptureMessage(message ?? area, Configure, SentryLevel.Error);
        }

        public void Breadcrumb(string category, string message)
        {
            if (Active) SentrySdk.AddBreadcrumb(Clean(message), category, level: BreadcrumbLevel.Error);
        }
    }
}
