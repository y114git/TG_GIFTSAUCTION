using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using G3MStudio.Core.Settings;
using G3MStudio.Localization;
using G3MStudio.Theme;

namespace G3MStudio.Settings;

public sealed partial class SettingsPage
{
    private List<string> FontChoices()
    {
        var fonts = new List<string> { "" };
        fonts.AddRange(FontManager.Current.SystemFonts.Select(f => f.Name).Concat(_userFonts.Families).Distinct().Order(StringComparer.CurrentCultureIgnoreCase));
        return fonts;
    }

    private Control[] AppearanceSection(AppSettings s)
    {
        var languages = Loc.Current.Languages.OrderBy(l => l.Key, StringComparer.Ordinal).ToList();
        var language = new ComboBox { Name = "Language", ItemsSource = languages.Select(l => l.Value).ToList(), SelectedIndex = languages.FindIndex(l => l.Key == s.Language), MinWidth = Wide(15) };
        language.SelectionChanged += (_, _) => { if (!_building && language.SelectedIndex >= 0) _store.Update(x => x with { Language = languages[language.SelectedIndex].Key }); };

        var fonts = FontChoices();
        var family = new ComboBox { Name = "FontFamily", ItemsSource = fonts.Select(f => f.Length == 0 ? Loc.Current["settings.default"] : f).ToList(), SelectedIndex = Math.Max(0, fonts.IndexOf(s.FontFamily)), MinWidth = Wide(15) };
        family.SelectionChanged += (_, _) => { if (!_building && family.SelectedIndex >= 0) _store.Update(x => x with { FontFamily = fonts[family.SelectedIndex] }); };

        var weight = new ComboBox { Name = "FontWeight", ItemsSource = Weights.Select(w => w.Length == 0 ? Loc.Current["settings.default"] : w).ToList(), SelectedIndex = Math.Max(0, Array.IndexOf(Weights, s.FontWeight)), MinWidth = Wide(15) };
        weight.SelectionChanged += (_, _) => { if (!_building && weight.SelectedIndex >= 0) _store.Update(x => x with { FontWeight = Weights[weight.SelectedIndex] }); };

        var showNulls = new CheckBox { Name = "ShowNullEntries", Tag = Loc.Current["settings.showNullEntries"], Content = Loc.Current["settings.showNullEntries"], IsChecked = s.ShowNullEntries };
        showNulls.IsCheckedChanged += (_, _) => { if (!_building) _store.Update(x => x with { ShowNullEntries = showNulls.IsChecked == true }); };

        var hoverPreviews = new CheckBox { Name = "HoverPreviews", Tag = Loc.Current["settings.hoverPreviews"], Content = Loc.Current["settings.hoverPreviews"], IsChecked = s.HoverPreviews };
        hoverPreviews.IsCheckedChanged += (_, _) => { if (!_building) _store.Update(x => x with { HoverPreviews = hoverPreviews.IsChecked == true }); };

        var levels = Core.Settings.MotionPolicy.Levels;
        // Item 0 is the unchosen state (the OS decides); the levels follow it.
        var animations = new ComboBox { Name = "Animations", ItemsSource = levels.Select(l => Loc.Current["settings.animationsName." + l]).Prepend($"{Loc.Current["settings.animationsName.System"]} ({Loc.Current["settings.animationsName." + Core.Settings.MotionPolicy.Resolve("", Motion.OsReducesMotion())]})").ToList(), SelectedIndex = s.ChosenAnimations.Length == 0 ? 0 : Array.IndexOf(levels, s.ChosenAnimations) + 1, MinWidth = Wide(15) };
        animations.SelectionChanged += (_, _) => { if (!_building && animations.SelectedIndex >= 0) _store.Update(x => x with { Animations = animations.SelectedIndex == 0 ? "" : levels[animations.SelectedIndex - 1], ReduceMotion = false }); };

        var places = Enum.GetValues<Core.Settings.TabButtonsPlace>();
        var tabButtons = new ComboBox { Name = "TabButtons", ItemsSource = places.Select(p => Loc.Current["settings.tabButtonsName." + p]).ToList(), SelectedIndex = Array.IndexOf(places, s.TabButtons), MinWidth = Wide(15) };
        tabButtons.SelectionChanged += (_, _) => { if (!_building && tabButtons.SelectedIndex >= 0) _store.Update(x => x with { TabButtons = places[tabButtons.SelectedIndex] }); };

        var helpLevels = Enum.GetValues<Core.Settings.HelpLevel>();
        var helpText = new ComboBox { Name = "HelpText", ItemsSource = helpLevels.Select(l => Loc.Current["settings.helpTextName." + l]).ToList(), SelectedIndex = Array.IndexOf(helpLevels, s.HelpLevel), MinWidth = Wide(15) };
        ToolTip.SetTip(helpText, Loc.Current["settings.helpTextTip"]);
        helpText.SelectionChanged += (_, _) => { if (!_building && helpText.SelectedIndex >= 0) _store.Update(x => x with { HelpLevel = helpLevels[helpText.SelectedIndex] }); };

        var sizePanel = new StackPanel { Spacing = Mods.ModsUi.Space(3), Tag = Loc.Current["settings.sizes"] };
        sizePanel.Children.Add(new TextBlock { Text = Loc.Current["settings.sizes"], Classes = { "muted" }, Margin = new Thickness(0, 8, 0, 0) });
        foreach (var (sizeKey, min, max, _) in ThemeSizes.All)
        {
            var key = sizeKey;
            sizePanel.Children.Add(Row("settings.size." + key, Number("Size_" + key, min, max, 1, ThemeSizes.Value(s.Sizes, s.Density, key), (x, n) => x with { Sizes = new Dictionary<string, int>(x.Sizes) { [key] = n } }), new TextBlock { Text = key == "Space.Scale" ? "%" : Loc.Current["settings.pixelUnit"], VerticalAlignment = VerticalAlignment.Center }));
        }

        return
        [
            Heading("settings.appearance"),
            Row("settings.language", language),
            .. ScaleRows(s),
            DensityRow(s),
            Row("settings.fontFamily", family),
            Row("settings.fontWeight", weight),
            sizePanel,
            Row("settings.animations", animations),
            Row("settings.tabButtons", tabButtons),
            Row("settings.helpText", helpText),
            hoverPreviews,
            showNulls,
            .. ExplorerSection(s),
            FontFiles(),
        ];
    }

