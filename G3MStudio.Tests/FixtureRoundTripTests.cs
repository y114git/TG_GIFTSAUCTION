using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

public sealed class FixtureRoundTripTests
{
    private static readonly string FixtureDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../../../../G3MLib/G3MLib.Tests/Fixtures"));

    public static TheoryData<string> Fixtures => ["data-2022.win", "data-2023.win", "data-2024.win", "data-2026.win"];

    [Theory]
    [MemberData(nameof(Fixtures))]
    public async Task OpenEditUndoRedoSaveAndReopen(string name)
    {
        var source = Path.Combine(FixtureDir, name);
        Assert.SkipUnless(File.Exists(source), "G3MLib fixtures are not next to this repository");
        using var dir = new TempDir();
        var path = dir.File(name);
        File.Copy(source, path);
        var token = TestContext.Current.CancellationToken;
        var userData = new UserData(dir.File("user"));
        var service = new WorkspaceService(userData, RecentFiles.Load(userData));

        var workspace = await service.OpenAsync(path, null, token);
        var script = workspace.Data.Scripts.FirstOrDefault() ?? throw new InvalidOperationException("fixture has no script");
        var old = script.Name.Content!;
        Assert.True(workspace.Edit(new RenameResourceCommand("scripts", old, old + "_x")));
        Assert.True(workspace.IsDirty);
        workspace.Undo();
        Assert.Equal(old, workspace.Data.Scripts[0].Name.Content);
        Assert.False(workspace.IsDirty);
        workspace.Redo();
        Assert.Equal(old + "_x", workspace.Data.Scripts[0].Name.Content);

        var result = await workspace.SaveAsync(new SavePipeline(userData), null, token);
        Assert.True(result.Saved);
        Assert.False(workspace.IsDirty);

        var again = await service.OpenAsync(path, null, token);
        Assert.Equal(old + "_x", again.Data.Scripts[0].Name.Content);
        Assert.Equal(0, again.RecoveredEdits);
        service.Close();
    }
}
