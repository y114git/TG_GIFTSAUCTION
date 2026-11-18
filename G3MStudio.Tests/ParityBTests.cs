using Avalonia;
using Avalonia.Input;
using G3MLib.DataFile.Models;
using G3MStudio.Core.Workspace;
using G3MStudio.Editor;

namespace G3MStudio.Tests;

public sealed class ParityBTests
{
    [Fact]
    public async Task PathPointsSnapToTheGridStep()
    {
        await HeadlessApp.Run(() =>
        {
            var path = new GameMakerPath { Name = new GameMakerString("p"), Points = [] };
            path.Points.Add(new GameMakerPath.PathPoint { X = 0, Y = 0 });
            path.Points.Add(new GameMakerPath.PathPoint { X = 100, Y = 100 });
            var view = new PathView(path, null) { SnapStep = 16 };
            view.Measure(new Size(400, 320));
            view.Arrange(new Rect(0, 0, 400, 320));
            var at = view.ToPath(view.ToScreen(new Point(37, 21)));
            Assert.Equal(32, at.X);
            Assert.Equal(16, at.Y);
            return Task.CompletedTask;
        });
    }

    [Fact]
    public void FontPresetsCoverTheirCharacters()
    {
        Assert.Equal(95, FontGlyphsView.Preset("Normal").Count);
        Assert.Equal(10, FontGlyphsView.Preset("Digits").Count);
        Assert.Equal(52, FontGlyphsView.Preset("Letters").Count);
    }

