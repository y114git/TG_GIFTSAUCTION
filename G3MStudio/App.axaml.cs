using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using G3MStudio.Core.Links;

namespace G3MStudio;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var smoke = desktop.Args?.Contains("--smoke") == true;
            var window = new MainWindow();
            desktop.MainWindow = window;
            if (!smoke) ListenForLaunches(desktop, window);

            window.OpenStartupFile(desktop.Args ?? []);
            if (desktop.Args?.Contains("--smoke") != true) window.Opened += (_, _) => window.RunFirstLaunch();
            // CI smoke run: show the window, wait, exit 0 only if it is still visible.
            if (desktop.Args?.Contains("--smoke") == true)
                DispatcherTimer.RunOnce(() => desktop.Shutdown(window.IsVisible ? 0 : 1), TimeSpan.FromSeconds(2));
        }

        base.OnFrameworkInitializationCompleted();
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
