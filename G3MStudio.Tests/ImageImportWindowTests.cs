using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using G3MLib.DataFile;
using G3MLib.DataFile.Models;
using G3MStudio.Core.Workspace;
using G3MStudio.Editor;

#pragma warning disable CA2000 // the windows are closed by the test or never shown
namespace G3MStudio.Tests;

public sealed class ImageImportWindowTests
{
    private static T Named<T>(Control root, string name) where T : Control => root.GetLogicalDescendants().OfType<T>().First(c => c.Name == name);

    private static PixelBuffer Strip(int frames, int size)
    {
        var strip = new PixelBuffer(frames * size, size);
        for (var i = 3; i < strip.Data.Length; i += 4) strip.Data[i] = 255;
        return strip;
    }

    [Fact]
    public async Task TheNumberBoxesFitFiveDigits()
    {
        await HeadlessApp.Run(() =>
        {
            var window = new SheetImportWindow([Strip(1, 16)], null, (16, 16));
            window.Show();
            foreach (var name in (string[])["SheetWidth", "SheetHeight", "SheetOffsetX", "SheetGapY"])
            {
                var box = Named<NumericUpDown>(window, name);
                box.Maximum = 99999;
                box.Value = 99999;
                window.UpdateLayout();
                var field = box.GetVisualDescendants().OfType<TextBox>().First();
                var text = new Avalonia.Media.FormattedText("99999", System.Globalization.CultureInfo.InvariantCulture, Avalonia.Media.FlowDirection.LeftToRight,
                    new Avalonia.Media.Typeface(field.FontFamily, field.FontStyle, field.FontWeight), field.FontSize, null);
                Assert.True(field.Bounds.Width - field.Padding.Left - field.Padding.Right >= text.Width, $"{name}: field {field.Bounds.Width} for text {text.Width}");
            }

            window.Close();
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task TheSheetWindowCutsByCountOrSizeAndRefusesAMismatchUntilReplace()
    {
        await HeadlessApp.Run(() =>
        {
            // A name with _strip4 starts as four columns; the frame size follows from the sheet.
            var window = new SheetImportWindow([Strip(4, 16)], 4, (32, 32));
            Assert.Equal(4, window.Frames.Count);
            Assert.Equal((16, 16), (window.Frames[0].Width, window.Frames[0].Height));
            Assert.True(window.Replace); // 16 by 16 is not the sprite size
            Assert.True(Named<Button>(window, "SheetOk").IsEnabled);

            Named<CheckBox>(window, "SheetReplace").IsChecked = false;
            Assert.False(Named<Button>(window, "SheetOk").IsEnabled);
            Assert.Contains("32", Named<TextBlock>(window, "SheetSummary").Text, StringComparison.Ordinal);

            Named<ComboBox>(window, "SheetMode").SelectedIndex = 1;
            Named<NumericUpDown>(window, "SheetWidth").Value = 32;
            Named<NumericUpDown>(window, "SheetHeight").Value = 16;
            Assert.Equal(2, window.Frames.Count);
            Named<NumericUpDown>(window, "SheetWidth").Value = 100;
            Assert.Empty(window.Frames); // wider than the sheet
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task AGifGivesItsOwnFramesWithoutCutting()
    {
        await HeadlessApp.Run(() =>
        {
            var window = new SheetImportWindow([Strip(1, 8), Strip(1, 8), Strip(1, 8)], null, (8, 8));
            Assert.Equal(3, window.Frames.Count);
            Assert.False(window.Replace);
            Assert.True(Named<Button>(window, "SheetOk").IsEnabled);
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task TheResizeWindowKeepsTheRatioConvertsPercentAndReadsTheAnchor()
    {
        await HeadlessApp.Run(() =>
        {
            var window = new ResizeImageWindow((32, 16), 3, ["tg_a", "tg_b"], "tg_b", false);
            Assert.False(window.Choice.Spec.Scale);
            Assert.Equal(("tg_b", true), (window.Choice.Group, window.Choice.AllFrames));
            Named<NumericUpDown>(window, "ResizeWidth").Value = 64;
            Assert.Equal(32, (int)(Named<NumericUpDown>(window, "ResizeHeight").Value ?? 0)); // ratio kept
            Named<RadioButton>(window, "ResizeAnchor5").IsChecked = true;
            Assert.Equal(new ResizeSpec(false, 64, 32, 2, 1), window.Choice.Spec);

            Named<ComboBox>(window, "ResizeUnit").SelectedIndex = 1;
            Named<NumericUpDown>(window, "ResizeWidth").Value = 50;
            Named<ComboBox>(window, "ResizeMode").SelectedIndex = 0;
            Named<CheckBox>(window, "ResizeAll").IsChecked = false;
            Assert.Equal((true, 16, 8, false), (window.Choice.Spec.Scale, window.Choice.Spec.Width, window.Choice.Spec.Height, window.Choice.AllFrames));
            return Task.CompletedTask;
        });
    }

    private sealed class FakeSession : ImageSession
    {
        public ResizeSpec? Spec;
        public string? Group;
        public override int FrameCount => 1;
        public override PixelBuffer Read(int frame) => Strip(1, 8);
        public override void Commit(int frame, ImageEdit edit) { }
        public override bool CanUndo => false;
        public override bool CanRedo => false;
        public override void Undo() { }
        public override void Redo() { }
        public override ResizeContext? ResizeContext => new(1, ["tg_a"], "tg_a");
        public override bool CommitResize(int frame, ResizeSpec spec, bool allFrames, string? group) { (Spec, Group) = (spec, group); return true; }
    }

    [Fact]
    public async Task ResizeInTheImageEditorOpensTheDialogAndCommitsItsChoice()
    {
        await HeadlessApp.Run(async () =>
        {
            var session = new FakeSession();
            using var view = new ImageEditorView(session);
            var host = new Window { Width = 1000, Height = 700, Content = view };
            host.Show();
            view.ResizeOpened += dialog =>
            {
                Named<NumericUpDown>(dialog, "ResizeWidth").Value = 16;
                Dispatcher.UIThread.Post(() => Named<Button>(dialog, "ResizeOk").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)));
            };
            view.Run("img.scale");
            for (var i = 0; i < 50 && session.Spec is null; i++) await Task.Delay(20);
            Assert.Equal(new ResizeSpec(true, 16, 16), session.Spec); // the ratio of the square follows
            Assert.Equal("tg_a", session.Group);
            host.Close();
        });
    }

    [Fact]
    public async Task TheImageToTilesWindowShowsTheMatchAndNeedsAPicture()
    {
        await HeadlessApp.Run(async () =>
        {
            using var data = TestData.Create(0);
            TestData.AddPage(data);
            var sheet = new PixelBuffer(16, 8);
            sheet.Place(Strip(1, 8), 8, 0, false);
            using var image = new ImageMagick.MagickImage(FramePicture.ToPng(sheet));
            var set = new GameMakerBackground { Name = data.Strings.MakeString("ts_w", out _), GMS2TileWidth = 8, GMS2TileHeight = 8, GMS2TileColumns = 2, GMS2TileCount = 2, GMS2OutputBorderX = 0, GMS2OutputBorderY = 0, Texture = SpriteTemplate.AddItem(data, image) };
            data.Backgrounds.Add(set);
            new ResourceListCommand("rooms", ResourceListCommand.New, "rm_w").Apply(data);
            var room = data.Rooms.Single(r => r.Name.Content == "rm_w");
            (room.Width, room.Height) = (16, 8);
            using var dir = new TempDir();
            await File.WriteAllBytesAsync(dir.File("pic.png"), FramePicture.ToPng(sheet), TestContext.Current.CancellationToken);

            var window = new ImageToTilesWindow(room, [set], () => Task.FromResult<string?>(null));
            Assert.Null(window.Match);
            Assert.False(Named<Button>(window, "TilesOk").IsEnabled);
            window.Load(dir.File("pic.png"));
            Assert.Equal((1, 0), (window.Match!.Matched, window.Match.Unmatched.Count));
            Assert.True(Named<Button>(window, "TilesOk").IsEnabled);
            Assert.Equal("Tiles", window.LayerName);

            await File.WriteAllTextAsync(dir.File("bad.png"), "nope", TestContext.Current.CancellationToken);
            window.Load(dir.File("bad.png"));
            Assert.Null(window.Match);
            Assert.False(Named<Button>(window, "TilesOk").IsEnabled);
        });
    }
}
