using G3MStudio.Core.Projects;
using G3MStudio.Core.Settings;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

/// <summary>Files in the user data folder that cannot be read or written for a moment must never cost the user their data.</summary>
public sealed class DataSafetyTests
{
    private static readonly DataFileIdentity Identity = new(Path.GetFullPath("safety-test.win"), 10, "abc");

    [Fact]
    public void ASettingsFileHeldAtStartIsKeptAndNotOverwrittenByAChange()
    {
        if (!OperatingSystem.IsWindows()) return; // needs an exclusive lock
        using var dir = new TempDir();
        var userData = new UserData(dir.File("user"));
        var file = userData.Combine("settings.json");
        File.WriteAllText(file, /*lang=json,strict*/ "{ \"uiScale\": 150 }");

        SettingsStore store;
        using (new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            store = new SettingsStore(userData);
            store.Update(x => x with { UiScale = 110 });
        }

        using (store)
        {
            Assert.True(File.Exists(file));
            Assert.False(File.Exists(file + ".invalid"));
            Assert.Contains("150", File.ReadAllText(file), StringComparison.Ordinal);
            store.Update(x => x with { UiScale = 120 }); // readable again: the stored settings are the base, then the change is written
            Assert.Equal(120, store.Current.UiScale);
            Assert.Contains("120", File.ReadAllText(file), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void AnUnknownEnumNameFallsBackForThatSettingOnly()
    {
        using var dir = new TempDir();
        var userData = new UserData(dir.File("user"));
        File.WriteAllText(userData.Combine("settings.json"), /*lang=json,strict*/ "{ \"uiScale\": 150, \"helpLevel\": \"FromTheFuture\" }");

        using var store = new SettingsStore(userData);

        Assert.Equal(150, store.Current.UiScale);
        Assert.Equal(HelpLevel.All, store.Current.HelpLevel);
        Assert.False(File.Exists(userData.Combine("settings.json.invalid")));
    }

    [Fact]
    public void ASettingsFileThatIsNotJsonIsKeptAside()
    {
        using var dir = new TempDir();
        var userData = new UserData(dir.File("user"));
        File.WriteAllText(userData.Combine("settings.json"), "{ not json");

        using var store = new SettingsStore(userData);

        Assert.Equal("{ not json", File.ReadAllText(userData.Combine("settings.json.invalid")));
    }

    [Fact]
    public void AHandEditThatBreaksTheFileWhileRunningIsKeptAsideBeforeTheNextChange()
    {
        using var dir = new TempDir();
        var userData = new UserData(dir.File("user"));
        using var store = new SettingsStore(userData);
        store.Update(x => x with { UiScale = 110 });
        File.WriteAllText(userData.Combine("settings.json"), "{ my hand edit");

        store.Update(x => x with { UiScale = 120 });

        Assert.Equal("{ my hand edit", File.ReadAllText(userData.Combine("settings.json.invalid")));
        Assert.Equal(120, store.Current.UiScale);
    }

    [Fact]
    public void ARecentListHeldAtStartKeepsItsPinnedEntries()
    {
        if (!OperatingSystem.IsWindows()) return; // needs an exclusive lock
        using var dir = new TempDir();
        var userData = new UserData(dir.File("user"));
        var first = RecentFiles.Load(userData);
        first.Add(dir.File("a.win"), "A", "1", DateTimeOffset.Now);
        first.SetPinned(dir.File("a.win"), true);

        RecentFiles held;
        using (new FileStream(userData.Combine("recent.json"), FileMode.Open, FileAccess.Read, FileShare.None))
        {
            held = RecentFiles.Load(userData);
            held.Add(dir.File("b.win"), "B", "1", DateTimeOffset.Now); // memory only while the file is held
        }

        held.Add(dir.File("c.win"), "C", "1", DateTimeOffset.Now);

        var stored = RecentFiles.Load(userData).Entries;
        Assert.Equal(3, stored.Count);
        Assert.True(stored.Single(e => e.Name == "A").Pinned);
    }

    [Fact]
    public void AFailedJournalResetKeepsTheJournalWritable()
    {
        using var dir = new TempDir();
        var userData = new UserData(dir.File("user"));
        var (journal, _) = Journal.Open(userData, Identity);
        var file = Journal.FileFor(userData, Identity.Path);
        Directory.CreateDirectory(file + ".new"); // the temp name is taken by a folder, so the reset cannot create it
        journal.Append(new JournalRecord("do", "k", new() { ["a"] = "1" }));

        Assert.ThrowsAny<Exception>(() => journal.Reset(Identity, []));
        journal.Append(new JournalRecord("undo"));
        journal.Dispose();

        Directory.Delete(file + ".new");
        var (again, recovered) = Journal.Open(userData, Identity);
        again.Dispose();
        Assert.Equal(["do", "undo"], recovered.Select(r => r.Op));
    }

    [Fact]
    public void ARestoreWithAMissingObjectLeavesTheResourceInPlace()
    {
        using var dir = new TempDir();
        var folder = dir.File("project");
        Directory.CreateDirectory(Path.Combine(folder, "objects"));
        var resource = Path.Combine(folder, "objects", "o.yy");
        File.WriteAllText(resource, "current");
        var store = new SnapshotStore(dir.File("snaps"));
        var snapshot = new Snapshot("1", DateTimeOffset.Now, "x", [new SnapshotUnit("objects/o.yy", "objects", "deadbeef")]);

        Assert.ThrowsAny<Exception>(() => store.Restore(snapshot, "objects/o.yy", folder));

        Assert.Equal("current", File.ReadAllText(resource));
        Assert.False(File.Exists(resource + ".restoring"));

        Directory.CreateDirectory(dir.File("snaps/objects/deadbeef"));
        File.WriteAllText(dir.File("snaps/objects/deadbeef/o.yy"), "old");
        store.Restore(snapshot, "objects/o.yy", folder);
        Assert.Equal("old", File.ReadAllText(resource));
    }

    [Fact]
    public void ARestoreOfALockedResourceKeepsTheCurrentFile()
    {
        if (!OperatingSystem.IsWindows()) return; // an open handle blocks a rename only on Windows
        using var dir = new TempDir();
        var folder = dir.File("project");
        Directory.CreateDirectory(Path.Combine(folder, "objects"));
        var resource = Path.Combine(folder, "objects", "o.yy");
        File.WriteAllText(resource, "current");
        Directory.CreateDirectory(dir.File("snaps/objects/deadbeef"));
        File.WriteAllText(dir.File("snaps/objects/deadbeef/o.yy"), "old");
        var snapshot = new Snapshot("1", DateTimeOffset.Now, "x", [new SnapshotUnit("objects/o.yy", "objects", "deadbeef")]);

        using (new FileStream(resource, FileMode.Open, FileAccess.Read, FileShare.Read))
            Assert.ThrowsAny<IOException>(() => new SnapshotStore(dir.File("snaps")).Restore(snapshot, "objects/o.yy", folder));

        Assert.Equal("current", File.ReadAllText(resource));
        Assert.False(File.Exists(resource + ".replaced"));
        Assert.False(File.Exists(resource + ".restoring"));
    }

    [Fact]
    public void ACutShortManifestIsSkippedAndDoesNotBreakTheNextSnapshot()
    {
        using var dir = new TempDir();
        var root = dir.File("snaps");
        Directory.CreateDirectory(Path.Combine(root, "snapshots"));
        File.WriteAllText(Path.Combine(root, "snapshots", "1.json"), "{\"Id\":\"1\",\"Ti");
        File.WriteAllText(Path.Combine(root, "snapshots", "2.json"), System.Text.Json.JsonSerializer.Serialize(new Snapshot("2", DateTimeOffset.Now, "x", [])));
        var store = new SnapshotStore(root);

        Assert.Equal(["2"], store.List().Select(x => x.Id));
        store.Prune(5);
    }

    [Fact]
    public void PruningDropsTheOldestSnapshotsAndTheObjectsOnlyTheyUsed()
    {
        using var dir = new TempDir();
        var root = dir.File("snaps");
        var store = new SnapshotStore(root);
        Directory.CreateDirectory(Path.Combine(root, "snapshots"));
        foreach (var (id, hash) in new[] { ("1", "aaa"), ("2", "bbb"), ("3", "bbb") })
        {
            Directory.CreateDirectory(Path.Combine(root, "objects", hash));
            File.WriteAllText(Path.Combine(root, "snapshots", id + ".json"), System.Text.Json.JsonSerializer.Serialize(new Snapshot(id, DateTimeOffset.Now, "x", [new SnapshotUnit("o.yy", "objects", hash)])));
        }

        store.Prune(2);

        Assert.Equal(["2", "3"], store.List().Select(x => x.Id));
        Assert.False(Directory.Exists(Path.Combine(root, "objects", "aaa")));
        Assert.True(Directory.Exists(Path.Combine(root, "objects", "bbb")));
    }

    [Fact]
    public async Task ASaveIsRefusedBeforeWritingWhenTheDriveHasNoRoom()
    {
        using var dir = new TempDir();
        var target = TestData.WriteSample(dir, objects: 3);
        var before = await File.ReadAllBytesAsync(target, TestContext.Current.CancellationToken);
        using var data = TestData.Create(5);
        var pipeline = new SavePipeline(new UserData(dir.File("user"))) { FreeSpace = _ => 1024 };

        await Assert.ThrowsAsync<IOException>(() => pipeline.SaveAsync(data, target, cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(before, await File.ReadAllBytesAsync(target, TestContext.Current.CancellationToken));
        Assert.False(File.Exists(target + SavePipeline.NextSuffix));
    }

    [Fact]
    public void ASecondStaleJournalDoesNotReplaceTheFirst()
    {
        using var dir = new TempDir();
        var userData = new UserData(dir.File("user"));
        var file = Journal.FileFor(userData, Identity.Path);
        for (var round = 1; round <= 2; round++)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllText(file, round + "\n");
            Journal.Open(userData, Identity).Journal.Dispose();
        }

        Assert.Equal("1\n", File.ReadAllText(file + ".stale"));
        Assert.Equal("2\n", File.ReadAllText(file + ".stale2"));
    }

    [Fact]
    public void AJournalHeldByAnotherInstanceIsReportedAsInUse()
    {
        using var dir = new TempDir();
        var userData = new UserData(dir.File("user"));
        var (first, _) = Journal.Open(userData, Identity);

        Assert.Throws<DataFileInUseException>(() => Journal.Open(userData, Identity));
        first.Dispose();
    }

    [Fact]
    public async Task ASaveOverAFileThatChangedOnDiskAsksFirstAndIsRefusedWithoutAnAnswer()
    {
        using var dir = new TempDir();
        var userData = new UserData(dir.File("user"));
        var path = TestData.WriteSample(dir, objects: 1);
        using var workspace = await new WorkspaceService(userData, RecentFiles.Load(userData)).OpenAsync(path, cancellationToken: TestContext.Current.CancellationToken);
        var pipeline = new SavePipeline(userData);
        workspace.History.Execute(new RenameResourceCommand("objects", "obj_0", "obj_a"));
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(5)); // another program wrote the file

        var refused = await workspace.SaveAsync(pipeline, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(SaveFailure.ChangedOnDisk, refused.Failure);
        Assert.True(workspace.IsDirty);

        var asked = 0;
        workspace.ConfirmOverwriteChanged = () => { asked++; return Task.FromResult(true); };
        Assert.True((await workspace.SaveAsync(pipeline, cancellationToken: TestContext.Current.CancellationToken)).Saved);
        Assert.True((await workspace.SaveAsync(pipeline, cancellationToken: TestContext.Current.CancellationToken)).Saved); // its own write is not a change
        Assert.Equal(1, asked);
    }

    [Fact]
    public async Task AFileThatCannotBeReadBackAfterTheReplaceIsStillASavedFile()
    {
        if (!OperatingSystem.IsWindows()) return; // needs an exclusive lock
        using var dir = new TempDir();
        var userData = new UserData(dir.File("user"));
        var path = TestData.WriteSample(dir, objects: 1);
        using var workspace = await new WorkspaceService(userData, RecentFiles.Load(userData)).OpenAsync(path, cancellationToken: TestContext.Current.CancellationToken);
        workspace.History.Execute(new RenameResourceCommand("objects", "obj_0", "obj_a"));
        List<FileStream> scanners = [];
        var pipeline = new SavePipeline(userData) { LockRetries = 2, LockRetryDelay = TimeSpan.FromMilliseconds(10), Hook = (step, _) => { if (step == SaveStep.Replaced) scanners.Add(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None)); } };

        try
        {
            var result = await workspace.SaveAsync(pipeline, cancellationToken: TestContext.Current.CancellationToken);
            Assert.True(result.Saved);
            Assert.NotNull(result.Warning);
            Assert.False(workspace.IsDirty);
            Assert.Equal(new FileInfo(path).Length, workspace.Identity.Size);
            Assert.Equal("", workspace.Identity.Md5); // unknown, not the old hash
        }
        finally
        {
            foreach (var scanner in scanners) await scanner.DisposeAsync();
        }

        workspace.ConfirmOverwriteChanged = () => throw new InvalidOperationException("the file this workspace wrote is not a change by another program");
        Assert.True((await workspace.SaveAsync(new SavePipeline(userData), cancellationToken: TestContext.Current.CancellationToken)).Saved);
    }

    [Fact]
    public void OnlyASharingViolationCountsAsAHeldJournal()
    {
        var sharing = new IOException("held", OperatingSystem.IsWindows() ? unchecked((int)0x80070020) : OperatingSystem.IsMacOS() ? 35 : 11);
        Assert.True(Journal.IsHeld(sharing));
        Assert.False(Journal.IsHeld(new IOException("There is not enough space on the disk.", unchecked((int)0x80070070))));
        Assert.False(Journal.IsHeld(new FileNotFoundException("gone")));
    }

    [Fact]
    public async Task AFileOpensWithoutAJournalWhenTheDataFolderCannotHoldOneAndTheWindowIsTold()
    {
        using var dir = new TempDir();
        var userData = new UserData(dir.File("user"));
        await File.WriteAllTextAsync(userData.Combine("journal"), "x", TestContext.Current.CancellationToken); // a file where the journal folder belongs
        var service = new WorkspaceService(userData, RecentFiles.Load(userData));
        Exception? told = null;
        service.CurrentChanged += w => w?.DataWriteFailed += e => told = e;

        using var workspace = await service.OpenAsync(TestData.WriteSample(dir, objects: 1), cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(told);
        Assert.True(workspace.JournalLost);
        workspace.History.Execute(new RenameResourceCommand("objects", "obj_0", "obj_a")); // edits still work without a journal
        Assert.True(workspace.IsDirty);
    }

    [Fact]
    public async Task SaveAsOntoTheOpenFileIsAnOrdinarySave()
    {
        using var dir = new TempDir();
        var userData = new UserData(dir.File("user"));
        using var workspace = await new WorkspaceService(userData, RecentFiles.Load(userData)).OpenAsync(TestData.WriteSample(dir, objects: 1), cancellationToken: TestContext.Current.CancellationToken);
        workspace.Edit(ResourceTemplates.New(workspace.Data, "paths", "pth_x"));
        Assert.True(workspace.IsDirty);

        // Another spelling of the same file: other letter case where paths ignore it, a dot segment on Linux where they do not.
        var spelled = OperatingSystem.IsLinux() ? Path.Combine(Path.GetDirectoryName(workspace.Path)!, ".", Path.GetFileName(workspace.Path)) : workspace.Path.ToUpperInvariant();
        var result = await workspace.SaveCopyAsync(new SavePipeline(userData), spelled, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Saved);
        Assert.False(workspace.IsDirty);
    }

    [Fact]
    public async Task AnOpenRemovesTheTempFileOfAKilledSave()
    {
        using var dir = new TempDir();
        var userData = new UserData(dir.File("user"));
        var path = TestData.WriteSample(dir, objects: 1);
        await File.WriteAllTextAsync(path + SavePipeline.NextSuffix, "half", TestContext.Current.CancellationToken);

        using var workspace = await new WorkspaceService(userData, RecentFiles.Load(userData)).OpenAsync(path, cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(File.Exists(path + SavePipeline.NextSuffix));
    }

    [Fact]
    public void AChangeFromAnotherInstanceIsTheBaseOfTheNextUpdate()
    {
        using var dir = new TempDir();
        var userData = new UserData(dir.File("user"));
        using var a = new SettingsStore(userData);
        using var b = new SettingsStore(userData);
        a.Update(x => x with { UiScale = 130 });

        b.Update(x => x with { CodeFontSize = 17 });

        Assert.Equal(130, b.Current.UiScale);
        Assert.Contains("130", File.ReadAllText(userData.Combine("settings.json")), StringComparison.Ordinal);
    }
}
