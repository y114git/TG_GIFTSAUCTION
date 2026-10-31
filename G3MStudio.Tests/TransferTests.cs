using G3MLib.Modding.Resources;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

public sealed class TransferTests(ITestOutputHelper output)
{
    private static readonly string[] ExpectedFolders = ["CodeEntries", "GameObjects", "Scripts"];

    private static async Task<Workspace> Open(TempDir dir, string file, int objects, bool withCode = true)
    {
        var path = TestData.WriteSample(dir, file, objects, withCode);
        var userData = new UserData(dir.File("user"));
        return await new WorkspaceService(userData, RecentFiles.Load(userData)).OpenAsync(path);
    }

    [Fact]
    public async Task ExportWritesTheNamedEntriesAndAnythingCancelledLeavesNothing()
    {
        using var dir = new TempDir();
        using var workspace = await Open(dir, "a.win", 3);
        var ct = TestContext.Current.CancellationToken;
        var folder = dir.File("out");

        var results = await ResourceTransfer.ExportAsync(workspace, folder, "objects", new HashSet<string> { "obj_1" }, null, ct);
        Assert.All(results, r => Assert.NotEqual(ResultKind.Failed, r.Kind));
        var written = Assert.Single(Directory.GetDirectories(Path.Combine(folder, "GameObjects")));
        Assert.StartsWith("obj_1", Path.GetFileName(written), StringComparison.Ordinal);

        var everything = dir.File("all");
        var all = await ResourceTransfer.ExportAllAsync(workspace, everything, null, ct);
        Assert.DoesNotContain(all, r => r.Kind == ResultKind.Failed);
        var found = ResourceTransfer.FoldersIn(everything);
        Assert.All(ExpectedFolders, type => Assert.Contains(type, found));

        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        var empty = dir.File("cancelled");
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ResourceTransfer.ExportAsync(workspace, empty, "objects", null, null, cancelled.Token));
        Assert.False(Directory.Exists(Path.Combine(empty, "GameObjects")));
        Assert.False(ResourceTransfer.Supports("strings"));
        await Assert.ThrowsAsync<ArgumentException>(() => ResourceTransfer.ExportAsync(workspace, empty, "strings", null, null, ct));
    }

    [Fact]
    public async Task ImportIsABulkChangeThatCannotBeUndoneAndNeedsASave()
    {
        using var dir = new TempDir();
        using var source = await Open(dir, "source.win", 3);
        var ct = TestContext.Current.CancellationToken;
        var folder = dir.File("export");
        await ResourceTransfer.ExportAllAsync(source, folder, null, ct);

        using var target = await Open(dir, "target.win", 1);
        target.Edit(new EditCodeCommand("gml_Script_probe", "return 9;"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => target.ApplyBulkAsync(() => { })); // unsaved edits first

        var save = await target.SaveAsync(new SavePipeline(new UserData(dir.File("user"))), cancellationToken: ct);
        Assert.True(save.Saved);
        var applied = 0;
        target.BulkApplied += () => applied++;
        IReadOnlyList<ItemResult> results = [];
        await target.ApplyBulkAsync(() => results = ResourceTransfer.Import(target, folder, ["GameObjects"], null, ct));

        Assert.Equal(1, applied);
        Assert.DoesNotContain(results, r => r.Kind == ResultKind.Failed);
        Assert.Equal(["obj_0", "obj_1", "obj_2"], target.Data.GameObjects.Select(o => o.Name.Content).Order());
        Assert.True(target.History.IsDirty);
        Assert.False(target.History.CanUndo);
        Assert.Equal(1, target.History.UnsavedCount);
        Assert.True(target.JournalSuspended);

        Assert.True((await target.SaveAsync(new SavePipeline(new UserData(dir.File("user"))), cancellationToken: ct)).Saved);
        Assert.False(target.History.IsDirty);
        Assert.False(target.JournalSuspended);
    }

    [Fact]
    public async Task ACancelledImportStillCountsAsAChange()
    {
        using var dir = new TempDir();
        using var source = await Open(dir, "source.win", 2);
        var folder = dir.File("export");
        await ResourceTransfer.ExportAllAsync(source, folder, null, TestContext.Current.CancellationToken);
        using var target = await Open(dir, "target.win", 0);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => target.ApplyBulkAsync(() => ResourceTransfer.Import(target, folder, ["GameObjects"], null, cancelled.Token)));
        Assert.True(target.History.IsDirty); // the model may hold part of the import, so only Revert to Saved is safe
    }

    [Fact]
    public async Task TheWindowExportsImportsAndRevertsThroughItsCommands()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir, "game.win", 2, true);
        var export = dir.File("export");
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                await window.OpenAsync(path);
                window.FolderPicker = _ => Task.FromResult<string?>(export);
                window.Confirmer = (_, _) => Task.FromResult(true);

                window.Explorer.Select("objects:obj_1");
                await window.ExportSelectionAsync();
                Assert.StartsWith("obj_1", Path.GetFileName(Assert.Single(Directory.GetDirectories(Path.Combine(export, "GameObjects")))), StringComparison.Ordinal);
                Assert.Contains(window.Notifications.Active, n => n.Text.Contains("written", StringComparison.Ordinal));

                // Export everything, change the model, then import the files over it: the object comes back.
                await window.ExportAllAsync();
                var workspace = window.Workspaces.Current!;
                workspace.Edit(new RenameResourceCommand("objects", "obj_0", "obj_renamed"));
                await window.ImportAsync();
                Assert.Contains(window.Notifications.Active, n => n.Text.Contains("save", StringComparison.OrdinalIgnoreCase));
                Assert.Equal("obj_renamed", workspace.Data.GameObjects[0].Name.Content); // unsaved edit: the import is refused

                window.Save();
                for (var i = 0; i < 100 && workspace.History.IsDirty; i++) await Task.Delay(50, TestContext.Current.CancellationToken);
                await window.ImportAsync();
                Assert.True(workspace.History.IsDirty);
                Assert.Equal(1, workspace.History.BulkChanges);

                await window.RevertAsync();
                Assert.NotSame(workspace, window.Workspaces.Current);
                Assert.False(window.Workspaces.Current!.History.IsDirty);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public async Task SpritesAndRoomsOfARealGameExportWithoutFailures()
    {
        var corpus = Environment.GetEnvironmentVariable("G3MSTUDIO_CORPUS_DIR");
        Assert.SkipWhen(string.IsNullOrEmpty(corpus) || !Directory.Exists(corpus), "G3MSTUDIO_CORPUS_DIR is not set");
        var file = Directory.EnumerateFiles(corpus, "data.win", SearchOption.AllDirectories).OrderBy(f => new FileInfo(f).Length).First();
        using var dir = new TempDir();
        var userData = new UserData(dir.File("user"));
        using var workspace = await new WorkspaceService(userData, RecentFiles.Load(userData)).OpenAsync(file, cancellationToken: TestContext.Current.CancellationToken);
        var watch = System.Diagnostics.Stopwatch.StartNew();
        foreach (var (type, count) in new[] { ("sprites", 40), ("rooms", 10), ("objects", 40), ("sounds", 10) })
        {
            var names = ResourceTypes.All.First(t => t.Key == type).Items(workspace.Data)!.Take(count).Select(ResourceTypes.NameOf).ToHashSet();
            var results = await ResourceTransfer.ExportAsync(workspace, dir.File("out"), type, names, null, TestContext.Current.CancellationToken);
            output.WriteLine($"{type}: {results.Count(r => r.Kind == ResultKind.Written)} written, {results.Count(r => r.Kind == ResultKind.Failed)} failed, {watch.Elapsed.TotalSeconds:F1} s");
            Assert.DoesNotContain(results, r => r.Kind == ResultKind.Failed);
        }
    }

    [Fact]
    public async Task RepackingTexturePagesKeepsEverySpriteReadable()
    {
        var corpus = Environment.GetEnvironmentVariable("G3MSTUDIO_CORPUS_DIR");
        Assert.SkipWhen(string.IsNullOrEmpty(corpus) || !Directory.Exists(corpus), "G3MSTUDIO_CORPUS_DIR is not set");
        var file = Directory.EnumerateFiles(corpus, "data.win", SearchOption.AllDirectories).OrderBy(f => new FileInfo(f).Length).First();
        using var dir = new TempDir();
        var copy = dir.File("data.win");
        File.Copy(file, copy);
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                await window.OpenAsync(copy);
                window.Confirmer = (_, _) => Task.FromResult(true);
                var workspace = window.Workspaces.Current!;
                var sprites = workspace.Data.Sprites.Count;
                var pagesBefore = workspace.Data.EmbeddedTextures.Count;
                await window.RepackTexturesAsync();
                Assert.Contains(window.Notifications.Active, n => n.Text.Contains("Repacked", StringComparison.Ordinal));
                Assert.True(workspace.History.IsDirty);
                Assert.Equal(sprites, workspace.Data.Sprites.Count);
                output.WriteLine($"pages {pagesBefore} -> {workspace.Data.EmbeddedTextures.Count}");

                using var stream = new MemoryStream();
                await G3MLib.DataFile.GameMakerIO.WriteAsync(stream, workspace.Data, cancellationToken: TestContext.Current.CancellationToken);
                stream.Position = 0;
                using var back = await G3MLib.DataFile.GameMakerIO.ReadAsync(stream, cancellationToken: TestContext.Current.CancellationToken);
                Assert.Equal(sprites, back.Sprites.Count);
                Assert.All(back.Sprites.Take(200).Where(s => s.Textures.Count > 0), sprite => Assert.NotNull(sprite.Textures[0].Texture?.TexturePage));
            }
            finally { window.Close(); }
        });
    }
}
