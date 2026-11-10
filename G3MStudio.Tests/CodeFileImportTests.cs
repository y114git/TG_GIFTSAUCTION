using G3MLib.DataFile;
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
            var create = false;
            window.Confirmer = (_, message) =>
            {
                asked = message;
                return Task.FromResult(!message.Contains("name no code entry", StringComparison.Ordinal) || create);
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

                // A file that does not compile is listed; refusing the question imports nothing.
                await File.WriteAllTextAsync(Path.Combine(folder, "gml_Script_probe.gml"), "return (", TestContext.Current.CancellationToken);
                await File.WriteAllTextAsync(Path.Combine(folder, "gml_Script_probe2.gml"), "return 7;", TestContext.Current.CancellationToken);
                await window.ImportCodePathsAsync([folder]);
                Assert.Contains("return 1", CodeText.Decompile(workspace.Data, "gml_Script_probe"), StringComparison.Ordinal);

                // A file with no entry gets one when asked; the file that fails is skipped and the rest go in as one step.
                create = true;
                var before = workspace.Data.Code.Count;
                window.Confirmer = (_, _) => Task.FromResult(true);
                await window.ImportCodePathsAsync([folder]);
                Assert.Equal(before + 2, workspace.Data.Code.Count);
                Assert.Contains("return 6", CodeText.Decompile(workspace.Data, "no_such_entry"), StringComparison.Ordinal);
                Assert.Contains("return 1", CodeText.Decompile(workspace.Data, "gml_Script_probe"), StringComparison.Ordinal);
                workspace.Undo();
                Assert.Equal(before, workspace.Data.Code.Count);
                Assert.Null(workspace.Data.Code.ByName("no_such_entry"));
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public async Task AFileOverTheSizeCapIsRefusedWithAClearMessage()
    {
        using var dir = new TempDir();
        var file = dir.File("gml_Script_probe.gml");
        await using (var stream = File.Create(file)) stream.SetLength(CodeFileImport.MaxFileBytes + 1);

        var error = await Assert.ThrowsAsync<InvalidDataException>(() => CodeFileImport.BuildAsync([new CodeFileMatch(file, "gml_Script_probe", false)], TestContext.Current.CancellationToken));
        Assert.Contains("too big", error.Message, StringComparison.Ordinal);
    }
}