    /// <summary>The interface scale: Auto (computed from the screen) or a percent. Either applies to the window at once, so the page itself is the preview.</summary>
    private Control[] ScaleRows(AppSettings s)
    {
        var screen = _screen();
        var values = new List<int>();
        var box = new ComboBox { Name = "UiScale", MinWidth = Wide(15) };
        var info = new TextBlock { Name = "UiScaleInfo", Classes = { "muted" }, [HelpText.KindProperty] = HelpKind.Detail };
        // Item 0 is Auto; a saved value outside the presets (Ctrl+wheel writes any percent) shows in its sorted place.
        void Fill(int scale)
        {
            values.Clear();
            values.Add(0);
            values.AddRange(ScalePresets.Append(scale).Where(v => v > 0).Distinct().Order());
            var was = _building; // Build() holds the flag while it makes the page
            _building = true;
            box.ItemsSource = values.Select(v => v == 0 ? Loc.Current["settings.scaleAuto"] : v + " %").ToList();
            box.SelectedIndex = values.IndexOf(scale);
            _building = was;
            info.Text = ScaleInfo(scale, screen);
        }

        Fill(s.UiScale);
        box.SelectionChanged += (_, _) =>
        {
            if (_building || box.SelectedIndex < 0) return;
            _store.Update(x => x with { UiScale = values[box.SelectedIndex] });
            info.Text = ScaleInfo(_store.Current.UiScale, screen);
            // The extra item of a value outside the presets goes once another one is chosen; the list is rebuilt after this handler, not while the drop-down closes.
            if (values.Any(v => v > 0 && !ScalePresets.Contains(v) && v != _store.Current.UiScale)) Dispatcher.UIThread.Post(() => Fill(_store.Current.UiScale));
        };
        // Ctrl+wheel and Ctrl+0 change the scale while the page is open.
        void Follow() { if (values[Math.Max(box.SelectedIndex, 0)] != _store.Current.UiScale) Fill(_store.Current.UiScale); }
        box.AttachedToVisualTree += (_, _) => _store.Changed += Follow;
        box.DetachedFromVisualTree += (_, _) => _store.Changed -= Follow;
        return [Row("settings.scale", box), info];
    }

