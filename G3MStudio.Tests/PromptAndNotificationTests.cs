using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using G3MStudio.Core.Notifications;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

public sealed class PromptAndNotificationTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void NotificationsExpireExceptErrorsAndKeepAHistory()
    {
        var center = new NotificationCenter();
        var info = center.Post(NotificationLevel.Info, "saved", now: T0);
        center.Post(NotificationLevel.Error, "failed", now: T0);
        center.Post(NotificationLevel.Warning, "later", now: T0.AddSeconds(7));
        Assert.Equal(3, center.Unread);

        center.Expire(T0.AddSeconds(8), TimeSpan.FromSeconds(8));
        Assert.Equal(["failed", "later"], center.Active.Select(n => n.Text));
        center.Expire(T0.AddMinutes(5), TimeSpan.FromSeconds(8));
        Assert.Equal(["failed"], center.Active.Select(n => n.Text));
        Assert.Equal(3, center.History.Count);

        center.Dismiss(info.Id);
        center.DismissAll();
        Assert.Empty(center.Active);
        Assert.Equal(0, center.Unread);
    }

    [Fact]
    public void HistoryKeepsTheLastHundred()
    {
        var center = new NotificationCenter();
        for (var i = 0; i < 120; i++) center.Post(NotificationLevel.Info, "n" + i);
        Assert.Equal(NotificationCenter.MaxHistory, center.History.Count);
        Assert.Equal("n20", center.History[0].Text);
    }

    private static async Task<Workspace> OpenDirty(TempDir dir, bool dirty)
    {
        var path = TestData.WriteSample(dir, objects: 2);
        var userData = new UserData(dir.File("user"));
        var workspace = await new WorkspaceService(userData, RecentFiles.Load(userData)).OpenAsync(path);
        if (dirty) workspace.History.Execute(new RenameResourceCommand("objects", "obj_0", "obj_a"));
        return workspace;
    }

    [Fact]
    public async Task ACleanWorkspaceClosesWithoutAskingAndCancelChangesNothing()
    {
        using var dir = new TempDir();
        using var clean = await OpenDirty(dir, false);
        var asked = 0;
        Assert.True(await CloseGuard.ConfirmAsync(clean, _ => { asked++; return Task.FromResult(CloseChoice.Cancel); }, _ => Task.FromResult(true)));
        Assert.Equal(0, asked);
        Assert.True(await CloseGuard.ConfirmAsync(null, _ => throw new InvalidOperationException(), _ => Task.FromResult(true)));

        using var dir2 = new TempDir();
        using var dirty = await OpenDirty(dir2, true);
        var before = TestData.Bytes(dirty.Data);
        var saved = false;
        ClosePrompt? prompt = null;
        Assert.False(await CloseGuard.ConfirmAsync(dirty, p => { prompt = p; return Task.FromResult(CloseChoice.Cancel); }, _ => { saved = true; return Task.FromResult(true); }));
        Assert.Equal(new ClosePrompt(dirty.Name, 1, false), prompt);
        Assert.False(saved);
        Assert.True(dirty.IsDirty);
        Assert.Equal(before, TestData.Bytes(dirty.Data));
    }

    [Fact]
    public async Task SaveSavesDoItLaterKeepsTheJournalAndAFailedSaveStaysInTheApp()
    {
        using var dir = new TempDir();
        using var workspace = await OpenDirty(dir, true);
        Assert.True(await CloseGuard.ConfirmAsync(workspace, _ => Task.FromResult(CloseChoice.DoItLater), _ => throw new InvalidOperationException()));
        Assert.True(workspace.IsDirty);
        Assert.False(await CloseGuard.ConfirmAsync(workspace, _ => Task.FromResult(CloseChoice.Save), _ => Task.FromResult(false)));
        Assert.True(await CloseGuard.ConfirmAsync(workspace, _ => Task.FromResult(CloseChoice.Save), async w => (await w.SaveAsync(new SavePipeline(new UserData(dir.File("user"))), cancellationToken: TestContext.Current.CancellationToken)).Saved));
        Assert.False(workspace.IsDirty);
    }

    [Fact]
    public async Task ClosingTheWindowWithEditsAsksAndCancelKeepsTheAppAsItWas()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir, objects: 2);
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            var choices = new Queue<CloseChoice>([CloseChoice.Cancel, CloseChoice.DoItLater]);
            var prompts = new List<ClosePrompt>();
            window.AskClose = p => { prompts.Add(p); return Task.FromResult(choices.Dequeue()); };
            await window.OpenAsync(path);
            window.Workspaces.Current!.History.Execute(new RenameResourceCommand("objects", "obj_0", "obj_a"));
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("● New GameMaker Game - G3MStudio", window.Title);

            window.Close();
            Dispatcher.UIThread.RunJobs();
            Assert.True(window.IsVisible);
            Assert.NotNull(window.Workspaces.Current);
            Assert.Single(prompts);
            Assert.Equal(1, prompts[0].Changes);

            window.Close();
            for (var i = 0; i < 50 && window.IsVisible; i++) { Dispatcher.UIThread.RunJobs(); await Task.Delay(10); }
            Assert.False(window.IsVisible);
            Assert.Equal(2, prompts.Count);
            Assert.Null(window.Workspaces.Current);

            // Do it later: the edit comes back from the journal.
            var again = TabsWorkspaceTests.NewWindow(dir);
            await again.OpenAsync(path);
            Assert.Equal(1, again.Workspaces.Current!.RecoveredEdits);
            again.Close();
        });
    }

    [Fact]
    public async Task OpenRecentListsFilesOpensOneAndTitleShowsTheActiveResource()
    {
        using var dir = new TempDir();
        var a = TestData.WriteSample(dir, "a.win", 2);
        var b = TestData.WriteSample(dir, "b.win", 3);
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                await window.OpenAsync(a);
                await window.OpenAsync(b);
                Dispatcher.UIThread.RunJobs();
                var file = window.GetVisualDescendants().OfType<Menu>().Single().Items.OfType<MenuItem>().First();
                var recent = file.Items.OfType<MenuItem>().Single(m => m.Name == "OpenRecent");
                var entries = recent.Items.OfType<MenuItem>().Where(m => m.Tag is string).ToList();
                Assert.Equal([Path.GetFullPath(b), Path.GetFullPath(a)], entries.Select(m => (string)m.Tag!));

                entries[1].RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(MenuItem.ClickEvent));
                for (var i = 0; i < 100 && window.Workspaces.Current?.Path != Path.GetFullPath(a); i++) { Dispatcher.UIThread.RunJobs(); await Task.Delay(10); }
                Assert.Equal(Path.GetFullPath(a), window.Workspaces.Current!.Path);

                window.Docking.OpenTab("resource", "objects:obj_1", false);
                Dispatcher.UIThread.RunJobs();
                Assert.Equal("obj_1 - New GameMaker Game - G3MStudio", window.Title);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Theory]
    [InlineData("Save", CloseChoice.Save)]
    [InlineData("Later", CloseChoice.DoItLater)]
    [InlineData("Cancel", CloseChoice.Cancel)]
    public async Task ThePromptDialogReturnsTheButtonThatWasPressed(string button, CloseChoice expected)
    {
        await HeadlessApp.Run(async () =>
        {
            var owner = new Window();
            owner.Show();
            var dialog = new ClosePromptWindow(new ClosePrompt("data.win", 3, true));
            var answer = dialog.AskAsync(owner);
            Dispatcher.UIThread.RunJobs();
            Assert.Contains(dialog.GetVisualDescendants().OfType<Button>(), b => b.Name == "Save" && (string?)b.Content == "Sync changes");
            dialog.GetVisualDescendants().OfType<Button>().Single(b => b.Name == button).RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(expected, await answer);
            owner.Close();
        });
    }
}