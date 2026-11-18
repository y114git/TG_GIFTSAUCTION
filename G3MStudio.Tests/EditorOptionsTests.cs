using G3MStudio.Editor;

namespace G3MStudio.Tests;

public sealed class EditorOptionsTests
{
    [Fact]
    public async Task CtrlWheelZoomChangesTheCodeFontSizeSettingWithinItsLimits()
    {
        using var dir = new TempDir();
        await HeadlessApp.Run(() =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                var before = window.Workspaces.Settings.Current.CodeFontSize;
                EditorOptions.Zoom(1);
                Assert.Equal(before + 1, window.Workspaces.Settings.Current.CodeFontSize);
                EditorOptions.Zoom(100);
                Assert.Equal(EditorOptions.MaxFontSize, window.Workspaces.Settings.Current.CodeFontSize);
                EditorOptions.Zoom(-100);
                Assert.Equal(EditorOptions.MinFontSize, window.Workspaces.Settings.Current.CodeFontSize);
            }
            finally { window.Close(); }

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task TheRoomGridOverrideFollowsTheSettingAndFallsBackToTheRoomsOwnGrid()
    {
        using var dir = new TempDir();
        await HeadlessApp.Run(() =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                Assert.Equal((32.0, 24.0), RoomOptions.GridFor(32, 24));
                window.Workspaces.Settings.Update(x => x with { RoomGridOverride = true, RoomGridWidth = 8, RoomGridHeight = 12 });
                Assert.Equal((8.0, 12.0), RoomOptions.GridFor(32, 24));
                window.Workspaces.Settings.Update(x => x with { RoomGridOverride = false });
                Assert.Equal((32.0, 24.0), RoomOptions.GridFor(32, 24));
            }
            finally { window.Close(); }

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task TheCompletionSwitchIsOnByDefaultIsReadFromSettingsAndFollowsAChange()
    {
        Assert.True(new G3MStudio.Core.Settings.AppSettings().CodeAutoComplete);
        using var dir = new TempDir();
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                Assert.True(EditorOptions.AutoComplete);
                window.Workspaces.Settings.Update(x => x with { CodeAutoComplete = false });
                Assert.False(EditorOptions.AutoComplete);
                await File.WriteAllTextAsync(dir.File("user/settings.json"), /*lang=json,strict*/ "{ \"codeAutoComplete\": true }", TestContext.Current.CancellationToken);
                window.Workspaces.Settings.Reload(); // the file watcher may have read it already
                Assert.True(window.Workspaces.Settings.Current.CodeAutoComplete);
            }
            finally { window.Close(); }
        });
    }
}