    internal static readonly int[] ScalePresets = [50, 60, 70, 75, 80, 90, 100, 110, 125, 150, 175, 200];

    private static string ScaleInfo(int uiScale, ScreenInfo screen) => Loc.Current.Format("settings.scaleInfo", ("percent", UiSizing.Effective(uiScale, screen)));

    private StackPanel DensityRow(AppSettings s)
    {
        var density = new ComboBox { Name = "Density", ItemsSource = UiSizing.Densities.Select(d => Loc.Current["settings.densityName." + d]).ToList(), SelectedIndex = Array.IndexOf(UiSizing.Densities, UiSizing.NormalDensity(s.Density)), MinWidth = Wide(15) };
        density.SelectionChanged += (_, _) =>
        {
            if (_building || density.SelectedIndex < 0) return;
            _store.Update(x => x with { Density = UiSizing.Densities[density.SelectedIndex] });
            Content = Build(); // the size boxes below show the values of the new density
        };
        return Row("settings.density", density);
    }

    private Control[] ColorsSection()
    {
        return
        [
            Heading("settings.colors"),
            Colors(),
        ];
    }

    private Control[] EditorSection(AppSettings s)
    {
        var fonts = FontChoices();
        var codeFamily = new ComboBox { Name = "CodeFontFamily", ItemsSource = fonts.Select(f => f.Length == 0 ? Loc.Current["settings.default"] : f).ToList(), SelectedIndex = Math.Max(0, fonts.IndexOf(s.CodeFontFamily)), MinWidth = Wide(15) };
        codeFamily.SelectionChanged += (_, _) => { if (!_building && codeFamily.SelectedIndex >= 0) _store.Update(x => x with { CodeFontFamily = fonts[codeFamily.SelectedIndex] }); };

        var codeWrap = new CheckBox { Name = "CodeWordWrap", Tag = Loc.Current["settings.codeWordWrap"], Content = Loc.Current["settings.codeWordWrap"], IsChecked = s.CodeWordWrap };
        codeWrap.IsCheckedChanged += (_, _) => { if (!_building) _store.Update(x => x with { CodeWordWrap = codeWrap.IsChecked == true }); };
        var codeNumbers = new CheckBox { Name = "CodeLineNumbers", Tag = Loc.Current["settings.codeLineNumbers"], Content = Loc.Current["settings.codeLineNumbers"], IsChecked = s.CodeLineNumbers };
        codeNumbers.IsCheckedChanged += (_, _) => { if (!_building) _store.Update(x => x with { CodeLineNumbers = codeNumbers.IsChecked == true }); };

        var codeComplete = new CheckBox { Name = "CodeAutoComplete", Tag = Loc.Current["settings.codeAutoComplete"], Content = Loc.Current["settings.codeAutoComplete"], IsChecked = s.CodeAutoComplete };
        codeComplete.IsCheckedChanged += (_, _) => { if (!_building) _store.Update(x => x with { CodeAutoComplete = codeComplete.IsChecked == true }); };

        var codeGuides = new CheckBox { Name = "CodeIndentGuides", Tag = Loc.Current["settings.codeIndentGuides"], Content = Loc.Current["settings.codeIndentGuides"], IsChecked = s.CodeIndentGuides };
        codeGuides.IsCheckedChanged += (_, _) => { if (!_building) _store.Update(x => x with { CodeIndentGuides = codeGuides.IsChecked == true }); };

        var codeBrackets = new CheckBox { Name = "CodeBracketColors", Tag = Loc.Current["settings.codeBracketColors"], Content = Loc.Current["settings.codeBracketColors"], IsChecked = s.CodeBracketColors };
        codeBrackets.IsCheckedChanged += (_, _) => { if (!_building) _store.Update(x => x with { CodeBracketColors = codeBrackets.IsChecked == true }); };

        var codeLigatures = new CheckBox { Name = "CodeLigatures", Tag = Loc.Current["settings.codeLigatures"], Content = Loc.Current["settings.codeLigatures"], IsChecked = s.CodeLigatures };
        codeLigatures.IsCheckedChanged += (_, _) => { if (!_building) _store.Update(x => x with { CodeLigatures = codeLigatures.IsChecked == true }); };

        var codeLive = new CheckBox { Name = "CodeLiveCheck", Tag = Loc.Current["settings.codeLiveCheck"], Content = Loc.Current["settings.codeLiveCheck"], IsChecked = s.CodeLiveCheck };
        codeLive.IsCheckedChanged += (_, _) => { if (!_building) _store.Update(x => x with { CodeLiveCheck = codeLive.IsChecked == true }); };

        var assetModes = new[] { AssetNumberMode.Off, AssetNumberMode.Manual, AssetNumberMode.Auto };
        var assetMode = new ComboBox
        {
            Name = "CodeAssetNumbers",
            ItemsSource = new[] { Loc.Current["settings.codeAssetNumbersOff"], Loc.Current["settings.codeAssetNumbersManual"], Loc.Current["settings.codeAssetNumbersAuto"] },
            SelectedIndex = Math.Max(0, Array.IndexOf(assetModes, s.CodeAssetNumbers)),
            MinWidth = Wide(15),
        };
        assetMode.SelectionChanged += (_, _) => { if (!_building && assetMode.SelectedIndex >= 0) _store.Update(x => x with { CodeAssetNumbers = assetModes[assetMode.SelectedIndex] }); };
        var assetGame = new CheckBox { Name = "CodeAssetNumbersGameFunctions", Tag = Loc.Current["settings.codeAssetNumbersGame"], Content = Loc.Current["settings.codeAssetNumbersGame"], IsChecked = s.CodeAssetNumbersGameFunctions };
        assetGame.IsCheckedChanged += (_, _) => { if (!_building) _store.Update(x => x with { CodeAssetNumbersGameFunctions = assetGame.IsChecked == true }); };
        var assetTypes = new WrapPanel { Tag = Loc.Current["settings.codeAssetNumberTypes"], ItemSpacing = 12, LineSpacing = 4 };
        foreach (var type in AssetNumberTypes.All)
        {
            var key = type;
            var box = new CheckBox { Name = "CodeAssetNumberType_" + key, Content = Loc.Current["explorer.type." + key], IsChecked = s.CodeAssetNumberTypes.Contains(key) };
            box.IsCheckedChanged += (_, _) =>
            {
                if (_building) return;
                _store.Update(x => x with { CodeAssetNumberTypes = [.. AssetNumberTypes.All.Where(t => t == key ? box.IsChecked == true : x.CodeAssetNumberTypes.Contains(t))] });
            };
            assetTypes.Children.Add(box);
        }

        return
        [
            Heading("settings.editor"),
            Row("settings.codeFontFamily", codeFamily),
            Row("settings.codeFontSize", Number("CodeFontSize", 8, 32, 1, s.CodeFontSize, (x, n) => x with { CodeFontSize = n }), new TextBlock { Text = Loc.Current["settings.pixelUnit"], VerticalAlignment = VerticalAlignment.Center }),
            codeWrap,
            codeNumbers,
            codeComplete,
            codeGuides,
            codeBrackets,
            codeLigatures,
            codeLive,
            Row("settings.codeAssetNumbers", assetMode),
            assetGame,
            Row("settings.codeAssetNumberTypes", assetTypes),
            Row("settings.codeTabWidth", Number("CodeTabWidth", 2, 8, 1, s.CodeTabWidth, (x, n) => x with { CodeTabWidth = n })),
            Row("settings.codeHoverDelay", Number("CodeHoverDelay", Editor.EditorOptions.MinHoverDelayMs, Editor.EditorOptions.MaxHoverDelayMs, 100, s.CodeHoverDelayMs, (x, n) => x with { CodeHoverDelayMs = n }), new TextBlock { Text = Loc.Current["settings.msUnit"], VerticalAlignment = VerticalAlignment.Center }),
        ];
    }

