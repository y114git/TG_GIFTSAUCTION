using G3MLib.DataFile;
using G3MLib.DataFile.Compiler;
using G3MStudio.Core.Projects;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

public sealed class ProjectMergeTests
{
    internal sealed class Session(TempDir dir)
    {
        public UserData UserData { get; } = new(dir.File("user"));

        public WorkspaceService Service { get; private set; } = null!;

        public SavePipeline Pipeline => new(UserData);

        public async Task<Workspace> Open(string path)
        {
            Service?.Close();
            Service = new WorkspaceService(UserData, RecentFiles.Load(UserData));
            return await Service.OpenAsync(path);
        }
    }

    private static string Code(GameMakerData data) => CodeText.Decompile(data, "gml_Script_probe");

    /// <summary>Project with unsynced edits of ours, then the data file is patched by "another tool": obj_1 depth and the code.</summary>
    internal static async Task<(Session Session, string Path, ProjectLink Link)> Diverged(TempDir dir)
    {
        var session = new Session(dir);
        var path = TestData.WriteSample(dir, objects: 3, withCode: true);
        var workspace = await session.Open(path);
        var link = await session.Service.Projects.EnableAsync(workspace, dir.File("projects"));
        workspace.Edit(new RenameResourceCommand("objects", "obj_2", "obj_b"));
        workspace.Edit(new EditCodeCommand("gml_Script_probe", "return 2;"));
        await link.SaveToProjectAsync();
        session.Service.Close();

        using (var theirs = (await GameMakerIO.ReadAsync(new MemoryStream((await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken))), cancellationToken: TestContext.Current.CancellationToken)))
        {
            theirs.GameObjects.ByName("obj_1").Depth = 25;
            var group = new CodeImportGroup(theirs);
            group.QueueReplace("gml_Script_probe", "return 99;");
            group.Import();
            TestData.Write(path, theirs);
        }

        var again = await session.Open(path);
        Assert.True(again.Project!.DataFileChanged);
        return (session, path, again.Project);
    }

    [Theory]
    [InlineData(MergeSide.Ours, "return 2")]
    [InlineData(MergeSide.Theirs, "return 99")]
    public async Task BringingInChangesMergesPerResourceAndAsksOnlyWhereBothSidesChanged(MergeSide side, string expectedCode)
    {
        using var dir = new TempDir();
        var (session, path, link) = await Diverged(dir);

        var plan = await link.PlanMergeAsync(cancellationToken: TestContext.Current.CancellationToken);
        var conflict = Assert.Single(plan.Conflicts);
        Assert.Equal("code/gml_Script_probe", conflict.Path);
        Assert.True(plan.TheirsOnly >= 1);
        var result = await link.BringInAsync(plan, new Dictionary<string, MergeSide> { [conflict.Path] = side }, session.Pipeline, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Saved);
        Assert.False(link.DataFileChanged);
        Assert.NotNull(session.Service.Projects.NewestBackup(path));
        var merged = await session.Open(path);
        Assert.False(merged.Project!.DataFileChanged);
        Assert.Equal(0, merged.Project.NotSynced);
        Assert.Contains(expectedCode, Code(merged.Data), StringComparison.Ordinal);
        Assert.Equal(25, merged.Data.GameObjects.ByName("obj_1").Depth);
        Assert.NotNull(merged.Data.GameObjects.ByName("obj_b"));
        Assert.Null(merged.Data.GameObjects.ByName("obj_2"));
    }

    [Fact]
    public async Task OverwritingTheDataFileTakesTheProjectAndDropsTheOutsideChanges()
    {
        using var dir = new TempDir();
        var (session, path, link) = await Diverged(dir);

        Assert.True((await link.OverwriteDataFileAsync(session.Pipeline, cancellationToken: TestContext.Current.CancellationToken)).Saved);

        var written = await session.Open(path);
        Assert.False(written.Project!.DataFileChanged);
        Assert.Contains("return 2", Code(written.Data), StringComparison.Ordinal);
        Assert.NotEqual(25, written.Data.GameObjects.ByName("obj_1").Depth);
        Assert.NotNull(written.Data.GameObjects.ByName("obj_b"));
        Assert.Null(written.Data.GameObjects.ByName("obj_2"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ALostOrCorruptDataFileIsRebuiltFromTheProjectAndTheBackup(bool corrupt)
    {
        using var dir = new TempDir();
        var session = new Session(dir);
        var path = TestData.WriteSample(dir, objects: 2, withCode: true);
        var workspace = await session.Open(path);
        var link = await session.Service.Projects.EnableAsync(workspace, dir.File("projects"), cancellationToken: TestContext.Current.CancellationToken);
        workspace.Edit(new EditCodeCommand("gml_Script_probe", "return 5;"));
        Assert.True((await link.SyncAsync(session.Pipeline, cancellationToken: TestContext.Current.CancellationToken)).Saved);
        var record = link.Record;
        session.Service.Close();
        if (corrupt) await File.WriteAllTextAsync(path, "not a data file", TestContext.Current.CancellationToken); else File.Delete(path);
        await Assert.ThrowsAnyAsync<Exception>(() => session.Open(path));
        Assert.Equal(record.Id, session.Service.Projects.Registry.FindByPath(path)?.Id);

        await session.Service.Projects.RebuildAsync(record, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(corrupt, File.Exists(path + ".corrupt"));
        var rebuilt = await session.Open(path);
        Assert.Contains("return 5", Code(rebuilt.Data), StringComparison.Ordinal);
        Assert.NotNull(rebuilt.Project);
        Assert.False(rebuilt.Project.DataFileChanged);
    }

    [Fact]
    public async Task SyncRefusesToOverwriteAChangedDataFile()
    {
        using var dir = new TempDir();
        var (session, _, link) = await Diverged(dir);
        await Assert.ThrowsAsync<InvalidOperationException>(() => link.SyncAsync(session.Pipeline, cancellationToken: TestContext.Current.CancellationToken));
    }
}