    [Fact]
    public async Task SampleTextNamesCharactersWithoutAGlyph()
    {
        await HeadlessApp.Run(() =>
        {
            var font = new GameMakerFont { Name = new GameMakerString("f"), Glyphs = [new GameMakerFont.Glyph { Character = 'a' }] };
            var view = new FontGlyphsView(font);
            Assert.Equal("", view.MissingText("aaa"));
            Assert.Contains("bz", view.MissingText("abz a"), StringComparison.Ordinal);
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task ARangeCountsOnlyTheGlyphsTheFontHas()
    {
        await HeadlessApp.Run(() =>
        {
            var font = new GameMakerFont { Name = new GameMakerString("f"), Glyphs = [.. "abc".Select(c => new GameMakerFont.Glyph { Character = c })] };
            var view = new FontGlyphsView(font);
            Assert.Equal(2, view.MatchingGlyphs("b-z"));
            Assert.Equal(0, view.MatchingGlyphs("x-z"));
            Assert.Equal(0, view.MatchingGlyphs(""));
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task HoldingPSelectsFromAnyLayer()
    {
        await HeadlessApp.Run(() =>
        {
            var view = new Rooms.RoomView();
            view.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.P });
            Assert.True(view.IgnoreLocks);
            view.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Key = Key.P });
            Assert.False(view.IgnoreLocks);
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task TheElementListFiltersRowsByText()
    {
        await HeadlessApp.Run(() =>
        {
            var layer = new GameMakerRoom.Layer { LayerName = new GameMakerString("l"), LayerType = GameMakerRoom.LayerType.Instances, Data = new GameMakerRoom.Layer.LayerInstancesData { Instances = [] } };
            layer.InstancesData.Instances.Add(new GameMakerRoom.GameObject { InstanceID = 100001, X = 1, Y = 2 });
            layer.InstancesData.Instances.Add(new GameMakerRoom.GameObject { InstanceID = 100002, X = 3, Y = 4 });
            var list = new RoomElementList(new GameMakerRoom { Layers = [layer] });
            list.Show(layer);
            Assert.Equal(2, list.ItemCount);
            list.Filter = "(3, 4)";
            Assert.Equal(1, list.ItemCount);
            return Task.CompletedTask;
        });
    }

    [Fact]
    public void PageItemsAreCountedPerPage()
    {
        var page = new GameMakerEmbeddedTexture();
        var other = new GameMakerEmbeddedTexture();
        using var a = new GameMakerTexturePageItem { TexturePage = page };
        using var b = new GameMakerTexturePageItem { TexturePage = page };
        using var c = new GameMakerTexturePageItem { TexturePage = other };
        Assert.Equal(2, TexturePagePreview.ItemsOn(page, [a, b, c]));
    }

    [Fact]
    public async Task TheSoundViewNamesItsStorageFlags()
    {
        await HeadlessApp.Run(() =>
        {
            var sound = new GameMakerSound { Name = new GameMakerString("s"), Volume = 0.5f, Pitch = 1f };
            var view = new SoundPlayerView(sound, _ => true);
            Assert.Contains("0.5", view.Options, StringComparison.Ordinal);
            return Task.CompletedTask;
        });
    }

    [Fact]
    public void TheShaderCheckFindsBracketsAndMissingOutputs()
    {
        Assert.Empty(ShaderCheck.Check("void main() { gl_Position = vec4(0.0); } // ) ignored", true, false));
        Assert.Contains("editors.shaderCheck.brackets", ShaderCheck.Check("void main() { gl_FragColor = vec4(1.0); ", false, false));
        Assert.Contains("editors.shaderCheck.noColor", ShaderCheck.Check("void main() { }", false, false));
        Assert.Contains("editors.shaderCheck.noMain", ShaderCheck.Check("float x;", true, true));
    }

    [Fact]
    public async Task AFontWithAnUnknownFamilyGetsAWarningAndItsSettingsLine()
    {
        await HeadlessApp.Run(() =>
        {
            var font = new GameMakerFont { Name = new GameMakerString("f"), DisplayName = new GameMakerString("NoSuchFamilyX"), EmSize = 12, SDFSpread = 8, Glyphs = [] };
            Assert.NotNull(FontGlyphsView.Warning(font));
            Assert.Contains("NoSuchFamilyX", FontGlyphsView.Settings(font), StringComparison.Ordinal);
            Assert.Contains("8", FontGlyphsView.Settings(font), StringComparison.Ordinal);
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task ABackdropRoomJoinsTheFittedArea()
    {
        await HeadlessApp.Run(() =>
        {
            var path = new GameMakerPath { Name = new GameMakerString("p"), Points = [] };
            path.Points.Add(new GameMakerPath.PathPoint { X = 10, Y = 10 });
            path.Points.Add(new GameMakerPath.PathPoint { X = 20, Y = 20 });
            var view = new PathView(path, null);
            view.Measure(new Size(400, 320));
            view.Arrange(new Rect(0, 0, 400, 320));
            var alone = view.ToScreen(new Point(20, 20)) - view.ToScreen(new Point(10, 10));
            view.Backdrop = (new Rect(0, 0, 640, 480), []);
            var inRoom = view.ToScreen(new Point(20, 20)) - view.ToScreen(new Point(10, 10));
            Assert.True(inRoom.X < alone.X);
            Assert.InRange(view.ToScreen(new Point(640, 480)).X, 0, 400);
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task TheImageEditorRightColumnFoldsEveryPanel()
    {
        await HeadlessApp.Run(() =>
        {
            using var view = new ImageEditorView(new LocalImageSession(new PixelBuffer(8, 8)));
            var folds = Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(view).OfType<Avalonia.Controls.Expander>().Where(e => e.Name?.StartsWith("Section_", StringComparison.Ordinal) == true).ToList();
            Assert.Equal(4, folds.Count);
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task TheExplorerZoomIsKeptBetweenFiftyAndTwoHundredFifty()
    {
        await HeadlessApp.Run(() =>
        {
            var view = new Explorer.ExplorerView();
            view.SetZoom(400);
            Assert.Equal(2.5, view.Zoom);
            view.SetZoom(10);
            Assert.Equal(0.5, view.Zoom);
            return Task.CompletedTask;
        });
    }

    [Fact]
    public void IndentAndOutdentChangeEveryTouchedLine()
    {
        var indented = GmlLineEdits.Indent("a\n\nb", 0, 4);
        Assert.Equal("\ta\n\n\tb", indented.Text);
        var back = GmlLineEdits.Outdent("\ta\n    b\nc", 0, 10);
        Assert.Equal("a\nb\nc", back.Text);
    }
}
