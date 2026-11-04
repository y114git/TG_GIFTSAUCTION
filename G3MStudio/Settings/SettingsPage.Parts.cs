using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using G3MStudio.Core.Settings;
using G3MStudio.Core.Workspace;
using G3MStudio.Fonts;
using G3MStudio.Localization;

namespace G3MStudio.Settings;

public sealed partial class SettingsPage
{
    // One row per decompiler setting of the library (UTMT-GMLS-01 to GMLS-08): a check box for a switch, a box for a text value. A value equal to the default is not stored, so the file only holds what the user changed.
    private List<Control> DecompilerRows(AppSettings s)
    {
        var rows = new List<Control>();
        foreach (var field in DecompilerOptions.Fields)
        {
            var name = field.Name;
            var label = DecompilerOptions.Label(name);
            var current = s.Decompiler.TryGetValue(name, out var saved) && DecompilerOptions.Valid(field, saved) ? saved : DecompilerOptions.DefaultOf(field);
            void Store(string value)
            {
                current = value;
                _store.Update(x => x with { Decompiler = DecompilerOptions.DefaultOf(field) == value ? x.Decompiler.Where(p => p.Key != name).ToDictionary(p => p.Key, p => p.Value) : new Dictionary<string, string>(x.Decompiler) { [name] = value } });
            }

            if (DecompilerOptions.IsText(field))
            {
                var box = new TextBox { Name = "Decompiler_" + name, Text = current, MinWidth = 220, Tag = label };
                box.LostFocus += (_, _) =>
                {
                    if (_building || box.Text == current) return;
                    if (DecompilerOptions.Valid(field, box.Text ?? "")) Store(box.Text ?? "");
                    else box.Text = current;
                };
                rows.Add(new StackPanel { Spacing = 4, Tag = label, Children = { new TextBlock { Text = label }, box } });
            }
            else
            {
                rows.Add(DecompilerSwitch(name, label, current == "true", Store));
            }
        }

        return rows;
    }

    private CheckBox DecompilerSwitch(string name, string label, bool on, Action<string> store)
    {
        var check = new CheckBox { Name = "Decompiler_" + name, Content = label, Tag = label, IsChecked = on };
        check.IsCheckedChanged += (_, _) => { if (!_building) store(check.IsChecked == true ? "true" : "false"); };
        return check;
    }

