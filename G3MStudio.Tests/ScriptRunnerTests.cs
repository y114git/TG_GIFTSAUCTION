using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

public sealed class ScriptRunnerTests
{
    private static async Task<(Workspace Workspace, string Script)> OpenWithScript(TempDir dir, string code)
    {
        var path = TestData.WriteSample(dir, objects: 2);
        var userData = new UserData(dir.File("user"));
        var workspace = await new WorkspaceService(userData, RecentFiles.Load(userData)).OpenAsync(path, cancellationToken: TestContext.Current.CancellationToken);
        var script = dir.File("test.csx");
        await File.WriteAllTextAsync(script, code, TestContext.Current.CancellationToken);
        return (workspace, script);
    }

    [Fact]
    public async Task AScriptReadsAndChangesTheDataAndReportsMessages()
    {
        using var dir = new TempDir();
        var (workspace, script) = await OpenWithScript(dir, "ScriptMessage(\"objects: \" + Data.GameObjects.Count);\nData.GameObjects[0].Depth = 77;\nScriptWarning(\"careful\");");
        using (workspace)
        {
            var messages = new List<string>();
            var warnings = new List<string>();
            Assert.Null(await ScriptRunner.RunAsync(workspace, script, messages.Add, warnings.Add, cancellationToken: TestContext.Current.CancellationToken));
            Assert.Equal(["objects: 2"], messages);
            Assert.Equal(["careful"], warnings);
            Assert.Equal(77, workspace.Data.GameObjects[0].Depth);
        }
    }

    [Theory]
    [InlineData("ScriptError(\"stop here\", \"Oops\");", "Oops: stop here")]
    [InlineData("this is not valid;", "error CS")]
    [InlineData("throw new InvalidOperationException(\"boom\");", "boom")]
    public async Task ScriptErrorsCompileErrorsAndExceptionsComeBackAsText(string code, string expected)
    {
        using var dir = new TempDir();
        var (workspace, script) = await OpenWithScript(dir, code);
        using (workspace) Assert.Contains(expected, await ScriptRunner.RunAsync(workspace, script, _ => { }, _ => { }, cancellationToken: TestContext.Current.CancellationToken) ?? "", StringComparison.Ordinal);
    }

    [Fact]
    public async Task AScriptCanAskAndPromptThroughTheHostAndShowProgressAndAnAsset()
    {
        using var dir = new TempDir();
        var (workspace, script) = await OpenWithScript(dir, """
            var name = SimpleTextInput("Name", "Pick", "fallback", false);
            ScriptMessage("name=" + name);
            ScriptMessage("yes=" + ScriptQuestion("Go on?"));
            ScriptMessage("file=" + PromptLoadFile(".txt", "*.txt"));
            SetProgressBar("Work", "step", 0, 10);
            IncrementProgress();
            ChangeSelection(Data.GameObjects[0]);
            SetFinishedMessage(false);
            RunUMTScript("nested.csx");
            """);
        await File.WriteAllTextAsync(dir.File("nested.csx"), "ScriptMessage(\"nested ran\");", TestContext.Current.CancellationToken);
        using (workspace)
        {
            var messages = new List<string>();
            var progress = new List<(string? Label, int Value, double Max)>();
            var host = new ScriptHost
            {
                Input = (_, _, _, _) => "Ada",
                Question = _ => false,
                LoadFile = (ext, _) => "C:/x/a" + ext,
                Progress = (label, _, value, max) => progress.Add((label, value, max)),
            };
            Assert.Null(await ScriptRunner.RunAsync(workspace, script, messages.Add, _ => { }, host, cancellationToken: TestContext.Current.CancellationToken));
            Assert.Contains("name=Ada", messages);
            Assert.Contains("yes=False", messages);
            Assert.Contains("file=C:/x/a.txt", messages);
            Assert.Contains("nested ran", messages);
            Assert.Equal(("Work", 0, 10.0), progress[0]);
            Assert.Equal(1, progress[^1].Value);
            Assert.False(host.FinishedMessage);
            Assert.Same(workspace.Data.GameObjects[0], Assert.Single(host.Selections));
        }
    }

    [Fact]
    public async Task APromptWithoutAHostFailsWithTheLibrarysMessage()
    {
        using var dir = new TempDir();
        var (workspace, script) = await OpenWithScript(dir, "PromptChooseDirectory();");
        using (workspace) Assert.Contains("UI host", await ScriptRunner.RunAsync(workspace, script, _ => { }, _ => { }, cancellationToken: TestContext.Current.CancellationToken) ?? "", StringComparison.Ordinal);
    }

    [Fact]
    public async Task StopEndsAScriptThatNeverReturnsAndTheFileIsUsableAgain()
    {
        using var dir = new TempDir();
        var (workspace, script) = await OpenWithScript(dir, "ScriptMessage(\"start\");\nwhile (true) { CancellationToken.ThrowIfCancellationRequested(); }");
        using (workspace)
        {
            using var stop = new CancellationTokenSource();
            var error = await ScriptRunner.RunAsync(workspace, script, _ => stop.Cancel(), _ => { }, allowUnsaved: true, cancellationToken: stop.Token);
            Assert.Contains("cancelled", error, StringComparison.Ordinal);
            Assert.False(workspace.History.Frozen);
            Assert.True(workspace.Edit(ResourceTemplates.New(workspace.Data, "paths", "pth_after_stop")));
        }
    }

    [Fact]
    public async Task ASaveAskedWhileAScriptRunsWaitsForItAndWritesItsWholeChange()
    {
        using var dir = new TempDir();
        var flag = dir.File("go.flag").Replace('\\', '/');
        var (workspace, script) = await OpenWithScript(dir, "Data.GameObjects[0].Depth = 55;\nScriptMessage(\"started\");\nSystem.Threading.SpinWait.SpinUntil(() => System.IO.File.Exists(\"" + flag + "\"), 20000);\nData.GameObjects[1].Depth = 66;");
        using (workspace)
        {
            var started = new TaskCompletionSource();
#pragma warning disable CA2025 // the script task is awaited below, before the workspace goes
            var running = ScriptRunner.RunAsync(workspace, script, _ => started.TrySetResult(), _ => { }, allowUnsaved: true, cancellationToken: TestContext.Current.CancellationToken);
            await started.Task.WaitAsync(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken);
            var saving = workspace.SaveAsync(new SavePipeline(new UserData(dir.File("user2"))), cancellationToken: TestContext.Current.CancellationToken);
            Assert.False(saving.IsCompleted); // the save waits for the script, which holds the model
            await File.WriteAllTextAsync(flag, "go", TestContext.Current.CancellationToken);
            Assert.Null(await running.WaitAsync(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken));
            Assert.True((await saving.WaitAsync(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken)).Saved);
            using var saved = await G3MLib.DataFile.GameMakerIO.ReadAsync(workspace.Path, cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal([55, 66], saved.GameObjects.Take(2).Select(o => o.Depth));
        }
    }
}
