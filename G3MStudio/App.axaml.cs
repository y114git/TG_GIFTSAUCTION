using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

namespace G3MStudio;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new MainWindow();
            desktop.MainWindow = window;
            window.OpenStartupFile(desktop.Args ?? []);
            // CI smoke run: show the window, wait, exit 0 only if it is still visible.
            if (desktop.Args?.Contains("--smoke") == true)
                DispatcherTimer.RunOnce(() => desktop.Shutdown(window.IsVisible ? 0 : 1), TimeSpan.FromSeconds(2));
        }

        base.OnFrameworkInitializationCompleted();
    }
}
