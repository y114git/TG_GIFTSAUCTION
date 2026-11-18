using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;

namespace G3MStudio.Tests;

public static class HeadlessApp
{
    /// <summary>The modifier of a command chord (Ctrl+S, Ctrl+Z): Cmd on macOS, Ctrl elsewhere.</summary>
    public static Avalonia.Input.RawInputModifiers Command => OperatingSystem.IsMacOS() ? Avalonia.Input.RawInputModifiers.Meta : Avalonia.Input.RawInputModifiers.Control;

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

    // AvaloniaEdit remembers the last focused control of the session in a static, so a closed window of a finished test stays reachable (and with it whatever
    // another window shares with it, like the scratch arrays of the layout) until some later focus replaces it. The compositor likewise keeps what a closed window
    // drew until the next frames. Neither is a subscription of the app, so each run starts with both released.
    private static void ReleaseLibraryStatics()
    {
        typeof(AvaloniaEdit.RoutedCommand).GetField("_inputElement", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)?.SetValue(null, null);
        Editor.UiMotion.Set(false); // a window of the test may have taken the reduced motion of the operating system
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(3);
    }

    // Every run gets its own user data folder, so tests never touch the real settings and do not share them.
    // The variable is set on the UI thread, where runs are serialized, so two runs cannot swap it under each other.
    private static bool _tracking;

    private static void FreshUserData()
    {
        MainWindow.UrlOpener = _ => true; // no test starts a browser
        G3MStudio.Localization.Loc.Current.SetLanguage("en"); // a failed test that did not restore the language must not switch every later test to it
        (G3MStudio.Explorer.HoverCards.Enabled, G3MStudio.Explorer.HoverCards.Describe, G3MStudio.Explorer.HoverCards.Picture) = (true, null, null); // the hover cards are shared statics
        G3MStudio.Audio.AudioDevices.Shutdown(); // no test opens a sound device either; the audio tests set their own
        G3MStudio.Audio.AudioDevices.Open = (_, _) => null;
        G3MStudio.Audio.AudioDevices.List = () => [];
        G3MStudio.Audio.AudioDevices.LibraryPresent = () => true;
        G3MStudio.Audio.AudioCaptureDevices.Shutdown(); // nor a microphone; the recorder tests set their own source
        G3MStudio.Audio.AudioCaptureDevices.Source = new FakeCaptureSource();
        if (!_tracking)
        {
            _tracking = true;
            Window.WindowOpenedEvent.AddClassHandler<Window>((w, _) => Opened.Add(new WeakReference<Window>(w)));
        }

        G3MStudio.Theme.DialogSizing.ScreenOverride = new G3MStudio.Core.Settings.ScreenInfo(1920, 1080, 1.0); // automatic scale = 100%
        G3MStudio.Theme.DialogSizing.Unhook();
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
                // Every test also guards against a frozen window: a UI thread that does not answer for 30 s fails the test, with the stack of what it was doing.
                using var monitor = new HangMonitor(new G3MStudio.Core.Workspace.UserData(Path.Combine(Path.GetTempPath(), "g3mstudio-tests", "hangs", Guid.NewGuid().ToString("N"))), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(1));
                try
                {
                    await body();
                }
                finally
                {
                    G3MStudio.Localization.Loc.Current.SetLanguage("en"); // a test that is not headless reads texts too and runs next without a fresh start
                    CloseLeftovers();
                    Dispatcher.UIThread.RunJobs(); // jobs a test left posted must not run while the session is reset
                    ReleaseLibraryStatics();
                }

                if (monitor.LastFile is { } hang) throw new InvalidOperationException("The UI thread stopped answering during the test:" + Environment.NewLine + await File.ReadAllTextAsync(hang, CancellationToken.None));
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
