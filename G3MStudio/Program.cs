using Avalonia;

namespace G3MStudio;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (args is ["--sentry-test", var resultFile]) return SentryCheck.Run(resultFile);
        // A file or link given to a later launch goes to the program that is already running, and this launch ends before it shows anything.
        if (!args.Contains("--smoke") && !args.Any(a => a.StartsWith("--memory-probe=", StringComparison.Ordinal)) && !args.Contains("--run") && !args.Contains("--run-with-runtime") && args.FirstOrDefault(a => !a.StartsWith("--", StringComparison.Ordinal)) is { } given && Core.Links.SingleInstance.TrySend(given)) return 0;
        var crashData = new Lazy<Core.Workspace.UserData>(Core.Workspace.UserData.Default);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            try
            {
                if (e.ExceptionObject is Exception crash) Core.Workspace.CrashLog.Write(crashData.Value, crash);
            }
            catch (Exception)
            {
                // the handler of a crash must never crash itself
            }
        };
        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>().UsePlatformDetect().LogToTrace();
}
