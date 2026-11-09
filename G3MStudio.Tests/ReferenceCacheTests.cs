using G3MLib.DataFile.References;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

public sealed class ReferenceCacheTests
{
    private static async Task<Workspace> Open(TempDir dir)
    {
        var path = TestData.WriteSample(dir, objects: 6, withCode: true);
        var userData = new UserData(dir.File("user"));
        return await new WorkspaceService(userData, RecentFiles.Load(userData)).OpenAsync(path, null, TestContext.Current.CancellationToken);
    }

    private static ReferenceIndex Index(Workspace workspace) => ReferenceCache.Of(workspace.Data).Get(workspace.Data, TestContext.Current.CancellationToken);

    private static void AssertSameAsFresh(Workspace workspace)
    {
        var cached = Index(workspace);
        var fresh = ReferenceIndex.Build(workspace.Data, TestContext.Current.CancellationToken);
        Assert.Equal(fresh.ReferenceCount, cached.ReferenceCount);
        foreach (var item in workspace.Data.GameObjects) Assert.Equal(fresh.FindUsages(item).Count, cached.FindUsages(item).Count);
        foreach (var item in workspace.Data.Strings.Take(200)) Assert.Equal(fresh.FindUsages(item).Count, cached.FindUsages(item).Count);
    }

    [Fact]
    public async Task ARenameKeepsTheIndexAndItStillMatchesAFreshOne()
    {
        using var dir = new TempDir();
        using var workspace = await Open(dir);
        var first = Index(workspace);
        workspace.Edit(new RenameResourceCommand("objects", "obj_1", "renamed_1"));
        Assert.True(ReferenceCache.Of(workspace.Data).IsBuilt);
        Assert.Same(first, Index(workspace));
        AssertSameAsFresh(workspace);
        workspace.Undo();
        Assert.Same(first, Index(workspace));
        AssertSameAsFresh(workspace);
        workspace.Redo();
        AssertSameAsFresh(workspace);
    }

    [Fact]
    public async Task AnyOtherEditDropsTheIndex()
    {
        using var dir = new TempDir();
        using var workspace = await Open(dir);
        var first = Index(workspace);
        workspace.Edit(new SetPropertyCommand("objects", "obj_1", "Depth", "5"));
        Assert.False(ReferenceCache.Of(workspace.Data).IsBuilt);
        Assert.NotSame(first, Index(workspace));
        workspace.Undo();
        Assert.False(ReferenceCache.Of(workspace.Data).IsBuilt);
    }
}
