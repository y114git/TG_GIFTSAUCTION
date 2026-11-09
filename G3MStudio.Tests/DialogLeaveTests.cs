using Avalonia.Controls;
using Avalonia.Threading;
using G3MStudio.Theme;

namespace G3MStudio.Tests;

public sealed class DialogLeaveTests
{
    [Fact]
    public async Task RepeatedCancelOpensOneDiscardPromptAtATime()
    {
        await HeadlessApp.Run(async () =>
        {
            var window = new Window();
            window.Show();
            try
            {
                DialogFrame.Guard(window, () => true);
                DialogFrame.Leave(window, window.Close);
                DialogFrame.Leave(window, window.Close);
                DialogFrame.RequestClose(window);
                Dispatcher.UIThread.RunJobs();
                Assert.Single(window.OwnedWindows);

                window.OwnedWindows[0].Close(); // Keep editing
                await Task.Delay(50, TestContext.Current.CancellationToken);
                Dispatcher.UIThread.RunJobs();
                Assert.Empty(window.OwnedWindows);
                Assert.True(window.IsVisible);

                DialogFrame.Leave(window, window.Close);
                Dispatcher.UIThread.RunJobs();
                Assert.Single(window.OwnedWindows);
                window.OwnedWindows[0].Close();
            }
            finally { window.Close(); }
        });
    }
}
