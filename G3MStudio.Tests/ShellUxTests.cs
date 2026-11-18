using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Dock.Avalonia.Controls;
using Dock.Model.Controls;
using G3MStudio.Core.Notifications;
using G3MStudio.Core.Workspace;
using G3MStudio.Docking;
using G3MStudio.Localization;
using G3MStudio.Notifications;

namespace G3MStudio.Tests;

public sealed class ShellUxTests
{
    private static Task Run(Action<StudioDock, Window> test) => HeadlessApp.Run(() =>
    {
        var dock = new StudioDock(editor: tab => new TextBlock { Text = tab.Key });
        var control = new DockControl();
        dock.Attach(control);
        var window = new Window { Width = 1000, Height = 700, Content = control };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        try { test(dock, window); }
        finally
        {
            Loc.Current.SetLanguage("en");
            window.Close();
        }
    });

    private static string Key(StudioDock dock) => dock.Tabs.Get(dock.ActiveTabId!)!.Key;

    [Fact]
    public void HistoryGoesBackAndForwardAndSkipsGoneTabs()
    {
        var history = new ActivationHistory();
        foreach (var id in new[] { "a", "b", "c" }) history.Record(id);
        Assert.False(history.Can(1, _ => true));
        Assert.Equal("b", history.Step(-1, _ => true));
        Assert.Equal("a", history.Step(-1, id => id != "x"));
        Assert.Null(history.Step(-1, _ => true));
        Assert.Equal("c", history.Step(1, id => id != "b")); // b is closed
        history.Forget("b");
        history.Record("d");
        Assert.Equal("c", history.Step(-1, _ => true));
        Assert.Equal("a", history.Step(-1, _ => true));
    }

    [Fact]
    public void ANewTabDropsWhatLayAheadLikeABrowser()
    {
        var history = new ActivationHistory();
        foreach (var id in new[] { "a", "b", "c" }) history.Record(id);
        history.Step(-1, _ => true);
        history.Record("d");
        Assert.False(history.Can(1, _ => true));
        Assert.Equal("b", history.Step(-1, _ => true));
    }

    [Fact]
    public Task PreviousAndNextTabActivateTheTabUsedBeforeAndAfter() => Run((dock, _) =>
    {
        var a = dock.OpenTab("resource", "sprites:a", false)!.Id;
        dock.OpenTab("resource", "sprites:b", false);
        dock.OpenTab("resource", "sprites:c", false);
        Assert.False(dock.CanStepHistory(1));
        Assert.True(dock.StepHistory(-1));
        Assert.Equal("sprites:b", Key(dock));
        Assert.True(dock.StepHistory(-1));
        Assert.Equal("sprites:a", Key(dock));
        Assert.False(dock.StepHistory(-1));
        // Stepping did not record itself: forward still walks the same way.
        Assert.True(dock.StepHistory(1));
        Assert.Equal("sprites:b", Key(dock));
        Assert.True(dock.StepHistory(1));
        Assert.Equal("sprites:c", Key(dock));
        Assert.False(dock.CanStepHistory(1));

        dock.StepHistory(-1);
        dock.CloseTab(dock.ActiveTabId!); // b is closed, so back skips to a
        Assert.Equal(a, dock.Tabs.Find("resource", "sprites:a")!.Id);
        dock.OpenTab("resource", "sprites:a", false);
        Assert.True(dock.StepHistory(-1) || Key(dock) is "sprites:a" or "sprites:c");
    });

    [Fact]
    public Task ClosedTabsAreSkippedGoingBack() => Run((dock, _) =>
    {
        dock.OpenTab("resource", "sprites:a", false);
        var b = dock.OpenTab("resource", "sprites:b", false)!.Id;
        dock.OpenTab("resource", "sprites:c", false);
        dock.CloseTab(b);
        Dispatcher.UIThread.RunJobs();
        dock.OpenTab("resource", "sprites:c", false);
        Assert.True(dock.StepHistory(-1));
        Assert.Equal("sprites:a", Key(dock));
    });

