using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using G3MLib.Analyzer.Decompiler;
using G3MStudio.Commands;
using G3MStudio.Core.Notifications;
using G3MStudio.Core.Projects;
using G3MStudio.Core.Settings;
using G3MStudio.Core.Workspace;
using G3MStudio.Fonts;
using G3MStudio.Localization;
using G3MStudio.Panels;
using G3MStudio.Settings;
using G3MStudio.Theme;

namespace G3MStudio;

/// <summary>The Settings page (a page over the work area, not a dialog) and Go to Resource.</summary>
public partial class MainWindow
{
    private ProjectSettings _appliedProject = new();

    public UserFonts Fonts => field ??= new UserFonts(Workspaces.UserData.Combine("fonts"));

    /// <summary>Hooks to static events and slots that this window must let go of when it closes, or a closed window stays in memory.</summary>
    private void FollowStatics()
    {
        // Loc is static, so the handler must go when the window does.
        void OnLanguageTexts(object? _, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(Loc.Current.Language)) return;
            UpdateTexts();
            Docking.RetitleTabs();
            RefreshMenuTips();
            RebuildEditors();
        }

        Loc.Current.PropertyChanged += OnLanguageTexts;
        Closed += (_, _) => Loc.Current.PropertyChanged -= OnLanguageTexts;
        void ApplyCrashReports()
        {
            var current = Workspaces.Settings.Current;
            if (!FailureReport.IsInstallId(current.InstallId))
            {
                Workspaces.Settings.Update(x => x with { InstallId = FailureReport.NewInstallId() }, notify: false);
                current = Workspaces.Settings.Current;
            }

            // nothing is sent before the start page was shown and closed, so a first start sends nothing until then
            CrashReporting.Apply(CrashReporting.ShouldReport(current), usage: current.UsageStats, installId: current.InstallId);
            CrashReporting.Describe("ui-language", Loc.Current.Language);
            CrashReporting.Describe("ui-scale", current.UiScale == 0 ? "auto" : current.UiScale.ToString(System.Globalization.CultureInfo.InvariantCulture));
            CrashReporting.Describe("display-scale", RenderScaling.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture));
            CrashReporting.Describe("theme", Application.Current?.ActualThemeVariant.Key?.ToString() ?? "unknown");
            CrashReporting.Describe("memory-limit", current.MemoryLimitMb == 0 ? "none" : FailureReport.SizeRange(current.MemoryLimitMb * 1048576L));
            Telemetry.Note("language", Loc.Current.Language);
            Telemetry.Note("theme", Application.Current?.ActualThemeVariant.Key?.ToString() ?? "unknown");
            Telemetry.Note("scale", current.UiScale == 0 ? "auto" : current.UiScale.ToString(System.Globalization.CultureInfo.InvariantCulture));
            Telemetry.Note("undo-limit", FailureReport.SizeRange(current.UndoLimitMb * 1048576L));
            Telemetry.Note("screen", ScreenClass());
        }
        string ScreenClass() => Screens.Primary?.Bounds.Width switch { null => "unknown", < 1280 => "<1280", < 1920 => "1280-1919", < 2560 => "1920-2559", _ => "2560+" };
        Workspaces.Settings.Changed += ApplyCrashReports;
        ApplyCrashReports();
        Closed += (_, _) => Workspaces.Settings.Changed -= ApplyCrashReports;
        // The transparency colours of Settings > Image also colour the grid behind pictures in the resource tabs.
        static Avalonia.Media.IBrush? Paint(string text) => Core.Workspace.Rgba.Parse(text) is { } c ? new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromArgb(c.A, c.R, c.G, c.B)) : null;
        void ApplyChequer()
        {
            var image = Workspaces.Settings.Current.Image;
            (G3MStudio.Editor.Chequer.Light, G3MStudio.Editor.Chequer.Dark) = (Paint(image.CheckerLight), Paint(image.CheckerDark));
        }

        ApplyChequer();
        Workspaces.Settings.Changed += ApplyChequer;
        Closed += (_, _) => Workspaces.Settings.Changed -= ApplyChequer;
        // A static slot: clear it when this window closes, unless a newer window took it over.
        var riskyEdit = G3MStudio.Editor.PathView.RiskyEdit = EditOrForceAsync;
        Closed += (_, _) => { if (G3MStudio.Editor.PathView.RiskyEdit == riskyEdit) G3MStudio.Editor.PathView.RiskyEdit = null; };
        // The visualizer toggle of the players is a setting that only new players read; a static slot, cleared when this window closes unless a newer window took it over.
        Action<bool> rememberVisualizer = on => Workspaces.Settings.Update(x => x with { Audio = x.Audio with { Visualizer = on } }, notify: false);
        G3MStudio.Editor.AudioTransport.RememberVisualizer = rememberVisualizer;
        Closed += (_, _) => { if (G3MStudio.Editor.AudioTransport.RememberVisualizer == rememberVisualizer) G3MStudio.Editor.AudioTransport.RememberVisualizer = null; };
        Action<double> rememberWaveformHeight = height => Workspaces.Settings.Update(x => x with { Audio = x.Audio with { WaveformHeight = (int)Math.Round(height) } }, notify: false);
        G3MStudio.Editor.AudioTransport.RememberWaveformHeight = rememberWaveformHeight;
        Closed += (_, _) => { if (G3MStudio.Editor.AudioTransport.RememberWaveformHeight == rememberWaveformHeight) G3MStudio.Editor.AudioTransport.RememberWaveformHeight = null; };
        G3MStudio.Editor.SpriteFramesView.RiskyEdit = riskyEdit;
        Closed += (_, _) => { if (G3MStudio.Editor.SpriteFramesView.RiskyEdit == riskyEdit) G3MStudio.Editor.SpriteFramesView.RiskyEdit = null; };
    }

    private void InitSettings()
    {
        // Language chosen by a command or the palette is a setting too. Loc is static, so the handler must go when the window does.
        void OnLanguage(object? _, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Loc.Current.Language)) Workspaces.Settings.Update(s => s with { Language = Loc.Current.Language });
        }

        Fonts.LoadAll();
        DialogSizing.Hook(Workspaces.Settings);
        Editor.FitOptionsPanel.Hook(Workspaces.Settings);
        Editor.ResizeChoices.Hook(Workspaces.Settings);
        _appliedProject = Workspaces.Settings.Current.Project;
        Workspaces.Settings.Changed += ApplySettings;
        Editor.EditorOptions.ZoomRequested += ZoomCode;
        Closed += (_, _) => Editor.EditorOptions.ZoomRequested -= ZoomCode;
        Opened += (_, _) => RestoreWindowPlacement();
        Opened += (_, _) => Workspaces.Settings.Watch(); // an edit of settings.json from outside is applied live while the window is open
        Loc.Current.PropertyChanged += OnLanguage;
        Closed += (_, _) =>
        {
            Workspaces.Settings.Changed -= ApplySettings;
            Workspaces.Settings.Unwatch();
            Loc.Current.PropertyChanged -= OnLanguage;
        };
        ApplySettings();
    }

    /// <summary>Puts the stored settings on screen: language, interface scale, font.</summary>
    private readonly HashSet<string> _reportedClashes = [];

    private ResourceHealth? _health;
    private bool _hideUndecompilable, _hideBroken;
    private AppSettings _childSettings = new();

    // The Explorer hides what the settings ask for; the decompiler is tried in the background, and the list is built again as failures turn up.
    private void UpdateHiding()
    {
        var settings = _childSettings;
        var dims = settings.ExplorerAnonymousFunctions == EntryVisibility.Dim || settings.ExplorerStructConstructors == EntryVisibility.Dim || settings.ExplorerInnerFunctions == EntryVisibility.Dim;
        var hidesChildren = settings.ExplorerAnonymousFunctions == EntryVisibility.Hide || settings.ExplorerStructConstructors == EntryVisibility.Hide || settings.ExplorerInnerFunctions == EntryVisibility.Hide;
        var dimsNow = Explorer.Model.DimItem is not null;
        Explorer.Model.DimItem = dims ? item => ChildEntries.VisibilityOf(item, settings) == EntryVisibility.Dim : null;
        if (!_hideUndecompilable && !_hideBroken && !hidesChildren || Workspaces.Current is not { } workspace)
        {
            Explorer.Model.HideItem = null;
            if (dims || dimsNow) Explorer.Model.RebuildRows(); // the gray rows follow the settings even when nothing is left out
            return;
        }

        if (_health is null)
        {
            var health = _health = new ResourceHealth();
            health.Changed += () => Avalonia.Threading.Dispatcher.UIThread.Post(() => { if (ReferenceEquals(_health, health)) Explorer.Model.RebuildRows(); });
        }

        var current = _health;
        if (_hideUndecompilable) current.Start(workspace);
        Explorer.Model.HideItem = item => _hideUndecompilable && current.Hides(item) || _hideBroken && ResourceHealth.IsBroken(item) || ChildEntries.VisibilityOf(item, settings) == EntryVisibility.Hide;
    }

    private void ApplySettings()
    {
        var s = Workspaces.Settings.Current;
        if (s.Language != Loc.Current.Language && Loc.Current.Languages.ContainsKey(s.Language)) Loc.Current.SetLanguage(s.Language);
        G3MStudio.Theme.Motion.Set(s.ChosenAnimations);
        Editor.UiMotion.Set(G3MStudio.Theme.Motion.Level == Core.Settings.MotionPolicy.Off);
        ZoomFactor = UiSizing.Effective(s.UiScale, DialogSizing.ScreenOf(this)) / 100.0;
        ZoomHost.LayoutTransform = new ScaleTransform(ZoomFactor, ZoomFactor);
        // 840 x 460 at 100%, grown with the scale but never past the screen: the menu bar and the tab buttons need 840 before the last menu is cut off.
        var screen = DialogSizing.ScreenOf(this);
        var (maxWidth, maxHeight) = screen.Scaling > 0 ? (screen.PixelWidth / screen.Scaling, screen.PixelHeight / screen.Scaling) : (double.PositiveInfinity, double.PositiveInfinity);
        MinWidth = Math.Min(840 * ZoomFactor, maxWidth);
        MinHeight = Math.Min(460 * ZoomFactor, maxHeight);
        if (s.FontFamily.Length > 0) FontFamily = Fonts.Resolve(s.FontFamily) ?? new FontFamily(s.FontFamily); else ClearValue(FontFamilyProperty);
        if (Enum.TryParse<FontWeight>(s.FontWeight, out var weight)) FontWeight = weight; else ClearValue(FontWeightProperty);
        TabButtons.IsVisible = s.TabButtons != Core.Settings.TabButtonsPlace.Hidden;
        DockPanel.SetDock(TabButtons, s.TabButtons == Core.Settings.TabButtonsPlace.MenuRight ? Avalonia.Controls.Dock.Right : Avalonia.Controls.Dock.Left);
        ApplyColors(s.Colors);
        ApplySizes(s.Sizes, s.Density);
        ApplyOverrides(s.Overrides);
        ApplyBodyFont(s);
        G3MStudio.Theme.HelpText.Level = s.HelpLevel;
        G3MStudio.Theme.FluentForwards.Apply();
        ApplyDecompiler(s.Decompiler);
        ApplyMemoryLimit(s.MemoryLimitMb);
        EditHistory.DefaultByteLimit = s.UndoLimitMb * 1024L * 1024L;
        Explorer.Model.ShowNulls = s.ShowNullEntries;
        G3MStudio.Explorer.HoverCards.Enabled = s.HoverPreviews;
        (Explorer.Model.DefaultFolderView, Explorer.Model.ShowCounts, Explorer.Model.FoldersOpen) = (s.ExplorerFolderView, s.ExplorerShowCounts, s.ExplorerFoldersOpen);
        Explorer.OptionsFolded = s.ExplorerOptionsFolded;
        Explorer.ThumbnailSize = s.ExplorerThumbnailSize;
        (_hideUndecompilable, _hideBroken, _childSettings) = (s.ExplorerHideUndecompilable, s.ExplorerHideBroken, s);
        UpdateHiding();
        Editor.RoomOptions.Set(s.RoomGridOverride, Math.Clamp(s.RoomGridWidth, 1, 1024), Math.Clamp(s.RoomGridHeight, 1, 1024));
        Editor.RoomOptions.SetAutoTile(s.AutoTile);
        Editor.EditorOptions.Set(s.CodeFontSize, s.CodeWordWrap, s.CodeLineNumbers, s.CodeFontFamily.Length > 0 ? Fonts.Resolve(s.CodeFontFamily) ?? new FontFamily(s.CodeFontFamily) : null, s.CodeAutoComplete, s.CodeIndentGuides);
        Editor.EditorOptions.SetLayout(s.CodeTabWidth, s.CodeBracketColors, s.CodeLigatures, s.CodeLiveCheck);
        Editor.EditorOptions.SetHoverDelay(s.CodeHoverDelayMs);
        Editor.EditorOptions.SetAssetNumbers(s.CodeAssetNumbers, s.CodeAssetNumberTypes);
        Editor.EditorOptions.SetFavourites(s.FavouriteFunctions);
        if (_layerAutosave is { } autosave) autosave.Enabled = s.Image.LayerAutosave;
        foreach (var command in Registry.All.ToList())
        {
            Registry.SetShortcut(command.Id, s.Shortcuts.TryGetValue(command.Id, out var shortcut) ? (shortcut.Length == 0 ? null : shortcut) : Registry.DefaultShortcut(command.Id));
            Registry.SetAliases(command.Id, s.ShortcutAliases.TryGetValue(command.Id, out var aliases) ? [.. aliases.Where(a => Shortcuts.Parse(a) is not null || MouseGestures.Is(a))] : Registry.DefaultAliases(command.Id));
        }
        foreach (var (user, other, gesture) in Registry.OverrideClashes())
        {
            if (_reportedClashes.Add($"{user.Id}|{other.Id}|{gesture}"))
            {
                var otherIsDefault = string.Equals(Registry.DefaultShortcut(other.Id), gesture, StringComparison.OrdinalIgnoreCase) && !Registry.IsOverridden(other.Id);
                Notify(NotificationLevel.Warning, Loc.Current.Format(otherIsDefault ? "settings.shortcutClash" : "settings.shortcutClashUser", ("shortcut", gesture), ("user", Loc.Current["cmd." + user.Id]), ("other", Loc.Current["cmd." + other.Id])));
            }
        }

        // The defaults for new projects also change the open one, but only when the defaults themselves changed.
        if (s.Project != _appliedProject)
        {
            _appliedProject = s.Project;
            Workspaces.Current?.Project?.UpdateSettings(s.Project);
        }
    }

    /// <summary>Puts the user's colors over the theme (and takes back the ones that were reset); controls that use the theme tokens follow at once.</summary>
    internal static void ApplyColors(IReadOnlyDictionary<string, string> chosen)
    {
        if (Application.Current?.Resources is not { } resources) return;
        // The selected row, the hover and the pressed fill follow the accent until the user picks a color for them.
        var colors = ThemeColors.WithFollowers(chosen);
        foreach (var key in ThemeColors.Editable)
        {
            if (colors.TryGetValue(key, out var hex))
            {
                // Only a color that differs is put over the theme, so a settings change that leaves colors alone does not redraw every control.
                foreach (var (token, value) in ThemeColors.Expand(key, hex))
                {
                    var color = Color.Parse(value);
                    if (!(resources.TryGetValue(token, out var current) && current is ISolidColorBrush brush && brush.Color == color)) resources[token] = new SolidColorBrush(color);
                }
                foreach (var (fluent, argb) in ThemeColors.FluentColors(key, hex))
                {
                    var color = Color.Parse(argb);
                    if (!(resources.TryGetValue(fluent, out var current) && current is Color existing && existing == color)) resources[fluent] = color;
                }
            }
            else
            {
                foreach (var token in ThemeColors.Tokens(key)) resources.Remove(token);
                foreach (var fluent in ThemeColors.FluentKeys(key)) resources.Remove(fluent);
            }
        }

        Editor.GmlColorizer.NotifyThemeChanged();
        G3MStudio.Theme.CanvasTheme.Redraw();
        Editor.EditorOptions.Refresh();
    }

    /// <summary>Puts the user's sizes over the theme: corner radii, the border width and the type scale.</summary>
    private static void ApplySizes(IReadOnlyDictionary<string, int> sizes, string density)
    {
        if (Application.Current?.Resources is not { } resources) return;
        ScalePadding(resources, ThemeSizes.Value(sizes, density, "Space.Scale"));
        foreach (var (key, _, _, _) in ThemeSizes.All.Where(a => a.Key != "Space.Scale"))
        {
            // Only a size that differs from the theme is put over it, so a settings change that leaves sizes alone does not touch (and re-measure) every control.
            foreach (var (token, size) in ThemeSizes.Expand(key, ThemeSizes.Value(sizes, density, key)))
            {
                if (ThemeSizes.IsDefault(sizes, density, key))
                {
                    resources.Remove(token);
                    continue;
                }

                resources[token] = token switch
                {
                    "Border.Width" or "Panel.Padding" => new Thickness(size),
                    _ when token.StartsWith("Radius.", StringComparison.Ordinal) => new CornerRadius(size),
                    _ => size,
                };
            }
        }
    }

    private static readonly string[] PaddingKeys = ["ButtonPadding", "TextControlThemePadding", "ComboBoxPadding", "MenuFlyoutItemThemePadding"];
    private static readonly Dictionary<string, Thickness> PaddingDefaults = [];

    /// <summary>The spacing size scales the padding Fluent gives buttons, boxes, combo boxes and menu items; without the size the theme's own padding is back.</summary>
    private static void ScalePadding(IResourceDictionary resources, int percent)
    {
        if (Application.Current is not { } app) return;
        foreach (var key in PaddingKeys)
        {
            // The theme's value is read once, before any override of it exists.
            if (!PaddingDefaults.TryGetValue(key, out var d))
            {
                if (!app.TryGetResource(key, app.ActualThemeVariant, out var found) || found is not Thickness own) continue;
                PaddingDefaults[key] = d = own;
            }

            if (percent == 100)
            {
                resources.Remove(key);
                continue;
            }

            var f = percent / 100.0;
            var scaled = new Thickness(d.Left * f, d.Top * f, d.Right * f, d.Bottom * f);
            if (!(resources.TryGetValue(key, out var current) && current is Thickness existing && existing == scaled)) resources[key] = scaled;
        }
    }

    private HashSet<string> _overridden = [];

    /// <summary>Puts the advanced overrides of settings.json over the theme and takes back the ones that are gone (see ThemeOverrides).</summary>
    /// <summary>A CJK language gets its own body font unless the user overrides Font.Body; any other language gets the theme's again.</summary>
    private void ApplyBodyFont(Core.Settings.AppSettings s)
    {
        if (Application.Current is not { } app) return;
        // The chosen font and weight go into the tokens too: popups, menus and tooltips read the tokens, as they do not inherit from this window.
        if (!s.Overrides.ContainsKey("Font.Body"))
        {
            if (s.FontFamily.Length > 0) app.Resources["Font.Body"] = Fonts.Resolve(s.FontFamily) ?? new FontFamily(s.FontFamily);
            else if (G3MStudio.Theme.BodyFont.For(s.Language) is { } stack) app.Resources["Font.Body"] = new FontFamily(stack);
            else app.Resources.Remove("Font.Body");
        }

        if (s.Overrides.ContainsKey("Font.Weight")) return;
        if (Enum.TryParse<FontWeight>(s.FontWeight, out var weight)) app.Resources["Font.Weight"] = weight; else app.Resources.Remove("Font.Weight");
    }

    private void ApplyOverrides(IReadOnlyDictionary<string, string> overrides)
    {
        if (Application.Current is not { } app) return;
        foreach (var gone in _overridden.Where(k => !overrides.ContainsKey(k))) app.Resources.Remove(gone);
        var applied = new HashSet<string>();
        foreach (var (key, text) in overrides)
        {
            if (!app.TryGetResource(key, app.ActualThemeVariant, out var current) || ThemeOverrides.Parse(current, text) is not { } value) continue;
            app.Resources[key] = value;
            applied.Add(key);
        }

        _overridden = applied;
    }

    /// <summary>The open Settings page, or null.</summary>
    public SettingsPage? OpenSettings => SettingsHost.Child as SettingsPage;

    public void ShowSettings() => ShowSettings(0);

    /// <summary>Opens Settings on a page (an index of <see cref="G3MStudio.Core.Commands.AppCommands.SettingsPages"/>).</summary>
    public void ShowSettings(int section)
    {
        SettingsHost.Child = new SettingsPage(Workspaces.Settings, Workspaces.Projects.Registry, Registry, Fonts, CloseSettings, () => DialogSizing.ScreenOf(this)) { Name = "SettingsPage", DataFile = () => Workspaces.Current?.Path };
        if (section > 0 && SettingsHost.Child is SettingsPage page) page.ShowSection(section);
        SettingsHost.IsVisible = true;
    }

    private void CloseSettings()
    {
        SettingsHost.IsVisible = false;
        SettingsHost.Child = null;
    }

    /// <summary>The panel control of a given type; tests read what the panels show.</summary>
    public T GetPanel<T>() where T : Control => (T)(Control)(typeof(T) == typeof(ProblemsPanel) ? _problems : typeof(T) == typeof(OutputPanel) ? _output : _git);

}

