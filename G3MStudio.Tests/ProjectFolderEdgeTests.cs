using G3MLib.DataFile;
using G3MLib.DataFile.Project;
using G3MStudio.Core.Projects;
using G3MStudio.Core.Workspace;
using ImageMagick;

#pragma warning disable CA2000 // the data model owns what is added to it

namespace G3MStudio.Tests;

/// <summary>Edge cases of Resolve Outside Changes on the project folder: no-op edits, renames, emptied or broken folders, odd names.</summary>
public sealed class ProjectFolderEdgeTests
{
    private static byte[] Png(int shade)
    {
        using var image = new MagickImage(new MagickColor((byte)(shade * 40), 0, 0, 255), 32, 32);
        image.Format = MagickFormat.Png;
        return image.ToByteArray();
    }

    private static GameMakerData SpriteData(int frames, string name)
    {
        var data = TestData.Create(0);
        TestData.AddPage(data);
        SpriteTemplate.Create(data, name);
        if (frames > 0) new FramesImportCommand(name, [.. Enumerable.Range(1, frames).Select(Png)], true).Apply(data);
        return data;
    }

    private static async Task<(ProjectMergeTests.Session Session, Workspace Workspace, ProjectLink Link, string Path)> Start(TempDir dir)
    {
        string path;
        using (var data = SpriteData(0, "spr_a")) path = TestData.Write(dir.File("data.win"), data);
        var session = new ProjectMergeTests.Session(dir);
        var workspace = await session.Open(path);
        var link = await session.Service.Projects.EnableAsync(workspace, dir.File("projects"), cancellationToken: TestContext.Current.CancellationToken);
        return (session, workspace, link, path);
    }

    private static string Sprite(ProjectLink link, string name = "spr_a") => Path.Combine(link.Folder, "sprites", name);

    private static string Other(TempDir dir, ProjectLink link, int frames, string name)
    {
        var other = dir.File("other-" + name);
        using (var data = SpriteData(frames, name)) G3MProjectService.Export(data, other, null, new G3MProjectOptions { Name = link.Record.Name }, null, TestContext.Current.CancellationToken);
        return Directory.GetDirectories(other, name, SearchOption.AllDirectories).Single();
    }

