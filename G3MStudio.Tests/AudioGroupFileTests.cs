using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

public sealed class AudioGroupFileTests(ITestOutputHelper output)
{
    [Fact]
    public async Task AnAudioGroupFileOpensOnItsOwnAsAWorkspace()
    {
        var folder = Environment.GetEnvironmentVariable("G3MSTUDIO_CORPUS_DIR");
        Assert.SkipWhen(string.IsNullOrEmpty(folder) || !Directory.Exists(folder), "G3MSTUDIO_CORPUS_DIR is not set");
        var file = Directory.EnumerateFiles(folder, "audiogroup*.dat", SearchOption.AllDirectories).FirstOrDefault();
        Assert.SkipWhen(file is null, "no audio group file in the corpus");

        using var dir = new TempDir();
        var service = new WorkspaceService(new UserData(dir.File("user")), RecentFiles.Load(new UserData(dir.File("user"))));
        await service.OpenAsync(file, cancellationToken: TestContext.Current.CancellationToken);
        var workspace = Assert.IsType<Workspace>(service.Current);
        var counts = ResourceTypes.All.Select(t => t.Key + "=" + (t.Items(workspace.Data)?.Count() ?? -1)).ToList();
        output.WriteLine(string.Join(", ", counts));
        Assert.Contains(counts, c => c.StartsWith("audio", StringComparison.Ordinal));
    }
}
