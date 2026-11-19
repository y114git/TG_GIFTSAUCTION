using Avalonia.Controls;
using Avalonia.Threading;
using Dock.Avalonia.Controls;
using G3MStudio.Core.Settings;
using G3MStudio.Docking;
using G3MStudio.Editor;
using G3MStudio.Theme;

#pragma warning disable CA2000 // windows made for a check only

namespace G3MStudio.Tests;

/// <summary>One Animations setting (the old Reduce motion checkbox migrates to Off), the closing animations of tabs and docks, and the one tooltip delay.</summary>
public sealed class MotionMergeTests
{
    private static async Task Pump(Func<bool> done)
    {
        for (var i = 0; i < 100 && !done(); i++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(20);
        }
    }

    [Fact]
    public void TheOldReduceMotionSwitchMigratesToOffUnlessALevelWasChosen()
    {
        Assert.Equal(MotionPolicy.Off, new AppSettings { ReduceMotion = true }.ChosenAnimations);
        Assert.Equal(MotionPolicy.Full, new AppSettings { ReduceMotion = true, Animations = MotionPolicy.Full }.ChosenAnimations);
        Assert.Equal("", new AppSettings().ChosenAnimations);
    }

    [Fact]
    public async Task AClosingTabFadesOutBeforeItIsRemovedAndAFoldedDockSlidesAwayBeforeItFolds()
    {
        await HeadlessApp.Run(async () =>
        {
            Motion.TimedInHeadless = true;
            Motion.SetLevel("Full");
            var dock = new StudioDock(editor: tab => new TextBlock { Text = tab.Key });
            var control = new DockControl();
            dock.Attach(control);
            var window = new Window { Width = 1000, Height = 700, Content = control };
            window.Show();
            Dispatcher.UIThread.RunJobs();
            try
            {
                var id = dock.OpenTab("resource", "sprites:a", false)!.Id;
                dock.OpenTab("resource", "sprites:b", false);
                Dispatcher.UIThread.RunJobs();
                dock.CloseTab(id);
                Assert.NotNull(dock.Tabs.Find("resource", "sprites:a")); // still there while it fades
                await Pump(() => dock.Tabs.Find("resource", "sprites:a") is null);
                Assert.Null(dock.Tabs.Find("resource", "sprites:a"));

                Assert.False(dock.IsFolded("left"));
                dock.ToggleDock("left");
                Assert.False(dock.IsFolded("left")); // still open while it slides out
                await Pump(() => dock.IsFolded("left"));
                Assert.True(dock.IsFolded("left"));
            }
            finally
            {
                Motion.TimedInHeadless = false;
                Motion.SetLevel("Medium");
                window.Close();
            }
        });
    }

    [Fact]
    public async Task OneTooltipDelaySettingClampedToItsRangeAppliesToEveryControl()
    {
        await HeadlessApp.Run(() =>
        {
            try
            {
                EditorOptions.SetHoverDelay(10);
                var button = new Button();
                var window = new Window { Content = button };
                window.Show();
                Assert.Equal(EditorOptions.MinHoverDelayMs, EditorOptions.HoverDelayMs);
                Assert.Equal(300, EditorOptions.MinHoverDelayMs);
                Assert.Equal(300, ToolTip.GetShowDelay(button));
                EditorOptions.SetHoverDelay(5000);
                window.UpdateLayout();
                Assert.Equal(3000, ToolTip.GetShowDelay(button));
                Assert.Equal(400, new AppSettings().CodeHoverDelayMs);
                window.Close();
            }
            finally { EditorOptions.SetHoverDelay(400); }

            return Task.CompletedTask;
        });
    }
}
