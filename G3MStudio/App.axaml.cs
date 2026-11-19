using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using G3MStudio.Core.Links;
using G3MStudio.Core.Workspace;

namespace G3MStudio;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        Theme.FluentForwards.Apply();
        Theme.PopupScale.Follow();
        Docking.DockTexts.Follow();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var smoke = desktop.Args?.Contains("--smoke") == true;
            var window = new MainWindow();
            desktop.MainWindow = window;
            if (!smoke) ListenForLaunches(desktop, window);
            if (!smoke) RegisterWithSystem();
            if (!smoke) HangMonitor.Start(desktop, UserData.Default()); // here, on the UI thread, which is the one it watches

            var probe = desktop.Args?.FirstOrDefault(a => a.StartsWith("--memory-probe=", StringComparison.Ordinal));
            if (probe is null) window.OpenStartupFile(desktop.Args ?? []);
            else window.Opened += (_, _) => _ = window.RunMemoryProbeAsync(probe["--memory-probe=".Length..], (desktop.Args ?? []).FirstOrDefault(a => !a.StartsWith("--", StringComparison.Ordinal)), () => desktop.Shutdown());
            var givenFile = desktop.Args?.Any(a => !a.StartsWith("--", StringComparison.Ordinal)) == true;
            if (!smoke)
            {
                desktop.Exit += (_, _) => CrashReporting.Shutdown();
                window.Opened += (_, _) =>
                {
                    var shown = Telemetry.SinceProcessStart();
                    window.RunFirstLaunch();
                    _ = ReadyAsync(window, givenFile, shown);
                };
            }
            // CI smoke run: show the window, wait, exit 0 only if it is still visible.
            if (desktop.Args?.Contains("--smoke") == true)
                DispatcherTimer.RunOnce(() => desktop.Shutdown(window.IsVisible ? 0 : 1), TimeSpan.FromSeconds(2));
        }

        base.OnFrameworkInitializationCompleted();
    }

    // The start page is ready when the session start (recent files, the last session) is done; a file given on the command line has no start page.
    private static async Task ReadyAsync(MainWindow window, bool givenFile, double shown)
    {
        if (!givenFile) await window.StartSessionAsync();
        Telemetry.Startup(shown, givenFile ? null : Telemetry.SinceProcessStart());
    }

    /// <summary>Every start registers g3mstudio:// links and the "Open with" entries for the current program, in the background. G3MSTUDIO_NO_REGISTRATION=1 skips it (automated runs).</summary>
    private static void RegisterWithSystem()
    {
        // Ensure itself refuses a path that is not the program (the dotnet host of "dotnet G3MStudio.dll", a bin\Debug output).
        if (Environment.GetEnvironmentVariable("G3MSTUDIO_NO_REGISTRATION") == "1" || Environment.ProcessPath is not { } exe) return;
        _ = Task.Run(() => LinkRegistration.Ensure(exe, Places.Real(Core.Workspace.UserData.Default())));
    }

    /// <summary>Takes the files and links that later launches hand over, for as long as the program runs.</summary>
    private static void ListenForLaunches(IClassicDesktopStyleApplicationLifetime desktop, MainWindow window)
    {
#pragma warning disable CA2000 // it lives until the program exits, where the handler below disposes it
        var instance = new SingleInstance();
#pragma warning restore CA2000
        if (!instance.Listen(text => Dispatcher.UIThread.Post(() =>
            {
                window.OpenStartupFile([text]);
                window.Activate();
            })))
        {
            instance.Dispose();
            return;
        }

        desktop.Exit += (_, _) => instance.Dispose();
    }
}
