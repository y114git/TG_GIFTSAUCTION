using Avalonia.LogicalTree;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

public sealed class StringsExportTests
{
    [Fact]
    public async Task ExportWritesTheFileAndAFailureIsANoteNotACrash()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir, objects: 2);
        var userData = new UserData(dir.File("user"));
        using var workspace = await new WorkspaceService(userData, RecentFiles.Load(userData)).OpenAsync(path, cancellationToken: TestContext.Current.CancellationToken);
        await HeadlessApp.Run(async () =>
        {
            var window = new StringsWindow(workspace);
            var note = window.GetLogicalDescendants().OfType<Avalonia.Controls.TextBlock>().Single(t => t.Name == "StringNote");
            var good = dir.File("out.csv");
            window.ExportTo(good);
            Assert.StartsWith("index,text", await File.ReadAllTextAsync(good, TestContext.Current.CancellationToken), StringComparison.Ordinal);
            Assert.Single(Directory.GetFiles(Path.GetDirectoryName(good)!, "out.csv*"));

            window.ExportTo(Path.Combine(dir.File("missing"), "out.csv"));
            Assert.StartsWith(Localization.Loc.Current.Format("strings.exportFailed", ("reason", "")), note.Text, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task AUsedStringIsDeletedOnlyAfterTheOwnerAgrees()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir, objects: 2);
        var userData = new UserData(dir.File("user"));
        using var workspace = await new WorkspaceService(userData, RecentFiles.Load(userData)).OpenAsync(path, cancellationToken: TestContext.Current.CancellationToken);
        await HeadlessApp.Run(async () =>
        {
            var asked = new List<string>();
            var answer = false;
            var window = new StringsWindow(workspace) { AskRisky = (refusal, _, owner) => { asked.Add(refusal.BodyKey + (owner is StringsWindow ? "" : "?")); return Task.FromResult(answer); } };
            var list = window.GetLogicalDescendants().OfType<Avalonia.Controls.ListBox>().Single(l => l.Name == "StringList");
            list.SelectedItem = list.Items.Single(i => i?.ToString()?.EndsWith("obj_0", StringComparison.Ordinal) == true);
            var count = workspace.Data.Strings.Count;
            await window.DeleteSelectedAsync();
            Assert.Equal(["strings.deleteRisk"], asked);
            Assert.Equal(count, workspace.Data.Strings.Count);
            answer = true;
            await window.DeleteSelectedAsync();
            Assert.Equal(count - 1, workspace.Data.Strings.Count);
            workspace.History.Undo();
            Assert.Equal(count, workspace.Data.Strings.Count);
        });
    }

    [Fact]
    public async Task AUsedStringIsNotDeletedWhenRiskyActionsAreRefusedAndTheWindowSaysSo()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir, objects: 2);
        var userData = new UserData(dir.File("user"));
        using var workspace = await new WorkspaceService(userData, RecentFiles.Load(userData)).OpenAsync(path, cancellationToken: TestContext.Current.CancellationToken);
        await HeadlessApp.Run(async () =>
        {
            var asked = 0;
            var window = new StringsWindow(workspace) { Risk = () => G3MStudio.Core.Settings.RiskChoice.Refuse, AskRisky = (_, _, _) => { asked++; return Task.FromResult(true); } };
            var list = window.GetLogicalDescendants().OfType<Avalonia.Controls.ListBox>().Single(l => l.Name == "StringList");
            var note = window.GetLogicalDescendants().OfType<Avalonia.Controls.TextBlock>().Single(t => t.Name == "StringNote");
            list.SelectedItem = list.Items.Single(i => i?.ToString()?.EndsWith("obj_0", StringComparison.Ordinal) == true);
            var count = workspace.Data.Strings.Count;
            await window.DeleteSelectedAsync();
            Assert.Equal(0, asked);
            Assert.Equal(count, workspace.Data.Strings.Count);
            Assert.Equal(Localization.Loc.Current["strings.deleteRefused"], note.Text);
        });
    }

    [Fact]
    public void ALongTextIsCutWithoutSplittingAPairAndHasNoLineBreak()
    {
        Assert.Equal(new string('a', 39) + "...", StringsWindow.Shorten(new string('a', 39) + "\U0001F600tail"));
        Assert.Equal(new string('a', 40) + "...", StringsWindow.Shorten(new string('a', 41)));
        Assert.Equal("one two", StringsWindow.Shorten("one\r\ntwo"));
    }
}
