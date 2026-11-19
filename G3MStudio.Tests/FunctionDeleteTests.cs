using G3MLib.DataFile;
using G3MLib.DataFile.Compiler;
using G3MStudio.Core.Notifications;
using G3MStudio.Core.Workspace;
using G3MStudio.Editor;

namespace G3MStudio.Tests;

public sealed class FunctionDeleteTests
{
    [Theory]
    [InlineData("function foo() { return 1; }\nfunction bar() { return 2; }\n", "function bar() { return 2; }\n")]
    [InlineData("var a = 1;\nfoo = function() { return 1; };\nvar b = 2;\n", "var a = 1;\nvar b = 2;\n")]
    [InlineData("function foo(a, b) constructor { x = a; }", "")]
    [InlineData("var a = 1;\nvar foo = function() { return 1; };\nvar b = 2;\n", "var a = 1;\nvar b = 2;\n")]
    [InlineData("static foo = function() { return 1; };\nx = 1;", "x = 1;")]
    [InlineData("self.foo = function() { return 1; };\nx = 1;", "x = 1;")]
    [InlineData("x = 1; var foo = function() {}\ny = 2;", "x = 1; \ny = 2;")]
    [InlineData("foo = function() { return 1; };\nx = 1;", "x = 1;")]
    public void TheDeclarationIsCutOutWithItsLine(string text, string expected) => Assert.Equal(expected, FunctionSource.Remove(text, "foo"));

    [Theory]
    [InlineData("function bar() { function foo() { return 1; } }")] // inside another function
    [InlineData("function foo() { return 1; }\nfunction foo() { return 2; }")] // declared twice
    [InlineData("function bar() { return 1; }")] // not declared
    [InlineData("function foo()\n    return 1;")] // no braces
    public void ADeclarationThatCannotBeLocatedSafelyIsNotCut(string text) => Assert.Null(FunctionSource.Remove(text, "foo"));

    // A library script with foo and bar, a script of its own, and code that calls them.
    private static string WriteGame(TempDir dir, bool barCallsFoo)
    {
        using var data = TestData.Create(1, true);
        var group = new CodeImportGroup(data);
        group.QueueReplace("gml_GlobalScript_lib", "function foo() { return 1; }\nfunction bar() { return " + (barCallsFoo ? "foo() + 2" : "2") + "; }\n");
        group.Import();
        return TestData.Write(dir.File("game.win"), data);
    }

