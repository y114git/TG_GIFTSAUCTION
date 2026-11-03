using Avalonia.Threading;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

public sealed class FileCommandsTests
{
    private static async Task Settle(Func<bool> done)
    {
        for (var i = 0; i < 300 && !done(); i++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(10);
        }

        Assert.True(done());
    }

    [Fact]
    public async Task SaveACopyWritesTheEditsToANewFileAndStaysOnTheOldOne()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir, objects: 2);
        var copy = dir.File("copy.win");
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                await window.OpenAsync(path);
                var original = await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken);
                window.Workspaces.Current!.Edit(new RenameResourceCommand("objects", "obj_0", "obj_a"));

                Assert.True(await window.SaveToAsync(copy, move: false));
                Assert.Equal(path, window.Workspaces.Current.Path);
                Assert.True(window.Workspaces.Current.IsDirty);
                Assert.Equal(original, await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
                using var saved = await G3MLib.DataFile.GameMakerIO.ReadAsync(copy, cancellationToken: TestContext.Current.CancellationToken);
                Assert.Equal("obj_a", saved.GameObjects[0].Name.Content);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public async Task SaveAsMovesTheWorkspaceToTheNewFileAndLeavesTheOldOneAlone()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir, objects: 2);
        var target = dir.File("moved.win");
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                await window.OpenAsync(path);
                var original = await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken);
                window.Workspaces.Current!.Edit(new RenameResourceCommand("objects", "obj_0", "obj_a"));

                Assert.True(await window.SaveToAsync(target, move: true));
                await Settle(() => window.Workspaces.Current?.Path == Path.GetFullPath(target));
                Assert.False(window.Workspaces.Current!.IsDirty);
                Assert.Equal("obj_a", window.Workspaces.Current.Data.GameObjects[0].Name.Content);
                Assert.Equal(original, await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));

                // The old file has no unsaved edits waiting for it: opening it again recovers nothing.
                window.Workspaces.Close();
                await window.OpenAsync(path);
                Assert.Equal("obj_0", window.Workspaces.Current!.Data.GameObjects[0].Name.Content);
                Assert.False(window.Workspaces.Current.IsDirty);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public async Task AboutShowsTheVersionAndTheLibrary()
    {
        using var dir = new TempDir();
        await HeadlessApp.Run(() =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                window.Registry.Execute("help.about");
                Dispatcher.UIThread.RunJobs();
                var text = AboutWindow.VersionInfo();
                Assert.StartsWith("G3MStudio ", text, StringComparison.Ordinal);
                Assert.Contains("G3MLib 1.1", text, StringComparison.Ordinal);
                Assert.Contains("GPL-3.0-only", text, StringComparison.Ordinal);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public async Task DocumentationOpensTheAddressAndSaysSoWhenNoBrowserStarts()
    {
        using var dir = new TempDir();
        await HeadlessApp.Run(() =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            var opened = new List<string>();
            try
            {
                MainWindow.UrlOpener = url => { opened.Add(url); return true; };
                window.Registry.Execute("help.documentation");
                Assert.Equal([MainWindow.DocumentationUrl], opened);
                Assert.Empty(window.Notifications.Active);

                MainWindow.UrlOpener = _ => false;
                window.Registry.Execute("help.documentation");
                Assert.Contains(MainWindow.DocumentationUrl, window.Notifications.Active.Single().Text, StringComparison.Ordinal);
            }
            finally
            {
                MainWindow.UrlOpener = _ => true;
                window.Close();
            }
        });
    }
}
