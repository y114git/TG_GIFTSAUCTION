using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using G3MLib.DataFile;
using G3MLib.DataFile.Models;
using G3MStudio.Core.Workspace;
using G3MStudio.Editor;

#pragma warning disable CA2000 // the data model owns what is added to it

namespace G3MStudio.Tests;

/// <summary>Image editor toolbar and canvas visuals: tool icons, the brush footprint overlay, the Apply menu, the go-to-original button and the status line mark.</summary>
public sealed class ImageToolbarTests
{
    private static readonly Rgba Red = new(255, 0, 0, 255);

    // The pixels of the 24 x 24 icon box the path covers, sampled at pixel centres.
    private static HashSet<(int X, int Y)> Cover(Geometry geometry) =>
        [.. Enumerable.Range(0, 24 * 24).Select(i => (X: i % 24, Y: i / 24)).Where(p => geometry.FillContains(new Point(p.X + 0.5, p.Y + 0.5)))];

    private static readonly string[] Keys = ["sprites:spr_a", "tileSets:ts_grid"];

    [Fact]
    public async Task TheLassoIconIsAnOutlinedLoopAndTheWandStarSitsOnTheAxisOfTheWand()
    {
        await HeadlessApp.Run(() =>
        {
            var lasso = Cover(Geometry.Parse(ImageTools.Icon(ImageTool.Lasso)));
            Assert.Contains((12, 4), lasso); // the ring
            Assert.Contains((4, 9), lasso);
            for (var y = 0; y < 24; y++)
                for (var x = 0; x < 24; x++)
                    if (Math.Pow((x + 0.5 - 12) / 6, 2) + Math.Pow((y + 0.5 - 9.5) / 3.5, 2) < 1) Assert.DoesNotContain((x, y), lasso);

            var star = Geometry.Parse(ImageTools.WandStar).Bounds;
            Assert.Equal((13, 10), (star.Center.X, star.Center.Y));
            Assert.Contains((13, 10), Cover(Geometry.Parse(ImageTools.Icon(ImageTool.Wand))));
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task TheBrushOverlayIsTheSetOfPixelsAStampPaintsForEveryShapeSizeAndMirror()
    {
        await HeadlessApp.Run(() =>
        {
            foreach (var shape in Enum.GetValues<BrushShape>())
                for (var size = 1; size <= 9; size++)
                    foreach (var mirror in new[] { SymmetryMode.None, SymmetryMode.Horizontal | SymmetryMode.Vertical })
                    {
                        using var canvas = new ImageCanvas(new ImageState(new PixelBuffer(40, 40))) { Tool = ImageTool.Pencil, BrushSize = size, Shape = shape, Symmetry = mirror };
                        var painted = new PixelBuffer(40, 40);
                        foreach (var copy in Symmetry.Copies([(13, 11)], 40, 40, mirror)) painted.Stamp(copy[0].X, copy[0].Y, size, Red, shape);
                        var expected = Enumerable.Range(0, 40 * 40).Where(i => painted.Get(i % 40, i / 40).A != 0).Select(i => (i % 40, i / 40)).ToHashSet();
                        Assert.Equal(expected, canvas.Footprint((13, 11)));
                    }

            using var other = new ImageCanvas(new ImageState(new PixelBuffer(40, 40))) { Tool = ImageTool.Fill, BrushSize = 5 };
            Assert.Equal([(3, 4)], other.Footprint((3, 4))); // a fill starts at one pixel
            Assert.Empty(other.Footprint((99, 4))); // outside the picture nothing is touched
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task ASmoothBrushOverlayIsThePixelsTheSmoothDabPaintsAndALockedLayerHasNone()
    {
        await HeadlessApp.Run(() =>
        {
            for (var size = 1; size <= 9; size++)
            {
                using var canvas = new ImageCanvas(new ImageState(new PixelBuffer(40, 40))) { Tool = ImageTool.Pencil, BrushSize = size, Smooth = true };
                var painted = new PixelBuffer(40, 40);
                new SmoothBrush(painted, Red, size).Segment(13.5, 11.5, 13.5, 11.5);
                var expected = Enumerable.Range(0, 40 * 40).Where(i => painted.Get(i % 40, i / 40).A != 0).Select(i => (i % 40, i / 40)).ToHashSet();
                Assert.Equal(expected, canvas.Footprint((13, 11)));
            }

            using var locked = new ImageCanvas(new ImageState(new PixelBuffer(40, 40))) { Tool = ImageTool.Pencil, BrushSize = 3, Locked = true };
            Assert.Empty(locked.Footprint((13, 11))); // painting is refused, so nothing is outlined
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task ApplyHasAnArrowMenuAndTheUnsavedMarkSitsInTheStatusLine()
    {
        await HeadlessApp.Run(() =>
        {
            var session = new TargetImageSession(new PageTarget("Texture 0"), new PixelBuffer(16, 16));
            using var view = new ImageEditorView(session);
            var window = new Window { Content = view, Width = 1200, Height = 700 };
            window.Show();
            try
            {
                var all = view.GetVisualDescendants().OfType<Control>().ToList();
                Assert.DoesNotContain(all, c => c.Name is "ImageSaveNew" or "ImageRound");
                var more = all.OfType<Button>().Single(b => b.Name == "ImageApplyMore");
                var menu = Assert.IsType<MenuFlyout>(more.Flyout);
                var items = menu.Items.OfType<MenuItem>().ToList();
                Assert.Equal(["New sprite from this frame...", "New sprite from all frames..."], items.Select(i => i.Header as string));
                var discard = all.OfType<Button>().Single(b => b.Name == "ImageDiscard"); // its own button right after Apply
                Assert.Equal("Discard", discard.Content);
                Assert.False(discard.IsEnabled); // nothing to discard yet
                var apply = all.OfType<Button>().Single(b => b.Name == "ImageApply");
                Assert.Same(apply.GetVisualParent()!.GetVisualParent(), discard.GetVisualParent());
                Assert.True(discard.TranslatePoint(default, window)!.Value.X > more.TranslatePoint(default, window)!.Value.X);
                var mark = all.OfType<TextBlock>().Single(t => t.Name == "ImagePending");
                Assert.False(mark.IsVisible);
                Assert.Same(all.OfType<TextBlock>().Single(t => t.Name == "ImageStatus").GetVisualParent(), mark.GetVisualParent()); // same line as the frame count and zoom
                view.Canvas.Run(new ImageOp(ImageOps.Stroke, "1", "0", "#FF0000", "1,1 5,1"));
                Assert.True(mark.IsVisible);
                Assert.True(discard.IsEnabled);
                var sent = new List<int>();
                view.SaveAsNewRequested += pictures => sent.Add(pictures.Count);
                items[0].RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(MenuItem.ClickEvent));
                Assert.Equal([1], sent);
                Assert.True(view.Run("img.discard"));
                Assert.False(mark.IsVisible);
            }
            finally
            {
                window.Close();
            }

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task TheGoToOriginalButtonOpensTheTabOfTheSpriteAndOfTheTileSet()
    {
        using var dir = new TempDir();
        using (var data = CopyResourcesTests.Source())
        {
            var item = new GameMakerTexturePageItem { TexturePage = data.EmbeddedTextures[0], SourceX = 64, SourceY = 64, SourceWidth = 16, SourceHeight = 16, TargetWidth = 16, TargetHeight = 16 };
            data.Backgrounds.Add(new GameMakerBackground { Name = data.Strings.MakeString("ts_grid", out _), GMS2TileWidth = 16, GMS2TileHeight = 16, GMS2TileColumns = 1, GMS2TileCount = 1, GMS2FrameLength = 1, GMS2TileIds = [new GameMakerBackground.TileID { ID = 0 }], Texture = item });
            data.TexturePageItems.Add(item);
            TestData.Write(dir.File("data.win"), data);
        }

        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                await window.OpenAsync(dir.File("data.win"));
                foreach (var key in Keys)
                {
                    window.EditImageKey(key);
                    Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                    window.UpdateLayout();
                    var tab = Assert.Single(window.Docking.Tabs.Tabs, t => t.Kind == "image" && t.Key == key);
                    var view = window.GetVisualDescendants().OfType<ImageEditorView>().Single(v => v.IsEffectivelyVisible);
                    var button = view.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "ImageOriginal");
                    Assert.True(button.IsVisible);
                    button.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                    Assert.Contains(window.Docking.Tabs.Tabs, t => t.Kind == "resource" && t.Key == key);
                    Assert.NotNull(tab);
                    window.Docking.CloseTab(tab.Id);
                }
            }
            finally { window.Close(); }
        });
    }
}