    private static void Copy(string from, string to)
    {
        foreach (var file in Directory.GetFiles(from, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(to, Path.GetRelativePath(from, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, true);
        }
    }

    [Fact]
    public async Task ALineEndingOnlyEditOfAJsonFileIsNotAChange()
    {
        using var dir = new TempDir();
        var (session, _, link, _) = await Start(dir);
        var json = Directory.GetFiles(Sprite(link), "*.json").Single();
        var text = (await File.ReadAllTextAsync(json, TestContext.Current.CancellationToken)).Replace("\r\n", "\n", StringComparison.Ordinal);
        await File.WriteAllTextAsync(json, text.Replace("\n", "\r\n", StringComparison.Ordinal), TestContext.Current.CancellationToken);

        Assert.Null(await link.BringInFolderAsync(session.Pipeline, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ATimestampOnlyChangeIsNotAChange()
    {
        using var dir = new TempDir();
        var (session, _, link, _) = await Start(dir);
        foreach (var file in Directory.GetFiles(link.Folder, "*", SearchOption.AllDirectories)) File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddDays(-3));

        Assert.Null(await link.BringInFolderAsync(session.Pipeline, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ARenamedResourceFolderIsNotSilentAndTheDataFileStaysValid()
    {
        using var dir = new TempDir();
        var (session, _, link, path) = await Start(dir);
        Directory.Move(Sprite(link), Sprite(link, "spr_moved"));

        var result = await link.BringInFolderAsync(session.Pipeline, TestContext.Current.CancellationToken);

        Assert.NotNull(result); // the library keeps the old resource: it cannot tell a rename from a delete
        Assert.NotEmpty((await session.Open(path)).Data.Sprites);
    }

    [Fact]
    public async Task AFileDeletedInsideAResourceFolderIsAChangeAndTheDataFileStaysValid()
    {
        using var dir = new TempDir();
        var (session, workspace, link, path) = await Start(dir);
        workspace.Edit(new FramesImportCommand("spr_a", [Png(1), Png(2)], true));
        Assert.True((await link.SyncAsync(session.Pipeline, cancellationToken: TestContext.Current.CancellationToken)).Saved);
        File.Delete(Directory.GetFiles(Sprite(link), "*.png").Order(StringComparer.Ordinal).Last());

        var result = await link.BringInFolderAsync(session.Pipeline, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.NotNull(await session.Open(path));
    }

    [Fact]
    public async Task AnEmptiedProjectFolderNeverDestroysTheDataFile()
    {
        using var dir = new TempDir();
        var (session, _, link, path) = await Start(dir);
        foreach (var entry in Directory.GetFileSystemEntries(link.Folder).Where(e => !e.EndsWith(".g3mproj", StringComparison.Ordinal)))
            if (Directory.Exists(entry)) Directory.Delete(entry, true); else File.Delete(entry);

        try { await link.BringInFolderAsync(session.Pipeline, TestContext.Current.CancellationToken); }
        catch (Exception e) when (e is IOException or InvalidOperationException or ProjectException) { /* a refusal is fine */ }

        var again = await session.Open(path);
        Assert.Single(again.Data.Sprites);
    }

    [Fact]
    public async Task AMissingProjectFileIsAnErrorAndLeavesTheDataFileAlone()
    {
        using var dir = new TempDir();
        var (session, _, link, path) = await Start(dir);
        var before = await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken);
        File.Delete(link.ProjectFile);

        await Assert.ThrowsAnyAsync<Exception>(() => link.BringInFolderAsync(session.Pipeline, TestContext.Current.CancellationToken));

        Assert.Equal(before, await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AFileHeldOpenByACopyFailsTheResolveAndLeavesTheDataFileAlone()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Only Windows locks a file against reading; elsewhere a half copy is read as it is.");
        using var dir = new TempDir();
        var (session, _, link, path) = await Start(dir);
        var before = await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken);
        Copy(Other(dir, link, 3, "spr_a"), Sprite(link));
        using var held = new FileStream(Directory.GetFiles(Sprite(link), "*.png").First(), FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        await Assert.ThrowsAsync<IOException>(() => link.BringInFolderAsync(session.Pipeline, TestContext.Current.CancellationToken));

        Assert.Equal(before, await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ADeletedResourceFolderIsListedAsMissingAndStaysInTheDataFile()
    {
        using var dir = new TempDir();
        var (session, _, link, path) = await Start(dir);
        Directory.Delete(Sprite(link), true);

        await link.BringInFolderAsync(session.Pipeline, TestContext.Current.CancellationToken);

        Assert.Contains("spr_a", link.MissingFromFolder);
        Assert.Single((await session.Open(path)).Data.Sprites);
    }

    [Fact]
    public async Task AFolderMovedToAnotherTypeFolderIsReportedMissingAndTheDataFileStaysValid()
    {
        using var dir = new TempDir();
        var (session, _, link, path) = await Start(dir);
        Directory.Move(Sprite(link), Path.Combine(link.Folder, "rooms", "spr_a"));

        var result = await link.BringInFolderAsync(session.Pipeline, TestContext.Current.CancellationToken);

        Assert.Contains("spr_a", link.MissingFromFolder);
        Assert.NotNull(result);
        Assert.NotNull(await session.Open(path));
    }

    [Fact]
    public async Task ADeletedProjectFolderIsAnErrorAndLeavesTheDataFileAlone()
    {
        using var dir = new TempDir();
        var (session, _, link, path) = await Start(dir);
        var before = await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken);
        Directory.Delete(link.Folder, true);

        await Assert.ThrowsAnyAsync<Exception>(() => link.BringInFolderAsync(session.Pipeline, TestContext.Current.CancellationToken));

        Assert.Equal(before, await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ABrokenProjectFileIsAnErrorAndLeavesTheDataFileAlone()
    {
        using var dir = new TempDir();
        var (session, _, link, path) = await Start(dir);
        var before = await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(link.ProjectFile, "{ not json", TestContext.Current.CancellationToken);

        await Assert.ThrowsAnyAsync<Exception>(() => link.BringInFolderAsync(session.Pipeline, TestContext.Current.CancellationToken));

        Assert.Equal(before, await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ACaseOnlyRenameLeavesTheDataFileValidOnAnyFileSystem()
    {
        using var dir = new TempDir();
        var (session, _, link, path) = await Start(dir);
        Directory.Move(Sprite(link), Sprite(link, "SPR_A"));

        await link.BringInFolderAsync(session.Pipeline, TestContext.Current.CancellationToken);

        // Case-insensitive disk: the same folder under a new case. Case-sensitive: a new unlisted folder and a missing one. Neither may break the file.
        Assert.NotEmpty((await session.Open(path)).Data.Sprites);
    }

    [Fact]
    public async Task ALongUnicodeFolderNameIsTaken()
    {
        using var dir = new TempDir();
        var (session, _, link, path) = await Start(dir);
        var name = "spr_" + new string('ü', 80);
        var target = Sprite(link, name);
        Copy(Other(dir, link, 2, "spr_b"), target);
        File.Move(Path.Combine(target, "spr_b.json"), Path.Combine(target, name + ".json"));

        Assert.NotNull(await link.BringInFolderAsync(session.Pipeline, TestContext.Current.CancellationToken));
        Assert.NotNull(await session.Open(path));
    }

    [Fact]
    public async Task ANewFolderWithSpacesAndUnicodeInItsNameIsTaken()
    {
        using var dir = new TempDir();
        var (session, _, link, path) = await Start(dir);
        var name = "spr_é c";
        var target = Sprite(link, name);
        Copy(Other(dir, link, 2, "spr_b"), target);
        File.Move(Path.Combine(target, "spr_b.json"), Path.Combine(target, name + ".json"));

        var result = await link.BringInFolderAsync(session.Pipeline, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.NotNull(await session.Open(path));
    }
}

/// <summary>Data file changes while a plain file is open: replaced by other data, moved away or deleted.</summary>
public sealed class DataFileChangedWhileOpenTests
{
    private static async Task<(Workspace Workspace, SavePipeline Pipeline, string Path)> Open(TempDir dir)
    {
        var userData = new UserData(dir.File("user"));
        var path = TestData.WriteSample(dir, objects: 1);
        var workspace = await new WorkspaceService(userData, RecentFiles.Load(userData)).OpenAsync(path, cancellationToken: TestContext.Current.CancellationToken);
        workspace.History.Execute(new RenameResourceCommand("objects", "obj_0", "obj_a"));
        return (workspace, new SavePipeline(userData), path);
    }

    [Fact]
    public async Task AFileReplacedByAnotherGamesDataIsNotOverwrittenBySave()
    {
        using var dir = new TempDir();
        var (workspace, pipeline, path) = await Open(dir);
        using (var other = TestData.Create(5)) TestData.Write(path, other);
        var theirs = await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken);

        var refused = await workspace.SaveAsync(pipeline, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(SaveFailure.ChangedOnDisk, refused.Failure);
        Assert.Equal(theirs, await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
        workspace.Dispose();
    }

    [Fact]
    public async Task AFileMovedAwayIsWrittenAgainFromMemoryAndTheMovedCopyIsKept()
    {
        using var dir = new TempDir();
        var (workspace, pipeline, path) = await Open(dir);
        File.Move(path, dir.File("moved.win"));

        Assert.True((await workspace.SaveAsync(pipeline, cancellationToken: TestContext.Current.CancellationToken)).Saved);

        Assert.True(File.Exists(path));
        Assert.True(File.Exists(dir.File("moved.win")));
        workspace.Dispose();
    }
}
