using Avalonia.Threading;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

public sealed class ExplorerFollowsTabTests
{
    [Fact]
    public async Task ExplorerSelectionFollowsTheActiveCodeTab()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir, objects: 1, withCode: true);
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                await window.OpenAsync(path);
                var entry = window.Workspaces.Current!.Data.Code.First(c => c.ParentEntry is null).Name.Content!;
                var key = ResourceTypes.KeyOf("code", entry);
                window.Explorer.Select("objects:obj_0");
                window.Docking.OpenTab("code", key, false);
                Dispatcher.UIThread.RunJobs();

                Assert.Equal(key, window.Explorer.SelectedKey);
            }
            finally
            {
                window.Close();
            }
        });
    }
}
