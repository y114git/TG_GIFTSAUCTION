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
            Assert.Null(await ScriptRunner.RunAsync(workspace, script, messages.Add, warnings.Add));
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
        using (workspace) Assert.Contains(expected, await ScriptRunner.RunAsync(workspace, script, _ => { }, _ => { }) ?? "", StringComparison.Ordinal);
    }
}
