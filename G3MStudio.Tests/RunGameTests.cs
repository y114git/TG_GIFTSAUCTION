using Avalonia.Threading;
using G3MStudio.Core.Workspace;
using G3MStudio.Panels;

namespace G3MStudio.Tests;

public sealed class RunGameTests
{
    /// <summary>The runner name the finder accepts on this system: a .exe is only looked for on Windows.</summary>
    private static string GameName => OperatingSystem.IsWindows() ? "Game.exe" : "Game";

    [Fact]
    public void TheRunnerIsTheGeneralInfoNameNextToTheDataFile()
    {
        using var dir = new TempDir();
        var data = dir.File("data.win");
        Assert.Null(GameRun.FindRunner(data, GameName));
        File.WriteAllText(dir.File(GameName), string.Empty);
        Assert.Equal(dir.File(GameName), GameRun.FindRunner(data, GameName));
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

    private static (string Runner, string Arguments) Shell(string windows, string unix) =>
        OperatingSystem.IsWindows() ? ("cmd.exe", $"/s /c \"{windows}\"") : ("sh", $"-c \"{unix}\" {{data}}"); // {data} stops the runner from adding -game, which sh rejects

    [Fact]
    public async Task ACrashShowsTheExitCodeTheLastLinesAndAProblemThatOpensTheCodeLine()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir, "game.win", 1, true);
        var (runner, arguments) = Shell("echo at gml_Script_probe (line 7)& exit 3", "echo at gml_Script_probe '(line 7)'; exit 3");
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                await window.OpenAsync(path);
                window.Workspaces.Settings.Update(x => x with { RunRunner = runner, RunArguments = arguments });
                await window.RunGameAsync();
                await window.WaitRunAsync();
                Dispatcher.UIThread.RunJobs();
                Assert.Contains(window.Notifications.Active, n => n.Text.Contains("exit code 3", StringComparison.Ordinal) && n.Text.Contains("(line 7)", StringComparison.Ordinal));
                Assert.Contains(window.Workspaces.Current!.Problems.All, p => p.Code == "RUN" && p.Source == "gml_Script_probe" && p.Line == 7);
                Assert.Contains(window.GetPanel<OutputPanel>().Rows, r => r.Location == new GmlLocation("gml_Script_probe", 7));
                Assert.False(File.Exists(dir.File("g3mstudio_run.tmp")));
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public async Task QuickRestartReusesTheWorkingCopyUntilAnEditAndStopOnlyEndsTheGame()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir, "game.win", 1, true);
        var sleeper = OperatingSystem.IsWindows() ? "ping -n 30 127.0.0.1 >nul" : "sleep 30";
        var (runner, arguments) = Shell(sleeper, sleeper);
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                await window.OpenAsync(path);
                window.Workspaces.Settings.Update(x => x with { RunRunner = runner, RunArguments = arguments, RunStopSeconds = 1 });
                await window.RunGameAsync();
                Assert.Equal(RunState.Running, window.RunState);
                var copy = dir.File("g3mstudio_run.tmp");
                var stamp = File.GetLastWriteTimeUtc(copy);
                await Task.Delay(50);
                await window.RestartGameAsync();
                Assert.Equal(RunState.Running, window.RunState);
                Assert.Equal(stamp, File.GetLastWriteTimeUtc(copy)); // nothing changed: no new write
                await window.StopGameAsync();
                await window.WaitRunAsync();
                Assert.Equal(RunState.Idle, window.RunState);
                Assert.False(File.Exists(copy));
                Assert.DoesNotContain(window.Notifications.Active, n => n.Text.Contains("ended with exit code", StringComparison.Ordinal));
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public void TheOutputPanelFiltersClearsAndOpensLocations()
    {
        var log = new RunLog();
        var center = new G3MStudio.Core.Notifications.NotificationCenter();
        var panel = new OutputPanel();
        panel.Bind(center, log);
        log.Add("hello", false);
        log.Add("at gml_Script_a (line 4)", true);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(2, panel.Rows.Count);
        panel.SetFilter("HELLO");
        Assert.Equal(["hello"], panel.Rows.Select(r => r.Text));
        panel.SetFilter("");
        GmlLocation? opened = null;
        panel.LocationRequested += l => opened = l;
        panel.Open(panel.Rows[1]);
        Assert.Equal(new GmlLocation("gml_Script_a", 4), opened);
        panel.Clear();
        Dispatcher.UIThread.RunJobs();
        Assert.Empty(panel.Rows);
    }

    [Fact]
    public async Task TheOutputPanelEnablesRestartAndStopPerRunStateAndNamesLevelsInTheLanguage()
    {
        await HeadlessApp.Run(() =>
        {
            var loc = G3MStudio.Localization.Loc.Current;
            var panel = new OutputPanel();
            var center = new G3MStudio.Core.Notifications.NotificationCenter();
            panel.Bind(center, new RunLog());
            var window = new Avalonia.Controls.Window { Content = panel };
            window.Show();
            try
            {
                Avalonia.Controls.Button Button(string name) => Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(window).OfType<Avalonia.Controls.Button>().Single(b => b.Name == name);
                foreach (var (state, restart, stop) in new[] { (RunState.Idle, true, false), (RunState.Preparing, false, false), (RunState.Running, true, true), (RunState.Stopping, false, false) })
                {
                    panel.SetRunState(state);
                    Assert.Equal((restart, stop), (Button("OutputRestart").IsEnabled, Button("OutputStop").IsEnabled));
                }

                loc.SetLanguage("ru");
                center.Post(G3MStudio.Core.Notifications.NotificationLevel.Warning, "x");
                Assert.StartsWith(loc["notify.warning"] + ": ", panel.Rows[0].Text, StringComparison.Ordinal);
            }
            finally
            {
                loc.SetLanguage("en");
                window.Close();
            }

            return Task.CompletedTask;
        });
    }
}
