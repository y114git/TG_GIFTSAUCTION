using Avalonia.Controls;
using Avalonia.VisualTree;
using G3MStudio.Core.Workspace;
using G3MStudio.Editor;

namespace G3MStudio.Tests;

public sealed class ResourceLockTests
{
    private static async Task<(Workspace Workspace, UserData UserData)> Open(TempDir dir, string path)
    {
        var userData = new UserData(dir.File("user"));
        return (await new WorkspaceService(userData, RecentFiles.Load(userData)).OpenAsync(path, null, TestContext.Current.CancellationToken), userData);
    }

    [Fact]
    public async Task ALockedResourceRefusesEditsThroughTheHistoryAndAnUnlockedOneTakesThem()
    {
        using var dir = new TempDir();
        var (workspace, _) = await Open(dir, TestData.WriteSample(dir, withCode: true));
        using (workspace)
        {
            workspace.Locks.Set("code:gml_Script_probe", true);
            var refused = Assert.Throws<ResourceLockedException>(() => workspace.Edit(new EditCodeCommand("gml_Script_probe", "return 2;")));
            Assert.Contains("gml_Script_probe", refused.Message, StringComparison.Ordinal);
            Assert.Contains("Inspector", refused.Message, StringComparison.Ordinal);
            Assert.Equal(0, workspace.History.Position);
            Assert.Contains("return 1", CodeText.Decompile(workspace.Data, "gml_Script_probe"), StringComparison.Ordinal);

            Assert.Throws<ResourceLockedException>(() => workspace.Edit(new GroupCommand([new SetPropertyCommand("objects", "obj_0", "Visible", "false"), new EditCodeCommand("gml_Script_probe", "return 3;")])));
            Assert.Throws<ResourceLockedException>(() => workspace.Edit(new RenameResourceCommand("code", "gml_Script_probe", "gml_Script_other")));
            Assert.Throws<ResourceLockedException>(workspace.Locks.CheckBulk);

            workspace.Locks.Set("code:gml_Script_probe", false);
            workspace.Edit(new EditCodeCommand("gml_Script_probe", "return 2;"));
            Assert.Contains("return 2", CodeText.Decompile(workspace.Data, "gml_Script_probe"), StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task ALockedScriptAlsoCoversItsCodeEntryAndSurvivesReopeningTheFile()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir, withCode: true);
        var (first, userData) = await Open(dir, path);
        using (first) first.Locks.Set("scripts:scr_0", true);

        using var second = await new WorkspaceService(userData, RecentFiles.Load(userData)).OpenAsync(path, null, TestContext.Current.CancellationToken);
        Assert.True(second.Locks.IsLocked("scripts:scr_0"));
        Assert.True(second.Locks.Covers("gml_Script_scr_0"));
        Assert.False(second.Locks.Covers("gml_Script_probe"));
        Assert.Throws<ResourceLockedException>(() => second.Locks.Check(new EditCodeCommand("gml_Script_scr_0", "return 1;")));
    }

    [Fact]
    public async Task TheCodeEditorIsReadOnlyWhileTheEntryIsLockedAndApplyIsRefused()
    {
        using var dir = new TempDir();
        var (workspace, _) = await Open(dir, TestData.WriteSample(dir, withCode: true));
        using (workspace)
        {
            await HeadlessApp.Run(async () =>
            {
                var tab = new CodeEditorTab(workspace, "gml_Script_probe");
                var window = new Window { Content = tab, Width = 700, Height = 400 };
                window.Show();
                await tab.Ready;
                Assert.False(tab.Editor.IsReadOnly);

                tab.Editor.Text = "return 9;";
                workspace.Locks.Set("code:gml_Script_probe", true);
                Assert.True(tab.Editor.IsReadOnly);
                Assert.False(tab.Apply());
                Assert.Contains("locked", tab.GetVisualDescendants().OfType<SelectableTextBlock>().Single(t => t.Name == "Message").Text, StringComparison.Ordinal);

                workspace.Locks.Set("code:gml_Script_probe", false);
                Assert.False(tab.Editor.IsReadOnly);
                Assert.True(tab.Apply());
                window.Close();
            });
        }
    }
}
