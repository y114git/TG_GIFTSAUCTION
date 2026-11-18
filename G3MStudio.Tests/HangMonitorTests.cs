using Avalonia.Threading;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

public sealed class HangMonitorTests
{
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void BlockTheUiThread(int milliseconds) => Thread.Sleep(milliseconds);

    [Fact]
    public async Task AUiThreadThatStopsAnsweringIsWrittenDownWithWhatItWasDoing()
    {
        using var dir = new TempDir();
        var sent = new List<string>();
        await HeadlessApp.Run(async () =>
        {
            using var monitor = new HangMonitor(new UserData(dir.File("user")), TimeSpan.FromSeconds(1), TimeSpan.FromMilliseconds(50), (message, _) => sent.Add(message));
            for (var i = 0; i < 4; i++) { await Task.Delay(50); Dispatcher.UIThread.RunJobs(); } // the first beats are answered
            Assert.Null(monitor.LastFile);
            BlockTheUiThread(4000);
            for (var i = 0; i < 400 && monitor.LastFile is null; i++) { await Task.Delay(50); Dispatcher.UIThread.RunJobs(); }
            Assert.NotNull(monitor.LastFile);
            var text = await File.ReadAllTextAsync(monitor.LastFile, TestContext.Current.CancellationToken);
            Assert.Contains("did not answer", text, StringComparison.Ordinal);
            // Only Windows can identify the thread, so elsewhere the report says it could not and still holds the rest.
            if (OperatingSystem.IsWindows())
            {
                Assert.Contains("waiting", text, StringComparison.Ordinal); // a sleeping thread uses no processor time
                Assert.Contains(nameof(BlockTheUiThread), text, StringComparison.Ordinal); // the stack names the call that blocks
            }
            else Assert.Contains("the thread could not be identified", text, StringComparison.Ordinal);
            Assert.Contains("Processor time in that window", text, StringComparison.Ordinal);
            for (var i = 0; i < 400 && !(await File.ReadAllTextAsync(monitor.LastFile, TestContext.Current.CancellationToken)).Contains("answered again", StringComparison.Ordinal); i++) { await Task.Delay(50); Dispatcher.UIThread.RunJobs(); }
            Assert.Contains("answered again", await File.ReadAllTextAsync(monitor.LastFile, TestContext.Current.CancellationToken), StringComparison.Ordinal);
        });

        Assert.Single(sent);
    }

    [Fact]
    public async Task AUiThreadThatKeepsAnsweringWritesNothing()
    {
        using var dir = new TempDir();
        await HeadlessApp.Run(async () =>
        {
            var sent = new List<string>();
            using var monitor = new HangMonitor(new UserData(dir.File("user")), TimeSpan.FromSeconds(4), TimeSpan.FromMilliseconds(50), (message, _) => sent.Add(message));
            for (var i = 0; i < 30; i++) { await Task.Delay(50); Dispatcher.UIThread.RunJobs(); }
            Assert.Null(monitor.LastFile);
            Assert.Empty(sent);
        });
    }
}
