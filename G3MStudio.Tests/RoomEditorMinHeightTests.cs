using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using G3MStudio.Core.Workspace;
using G3MStudio.Editor;

namespace G3MStudio.Tests;

/// <summary>In a small window the room editor keeps a usable canvas and its toolbar never lies over other controls; the tab scrolls instead.</summary>
public sealed class RoomEditorMinHeightTests
{
    [Theory]
    [InlineData("ru", 200, 960, 540)]
    [InlineData("en", 200, 800, 450)]
    [InlineData("ru", 150, 1280, 720)]
    [InlineData("en", 125, 1536, 864)]
    public async Task TheCanvasKeepsItsMinimumAndTheToolbarStaysAboveIt(string language, int uiScale, int width, int height)
    {
        using var dir = new TempDir();
        await HeadlessApp.Run(async () =>
        {
            var window = await PanelChromeTests.Shown(dir, Corpus.Files(Corpus.Fixtures).First(), language, uiScale, width, height);
            try
            {
                var room = window.Workspaces.Current!.Data.Rooms.OrderByDescending(r => r.Layers?.Count ?? 0).First();
                window.Docking.OpenTab("resource", ResourceTypes.KeyOf("rooms", ResourceTypes.NameOf(room)), false);
                await PanelChromeTests.Settle(window);
                var preview = window.GetVisualDescendants().OfType<RoomPreview>().Single();
                Assert.True(preview.View.Bounds.Height >= RoomPreview.CanvasMinHeight - 1, $"{language} {uiScale}%: canvas is {preview.View.Bounds.Height:0} high");
                var bar = preview.GetVisualDescendants().OfType<WrapPanel>().First(p => p.Children.OfType<ComboBox>().Any(c => c.Name == "RoomTool"));
                var barBottom = bar.TranslatePoint(new Point(0, bar.Bounds.Height), preview)!.Value.Y;
                var canvasTop = preview.View.TranslatePoint(default, preview)!.Value.Y;
                Assert.True(barBottom <= canvasTop + 1, $"{language} {uiScale}%: toolbar ends at {barBottom:0}, canvas starts at {canvasTop:0}");
            }
            finally { window.Close(); }
        });
    }
}
