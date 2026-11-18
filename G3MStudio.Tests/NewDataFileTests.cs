namespace G3MStudio.Tests;

public sealed class NewDataFileTests
{
    [Fact]
    public async Task ANeighbourNamedLikeTheOldTempFileIsLeftAlone()
    {
        using var dir = new TempDir();
        var path = dir.File("fresh.win");
        var neighbour = path + ".new";
        await File.WriteAllTextAsync(neighbour, "mine", TestContext.Current.CancellationToken);
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                window.FilePicker = (_, _) => Task.FromResult<string?>(path);
                await window.NewDataFileAsync();
                Assert.True(File.Exists(path));
                Assert.Equal("mine", await File.ReadAllTextAsync(neighbour, TestContext.Current.CancellationToken));
                Assert.Equal([path, neighbour], [.. Directory.GetFiles(dir.Path).Order()]);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public async Task NewDataFileWritesAnEmptyGameAndOpensIt()
    {
        using var dir = new TempDir();
        var path = dir.File("fresh.win");
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                window.FilePicker = (_, _) => Task.FromResult<string?>(path);
                await window.NewDataFileAsync();
                Assert.True(File.Exists(path));
                var workspace = window.Workspaces.Current!;
                Assert.Equal(path, workspace.Path);
                Assert.Empty(workspace.Data.GameObjects);
                Assert.False(workspace.History.IsDirty);
                using var back = await G3MLib.DataFile.GameMakerIO.ReadAsync(path, cancellationToken: TestContext.Current.CancellationToken);
                Assert.Empty(back.Sprites);
            }
            finally { window.Close(); }
        });
    }
}
