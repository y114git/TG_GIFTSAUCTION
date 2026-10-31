using Avalonia.LogicalTree;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

public sealed class StringUseTests
{
    [Fact]
    public async Task AResourceNameStringHasAtLeastOneUseAndAnUnusedStringHasNone()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir, objects: 2);
        var userData = new UserData(dir.File("user"));
        using var workspace = await new WorkspaceService(userData, RecentFiles.Load(userData)).OpenAsync(path, cancellationToken: TestContext.Current.CancellationToken);

        var name = workspace.Data.GameObjects[0].Name;
        Assert.True(await UsageSearch.CountAsync(workspace, name, TestContext.Current.CancellationToken) >= 1);
        using var lonely = new G3MLib.DataFile.Models.GameMakerString("nobody uses this");
        Assert.Equal(0, await UsageSearch.CountAsync(workspace, lonely, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AResourceNameIsListedAsAName()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir, objects: 2);
        var userData = new UserData(dir.File("user"));
        using var workspace = await new WorkspaceService(userData, RecentFiles.Load(userData)).OpenAsync(path, cancellationToken: TestContext.Current.CancellationToken);
        await HeadlessApp.Run(() =>
        {
            var window = new StringsWindow(workspace);
            var list = window.GetLogicalDescendants().OfType<Avalonia.Controls.ListBox>().Single(l => l.Name == "StringList");
            var rows = list.Items.Select(i => i?.ToString() ?? "").ToList();
            Assert.Contains(rows, r => r.Contains("[" + Localization.Loc.Current["strings.kindName"] + "]", StringComparison.Ordinal) && r.EndsWith("obj_0", StringComparison.Ordinal));
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task TheInspectorShowsTheSelectedResourceAndItsUses()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir, objects: 2);
        var userData = new UserData(dir.File("user"));
        using var workspace = await new WorkspaceService(userData, RecentFiles.Load(userData)).OpenAsync(path, cancellationToken: TestContext.Current.CancellationToken);
        await HeadlessApp.Run(async () =>
        {
            var panel = new Panels.InspectorPanel(() => { });
            var texts = panel.GetLogicalDescendants().OfType<Avalonia.Controls.TextBlock>().ToDictionary(t => t.Name ?? "");
            Assert.Equal(Localization.Loc.Current["inspector.none"], texts["InspectorName"].Text);
            await panel.ShowAsync(workspace, "objects:obj_0");
            Assert.Equal("obj_0", texts["InspectorName"].Text);
            Assert.Equal(Localization.Loc.Current["explorer.type.objects"], texts["InspectorType"].Text);
            Assert.False(string.IsNullOrEmpty(texts["InspectorUses"].Text));
            await panel.ShowAsync(workspace, null);
            Assert.Equal(Localization.Loc.Current["inspector.none"], texts["InspectorName"].Text);
        });
    }

    [Fact]
    public async Task GmlNamesAreClassifiedFromTheBuiltinsAndTheOpenFile()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir, objects: 2);
        var userData = new UserData(dir.File("user"));
        using var workspace = await new WorkspaceService(userData, RecentFiles.Load(userData)).OpenAsync(path, cancellationToken: TestContext.Current.CancellationToken);
        var classify = Editor.GmlNames.For(workspace);
        Assert.Equal("Function", classify("show_debug_message"));
        Assert.Equal("Asset", classify("obj_0"));
        Assert.Equal("Constant", classify("pi"));
        Assert.Null(classify("my_local_variable"));
    }
}
