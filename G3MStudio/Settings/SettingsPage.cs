using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using G3MStudio.Core.Commands;
using G3MStudio.Core.Projects;
using G3MStudio.Core.Settings;
using G3MStudio.Fonts;
using G3MStudio.Localization;

namespace G3MStudio.Settings;

/// <summary>
/// The Settings page (design/settings.md): a page in the window, not a dialog. Every control writes its setting at once; the window applies it.
/// The controls are named so tests can find them.
/// </summary>
public sealed class SettingsPage : UserControl
{
    private static readonly string[] Weights = ["", "Normal", "Medium", "SemiBold", "Bold"];

    private readonly SettingsStore _store;
    private readonly ProjectRegistry _registry;
    private readonly CommandRegistry _commands;
    private readonly UserFonts _userFonts;
    private string _fontMessage = "";
    private string _themeMessage = "";
    private string _search = "";
    private readonly Action _close;
    private bool _building;
    private int _section;

    public SettingsPage(SettingsStore store, ProjectRegistry registry, CommandRegistry commands, UserFonts userFonts, Action close)
    {
        (_store, _registry, _commands, _userFonts, _close) = (store, registry, commands, userFonts, close);
        Content = Build();
        // The labels follow the language; the page is built again when it changes.
        void Rebuild(object? _, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Loc.Current.Language)) Content = Build();
        }

        AttachedToVisualTree += (_, _) => Loc.Current.PropertyChanged += Rebuild;
        DetachedFromVisualTree += (_, _) => Loc.Current.PropertyChanged -= Rebuild;
    }

    private static TextBlock Heading(string key) => new() { Text = Loc.Current[key], FontSize = 16, Margin = new Thickness(0, 12, 0, 0), Classes = { "accent" } };

    private static StackPanel Row(string labelKey, params Control[] controls)
    {
        var line = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        foreach (var control in controls) line.Children.Add(control);
        return new StackPanel { Spacing = 4, Tag = Loc.Current[labelKey], Children = { new TextBlock { Text = Loc.Current[labelKey] }, line } };
    }

    /// <summary>
    /// Settings search: a row shows when its label contains the text, a whole section shows when its heading does.
    /// Children after the title and the search box belong to the heading before them.
    /// </summary>
    private static void Filter(StackPanel page, string text, int selected)
    {
        text = text.Trim();
        var sections = new List<(TextBlock Heading, List<Control> Items)>();
        foreach (var child in page.Children.Skip(2))
        {
            if (child is TextBlock { Text: not null } heading && heading.Classes.Contains("accent")) sections.Add((heading, []));
            else if (sections.Count > 0) sections[^1].Items.Add(child);
        }

        for (var index = 0; index < sections.Count; index++)
        {
            var (heading, items) = sections[index];
            // Without a search only the chosen section shows; a search looks in all of them.
            var headingHit = text.Length == 0 ? index == selected : heading.Text!.Contains(text, StringComparison.CurrentCultureIgnoreCase);
            var any = headingHit;
            foreach (var item in items)
            {
                item.IsVisible = headingHit || text.Length > 0 && item.Tag is string label && label.Contains(text, StringComparison.CurrentCultureIgnoreCase);
                any |= item.IsVisible;
            }

            heading.IsVisible = any;
        }
    }

    /// <summary>The accent and the code colors: a swatch, a #RRGGBB box and a reset button per color. A change is stored and shown at once.</summary>
    private StackPanel Colors()
    {
        static string HexOf(string token) =>
            Application.Current is { } app && app.TryGetResource(token, app.ActualThemeVariant, out var found) && found is SolidColorBrush { Color: var c } ? $"#{c.R:X2}{c.G:X2}{c.B:X2}" : "#000000";

        var block = new StackPanel { Spacing = 8 };
        var labels = new List<string>();
        foreach (var key in ThemeColors.Editable)
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
                var hex = custom ?? HexOf(key == ThemeColors.Accent ? "Accent.Fill" : key);
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
                var colors = ThemeFile.Read(await File.ReadAllTextAsync(path));
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

    private DockPanel Build()
    {
        var s = _store.Current;
        _building = true;
        var triggers = Enum.GetValues<Trigger>();
        var triggerNames = triggers.Select(t => Loc.Current["project.trigger." + t]).ToList();
        ComboBox Triggers(string name, Trigger value, Func<AppSettings, Trigger, AppSettings> set)
        {
            var box = new ComboBox { Name = name, ItemsSource = triggerNames, SelectedIndex = Array.IndexOf(triggers, value), MinWidth = 240 };
            box.SelectionChanged += (_, _) => { if (!_building && box.SelectedIndex >= 0) _store.Update(x => set(x, triggers[box.SelectedIndex])); };
            return box;
        }

        NumericUpDown Number(string name, int min, int max, int step, int value, Func<AppSettings, int, AppSettings> set)
        {
            var box = new NumericUpDown { Name = name, Minimum = min, Maximum = max, Increment = step, Value = value, FormatString = "0", Width = 120 };
            box.ValueChanged += (_, e) => { if (!_building && e.NewValue is { } n) _store.Update(x => set(x, (int)n)); };
            return box;
        }

        var languages = Loc.Current.Languages.OrderBy(l => l.Key, StringComparer.Ordinal).ToList();
        var language = new ComboBox { Name = "Language", ItemsSource = languages.Select(l => l.Value).ToList(), SelectedIndex = languages.FindIndex(l => l.Key == s.Language), MinWidth = 240 };
        language.SelectionChanged += (_, _) => { if (!_building && language.SelectedIndex >= 0) _store.Update(x => x with { Language = languages[language.SelectedIndex].Key }); };

        var fonts = new List<string> { "" };
        fonts.AddRange(FontManager.Current.SystemFonts.Select(f => f.Name).Concat(_userFonts.Families).Distinct().Order(StringComparer.CurrentCultureIgnoreCase));
        var family = new ComboBox { Name = "FontFamily", ItemsSource = fonts.Select(f => f.Length == 0 ? Loc.Current["settings.default"] : f).ToList(), SelectedIndex = Math.Max(0, fonts.IndexOf(s.FontFamily)), MinWidth = 240 };
        family.SelectionChanged += (_, _) => { if (!_building && family.SelectedIndex >= 0) _store.Update(x => x with { FontFamily = fonts[family.SelectedIndex] }); };

        var weight = new ComboBox { Name = "FontWeight", ItemsSource = Weights.Select(w => w.Length == 0 ? Loc.Current["settings.default"] : w).ToList(), SelectedIndex = Math.Max(0, Array.IndexOf(Weights, s.FontWeight)), MinWidth = 240 };
        weight.SelectionChanged += (_, _) => { if (!_building && weight.SelectedIndex >= 0) _store.Update(x => x with { FontWeight = Weights[weight.SelectedIndex] }); };

        var ask = new CheckBox { Name = "AskOnClose", Tag = Loc.Current["settings.askOnClose"], Content = Loc.Current["settings.askOnClose"], IsChecked = s.AskOnClose };
        ask.IsCheckedChanged += (_, _) => { if (!_building) _store.Update(x => x with { AskOnClose = ask.IsChecked == true }); };

        var codeWrap = new CheckBox { Name = "CodeWordWrap", Tag = Loc.Current["settings.codeWordWrap"], Content = Loc.Current["settings.codeWordWrap"], IsChecked = s.CodeWordWrap };
        codeWrap.IsCheckedChanged += (_, _) => { if (!_building) _store.Update(x => x with { CodeWordWrap = codeWrap.IsChecked == true }); };
        var codeNumbers = new CheckBox { Name = "CodeLineNumbers", Tag = Loc.Current["settings.codeLineNumbers"], Content = Loc.Current["settings.codeLineNumbers"], IsChecked = s.CodeLineNumbers };
        codeNumbers.IsCheckedChanged += (_, _) => { if (!_building) _store.Update(x => x with { CodeLineNumbers = codeNumbers.IsChecked == true }); };

        var reduce = new CheckBox { Name = "ReduceMotion", Tag = Loc.Current["settings.reduceMotion"], Content = Loc.Current["settings.reduceMotion"], IsChecked = s.ReduceMotion };
        reduce.IsCheckedChanged += (_, _) => { if (!_building) _store.Update(x => x with { ReduceMotion = reduce.IsChecked == true }); };
        var compact = new CheckBox { Name = "CompactDensity", Tag = Loc.Current["settings.compact"], Content = Loc.Current["settings.compact"], IsChecked = s.CompactDensity };
        compact.IsCheckedChanged += (_, _) => { if (!_building) _store.Update(x => x with { CompactDensity = compact.IsChecked == true }); };

        var linkState = new TextBlock { Name = "LinkMessage", Classes = { "muted" }, TextWrapping = TextWrapping.Wrap };
        var registerLinks = new Button { Name = "RegisterLinks", Content = Loc.Current["settings.registerLinks"], IsEnabled = G3MStudio.Core.Links.LinkRegistration.Supported };
        registerLinks.Click += (_, _) =>
        {
            try
            {
                G3MStudio.Core.Links.LinkRegistration.Register(Environment.ProcessPath ?? "");
                linkState.Text = Loc.Current["settings.linksDone"];
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
            {
                linkState.Text = Loc.Current["settings.linksFailed"] + ": " + e.Message;
            }
        };
        var location = new TextBox { Name = "Location", Text = _registry.RememberedRoot ?? "", MinWidth = 360 };
        location.TextChanged += (_, _) => { if (!_building && !string.IsNullOrWhiteSpace(location.Text)) _registry.RememberRoot(location.Text.Trim()); };
        var browse = new Button { Content = Loc.Current["project.location.browse"] };
        browse.Click += async (_, _) =>
        {
            if (TopLevel.GetTopLevel(this) is not { } top) return;
            var folders = await top.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions());
            if (folders is [{ } folder, ..] && folder.TryGetLocalPath() is { } picked) location.Text = picked;
        };

        var runner = new TextBox { Name = "RunRunner", Text = s.RunRunner, MinWidth = 360 };
        runner.TextChanged += (_, _) => { if (!_building) _store.Update(x => x with { RunRunner = (runner.Text ?? "").Trim() }); };
        var runArguments = new TextBox { Name = "RunArguments", Text = s.RunArguments, MinWidth = 360 };
        runArguments.TextChanged += (_, _) => { if (!_building) _store.Update(x => x with { RunArguments = runArguments.Text ?? "" }); };
        var debugOutput = new CheckBox { Name = "RunDebugOutput", Tag = Loc.Current["settings.runDebugOutput"], Content = Loc.Current["settings.runDebugOutput"], IsChecked = s.RunDebugOutput };
        debugOutput.IsCheckedChanged += (_, _) => { if (!_building) _store.Update(x => x with { RunDebugOutput = debugOutput.IsChecked == true }); };

        var profiles = new ComboBox { Name = "RunProfiles", MinWidth = 200, ItemsSource = s.RunProfiles.Select(p => p.Name).ToList(), SelectedItem = s.RunProfiles.Any(p => p.Name == s.ActiveRunProfile) ? s.ActiveRunProfile : null };
        profiles.SelectionChanged += (_, _) =>
        {
            if (_building || profiles.SelectedItem is not string pick || _store.Current.RunProfiles.FirstOrDefault(p => p.Name == pick) is not { } profile) return;
            _store.Update(x => x with { ActiveRunProfile = pick, RunRunner = profile.Runner, RunArguments = profile.Arguments, RunDebugOutput = profile.DebugOutput });
            runner.Text = profile.Runner;
            runArguments.Text = profile.Arguments;
            debugOutput.IsChecked = profile.DebugOutput;
        };
        var profileName = new TextBox { Name = "RunProfileName", MinWidth = 160, PlaceholderText = Loc.Current["settings.profileName"] };
        var saveProfile = new Button { Name = "SaveRunProfile", Content = Loc.Current["settings.profileSave"] };
        saveProfile.Click += (_, _) =>
        {
            if (profileName.Text?.Trim() is not { Length: > 0 } name) return;
            var now = _store.Current;
            var saved = new RunProfile(name, now.RunRunner, now.RunArguments, now.RunDebugOutput);
            _store.Update(x => x with { RunProfiles = [.. x.RunProfiles.Where(p => p.Name != name), saved], ActiveRunProfile = name });
            Content = Build();
        };
        var deleteProfile = new Button { Name = "DeleteRunProfile", Content = Loc.Current["settings.profileDelete"] };
        deleteProfile.Click += (_, _) =>
        {
            if (profiles.SelectedItem is not string pick) return;
            _store.Update(x => x with { RunProfiles = [.. x.RunProfiles.Where(p => p.Name != pick)], ActiveRunProfile = "" });
            Content = Build();
        };

        var close = new Button { Name = "SettingsClose", Content = Loc.Current["settings.close"], HorizontalAlignment = HorizontalAlignment.Right };
        close.Click += (_, _) => _close();
        DockPanel.SetDock(close, Avalonia.Controls.Dock.Right);

        // The defaults come from a new AppSettings, one reset per section in the order of the headings below.
        var d = new AppSettings();
        Func<AppSettings, AppSettings>[] resets =
        [
            x => x with { UiScale = d.UiScale, FontFamily = d.FontFamily, FontWeight = d.FontWeight, CompactDensity = d.CompactDensity, ReduceMotion = d.ReduceMotion },
            x => x with { Colors = d.Colors },
            x => x with { CodeFontSize = d.CodeFontSize, CodeWordWrap = d.CodeWordWrap, CodeLineNumbers = d.CodeLineNumbers },
            x => x with { BackupCount = d.BackupCount, AskOnClose = d.AskOnClose },
            x => x with { Project = d.Project },
            x => x with { RunRunner = d.RunRunner, RunArguments = d.RunArguments, RunDebugOutput = d.RunDebugOutput, ActiveRunProfile = d.ActiveRunProfile },
            x => x with { Shortcuts = d.Shortcuts },
        ];
        var restore = new Button { Name = "SettingsRestoreDefaults", Content = Loc.Current["settings.restore"], Margin = new Thickness(0, 0, 8, 0), HorizontalAlignment = HorizontalAlignment.Right };
        ToolTip.SetTip(restore, Loc.Current["settings.restoreTip"]);
        restore.Click += (_, _) =>
        {
            _store.Update(resets[Math.Clamp(_section, 0, resets.Length - 1)]);
            Content = Build();
        };
        DockPanel.SetDock(restore, Avalonia.Controls.Dock.Right);

        var search = new TextBox { Name = "SettingsSearch", Text = _search, PlaceholderText = Loc.Current["settings.search"] };
        var page = new StackPanel
        {
            MaxWidth = 640,
            Margin = new Thickness(24),
            Spacing = 10,
            Children =
            {
                new DockPanel { Children = { close, restore, new TextBlock { Text = Loc.Current["settings.title"], FontSize = 20 } } },
                search,
                Heading("settings.appearance"),
                Row("settings.language", language),
                Row("settings.scale", Number("UiScale", 50, 200, 10, s.UiScale, (x, n) => x with { UiScale = n }), new TextBlock { Text = "%", VerticalAlignment = VerticalAlignment.Center }),
                Row("settings.fontFamily", family),
                Row("settings.fontWeight", weight),
                compact,
                reduce,
                FontFiles(),
                Heading("settings.colors"),
                Colors(),
                Heading("settings.editor"),
                Row("settings.codeFontSize", Number("CodeFontSize", 8, 32, 1, s.CodeFontSize, (x, n) => x with { CodeFontSize = n }), new TextBlock { Text = "px", VerticalAlignment = VerticalAlignment.Center }),
                codeWrap,
                codeNumbers,
                Heading("settings.files"),
                Row("settings.backups", Number("Backups", 1, 3, 1, s.BackupCount, (x, n) => x with { BackupCount = n })),
                ask,
                new StackPanel { Spacing = 4, Tag = Loc.Current["settings.registerLinks"], Children = { registerLinks, linkState } },
                Row("settings.location", location, browse),
                Heading("settings.projects"),
                Row("settings.autosave", Triggers("AutoSave", s.Project.AutoSave, (x, t) => x with { Project = x.Project with { AutoSave = t } }),
                    Number("AutoSaveSeconds", 1, 3600, 1, s.Project.AutoSaveSeconds, (x, n) => x with { Project = x.Project with { AutoSaveSeconds = n } }), new TextBlock { Text = Loc.Current["project.settings.seconds"], VerticalAlignment = VerticalAlignment.Center }),
                Row("settings.autosync", Triggers("AutoSync", s.Project.AutoSync, (x, t) => x with { Project = x.Project with { AutoSync = t } }),
                    Number("AutoSyncSeconds", 1, 3600, 1, s.Project.AutoSyncSeconds, (x, n) => x with { Project = x.Project with { AutoSyncSeconds = n } }), new TextBlock { Text = Loc.Current["project.settings.seconds"], VerticalAlignment = VerticalAlignment.Center }),
                new TextBlock { Text = Loc.Current["settings.projectsNote"], Classes = { "muted" }, TextWrapping = TextWrapping.Wrap },
                Heading("settings.run"),
                Row("settings.runRunner", runner),
                Row("settings.runArguments", runArguments),
                debugOutput,
                Row("settings.profiles", profiles, deleteProfile),
                new TextBlock { Text = Loc.Current["settings.profilesNote"], Classes = { "muted" }, TextWrapping = TextWrapping.Wrap },
                Row("settings.profileName", profileName, saveProfile),
                Heading("settings.shortcuts"),
                new ShortcutsEditor(_commands, _store),
            },
        };
        // The categories are the headings of the page, listed on the left.
        var nav = new ListBox { Name = "SettingsNav", Width = 200, BorderThickness = new Thickness(0), Margin = new Thickness(16, 0, 0, 0), Background = Avalonia.Media.Brushes.Transparent, ItemsSource = page.Children.OfType<TextBlock>().Where(t => t.Classes.Contains("accent")).Select(t => t.Text).ToList(), SelectedIndex = Math.Max(0, _section) };
        nav.SelectionChanged += (_, _) =>
        {
            _section = nav.SelectedIndex;
            Filter(page, search.Text ?? "", _section);
        };
        search.TextChanged += (_, _) => Filter(page, _search = search.Text ?? "", _section);
        Filter(page, _search, Math.Max(0, _section));
        _building = false;
        var layout = new DockPanel { Margin = new Thickness(0, 24, 0, 0) };
        DockPanel.SetDock(nav, Avalonia.Controls.Dock.Left);
        layout.Children.Add(nav);
        layout.Children.Add(new ScrollViewer { Content = page });
        return layout;
    }
}
