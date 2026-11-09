using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

public sealed class CodeFileImportTests
{
    [Fact]
    public async Task FilesReplaceTheEntriesTheyAreNamedAfterAsOneUndoableEdit()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir, objects: 1, withCode: true);
        var folder = Directory.CreateDirectory(dir.File("code")).FullName;
        await File.WriteAllTextAsync(Path.Combine(folder, "gml_Script_probe.gml"), "return 5;", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(folder, "no_such_entry.gml"), "return 6;", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(folder, "readme.txt"), "ignored", TestContext.Current.CancellationToken);

        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            string? asked = null;
            window.Confirmer = (_, message) =>
            {
                asked = message;
                return Task.FromResult(true);
            };
            try
            {
                await window.OpenAsync(path);
                var workspace = window.Workspaces.Current!;
                var plan = CodeFileImport.Plan(workspace.Data, CodeFileImport.Collect([folder]));
                Assert.Equal(["gml_Script_probe"], plan.Matches.Select(m => m.Entry));
                Assert.Equal(["no_such_entry.gml"], plan.Misses);

                await window.ImportCodePathsAsync([folder]);
                Assert.Contains("gml_Script_probe", asked, StringComparison.Ordinal);
                Assert.Contains("no_such_entry.gml", asked, StringComparison.Ordinal);
                Assert.Contains("return 5", CodeText.Decompile(workspace.Data, "gml_Script_probe"), StringComparison.Ordinal);
                workspace.Undo();
                Assert.Contains("return 1", CodeText.Decompile(workspace.Data, "gml_Script_probe"), StringComparison.Ordinal);

                // A file that does not compile stops the whole import and changes nothing.
                await File.WriteAllTextAsync(Path.Combine(folder, "gml_Script_probe.gml"), "return (", TestContext.Current.CancellationToken);
                await window.ImportCodePathsAsync([folder]);
                Assert.Contains("return 1", CodeText.Decompile(workspace.Data, "gml_Script_probe"), StringComparison.Ordinal);
            }
            finally { window.Close(); }
        });
    }
}
