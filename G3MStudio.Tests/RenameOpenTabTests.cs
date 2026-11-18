using Avalonia.Threading;
using Avalonia.VisualTree;
using G3MStudio.Core.Workspace;
using G3MStudio.Editor;

namespace G3MStudio.Tests;

public sealed class RenameOpenTabTests
{
    [Fact]
    public async Task AnOpenTabShowsTheResourceUnderItsNewNameAfterARenameAndAfterUndo()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir, objects: 3, withCode: true);
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                await window.OpenAsync(path);
                window.Docking.OpenTab("resource", "objects:obj_0", false);
                Dispatcher.UIThread.RunJobs();
                var history = window.Workspaces.Current!.History;

                history.Execute(new RenameResourceCommand("objects", "obj_0", "obj_first"));
                Dispatcher.UIThread.RunJobs();
                Assert.Equal("objects:obj_first", Assert.Single(window.Docking.Tabs.Tabs).Key);
                Assert.All(Grids(window), g => Assert.Equal("", g.Message));

                window.Registry.Execute("edit.undo");
                Dispatcher.UIThread.RunJobs();
                Assert.All(Grids(window), g => Assert.Equal("", g.Message));
            }
            finally { window.Close(); }
        });
    }

    private static List<PropertyGridTab> Grids(MainWindow window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        return [.. window.GetVisualDescendants().OfType<PropertyGridTab>()];
    }
}
