using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using G3MStudio.Core.Workspace;
using G3MStudio.Editor;
using G3MStudio.History;

namespace G3MStudio.Tests;

public sealed class EditorFlowTests
{
    private static async Task<Workspace> Open(TempDir dir, bool withCode = true, int objects = 2)
    {
        var path = TestData.WriteSample(dir, objects: objects, withCode: withCode);
        var userData = new UserData(dir.File("user"));
        return await new WorkspaceService(userData, RecentFiles.Load(userData)).OpenAsync(path);
    }

    private static async Task Settle(Func<bool> done)
    {
        for (var i = 0; i < 300 && !done(); i++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(10);
        }

        Assert.True(done(), "did not settle");
    }

    [Fact]
    public async Task TypedCodeAppliesAsOneUndoableEditAndUndoShowsTheOldCode()
    {
        using var dir = new TempDir();
        using var workspace = await Open(dir);
        await HeadlessApp.Run(async () =>
        {
            var tab = new CodeEditorTab(workspace, "gml_Script_probe");
            var window = new Window { Content = tab, Width = 700, Height = 400 };
            window.Show();
            await tab.Ready;
            Assert.Contains("return 1", tab.Text, StringComparison.Ordinal);
            Assert.False(tab.HasPending);

            tab.Editor.Text = "return 2;";
            Assert.True(tab.HasPending);
            Assert.True(tab.Apply());
            Assert.False(tab.HasPending);
            Assert.Equal(["code"], workspace.History.Items.Select(c => c.Editor));
            Assert.Contains("return 2", CodeText.Decompile(workspace.Data, "gml_Script_probe"), StringComparison.Ordinal);
            Assert.Equal("return 2;", tab.Text);

            workspace.Undo();
            await Settle(() => tab.Text.Contains("return 1", StringComparison.Ordinal));
            workspace.Redo();
            await Settle(() => tab.Text.Contains("return 2", StringComparison.Ordinal));
            window.Close();
        });
    }

