using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.LogicalTree;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

public sealed class AssetIdTests
{
    [Fact]
    public async Task TheStatusBarShowsTheNumberOfTheOpenResourceInItsList()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir, objects: 3, withCode: true);
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                await window.OpenAsync(path);
                var label = window.FindControl<TextBlock>("AssetIdText")!;
                window.Docking.OpenTab("resource", "objects:obj_2", false);
                Dispatcher.UIThread.RunJobs();
                Assert.True(label.IsVisible);
                Assert.Equal("ID 2", label.Text);
                window.Docking.OpenTab("resource", "objects:obj_0", false);
                Dispatcher.UIThread.RunJobs();
                Assert.Equal("ID 0", label.Text);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public async Task TheInspectorHeadingOfAFolderFollowsItsRenameAndTheLanguage()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir, objects: 1);
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                await window.OpenAsync(path);
                var (organizer, id) = window.Explorer.Model.Organizer.AddFolder("Heroes", null);
                window.Explorer.Model.Organizer = organizer with { FolderView = true };
                window.Explorer.List.SelectedIndex = window.Explorer.Model.Rows.ToList().FindIndex(r => r.Kind == ExplorerRowKind.Folder);
                string Text(string name) => window.PanelContent("Inspector")!.GetLogicalDescendants().OfType<TextBlock>().First(t => t.Name == name).Text!;
                Assert.Equal(("Heroes", "Folder"), (Text("InspectorName"), Text("InspectorType")));
                window.Explorer.Model.Organizer = window.Explorer.Model.Organizer.RenameFolder(id!, "Bosses");
                Assert.Equal("Bosses", Text("InspectorName"));
                Localization.Loc.Current.SetLanguage("ru");
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(Localization.Loc.Current["inspector.kindFolder"], Text("InspectorType"));
            }
            finally
            {
                Localization.Loc.Current.SetLanguage("en");
                window.Close();
            }
        });
    }
}
