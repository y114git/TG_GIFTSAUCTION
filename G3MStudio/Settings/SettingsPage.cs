using G3MStudio.Mods;
using G3MStudio.Theme;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.VisualTree;
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
public sealed partial class SettingsPage : UserControl
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
    private readonly Func<ScreenInfo> _screen;
    /// <summary>The data file of the open workspace, for options saved per data file; null when none is open.</summary>
    public Func<string?> DataFile { get; init; } = () => null;

    private bool _building;
    private int _section;

    public SettingsPage(SettingsStore store, ProjectRegistry registry, CommandRegistry commands, UserFonts userFonts, Action close, Func<ScreenInfo>? screen = null)
    {
        (_store, _registry, _commands, _userFonts, _close) = (store, registry, commands, userFonts, close);
        _screen = screen ?? (() => default);
        Content = Build();
        // The labels follow the language; the page is built again when it changes.
        void Rebuild(object? _, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Loc.Current.Language)) Content = Build();
        }

        AttachedToVisualTree += (_, _) => Loc.Current.PropertyChanged += Rebuild;
        DetachedFromVisualTree += (_, _) => Loc.Current.PropertyChanged -= Rebuild;
    }

    /// <summary>Shows the page of a settings section (an index of the headings, in order).</summary>
    public void ShowSection(int index)
    {
        _section = index;
        Content = Build();
    }

    /// <summary>A width in steps of the spacing scale (16 px at 100%), so boxes follow the size settings.</summary>
    private static double Wide(double steps) => ModsUi.Space(4) * steps;

    private static TextBlock Heading(string key) => new TextBlock { Text = Loc.Current[key], Margin = new Thickness(0, ModsUi.Space(3), 0, 0), Classes = { "accent" } }.TextSize("FontSize.Title");

    private static StackPanel Row(string labelKey, params Control[] controls)
    {
        var line = new StackPanel { Orientation = Orientation.Horizontal, Spacing = ModsUi.Space(2) };
        foreach (var control in controls) line.Children.Add(control);
        return new StackPanel { Spacing = ModsUi.Space(1), Tag = Loc.Current[labelKey], Children = { new TextBlock { Text = Loc.Current[labelKey] }, line } };
    }

    private const string SectionHidden = "sectionHidden", SelfHidden = "selfHidden";

    /// <summary>A row that hides itself (a note that applies only sometimes) goes through here, so the search filter does not show it again.</summary>
    private static void SetSelfHidden(Control control, bool hidden)
    {
        control.Classes.Set(SelfHidden, hidden);
        SyncVisible(control);
    }

    private static void SyncVisible(Control control) => control.IsVisible = !control.Classes.Contains(SectionHidden) && !control.Classes.Contains(SelfHidden);

    /// <summary>
    /// Settings search: a row shows when its label contains the text, a whole section shows when its heading does.
    /// Children after the title and the search box belong to the heading before them.
    /// </summary>
    private static bool Filter(StackPanel page, string text, int selected)
    {
        text = text.Trim();
        var found = false;
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
            var headingHit = text.Length == 0 ? index == selected : (heading.Text ?? "").Contains(text, StringComparison.CurrentCultureIgnoreCase);
            var any = headingHit;
            foreach (var item in items)
            {
                var shown = headingHit || text.Length > 0 && item.Tag is string label && label.Contains(text, StringComparison.CurrentCultureIgnoreCase);
                item.Classes.Set(SectionHidden, !shown);
                SyncVisible(item);
                any |= shown;
            }

            heading.IsVisible = any;
            found |= any;
        }

        return found;
    }

    private ComboBox Triggers(string name, Trigger value, Func<AppSettings, Trigger, AppSettings> set)
    {
        var triggers = Enum.GetValues<Trigger>();
        var box = new ComboBox { Name = name, ItemsSource = triggers.Select(t => Loc.Current["project.trigger." + t]).ToList(), SelectedIndex = Array.IndexOf(triggers, value), MinWidth = Wide(15) };
        box.SelectionChanged += (_, _) => { if (!_building && box.SelectedIndex >= 0) _store.Update(x => set(x, triggers[box.SelectedIndex])); };
        return box;
    }

    private NumericUpDown Number(string name, int min, int max, int step, int value, Func<AppSettings, int, AppSettings> set)
    {
        var box = new NumericUpDown { Name = name, Minimum = min, Maximum = max, Increment = step, Value = value, FormatString = "0", Width = Wide(7.5) };
        box.ValueChanged += (_, e) => { if (!_building && e.NewValue is { } n) _store.Update(x => set(x, (int)n)); };
        return box;
    }

    private DockPanel Header()
    {
        var close = new Button { Name = "SettingsClose", Content = Loc.Current["settings.close"], HorizontalAlignment = HorizontalAlignment.Right };
        close.Click += (_, _) => _close();
        DockPanel.SetDock(close, Avalonia.Controls.Dock.Right);

        // The defaults come from a new AppSettings, one reset per section in the order of the headings below.
        var d = new AppSettings();
        Func<AppSettings, AppSettings>[] resets =
        [
            x => x with { UiScale = d.UiScale, FontFamily = d.FontFamily, FontWeight = d.FontWeight, Density = d.Density, ReduceMotion = d.ReduceMotion, ShowNullEntries = d.ShowNullEntries, Sizes = d.Sizes, ExplorerFolderView = d.ExplorerFolderView, ExplorerShowCounts = d.ExplorerShowCounts, ExplorerFoldersOpen = d.ExplorerFoldersOpen, ExplorerSortOrder = d.ExplorerSortOrder },
            x => x with { Colors = d.Colors },
            x => x with { CodeFontSize = d.CodeFontSize, CodeFontFamily = d.CodeFontFamily, CodeWordWrap = d.CodeWordWrap, CodeLineNumbers = d.CodeLineNumbers, CodeAutoComplete = d.CodeAutoComplete, CodeIndentGuides = d.CodeIndentGuides, CodeTabWidth = d.CodeTabWidth, CodeBracketColors = d.CodeBracketColors, CodeLigatures = d.CodeLigatures, CodeLiveCheck = d.CodeLiveCheck },
            x => x with { Image = d.Image },
            x => x with { Audio = d.Audio },
            x => x with { Diff = d.Diff },
            x => x with { RoomGridOverride = d.RoomGridOverride, RoomGridWidth = d.RoomGridWidth, RoomGridHeight = d.RoomGridHeight },
            x => x with { Decompiler = d.Decompiler },
            x => x with { BackupCount = d.BackupCount, AskOnClose = d.AskOnClose, RememberWindow = d.RememberWindow },
            x => x with { Project = d.Project },
            x => x with { RuntimeFolders = d.RuntimeFolders, RunRunner = d.RunRunner, RunArguments = d.RunArguments, RunDebugOutput = d.RunDebugOutput, RunWorkingDir = d.RunWorkingDir, RunEnvironment = d.RunEnvironment, RunSaveInPlace = d.RunSaveInPlace, RunAutoRestart = d.RunAutoRestart, RunStopSeconds = d.RunStopSeconds, WarnTempRun = d.WarnTempRun, RunOptionsLocation = d.RunOptionsLocation, ActiveRunProfile = d.ActiveRunProfile },
            x => x with { Overrides = d.Overrides, MemoryLimitMb = d.MemoryLimitMb, AskRiskyActions = d.AskRiskyActions, AllowUnsafeSilently = d.AllowUnsafeSilently },
            x => x with { CrashReports = d.CrashReports },
            Prompts.ResetAll,
            x => x with { ModBaseEnabled = d.ModBaseEnabled, ModBaseMaxMb = d.ModBaseMaxMb },
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

        return new DockPanel { Children = { close, restore, new TextBlock { Text = Loc.Current["settings.title"], Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center }.TextSize("FontSize.Display") } };
    }

    /// <summary>Moves the focus to the first input of the first row the search left visible.</summary>
    internal static void FocusFirstSetting(StackPanel page)
    {
        foreach (var row in page.Children.Skip(3).Where(c => c.IsVisible && c is not TextBlock))
        {
            var input = row.GetVisualDescendants().OfType<InputElement>().FirstOrDefault(c => c.Focusable && c.IsVisible && c.IsEnabled);
            if (input is null) continue;
            input.Focus();
            return;
        }
    }

    private DockPanel Build()
    {
        _building = true;
        var s = _store.Current;
        var search = new TextBox { Name = "SettingsSearch", Text = _search, PlaceholderText = Loc.Current["settings.search"] };
        Avalonia.Automation.AutomationProperties.SetName(search, Loc.Current["settings.search"]);
        var clear = new Button { Name = "SettingsSearchClear", Content = "×", Classes = { "icon" }, MinWidth = 24, MinHeight = 24, Width = 24, Height = 24, Margin = new Thickness(0, 0, 4, 0), VerticalAlignment = VerticalAlignment.Center, IsVisible = _search.Length > 0 }.Labeled("settings.searchClear");
        clear.Click += (_, _) =>
        {
            search.Text = "";
            search.Focus();
        };
        search.InnerRightContent = clear;
        var page = new StackPanel { MaxWidth = 640, HorizontalAlignment = HorizontalAlignment.Stretch, Margin = new Thickness(24), Spacing = Mods.ModsUi.Space(3), Children = { Header(), search } };
        Control[] sections =
        [
            .. AppearanceSection(s), .. ColorsSection(), .. EditorSection(s), .. ImageSection(s), .. SoundSection(s), .. DiffSection(s), .. RoomsSection(s), .. DecompilerSection(s), .. FilesSection(s),
            .. ProjectsSection(s), .. RunSection(s), .. AdvancedSection(), .. PrivacySection(s), .. PromptsSection(s), .. ModBaseSection(s), .. ShortcutsSection(),
        ];
        page.Children.AddRange(sections);
        // The categories are the headings of the page, listed on the left.
        var nav = new ListBox { Name = "SettingsNav", MinWidth = Wide(12.5), BorderThickness = new Thickness(0), Margin = new Thickness(16, 0, 0, 0), Background = Avalonia.Media.Brushes.Transparent, ItemsSource = page.Children.OfType<TextBlock>().Where(t => t.Classes.Contains("accent")).Select(t => t.Text).ToList(), SelectedIndex = Math.Max(0, _section) };
        Avalonia.Automation.AutomationProperties.SetName(nav, Loc.Current["settings.nav"]);
        nav.SelectionChanged += (_, _) =>
        {
            if (nav.SelectedIndex < 0) return; // cleared on purpose while a search finds nothing
            _section = nav.SelectedIndex;
            Filter(page, search.Text ?? "", _section);
        };
        var nothing = new StackPanel { Name = "SettingsNothing", Spacing = 4, IsVisible = false, Children = { new TextBlock { Text = Loc.Current["settings.nothing"] }, new TextBlock { Text = Loc.Current["settings.nothingHint"], Classes = { "muted" } } } };
        page.Children.Insert(2, nothing);
        void Refresh()
        {
            var text = _search = search.Text ?? "";
            clear.IsVisible = text.Length > 0;
            var found = Filter(page, text, _section);
            nothing.IsVisible = text.Trim().Length > 0 && !found;
            nav.SelectedIndex = nothing.IsVisible ? -1 : _section; // no category stays lit when the search hides all of them
            MarkNav();
        }

        // During a search the categories without a match are dimmed, so the list shows where the hits are.
        void MarkNav()
        {
            var hits = page.Children.OfType<TextBlock>().Where(t => t.Classes.Contains("accent")).Select(t => t.IsVisible).ToList();
            var searching = (search.Text ?? "").Trim().Length > 0;
            for (var i = 0; i < hits.Count; i++)
                if (nav.ContainerFromIndex(i) is { } item) item.Opacity = !searching || hits[i] ? 1 : 0.4;
        }

        nav.ContainerPrepared += (_, _) => MarkNav();

        search.TextChanged += (_, _) => Refresh();
        search.KeyDown += (_, e) =>
        {
            if (e.Key != Avalonia.Input.Key.Enter) return;
            e.Handled = true;
            FocusFirstSetting(page);
        };
        Filter(page, _search, Math.Max(0, _section));
        _building = false;
        // Navigation and page sit together in the middle of a wide window, not at its two edges.
        var layout = new DockPanel { Margin = new Thickness(0, 24, 0, 0), MaxWidth = 900, HorizontalAlignment = HorizontalAlignment.Stretch };
        DockPanel.SetDock(nav, Avalonia.Controls.Dock.Left);
        layout.Children.Add(nav);
        layout.Children.Add(new ScrollViewer { Content = page });
        return layout;
    }
}
