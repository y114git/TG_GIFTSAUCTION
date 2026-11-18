using Avalonia.Controls;
using Avalonia.Threading;
using G3MStudio.Core.Notifications;
using G3MStudio.Explorer;
using G3MStudio.Notifications;
using G3MStudio.Theme;

namespace G3MStudio.Tests;

public sealed class MotionTransitionTests
{
    private static async Task Settle(Func<bool> done)
    {
        for (var i = 0; i < 100 && !done(); i++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(10);
        }

        Assert.True(done(), "the dialog result did not settle in time");
    }

    private static async Task Timed(string level, Func<Task> body)
    {
        await HeadlessApp.Run(async () =>
        {
            Motion.TimedInHeadless = true;
            try
            {
                Motion.CancelAll(); // runs left by other tests
                await Settle(() => Motion.Active == 0);
                Motion.SetLevel(level);
                await body();
            }
            finally
            {
                Motion.TimedInHeadless = false;
                Motion.SetLevel("Medium");
            }
        });
    }

    [Fact]
    public async Task OffStartsNothingAndExitsAtOnce()
    {
        await Timed("Off", () =>
        {
            var box = new Border();
            var list = new ListBox();
            var started = Motion.EntersStarted;
            var running = Motion.Active; // other tests may leave runs behind; only the change counts
            var done = 0;
            Motion.Enter(box, scale: true);
            Motion.Fold(box);
            Motion.Slide(box, 10, 10);
            Motion.ScrollTo(list, 0);
            Motion.Follow(new StackPanel());
            Motion.Exit(box, () => done++);
            Assert.Equal(1, done);
            Assert.Equal(started, Motion.EntersStarted);
            Assert.Equal(running, Motion.Active);
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task InterruptedAnimationsHandBackPlainValues()
    {
        await Timed("Full", async () =>
        {
            var box = new Border();
            new Window { Content = box }.Show();
            var running = Motion.Active;
            Motion.Enter(box, scale: true);
            Motion.Fold(box); // replaces the first run
            Assert.Equal(running + 1, Motion.Active);
            Motion.Cancel(box);
            await Settle(() => Motion.Active == running);
            Assert.Equal(running, Motion.Active);
            Assert.Equal(1.0, box.Opacity);
            Assert.Equal(Avalonia.Matrix.Identity, box.RenderTransform?.Value ?? Avalonia.Matrix.Identity);

            var closed = 0;
            Motion.Exit(box, () => closed++);
            Motion.SetLevel("Off"); // a level change ends the fade and still runs its exit
            await Settle(() => closed == 1);
            Assert.Equal(1, closed);
            Assert.Equal(1.0, box.Opacity);
            Assert.True(box.IsHitTestVisible);
        });
    }

    [Theory]
    [InlineData("Full", 1)]
    [InlineData("Minimal", 1)]
    [InlineData("Off", 0)]
    public async Task CuesRunOnOneElementAndLeaveItAtRest(string level, int runs)
    {
        await Timed(level, async () =>
        {
            var tab = new Border();
            var row = new ListBoxItem { Content = "row" };
            var input = new TextBox();
            var panel = new StackPanel { Children = { tab, row, input } };
            new Window { Content = panel }.Show();
            Dispatcher.UIThread.RunJobs();
            var before = Motion.EntersStarted;
            Motion.Open(tab);
            Motion.Shake(input);
            Motion.Flash(row);
            Assert.Equal(before + 3 * runs, Motion.EntersStarted);
            await Settle(() => Motion.Active == 0);
            Assert.Equal(1.0, tab.Opacity);
            Assert.Equal(1.0, input.Opacity);
            Assert.Equal(Avalonia.Matrix.Identity, input.RenderTransform?.Value ?? Avalonia.Matrix.Identity);
            Assert.Equal(Avalonia.Matrix.Identity, tab.RenderTransform?.Value ?? Avalonia.Matrix.Identity);
        });
    }

    [Fact]
    public async Task ToastFadesOutThenLeavesTheStack()
    {
        await Timed("Full", async () =>
        {
            var center = new NotificationCenter();
            var host = new ToastHost();
            new Window { Content = host }.Show();
            host.Bind(center);
            center.Post(NotificationLevel.Info, "hello");
            Dispatcher.UIThread.RunJobs();
            Assert.Single(host.Children);
            center.DismissAll();
            Assert.Single(host.Children); // still fading
            await Settle(() => host.Children.Count == 0);
            Assert.Empty(host.Children);
        });
    }

    [Fact]
    public async Task DialogResultSurvivesTheCloseFade()
    {
        await Timed("Full", async () =>
        {
            var owner = new Window();
            owner.Show();
            var dialog = new Window { Width = 300, Height = 200 };
            DialogFrame.Show(dialog, new TextBlock { Text = "x" }, []);
            var task = dialog.ShowDialog<string>(owner);
            Dispatcher.UIThread.RunJobs();
            dialog.Close("yes");
            Assert.True(dialog.IsVisible); // fading
            await Settle(() => task.IsCompleted);
            Assert.Equal("yes", await task);
            owner.Close();
        });
    }

    [Fact]
    public async Task ReplacingTheFrameKeepsTheFadeAndTheResult()
    {
        await Timed("Full", async () =>
        {
            var owner = new Window();
            owner.Show();
            var dialog = new Window { Width = 300, Height = 200 };
            DialogFrame.Show(dialog, new TextBlock { Text = "x" }, []);
            var task = dialog.ShowDialog<bool>(owner);
            Dispatcher.UIThread.RunJobs();
            DialogFrame.Show(dialog, new TextBlock { Text = "y" }, []);
            Dispatcher.UIThread.RunJobs();
            dialog.Close(true);
            Assert.True(dialog.IsVisible); // fading
            await Settle(() => task.IsCompleted);
            Assert.True(await task);
            owner.Close();
        });
    }

    [Fact]
    public async Task FoldingABigExplorerListAnimatesOneControlAndOffAnimatesNone()
    {
        await Timed("Full", () =>
        {
            var view = new ExplorerView();
            new Window { Width = 400, Height = 600, Content = view }.Show();
            var list = view.FindControl<ListBox>("List")!;
            var before = Motion.EntersStarted;
            Motion.Fold(list);
            Assert.Equal(before + 1, Motion.EntersStarted);
            Motion.SetLevel("Off");
            var running = Motion.Active;
            Motion.Fold(list);
            Assert.Equal(running, Motion.Active);
            Assert.Equal(before + 1, Motion.EntersStarted);
            return Task.CompletedTask;
        });
    }
}