    private Control[] RoomsSection(AppSettings s)
    {
        var gridOverride = new CheckBox { Name = "RoomGridOverride", Tag = Loc.Current["settings.roomGridOverride"], Content = Loc.Current["settings.roomGridOverride"], IsChecked = s.RoomGridOverride };
        gridOverride.IsCheckedChanged += (_, _) => { if (!_building) _store.Update(x => x with { RoomGridOverride = gridOverride.IsChecked == true }); };

        return
        [
            Heading("settings.rooms"),
            gridOverride,
            Row("settings.roomGridSize", Number("RoomGridWidth", 1, 1024, 1, s.RoomGridWidth, (x, n) => x with { RoomGridWidth = n }), Number("RoomGridHeight", 1, 1024, 1, s.RoomGridHeight, (x, n) => x with { RoomGridHeight = n })),
            Heading("settings.autoTile"),
            AutoTileCheck("AutoTileOutside", "settings.autoTileOutside", s.AutoTile.OutsideIsTerrain, (o, v) => o with { OutsideIsTerrain = v }),
            AutoTileCheck("AutoTileStart", "settings.autoTileStart", s.AutoTile.StartOn, (o, v) => o with { StartOn = v }),
            Row("settings.autoTileLayout", Number("AutoTileLayoutDefault", 0, 2, 1, (int)s.AutoTile.DefaultLayout, (x, n) => x with { AutoTile = x.AutoTile with { DefaultLayout = (G3MStudio.Core.Workspace.AutoTileLayout)n } })),
        ];
    }