public sealed partial class MainWindow
{
    /// <summary>Puts the decompiler settings into the open data file; a code tab shows them the next time its text is made.</summary>
    private void ApplyDecompiler(IReadOnlyDictionary<string, string> saved)
    {
        if (Workspaces.Current is not { Data.ToolInfo.DecompilerSettings: DecompileSettings target } workspace) return;
        DecompilerOptions.Apply(target, saved);
        workspace.DecompilerChanged();
    }

    private void ZoomCode(int size) => Workspaces.Settings.Update(x => x with { CodeFontSize = size });

    private PixelPoint _normalPosition;
    private PixelSize _normalSize;

    /// <summary>Puts the window back where it was closed, when the setting is on and that place is still on a screen (a removed monitor must not hide the window).</summary>
    private void RestoreWindowPlacement()
    {
        PositionChanged += (_, _) => { if (WindowState == WindowState.Normal) _normalPosition = Position; };
        SizeChanged += (_, _) => { if (WindowState == WindowState.Normal) _normalSize = PixelSize.FromSize(ClientSize, DesktopScaling); };
        _normalPosition = Position;
        _normalSize = PixelSize.FromSize(ClientSize, DesktopScaling);
        ScalingChanged += (_, _) => KeepOnScreen(); // moved to a smaller or denser screen
        if (Workspaces.Settings.Current is not { RememberWindow: true, Window: { } saved }) { UseDefaultSize(); return; }
        if (!UiSizing.MostlyVisible(saved.X, saved.Y, saved.Width, saved.Height, WorkAreas())) { UseDefaultSize(); return; }
        Position = new PixelPoint(saved.X, saved.Y);
        if (saved.Width > 300 && saved.Height > 200) ClientSize = new Size(saved.Width / DesktopScaling, saved.Height / DesktopScaling);
        KeepOnScreen();
        if (saved.Maximized) WindowState = WindowState.Maximized;
    }

