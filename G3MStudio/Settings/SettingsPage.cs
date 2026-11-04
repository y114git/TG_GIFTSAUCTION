using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
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
            var headingHit = text.Length == 0 ? index == selected : (heading.Text ?? "").Contains(text, StringComparison.CurrentCultureIgnoreCase);
            var any = headingHit;
            foreach (var item in items)
            {
                item.IsVisible = headingHit || text.Length > 0 && item.Tag is string label && label.Contains(text, StringComparison.CurrentCultureIgnoreCase);
                any |= item.IsVisible;
            }

            heading.IsVisible = any;
        }
    }

    private ComboBox Triggers(string name, Trigger value, Func<AppSettings, Trigger, AppSettings> set)
    {
        var triggers = Enum.GetValues<Trigger>();
        var box = new ComboBox { Name = name, ItemsSource = triggers.Select(t => Loc.Current["project.trigger." + t]).ToList(), SelectedIndex = Array.IndexOf(triggers, value), MinWidth = 240 };
        box.SelectionChanged += (_, _) => { if (!_building && box.SelectedIndex >= 0) _store.Update(x => set(x, triggers[box.SelectedIndex])); };
        return box;
    }

    private NumericUpDown Number(string name, int min, int max, int step, int value, Func<AppSettings, int, AppSettings> set)
    {
        var box = new NumericUpDown { Name = name, Minimum = min, Maximum = max, Increment = step, Value = value, FormatString = "0", Width = 120 };
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
            x => x with { UiScale = d.UiScale, FontFamily = d.FontFamily, FontWeight = d.FontWeight, CompactDensity = d.CompactDensity, ReduceMotion = d.ReduceMotion, ShowNullEntries = d.ShowNullEntries, Sizes = d.Sizes },
            x => x with { Colors = d.Colors },
            x => x with { CodeFontSize = d.CodeFontSize, CodeFontFamily = d.CodeFontFamily, CodeWordWrap = d.CodeWordWrap, CodeLineNumbers = d.CodeLineNumbers, CodeAutoComplete = d.CodeAutoComplete },
            x => x with { RoomGridOverride = d.RoomGridOverride, RoomGridWidth = d.RoomGridWidth, RoomGridHeight = d.RoomGridHeight },
            x => x with { Decompiler = d.Decompiler },
            x => x with { BackupCount = d.BackupCount, AskOnClose = d.AskOnClose, RememberWindow = d.RememberWindow },
            x => x with { Project = d.Project },
            x => x with { RuntimeFolders = d.RuntimeFolders, RunRunner = d.RunRunner, RunArguments = d.RunArguments, RunDebugOutput = d.RunDebugOutput, WarnTempRun = d.WarnTempRun, ActiveRunProfile = d.ActiveRunProfile },
            x => x with { Overrides = d.Overrides, MemoryLimitMb = d.MemoryLimitMb },
            x => x with { CrashReports = d.CrashReports },
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

        return new DockPanel { Children = { close, restore, new TextBlock { Text = Loc.Current["settings.title"], FontSize = 20, Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center } } };
    }

    private DockPanel Build()
    {
        _building = true;
        var s = _store.Current;
        var search = new TextBox { Name = "SettingsSearch", Text = _search, PlaceholderText = Loc.Current["settings.search"] };
        var page = new StackPanel { MaxWidth = 640, HorizontalAlignment = HorizontalAlignment.Stretch, Margin = new Thickness(24), Spacing = 10, Children = { Header(), search } };
        Control[] sections =
        [
            .. AppearanceSection(s), .. ColorsSection(), .. EditorSection(s), .. RoomsSection(s), .. DecompilerSection(s), .. FilesSection(s),
            .. ProjectsSection(s), .. RunSection(s), .. AdvancedSection(), .. PrivacySection(s), .. ShortcutsSection(),
        ];
        page.Children.AddRange(sections);
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
        // Navigation and page sit together in the middle of a wide window, not at its two edges.
        var layout = new DockPanel { Margin = new Thickness(0, 24, 0, 0), MaxWidth = 900, HorizontalAlignment = HorizontalAlignment.Stretch };
        DockPanel.SetDock(nav, Avalonia.Controls.Dock.Left);
        layout.Children.Add(nav);
        layout.Children.Add(new ScrollViewer { Content = page });
        return layout;
    }
}
