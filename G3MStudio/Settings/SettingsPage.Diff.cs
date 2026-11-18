using Avalonia.Controls;
using G3MStudio.Core.Settings;
using G3MStudio.Localization;

namespace G3MStudio.Settings;

public sealed partial class SettingsPage
{
    private static readonly string[] PictureModes = ["overlay", "swipe", "difference"];

    private Control[] DiffSection(AppSettings s)
    {
        var o = s.Diff;
        CheckBox Check(string name, string key, bool value, Func<DiffOptions, bool, DiffOptions> set)
        {
            var box = new CheckBox { Name = name, Tag = Loc.Current[key], Content = Loc.Current[key], IsChecked = value };
            box.IsCheckedChanged += (_, _) => { if (!_building) _store.Update(x => x with { Diff = set(x.Diff, box.IsChecked == true) }); };
            return box;
        }

        var mode = new ComboBox { Name = "DiffOptPictureMode", Width = 160, ItemsSource = new[] { Loc.Current["mods.diff.pictureOverlay"], Loc.Current["mods.diff.pictureSwipe"], Loc.Current["mods.diff.pictureDifference"] }, SelectedIndex = Math.Max(Array.IndexOf(PictureModes, o.PictureMode), 0) };
        mode.SelectionChanged += (_, _) => { if (!_building && mode.SelectedIndex >= 0) _store.Update(x => x with { Diff = x.Diff with { PictureMode = PictureModes[mode.SelectedIndex] } }); };
        return
        [
            Heading("settings.diff"),
            Check("DiffOptIgnoreSpace", "settings.diffIgnoreSpace", o.IgnoreWhitespace, (d, v) => d with { IgnoreWhitespace = v }),
            Check("DiffOptUnified", "settings.diffUnified", o.Unified, (d, v) => d with { Unified = v }),
            Row("settings.diffContext", Number("DiffOptContext", 0, 50, 1, o.ContextLines, (x, n) => x with { Diff = x.Diff with { ContextLines = n } })),
            Row("settings.diffPicture", mode),
        ];
    }
}
