using Avalonia;
using Avalonia.Headless;
using Avalonia.Threading;

namespace G3MStudio.Tests;

public static class HeadlessApp
{
    // Skia with real drawing, so text layout and rendering cost in the editor benchmark are real; other tests do not mind.
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>().UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });

    private static readonly Lazy<HeadlessUnitTestSession> Session = new(() => HeadlessUnitTestSession.StartNew(typeof(HeadlessApp)));

    // Every run gets its own user data folder, so tests never touch the real settings and do not share them.
    // The variable is set on the UI thread, where runs are serialized, so two runs cannot swap it under each other.
    private static void FreshUserData()
    {
        MainWindow.UrlOpener = _ => true; // no test starts a browser
        Environment.SetEnvironmentVariable("G3MSTUDIO_DATA", Path.Combine(Path.GetTempPath(), "g3mstudio-tests", Guid.NewGuid().ToString("N")));
    }

    // Avalonia's session reset after a run sometimes throws "dispatcher loop" on a busy machine, after the test body and its asserts finished.
    // That is a reset glitch, not a test result; anything thrown before the body completed still fails the test.
    // The work returns Task<bool> on purpose: Dispatch only waits for a Func<Task<T>>, and a plain Func<Task> would end the run at its first await.
    private static async Task Guarded(Func<Task> body)
    {
        var finished = false;
        try
        {
            await Session.Value.Dispatch(async () =>
            {
                FreshUserData();
                await body();
                Dispatcher.UIThread.RunJobs(); // jobs a test left posted must not run while the session is reset
                finished = true;
                return true;
            }, CancellationToken.None);
        }
        catch (InvalidProgramException e) when (finished && e.Message.Contains("dispatcher loop", StringComparison.Ordinal))
        {
        }
    }

    public static Task Run(Action action) => Guarded(() =>
    {
        action();
        return Task.CompletedTask;
    });

    public static Task Run(Func<Task> action) => Guarded(action);
}
