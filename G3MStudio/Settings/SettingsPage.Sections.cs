using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using G3MStudio.Core.Settings;
using G3MStudio.Localization;

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
        var language = new ComboBox { Name = "Language", ItemsSource = languages.Select(l => l.Value).ToList(), SelectedIndex = languages.FindIndex(l => l.Key == s.Language), MinWidth = 240 };
        language.SelectionChanged += (_, _) => { if (!_building && language.SelectedIndex >= 0) _store.Update(x => x with { Language = languages[language.SelectedIndex].Key }); };

        var fonts = FontChoices();
        var family = new ComboBox { Name = "FontFamily", ItemsSource = fonts.Select(f => f.Length == 0 ? Loc.Current["settings.default"] : f).ToList(), SelectedIndex = Math.Max(0, fonts.IndexOf(s.FontFamily)), MinWidth = 240 };
        family.SelectionChanged += (_, _) => { if (!_building && family.SelectedIndex >= 0) _store.Update(x => x with { FontFamily = fonts[family.SelectedIndex] }); };

        var weight = new ComboBox { Name = "FontWeight", ItemsSource = Weights.Select(w => w.Length == 0 ? Loc.Current["settings.default"] : w).ToList(), SelectedIndex = Math.Max(0, Array.IndexOf(Weights, s.FontWeight)), MinWidth = 240 };
        weight.SelectionChanged += (_, _) => { if (!_building && weight.SelectedIndex >= 0) _store.Update(x => x with { FontWeight = Weights[weight.SelectedIndex] }); };

        var showNulls = new CheckBox { Name = "ShowNullEntries", Tag = Loc.Current["settings.showNullEntries"], Content = Loc.Current["settings.showNullEntries"], IsChecked = s.ShowNullEntries };
        showNulls.IsCheckedChanged += (_, _) => { if (!_building) _store.Update(x => x with { ShowNullEntries = showNulls.IsChecked == true }); };

        var reduce = new CheckBox { Name = "ReduceMotion", Tag = Loc.Current["settings.reduceMotion"], Content = Loc.Current["settings.reduceMotion"], IsChecked = s.ReduceMotion };
        reduce.IsCheckedChanged += (_, _) => { if (!_building) _store.Update(x => x with { ReduceMotion = reduce.IsChecked == true }); };

        var sizePanel = new StackPanel { Spacing = 10, Tag = Loc.Current["settings.sizes"] };
        sizePanel.Children.Add(new TextBlock { Text = Loc.Current["settings.sizes"], Classes = { "muted" }, Margin = new Thickness(0, 8, 0, 0) });
        foreach (var (sizeKey, min, max, _) in ThemeSizes.All)
        {
            var key = sizeKey;
            sizePanel.Children.Add(Row("settings.size." + key, Number("Size_" + key, min, max, 1, ThemeSizes.Value(s.Sizes, s.Density, key), (x, n) => x with { Sizes = new Dictionary<string, int>(x.Sizes) { [key] = n } }), new TextBlock { Text = key == "Space.Scale" ? "%" : "px", VerticalAlignment = VerticalAlignment.Center }));
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
            reduce,
            showNulls,
            FontFiles(),
        ];
    }

    /// <summary>The interface scale: Auto (computed from the screen) or a percent. Either applies to the window at once, so the page itself is the preview.</summary>
    private Control[] ScaleRows(AppSettings s)
    {
        var screen = _screen();
        var auto = UiSizing.AutoScale(screen);
        var box = Number("UiScale", UiSizing.MinManual, UiSizing.MaxScale, 10, s.UiScale == 0 ? auto : s.UiScale, (x, n) => x with { UiScale = n });
        box.IsEnabled = s.UiScale != 0;
        var check = new CheckBox { Name = "UiScaleAuto", Content = Loc.Current["settings.scaleAuto"], IsChecked = s.UiScale == 0 };
        var info = new TextBlock { Name = "UiScaleInfo", Classes = { "muted" }, Text = ScaleInfo(s.UiScale, screen) };
        check.IsCheckedChanged += (_, _) =>
        {
            if (_building) return;
            var on = check.IsChecked == true;
            box.IsEnabled = !on;
            _store.Update(x => x with { UiScale = on ? 0 : (int)(box.Value ?? auto) });
            info.Text = ScaleInfo(_store.Current.UiScale, screen);
        };
        box.ValueChanged += (_, _) => info.Text = ScaleInfo(_store.Current.UiScale, screen);
        return [Row("settings.scale", box, new TextBlock { Text = "%", VerticalAlignment = VerticalAlignment.Center }, check), info];
    }

    private static string ScaleInfo(int uiScale, ScreenInfo screen) => Loc.Current.Format("settings.scaleInfo", ("percent", UiSizing.Effective(uiScale, screen)));

    private StackPanel DensityRow(AppSettings s)
    {
        var density = new ComboBox { Name = "Density", ItemsSource = UiSizing.Densities.Select(d => Loc.Current["settings.densityName." + d]).ToList(), SelectedIndex = Array.IndexOf(UiSizing.Densities, UiSizing.NormalDensity(s.Density)), MinWidth = 240 };
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
        var codeFamily = new ComboBox { Name = "CodeFontFamily", ItemsSource = fonts.Select(f => f.Length == 0 ? Loc.Current["settings.default"] : f).ToList(), SelectedIndex = Math.Max(0, fonts.IndexOf(s.CodeFontFamily)), MinWidth = 240 };
        codeFamily.SelectionChanged += (_, _) => { if (!_building && codeFamily.SelectedIndex >= 0) _store.Update(x => x with { CodeFontFamily = fonts[codeFamily.SelectedIndex] }); };

        var codeWrap = new CheckBox { Name = "CodeWordWrap", Tag = Loc.Current["settings.codeWordWrap"], Content = Loc.Current["settings.codeWordWrap"], IsChecked = s.CodeWordWrap };
        codeWrap.IsCheckedChanged += (_, _) => { if (!_building) _store.Update(x => x with { CodeWordWrap = codeWrap.IsChecked == true }); };
        var codeNumbers = new CheckBox { Name = "CodeLineNumbers", Tag = Loc.Current["settings.codeLineNumbers"], Content = Loc.Current["settings.codeLineNumbers"], IsChecked = s.CodeLineNumbers };
        codeNumbers.IsCheckedChanged += (_, _) => { if (!_building) _store.Update(x => x with { CodeLineNumbers = codeNumbers.IsChecked == true }); };

        var codeComplete = new CheckBox { Name = "CodeAutoComplete", Tag = Loc.Current["settings.codeAutoComplete"], Content = Loc.Current["settings.codeAutoComplete"], IsChecked = s.CodeAutoComplete };
        codeComplete.IsCheckedChanged += (_, _) => { if (!_building) _store.Update(x => x with { CodeAutoComplete = codeComplete.IsChecked == true }); };

        var codeGuides = new CheckBox { Name = "CodeIndentGuides", Tag = Loc.Current["settings.codeIndentGuides"], Content = Loc.Current["settings.codeIndentGuides"], IsChecked = s.CodeIndentGuides };
        codeGuides.IsCheckedChanged += (_, _) => { if (!_building) _store.Update(x => x with { CodeIndentGuides = codeGuides.IsChecked == true }); };

        return
        [
            Heading("settings.editor"),
            Row("settings.codeFontFamily", codeFamily),
            Row("settings.codeFontSize", Number("CodeFontSize", 8, 32, 1, s.CodeFontSize, (x, n) => x with { CodeFontSize = n }), new TextBlock { Text = "px", VerticalAlignment = VerticalAlignment.Center }),
            codeWrap,
            codeNumbers,
            codeComplete,
            codeGuides,
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
        ];
    }

    private Control[] DecompilerSection(AppSettings s)
    {
        var decompiler = new StackPanel { Name = "DecompilerSettings", Spacing = 6 };
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
        var assocState = new TextBlock { Name = "AssociationMessage", Classes = { "muted" }, TextWrapping = TextWrapping.Wrap };
        static string AssocText() => G3MStudio.Core.Links.FileAssociations.Registered() is { Count: > 0 } now ? Loc.Current.Format("settings.assocNow", ("types", string.Join(" ", now))) : Loc.Current["settings.assocNone"];
        assocState.Text = G3MStudio.Core.Links.FileAssociations.Supported ? AssocText() : Loc.Current["settings.assocWindows"];
        var useStudio = new Button { Name = "AssociateFiles", Content = Loc.Current["settings.assocUse"], IsEnabled = G3MStudio.Core.Links.FileAssociations.Supported };
        var dropStudio = new Button { Name = "DissociateFiles", Content = Loc.Current["settings.assocStop"], IsEnabled = G3MStudio.Core.Links.FileAssociations.Supported };
        useStudio.Click += (_, _) =>
        {
            try
            {
                G3MStudio.Core.Links.FileAssociations.Register(Environment.ProcessPath ?? "", G3MStudio.Core.Links.FileAssociations.Extensions);
                assocState.Text = AssocText();
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or PlatformNotSupportedException or System.Security.SecurityException)
            {
                assocState.Text = Loc.Current["settings.linksFailed"] + ": " + e.Message;
            }
        };
        dropStudio.Click += (_, _) =>
        {
            try
            {
                G3MStudio.Core.Links.FileAssociations.Remove();
                assocState.Text = AssocText();
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                assocState.Text = Loc.Current["settings.linksFailed"] + ": " + e.Message;
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

        return
        [
            Heading("settings.files"),
            Row("settings.backups", Number("Backups", 1, 3, 1, s.BackupCount, (x, n) => x with { BackupCount = n })),
            ask,
            rememberWindow,
            new StackPanel { Spacing = 4, Tag = Loc.Current["settings.registerLinks"], Children = { registerLinks, linkState } },
            new StackPanel { Spacing = 4, Tag = Loc.Current["settings.assocUse"], Children = { new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { useStudio, dropStudio } }, assocState } },
            Row("settings.location", location, browse),
        ];
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
            new TextBlock { Text = Loc.Current["settings.projectsNote"], Classes = { "muted" }, TextWrapping = TextWrapping.Wrap },
        ];
    }

    private Control[] RunSection(AppSettings s)
    {
        var runner = new TextBox { Name = "RunRunner", Text = s.RunRunner, MinWidth = 360 };
        runner.TextChanged += (_, _) => { if (!_building) _store.Update(x => x with { RunRunner = (runner.Text ?? "").Trim() }); };
        var runtimeFolders = new TextBox { Name = "RuntimeFolders", Text = s.RuntimeFolders, MinWidth = 360 };
        runtimeFolders.TextChanged += (_, _) => { if (!_building) _store.Update(x => x with { RuntimeFolders = runtimeFolders.Text ?? "" }); };
        var runArguments = new TextBox { Name = "RunArguments", Text = s.RunArguments, MinWidth = 360 };
        runArguments.TextChanged += (_, _) => { if (!_building) _store.Update(x => x with { RunArguments = runArguments.Text ?? "" }); };
        var warnTemp = new CheckBox { Name = "WarnTempRun", Tag = Loc.Current["settings.warnTempRun"], Content = Loc.Current["settings.warnTempRun"], IsChecked = s.WarnTempRun };
        warnTemp.IsCheckedChanged += (_, _) => { if (!_building) _store.Update(x => x with { WarnTempRun = warnTemp.IsChecked == true }); };
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

        return
        [
            Heading("settings.run"),
            Row("settings.runRunner", runner),
            Row("settings.runArguments", runArguments),
            Row("settings.runtimeFolders", runtimeFolders),
            debugOutput,
            warnTemp,
            Row("settings.profiles", profiles, deleteProfile),
            new TextBlock { Text = Loc.Current["settings.profilesNote"], Classes = { "muted" }, TextWrapping = TextWrapping.Wrap },
            Row("settings.profileName", profileName, saveProfile),
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
            new TextBlock { Text = Loc.Current["settings.memoryNote"], Classes = { "muted" }, TextWrapping = TextWrapping.Wrap },
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

        return
        [
            Heading("settings.advanced"),
            new TextBlock { Text = Loc.Current["settings.advancedNote"], Classes = { "muted" }, TextWrapping = TextWrapping.Wrap },
            askRisky,
            unsafeSilently,
            new TextBlock { Text = Loc.Current["settings.riskyNote"], Classes = { "muted" }, TextWrapping = TextWrapping.Wrap },
            .. MemoryControls(_store.Current),
            openFile,
        ];
    }

    private Control[] PrivacySection(AppSettings s)
    {
        var crash = new CheckBox { Name = "CrashReports", Tag = Loc.Current["settings.crashReports"], Content = Loc.Current["settings.crashReports"], IsChecked = s.CrashReports };
        crash.IsCheckedChanged += (_, _) => { if (!_building) _store.Update(x => x with { CrashReports = crash.IsChecked == true }); };

        return
        [
            Heading("settings.privacy"),
            crash,
            new TextBlock { Text = Loc.Current["settings.crashNote"], Classes = { "muted" }, TextWrapping = TextWrapping.Wrap },
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
