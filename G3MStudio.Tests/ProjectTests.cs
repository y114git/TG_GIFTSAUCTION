using G3MLib.DataFile;
using G3MLib.DataFile.Models;
using G3MLib.DataFile.Project;
using G3MLib.Modding.Resources;
using G3MStudio.Core.Projects;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

public sealed class ProjectTests
{
    private sealed class Session(TempDir dir)
    {
        public UserData UserData { get; } = new(dir.File("user"));

        public WorkspaceService Service { get; private set; } = null!;

        public string Root => dir.File("projects");

        public SavePipeline Pipeline => new(UserData);

        public async Task<Workspace> Open(string path)
        {
            Service?.Close();
            Service = new WorkspaceService(UserData, RecentFiles.Load(UserData));
            return await Service.OpenAsync(path);
        }

        public async Task<(Workspace Workspace, ProjectLink Link)> Enabled(string path)
        {
            var workspace = await Open(path);
            var link = await Service.Projects.EnableAsync(workspace, Root, remember: true);
            return (workspace, link);
        }
    }

    private static EditCodeCommand Code(string text) => new("gml_Script_probe", text);

    private static string Gml(ProjectLink link) => File.ReadAllText(Path.Combine(link.Folder, "code", "gml_Script_probe", "gml_Script_probe.gml"));

    private static Task<G3MProject> OpenProject(ProjectLink link) => G3MProjectService.OpenAsync(link.ProjectFile);

    [Fact]
    public async Task EnableExportsEveryResourceInTheBackgroundAndQueuesEditsMadeMeanwhile()
    {
        using var dir = new TempDir();
        var session = new Session(dir);
        var path = TestData.WriteSample(dir, objects: 3, withCode: true);
        var workspace = await session.Open(path);
        var counts = new List<BatchProgress>();
        Assert.True(session.Service.Projects.Registry.NeedsLocationQuestion);
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.Service.Projects.EnableAsync(workspace, cancellationToken: TestContext.Current.CancellationToken));

#pragma warning disable CA2025 // awaited below, before the workspace goes
        var enabling = session.Service.Projects.EnableAsync(workspace, session.Root, remember: true, new SyncProgress<BatchProgress>(counts.Add), cancellationToken: TestContext.Current.CancellationToken);
#pragma warning restore CA2025
        Assert.False(workspace.Edit(Code("return 5;")));
        Assert.False(workspace.Edit(new RenameResourceCommand("objects", "obj_9", "x")));
        Assert.Empty(workspace.History.Items);
        var failed = new List<EditCommand>();
        workspace.QueuedEditFailed += (c, _) => failed.Add(c);
        var link = await enabling;

