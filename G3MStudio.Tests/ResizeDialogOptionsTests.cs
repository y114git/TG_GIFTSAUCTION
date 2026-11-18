using Avalonia.Controls;
using Avalonia.LogicalTree;
using G3MStudio.Core.Workspace;
using G3MStudio.Editor;
using G3MStudio.Localization;

namespace G3MStudio.Tests;

/// <summary>
/// The Resize Image/Canvas dialog starts on its first choices, names what each mode does, switches off (with the reason) the options the other mode ignores,
/// and brings the options back as they were applied.
/// </summary>
public sealed class ResizeDialogOptionsTests
{
    private static T Field<T>(Window window, string name) where T : Control => window.GetLogicalDescendants().OfType<T>().Single(c => c.Name == name);

    [Fact]
    public async Task TheDialogStartsOnItsFirstChoicesAndEachModeSwitchesOffWhatItIgnores()
    {
        await HeadlessApp.Run(() =>
        {
            ResizeChoices.Current = new ResizeChoices();
            var dialog = new ResizeImageWindow((32, 16), 2, [], null, null);
            Assert.Equal(0, Field<ComboBox>(dialog, "ResizeMode").SelectedIndex);
            Assert.Equal(0, Field<ComboBox>(dialog, "ResizeMethod").SelectedIndex);
            Assert.Equal(0, Field<ComboBox>(dialog, "ResizeFill").SelectedIndex);
            Assert.Equal(0, Field<ComboBox>(dialog, "ResizeUnit").SelectedIndex);
            Assert.True(Field<RadioButton>(dialog, "ResizeAnchor0").IsChecked);
            Assert.Equal(Loc.Current["image.resizeTitle"], dialog.Title);
            Assert.True(dialog.Choice.Spec.Scale);

            // Resize image: the method works, the anchor and the fill do nothing, and say so.
            var (method, fill, anchor) = (Field<ComboBox>(dialog, "ResizeMethod"), Field<ComboBox>(dialog, "ResizeFill"), Field<RadioButton>(dialog, "ResizeAnchor4"));
            Assert.True(method.IsEnabled);
            Assert.False(fill.IsEnabled);
            Assert.False(anchor.IsEffectivelyEnabled);
            Assert.Equal(Loc.Current["image.fillOff"], ToolTip.GetTip(fill));
            Assert.Equal(Loc.Current["image.anchorOff"], ToolTip.GetTip(anchor.GetLogicalAncestors().OfType<Grid>().First()));

            Field<ComboBox>(dialog, "ResizeMode").SelectedIndex = 1;
            Assert.False(method.IsEnabled);
            Assert.True(fill.IsEnabled);
            Assert.True(anchor.IsEffectivelyEnabled);
            Assert.Equal(Loc.Current["image.methodOff"], ToolTip.GetTip(method));
            Assert.False(dialog.Choice.Spec.Scale);
            dialog.Close();
        });
    }

    [Fact]
    public async Task TheOptionsComeBackAsAppliedAndTheSizesDoNot()
    {
        await HeadlessApp.Run(() =>
        {
            ResizeChoices.Current = new ResizeChoices();
            var first = new ResizeImageWindow((32, 16), 2, [], null, null);
            Field<ComboBox>(first, "ResizeMethod").SelectedIndex = 1;
            Field<CheckBox>(first, "ResizeKeep").IsChecked = false;
            Field<ComboBox>(first, "ResizeUnit").SelectedIndex = 1;
            Field<NumericUpDown>(first, "ResizeWidth").Value = 250;
            Assert.True(first.Choice.Spec.Smooth);
            Assert.Equal(80, first.Choice.Spec.Width); // 250 percent of 32
            first.Remember();
            first.Close();

            var again = new ResizeImageWindow((50, 20), 2, [], null, null);
            Assert.Equal(1, Field<ComboBox>(again, "ResizeMethod").SelectedIndex);
            Assert.False(Field<CheckBox>(again, "ResizeKeep").IsChecked);
            Assert.Equal(1, Field<ComboBox>(again, "ResizeUnit").SelectedIndex);
            Assert.Equal(250m, Field<NumericUpDown>(again, "ResizeWidth").Value); // percent: the last percent comes back
            Assert.Equal(100m, Field<NumericUpDown>(again, "ResizeHeight").Value); // the aspect was not kept, so the height stayed
            again.Close();

            var canvas = new ResizeImageWindow((50, 20), 2, [], null, null);
            Field<ComboBox>(canvas, "ResizeMode").SelectedIndex = 1;
            Field<ComboBox>(canvas, "ResizeFill").SelectedIndex = 2;
            Field<RadioButton>(canvas, "ResizeAnchor8").IsChecked = true;
            canvas.Remember();
            canvas.Close();
            var last = new ResizeImageWindow((50, 20), 2, [], null, null);
            Assert.Equal(1, Field<ComboBox>(last, "ResizeMode").SelectedIndex);
            Assert.Equal(2, Field<ComboBox>(last, "ResizeFill").SelectedIndex);
            Assert.True(Field<RadioButton>(last, "ResizeAnchor8").IsChecked);
            Assert.Equal((2, 2, new Rgba(255, 255, 255, 255)), (last.Choice.Spec.AnchorX, last.Choice.Spec.AnchorY, last.Choice.Spec.Fill));
            last.Close();

            // The entry that names a mode (the Canvas menu) opens on that mode whatever was kept.
            var named = new ResizeImageWindow((50, 20), 2, [], null, true);
            Assert.Equal(0, Field<ComboBox>(named, "ResizeMode").SelectedIndex);
            named.Close();
            ResizeChoices.Current = new ResizeChoices();
        });
    }

    [Fact]
    public async Task TheSheetDialogStartsByCountAndKeepsTheLastWay()
    {
        await HeadlessApp.Run(() =>
        {
            ResizeChoices.Current = new ResizeChoices();
            var strip = new PixelBuffer(16, 16);
            var window = new SheetImportWindow([strip], null, (16, 16));
            Assert.Equal(0, Field<ComboBox>(window, "SheetMode").SelectedIndex);
            window.Close();
            ResizeChoices.Current = new ResizeChoices { SheetBySize = true };
            var bySize = new SheetImportWindow([strip], null, (16, 16));
            Assert.Equal(1, Field<ComboBox>(bySize, "SheetMode").SelectedIndex);
            bySize.Close();
            ResizeChoices.Current = new ResizeChoices();
        });
    }
}
