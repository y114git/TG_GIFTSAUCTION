using G3MLib.DataFile;
using Avalonia.Controls;
using Avalonia.VisualTree;
using G3MLib.DataFile.Compiler;
using G3MStudio.Core.Workspace;
using G3MStudio.Editor;

namespace G3MStudio.Tests;

public sealed class ParentCodeTests
{
    private const string Lib = "gml_GlobalScript_lib";

    // A global script holding two functions: its children are gml_Script_foo and gml_Script_bar, which cannot be decompiled on their own.
    private static string WriteLibrary(TempDir dir)
    {
        using var data = TestData.Create(1, true);
        var group = new CodeImportGroup(data);
        group.QueueReplace(Lib, "function foo() { return 1; }\nfunction bar() { return foo() + 2; }\n");
        group.Import();
        return TestData.Write(dir.File("lib.win"), data);
    }

    // Go to Parent asks the window for the parent's own tab; the test opens that tab the way the window does.
    private static async Task<CodeEditorTab> GoToParent(CodeEditorTab child, Workspace workspace)
    {
        string? requested = null;
        child.GoToRequested += key => requested = key;
        child.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "ShowParent").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Assert.Equal("code:" + Lib, requested);
        var parent = new CodeEditorTab(workspace, Lib);
        new Window { Content = parent, Width = 700, Height = 400 }.Show();
        await parent.Ready;
        return parent;
    }

    [Fact]
    public async Task ApplyOnTheParentViewCompilesIntoTheParentKeepsTheChildrenConsistentAndSavesAndUndoes()
    {
        using var dir = new TempDir();
        var path = WriteLibrary(dir);
        var userData = new UserData(dir.File("user"));
        var workspace = await new WorkspaceService(userData, RecentFiles.Load(userData)).OpenAsync(path, null, TestContext.Current.CancellationToken);
        using (workspace)
        {
            var original = CodeText.Decompile(workspace.Data, Lib);
            await HeadlessApp.Run(async () =>
            {
                var child = new CodeEditorTab(workspace, "gml_Script_foo");
                var window = new Window { Content = child, Width = 700, Height = 400 };
                window.Show();
                await child.Ready;
                Assert.Equal(CodeMode.Assembly, child.Mode);
                var tab = await GoToParent(child, workspace);
                Assert.False(tab.Editor.IsReadOnly);
                Assert.Equal(original, tab.Text);

                tab.Editor.Text = "function foo() { return 10; }\nfunction bar() { return foo() + 20; }\nfunction baz() { return 3; }\n";
                Assert.True(tab.HasPending);
                Assert.True(tab.Apply());
                Assert.False(tab.HasPending);
                Assert.Empty(workspace.Problems.All);

                // The parent holds the new text, its children follow, and the child entry that was open is still there.
                var parent = workspace.Data.Code.ByName(Lib)!;
                Assert.Equal(["gml_Script_foo", "gml_Script_bar", "gml_Script_baz"], parent.ChildEntries.Select(c => c.Name!.Content));
                Assert.All(parent.ChildEntries, c => Assert.Same(parent, c.ParentEntry));
                Assert.Contains("return foo() + 20", CodeText.Decompile(workspace.Data, Lib), StringComparison.Ordinal);

                // The file still writes and reads back with the edit in it.
                var saved = TestData.Write(dir.File("saved.win"), workspace.Data);
                var reopened = await new WorkspaceService(new UserData(dir.File("user2")), RecentFiles.Load(userData)).OpenAsync(saved, null, TestContext.Current.CancellationToken);
                using (reopened) Assert.Contains("return foo() + 20", CodeText.Decompile(reopened.Data, Lib), StringComparison.Ordinal);

                workspace.History.Undo();
                Assert.Equal(original, CodeText.Decompile(workspace.Data, Lib));
                await tab.Ready;
                Assert.Equal(CodeMode.Decompiled, tab.Mode);
                window.Close();
            });
        }
    }

    [Fact]
    public async Task ABrokenParentTextShowsTheProblemsAgainstTheParentAndChangesNothing()
    {
        using var dir = new TempDir();
        var path = WriteLibrary(dir);
        var userData = new UserData(dir.File("user"));
        using var workspace = await new WorkspaceService(userData, RecentFiles.Load(userData)).OpenAsync(path, null, TestContext.Current.CancellationToken);
        var original = CodeText.Decompile(workspace.Data, Lib);
        await HeadlessApp.Run(async () =>
        {
            var child = new CodeEditorTab(workspace, "gml_Script_foo");
            var window = new Window { Content = child, Width = 700, Height = 400 };
            window.Show();
            await child.Ready;
            var tab = await GoToParent(child, workspace);
            tab.Editor.Text = "function foo( { return ; ";
            Assert.False(tab.Apply());
            Assert.NotEmpty(workspace.Problems.All);
            Assert.All(workspace.Problems.All, p => Assert.Equal(Lib, p.Source));
            Assert.Equal(original, CodeText.Decompile(workspace.Data, Lib));
            Assert.Equal(0, workspace.History.Position);
            window.Close();
        });
    }

    [Fact]
    public async Task ALockOnTheParentMakesTheParentViewReadOnly()
    {
        using var dir = new TempDir();
        var path = WriteLibrary(dir);
        var userData = new UserData(dir.File("user"));
        using var workspace = await new WorkspaceService(userData, RecentFiles.Load(userData)).OpenAsync(path, null, TestContext.Current.CancellationToken);
        await HeadlessApp.Run(async () =>
        {
            var child = new CodeEditorTab(workspace, "gml_Script_foo");
            var window = new Window { Content = child, Width = 700, Height = 400 };
            window.Show();
            await child.Ready;
            var tab = await GoToParent(child, workspace);
            Assert.False(tab.Editor.IsReadOnly);
            workspace.Locks.Set("code:" + Lib, true);
            Assert.True(tab.Editor.IsReadOnly);
            window.Close();
        });
    }

    [Fact]
    public async Task GoToParentThenShowAssemblyShowsTheParentAssemblyNotTheChilds()
    {
        using var dir = new TempDir();
        var path = WriteLibrary(dir);
        var userData = new UserData(dir.File("user"));
        using var workspace = await new WorkspaceService(userData, RecentFiles.Load(userData)).OpenAsync(path, null, TestContext.Current.CancellationToken);
        var childAssembly = CodeText.Disassemble(workspace.Data, "gml_Script_foo");
        var parentAssembly = CodeText.Disassemble(workspace.Data, Lib);
        await HeadlessApp.Run(async () =>
        {
            var child = new CodeEditorTab(workspace, "gml_Script_foo");
            var window = new Window { Content = child, Width = 700, Height = 400 };
            window.Show();
            await child.Ready;
            var tab = await GoToParent(child, workspace);
            tab.ShowAssembly = true;
            await tab.Ready;
            Assert.NotEqual(childAssembly, parentAssembly);
            Assert.Equal(parentAssembly, tab.Text);
            window.Close();
        });
    }

    [Fact]
    public async Task SwitchToAssemblySavesPendingTextFirstAndAFailedSaveKeepsTheView()
    {
        using var dir = new TempDir();
        var path = WriteLibrary(dir);
        var userData = new UserData(dir.File("user"));
        using var workspace = await new WorkspaceService(userData, RecentFiles.Load(userData)).OpenAsync(path, null, TestContext.Current.CancellationToken);
        await HeadlessApp.Run(async () =>
        {
            var tab = new CodeEditorTab(workspace, Lib);
            var window = new Window { Content = tab, Width = 700, Height = 400 };
            window.Show();
            await tab.Ready;
            var toggle = tab.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "AssemblyToggle");
            Assert.Equal("Switch to assembly", toggle.Content);

            tab.Editor.Text = "function foo( { return ; ";
            toggle.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Assert.False(tab.ShowAssembly);
            Assert.True(tab.HasPending);

            tab.Editor.Text = "function foo() { return 5; }\n";
            toggle.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            await tab.Ready;
            Assert.True(tab.ShowAssembly);
            Assert.Contains("return 5", CodeText.Decompile(workspace.Data, Lib), StringComparison.Ordinal);
            Assert.Equal(CodeText.Disassemble(workspace.Data, Lib), tab.Text);
            Assert.Equal("Switch to GML", toggle.Content);
            window.Close();
        });
    }
}
