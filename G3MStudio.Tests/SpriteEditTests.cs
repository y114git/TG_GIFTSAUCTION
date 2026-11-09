using Avalonia.VisualTree;
using Avalonia.LogicalTree;
using G3MLib.DataFile;
using G3MLib.DataFile.Models;
using G3MStudio.Core.Workspace;
using G3MStudio.Editor;
using G3MStudio.Rooms;

#pragma warning disable CA2000 // the data model owns the sprites it holds

namespace G3MStudio.Tests;

public sealed class SpriteEditTests
{
    private static GameMakerSprite NewSprite(GameMakerData data)
    {
        TestData.AddPage(data);
        return SpriteTemplate.Create(data, "spr_edit");
    }

    [Fact]
    public void AutoBoxHoldsEverySolidPixelOfEveryFrame()
    {
        Func<int, int, bool> a = (x, y) => x == 3 && y == 4, b = (x, y) => x == 9 && y == 2;
        Assert.Equal(new MaskBox(3, 2, 9, 4), SpriteMask.Auto([a, b], 16, 16));
        Assert.Equal(new MaskBox(0, 0, 15, 15), SpriteMask.Auto([(x, y) => false], 16, 16));
    }

    [Fact]
    public void ShapesFillTheBoxAsTheirNameSays()
    {
        var area = new MaskBox(0, 0, 7, 7);
        var rect = SpriteMask.Bits(MaskShape.Rectangle, (_, _) => false, new MaskBox(2, 2, 5, 5), area);
        Assert.Equal(16, rect.Sum(v => System.Numerics.BitOperations.PopCount(v)));
        var diamond = SpriteMask.Bits(MaskShape.Diamond, (_, _) => false, area, area);
        Assert.Equal(0, diamond[0] & 0x80);
        Assert.NotEqual(0, diamond[3] & 0x10);
        var ellipse = SpriteMask.Bits(MaskShape.Ellipse, (_, _) => false, area, area);
        Assert.True(ellipse.Sum(v => System.Numerics.BitOperations.PopCount(v)) is > 32 and < 64);
        var precise = SpriteMask.Bits(MaskShape.Precise, (x, y) => x == y, area, area);
        Assert.Equal(8, precise.Sum(v => System.Numerics.BitOperations.PopCount(v)));
    }

    [Fact]
    public void DraggingAHandleKeepsTheBoxInsideThePicture()
    {
        var box = new MaskBox(4, 4, 10, 10);
        Assert.Equal(new MaskBox(0, 4, 10, 10), SpriteMask.Drag(box, BoxHandle.Left, -50, 0, 16, 16));
        Assert.Equal(new MaskBox(10, 4, 10, 10), SpriteMask.Drag(box, BoxHandle.Left, 50, 0, 16, 16));
        Assert.Equal(new MaskBox(9, 9, 15, 15), SpriteMask.Drag(box, BoxHandle.Move, 5, 5, 16, 16));
        Assert.Equal(new MaskBox(4, 4, 15, 15), SpriteMask.Drag(box, BoxHandle.BottomRight, 99, 99, 16, 16));
        Assert.Equal(BoxHandle.TopLeft, SpriteMask.HandleAt(box, 4.5, 3.8, 1));
        Assert.Equal(BoxHandle.Move, SpriteMask.HandleAt(box, 7, 7, 1));
        Assert.Null(SpriteMask.HandleAt(box, 0, 0, 1));
    }

    [Fact]
    public void OriginPresetsCoverTheNinePositionsAndGuidesNeverCross()
    {
        Assert.Equal((0, 0), SpriteMask.OriginPreset(0, 32, 20));
        Assert.Equal((16, 10), SpriteMask.OriginPreset(4, 32, 20));
        Assert.Equal((32, 20), SpriteMask.OriginPreset(8, 32, 20));
        Assert.Equal((10, 2, 5, 3), SpriteMask.MoveGuide((4, 2, 5, 3), "left", 10, 32, 20));
        Assert.Equal((27, 2, 5, 3), SpriteMask.MoveGuide((4, 2, 5, 3), "left", 99, 32, 20));
        Assert.Equal((4, 2, 28, 3), SpriteMask.MoveGuide((4, 2, 5, 3), "right", 4, 32, 20));
    }

