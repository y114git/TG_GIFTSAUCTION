namespace G3MStudio.Tests;

/// <summary>The default dock layout keeps the editors tall: a smaller bottom dock, folded in a window shorter than 700 layout px.</summary>
public sealed class DockDefaultTests
{
    [Theory]
    [InlineData(100, 1920, 1080, false)]
    [InlineData(125, 1536, 864, true)]
    [InlineData(150, 1280, 720, true)]
    [InlineData(200, 960, 540, true)]
    public async Task TheBottomDockStartsFoldedOnlyInAShortWindow(int uiScale, int width, int height, bool folded)
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir);
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                window.Width = width;
                window.Height = height;
                window.Workspaces.Settings.Update(x => x with { UiScale = uiScale });
                await window.OpenAsync(path);
                Assert.Equal(folded, window.Docking.IsFolded("bottom"));
                Assert.False(window.Docking.IsFolded("left"));
                if (!folded) Assert.True(window.Docking.Find<Dock.Model.Controls.IToolDock>("BottomDock")!.Proportion <= 0.2, "the bottom dock takes at most a fifth");
            }
            finally { window.Close(); }
        });
    }
}
