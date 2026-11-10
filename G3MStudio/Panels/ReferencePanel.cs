using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using G3MStudio.Core.GmlDocs;
using G3MStudio.Core.Localization;
using G3MStudio.Editor;
using G3MStudio.Localization;

namespace G3MStudio.Panels;

/// <summary>Function reference: the bundled GML reference as a searchable list by category, with the detail of the chosen entry. Enter or the Insert button puts the name (with its parameters) in the code at the caret.</summary>
public sealed class ReferencePanel : UserControl
{
    private readonly GmlReference _english;
    private GmlReference _reference;
    private readonly TextBox _search = new() { Name = "ReferenceSearch" };
    private readonly ComboBox _category = new() { Name = "ReferenceCategory", MinWidth = 160 };
    private readonly ListBox _list = new() { Name = "ReferenceList" };
    private readonly TextBlock _title = new() { Name = "ReferenceTitle", FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _detail = new() { Name = "ReferenceDetail", TextWrapping = TextWrapping.Wrap };
    private readonly Button _insert = new() { Name = "ReferenceInsert" };
    private readonly Button _manual = new() { Name = "ReferenceManual" };
    private bool _building;

    public ReferencePanel(GmlReference reference)
    {
        _english = reference;
        _reference = reference.For(Loc.Current.Language);
        _search.PlaceholderText = Loc.Current["reference.search"];
        _insert.Content = Loc.Current["reference.insert"];
        _manual.Content = Loc.Current["reference.manual"];
        _list.ItemTemplate = new FuncDataTemplate<RefEntry>((entry, _) => entry is null ? new TextBlock() : Row(entry));
        FillCategories();
        // The descriptions follow the language of the window, also a change made while the panel was detached.
        void Follow(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Localizer.Language)) SyncLanguage();
        }

