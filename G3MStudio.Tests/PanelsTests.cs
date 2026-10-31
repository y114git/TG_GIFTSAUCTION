using Avalonia.Threading;
using G3MStudio.Core.Notifications;
using G3MStudio.Panels;

namespace G3MStudio.Tests;

public sealed class PanelsTests
{
    [Fact]
    public async Task AProblemOpensItsCodeEntryAndOutputKeepsEveryMessage()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir, withCode: true);
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                await window.OpenAsync(path);
                var workspace = window.Workspaces.Current!;
                var problems = window.Docking.Documents().Count();
                window.Notify(NotificationLevel.Warning, "first");
                window.Notify(NotificationLevel.Info, "second");
                window.Notifications.DismissAll();

                var diagnostic = new G3MLib.Analyzer.Compiler.GmlDiagnostic(G3MLib.Analyzer.Compiler.DiagnosticSeverity.Error, "GML2001", "Expected ')'", new G3MLib.Analyzer.Compiler.SourceSpan(0, 1, 1, 8, 1, 9));
                workspace.Problems.Set("gml_Script_probe", [diagnostic]);
                Dispatcher.UIThread.RunJobs();

                var panels = window.GetPanel<ProblemsPanel>();
                var problem = Assert.Single(panels.Rows);
                Assert.Equal((1, 8), (problem.Line, problem.Column));
                panels.Open(problem);
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(problems + 1, window.Docking.Documents().Count());
                Assert.Contains(window.Docking.Tabs.Tabs, t => t.Key.EndsWith("gml_Script_probe", StringComparison.Ordinal));

                Assert.Equal(["first", "second"], window.GetPanel<OutputPanel>().Rows.Select(n => n.Text).TakeLast(2));
            }
            finally { window.Close(); }
        });
    }
}
