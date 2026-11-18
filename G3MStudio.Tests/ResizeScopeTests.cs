using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.LogicalTree;
using G3MLib.DataFile;
using G3MLib.DataFile.Models;
using G3MStudio.Core.Workspace;
using G3MStudio.Editor;
using G3MStudio.Localization;

#pragma warning disable CA2000 // the windows are closed by the test
namespace G3MStudio.Tests;

/// <summary>A resize touches the selected frame unless every frame is chosen on purpose; the dialog and the handle label say which, with the real sizes.</summary>
public sealed class ResizeScopeTests
{
    private static readonly Rgba Red = new(255, 0, 0, 255);

    // spr_a with three frames of 40 x 40, 64 x 64 and 20 x 10 (each frame on its own texture item).
    private static (GameMakerData Data, EditHistory History, GameMakerSprite Sprite) Fixture()
    {
        var data = CopyResourcesTests.Source();
        var history = new EditHistory(data);
        var sprite = data.Sprites.First(s => s.Name.Content == "spr_a");
        static PixelBuffer Solid(int w, int h)
        {
            var p = new PixelBuffer(w, h);
            for (var y = 0; y < h; y++)
                for (var x = 0; x < w; x++) p.Set(x, y, Red);
            return p;
        }

        history.Execute(new TextureFitCommand("sprites", "spr_a", 0, FramePicture.ToPng(Solid(40, 40))));
        (sprite.Width, sprite.Height) = (40, 40);
        foreach (var (name, w, h) in new[] { ("spr_b", 64, 64), ("spr_c", 20, 10) })
        {
            history.Execute(ResourceTemplates.New(data, "sprites", name));
            history.Execute(new TextureFitCommand("sprites", name, 0, FramePicture.ToPng(Solid(w, h))));
            sprite.Textures.Add(new GameMakerSprite.TextureEntry { Texture = data.Sprites.First(s => s.Name.Content == name).Textures[0].Texture });
        }

        return (data, history, sprite);
    }

    private static (int, int)[] Sizes(GameMakerSprite sprite) => [.. sprite.Textures.Select(t => ((int)t.Texture!.TargetWidth, (int)t.Texture.TargetHeight))];

    [Fact]
    public void OneFrameChangesAloneAndTheSpriteSizeStaysAndUndoAndRedoPutItBack()
    {
        var (data, history, sprite) = Fixture();
        using (data)
        {
            history.Execute(SpriteResize.Build(data, "spr_a", 1, new ResizeSpec(false, 30, 30), false, null));
            Assert.Equal([(40, 40), (30, 30), (20, 10)], Sizes(sprite));
            Assert.Equal((40u, 40u), (sprite.Width, sprite.Height)); // the sprite keeps one size, GameMaker pads or crops the frame
            history.Undo();
            Assert.Equal([(40, 40), (64, 64), (20, 10)], Sizes(sprite));
            history.Redo();
            Assert.Equal([(40, 40), (30, 30), (20, 10)], Sizes(sprite));
        }
    }

    [Fact]
    public void EveryFrameBecomesTheSameSizeAsOneEditThatUndoesAndRedoesTogether()
    {
        var (data, history, sprite) = Fixture();
        using (data)
        {
            var before = history.Position;
            history.Execute(SpriteResize.Build(data, "spr_a", 1, new ResizeSpec(false, 30, 30), true, null));
            Assert.Equal(before + 1, history.Position);
            Assert.Equal([(30, 30), (30, 30), (30, 30)], Sizes(sprite));
            Assert.Equal((30u, 30u), (sprite.Width, sprite.Height));
            history.Undo();
            Assert.Equal([(40, 40), (64, 64), (20, 10)], Sizes(sprite));
            Assert.Equal((40u, 40u), (sprite.Width, sprite.Height));
            history.Redo();
            Assert.Equal([(30, 30), (30, 30), (30, 30)], Sizes(sprite));
        }
    }

    [Fact]
    public void TheSizesListNamesEachDistinctSizeOnceAndCapsAtThree()
    {
        Assert.Equal("40 x 40, 64 x 64", ResizeScope.Sizes([(40, 40), (64, 64), (40, 40)]));
        Assert.Equal("1 x 1, 2 x 2, 3 x 3 ...", ResizeScope.Sizes([(1, 1), (2, 2), (3, 3), (4, 4)]));
    }

