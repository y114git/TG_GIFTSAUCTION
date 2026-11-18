using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;
using G3MStudio.Core.Workspace;
using G3MStudio.Editor;

namespace G3MStudio.Tests;

/// <summary>In the room editor the instance list panel and the layer rows keep a gap to their borders, at every interface scale.</summary>
public sealed class RoomPanelGapTests
{
    [Theory]
    [InlineData("en", 100, 1920, 1080)]
    [InlineData("ru", 200, 960, 540)]
    public async Task TheListControlsAndTheLayerButtonsKeepAGapToTheirBorders(string language, int uiScale, int width, int height)
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
                var panel = preview.GetVisualDescendants().OfType<DockPanel>().First(p => p.Children.OfType<TextBox>().Any(t => t.Name == "RoomElementSearch"));
                var grid = (Grid)panel.Parent!;
                var splitter = grid.Children.OfType<GridSplitter>().First(g => Grid.GetColumn(g) == Grid.GetColumn(panel) - 1);
                var edge = splitter.TranslatePoint(new Point(splitter.Bounds.Width, 0), window)!.Value.X; // the left edge of the panel's column
                Assert.All(ListNames, name => Assert.True(Left(preview, name, window) - edge >= 4, $"{language} {uiScale}%: {name} touches the panel edge"));
                Assert.True(window.GetVisualDescendants().OfType<TextBox>().First(t => t.Name == "RoomElementSearch").Bounds.Width <= 200);

                var list = window.GetVisualDescendants().OfType<ListBox>().First(l => l.Name == "RoomLayers");
                var listLeft = list.TranslatePoint(default, window)!.Value.X + list.BorderThickness.Left;
                var rows = list.GetVisualDescendants().OfType<ToggleButton>().ToList();
                Assert.NotEmpty(rows);
                Assert.All(rows, b => Assert.True(b.TranslatePoint(default, window)!.Value.X - listLeft >= 2, $"{language} {uiScale}%: a layer button touches the list border"));
                var eyes = rows.Where((_, i) => i % 2 == 0).Select(b => b.TranslatePoint(default, window)!.Value.Y).Order().ToList();
                if (eyes.Count > 1) Assert.True(eyes[1] - eyes[0] >= rows[0].Bounds.Height + 2, $"{language} {uiScale}%: layer buttons of two rows touch");
            }
            finally { window.Close(); }
        });
    }

    private static readonly string[] ListNames = ["ElementShowAll", "RoomElementSearch"];

    private static double Left(Visual root, string name, Visual relativeTo) =>
        root.GetVisualDescendants().OfType<Control>().First(c => c.Name == name).TranslatePoint(default, relativeTo)!.Value.X;
}