    private static async Task Run(TempDir dir, string path, Func<MainWindow, Workspace, Task> body)
    {
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                await window.OpenAsync(path);
                await body(window, window.Workspaces.Current!);
            }
            finally { window.Workspaces.Close(); window.Close(); }
        });
    }

    private static string[] Functions(Workspace w) => [.. w.Data.Functions.Select(f => f.Name.Content!)];

    [Fact]
    public void ADeleteGroupOfSeveralFunctionEntriesReadsTheReferencesOnce()
    {
        using var data = TestData.Create(1, true);
        var group = new CodeImportGroup(data);
        group.QueueReplace("gml_GlobalScript_lib", "function foo() { return 1; }\nfunction bar() { return 2; }\n");
        group.Import();
        new EditCodeCommand("gml_GlobalScript_lib", "x = 1;").Apply(data); // nothing defines or calls them any more
        var index = ReferenceCache.Of(data).Get(data, TestContext.Current.CancellationToken);
        var delete = new GroupCommand([new ResourceListCommand("functions", ResourceListCommand.Delete, "gml_Script_foo"), new ResourceListCommand("functions", ResourceListCommand.Delete, "gml_Script_bar")]);
        delete.Apply(data);
        Assert.DoesNotContain(data.Functions, f => f.Name.Content is "gml_Script_foo" or "gml_Script_bar");
        Assert.Same(index, ReferenceCache.Of(data).Get(data, TestContext.Current.CancellationToken)); // not built again per entry
        delete.Revert(data);
        Assert.Contains(data.Functions, f => f.Name.Content == "gml_Script_foo");
    }

    [Fact]
    public async Task AFunctionOfALargerScriptIsCutOutAndLeavesTheListAtOnceAndUndoBringsItBack()
    {
        using var dir = new TempDir();
        await Run(dir, WriteGame(dir, false), async (window, workspace) =>
        {
            Assert.Contains("gml_Script_foo", Functions(workspace));
            string? shown = null;
            window.Confirmer = (title, message) => { shown = title + "|" + message; return Task.FromResult(true); };
            await window.DeleteFunctionAsync(workspace, "gml_Script_foo");
            Assert.Contains("gml_GlobalScript_lib", shown, StringComparison.Ordinal);
            Assert.True(!Functions(workspace).Contains("gml_Script_foo"), string.Join(" ", window.Notifications.Active.Select(n => n.Text)));
            Assert.Null(workspace.Data.Code.ByName("gml_Script_foo"));
            Assert.NotNull(workspace.Data.Code.ByName("gml_Script_bar"));
            Assert.Contains("gml_Script_bar", Functions(workspace));
            workspace.History.Undo();
            Assert.Contains("gml_Script_foo", Functions(workspace));
            Assert.NotNull(workspace.Data.Code.ByName("gml_Script_foo"));
            Assert.Contains(workspace.Data.Scripts, s => s.Name.Content == "gml_Script_foo");
        });
    }

    [Fact]
    public async Task TheOpenTabOfTheDeletedFunctionCodeIsClosed()
    {
        using var dir = new TempDir();
        await Run(dir, WriteGame(dir, false), async (window, workspace) =>
        {
            window.Explorer.Model.Toggle("code");
            window.Explorer.RequestOpen(window.Explorer.Model.Rows.Single(r => r.IsItem && r.Key == "code" && r.Text == "gml_Script_foo"), true);
            Assert.Contains(window.Docking.Tabs.Tabs, t => t.Key == "code:gml_Script_foo");
            window.Confirmer = (_, _) => Task.FromResult(true);
            await window.DeleteFunctionAsync(workspace, "gml_Script_foo");
            Assert.DoesNotContain(window.Docking.Tabs.Tabs, t => t.Key == "code:gml_Script_foo");
        });
    }

    [Fact]
    public void AGlobalScriptOfItsOwnIsPlannedAsOwnScriptAndALibraryFunctionAsInside()
    {
        using var data = TestData.Create(1, true);
        var group = new CodeImportGroup(data);
        group.QueueReplace("gml_GlobalScript_lib", "function foo() { return 1; }");
        group.Import();
        Assert.Equal(new FunctionDeletePlan(FunctionDeletePlan.Way.OwnScript, "gml_Script_probe", "probe"), FunctionDeletion.Plan(data, "gml_Script_probe"));
        Assert.Equal(new FunctionDeletePlan(FunctionDeletePlan.Way.Inside, "gml_GlobalScript_lib"), FunctionDeletion.Plan(data, "gml_Script_foo"));
        Assert.Equal("Code", FunctionDeletion.Plan(data, "method").Reason);
    }

    [Fact]
    public async Task AFunctionThatOtherCodeCallsIsAskedAboutWithTheUsagesAndKeepsItsListEntry()
    {
        using var dir = new TempDir();
        await Run(dir, WriteGame(dir, true), async (window, workspace) =>
        {
            string? shown = null;
            window.Confirmer = (title, message) => { shown = title + "|" + message; return Task.FromResult(true); };
            await window.DeleteFunctionAsync(workspace, "gml_Script_foo");
            Assert.NotNull(shown);
            Assert.True(shown!.Contains("gml_GlobalScript_lib", StringComparison.Ordinal), shown); // the call left in the script is listed
            Assert.Null(workspace.Data.Code.ByName("gml_Script_foo"));
            Assert.Contains("gml_Script_foo", Functions(workspace)); // the call still names it
        });
    }

    [Fact]
    public async Task ABuiltInFunctionIsRefusedWithAReasonAndNothingChanges()
    {
        using var dir = new TempDir();
        await Run(dir, WriteGame(dir, false), async (window, workspace) =>
        {
            var before = Functions(workspace);
            window.Confirmer = (_, _) => throw new InvalidOperationException("no question for a refused delete");
            await window.DeleteFunctionAsync(workspace, "method");
            Assert.Equal(before, Functions(workspace));
            Assert.Contains(window.Notifications.Active, n => n.Level == NotificationLevel.Warning && n.Text.Contains("method", StringComparison.Ordinal));
            Assert.False(workspace.History.CanUndo);
        });
    }

    [Fact]
    public async Task AnUnusedVariableEntryIsDeletedAndUndoneAndAUsedOneStays()
    {
        using var dir = new TempDir();
        await Run(dir, WriteGame(dir, false), async (window, workspace) =>
        {
            var index = ReferenceCache.Of(workspace.Data).Get(workspace.Data);
            var used = workspace.Data.Variables.First(v => index.FindUsages(v).Count > 0);
            var usedName = used.Name.Content!;
            using var spare = new G3MLib.DataFile.Models.GameMakerVariable { Name = workspace.Data.Strings.MakeString("zz_unused"), InstanceType = used.InstanceType };
            workspace.Data.Variables.Add(spare);
            window.Confirmer = (_, _) => Task.FromResult(true);
            var count = workspace.Data.Variables.Count;
            await window.DeleteVariableAsync(workspace, usedName);
            Assert.Equal(count, workspace.Data.Variables.Count);
            Assert.Contains(window.Notifications.Active, n => n.Level == NotificationLevel.Warning && n.Text.Contains(usedName, StringComparison.Ordinal));
            await window.DeleteVariableAsync(workspace, "zz_unused");
            Assert.DoesNotContain(spare, workspace.Data.Variables);
            workspace.History.Undo();
            Assert.Contains(spare, workspace.Data.Variables);
        });
    }
}
