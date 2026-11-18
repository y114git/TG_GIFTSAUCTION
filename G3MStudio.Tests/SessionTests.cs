using G3MStudio.Core.Settings;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

/// <summary>Start behaviour and lost data files through the main window. Every test gets its own user data folder, so nothing touches the real settings.</summary>
public sealed class SessionTests
{
    private static MainWindow Start(TempDir dir, Func<AppSettings, AppSettings>? settings = null)
    {
        var window = TabsWorkspaceTests.NewWindow(dir);
        if (settings is not null) window.Workspaces.Settings.Update(settings);
        window.LostFileAsker = _ => throw new InvalidOperationException("the dialog must not open");
        return window;
    }

    [Fact]
    public async Task ARestoreStartReopensTheLastFileWithItsTabs()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir);
        await HeadlessApp.Run(async () =>
        {
            var first = Start(dir);
            await first.OpenAsync(path);
            first.Docking.OpenTab("resource", "objects:obj_1", false);
            first.Close();
            var second = Start(dir);
            try
            {
                Assert.Null(second.Workspaces.Current);
                await second.StartSessionAsync();
                Assert.Equal(Path.GetFullPath(path), second.Workspaces.Current?.Path);
                Assert.Equal("objects:obj_1", second.Docking.Tabs.Tabs.Single().Key);
            }
            finally
            {
                second.Close();
            }
        });
    }

    [Fact]
    public async Task HomeModeAndAnAskedNoShowHomeAndAnAskedYesOpens()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir);
        await HeadlessApp.Run(async () =>
        {
            var first = Start(dir);
            await first.OpenAsync(path);
            first.Close();
            var window = Start(dir, x => x with { Startup = StartupMode.Home });
            try
            {
                await window.StartSessionAsync();
                Assert.Null(window.Workspaces.Current);
                window.Workspaces.Settings.Update(x => x with { Startup = StartupMode.Ask });
                window.StartAsker = _ => Task.FromResult(false);
                await window.StartSessionAsync();
                Assert.Null(window.Workspaces.Current);
                var asked = "";
                window.StartAsker = name => { asked = name; return Task.FromResult(true); };
                await window.StartSessionAsync();
                Assert.Equal("data.win", asked);
                Assert.NotNull(window.Workspaces.Current);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task ClosingTheFileOnPurposeMakesTheNextStartShowHome()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir);
        await HeadlessApp.Run(async () =>
        {
            var window = Start(dir);
            try
            {
                await window.OpenAsync(path);
                Assert.NotNull(window.Workspaces.Settings.Current.LastSession);
                await window.CloseWorkspaceAsync();
                Assert.Null(window.Workspaces.Settings.Current.LastSession);
                await window.StartSessionAsync();
                Assert.Null(window.Workspaces.Current);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task AMissingFileIsAskedAboutAndGoingHomeLeavesNothingOpen()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir);
        await HeadlessApp.Run(async () =>
        {
            var first = Start(dir);
            await first.OpenAsync(path);
            first.Close();
            File.Delete(path);
            var window = Start(dir);
            try
            {
                LostRequest? seen = null;
                window.LostFileAsker = request => { seen = request; return Task.FromResult(new LostChoice(LostChoiceKind.Home)); };
                await window.StartSessionAsync();
                Assert.Equal(LostReason.Missing, seen?.Reason);
                Assert.Empty(seen!.Entries);
                Assert.Null(window.Workspaces.Current);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task AMovedFileWithTheSameHashOpensAtOnceAndTakesOverTheLayoutAndTheOldEntryGoes()
    {
        using var dir = new TempDir();
        Directory.CreateDirectory(dir.File("old"));
        var path = TestData.WriteSample(dir, "old/data.win");
        await HeadlessApp.Run(async () =>
        {
            var first = Start(dir);
            await first.OpenAsync(path);
            first.Docking.OpenTab("resource", "objects:obj_2", false);
            first.Close();
            Directory.Move(dir.File("old"), dir.File("renamed"));
            var window = Start(dir);
            try
            {
                await window.StartSessionAsync();
                Assert.Equal(Path.GetFullPath(dir.File("renamed/data.win")), window.Workspaces.Current?.Path);
                Assert.Equal("objects:obj_2", window.Docking.Tabs.Tabs.Single().Key);
                Assert.Null(window.Workspaces.Recent.Find(path));
                Assert.Contains(window.Notifications.History, n => n.Text.Contains("same content", StringComparison.Ordinal));
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task TwoCopiesWithTheHashAreNotGuessedAndTheChosenOneOpens()
    {
        using var dir = new TempDir();
        Directory.CreateDirectory(dir.File("a"));
        var path = TestData.WriteSample(dir, "a/data.win");
        await HeadlessApp.Run(async () =>
        {
            var first = Start(dir);
            await first.OpenAsync(path);
            first.Close();
            File.Move(path, dir.File("a/one.win"));
            File.Copy(dir.File("a/one.win"), dir.File("a/two.win"));
            var window = Start(dir);
            try
            {
                LostRequest? seen = null;
                window.LostFileAsker = request => { seen = request; return Task.FromResult(new LostChoice(LostChoiceKind.Open, request.Entries[1].Path)); };
                await window.StartSessionAsync();
                Assert.Equal(2, seen!.Entries.Count(e => e.SameHash));
                Assert.NotNull(window.Workspaces.Current);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task APickedFileOfOtherContentKeepsItsOwnLayoutAndAnUnansweredCloseChangesNothing()
    {
        using var dir = new TempDir();
        Directory.CreateDirectory(dir.File("a"));
        Directory.CreateDirectory(dir.File("b"));
        var lost = TestData.WriteSample(dir, "a/data.win");
        var other = TestData.WriteSample(dir, "b/data.win", objects: 5);
        await HeadlessApp.Run(async () =>
        {
            var first = Start(dir);
            await first.OpenAsync(lost);
            first.Docking.OpenTab("resource", "objects:obj_0", false);
            first.Close();
            var second = Start(dir);
            await second.OpenAsync(other);
            second.Docking.OpenTab("resource", "objects:obj_1", false);
            second.Close();
            var good = await DataFileIdentity.ComputeAsync(lost, TestContext.Current.CancellationToken);
            File.Delete(lost);

            var window = Start(dir);
            try
            {
                window.LostFileAsker = _ => Task.FromResult(new LostChoice(LostChoiceKind.Open, other, true));
                Assert.True(await window.OpenRememberedAsync(lost, good.Size, good.Md5));
                Assert.Equal("objects:obj_1", window.Docking.Tabs.Tabs.Single().Key); // its own tabs, not the ones of the lost file
                window.Workspaces.Close();

                // Unsaved edits in another file and the question answered with Cancel: the picked file gets nothing of the lost layout.
                var third = TestData.WriteSample(dir, "c.win", objects: 2, withCode: true);
                var fresh = TestData.WriteSample(dir, "b/fresh.win", objects: 4);
                await window.OpenAsync(third);
                window.Workspaces.Current!.Edit(new EditCodeCommand("gml_Script_probe", "return 2;"));
                window.AskClose = _ => Task.FromResult(CloseChoice.Cancel);
                var asked = 0;
                window.LostFileAsker = _ => Task.FromResult(asked++ == 0 ? new LostChoice(LostChoiceKind.Open, fresh, true) : new LostChoice(LostChoiceKind.Home));
                Assert.False(await window.OpenRememberedAsync(lost, good.Size, good.Md5));
                Assert.Equal(2, asked);
                Assert.Null(new ViewStateStore(window.Workspaces.UserData).Load(fresh));
                Assert.Equal(Path.GetFullPath(third), window.Workspaces.Current?.Path);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task AFileThatFailsToOpenTakesNothingOfTheLostFilesLayoutAndRunProfile()
    {
        using var dir = new TempDir();
        Directory.CreateDirectory(dir.File("a"));
        Directory.CreateDirectory(dir.File("b"));
        var lost = TestData.WriteSample(dir, "a/data.win");
        var junk = dir.File("b/junk.win");
        await File.WriteAllTextAsync(junk, "not a data file", TestContext.Current.CancellationToken);
        await HeadlessApp.Run(async () =>
        {
            var first = Start(dir);
            await first.OpenAsync(lost);
            first.Docking.OpenTab("resource", "objects:obj_0", false);
            first.Close();
            var good = await DataFileIdentity.ComputeAsync(lost, TestContext.Current.CancellationToken);
            File.Delete(lost);

            var window = Start(dir);
            try
            {
                window.Workspaces.Settings.Update(x => x with { FileRunProfiles = new Dictionary<string, string> { [Path.GetFullPath(lost)] = "Debug" } });
                var asked = 0;
                window.LostFileAsker = _ => Task.FromResult(asked++ == 0 ? new LostChoice(LostChoiceKind.Open, junk, true) : new LostChoice(LostChoiceKind.Home));
                Assert.False(await window.OpenRememberedAsync(lost, good.Size, good.Md5));
                Assert.Equal(2, asked);
                Assert.Null(new ViewStateStore(window.Workspaces.UserData).Load(junk));
                Assert.DoesNotContain(Path.GetFullPath(junk), window.Workspaces.Settings.Current.FileRunProfiles.Keys);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task UnsavedEditsAreAskedAboutBeforeTheOpenFileIsReplacedByItsChangedSelf()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir, objects: 2, withCode: true);
        await HeadlessApp.Run(async () =>
        {
            var window = Start(dir);
            try
            {
                await window.OpenAsync(path);
                var open = window.Workspaces.Current!;
                var old = open.Identity;
                open.Edit(new EditCodeCommand("gml_Script_probe", "return 2;"));
                TestData.WriteSample(dir, objects: 5, withCode: true); // the file changed under the open workspace
                var asked = 0;
                window.AskClose = _ =>
                {
                    asked++;
                    return Task.FromResult(CloseChoice.Cancel);
                };
                var rounds = 0;
                window.LostFileAsker = _ => Task.FromResult(rounds++ == 0 ? new LostChoice(LostChoiceKind.Open, path, true) : new LostChoice(LostChoiceKind.Home));
                await window.OpenRememberedAsync(path, old.Size, old.Md5);
                Assert.Equal(1, asked);
                Assert.Same(open, window.Workspaces.Current); // the edits are still there
                Assert.True(open.IsDirty);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task AChangedFileFollowsThePolicyAskKeepFreshOrRefuse()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir);
        await HeadlessApp.Run(async () =>
        {
            var first = Start(dir);
            await first.OpenAsync(path);
            first.Docking.OpenTab("resource", "objects:obj_0", false);
            first.Close();
            TestData.WriteSample(dir, objects: 5); // the game was updated in place
            var window = Start(dir);
            try
            {
                // Ask: the changed file itself is the first entry, and the answer says whether the layout stays.
                LostRequest? seen = null;
                window.LostFileAsker = request => { seen = request; return Task.FromResult(new LostChoice(LostChoiceKind.Open, path, false)); };
                await window.StartSessionAsync();
                Assert.Equal(LostReason.Changed, seen?.Reason);
                Assert.True(seen!.ChooseLayout);
                Assert.NotNull(window.Workspaces.Current);
                Assert.Empty(window.Docking.Tabs.Tabs); // fresh layout was chosen
                window.Workspaces.Close();

                window.LostFileAsker = _ => throw new InvalidOperationException("the policy decides");
                window.Workspaces.Settings.Update(x => x with { HashMismatch = MismatchPolicy.KeepLayout, LastSession = x.LastSession! with { Md5 = new string('1', 32) } });
                await window.StartSessionAsync();
                Assert.NotNull(window.Workspaces.Current);
                window.Workspaces.Close();

                window.Workspaces.Settings.Update(x => x with { HashMismatch = MismatchPolicy.Refuse, LastSession = x.LastSession! with { Md5 = new string('2', 32) } });
                window.LostFileAsker = request => Task.FromResult(new LostChoice(LostChoiceKind.Home));
                await window.StartSessionAsync();
                Assert.Null(window.Workspaces.Current);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task ABrokenFileFallsBackToHomeAndRecentRemovalForgetsTheEntry()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir);
        await HeadlessApp.Run(async () =>
        {
            var first = Start(dir);
            await first.OpenAsync(path);
            first.Close();
            var good = await DataFileIdentity.ComputeAsync(path, TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(path, "not a data file", TestContext.Current.CancellationToken);
            var window = Start(dir);
            try
            {
                window.Workspaces.Settings.Update(x => x with { HashMismatch = MismatchPolicy.KeepLayout });
                await window.StartSessionAsync(); // the file is there and changed, the policy opens it, the read fails: Home
                Assert.Null(window.Workspaces.Current);

                File.Delete(path);
                window.LostFileAsker = _ => Task.FromResult(new LostChoice(LostChoiceKind.RemoveRecent));
                Assert.False(await window.OpenRememberedAsync(path, good.Size, good.Md5));
                Assert.Null(window.Workspaces.Recent.Find(path));
            }
            finally
            {
                window.Close();
            }
        });
    }
}
