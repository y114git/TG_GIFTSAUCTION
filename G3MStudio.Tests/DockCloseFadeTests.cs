using Avalonia.Controls;
using Avalonia.Threading;
using Dock.Avalonia.Controls;
using G3MStudio.Docking;
using G3MStudio.Theme;

namespace G3MStudio.Tests;

public sealed class DockCloseFadeTests
{
    private static async Task Settle(Func<bool> done)
    {
        for (var i = 0; i < 100 && !done(); i++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(10);
        }

        Assert.True(done(), "did not settle in time");
    }

    private static async Task Timed(Func<StudioDock, Task> body)
    {
        await HeadlessApp.Run(async () =>
        {
            Motion.TimedInHeadless = true;
            var dock = new StudioDock(editor: tab => new TextBlock { Text = tab.Key });
            var control = new DockControl();
            dock.Attach(control);
            var window = new Window { Width = 1000, Height = 700, Content = control };
            window.Show();
            try
            {
                Motion.SetLevel("Full");
                for (var i = 0; i < 20; i++) // the window's own start-up runs end first
                {
                    Dispatcher.UIThread.RunJobs();
                    Motion.CancelAll();
                    await Task.Delay(10);
                }

                await body(dock);
            }
            finally
            {
                window.Close();
                for (var i = 0; i < 100 && Motion.Active > 0; i++) // leave no run behind for the next test
                {
                    Motion.CancelAll();
                    Dispatcher.UIThread.RunJobs();
                    await Task.Delay(10);
                }

                Motion.TimedInHeadless = false;
                Motion.SetLevel("Medium");
            }
        });
    }

    private static string Open(StudioDock dock, string name) => dock.OpenTab("resource", "sprites:" + name, false)!.Id;

    [Fact]
    public Task ATabThatFadesOutIsGoneAfterwardsAndItsNameCanOpenAgain() => Timed(async dock =>
    {
        var a = Open(dock, "spr_a");
        Open(dock, "spr_b");
        Dispatcher.UIThread.RunJobs();
        dock.CloseTab(a);
        dock.CloseTab(a); // a second close while fading changes nothing
        Assert.NotNull(dock.DocumentOf(a));
        await Settle(() => dock.DocumentOf(a) is null);
        Assert.Single(dock.Documents());
        Assert.NotNull(dock.OpenTab("resource", "sprites:spr_a", false));
        Assert.Equal(2, dock.Documents().Count());
    });

    [Fact]
    public Task BulkClosesTakeATabThatIsStillFadingAtOnce() => Timed(dock =>
    {
        var a = Open(dock, "spr_a");
        Open(dock, "spr_b");
        Open(dock, "spr_c");
        Dispatcher.UIThread.RunJobs();
        dock.CloseTab(a);
        Assert.NotNull(dock.DocumentOf(a));
        dock.CloseAll();
        Assert.Empty(dock.Documents());
        Assert.Null(dock.Tabs.Get(a));
        return Task.CompletedTask;
    });
}