    private CheckBox AutoTileCheck(string name, string key, bool value, Func<AutoTileOptions, bool, AutoTileOptions> set)
    {
        var box = new CheckBox { Name = name, Tag = Loc.Current[key], Content = Loc.Current[key], IsChecked = value };
        box.IsCheckedChanged += (_, _) => { if (!_building) _store.Update(x => x with { AutoTile = set(x.AutoTile, box.IsChecked == true) }); };
        return box;
    }

    private Control[] DecompilerSection(AppSettings s)
    {
        var decompiler = new StackPanel { Name = "DecompilerSettings", Spacing = Mods.ModsUi.Space(3) };
        foreach (var row in DecompilerRows(s)) decompiler.Children.Add(row);

        return
        [
            Heading("settings.decompiler"),
            decompiler,
        ];
    }

    private Control[] FilesSection(AppSettings s)
    {
        var ask = new CheckBox { Name = "AskOnClose", Tag = Loc.Current["settings.askOnClose"], Content = Loc.Current["settings.askOnClose"], IsChecked = s.AskOnClose };
        ask.IsCheckedChanged += (_, _) => { if (!_building) _store.Update(x => x with { AskOnClose = ask.IsChecked == true }); };

        var rememberWindow = new CheckBox { Name = "RememberWindow", Tag = Loc.Current["settings.rememberWindow"], Content = Loc.Current["settings.rememberWindow"], IsChecked = s.RememberWindow };
        rememberWindow.IsCheckedChanged += (_, _) => { if (!_building) _store.Update(x => x with { RememberWindow = rememberWindow.IsChecked == true }); };

        var places = G3MStudio.Core.Links.Places.Real(_store.UserData);
        var assocState = new TextBlock { Name = "AssociationMessage", Classes = { "muted" }, TextWrapping = TextWrapping.Wrap };
        string AssocText() => G3MStudio.Core.Links.FileAssociations.Registered(places) is { Count: > 0 } now ? Loc.Current.Format("settings.assocNow", ("types", string.Join(" ", now))) : Loc.Current.Format("settings.assocNone", ("button", Loc.Current["settings.assocStop"]));
        assocState.Text = G3MStudio.Core.Links.FileAssociations.Supported ? AssocText() : Loc.Current["settings.assocWindows"];
        var useStudio = new Button { Name = "AssociateFiles", Content = Loc.Current["settings.assocUse"], IsEnabled = G3MStudio.Core.Links.FileAssociations.Supported };
        var dropStudio = new Button { Name = "DissociateFiles", Content = Loc.Current["settings.assocStop"], IsEnabled = G3MStudio.Core.Links.FileAssociations.Supported };
        useStudio.Click += (_, _) =>
        {
            var error = G3MStudio.Core.Links.FileAssociations.TryRegister(Environment.ProcessPath ?? "", places);
            assocState.Text = error is null ? AssocText() : Loc.Current["settings.linksFailed"] + ": " + error;
        };
        dropStudio.Click += (_, _) =>
        {
            var error = G3MStudio.Core.Links.FileAssociations.TryRemove(places);
            assocState.Text = error is null ? AssocText() : Loc.Current["settings.linksFailed"] + ": " + error;
        };
        var startup = Modes("StartupMode", Enum.GetValues<StartupMode>(), ["settings.startupRestore", "settings.startupHome", "settings.startupAsk"], s.Startup, (x, m) => x with { Startup = m });
        var mismatch = Modes("HashMismatch", Enum.GetValues<MismatchPolicy>(), ["settings.mismatchAsk", "settings.mismatchKeep", "settings.mismatchFresh", "settings.mismatchRefuse"], s.HashMismatch, (x, m) => x with { HashMismatch = m });
        var lostSearch = new CheckBox { Name = "LostFileSearch", Tag = Loc.Current["settings.lostSearch"], Content = Loc.Current["settings.lostSearch"], IsChecked = s.LostFileSearch };
        lostSearch.IsCheckedChanged += (_, _) => { if (!_building) _store.Update(x => x with { LostFileSearch = lostSearch.IsChecked == true }); };
        var lostAuto = new CheckBox { Name = "LostFileAutoUse", Tag = Loc.Current["settings.lostAuto"], Content = Loc.Current["settings.lostAuto"], IsChecked = s.LostFileAutoUse };
        lostAuto.IsCheckedChanged += (_, _) => { if (!_building) _store.Update(x => x with { LostFileAutoUse = lostAuto.IsChecked == true }); };
        var lostForget = new CheckBox { Name = "LostFileForgetOld", Tag = Loc.Current["settings.lostForget"], Content = Loc.Current["settings.lostForget"], IsChecked = s.LostFileForgetOld };
        lostForget.IsCheckedChanged += (_, _) => { if (!_building) _store.Update(x => x with { LostFileForgetOld = lostForget.IsChecked == true }); };
        var location = new TextBox { Name = "Location", Text = _registry.RememberedRoot ?? "", MinWidth = Wide(22.5) };
        location.TextChanged += (_, _) => { if (!_building && !string.IsNullOrWhiteSpace(location.Text)) _registry.RememberRoot(location.Text.Trim()); };
        var browse = new Button { Content = Loc.Current["project.location.browse"] };
        browse.Click += async (_, _) =>
        {
            if (TopLevel.GetTopLevel(this) is not { } top) return;
            var folders = await top.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions());
            if (folders is [{ } folder, ..] && folder.TryGetLocalPath() is { } picked) location.Text = picked;
        };