    private List<(int X, int Y, int Width, int Height)> WorkAreas() => [.. Screens.All.Select(s => (s.WorkingArea.X, s.WorkingArea.Y, s.WorkingArea.Width, s.WorkingArea.Height))];

    /// <summary>A normal window never reaches past the work area it is on: a size saved on a larger screen, or a move to a smaller one, would push the right dock out of view.</summary>
    internal void KeepOnScreen()
    {
        if (WindowState != WindowState.Normal) return;
        var scale = DesktopScaling;
        var frame = FrameSize ?? ClientSize;
        var (x, y, width, height) = UiSizing.FitToArea(Position.X, Position.Y, (int)Math.Ceiling(frame.Width * scale), (int)Math.Ceiling(frame.Height * scale), WorkAreas());
        var client = new Size(Math.Max(0, width / scale - (frame.Width - ClientSize.Width)), Math.Max(0, height / scale - (frame.Height - ClientSize.Height)));
        if (client.Width < ClientSize.Width - 0.5 || client.Height < ClientSize.Height - 0.5) ClientSize = new Size(Math.Min(client.Width, ClientSize.Width), Math.Min(client.Height, ClientSize.Height));
        if (new PixelPoint(x, y) != Position) Position = new PixelPoint(x, y);
    }

    /// <summary>A first start (or a lost screen) opens at 85% of the work area, centered, so the same layout fits a laptop and a 4K screen.</summary>
    private void UseDefaultSize()
    {
        if (Width != 1100 || Height != 700) return; // the size in MainWindow.axaml; a size set since then stands
        if ((Screens.ScreenFromWindow(this) ?? Screens.Primary) is not { } screen) return;
        var area = screen.WorkingArea;
        var (width, height) = UiSizing.WindowSize(area.Width, area.Height, screen.Scaling);
        (Width, Height) = (width, height);
        Position = new PixelPoint(area.X + (int)((area.Width - width * screen.Scaling) / 2), area.Y + (int)((area.Height - height * screen.Scaling) / 2));
    }

    private void SaveWindowPlacement()
    {
        if (!Workspaces.Settings.Current.RememberWindow || WindowState == WindowState.Minimized) return;
        var normal = WindowState == WindowState.Normal;
        var placement = new WindowPlacement(normal ? Position.X : _normalPosition.X, normal ? Position.Y : _normalPosition.Y, normal ? (int)(ClientSize.Width * DesktopScaling) : _normalSize.Width, normal ? (int)(ClientSize.Height * DesktopScaling) : _normalSize.Height, WindowState == WindowState.Maximized);
        Workspaces.Settings.Update(x => x with { Window = placement });
    }
}
