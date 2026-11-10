using G3MLib.DataFile;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

public sealed class BackgroundEditTests
{
    private sealed class Probe(Action? apply = null, Action? revert = null) : EditCommand
    {
        public override string Kind => "probe";

        public override string Editor => "probe";

        public override IReadOnlyDictionary<string, string> Args { get; } = new Dictionary<string, string>();

        public override void Apply(GameMakerData data) => apply?.Invoke();

        public override void Revert(GameMakerData data) => revert?.Invoke();
    }

    private static async Task<Workspace> Open(TempDir dir)
    {
        var userData = new UserData(dir.File("user"));
        return await new WorkspaceService(userData, RecentFiles.Load(userData)).OpenAsync(TestData.WriteSample(dir, objects: 1), cancellationToken: TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task AnEditMadeWhileABackgroundEditRunsIsAppliedWhenItEnds()
    {
        using var dir = new TempDir();
        using var workspace = await Open(dir);
        using var gate = new ManualResetEventSlim();
        var running = workspace.EditInBackground(new Probe(apply: () => gate.Wait(TimeSpan.FromSeconds(10))));
        Assert.False(workspace.Edit(ResourceTemplates.New(workspace.Data, "paths", "pth_during")));
        gate.Set();
        await running.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.Contains(workspace.Data.Paths, p => p.Name.Content == "pth_during");
    }

    [Fact]
    public async Task ProbingReportsAnyExceptionAsAFailureAndStaysUsable()
    {
        using var dir = new TempDir();
        using var workspace = await Open(dir);
        var crash = new Probe(apply: () => throw new FormatException("boom"));
        var stuck = new Probe(revert: () => throw new ArgumentOutOfRangeException("no way back"));
        var fine = new Probe();
        var failures = await workspace.ProbeInBackground([crash, fine, stuck]);
        Assert.Equal([crash, stuck], failures.Select(f => f.Command));
        Assert.False(workspace.History.Frozen);
        Assert.True(workspace.Edit(ResourceTemplates.New(workspace.Data, "paths", "pth_after")));
    }
}
