using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using G3MStudio.Core.Notifications;
using G3MStudio.Core.Workspace;
using G3MStudio.History;
using G3MStudio.Localization;

namespace G3MStudio.Tests;

/// <summary>Item 68: a nickname of several words, with an emoji, shows on every surface that names a resource; the real name stays second where there is room, and in the code text.</summary>
public sealed class NicknameSurfaceTests
{
    private const string Nick = "🐉 Red Dragon";

    private static Nicknames Dragon()
    {
        var nicknames = new Nicknames();
        Assert.True(nicknames.Set("objects:obj_0", Nick));
        return nicknames;
    }

    [Fact]
    public void ABareNameAndAListedResourceShowTheNicknameWithTheNameBehindIt()
    {
        var nicknames = Dragon();
        Assert.Equal($"{Nick} (obj_0)", nicknames.BothNamed("obj_0"));
        Assert.Equal("obj_1", nicknames.BothNamed("obj_1"));
        Assert.Equal($"A message about '{Nick} (obj_0)'.", nicknames.Decorate("A message about 'obj_0'."));
    }

    [Fact]
    public async Task AQuickFixThatSuggestsAResourceNamesItByNicknameAndStillWritesTheName()
    {
        await CodeIntelUiTests.WithTab((tab, workspace, _) =>
        {
            workspace.Nicknames.Set("objects:obj_0", Nick);
            tab.Editor.Text = "var o = obj_O;\n";
            tab.Editor.CaretOffset = tab.Text.IndexOf("obj_O", StringComparison.Ordinal) + 2;
            var fix = Assert.Single(tab.QuickFixesAtCaret(), f => f is { Key: "code.fix.rename", Argument: "obj_0" });
            Assert.Equal($"Change to '{Nick} (obj_0)'", tab.QuickFixTitle(fix));
            Assert.All(fix.Edits, e => Assert.Equal("obj_0", e.Text)); // the code gets the name, never the nickname
            Assert.Equal("Declare 'obj_O' as a local variable", tab.QuickFixTitle(tab.QuickFixesAtCaret().First(f => f.Key == "code.fix.declareLocal")));
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task APeekOfCodeThatBelongsToAnOwnerWithANicknameHasAHeaderWithBothNames()
    {
        await CodeIntelUiTests.WithTab(async (tab, workspace, _) =>
        {
            workspace.Nicknames.Set("scripts:probe", Nick);
            tab.Editor.Text = "probe();\n";
            tab.Editor.CaretOffset = 2;
            tab.Run("code.peekDefinition");
            await CodeIntelUiTests.Settle(() => tab.PeekText is not null);
            Dispatcher.UIThread.RunJobs();
            Assert.Contains(tab.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text == $"gml_Script_{Nick} (gml_Script_probe)");
        });
    }

    [Fact]
    public void AHistoryRenameLineReadsOldNicknameOrNameToTheNewNameAndADeleteLineShowsTheNickname()
    {
        Loc.Current.SetLanguage("en");
        var nicknames = Dragon();
        // The nickname follows the rename, so it sits on the new name once the rename is done.
        nicknames.Rekey("objects:obj_0", "objects:obj_first");
        var rename = new RenameResourceCommand("objects", "obj_0", "obj_first");
        Assert.Equal($"Rename {Nick} to obj_first", HistoryView.Describe(rename, nicknames));
        nicknames.Rekey("objects:obj_first", "objects:obj_0"); // undone: it sits on the old name again
        Assert.Equal($"Rename {Nick} to obj_first", HistoryView.Describe(rename, nicknames));
        Assert.Equal("Rename obj_0 to obj_first", HistoryView.Describe(rename)); // no nickname: the names
        Assert.Equal($"Delete Object: {Nick}", HistoryView.Describe(new ResourceListCommand("objects", ResourceListCommand.Delete, "obj_0"), nicknames));
    }

    [Fact]
    public async Task TheStringsWindowShowsTheNicknameOfAResourceNameAndTheFilterFindsItByEither()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir, objects: 2);
        var userData = new UserData(dir.File("user"));
        using var workspace = await new WorkspaceService(userData, RecentFiles.Load(userData)).OpenAsync(path, cancellationToken: TestContext.Current.CancellationToken);
        workspace.Nicknames.Set("objects:obj_0", Nick);
        await HeadlessApp.Run(() =>
        {
            var window = new StringsWindow(workspace);
            var parts = window.GetLogicalDescendants().ToList();
            var list = parts.OfType<ListBox>().Single(l => l.Name == "StringList");
            var filter = parts.OfType<TextBox>().Single(t => t.Name == "StringFilter");
            Assert.Contains(list.Items.Select(i => i?.ToString() ?? ""), r => r.EndsWith($"{Nick} (obj_0)", StringComparison.Ordinal));
            filter.Text = "dragon"; // the nickname, not in any string
            window.FlushFilter();
            Assert.Contains(list.Items.Select(i => i?.ToString() ?? ""), r => r.EndsWith($"{Nick} (obj_0)", StringComparison.Ordinal));
            filter.Text = "obj_0";
            window.FlushFilter();
            Assert.Contains(list.Items.Select(i => i?.ToString() ?? ""), r => r.EndsWith($"{Nick} (obj_0)", StringComparison.Ordinal));
            workspace.Nicknames.Set("objects:obj_0", "");
            Assert.Contains(list.Items.Select(i => i?.ToString() ?? ""), r => r.EndsWith("obj_0", StringComparison.Ordinal) && !r.Contains(Nick, StringComparison.Ordinal));
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task DialogsAndFindResultsNameTheResourceByNicknameWithTheNameBehindIt()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir, objects: 3, withCode: true);
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                await window.OpenAsync(path);
                var workspace = window.Workspaces.Current!;
                workspace.Nicknames.Set("objects:obj_0", Nick);
                Dispatcher.UIThread.RunJobs();
                window.Explorer.Select("objects:obj_0");

                var before = window.OwnedWindows.Count;
                window.Rename();
                var rename = Assert.IsType<RenameWindow>(window.OwnedWindows[before]);
                Assert.Contains(rename.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text == $"New name for {Nick} (obj_0)");
                rename.Close();

                window.DuplicateResource();
                var copy = Assert.IsType<RenameWindow>(window.OwnedWindows[before]);
                Assert.Equal($"Duplicate {Nick} (obj_0)", copy.Title);
                copy.Close();

                window.Registry.Execute("edit.findUsages");
                var usages = (G3MStudio.Panels.UsagesPanel)typeof(MainWindow).GetField("_usages", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(window)!;
                for (var wait = 0; wait < 100 && !usages.Status.Contains(Nick, StringComparison.Ordinal); wait++)
                {
                    await Task.Delay(50, TestContext.Current.CancellationToken);
                    Dispatcher.UIThread.RunJobs();
                }

                Assert.Contains($"{Nick} (obj_0)", usages.Status, StringComparison.Ordinal);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public async Task NotificationsPickersAndMenusNameAResourceByNickname()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir, objects: 3, withCode: true);
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                await window.OpenAsync(path);
                var workspace = window.Workspaces.Current!;
                workspace.Nicknames.Set("objects:obj_0", Nick);
                window.Notify(NotificationLevel.Warning, "Nothing uses 'obj_0'.");
                Assert.Contains(window.Notifications.Active, n => n.Text == $"Nothing uses '{Nick} (obj_0)'.");

                // A picker row shows the nickname and holds the real name in its tip.
                var row = Assert.IsType<TextBlock>(NicknameUi.Template(workspace.Nicknames, "objects").Build("obj_0"));
                Assert.Equal(Nick, row.Text);
                Assert.Equal($"{Nick} (obj_0)", ToolTip.GetTip(row));
                var plain = Assert.IsType<TextBlock>(NicknameUi.Template(workspace.Nicknames, "objects").Build("obj_1"));
                Assert.Equal("obj_1", plain.Text);
                Assert.Null(ToolTip.GetTip(plain));
            }
            finally { window.Close(); }
        });
    }
}