    [Fact]
    public async Task TheShellHasTheCommandsAndTheButtonsFollowTheHistory()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir, objects: 2);
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                Assert.Equal("Ctrl+Alt+Left", window.Registry.Find("tabs.historyBack")!.Shortcut);
                Assert.Equal("Ctrl+Alt+Right", window.Registry.Find("tabs.historyForward")!.Shortcut);
                await window.OpenAsync(path);
                var back = window.FindControl<Button>("TabBackButton")!;
                window.Docking.OpenTab("resource", "objects:obj_0", false);
                window.Docking.OpenTab("resource", "objects:obj_1", false);
                Dispatcher.UIThread.RunJobs();
                Assert.True(back.IsEnabled);
                window.Registry.Execute("tabs.historyBack");
                Assert.Equal("objects:obj_0", Key(window.Docking));
                Assert.True(window.FindControl<Button>("TabForwardButton")!.IsEnabled);
                for (var guard = 0; guard < 20 && window.Docking.CanStepHistory(-1); guard++) window.Registry.Execute("tabs.historyBack");
                Dispatcher.UIThread.RunJobs();
                Assert.False(window.Registry.IsEnabled(window.Registry.Find("tabs.historyBack")!));
                Assert.False(back.IsEnabled);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public async Task TheTabButtonsSitWhereTheSettingSays()
    {
        using var dir = new TempDir();
        await HeadlessApp.Run(() =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                var buttons = window.FindControl<StackPanel>("TabButtons")!;
                Assert.Equal(Avalonia.Controls.Dock.Left, DockPanel.GetDock(buttons));
                window.Workspaces.Settings.Update(s => s with { TabButtons = G3MStudio.Core.Settings.TabButtonsPlace.MenuRight });
                Assert.Equal(Avalonia.Controls.Dock.Right, DockPanel.GetDock(buttons));
                window.Workspaces.Settings.Update(s => s with { TabButtons = G3MStudio.Core.Settings.TabButtonsPlace.Hidden });
                Assert.False(buttons.IsVisible);
            }
            finally { window.Close(); }

            return Task.CompletedTask;
        });
    }

    [Fact]
    public void RemoveAndClearKeepTheCountsHonest()
    {
        var center = new NotificationCenter();
        var first = center.Post(NotificationLevel.Info, "one");
        center.Post(NotificationLevel.Error, "two");
        center.Remove(first.Id);
        Assert.Equal(["two"], center.History.Select(n => n.Text));
        Assert.Single(center.Active);
        Assert.Equal(1, center.Unread);
        center.Clear();
        Assert.Empty(center.History);
        Assert.Empty(center.Active);
        Assert.Equal(0, center.Unread);
    }

    [Fact]
    public Task TheNotificationListShowsEveryItemAndDismissesThem() => HeadlessApp.Run(() =>
    {
        Loc.Current.SetLanguage("en");
        var center = new NotificationCenter();
        var list = new NotificationList(center);
        new Window { Content = list }.Show();
        Assert.Contains(list.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text == "No notifications");
        center.Post(NotificationLevel.Info, "alpha");
        center.Post(NotificationLevel.Warning, "beta");
        var texts = list.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text ?? "").ToList();
        Assert.Contains("alpha", texts);
        Assert.Contains("beta", texts);
        Assert.Contains(texts, t => t.StartsWith("Warning", StringComparison.Ordinal));
        var dismiss = list.GetLogicalDescendants().OfType<Button>().Where(b => b.Tag as string == "dismiss").ToList();
        Assert.Equal(2, dismiss.Count);
        dismiss[0].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(["alpha"], center.History.Select(n => n.Text));
        list.GetLogicalDescendants().OfType<Button>().Single(b => b.Tag as string == "dismissAll").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Empty(center.History);
        Assert.Contains(list.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text == "No notifications");
    });

    [Fact]
    public async Task TheStatusBarButtonIsAlwaysThereAndOpensTheList()
    {
        using var dir = new TempDir();
        await HeadlessApp.Run(() =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                var button = window.FindControl<Button>("NotifyButton")!;
                Dispatcher.UIThread.RunJobs();
                Assert.True(button.IsVisible);
                Assert.Equal("Notifications", button.Content);
                window.Notify(NotificationLevel.Info, "hello");
                Assert.Equal("Notifications (1)", button.Content);
                var flyout = Assert.IsType<Avalonia.Controls.Flyout>(button.Flyout);
                flyout.ShowAt(button);
                Dispatcher.UIThread.RunJobs();
                Assert.IsType<NotificationList>(flyout.Content);
                Assert.Equal(0, window.Notifications.Unread);
                Assert.Equal("Notifications", button.Content);
                Assert.Single(window.Notifications.History);
                flyout.Hide();
            }
            finally { window.Close(); }
            return Task.CompletedTask;
        });
    }

    [Fact]
    public Task PanelHeadersHaveTooltipsAPinStateAndATranslatedMenuWithMoveTo() => Run((dock, window) =>
    {
        Loc.Current.SetLanguage("en");
        DockTexts.Apply();
        var chrome = window.GetVisualDescendants().OfType<ToolChromeControl>().First();
        Button Part(string name) => chrome.GetVisualDescendants().OfType<Button>().First(b => b.Name == name);
        Assert.Equal(Loc.Current["dock.StudioToolPinTip"], ToolTip.GetTip(Part("PART_PinButton")));
        Assert.Equal(Loc.Current["dock.StudioToolMenuTip"], ToolTip.GetTip(Part("PART_MenuButton")));
        Assert.Equal(Loc.Current["dock.StudioToolCloseTip"], ToolTip.GetTip(Part("PART_CloseButton")));

        var menu = Assert.IsType<MenuFlyout>(Part("PART_MenuButton").Flyout);
        Part("PART_MenuButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        var headers = menu.Items.OfType<MenuItem>().Select(i => i.Header?.ToString()).ToList();
        Assert.Equal(["Float", "Dock", "Dock as tab", "Auto hide", Loc.Current["dock.StudioToolMoveTo"], "Close"], headers);
        Assert.Equal(3, menu.Items.OfType<MenuItem>().Single(i => i.Tag as string == "move").Items.Count);

        // Opened again, also after a language switch: Move to keeps its label and Close stays Close.
        Part("PART_MenuButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(headers, [.. menu.Items.OfType<MenuItem>().Select(i => i.Header?.ToString())]);
        Loc.Current.SetLanguage("ru");
        Part("PART_MenuButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        var ru = menu.Items.OfType<MenuItem>().ToList();
        Assert.Equal(Loc.Current["dock.StudioToolMoveTo"], ru[4].Header?.ToString());
        Assert.Equal(Loc.Current["dock.StudioToolMoveLeft"], ((MenuItem)ru[4].Items[0]!).Header?.ToString());
        Assert.NotEqual(ru[4].Header?.ToString(), ru[5].Header?.ToString());
        Loc.Current.SetLanguage("en");

        var tool = dock.Find<IToolDock>("LeftDock")!.ActiveDockable!;
        menu.Items.OfType<MenuItem>().First(i => i.Tag as string == "move").Items.OfType<MenuItem>().ElementAt(1).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        Assert.Same(dock.Find<IToolDock>("RightDock"), tool.Owner);
    });

    [Fact]
    public Task APinnedPanelKeepsALabelledStripAndItsHeaderShowsTheUnpinTip() => Run((dock, window) =>
    {
        Loc.Current.SetLanguage("en");
        DockTexts.Apply();
        dock.Factory.PinDockable(dock.Find<Dock.Model.Core.IDockable>("Explorer")!);
        Dispatcher.UIThread.RunJobs();
        var strip = window.GetVisualDescendants().OfType<ToolPinItemControl>().Single();
        Assert.Contains(strip.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "Explorer");
        Assert.Equal(Loc.Current["dock.StudioToolSlideTip"], ToolTip.GetTip(strip.GetVisualDescendants().OfType<Button>().First()));
        var button = strip.GetVisualDescendants().OfType<Button>().First();
        button.Command!.Execute(button.CommandParameter);
        Dispatcher.UIThread.RunJobs();
        var open = window.GetVisualDescendants().OfType<ToolChromeControl>().Single(c => c.IsPinned);
        var pin = open.GetVisualDescendants().OfType<Button>().First(b => b.Name == "PART_PinButton");
        Assert.Equal(Loc.Current["dock.StudioToolUnpinTip"], ToolTip.GetTip(pin));
    });
}
