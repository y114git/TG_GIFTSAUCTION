using G3MStudio.Core.Settings;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

/// <summary>A remembered data file that is lost, moved, renamed, changed or locked. Plain files stand in for data files: only size and MD5 matter here.</summary>
public sealed class LostFileTests
{
    private static async Task<DataFileIdentity> Make(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, content, TestContext.Current.CancellationToken);
        return await DataFileIdentity.ComputeAsync(path, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CheckTellsMissingChangedLockedAndFine()
    {
        using var dir = new TempDir();
        var token = TestContext.Current.CancellationToken;
        var id = await Make(dir.File("a/data.win"), "original");
        Assert.Equal(LostReason.None, await LostFile.CheckAsync(id.Path, id.Size, id.Md5, token));
        Assert.Equal(LostReason.None, await LostFile.CheckAsync(id.Path, 0, null, token)); // an old entry without a hash is used as it is
        Assert.Equal(LostReason.Missing, await LostFile.CheckAsync(dir.File("a/none.win"), id.Size, id.Md5, token));
        Assert.Equal(LostReason.Changed, await LostFile.CheckAsync(id.Path, id.Size + 1, id.Md5, token));
        Assert.Equal(LostReason.Changed, await LostFile.CheckAsync(id.Path, id.Size, new string('0', 32), token));
        await using var hold = new FileStream(id.Path, FileMode.Open, FileAccess.Read, FileShare.None);
        Assert.Equal(LostReason.Unreadable, await LostFile.CheckAsync(id.Path, id.Size, id.Md5, token));
    }

    [Fact]
    public async Task ARenamedFileAndAMovedFolderAreFoundByTheirHash()
    {
        using var dir = new TempDir();
        var token = TestContext.Current.CancellationToken;
        var id = await Make(dir.File("games/old/data.win"), "original content");
        File.Move(id.Path, dir.File("games/old/renamed.win"));
        var renamed = await LostFile.FindAsync(id.Path, id.Size, id.Md5, [], token);
        Assert.Equal([new Candidate(Path.GetFullPath(dir.File("games/old/renamed.win")), true)], renamed);

        Directory.Move(dir.File("games/old"), dir.File("games/new"));
        var moved = await LostFile.FindAsync(id.Path, id.Size, id.Md5, [], token); // the old folder is gone: found in a folder beside it
        Assert.Equal(Path.GetFullPath(dir.File("games/new/renamed.win")), Assert.Single(moved).Path);
    }

    [Fact]
    public async Task AnEntryWithoutASizeIsFoundByItsHashAlone()
    {
        using var dir = new TempDir();
        var token = TestContext.Current.CancellationToken;
        var id = await Make(dir.File("a/data.win"), "original content");
        File.Move(id.Path, dir.File("a/renamed.win"));
        var found = await LostFile.FindAsync(id.Path, 0, id.Md5, [], token);
        Assert.Equal([new Candidate(Path.GetFullPath(dir.File("a/renamed.win")), true)], found);
    }

    [Fact]
    public async Task OtherFoldersAreSearchedAndOnlyDataExtensionsCount()
    {
        using var dir = new TempDir();
        var token = TestContext.Current.CancellationToken;
        var id = await Make(dir.File("one/data.win"), "same");
        File.Delete(id.Path);
        await Make(dir.File("far/away/copy.unx"), "same");
        await Make(dir.File("far/away/notes.txt"), "same");
        var found = await LostFile.FindAsync(id.Path, id.Size, id.Md5, [dir.File("far/away"), dir.File("no/such")], token);
        Assert.Equal(Path.GetFullPath(dir.File("far/away/copy.unx")), Assert.Single(found).Path);
    }

    [Fact]
    public async Task ASameNamedFileWithOtherContentIsOfferedButNeverUsedByItself()
    {
        using var dir = new TempDir();
        var token = TestContext.Current.CancellationToken;
        var id = await Make(dir.File("a/data.win"), "version one");
        await Make(dir.File("a/data.win"), "version two, longer"); // the game was updated in place
        var found = await LostFile.FindAsync(dir.File("b/data.win"), id.Size, id.Md5, [dir.File("a")], token);
        var one = Assert.Single(found);
        Assert.False(one.SameHash);
        Assert.Null(LostFile.Unique(found));
    }

    [Fact]
    public async Task SeveralFilesWithTheHashAreNotGuessed()
    {
        using var dir = new TempDir();
        var token = TestContext.Current.CancellationToken;
        var id = await Make(dir.File("a/data.win"), "same");
        File.Delete(id.Path);
        await Make(dir.File("a/copy1.win"), "same");
        await Make(dir.File("a/copy2.win"), "same");
        var found = await LostFile.FindAsync(id.Path, id.Size, id.Md5, [], token);
        Assert.Equal(2, found.Count(c => c.SameHash));
        Assert.Null(LostFile.Unique(found));
        File.Delete(dir.File("a/copy2.win"));
        Assert.NotNull(LostFile.Unique(await LostFile.FindAsync(id.Path, id.Size, id.Md5, [], token)));
    }

    [Fact]
    public void AMismatchPolicyMapsToKeepingOrDroppingTheLayout()
    {
        Assert.True(LostFile.KeepLayout(MismatchPolicy.KeepLayout));
        Assert.False(LostFile.KeepLayout(MismatchPolicy.FreshLayout));
        Assert.Null(LostFile.KeepLayout(MismatchPolicy.Ask));
        Assert.Null(LostFile.KeepLayout(MismatchPolicy.Refuse));
    }

    [Fact]
    public void AdoptingGivesTheNewPathTheOldLayoutAndForgetDropsOnlyTheLayout()
    {
        using var dir = new TempDir();
        var data = new UserData(dir.File("data"));
        var store = new ViewStateStore(data);
        store.Save("C:/games/old.win", new ViewState("layout", []));
        store.SaveLayers("C:/games/old.win", new Dictionary<string, string> { ["k"] = "v" });
        Assert.Equal(2, data.AdoptFileData("C:/games/old.win", "C:/games/new.win"));
        Assert.Equal("layout", store.Load("C:/games/new.win")?.Dock);
        Assert.NotNull(store.LoadLayers("C:/games/new.win"));
        store.Save("C:/games/new.win", new ViewState("own", []));
        Assert.Equal(0, data.AdoptFileData("C:/games/old.win", "C:/games/new.win")); // a file with its own layout keeps it
        Assert.Equal("own", store.Load("C:/games/new.win")?.Dock);
        Assert.True(data.AdoptFileData("C:/games/old.win", "C:/games/new.win", overwrite: true) > 0);
        Assert.Equal("layout", store.Load("C:/games/new.win")?.Dock);

        data.ForgetLayout("C:/games/new.win");
        Assert.Null(store.Load("C:/games/new.win"));
        Assert.NotNull(store.LoadLayers("C:/games/new.win"));
        Assert.Equal("layout", store.Load("C:/games/old.win")?.Dock);
    }

    [Fact]
    public void AdoptingSkipsTemporaryLeftoversOfAtomicWrites()
    {
        using var dir = new TempDir();
        var data = new UserData(dir.File("data"));
        var key = UserData.KeyFor("C:/games/old.win");
        Directory.CreateDirectory(data.Combine("locks"));
        File.WriteAllText(data.Combine("locks", key + ".json"), "[]");
        File.WriteAllText(data.Combine("locks", key + ".json.0123abcd.tmp"), "[");
        Assert.Equal(1, data.AdoptFileData("C:/games/old.win", "C:/games/new.win"));
        Assert.Equal([UserData.KeyFor("C:/games/new.win") + ".json"], Directory.GetFiles(data.Combine("locks"), UserData.KeyFor("C:/games/new.win") + "*").Select(Path.GetFileName));
    }

    [Fact]
    public void AdoptingTakesOnlyTheKnownFileNamesAndLeavesNothingWhenACopyFails()
    {
        using var dir = new TempDir();
        var data = new UserData(dir.File("data"));
        var (oldKey, newKey) = (UserData.KeyFor("C:/games/old.win"), UserData.KeyFor("C:/games/new.win"));
        foreach (var folder in new[] { "view", "organize" }) Directory.CreateDirectory(data.Combine(folder));
        File.WriteAllText(data.Combine("view", oldKey + ".json"), "[]");
        File.WriteAllText(data.Combine("view", oldKey + ".json.bak"), "[");
        File.WriteAllText(data.Combine("view", oldKey + "x.json"), "[");
        File.WriteAllText(data.Combine("organize", oldKey + ".json"), "[]");
        Directory.CreateDirectory(data.Combine("organize", newKey + ".json")); // the second copy cannot be written
        Assert.ThrowsAny<Exception>(() => data.AdoptFileData("C:/games/old.win", "C:/games/new.win"));
        Assert.Empty(Directory.GetFiles(data.Combine("view"), newKey + "*"));

        Directory.Delete(data.Combine("organize", newKey + ".json"));
        Assert.Equal(2, data.AdoptFileData("C:/games/old.win", "C:/games/new.win"));
        Assert.Equal([newKey + ".json"], Directory.GetFiles(data.Combine("view"), newKey + "*").Select(Path.GetFileName));
    }

    [Fact]
    public async Task RecentFoldersAreSearchedEvenWhenTheParentHasManyFolders()
    {
        using var dir = new TempDir();
        var token = TestContext.Current.CancellationToken;
        var id = await Make(dir.File("games/old/data.win"), "same");
        File.Delete(id.Path);
        for (var i = 0; i < LostFile.MaxFolders + 6; i++) Directory.CreateDirectory(dir.File($"games/sibling{i:00}"));
        await Make(dir.File("elsewhere/copy.win"), "same");
        var found = await LostFile.FindAsync(id.Path, id.Size, id.Md5, [dir.File("elsewhere")], token);
        Assert.Equal(Path.GetFullPath(dir.File("elsewhere/copy.win")), Assert.Single(found).Path);
    }

    [Fact]
    public void AMovedFileKeepsItsRunProfileUnlessTheNewPathHasOne()
    {
        using var dir = new TempDir();
        var user = new UserData(dir.File("user"));
        var service = new WorkspaceService(user, RecentFiles.Load(user));
        var (old, moved, other) = (Path.GetFullPath(dir.File("a/data.win")), Path.GetFullPath(dir.File("b/data.win")), Path.GetFullPath(dir.File("c/data.win")));
        service.Settings.Update(x => x with { FileRunProfiles = new Dictionary<string, string> { [old] = "Debug", [other] = "Release" } });
        service.MoveRunProfile(old, moved);
        service.MoveRunProfile(old, other); // the file there has its own
        Assert.Equal("Debug", service.Settings.Current.FileRunProfiles[moved]);
        Assert.Equal("Release", service.Settings.Current.FileRunProfiles[other]);
    }

    [Fact]
    public void AMovedWorkspaceReplacesTheLocksAndNicknamesOfTheNewPathWithItsOwn()
    {
        using var dir = new TempDir();
        var user = new UserData(dir.File("data"));
        var (old, target) = ("C:/games/old.win", "C:/games/new.win");
        var locks = new ResourceLocks(user, old);
        var nicknames = new Nicknames(user, old);
        locks.Set("objects:obj_a", true);
        nicknames.Set("objects:obj_a", "Alpha");
        new ResourceLocks(user, target).Set("objects:obj_stale", true); // what an earlier file of that path left
        new Nicknames(user, target).Set("objects:obj_stale", "Stale");

        user.AdoptFileData(old, target, overwrite: true);
        locks.MoveTo(user, target);
        nicknames.MoveTo(user, target);
        var saved = new ResourceLocks(user, target);
        Assert.True(saved.IsLocked("objects:obj_a"));
        Assert.False(saved.IsLocked("objects:obj_stale"));
        Assert.Equal("Alpha", new Nicknames(user, target).Of("objects:obj_a"));
        Assert.Null(new Nicknames(user, target).Of("objects:obj_stale"));

        // A workspace with none of its own still empties a stale list at the new path, so the saved lists equal the live ones.
        var empty = new Nicknames(user, "C:/games/none.win");
        new Nicknames(user, "C:/games/dest.win").Set("objects:obj_stale", "Stale");
        empty.MoveTo(user, "C:/games/dest.win");
        Assert.False(new Nicknames(user, "C:/games/dest.win").Any);
    }

    [Fact]
    public async Task TheRecentListKeepsTheHashAndARefreshChangesIt()
    {
        using var dir = new TempDir();
        var data = new UserData(dir.File("data"));
        var id = await Make(dir.File("g/data.win"), "x");
        var recent = RecentFiles.Load(data);
        recent.Add(id.Path, "Game", "1", DateTimeOffset.Now, id);
        Assert.Equal(id.Md5, RecentFiles.Load(data).Find(id.Path)?.Md5);
        recent.Refresh(id with { Size = 99, Md5 = "abc" });
        var entry = RecentFiles.Load(data).Find(id.Path);
        Assert.Equal((99, "abc"), (entry?.Size, entry?.Md5));
    }
}
