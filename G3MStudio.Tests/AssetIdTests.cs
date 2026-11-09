using Avalonia.Controls;
using Avalonia.Threading;

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
}