    [Fact]
    public async Task TheDialogOpensOnTheSelectedFrameAndAllFramesShowsTheWarningWithTheNumbers()
    {
        await HeadlessApp.Run(() =>
        {
            Loc.Current.SetLanguage("en");
            ResizeChoices.Current = new ResizeChoices();
            var sizes = new[] { (40, 40), (64, 64), (20, 10) };
            var first = new ResizeImageWindow((64, 64), 3, [], null, false, frame: 1, frameSizes: sizes);
            Assert.False(first.Choice.AllFrames);
            Assert.False(Field<TextBlock>(first, "ResizeScopeWarning").IsVisible);
            Assert.Equal("Resize frame 2", Field<Button>(first, "ResizeOk").Content);
            Field<RadioButton>(first, "ResizeScopeAll").IsChecked = true;
            Field<NumericUpDown>(first, "ResizeWidth").Value = 30;
            Field<NumericUpDown>(first, "ResizeHeight").Value = 30;
            Assert.True(first.Choice.AllFrames);
            var warning = Field<TextBlock>(first, "ResizeScopeWarning");
            Assert.True(warning.IsVisible);
            Assert.Equal("All 3 frames will become 30 x 30 (now 40 x 40, 64 x 64, 20 x 10)", warning.Text);
            Assert.Equal("Resize 3 frames", Field<Button>(first, "ResizeOk").Content);
            first.Remember();
            first.Close();

            var again = new ResizeImageWindow((64, 64), 3, [], null, false, frame: 1, frameSizes: sizes);
            Assert.False(again.Choice.AllFrames); // never carried over to the next dialog
            Assert.True(Field<RadioButton>(again, "ResizeScopeFrame").IsChecked);
            again.Close();
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task ASpriteOfOneFrameHasNoScopeControlAndResizesTheWholeSprite()
    {
        await HeadlessApp.Run(() =>
        {
            var window = new ResizeImageWindow((32, 16), 1, [], null, false);
            Assert.DoesNotContain(window.GetLogicalDescendants().OfType<Control>(), c => c.Name is "ResizeScope" or "ResizeScopeAll");
            Assert.True(window.Choice.AllFrames);
            window.Close();
            return Task.CompletedTask;
        });
    }

    [Theory]
    [InlineData(125, "ru")]
    [InlineData(150, "ru")]
    [InlineData(150, "es")]
    public async Task TheScopeControlAndItsWarningAreNotCroppedAtLargeScaleInLongLanguages(int scale, string language)
    {
        using var dir = new TempDir();
        await HeadlessApp.Run(async () =>
        {
            var owner = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                owner.Workspaces.Settings.Update(x => x with { UiScale = scale });
                Loc.Current.SetLanguage(language);
                ResizeChoices.Current = new ResizeChoices();
                var dialog = new ResizeImageWindow((55, 75), 12, [], null, true, frameSizes: [.. Enumerable.Range(0, 12).Select(i => (10 + i, 20 + i))]);
                dialog.Show(owner);
                Field<RadioButton>(dialog, "ResizeScopeAll").IsChecked = true;
                for (var i = 0; i < 4; i++)
                {
                    await Task.Delay(30);
                    Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                }

                var warning = Field<TextBlock>(dialog, "ResizeScopeWarning");
                Assert.True(warning.IsVisible);
                Assert.True(warning.Bounds.Height > 0);
                // wrapped text is as tall as its lines: nothing is clipped by a fixed height
                Assert.True(double.IsNaN(warning.Height));
                Assert.True(warning.Bounds.Right <= Field<StackPanel>(dialog, "ResizeScope").Bounds.Width + 0.5);
                dialog.Close();
            }
            finally
            {
                Loc.Current.SetLanguage("en");
                owner.Close();
            }
        });
    }

    [Fact]
    public void TheHandleLabelNamesTheFrameOrEveryFrameWithTheCurrentSizes()
    {
        Loc.Current.SetLanguage("en");
        ResizeChoices.Current = new ResizeChoices();
        var sizes = new[] { (1000, 1000), (64, 64), (1000, 1000) };
        Assert.Equal(", frame 3", ResizeScope.DragSuffix(new ResizeSpec(false, 120, 90), 2, sizes));
        Assert.Equal(", all 3 frames (now 1000 x 1000, 64 x 64)", ResizeScope.DragSuffix(new ResizeSpec(false, 120, 90) { AllFrames = true }, 2, sizes));
        Assert.Equal("", ResizeScope.DragSuffix(new ResizeSpec(false, 120, 90) { AllFrames = true }, 0, [(8, 8)])); // one frame: no scope to name
        Assert.Equal(", at the limit of 4096 pixels", ResizeScope.DragSuffix(new ResizeSpec(false, 4096, 90), 0, [(8, 8)]));
        Assert.True(CanvasHandles.Spec((1, 0), (10, 10), false, true).AllFrames);
        Assert.False(CanvasHandles.Spec((1, 0), (10, 10), true).AllFrames);
    }

    [Fact]
    public void TheAllFramesKeyIsOneSettingWithAltAsItsDefault()
    {
        ResizeChoices.Current = new ResizeChoices();
        Assert.Equal(KeyModifiers.Alt, ResizeScope.Modifier);
        ResizeChoices.Current = new ResizeChoices { AllKey = "Shift" };
        Assert.Equal(KeyModifiers.Shift, ResizeScope.Modifier);
        ResizeChoices.Current = new ResizeChoices { AllKey = "nonsense" };
        Assert.Equal(KeyModifiers.Alt, ResizeScope.Modifier);
        ResizeChoices.Current = new ResizeChoices();
    }

    private static T Field<T>(Window window, string name) where T : Control => window.GetLogicalDescendants().OfType<T>().Single(c => c.Name == name);
}
