using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using G3MStudio.Core.Projects;
using G3MStudio.Core.Settings;
using G3MStudio.Core.Workspace;
using G3MStudio.Fonts;
using G3MStudio.Localization;
using G3MStudio.Panels;
using G3MStudio.Settings;

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
        Loc.Current.PropertyChanged += OnLanguage;
        Closed += (_, _) =>
        {
            Workspaces.Settings.Changed -= ApplySettings;
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
        ZoomFactor = s.UiScale / 100.0;
        ZoomHost.LayoutTransform = new ScaleTransform(ZoomFactor, ZoomFactor);
        if (s.FontFamily.Length > 0) FontFamily = Fonts.Resolve(s.FontFamily) ?? new FontFamily(s.FontFamily); else ClearValue(FontFamilyProperty);
        if (Enum.TryParse<FontWeight>(s.FontWeight, out var weight)) FontWeight = weight; else ClearValue(FontWeightProperty);
        ApplyColors(s.Colors);
        Editor.EditorOptions.Set(s.CodeFontSize, s.CodeWordWrap, s.CodeLineNumbers);
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
    private static void ApplyColors(IReadOnlyDictionary<string, string> colors)
    {
        if (Application.Current?.Resources is not { } resources) return;
        foreach (var key in ThemeColors.Editable)
        {
            if (colors.TryGetValue(key, out var hex))
            {
                foreach (var (token, value) in ThemeColors.Expand(key, hex)) resources[token] = new SolidColorBrush(Color.Parse(value));
            }
            else
            {
                foreach (var token in ThemeColors.Tokens(key)) resources.Remove(token);
            }
        }

        Editor.GmlColorizer.NotifyThemeChanged();
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