    [Fact]
    public void MaskCommandSetsModeBoundsBitsAndUndoRestoresThem()
    {
        using var data = TestData.Create(0);
        var sprite = NewSprite(data);
        var before = (sprite.BBoxMode, sprite.MarginLeft, sprite.MarginRight, sprite.CollisionMasks.Count, sprite.SepMasks);
        var history = new EditHistory(data);
        history.Execute(new SpriteMaskCommand("spr_edit", MaskMode.Manual, MaskShape.Rectangle, new MaskBox(4, 5, 11, 12)));
        Assert.Equal(((uint)MaskMode.Manual, 4, 11, 5, 12), (sprite.BBoxMode, sprite.MarginLeft, sprite.MarginRight, sprite.MarginTop, sprite.MarginBottom));
        var (w, h) = sprite.CalculateMaskDimensions(data);
        var mask = Assert.Single(sprite.CollisionMasks);
        Assert.Equal((w, h), (mask.Width, mask.Height));
        Assert.Equal((w + 7) / 8 * h, mask.Data.Length);
        Assert.Equal(64, mask.Data.Sum(v => System.Numerics.BitOperations.PopCount(v)));
        history.Undo();
        Assert.Equal(before, (sprite.BBoxMode, sprite.MarginLeft, sprite.MarginRight, sprite.CollisionMasks.Count, sprite.SepMasks));
    }

    [Fact]
    public void PrecisePerFrameKeepsOneMaskPerFrameAndFullUsesThePicture()
    {
        using var data = TestData.Create(0);
        var sprite = NewSprite(data);
        sprite.Textures.Add(new GameMakerSprite.TextureEntry { Texture = sprite.Textures[0].Texture });
        new SpriteMaskCommand("spr_edit", MaskMode.Full, MaskShape.PrecisePerFrame, default).Apply(data);
        Assert.Equal(2, sprite.CollisionMasks.Count);
        Assert.Equal(GameMakerSprite.SepMaskType.Precise, sprite.SepMasks);
        Assert.Equal((0, (int)sprite.Width - 1), (sprite.MarginLeft, sprite.MarginRight));
        Assert.Equal(MaskShape.PrecisePerFrame, SpriteMaskCommand.ShapeOf(sprite));
    }

    [Fact]
    public void OriginAndNineSliceUndoAndReplayFromArgs()
    {
        using var data = TestData.Create(0);
        var sprite = NewSprite(data);
        sprite.SVersion = 3;
        var (ox, oy) = (sprite.OriginX, sprite.OriginY);
        var origin = EditCommand.Create(SpriteOriginCommand.Name, new SpriteOriginCommand("spr_edit", 7, 9).Args);
        origin.Apply(data);
        Assert.Equal((7, 9), (sprite.OriginX, sprite.OriginY));
        origin.Revert(data);
        Assert.Equal((ox, oy), (sprite.OriginX, sprite.OriginY));

        var slice = EditCommand.Create(NineSliceCommand.Name, new NineSliceCommand("spr_edit", true, new SliceEdges(4, 5, 6, 7), [1, 1, 0, 0, 2]).Args);
        slice.Apply(data);
        Assert.Equal((4, 5, 6, 7, true), (sprite.V3NineSlice!.Left, sprite.V3NineSlice.Top, sprite.V3NineSlice.Right, sprite.V3NineSlice.Bottom, sprite.V3NineSlice.Enabled));
        Assert.Equal(GameMakerSprite.NineSlice.TileMode.Mirror, sprite.V3NineSlice.TileModes[4]);
        slice.Revert(data);
        Assert.False(sprite.V3NineSlice?.Enabled ?? false);
        Assert.Equal(0, sprite.V3NineSlice?.Left ?? 0);

        sprite.SVersion = 1;
        Assert.Throws<InvalidOperationException>(() => slice.Apply(data));
    }

