using Avalonia.Controls;
using G3MStudio.Core.Settings;
using G3MStudio.Localization;

namespace G3MStudio.Settings;

public sealed partial class SettingsPage
{
    private Control[] ImageSection(AppSettings s)
    {
        var o = s.Image;
        CheckBox Check(string name, string key, bool value, Func<ImageOptions, bool, ImageOptions> set)
        {
            var box = new CheckBox { Name = name, Tag = Loc.Current[key], Content = Loc.Current[key], IsChecked = value };
            box.IsCheckedChanged += (_, _) => { if (!_building) _store.Update(x => x with { Image = set(x.Image, box.IsChecked == true) }); };
            return box;
        }

        TextBox Text(string name, string value, Func<ImageOptions, string, ImageOptions> set, string placeholder = "settings.imageColourDefault")
        {
            var box = new TextBox { Name = name, Text = value, Width = 160, PlaceholderText = Loc.Current[placeholder] };
            box.LostFocus += (_, _) => { if (!_building) _store.Update(x => x with { Image = set(x.Image, box.Text ?? "") }); };
            return box;
        }

        NumericUpDown Num(string name, int min, int max, int value, Func<ImageOptions, int, ImageOptions> set) => Number(name, min, max, 1, value, (x, n) => x with { Image = set(x.Image, n) });

        return
        [
            Heading("settings.image"),
            Row("settings.imageBrushSize", Num("ImageOptBrushSize", 1, 256, o.BrushSize, (i, n) => i with { BrushSize = n })),
            Check("ImageOptRound", "settings.imageRound", o.BrushRound, (i, v) => i with { BrushRound = v }),
            Check("ImageOptSmooth", "settings.imageSmooth", o.Smooth, (i, v) => i with { Smooth = v }),
            Row("settings.imageTolerance", Num("ImageOptTolerance", 0, 255, o.Tolerance, (i, n) => i with { Tolerance = n })),
            Check("ImageOptContiguous", "settings.imageContiguous", o.Contiguous, (i, v) => i with { Contiguous = v }),
            Row("settings.imageTextSize", Num("ImageOptTextSize", 4, 256, o.TextSize, (i, n) => i with { TextSize = n }), Text("ImageOptTextFont", o.TextFont, (i, t) => i with { TextFont = t }, "settings.imageFontDefault")),
            Check("ImageOptLayerAutosave", "settings.imageLayerAutosave", o.LayerAutosave, (i, v) => i with { LayerAutosave = v }),
            Check("ImageOptGrid", "settings.imageGrid", o.ShowGrid, (i, v) => i with { ShowGrid = v }),
            Row("settings.imageGridCell", Num("ImageOptGridCell", 0, 1024, o.GridCell, (i, n) => i with { GridCell = n }), Text("ImageOptGridColour", o.GridColour, (i, t) => i with { GridColour = t })),
            Row("settings.imageOnion", Num("ImageOptOnionFrames", 1, 8, o.OnionFrames, (i, n) => i with { OnionFrames = n }), Num("ImageOptOnionOpacity", 5, 100, o.OnionOpacity, (i, n) => i with { OnionOpacity = n })),
            Row("settings.imageChecker", Text("ImageOptCheckerLight", o.CheckerLight, (i, t) => i with { CheckerLight = t }), Text("ImageOptCheckerDark", o.CheckerDark, (i, t) => i with { CheckerDark = t })),
            Row("settings.imageBackground", Text("ImageOptBackground", o.CanvasBackground, (i, t) => i with { CanvasBackground = t })),
            Row("settings.imageHandle", Num("ImageOptHandle", 1, 12, o.HandleSize, (i, n) => i with { HandleSize = n })),
        ];
    }
}