        var backups = Number("Backups", 1, AppSettings.MaxBackups, 1, s.BackupCount, (x, n) => x with { BackupCount = n });
        ToolTip.SetTip(backups, Loc.Current["settings.backupsTip"]);

        return
        [
            Heading("settings.files"),
            Row("settings.backups", backups),
            ask,
            rememberWindow,
            Row("settings.startupMode", startup),
            lostSearch,
            lostAuto,
            Row("settings.mismatchPolicy", mismatch),
            lostForget,
            new StackPanel { Spacing = 4, Tag = Loc.Current["settings.assocUse"], Children = { new WrapPanel { ItemSpacing = 8, LineSpacing = 4, Children = { useStudio, dropStudio } }, assocState } },
            Row("settings.location", location, browse),
        ];
    }

    // One combo box over the values of an enum, each with the language key of its label.
    private ComboBox Modes<T>(string name, T[] values, string[] keys, T current, Func<AppSettings, T, AppSettings> apply) where T : struct, Enum
    {
        var box = new ComboBox { Name = name, ItemsSource = keys.Select(k => Loc.Current[k]).ToList(), SelectedIndex = Array.IndexOf(values, current), MinWidth = Wide(15) };
        box.SelectionChanged += (_, _) => { if (!_building && box.SelectedIndex >= 0) _store.Update(x => apply(x, values[box.SelectedIndex])); };
        return box;
    }

    private Control[] ProjectsSection(AppSettings s)
    {
        return
        [
            Heading("settings.projects"),
            Row("settings.autosave", Triggers("AutoSave", s.Project.AutoSave, (x, t) => x with { Project = x.Project with { AutoSave = t } }),
                Number("AutoSaveSeconds", 1, 3600, 1, s.Project.AutoSaveSeconds, (x, n) => x with { Project = x.Project with { AutoSaveSeconds = n } }), new TextBlock { Text = Loc.Current["project.settings.seconds"], VerticalAlignment = VerticalAlignment.Center }),
            Row("settings.autosync", Triggers("AutoSync", s.Project.AutoSync, (x, t) => x with { Project = x.Project with { AutoSync = t } }),
                Number("AutoSyncSeconds", 1, 3600, 1, s.Project.AutoSyncSeconds, (x, n) => x with { Project = x.Project with { AutoSyncSeconds = n } }), new TextBlock { Text = Loc.Current["project.settings.seconds"], VerticalAlignment = VerticalAlignment.Center }),
            new TextBlock { Text = Loc.Current["settings.projectsNote"], Classes = { "muted" }, [HelpText.KindProperty] = HelpKind.Detail, TextWrapping = TextWrapping.Wrap },
        ];
    }

    private static readonly int[] MemoryPresets = [0, 1024, 2048, 4096, 8192, 16384];

    private Control[] MemoryControls(AppSettings s)
    {
        var labels = MemoryPresets.Select(m => m == 0 ? Loc.Current["settings.memoryNone"] : m / 1024 + " GB").Append(Loc.Current["settings.memoryCustom"]).ToList();
        var index = Array.IndexOf(MemoryPresets, s.MemoryLimitMb);
        var preset = new ComboBox { Name = "MemoryLimit", ItemsSource = labels, SelectedIndex = index < 0 ? MemoryPresets.Length : index, MinWidth = 160 };
        var custom = Number("MemoryLimitMb", 0, 1_048_576, 256, s.MemoryLimitMb, (x, n) => x with { MemoryLimitMb = n });
        var unit = new TextBlock { Text = "MB", VerticalAlignment = VerticalAlignment.Center };
        // The number is only for a custom limit; a preset already says it.
        void ShowNumber() => custom.IsVisible = unit.IsVisible = preset.SelectedIndex == MemoryPresets.Length;
        ShowNumber();
        preset.SelectionChanged += (_, _) =>
        {
            ShowNumber();
            if (_building || preset.SelectedIndex < 0 || preset.SelectedIndex >= MemoryPresets.Length) return;
            var megabytes = MemoryPresets[preset.SelectedIndex];
            _building = true;
            custom.Value = megabytes;
            _building = false;
            _store.Update(x => x with { MemoryLimitMb = megabytes });
        };
        custom.ValueChanged += (_, e) =>
        {
            if (_building || e.NewValue is not { } n) return;
            _building = true;
            var at = Array.IndexOf(MemoryPresets, (int)n);
            preset.SelectedIndex = at < 0 ? MemoryPresets.Length : at;
            _building = false;
        };
        return
        [
            Row("settings.memoryLimit", preset, custom, unit),
            new TextBlock { Text = Loc.Current["settings.memoryNote"], Classes = { "muted" }, [HelpText.KindProperty] = HelpKind.Detail, TextWrapping = TextWrapping.Wrap },
        ];
    }

    private Control[] AdvancedSection()
    {
        var openFile = new Button { Name = "OpenSettingsFile", Content = Loc.Current["settings.openFile"] };
        openFile.Click += async (_, _) =>
        {
            if (TopLevel.GetTopLevel(this) is { } top && await top.StorageProvider.TryGetFileFromPathAsync(_store.FilePath) is { } file) await top.Launcher.LaunchFileAsync(file);
        };

        var s = _store.Current;
        var askRisky = new CheckBox { Name = "AskRiskyActions", Tag = Loc.Current["settings.askRisky"], Content = Loc.Current["settings.askRisky"], IsChecked = s.AskRiskyActions };
        askRisky.IsCheckedChanged += (_, _) => { if (!_building) _store.Update(x => x with { AskRiskyActions = askRisky.IsChecked == true }); };
        var unsafeSilently = new CheckBox { Name = "AllowUnsafeSilently", Tag = Loc.Current["settings.unsafeSilently"], Content = Loc.Current["settings.unsafeSilently"], IsChecked = s.AllowUnsafeSilently };
        unsafeSilently.IsCheckedChanged += (_, _) => { if (!_building) _store.Update(x => x with { AllowUnsafeSilently = unsafeSilently.IsChecked == true }); };

        var pluginsOn = new CheckBox { Name = "PluginsEnabled", Tag = Loc.Current["settings.pluginsEnabled"], Content = Loc.Current["settings.pluginsEnabled"], IsChecked = s.PluginsEnabled };
        pluginsOn.IsCheckedChanged += (_, _) => { if (!_building) _store.Update(x => x with { PluginsEnabled = pluginsOn.IsChecked == true }); };
        var pluginsReload = new CheckBox { Name = "PluginsHotReload", Tag = Loc.Current["settings.pluginsHotReload"], Content = Loc.Current["settings.pluginsHotReload"], IsChecked = s.PluginsHotReload };
        pluginsReload.IsCheckedChanged += (_, _) => { if (!_building) _store.Update(x => x with { PluginsHotReload = pluginsReload.IsChecked == true }); };

        return
        [
            Heading("settings.advanced"),
            new TextBlock { Text = Loc.Current["settings.advancedNote"], Classes = { "muted" }, [HelpText.KindProperty] = HelpKind.Detail, TextWrapping = TextWrapping.Wrap },
            askRisky,
            unsafeSilently,
            new TextBlock { Text = Loc.Current["settings.riskyNote"], Classes = { "muted" }, [HelpText.KindProperty] = HelpKind.Detail, TextWrapping = TextWrapping.Wrap },
            pluginsOn,
            pluginsReload,
            .. MemoryControls(_store.Current),
            Row("settings.undoLimit", Number("UndoLimitMb", 16, 1_048_576, 64, s.UndoLimitMb, (x, n) => x with { UndoLimitMb = n }), new TextBlock { Text = "MB", VerticalAlignment = VerticalAlignment.Center }),
            new TextBlock { Text = Loc.Current["settings.undoNote"], Classes = { "muted" }, [HelpText.KindProperty] = HelpKind.Detail, TextWrapping = TextWrapping.Wrap },
            openFile,
        ];
    }

    private Control[] PrivacySection(AppSettings s)
    {
        var crash = new CheckBox { Name = "CrashReports", Tag = Loc.Current["settings.crashReports"], Content = Loc.Current["settings.crashReports"], IsChecked = s.CrashReports };
        var usage = new CheckBox { Name = "UsageStats", Tag = Loc.Current["settings.usageStats"], Content = Loc.Current["settings.usageStats"], IsChecked = s.UsageStats, IsEnabled = s.CrashReports };
        crash.IsCheckedChanged += (_, _) =>
        {
            usage.IsEnabled = crash.IsChecked == true;
            if (!_building) _store.Update(x => x with { CrashReports = crash.IsChecked == true });
        };
        usage.IsCheckedChanged += (_, _) => { if (!_building) _store.Update(x => x with { UsageStats = usage.IsChecked == true }); };
        var sent = new Button { Name = "WhatIsSent", Content = Loc.Current["privacy.button"], HorizontalAlignment = HorizontalAlignment.Left };
        sent.Click += (_, _) => { if (TopLevel.GetTopLevel(this) is Window owner) PrivacyInfoWindow.Open(owner); };
        var id = new SelectableTextBlock { Name = "InstallId", Text = Loc.Current["settings.installId"] + ": " + s.InstallId, TextWrapping = TextWrapping.Wrap };
        var reset = new Button { Name = "ResetInstallId", Content = Loc.Current["settings.resetInstallId"], HorizontalAlignment = HorizontalAlignment.Left };
        reset.Click += (_, _) =>
        {
            var next = G3MStudio.Core.Settings.FailureReport.NewInstallId();
            _store.Update(x => x with { InstallId = next });
            id.Text = Loc.Current["settings.installId"] + ": " + next;
        };

        return
        [
            Heading("settings.privacy"),
            crash,
            usage,
            new TextBlock { Text = Loc.Current["settings.crashNote"], Classes = { "muted" }, [HelpText.KindProperty] = HelpKind.Detail, TextWrapping = TextWrapping.Wrap },
            sent,
            id,
            reset,
            new TextBlock { Text = Loc.Current["settings.installIdNote"], Classes = { "muted" }, [HelpText.KindProperty] = HelpKind.Detail, TextWrapping = TextWrapping.Wrap },
        ];
    }

    private Control[] ShortcutsSection()
    {
        return
        [
            Heading("settings.shortcuts"),
            new ShortcutsEditor(_commands, _store),
        ];
    }
}
