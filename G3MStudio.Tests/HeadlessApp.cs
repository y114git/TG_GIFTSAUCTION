using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;

namespace G3MStudio.Tests;

public static class HeadlessApp
{
    // Skia with real drawing, so text layout and rendering cost in the editor benchmark are real; other tests do not mind.
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>().UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });

    private static readonly Lazy<HeadlessUnitTestSession> Session = new(() => HeadlessUnitTestSession.StartNew(typeof(HeadlessApp)));

    // Windows a test left open: they keep their static subscriptions (language, editor options), so every later test pays for them.
    private static readonly List<WeakReference<Window>> Opened = [];

    private static void CloseLeftovers()
    {
        for (var i = Opened.Count - 1; i >= 0; i--)
        {
            if (Opened[i].TryGetTarget(out var window) && window.IsVisible)
            {
                if (window is MainWindow main) main.Workspaces.Close(); // an unsaved file would make the close ask instead of close
                window.Close();
            }
        }

        Opened.Clear();
    }

    // Every run gets its own user data folder, so tests never touch the real settings and do not share them.
    // The variable is set on the UI thread, where runs are serialized, so two runs cannot swap it under each other.
    private static bool _tracking;

    private static void FreshUserData()
    {
        MainWindow.UrlOpener = _ => true; // no test starts a browser
        G3MStudio.Audio.AudioDevices.Shutdown(); // no test opens a sound device either; the audio tests set their own
        G3MStudio.Audio.AudioDevices.Open = (_, _) => null;
        G3MStudio.Audio.AudioDevices.List = () => [];
        G3MStudio.Audio.AudioDevices.LibraryPresent = () => true;
        if (!_tracking)
        {
            _tracking = true;
            Window.WindowOpenedEvent.AddClassHandler<Window>((w, _) => Opened.Add(new WeakReference<Window>(w)));
        }

        G3MStudio.Theme.DialogSizing.ScreenOverride = new G3MStudio.Core.Settings.ScreenInfo(1920, 1080, 1.0); // automatic scale = 100%
        Environment.SetEnvironmentVariable("G3MSTUDIO_DATA", Path.Combine(Path.GetTempPath(), "g3mstudio-tests", Guid.NewGuid().ToString("N")));
    }

    // Avalonia's session reset after a run sometimes throws "dispatcher loop" on a busy machine, after the test body and its asserts finished.
    // That is a reset glitch, not a test result; anything thrown before the body completed still fails the test.
    // The work returns Task<bool> on purpose: Dispatch only waits for a Func<Task<T>>, and a plain Func<Task> would end the run at its first await.
    /// <summary>The longest a headless test body may run (the slowest takes about 4 minutes). A body stuck on an await that never completes fails with a TimeoutException instead of freezing the whole run, as it did once on macOS arm64.</summary>
    private static readonly TimeSpan Limit = TimeSpan.FromMinutes(12);

    private static async Task Guarded(Func<Task> body)
    {
        var finished = false;
        try
        {
            await Session.Value.Dispatch(async () =>
            {
                FreshUserData();
                await body();
                CloseLeftovers();
                Dispatcher.UIThread.RunJobs(); // jobs a test left posted must not run while the session is reset
                finished = true;
                return true;
            }, CancellationToken.None).WaitAsync(Limit);
        }
        catch (InvalidProgramException e) when (finished && e.Message.Contains("dispatcher loop", StringComparison.Ordinal))
        {
        }
        finally
        {
            // Windows and workspaces of the finished test must not pile up in a long serial run: macOS arm64 CI has about 7 GB.
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
    }

    public static Task Run(Action action) => Guarded(() =>
    {
        action();
        return Task.CompletedTask;
    });

    public static Task Run(Func<Task> action) => Guarded(action);
}
