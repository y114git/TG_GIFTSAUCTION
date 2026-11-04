using System.Reflection;
using G3MStudio.Core.Settings;

namespace G3MStudio;

/// <summary>
/// Crash reporting through Sentry (F-PRIV-1). The key is baked in by the release build (AssemblyMetadata "SentryDsn"); a build without one, the
/// switch off in Settings, or any failure in Sentry leaves the app exactly as it is. Reports carry no user name, IP address or file content.
/// </summary>
public static class CrashReporting
{
    private static IDisposable? _session;

    public static string? Dsn => Assembly.GetExecutingAssembly().GetCustomAttributes<AssemblyMetadataAttribute>().FirstOrDefault(a => a.Key == "SentryDsn")?.Value is { Length: > 0 } dsn ? dsn : null;

    /// <summary>Starts or stops the reporting to match the setting; safe to call again whenever the setting changes.</summary>
    public static void Apply(bool enabled, string? key = null)
    {
        try
        {
            if (!enabled || (key ?? Dsn) is not { } dsn)
            {
                _session?.Dispose();
                _session = null;
                return;
            }

            if (_session is not null) return;
            _session = SentrySdk.Init(options =>
            {
                options.Dsn = dsn;
                options.SendDefaultPii = false;
                options.AutoSessionTracking = false;
                options.MaxBreadcrumbs = 0;
                options.AttachStacktrace = true;
                options.SetBeforeSend(Scrub);
            });
        }
        catch (Exception e)
        {
            // Reporting must never be the reason the app fails.
            InitFailure = e;
            _session = null;
        }
    }

    /// <summary>Why reporting could not start, kept so a missing report can be explained.</summary>
    public static Exception? InitFailure { get; private set; }

    private static SentryEvent? Scrub(SentryEvent e, SentryHint hint)
    {
        e.ServerName = null;
        e.User = new SentryUser();
        e.SetExtra("scrubbed", true);
        e.Message = e.Message is { } m ? new SentryMessage { Message = CrashScrub.Text(m.Message), Formatted = CrashScrub.Text(m.Formatted) } : null;
        foreach (var exception in e.SentryExceptions ?? [])
        {
            exception.Value = CrashScrub.Text(exception.Value);
            foreach (var frame in exception.Stacktrace?.Frames ?? [])
            {
                frame.AbsolutePath = null;
                frame.FileName = null;
            }
        }

        return e;
    }
}