        AttachedToVisualTree += (_, _) =>
        {
            SyncLanguage();
            Loc.Current.PropertyChanged += Follow;
        };
        DetachedFromVisualTree += (_, _) => Loc.Current.PropertyChanged -= Follow;
        _search.TextChanged += (_, _) => Refresh();
        _category.SelectionChanged += (_, _) => Refresh();
        _list.SelectionChanged += (_, _) => ShowDetail();
        _list.DoubleTapped += (_, _) => InsertSelected();
        _list.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter) return;
            InsertSelected();
            e.Handled = true;
        };
        // Down from the search box moves into the list, so a name can be found and chosen without the mouse.
        _search.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Down && _list.ItemCount > 0)
            {
                _list.SelectedIndex = Math.Max(0, _list.SelectedIndex);
                _list.Focus();
                e.Handled = true;
            }
            else if (e.Key == Key.Enter)
            {
                InsertSelected();
                e.Handled = true;
            }
        };
        _insert.Click += (_, _) => InsertSelected();
        _manual.Click += (_, _) => OpenManual();
        var top = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 0, 0, 6) };
        DockPanel.SetDock(_category, Avalonia.Controls.Dock.Right);
        top.Children.Add(_category);
        top.Children.Add(_search);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { _insert, _manual } };
        // Results on the left, the detail beside them: a short dock still shows a scrolling list.
        var detail = new DockPanel { Margin = new Thickness(8, 0, 0, 0) };
        DockPanel.SetDock(_title, Avalonia.Controls.Dock.Top);
        DockPanel.SetDock(buttons, Avalonia.Controls.Dock.Bottom);
        detail.Children.Add(_title);
        detail.Children.Add(buttons);
        detail.Children.Add(new ScrollViewer { Content = _detail });
        var layout = new DockPanel { Margin = new Thickness(8) };
        DockPanel.SetDock(top, Avalonia.Controls.Dock.Top);
        var body = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*") };
        Grid.SetColumn(detail, 1);
        body.Children.Add(_list);
        body.Children.Add(detail);
        layout.Children.Add(top);
        layout.Children.Add(body);
        Content = layout;
        Refresh();
    }

    // The entries the list shows now.
    public IReadOnlyList<RefEntry> Shown => [.. _list.Items.OfType<RefEntry>()];

    public RefEntry? Selected => _list.SelectedItem as RefEntry;

    /// <summary>The detail text of the chosen entry, for tests.</summary>
    public string DetailText => (_title.Text ?? "") + "\n" + (_detail.Text ?? "");

    public string SearchText => _search.Text ?? "";

    /// <summary>Text to put in the code at the caret was asked for.</summary>
    public event Action<string>? InsertRequested;

    /// <summary>A manual page was asked for: the web address.</summary>
    public event Action<string>? ManualRequested;

    /// <summary>Lists the entries for a word and selects the one with exactly that name; a word without an entry is left in the search box.</summary>
    public void Show(string word)
    {
        _building = true;
        _category.SelectedIndex = 0;
        _building = false;
        _search.Text = word;
        Refresh();
        var exact = _reference.Find(word);
        if (exact is not null && _list.Items.OfType<RefEntry>().FirstOrDefault(e => e == exact) is { } found) _list.SelectedItem = found;
        else if (_list.ItemCount > 0 && word.Length > 0) _list.SelectedIndex = 0;
    }

    /// <summary>The text Insert puts in the code: the name with its parameters for a function, the name for a variable, nothing for a constant group.</summary>
    public static string? InsertText(RefEntry entry) => entry.Kind switch
    {
        RefKind.Function => CallSnippet.For(Signature.FromReference(entry)).Text,
        RefKind.Variable => entry.Name,
        _ => null,
    };

    private void FillCategories()
    {
        _building = true;
        _category.ItemsSource = new[] { Loc.Current["reference.allCategories"] }.Concat(_reference.Categories).ToList();
        _category.SelectedIndex = 0;
        _building = false;
    }

    // Takes the reference of the current language, refills the categories (the chosen one stays chosen) and the list.
    private void SyncLanguage()
    {
        var chosen = _category.SelectedIndex > 0 ? _category.SelectedItem as string : null;
        _reference = _english.For(Loc.Current.Language);
        FillCategories();
        if (chosen is not null && _category.ItemsSource is IList<string> items && items.IndexOf(chosen) is var at and > 0)
        {
            _building = true;
            _category.SelectedIndex = at;
            _building = false;
        }

        Refresh();
    }

    private static StackPanel Row(RefEntry entry)
    {
        var key = entry.Kind switch { RefKind.Function => "Syntax.Function", RefKind.Variable => "Syntax.Variable", _ => "Syntax.Constant" };
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        row.Children.Add(new TextBlock { Text = GmlReference.Badge(entry.Kind), Width = 12, FontWeight = FontWeight.Bold, Foreground = Application.Current?.FindResource(key) as IBrush });
        row.Children.Add(new TextBlock { Text = entry.Kind == RefKind.Function ? entry.Signature : entry.Name, TextTrimming = TextTrimming.CharacterEllipsis, Opacity = entry.Deprecated ? 0.55 : 1 });
        return row;
    }

    private void Refresh()
    {
        if (_building) return;
        var category = _category.SelectedIndex > 0 ? _category.SelectedItem as string : null;
        var query = _search.Text?.Trim() ?? "";
        _list.ItemsSource = query.Length == 0 && category is null
            ? [.. _reference.Entries.OrderBy(e => e.Category, StringComparer.Ordinal).ThenBy(e => e.Name, StringComparer.Ordinal).Take(300)]
            : _reference.Search(query, category);
        ShowDetail();
    }

    private void ShowDetail()
    {
        if (Selected is not { } entry)
        {
            _title.Text = "";
            _detail.Text = "";
            _insert.IsEnabled = _manual.IsEnabled = false;
            return;
        }

        _title.Text = entry.Kind == RefKind.Function ? entry.Signature + (entry.Returns is { Length: > 0 } and not "Undefined" ? "  ->  " + entry.Returns : "") : entry.Name;
        var lines = new List<string> { entry.Summary, entry.Category + (entry.Deprecated ? "  |  " + Loc.Current["code.hover.deprecated"] : "") };
        if (entry.Kind == RefKind.Variable) lines.Add(entry.Returns + (entry.ReadOnly ? "  |  " + Loc.Current["code.hover.readOnly"] : ""));
        foreach (var p in entry.Parameters) lines.Add($"{(p.Optional ? "[" + p.Name + "]" : p.Rest ? "..." + p.Name : p.Name)}: {p.Type}" + (p.Meaning.Length > 0 ? " - " + p.Meaning : ""));
        _detail.Text = string.Join('\n', lines);
        _insert.IsEnabled = InsertText(entry) is not null;
        _manual.IsEnabled = true;
    }

    private void InsertSelected()
    {
        if (Selected is { } entry && InsertText(entry) is { } text) InsertRequested?.Invoke(text);
    }

    private void OpenManual()
    {
        if (Selected is { } entry) ManualRequested?.Invoke(_reference.ManualLink(entry));
    }
}
