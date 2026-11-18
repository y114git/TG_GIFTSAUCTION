using System.Diagnostics;
using Avalonia.Controls;
using G3MStudio.Core.Settings;
using G3MStudio.Core.Workspace;
using G3MStudio.Explorer;
using G3MStudio.Theme;

namespace G3MStudio.Tests;

public sealed class MotionTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("Full", 100, 100)]
    [InlineData("Medium", 100, 70)]
    [InlineData("Minimal", 100, 40)]
    [InlineData("Off", 100, 0)]
    [InlineData("Full", 900, 200)] // nothing runs longer than 200 ms
    public void DurationsFollowTheLevel(string level, int baseMs, int expected) => Assert.Equal(expected, MotionPolicy.Milliseconds(level, baseMs));

    [Fact]
    public void CuesFollowTheirRules()
    {
        Assert.False(MotionPolicy.ShowsProgress(299));
        Assert.True(MotionPolicy.ShowsProgress(300));
        Assert.True(MotionPolicy.FlashesItem(1));
        Assert.False(MotionPolicy.FlashesItem(0));
        Assert.False(MotionPolicy.FlashesItem(28000)); // a bulk change never lights rows
        Assert.Equal(0, MotionPolicy.ShakeOffsets[0]);
        Assert.Equal(0, MotionPolicy.ShakeOffsets[^1]); // the shake ends at rest
    }

    [Fact]
    public void NotChosenMeansMediumUnlessTheOsAsksForReducedMotion()
    {
        Assert.Equal("Medium", MotionPolicy.Resolve("", false));
        Assert.Equal("Off", MotionPolicy.Resolve("", true));
        Assert.Equal("Full", MotionPolicy.Resolve("Full", true)); // an explicit choice wins
        Assert.Equal("", MotionPolicy.Normal("Wild"));
        Assert.True(MotionPolicy.AllowsMovement("Medium"));
        Assert.False(MotionPolicy.AllowsMovement("Minimal"));
        Assert.Equal("", new AppSettings().Animations);
    }

    [Fact]
    public async Task OffFinishesAnimationsAtOnceAndAnyOtherLevelRunsThem()
    {
        await HeadlessApp.Run(() =>
        {
            Motion.TimedInHeadless = true;
            try
            {
                var box = new Border();
                Motion.SetLevel("Off");
                Assert.True(Motion.Instant);
                Assert.Equal(TimeSpan.Zero, Motion.Ms(150));
                var before = Motion.EntersStarted;
                Motion.Enter(box, scale: true);
                Assert.Equal(before, Motion.EntersStarted);

                Motion.SetLevel("Medium");
                Assert.Equal(TimeSpan.FromMilliseconds(105), Motion.Ms(150));
                Motion.Enter(box);
                Assert.Equal(before + 1, Motion.EntersStarted);
            }
            finally
            {
                Motion.TimedInHeadless = false;
                Motion.SetLevel("Medium");
            }

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task AFlyoutOpensWithItsGrowAnimationWithoutCrashing()
    {
        await HeadlessApp.Run(() =>
        {
            Motion.TimedInHeadless = true;
            var window = new Window();
            try
            {
                Motion.SetLevel("Full");
                var button = new Button { Flyout = new Flyout { Content = new TextBlock { Text = "x" } } };
                window.Content = button;
                window.Show();
                button.Flyout.ShowAt(button);
            }
            finally
            {
                window.Close();
                Motion.TimedInHeadless = false;
                Motion.SetLevel("Medium");
            }

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task TheChoiceIsStoredAndAppliedLiveWithoutARestart()
    {
        using var dir = new TempDir();
        await HeadlessApp.Run(() =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                window.Workspaces.Settings.Update(x => x with { Animations = "Minimal" });
                Assert.Equal("Minimal", Motion.Level);
                window.Workspaces.Settings.Update(x => x with { Animations = "Off" });
                Assert.Equal("Off", Motion.Level);
                Assert.Equal("Off", new SettingsStore(window.Workspaces.Settings.UserData).Current.Animations);
            }
            finally
            {
                window.Close();
                Motion.SetLevel("Medium");
            }

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task BigExplorerListOpensWithoutAnimatingRowsOrSlowingDown()
    {
        await HeadlessApp.Run(() =>
        {
            Motion.TimedInHeadless = true;
            try
            {
                Motion.SetLevel("Full");
                var view = new ExplorerView();
                var window = new Window { Width = 400, Height = 600, Content = view };
                window.Show();
                var list = view.FindControl<ListBox>("List")!;
                var rows = new List<ExplorerRow> { new(ExplorerRowKind.Type, "sprites", "Sprites", 1) { Count = 28000, Expanded = true } };
                rows.AddRange(Enumerable.Range(0, 28000).Select(i => new ExplorerRow(ExplorerRowKind.Item, "sprites", "n" + i, 2)));
                var enters = Motion.EntersStarted;
                var watch = Stopwatch.StartNew();
                ((ExplorerRowList)list.ItemsSource!).Update(rows);
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                output.WriteLine($"28000 rows shown with Full animations: {watch.Elapsed.TotalMilliseconds:F0} ms");
                Assert.Equal(enters, Motion.EntersStarted);
                Budget.AssertUnder(watch.Elapsed.TotalMilliseconds, 3000, "28k Explorer rows with animations on", output);
                window.Close();
            }
            finally
            {
                Motion.TimedInHeadless = false;
                Motion.SetLevel("Medium");
            }

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task AnEnterThatGrowsRunsToTheEndAndLeavesNoTransformBehind()
    {
        using var probe = new SweepProbe();
        await HeadlessApp.Run(async () =>
        {
            Motion.TimedInHeadless = true;
            try
            {
                Motion.SetLevel("Full");
                var box = new Border();
                var window = new Window { Content = box };
                window.Show();
                Motion.Enter(box, scale: true);
                Assert.True(Motion.Active > 0, "the grow-in did not start");
                for (var i = 0; i < 100 && Motion.Active > 0; i++)
                {
                    await Task.Delay(20);
                    Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                }

                Assert.Null(box.RenderTransform);
                window.Close();
            }
            finally
            {
                Motion.TimedInHeadless = false;
                Motion.SetLevel("Medium");
            }
        });
        SweepProbe.Flush();
        Assert.Empty(probe.Errors);
    }
}
