using Avalonia.Media;

namespace G3MStudio.Tests;

public sealed class CanvasThemeTests
{
    [Fact]
    public async Task ACanvasColorChangedInSettingsReachesTheDrawnViewsAndAResetTakesItBack()
    {
        using var dir = new TempDir();
        await HeadlessApp.Run(() =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                var before = ((ISolidColorBrush)G3MStudio.Theme.CanvasTheme.Line).Color;
                window.Workspaces.Settings.Update(x => x with { Colors = new Dictionary<string, string>(x.Colors) { ["Canvas.Line"] = "#123456" } });
                Assert.Equal(Color.Parse("#123456"), ((ISolidColorBrush)G3MStudio.Theme.CanvasTheme.Line).Color);
                window.Workspaces.Settings.Update(x => x with { Colors = new Dictionary<string, string>() });
                Assert.Equal(before, ((ISolidColorBrush)G3MStudio.Theme.CanvasTheme.Line).Color);
            }
            finally
            {
                window.Close();
            }

            return Task.CompletedTask;
        });
    }
}
