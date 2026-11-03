using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

public sealed class RunGameTests
{
    [Fact]
    public async Task OutputLinesArriveAndTheExitCodeIsReported()
    {
        var lines = new List<(string Text, bool Error)>();
        using var run = GameRun.StartRaw("dotnet", ["--version"], (text, error) => { lock (lines) lines.Add((text, error)); });
        Assert.Equal(0, await run.Exited);
        lock (lines) Assert.Contains(lines, l => !l.Error && l.Text.Length > 0);
    }

    [Fact]
    public void TheRunnerIsTheGeneralInfoNameNextToTheDataFile()
    {
        using var dir = new TempDir();
        var data = dir.File("data.win");
        Assert.Null(GameRun.FindRunner(data, "Game.exe"));
        File.WriteAllText(dir.File("Game.exe"), string.Empty);
        Assert.Equal(dir.File("Game.exe"), GameRun.FindRunner(data, "Game.exe"));
    }

    [Fact]
    public async Task WithoutARunnerTheWindowSaysSoAndLeavesNoTemporaryFile()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir, "game.win", 1, true);
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                await window.OpenAsync(path);
                await window.RunGameAsync();
                Assert.Contains(window.Notifications.Active, n => n.Text.Contains("was not found", StringComparison.Ordinal));
                Assert.False(File.Exists(dir.File("g3mstudio_run.tmp")));
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public void ArgumentsSplitOnSpacesAndGroupInQuotes()
    {
        Assert.Equal(["-a", "two words", "b"], GameRun.SplitArguments("-a \"two words\"  b"));
        Assert.Empty(GameRun.SplitArguments("   "));
    }
}