        Assert.Same(link, workspace.Project);
        Assert.Single(workspace.History.Items);
        Assert.Single(failed);
        Assert.Equal(ProjectState.NotSynced, link.State);
        var project = await OpenProject(link);
        Assert.Equal("full", project.Mode);
        Assert.Equal(path, Path.GetFullPath(path));
        Assert.Contains(project.Resources, r => r.Type == "CodeEntries" && r.Name == "gml_Script_probe");
        Assert.Equal(3, project.OfType("GameObjects").Count());
        Assert.StartsWith(session.Root, link.Folder, StringComparison.Ordinal);
        Assert.False(session.Service.Projects.Registry.NeedsLocationQuestion);
        Assert.Equal(session.Root, session.Service.Projects.Registry.RememberedRoot);
        Assert.Contains("return 1", Gml(link), StringComparison.Ordinal);
        Assert.NotEmpty(counts);
        Assert.Contains(counts, c => c.Total > 0 && c.Done == c.Total); // reports from parallel workers and phases arrive in no fixed order
    }

    [Fact]
    public async Task CancellingTheFirstExportRemovesTheProjectAndLetsEditsRunAgain()
    {
        using var dir = new TempDir();
        var session = new Session(dir);
        var workspace = await session.Open(TestData.WriteSample(dir, objects: 3, withCode: true));
        using var cancel = new CancellationTokenSource();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => session.Service.Projects.EnableAsync(
            workspace, session.Root, progress: new SyncProgress<BatchProgress>(_ => cancel.Cancel()), cancellationToken: cancel.Token));

        Assert.Null(workspace.Project);
        Assert.Empty(session.Service.Projects.Registry.Projects);
        Assert.Empty(Directory.GetFileSystemEntries(session.Root));
        Assert.True(workspace.Edit(Code("return 7;")));
    }

    [Fact]
    public async Task AutosaveWritesOnlyTheChangedResourcesAndKeepsTheProjectFileInStep()
    {
        using var dir = new TempDir();
        var session = new Session(dir);
        var (workspace, link) = await session.Enabled(TestData.WriteSample(dir, objects: 3, withCode: true));
        var room = Path.Combine(link.Folder, "rooms", "room0", "room.json");
        File.SetLastWriteTimeUtc(room, new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var before = await OpenProject(link);

        workspace.Edit(Code("return 2;"));
        Assert.True(link.HasPendingWrite);
        var written = await link.SaveToProjectAsync(TestContext.Current.CancellationToken);

        Assert.InRange(written, 1, 4);
        Assert.False(link.HasPendingWrite);
        Assert.Contains("return 2", Gml(link), StringComparison.Ordinal);
        Assert.Equal(new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc), File.GetLastWriteTimeUtc(room));
        var after = await OpenProject(link);
        Assert.Equal(before.Resources.Count, after.Resources.Count);
        Assert.NotEqual(before.Find("code/gml_Script_probe").Hash, after.Find("code/gml_Script_probe").Hash);
        Assert.Equal(before.Find("rooms/room0").Hash, after.Find("rooms/room0").Hash);

        // The written project is what a fresh export of the model gives.
        using var fresh = new TempDir();
        var export = G3MProjectService.Export(workspace.Data, fresh.File("p"), null, new G3MProjectOptions { Name = link.Record.Name }, cancellationToken: TestContext.Current.CancellationToken);
        var reference = await G3MProjectService.OpenAsync(export.Path, cancellationToken: TestContext.Current.CancellationToken);
        Assert.All(reference.Resources, r => Assert.Equal(r.Hash, after.Find(r.Path)?.Hash));
        Assert.Empty(Directory.GetDirectories(link.Folder, ".staging-*"));
    }

    [Fact]
    public async Task ARenameRefreshesTheProjectAndRemovesTheOldResourceFolders()
    {
        using var dir = new TempDir();
        var session = new Session(dir);
        var (workspace, link) = await session.Enabled(TestData.WriteSample(dir, objects: 3, withCode: true));
        var room = Path.Combine(link.Folder, "rooms", "room0", "room.json");
        File.SetLastWriteTimeUtc(room, new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        workspace.Edit(new RenameResourceCommand("objects", "obj_0", "obj_a"));
        var written = await link.SaveToProjectAsync(TestContext.Current.CancellationToken);

        Assert.True(written >= 1);
        Assert.Equal(new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc), File.GetLastWriteTimeUtc(room));
        var project = await OpenProject(link);
        Assert.Contains(project.OfType("GameObjects"), r => r.Name.StartsWith("obj_a", StringComparison.Ordinal));
        Assert.DoesNotContain(project.OfType("GameObjects"), r => r.Name.StartsWith("obj_0", StringComparison.Ordinal));
        Assert.Empty(Directory.GetDirectories(Path.Combine(link.Folder, "objects"), "obj_0*"));
        Assert.Contains("obj_a", project.ResourceOrder["GameObjects"]);
    }

    [Fact]
    public async Task ARenameWritesOnlyTheRenamedAndReferencingResourcesAndEqualsAFreshExport()
    {
        using var dir = new TempDir();
        var session = new Session(dir);
        using (var data = TestData.Create(3, withCode: true))
        {
            data.Rooms[0].GameObjects.Add(new GameMakerRoom.GameObject { ObjectDefinition = data.GameObjects.ByName("obj_0") });
            TestData.Write(dir.File("data.win"), data);
        }

        var (workspace, link) = await session.Enabled(dir.File("data.win"));
        workspace.Edit(Code("return obj_0;"));
        await link.SaveToProjectAsync(TestContext.Current.CancellationToken);
        var other = Directory.GetDirectories(Path.Combine(link.Folder, "objects"), "obj_1*").Single();
        foreach (var file in Directory.EnumerateFiles(other)) File.SetLastWriteTimeUtc(file, new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var before = await OpenProject(link);

        workspace.Edit(new RenameResourceCommand("objects", "obj_0", "obj_a"));
        var written = await link.SaveToProjectAsync(TestContext.Current.CancellationToken);

        Assert.InRange(written, 3, before.Resources.Count - 3);
        var after = await OpenProject(link);
        Assert.NotEqual(before.Find("rooms/room0").Hash, after.Find("rooms/room0").Hash);
        Assert.NotEqual(before.Find("code/gml_Script_probe").Hash, after.Find("code/gml_Script_probe").Hash);
        Assert.Contains("obj_a", (await File.ReadAllTextAsync(Path.Combine(link.Folder, "rooms", "room0", "room.json"), TestContext.Current.CancellationToken)), StringComparison.Ordinal);
        Assert.All(Directory.EnumerateFiles(other), f => Assert.Equal(new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc), File.GetLastWriteTimeUtc(f)));
        using var fresh = new TempDir();
        var reference = await G3MProjectService.OpenAsync(G3MProjectService.Export(workspace.Data, fresh.File("p"), null, new G3MProjectOptions { Name = link.Record.Name }, cancellationToken: TestContext.Current.CancellationToken).Path, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(reference.Resources.Count, after.Resources.Count);
        Assert.All(reference.Resources, r => Assert.Equal(r.Hash, after.Find(r.Path)?.Hash));

        workspace.Undo();
        await link.SaveToProjectAsync(TestContext.Current.CancellationToken);
        var undone = await OpenProject(link);
        Assert.Equal(before.Resources.Count, undone.Resources.Count);
        Assert.All(before.Resources, r => Assert.Equal(r.Hash, undone.Find(r.Path)?.Hash));
    }

    [Fact]
    public async Task SyncWritesTheDataFileAndTakesAContentAddressedSnapshotEachTime()
    {
        using var dir = new TempDir();
        var session = new Session(dir);
        var path = TestData.WriteSample(dir, objects: 3, withCode: true);
        var (workspace, link) = await session.Enabled(path);

        workspace.Edit(Code("return 2;"));
        Assert.Equal(1, link.NotSynced);
        var first = await link.SyncAsync(session.Pipeline, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(first.Saved);
        Assert.Equal(ProjectState.Synced, link.State);
        var store = link.Snapshots;
        var snapshots = store.List();
        Assert.Single(snapshots);
        var project = await OpenProject(link);
        Assert.Equal(workspace.Identity.Md5, project.BaseFile.Md5);
        Assert.Equal(workspace.Identity, link.Record.Synced);
        var blobs = Directory.GetDirectories(Path.Combine(dir.File("user"), "projects", link.Record.Id, "objects")).Length;
        Assert.Equal(project.Resources.Select(r => r.Hash).Distinct().Count(), blobs);

        workspace.Edit(Code("return 3;"));
        await link.SyncAsync(session.Pipeline, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(2, store.List().Count);
        var blobsAfter = Directory.GetDirectories(Path.Combine(dir.File("user"), "projects", link.Record.Id, "objects")).Length;
        Assert.InRange(blobsAfter - blobs, 1, 4);

        var changed = SnapshotStore.Compare(store.List()[0], await OpenProject(link));
        Assert.Contains("code/gml_Script_probe", changed);
        Assert.DoesNotContain("rooms/room0", changed);
        store.Restore(store.List()[0], "code/gml_Script_probe", link.Folder);
        Assert.Contains("return 2", Gml(link), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AReopenedDataFileFindsItsProjectByPathAndByIdentityAndSeesExternalChanges()
    {
        using var dir = new TempDir();
        var session = new Session(dir);
        var path = TestData.WriteSample(dir, objects: 3, withCode: true);
        var (workspace, link) = await session.Enabled(path);
        workspace.Edit(Code("return 2;"));
        await link.SyncAsync(session.Pipeline, cancellationToken: TestContext.Current.CancellationToken);
        workspace.Edit(Code("return 3;"));
        session.Service.Close();

        var again = await session.Open(path);
        Assert.NotNull(again.Project);
        Assert.False(again.Project.DataFileChanged);
        Assert.Equal(1, again.RecoveredEdits);
        Assert.True(again.Project.HasPendingWrite);
        Assert.Equal(link.Folder, again.Project.Folder);

        // A copy of the synced file at another path is the same game: the project follows it.
        session.Service.Close();
        var copy = dir.File("copy.win");
        File.Copy(path, copy);
        var other = await session.Open(copy);
        Assert.Equal(link.Folder, other.Project?.Folder);
        Assert.Equal(Path.GetFullPath(copy), other.Project!.Record.Synced.Path);

        // Patched by another tool: the file no longer matches the last sync.
        session.Service.Close();
        using (var patched = TestData.Create(5, withCode: true)) TestData.Write(copy, patched);
        var changed = await session.Open(copy);
        Assert.NotNull(changed.Project);
        Assert.True(changed.Project.DataFileChanged);
        Assert.Equal(ProjectState.DataFileChanged, changed.Project.State);
    }

    [Fact]
    public async Task AutosaveAndAutosyncAreSeparateSettingsWithTheirOwnTriggers()
    {
        using var dir = new TempDir();
        var session = new Session(dir);
        var (workspace, link) = await session.Enabled(TestData.WriteSample(dir, objects: 2, withCode: true));
        var now = DateTimeOffset.Now;

        workspace.Edit(Code("return 2;"));
        await link.TickAsync(now.AddSeconds(1), session.Pipeline);
        Assert.True(link.HasPendingWrite);
        await link.TickAsync(now.AddSeconds(3), session.Pipeline);
        Assert.False(link.HasPendingWrite);
        Assert.Equal(1, link.NotSynced);

        // Default autosync is On Save: Save writes the project and the data file.
        workspace.Edit(Code("return 3;"));
        Assert.True((await link.SaveAsync(session.Pipeline, cancellationToken: TestContext.Current.CancellationToken))!.Saved);
        Assert.Equal(0, link.NotSynced);

        link.UpdateSettings(new ProjectSettings(Trigger.OnSave, 2, Trigger.Off, 30));
        workspace.Edit(Code("return 4;"));
        Assert.Null(await link.SaveAsync(session.Pipeline, cancellationToken: TestContext.Current.CancellationToken));
        Assert.False(link.HasPendingWrite);
        Assert.Equal(1, link.NotSynced);
        Assert.Equal(ProjectState.NotSynced, link.State);
        await link.TickAsync(now.AddMinutes(10), session.Pipeline);
        Assert.Equal(1, link.NotSynced);

        link.UpdateSettings(new ProjectSettings(Trigger.Off, 2, Trigger.OnFocusLoss, 30));
        workspace.Edit(Code("return 5;"));
        await link.TickAsync(now.AddMinutes(10), session.Pipeline);
        Assert.True(link.HasPendingWrite);
        await link.FocusLostAsync(session.Pipeline);
        Assert.Equal(0, link.NotSynced);
        Assert.False(link.HasPendingWrite);

        link.UpdateSettings(new ProjectSettings(Trigger.Off, 2, Trigger.AfterDelay, 5));
        workspace.Edit(Code("return 6;"));
        await link.TickAsync(DateTimeOffset.Now.AddSeconds(2), session.Pipeline);
        Assert.Equal(1, link.NotSynced);
        await link.TickAsync(DateTimeOffset.Now.AddSeconds(8), session.Pipeline);
        Assert.Equal(0, link.NotSynced);
        Assert.Equal(workspace.Identity, link.Record.Synced);
        Assert.Equal(link.Record, session.Service.Projects.Registry.Get(link.Record.Id));
    }

    [Theory]
    [InlineData(RelocateMode.Move)]
    [InlineData(RelocateMode.Copy)]
    [InlineData(RelocateMode.Recreate)]
    public async Task ChangingTheDataFileLocationSyncsFirstThenMovesCopiesOrRecreates(RelocateMode mode)
    {
        using var dir = new TempDir();
        var session = new Session(dir);
        var path = TestData.WriteSample(dir, objects: 3, withCode: true);
        var (workspace, link) = await session.Enabled(path);
        workspace.Edit(Code("return 9;"));
        var target = dir.File(Path.Combine("elsewhere", "game.win"));

        await link.RelocateAsync(target, mode, session.Pipeline, TestContext.Current.CancellationToken);

        Assert.Equal(Path.GetFullPath(target), workspace.Path);
        Assert.Equal(mode != RelocateMode.Move, File.Exists(path));
        Assert.Equal(0, link.NotSynced);
        Assert.Equal(Path.GetFullPath(target), link.Record.Synced.Path);
        using (var moved = (await GameMakerIO.ReadAsync(File.OpenRead(target), cancellationToken: TestContext.Current.CancellationToken))) Assert.Contains("return 9", CodeText.Decompile(moved, "gml_Script_probe"), StringComparison.Ordinal);
        if (mode == RelocateMode.Recreate) Assert.Contains("return 9", (await File.ReadAllTextAsync(Path.Combine(link.Folder, "code", "gml_Script_probe", "gml_Script_probe.gml"), TestContext.Current.CancellationToken)), StringComparison.Ordinal);

        // Nothing relies on the old location: edits journal under the new path and the project is found there.
        workspace.Edit(Code("return 10;"));
        session.Service.Close();
        var again = await session.Open(target);
        Assert.Equal(1, again.RecoveredEdits);
        Assert.Equal(link.Folder, again.Project?.Folder);
        await Assert.ThrowsAsync<IOException>(() => again.Project!.RelocateAsync(target, RelocateMode.Copy, session.Pipeline, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DisableKeepsTheProjectArchivedOrDeletesItAndTheOfferComesOnce()
    {
        using var dir = new TempDir();
        var session = new Session(dir);
        var path = TestData.WriteSample(dir, withCode: true);
        var workspace = await session.Open(path);
        Assert.True(session.Service.Projects.ShouldOffer(workspace));
        Assert.False(session.Service.Projects.ShouldOffer(workspace));
        var link = await session.Service.Projects.EnableAsync(workspace, session.Root, cancellationToken: TestContext.Current.CancellationToken);
        Assert.False(session.Service.Projects.ShouldOffer(workspace));

        session.Service.Projects.Disable(workspace, deleteProject: false);
        Assert.Null(workspace.Project);
        Assert.True(Directory.Exists(link.Folder));
        Assert.True(session.Service.Projects.Registry.Get(link.Record.Id)!.Archived);
        Assert.Null(session.Service.Projects.Registry.Find(workspace.Identity));
        session.Service.Close();
        Assert.Null((await session.Open(path)).Project);

        var second = await session.Service.Projects.EnableAsync(session.Service.Current!, session.Root, cancellationToken: TestContext.Current.CancellationToken);
        session.Service.Projects.Disable(session.Service.Current!, deleteProject: true);
        Assert.False(Directory.Exists(second.Folder));
        Assert.Null(session.Service.Projects.Registry.Get(second.Record.Id));
        Assert.True(File.Exists(path));
    }

    [Fact]
    public async Task ClosingAProjectWorkspaceSavesToTheProjectFirstAndPromptsWithSync()
    {
        using var dir = new TempDir();
        var session = new Session(dir);
        var (workspace, link) = await session.Enabled(TestData.WriteSample(dir, withCode: true));
        workspace.Edit(Code("return 8;"));
        ClosePrompt? asked = null;

        Assert.False(await CloseGuard.ConfirmAsync(workspace, p => { asked = p; return Task.FromResult(CloseChoice.Cancel); }, _ => Task.FromResult(true)));
        Assert.True(asked!.ProjectMode);
        Assert.False(link.HasPendingWrite);
        Assert.Contains("return 8", Gml(link), StringComparison.Ordinal);
        Assert.True(await CloseGuard.ConfirmAsync(workspace, _ => Task.FromResult(CloseChoice.Save), async w => (await w.Project!.SyncAsync(session.Pipeline)).Saved));
        Assert.Equal(0, link.NotSynced);
    }

    [Fact]
    public async Task TheRegistrySurvivesARestartAndADamagedFileReadsAsEmpty()
    {
        using var dir = new TempDir();
        var session = new Session(dir);
        var (_, link) = await session.Enabled(TestData.WriteSample(dir, withCode: true));
        var loaded = ProjectRegistry.Load(session.UserData);
        Assert.Equal(link.Record, loaded.Get(link.Record.Id));
        Assert.Equal(session.Root, loaded.RememberedRoot);

        Directory.Delete(session.Root, true);
        Assert.True(ProjectRegistry.Load(session.UserData).NeedsLocationQuestion);
        await File.WriteAllTextAsync(session.UserData.Combine("projects.json"), "{ broken", TestContext.Current.CancellationToken);
        Assert.Empty(ProjectRegistry.Load(session.UserData).Projects);
    }
}
