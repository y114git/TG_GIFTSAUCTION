using G3MStudio.Core.Settings;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

public sealed class LiveRunTests
{
    // A tiny child process stands in for the game: the shell runs a one-line script.
    private static RunSpec Child(string data, string windows, string unix, string environment = "") =>
        OperatingSystem.IsWindows()
            ? new RunSpec("cmd.exe", data, $"/s /c \"{windows}\"", Environment: environment)
            : new RunSpec("sh", data, $"-c \"{unix}\" {{data}}", Environment: environment); // {data} stops the runner from adding -game, which sh rejects

    private static string Sleeper => OperatingSystem.IsWindows() ? "ping -n 30 127.0.0.1 >nul" : "sleep 30";

    /// <summary>The runner name the finder accepts on this system: a .exe is only looked for on Windows.</summary>
    private static string GameName => OperatingSystem.IsWindows() ? "Game.exe" : "Game";

    [Fact]
    public async Task OutputOfBothStreamsIsCapturedWithTheDataPathAndEnvironment()
    {
        using var dir = new TempDir();
        var data = dir.File("data.win");
        var live = new LiveRun();
        RunExit? exit = null;
        live.Finished += e => exit = e;
        var win = "echo out {data}& echo %G3MTEST%& echo err 1>&2";
        var unix = "echo out {data}; echo $G3MTEST; echo err >&2";
        Assert.True(await live.StartAsync(() => Task.FromResult<RunSpec?>(Child(data, win, unix, "G3MTEST=hello"))));
        await live.WaitIdleAsync();
        var lines = live.Log.Lines;
        Assert.Contains(lines, l => !l.Error && l.Text.Contains("out " + data, StringComparison.Ordinal));
        Assert.Contains(lines, l => !l.Error && l.Text.Contains("hello", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.Error && l.Text.Contains("err", StringComparison.Ordinal));
        Assert.NotNull(exit);
        Assert.Equal(0, exit.Code);
        Assert.False(exit.Crashed);
        Assert.Equal(RunState.Idle, live.State);
    }

    [Fact]
    public async Task ANonZeroExitIsACrashWithItsLastLines()
    {
        using var dir = new TempDir();
        var live = new LiveRun();
        RunExit? exit = null;
        live.Finished += e => exit = e;
        await live.StartAsync(() => Task.FromResult<RunSpec?>(Child(dir.File("d.win"), "echo boom& exit 3", "echo boom; exit 3")));
        await live.WaitIdleAsync();
        Assert.NotNull(exit);
        Assert.Equal(3, exit.Code);
        Assert.True(exit.Crashed);
        Assert.Contains("boom", exit.Tail);
    }

    [Fact]
    public async Task StopEndsOnlyTheStartedProcessAndIsNotACrash()
    {
        using var dir = new TempDir();
        var live = new LiveRun { Grace = TimeSpan.FromMilliseconds(200) };
        RunExit? exit = null;
        live.Finished += e => exit = e;
        await live.StartAsync(() => Task.FromResult<RunSpec?>(Child(dir.File("d.win"), Sleeper, Sleeper)));
        Assert.Equal(RunState.Running, live.State);
        await live.StopAsync();
        Assert.Equal(RunState.Idle, live.State);
        Assert.NotNull(exit);
        Assert.True(exit.Stopped);
        Assert.False(exit.Crashed);
    }

    [Fact]
    public async Task RestartRunsThePrepareStepAgainAndStartsANewProcess()
    {
        using var dir = new TempDir();
        var live = new LiveRun { Grace = TimeSpan.FromMilliseconds(200) };
        var prepared = 0;
        Task<RunSpec?> Prepare()
        {
            prepared++;
            return Task.FromResult<RunSpec?>(Child(dir.File("d.win"), Sleeper, Sleeper));
        }

        Assert.True(await live.StartAsync(Prepare));
        Assert.False(await live.StartAsync(Prepare)); // one at a time
        Assert.True(await live.RestartAsync(Prepare));
        Assert.Equal(2, prepared);
        Assert.Equal(RunState.Running, live.State);
        await live.StopAsync();
    }

    [Fact]
    public async Task AFailedPrepareLeavesNothingRunning()
    {
        var live = new LiveRun();
        Assert.False(await live.StartAsync(() => Task.FromResult<RunSpec?>(null)));
        Assert.Equal(RunState.Idle, live.State);
    }

    [Fact]
    public void RunnersAreFoundNextToTheDataFileOneFolderUpAndInTheMacBundle()
    {
        using var dir = new TempDir();
        var assets = Directory.CreateDirectory(dir.File("assets")).FullName;
        var data = Path.Combine(assets, "game.unx");
        Assert.Null(RunLocator.Find(data, null));
        File.WriteAllText(dir.File("runner"), "");
        Assert.Equal(dir.File("runner"), RunLocator.Find(data, null)); // Steam layout: the runner sits above assets/
        var mac = Directory.CreateDirectory(dir.File("App.app/Contents")).FullName;
        Directory.CreateDirectory(Path.Combine(mac, "MacOS"));
        File.WriteAllText(Path.Combine(mac, "MacOS", "Mac_Runner"), "");
        Assert.Equal(Path.Combine(mac, "MacOS", "Mac_Runner"), RunLocator.Find(Path.Combine(dir.File("App.app/Contents"), "Resources", "game.ios"), null));
    }

    [Fact]
    public void AnUnknownProgramIsAllowedWithAWarningAndAMissingOneIsNot()
    {
        using var dir = new TempDir();
        var data = dir.File("data.win");
        var tool = dir.File("tool.exe");
        File.WriteAllText(tool, "");
        File.WriteAllText(dir.File("Game.exe"), "");
        Assert.Equal(RunnerCheck.Known, RunLocator.Check(dir.File("Game.exe"), data, "Game.exe"));
        Assert.Equal(RunnerCheck.Unknown, RunLocator.Check(tool, data, "Game.exe"));
        Assert.Equal(RunnerCheck.Missing, RunLocator.Check(dir.File("nothing.exe"), data, "Game.exe"));
        var (spec, check, _) = RunPlan.Resolve(new AppSettings { RunRunner = tool }, data, "Game.exe", data);
        Assert.NotNull(spec);
        Assert.Equal(RunnerCheck.Unknown, check);
        Assert.Null(RunPlan.Resolve(new AppSettings { RunRunner = dir.File("nothing.exe") }, data, "Game.exe", data).Spec);
    }

    [Fact]
    public void TheProjectFileOverridesTheSettingsAndEnvironmentPartsParse()
    {
        using var dir = new TempDir();
        var data = dir.File("data.win");
        File.WriteAllText(dir.File(GameName), "");
        RunPlan.SaveProject(new AppSettings { RunOptionsLocation = RunOptionsLocation.BesideDataFile }, new UserData(dir.File("home")), data, new RunProfile("p", "", "-x {data}", true, "wd", "A=1;B=2"));
        var ignored = RunPlan.Resolve(new AppSettings { RunArguments = "-settings" }, data, GameName, "work.tmp").Spec;
        Assert.Equal("-settings", ignored?.Arguments); // a project file nobody approved is ignored
        var approved = new AppSettings { RunArguments = "-settings", TrustedRunFiles = [RunPlan.ProjectHash(data)!] };
        var (spec, _, _) = RunPlan.Resolve(approved, data, GameName, "work.tmp");
        Assert.Equal(new RunSpec(dir.File(GameName), "work.tmp", "-x {data}", "wd", "A=1;B=2", true), spec);
        Assert.Equal([("A", "1"), ("B", "2")], GameRun.ParseEnvironment("A=1;B=2;;junk;=x"));
    }

    [Fact]
    public void GmlErrorLocationsAreRecognised()
    {
        Assert.Equal(new GmlLocation("gml_Object_obj_player_Step_0", 12), GmlLocation.Find("at gml_Object_obj_player_Step_0 (line 12) - x = y"));
        Assert.Equal(new GmlLocation("gml_Script_scr_a", 3), GmlLocation.Find("called from - gml_Script_scr_a (line 3)"));
        Assert.Null(GmlLocation.Find("nothing here"));
    }
}