    [Fact]
    public async Task ThePresetListAndModeListEditThroughTheWorkspaceAndUndo()
    {
        using var dir = new TempDir();
        string file;
        using (var data = TestData.Create(0))
        {
            NewSprite(data);
            file = TestData.Write(dir.File("data.win"), data);
        }

        var userData = new UserData(dir.File("user"));
        var service = new WorkspaceService(userData, RecentFiles.Load(userData));
        var workspace = await service.OpenAsync(file, cancellationToken: TestContext.Current.CancellationToken);
        var sprite = workspace.Data.Sprites.First(x => x.Name.Content == "spr_edit");
        await HeadlessApp.Run(async () =>
        {
            using var textures = new TextureBitmaps();
            var tools = new SpriteToolsView(sprite, textures, workspace);
            await tools.Ready;
            var combos = new Dictionary<string, Avalonia.Controls.ComboBox>();
            foreach (var combo in tools.GetLogicalDescendants().OfType<Avalonia.Controls.ComboBox>()) combos[combo.Name ?? ""] = combo;
            combos["SpriteOriginPreset"].SelectedIndex = 8;
            Assert.Equal(((int)sprite.Width, (int)sprite.Height), (sprite.OriginX, sprite.OriginY));
            combos["SpriteMaskMode"].SelectedIndex = (int)MaskMode.Full;
            Assert.Equal((uint)MaskMode.Full, sprite.BBoxMode);
            workspace.Undo();
            workspace.Undo();
            Assert.Equal(0, sprite.OriginX);
        });
    }

    [Theory]
    [InlineData(1000, 700)]
    [InlineData(700, 600)]
    public async Task TheSpriteTabKeepsOneBigCanvasAndNothingOutsideTheTab(int width, int height)
    {
        using var dir = new TempDir();
        string file;
        using (var data = TestData.Create(0))
        {
            NewSprite(data);
            file = TestData.Write(dir.File("data.win"), data);
        }

        var userData = new UserData(dir.File("user"));
        var service = new WorkspaceService(userData, RecentFiles.Load(userData));
        using var workspace = await service.OpenAsync(file, cancellationToken: TestContext.Current.CancellationToken);
        var sprite = workspace.Data.Sprites.First(x => x.Name.Content == "spr_edit");
        await HeadlessApp.Run(async () =>
        {
            using var textures = new TextureBitmaps();
            var editor = new SpriteEditorView(sprite, textures, workspace);
            var tab = new PropertyGridTab(workspace, "sprites", "spr_edit", editor);
            var window = new Avalonia.Controls.Window { Content = tab, Width = width, Height = height };
            window.Show();
            await editor.Tools.Ready;
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            var host = Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(editor).OfType<Avalonia.Controls.ScrollViewer>().Single(v => v.Name == "SpriteCanvasHost");
            Assert.True(host.Bounds.Height >= 240, $"canvas host {host.Bounds.Height}");
            Assert.Equal(width > SpriteEditorView.WideWidth, editor.IsWide);
            var area = new Avalonia.Rect(0, 0, tab.Bounds.Width, tab.Bounds.Height);
            foreach (var control in Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(editor).OfType<Avalonia.Controls.Control>().Where(c => c.IsVisible && c.Bounds.Width > 0 && !c.GetVisualAncestors().OfType<Avalonia.Controls.Primitives.ScrollBar>().Any() && c is not Avalonia.Controls.Primitives.ScrollBar && !c.GetVisualAncestors().OfType<Avalonia.Controls.Viewbox>().Any()))
            {
                var origin = Avalonia.VisualExtensions.TranslatePoint(control, default, tab);
                Assert.True(origin is { } p && p.X >= -0.5 && p.X + control.Bounds.Width <= area.Width + 0.5, $"{control.GetType().Name} {control.Name} at {origin} width {control.Bounds.Width}");
            }

            window.Close();
        });
    }
}
