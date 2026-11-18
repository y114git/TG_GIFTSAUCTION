using Avalonia;
using Avalonia.Threading;
using G3MStudio.Core.Settings;

namespace G3MStudio.Tests;

public sealed class WindowPlacementTests
{
    [Fact]
    public async Task ClosingTheWindowRemembersItsPlaceAndTheSwitchTurnsThatOff()
    {
        using var dir = new TempDir();
        PixelPoint? seen = null;
        WindowPlacement? saved = null;
        await HeadlessApp.Run(() =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            window.Position = new PixelPoint(120, 80);
            window.Width = 1111;
            window.Height = 777;
            Dispatcher.UIThread.RunJobs();
            seen = window.Position;
            var settings = window.Workspaces.Settings;
            window.Close();
            saved = settings.Current.Window;
            return Task.CompletedTask;
        });
        Assert.NotNull(saved);
        Assert.Equal(seen!.Value.X, saved.X);
        Assert.Equal(1111, saved.Width);
        Assert.Equal(777, saved.Height);
        Assert.False(saved.Maximized);

        using var other = new TempDir();
        WindowPlacement? off = null;
        await HeadlessApp.Run(() =>
        {
            var window = TabsWorkspaceTests.NewWindow(other);
            window.Workspaces.Settings.Update(x => x with { RememberWindow = false });
            window.Close();
            off = window.Workspaces.Settings.Current.Window;
            return Task.CompletedTask;
        });
        Assert.Null(off);
    }
}