    [Fact]
    public async Task CodeThatDoesNotCompileIsRefusedWithAMessageAndChangesNothing()
    {
        using var dir = new TempDir();
        using var workspace = await Open(dir);
        await HeadlessApp.Run(async () =>
        {
            var tab = new CodeEditorTab(workspace, "gml_Script_probe");
            var window = new Window { Content = tab };
            window.Show();
            await tab.Ready;
            var before = TestData.Bytes(workspace.Data);
            tab.Editor.Text = "return (";
            Assert.False(tab.Apply());
            Assert.True(tab.HasPending);
            Assert.Empty(workspace.History.Items);
            Assert.Equal(before, TestData.Bytes(workspace.Data));
            Assert.NotEmpty(tab.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "Message").Text!);

            var problem = Assert.Single(workspace.Problems.All);
            Assert.Equal("gml_Script_probe", problem.Source);
            Assert.True(problem.Line >= 1 && problem.Column >= 1);
            Assert.StartsWith("GML", problem.Code, StringComparison.Ordinal);

            tab.Editor.Text = "return 2;";
            Assert.True(tab.Apply());
            Assert.Empty(workspace.Problems.All);

            tab.Editor.Text = "return (";
            Assert.False(tab.Apply());
            tab.Revert();
            await tab.Ready;
            Assert.False(tab.HasPending);
            Assert.Contains("return 2", tab.Text, StringComparison.Ordinal);
            window.Close();
        });
    }

    [Fact]
    public async Task AMissingEntryShowsAMessageInsteadOfCode()
    {
        using var dir = new TempDir();
        using var workspace = await Open(dir);
        await HeadlessApp.Run(async () =>
        {
            var tab = new CodeEditorTab(workspace, "gml_Script_gone");
            var window = new Window { Content = tab };
            window.Show();
            await tab.Ready;
            Assert.Contains("gml_Script_gone", tab.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "Message").Text, StringComparison.Ordinal);
            Assert.True(tab.Editor.IsReadOnly);
            window.Close();
        });
    }

    [Fact]
    public async Task TheCodeEntryOpensFromTheExplorerTypingKeepsTheTabAndSaveAppliesFirst()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir, withCode: true);
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                await window.OpenAsync(path);
                window.Explorer.Model.Toggle("code");
                window.Explorer.RequestOpen(window.Explorer.Model.Rows.Single(r => r.IsItem && r.Key == "code" && r.Text == "gml_Script_probe"), true);
                Assert.True(window.Docking.Tabs.Tabs.Single().Preview);
                Dispatcher.UIThread.RunJobs();
                var tab = window.GetVisualDescendants().OfType<CodeEditorTab>().Single();
                await tab.Ready;

                tab.Editor.Text = "return 7;";
                Assert.False(window.Docking.Tabs.Tabs.Single().Preview);
                Assert.True(window.Docking.Documents().Single().IsModified);

                Assert.True(window.Registry.Execute("file.save"));
                await Settle(() => window.Notifications.History.Any(n => n.Text.StartsWith("Saved", StringComparison.Ordinal)));
                Assert.Single(window.Workspaces.Current!.History.Items);
                Assert.False(window.Workspaces.Current.IsDirty);
                Assert.False(window.Docking.Documents().Single().IsModified);
                using var saved = await G3MLib.DataFile.GameMakerIO.ReadAsync(File.OpenRead(path), cancellationToken: TestContext.Current.CancellationToken);
                Assert.Contains("return 7", CodeText.Decompile(saved, "gml_Script_probe"), StringComparison.Ordinal);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task ClosingACodeTabThatDoesNotCompileKeepsItOpen()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir, withCode: true);
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                await window.OpenAsync(path);
                window.Docking.OpenTab("code", "code:gml_Script_probe", false);
                Dispatcher.UIThread.RunJobs();
                var tab = window.GetVisualDescendants().OfType<CodeEditorTab>().Single();
                await tab.Ready;
                tab.Editor.Text = "return (";
                window.Registry.Execute("tabs.close");
                Assert.Single(window.Docking.Tabs.Tabs);
                tab.Editor.Text = "return 3;";
                window.Registry.Execute("tabs.close");
                Assert.Empty(window.Docking.Tabs.Tabs);
                Assert.Single(window.Workspaces.Current!.History.Items);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task HistoryListShowsTheStackNewestFirstAndAClickMovesToThatPoint()
    {
        using var dir = new TempDir();
        using var workspace = await Open(dir, withCode: false, objects: 2);
        await HeadlessApp.Run(() =>
        {
            var view = new HistoryView();
            view.Bind(workspace);
            workspace.Edit(new RenameResourceCommand("objects", "obj_0", "obj_a"));
            workspace.Edit(new RenameResourceCommand("objects", "obj_1", "obj_b"));
            Assert.Equal([2, 1, 0], view.Rows.Select(r => r.Position));
            Assert.EndsWith("Rename obj_1 to obj_b", view.Rows[0].Text, StringComparison.Ordinal);
            Assert.All(view.Rows, r => Assert.True(r.Applied));

            view.GoTo(view.Rows[2]);
            Assert.Equal(["obj_0", "obj_1"], workspace.Data.GameObjects.Select(o => o.Name.Content));
            Assert.Equal([false, false, true], view.Rows.Select(r => r.Applied));
            view.GoTo(view.Rows[1]);
            Assert.Equal(["obj_a", "obj_1"], workspace.Data.GameObjects.Select(o => o.Name.Content));
        });
    }

    [Fact]
    public async Task RenameDialogRenamesRefusesATakenNameAndTheOpenTabFollows()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir, objects: 2);
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                await window.OpenAsync(path);
                var row = window.Explorer.Model.Rows.Single(r => r.IsItem && r.Key == "objects" && r.Text == "obj_0");
                window.Explorer.RequestOpen(row, false);
                window.Explorer.Select("objects:obj_0");
                Assert.True(window.Registry.Execute("edit.rename"));
                Dispatcher.UIThread.RunJobs();
                var dialog = window.OwnedWindows.OfType<RenameWindow>().Single();
                var name = dialog.GetVisualDescendants().OfType<TextBox>().Single();
                var ok = dialog.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "Rename");

                Assert.False(ok.IsEnabled);
                name.Text = "obj_1";
                Assert.True(ok.IsEnabled);
                ok.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                Assert.NotEmpty(dialog.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "Error").Text!);
                Assert.Empty(window.Workspaces.Current!.History.Items);

                name.Text = "obj_player";
                ok.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                Dispatcher.UIThread.RunJobs();
                Assert.Empty(window.OwnedWindows);
                Assert.Equal(["obj_player", "obj_1"], window.Workspaces.Current.Data.GameObjects.Select(o => o.Name.Content));
                Assert.Equal("objects:obj_player", window.Docking.Tabs.Tabs.Single().Key);
                Assert.Equal("obj_player", window.Docking.Documents().Single().Title);

                window.Registry.Execute("edit.undo");
                Assert.Equal("objects:obj_0", window.Docking.Tabs.Tabs.Single().Key);
            }
            finally
            {
                window.Close();
            }
        });
    }
}
