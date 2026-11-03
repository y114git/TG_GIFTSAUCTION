using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using G3MLib.Analyzer.Decompiler;
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

    private void InitSettings()
    {
        // Language chosen by a command or the palette is a setting too. Loc is static, so the handler must go when the window does.
        void OnLanguage(object? _, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Loc.Current.Language)) Workspaces.Settings.Update(s => s with { Language = Loc.Current.Language });
        }

        Fonts.LoadAll();
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
    private void ApplySettings()
    {
        var s = Workspaces.Settings.Current;
        if (s.Language != Loc.Current.Language && Loc.Current.Languages.ContainsKey(s.Language)) Loc.Current.SetLanguage(s.Language);
        Classes.Set("compact", s.CompactDensity);
        Editor.UiMotion.Set(s.ReduceMotion);
        ZoomFactor = s.UiScale / 100.0;
        ZoomHost.LayoutTransform = new ScaleTransform(ZoomFactor, ZoomFactor);
        if (s.FontFamily.Length > 0) FontFamily = Fonts.Resolve(s.FontFamily) ?? new FontFamily(s.FontFamily); else ClearValue(FontFamilyProperty);
        if (Enum.TryParse<FontWeight>(s.FontWeight, out var weight)) FontWeight = weight; else ClearValue(FontWeightProperty);
        ApplyColors(s.Colors);
        ApplySizes(s.Sizes);
        ApplyOverrides(s.Overrides);
        G3MStudio.Theme.FluentForwards.Apply();
        ApplyDecompiler(s.Decompiler);
        Editor.RoomOptions.Set(s.RoomGridOverride, Math.Clamp(s.RoomGridWidth, 1, 1024), Math.Clamp(s.RoomGridHeight, 1, 1024));
        Editor.EditorOptions.Set(s.CodeFontSize, s.CodeWordWrap, s.CodeLineNumbers, s.CodeFontFamily.Length > 0 ? Fonts.Resolve(s.CodeFontFamily) ?? new FontFamily(s.CodeFontFamily) : null);
        foreach (var command in Registry.All.ToList())
            Registry.SetShortcut(command.Id, s.Shortcuts.TryGetValue(command.Id, out var shortcut) ? (shortcut.Length == 0 ? null : shortcut) : Registry.DefaultShortcut(command.Id));
        // The defaults for new projects also change the open one, but only when the defaults themselves changed.
        if (s.Project != _appliedProject)
        {
            _appliedProject = s.Project;
            Workspaces.Current?.Project?.UpdateSettings(s.Project);
        }
    }

    /// <summary>Puts the user's colors over the theme (and takes back the ones that were reset); controls that use the theme tokens follow at once.</summary>
    private static void ApplyColors(IReadOnlyDictionary<string, string> chosen)
    {
        if (Application.Current?.Resources is not { } resources) return;
        // The selected row follows the accent until the user picks a color for it.
        var colors = !chosen.ContainsKey("Selection.Bg") && chosen.TryGetValue(ThemeColors.Accent, out var accent) ? new Dictionary<string, string>(chosen) { ["Selection.Bg"] = accent } : chosen;
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
    private static void ApplySizes(IReadOnlyDictionary<string, int> sizes)
    {
        if (Application.Current?.Resources is not { } resources) return;
        ScalePadding(resources, sizes);
        foreach (var (key, _, _, _) in ThemeSizes.All.Where(a => a.Key != "Space.Scale"))
        {
            // Only a size the user changed is put over the theme, so a settings change that leaves sizes alone does not touch (and re-measure) every control.
            foreach (var (token, size) in ThemeSizes.Expand(key, ThemeSizes.Value(sizes, key)))
            {
                if (!sizes.ContainsKey(key))
                {
                    resources.Remove(token);
                    continue;
                }

                resources[token] = token switch
                {
                    "Border.Width" => new Thickness(size),
                    _ when token.StartsWith("Radius.", StringComparison.Ordinal) => new CornerRadius(size),
                    _ => size,
                };
            }
        }
    }

    private static readonly string[] PaddingKeys = ["ButtonPadding", "TextControlThemePadding", "ComboBoxPadding", "MenuFlyoutItemThemePadding"];
    private static readonly Dictionary<string, Thickness> PaddingDefaults = [];

    /// <summary>The spacing size scales the padding Fluent gives buttons, boxes, combo boxes and menu items; without the size the theme's own padding is back.</summary>
    private static void ScalePadding(IResourceDictionary resources, IReadOnlyDictionary<string, int> sizes)
    {
        var app = Application.Current!;
        foreach (var key in PaddingKeys)
        {
            // The theme's value is read once, before any override of it exists.
            if (!PaddingDefaults.TryGetValue(key, out var d))
            {
                if (!app.TryGetResource(key, app.ActualThemeVariant, out var found) || found is not Thickness own) continue;
                PaddingDefaults[key] = d = own;
            }

            if (!sizes.TryGetValue("Space.Scale", out var percent))
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

    public void ShowSettings()
    {
        SettingsHost.Child = new SettingsPage(Workspaces.Settings, Workspaces.Projects.Registry, Registry, Fonts, CloseSettings) { Name = "SettingsPage" };
        SettingsHost.IsVisible = true;
    }

    private void CloseSettings()
    {
        SettingsHost.IsVisible = false;
        SettingsHost.Child = null;
    }

    /// <summary>The panel control of a given type; tests read what the panels show.</summary>
    public T GetPanel<T>() where T : Control => (T)(Control)(typeof(T) == typeof(ProblemsPanel) ? _problems : typeof(T) == typeof(OutputPanel) ? _output : _git);

    public QuickOpenWindow? QuickOpenBox { get; private set; }

    public void QuickOpen()
    {
        if (Workspaces.Current is not { } workspace) return;
        // Open tabs, latest first, rank ties.
        var recent = Docking.Tabs.Tabs.Select(t => t.Key).Reverse().ToList();
        QuickOpenBox = new QuickOpenWindow(query => ResourceSearch.Search(workspace.Data, query, recent));
        QuickOpenBox.Closed += (_, _) => QuickOpenBox = null;
        QuickOpenBox.Chosen += (item, mode) =>
        {
            if (Docking.OpenTab(item.Type == "code" ? "code" : "resource", item.Key, mode == QuickOpenMode.Preview) is not null && mode == QuickOpenMode.Side) SplitRight();
        };
        _ = QuickOpenBox.ShowDialog(this);
    }
}

public sealed partial class MainWindow
{
    /// <summary>Puts the decompiler settings into the open data file; a code tab shows them the next time its text is made.</summary>
    private void ApplyDecompiler(IReadOnlyDictionary<string, string> saved)
    {
        if (Workspaces.Current is { Data.ToolInfo.DecompilerSettings: DecompileSettings target }) DecompilerOptions.Apply(target, saved);
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
        if (Workspaces.Settings.Current is not { RememberWindow: true, Window: { } saved }) return;
        var corner = new PixelPoint(saved.X + 40, saved.Y + 40);
        if (!Screens.All.Any(screen => screen.WorkingArea.Contains(corner))) return;
        Position = new PixelPoint(saved.X, saved.Y);
        if (saved.Width > 300 && saved.Height > 200) ClientSize = new Size(saved.Width / DesktopScaling, saved.Height / DesktopScaling);
        if (saved.Maximized) WindowState = WindowState.Maximized;
    }

    private void SaveWindowPlacement()
    {
        if (!Workspaces.Settings.Current.RememberWindow || WindowState == WindowState.Minimized) return;
        var normal = WindowState == WindowState.Normal;
        var placement = new WindowPlacement(normal ? Position.X : _normalPosition.X, normal ? Position.Y : _normalPosition.Y, normal ? (int)(ClientSize.Width * DesktopScaling) : _normalSize.Width, normal ? (int)(ClientSize.Height * DesktopScaling) : _normalSize.Height, WindowState == WindowState.Maximized);
        Workspaces.Settings.Update(x => x with { Window = placement });
    }
}