    /// <summary>The accent and the code colors: a swatch, a #RRGGBB box and a reset button per color. A change is stored and shown at once.</summary>
    private StackPanel Colors()
    {
        static string HexOf(string token) =>
            Application.Current is { } app && app.TryGetResource(token, app.ActualThemeVariant, out var found) && found is SolidColorBrush { Color: var c } ? $"#{c.R:X2}{c.G:X2}{c.B:X2}" : "#000000";

        var block = new StackPanel { Spacing = 8 };
        var labels = new List<string>();
        foreach (var (group, keys) in ThemeColors.Groups)
        {
            block.Children.Add(new TextBlock { Text = Loc.Current[group], Classes = { "muted" }, Margin = new Thickness(0, 8, 0, 0) });
            foreach (var key in keys)
            {
                var label = Loc.Current["colors." + key];
                labels.Add(label);
                var swatch = new Border { Name = "ColorSwatch_" + key, Width = 24, Height = 24, CornerRadius = new CornerRadius(4) };
                var box = new TextBox { Name = "Color_" + key, Width = 110, MaxLength = 7 };
                var reset = new Button { Name = "ColorReset_" + key, Content = Loc.Current["settings.reset"] };
                void Show()
                {
                    var changed = _store.Current.Colors.TryGetValue(key, out var custom);
                    // The theme token holds the color in use: the user's, or the default once the override is taken away.
                    var hex = custom ?? HexOf(ThemeColors.Tokens(key).First());
                    box.Text = hex;
                    swatch.Background = new SolidColorBrush(Color.Parse(hex));
                    reset.IsEnabled = changed;
                }

                void Commit()
                {
                    var text = box.Text?.Trim() ?? "";
                    if (ThemeColors.IsValid(text)) _store.Update(x => x with { Colors = new Dictionary<string, string>(x.Colors) { [key] = text.ToUpperInvariant() } });
                    Show(); // a value that is not #RRGGBB goes back to the color in use
                }

                box.LostFocus += (_, _) => Commit();
                box.KeyDown += (_, e) =>
                {
                    if (e.Key == Avalonia.Input.Key.Enter) Commit();
                };
                reset.Click += (_, _) =>
                {
                    _store.Update(x => x with { Colors = x.Colors.Where(p => p.Key != key).ToDictionary() });
                    Show();
                };
                Show();
                block.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { new TextBlock { Text = label, Width = 200, VerticalAlignment = VerticalAlignment.Center }, swatch, box, reset } });
            }
        }

        var export = new Button { Name = "ThemeExport", Content = Loc.Current["settings.themeExport"] };
        export.Click += async (_, _) =>
        {
            if (TopLevel.GetTopLevel(this) is not { } top) return;
            var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions { Title = Loc.Current["settings.themeExport"], DefaultExtension = "json", SuggestedFileName = "g3mstudio-theme.json" });
            if (file?.TryGetLocalPath() is { } path) await File.WriteAllTextAsync(path, ThemeFile.Write(_store.Current.Colors));
        };
        var import = new Button { Name = "ThemeImport", Content = Loc.Current["settings.themeImport"] };
        import.Click += async (_, _) =>
        {
            if (TopLevel.GetTopLevel(this) is not { } top) return;
            var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = Loc.Current["settings.themeImport"], AllowMultiple = false });
            if (files is not [{ } picked, ..] || picked.TryGetLocalPath() is not { } path) return;
            try
            {
                var colors = ThemeFile.ReadFile(path);
                _store.Update(x => x with { Colors = colors });
                Content = Build(); // the boxes show the imported colors
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException)
            {
                _themeMessage = Loc.Current.Format("settings.themeBad", ("reason", e.Message));
                Content = Build();
            }
        };
        block.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { export, import } });
        block.Children.Add(new TextBlock { Name = "ThemeNote", Text = _themeMessage, Classes = { "muted" }, TextWrapping = TextWrapping.Wrap });
        block.Tag = string.Join(' ', labels);
        return block;
    }

    /// <summary>Fonts of the user's own: add a font file, remove one (Q56).</summary>
    private StackPanel FontFiles()
    {
        var note = new TextBlock { Name = "FontNote", Text = _fontMessage, Classes = { "muted" }, TextWrapping = TextWrapping.Wrap };
        var add = new Button { Name = "AddFont", Content = Loc.Current["settings.addFont"] };
        add.Click += async (_, _) =>
        {
            if (TopLevel.GetTopLevel(this) is not { } top) return;
            var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { AllowMultiple = true, FileTypeFilter = [new FilePickerFileType(Loc.Current["settings.fontFiles"]) { Patterns = UserFonts.FilePatterns }] });
            foreach (var path in files.Select(f => f.TryGetLocalPath()).OfType<string>()) AddFont(path);
        };
        var list = new StackPanel { Spacing = 2 };
        foreach (var family in _userFonts.Families.Order(StringComparer.CurrentCultureIgnoreCase))
        {
            var remove = new Button { Name = "RemoveFont_" + family, Content = Loc.Current["settings.removeFont"] };
            remove.Click += (_, _) => RemoveFont(family);
            DockPanel.SetDock(remove, Avalonia.Controls.Dock.Right);
            list.Children.Add(new DockPanel { Children = { remove, new TextBlock { Text = family, VerticalAlignment = VerticalAlignment.Center } } });
        }

        return new StackPanel { Spacing = 4, Tag = Loc.Current["settings.addFont"], Children = { add, note, list } };
    }

    /// <summary>Adds one font file and picks its first family; a file that is no font leaves a note.</summary>
    public void AddFont(string path)
    {
        var added = _userFonts.Add(path);
        _fontMessage = added.Count == 0 ? Loc.Current.Format("settings.notAFont", ("file", Path.GetFileName(path))) : "";
        if (added.Count > 0) _store.Update(x => x with { FontFamily = added[0] });
        Content = Build();
    }

    private void RemoveFont(string family)
    {
        _userFonts.Remove(family);
        if (_store.Current.FontFamily == family) _store.Update(x => x with { FontFamily = "" });
        Content = Build();
    }
}
