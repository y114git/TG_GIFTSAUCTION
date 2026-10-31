using Avalonia;

namespace G3MStudio;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        // A file or link given to a later launch goes to the program that is already running, and this launch ends before it shows anything.
        if (!args.Contains("--smoke") && args.FirstOrDefault(a => !a.StartsWith("--", StringComparison.Ordinal)) is { } given && Core.Links.SingleInstance.TrySend(given)) return 0;
        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>().UsePlatformDetect().LogToTrace();
}
