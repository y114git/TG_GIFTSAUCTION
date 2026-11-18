using Avalonia.Threading;
using G3MStudio.Core.Workspace;
using G3MStudio.Editor;

namespace G3MStudio.Tests;

/// <summary>The worker-built indexes the code check reads: script functions of the game and names other code entries declare.</summary>
public sealed class CodeIntelIndexTests
{
    private const string Config = "#macro TOP_SPEED 12\nenum Team { red, blue }\n";

    private static bool Unknown(GmlLintContext context, string text) => GmlLint.Analyze(GmlDoc.Parse(text), context).Any(i => i.Diagnostic.Code == GmlLint.UnknownVariable);

    [Fact]
    public async Task BeforeTheFirstIndexNoNameIsCalledUnknownAndAfterItTyposAre()
    {
        using var dir = new TempDir();
        using var workspace = await CodeIntelUiTests.Open(dir);
        var context = GmlLintSource.For(workspace, "gml_Script_probe");
        // The build waits for the model lock, so the index is surely missing while the test holds it.
        using (workspace.Lock.Write())
        {
            Assert.False(GmlProjectSymbols.IsBuilt(workspace));
            Assert.False(Unknown(context, "x = TOP_SPEDE;\n"));
        }

        await GmlProjectSymbols.ReadyAsync(workspace);
        Assert.True(Unknown(context, "x = TOP_SPEDE;\n"));
    }

    [Fact]
    public async Task ASaveKeepsTheIndexAndAnEditKeepsTheLastOneAnsweringWhileItIsBuiltAgain()
    {
        using var dir = new TempDir();
        using var workspace = await CodeIntelUiTests.Open(dir);
        workspace.Edit(new CodeEntryCommand("gml_Script_config"));
        workspace.Edit(new EditCodeCommand("gml_Script_config", Config));
        await GmlProjectSymbols.ReadyAsync(workspace);

        workspace.History.MarkSaved();
        using (workspace.Lock.Write()) Assert.NotNull(GmlProjectSymbols.Find(workspace, "TOP_SPEED"));
        await GmlProjectSymbols.ReadyAsync(workspace);

        workspace.Edit(new EditCodeCommand("gml_Script_probe", "x = 1;\n"));
        using (workspace.Lock.Write()) Assert.NotNull(GmlProjectSymbols.Find(workspace, "Team"));
        await GmlProjectSymbols.ReadyAsync(workspace);
        Assert.NotNull(GmlProjectSymbols.Find(workspace, "Team"));
    }

    [Fact]
    public async Task AnEditDuringABuildStartsItAgainAndTheNewIndexHasTheEdit()
    {
        using var dir = new TempDir();
        using var workspace = await CodeIntelUiTests.Open(dir);
        workspace.Edit(new CodeEntryCommand("gml_Script_config"));
        using var hold = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void Gate(Workspace w, string _)
        {
            if (!ReferenceEquals(w, workspace) || hold.IsSet) return;
            entered.TrySetResult();
            hold.Wait(TimeSpan.FromSeconds(30));
        }

        GmlProjectSymbols.Decompiling += Gate;
        try
        {
            Assert.False(GmlProjectSymbols.IsBuilt(workspace));
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
            workspace.Edit(new EditCodeCommand("gml_Script_config", Config));
            hold.Set();
            await GmlProjectSymbols.ReadyAsync(workspace).WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
            Assert.Equal("gml_Script_config", GmlProjectSymbols.Find(workspace, "TOP_SPEED")?.Entry);
        }
        finally
        {
            GmlProjectSymbols.Decompiling -= Gate;
            hold.Set();
        }
    }

    [Fact]
    public async Task AnOpenTabIsCheckedAgainWhenTheIndexIsReady()
    {
        await CodeIntelUiTests.WithTab(async (tab, workspace, _) =>
        {
            await GmlProjectSymbols.ReadyAsync(workspace);
            await tab.CheckLiveAsync();
            bool Flagged() => workspace.Problems.All.Any(p => p.Source == "gml_Script_probe" && p.Code == GmlLint.UnknownVariable);
            Assert.True(Flagged());

            workspace.Edit(new CodeEntryCommand("gml_Script_config"));
            workspace.Edit(new EditCodeCommand("gml_Script_config", Config));
            await GmlProjectSymbols.ReadyAsync(workspace);
            await CodeIntelUiTests.Settle(() => !Flagged());
        }, "x = TOP_SPEED;\n");
    }

    [Fact]
    public async Task ManyPeeksAtAScriptFunctionAskForOneRead()
    {
        using var dir = new TempDir();
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                await window.OpenAsync(TestData.WriteSample(dir, objects: 2, withCode: true));
                var workspace = window.Workspaces.Current!;
                var ready = 0;
                void Count(Workspace w) { if (ReferenceEquals(w, workspace)) ready++; }
                GameFunctions.Ready += Count;
                try
                {
                    for (var i = 0; i < 1000; i++) Assert.Null(GameFunctions.Peek(workspace, "scr_0"));
                    for (var i = 0; i < 500 && ready == 0; i++)
                    {
                        Dispatcher.UIThread.RunJobs();
                        await Task.Delay(10);
                    }

                    Dispatcher.UIThread.RunJobs();
                    Assert.Equal(1, ready);
                    Assert.NotNull(GameFunctions.Peek(workspace, "scr_0"));
                }
                finally { GameFunctions.Ready -= Count; }
            }
            finally { window.Close(); }
        });
    }
}
