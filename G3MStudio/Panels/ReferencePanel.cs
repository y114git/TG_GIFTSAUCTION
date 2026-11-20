using System.Text.RegularExpressions;
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
public sealed partial class ReferencePanel : UserControl
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
    private readonly Button _star = new() { Name = "ReferenceStar" };
    private readonly Button _usages = new Button { Name = "ReferenceUsages" }.Tr(ContentControl.ContentProperty, "cmd.edit.findUsages");
    private readonly Button _definition = new Button { Name = "ReferenceDefinition" }.Tr(ContentControl.ContentProperty, "cmd.code.goToDefinition");
    private readonly CheckBox _onlyFile = new() { Name = "ReferenceOnlyFile" };
    private bool _building;

    // The first entries of the category box after "All categories": the starred functions, then the game's own functions.
    private const int FavouritesIndex = 1, GameIndex = 2;

    public ReferencePanel(GmlReference reference)
    {
        _english = reference;
        _reference = reference.For(Loc.Current.Language);
        _search.PlaceholderText = Loc.Current["reference.search"];
        _insert.Content = Loc.Current["reference.insert"];
        _manual.Content = Loc.Current["reference.manual"];
        _onlyFile.Content = Loc.Current["reference.onlyFile"];
        _onlyFile.IsCheckedChanged += (_, _) => Refresh();
        _list.ItemTemplate = new FuncDataTemplate<RefEntry>((entry, _) => entry is null ? new TextBlock() : Row(entry));
        FillCategories();
        // The descriptions follow the language of the window, also a change made while the panel was detached.
        void Follow(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Localizer.Language)) SyncLanguage();
        }

        // A game function read in the background: the title of the chosen entry gets its parameters.
        void Read(Core.Workspace.Workspace _) => ShowDetail();

        void Starred(IReadOnlyList<string> names)
        {
            var name = Selected?.Name;
            Refresh();
            if (name is not null) _list.SelectedItem = Shown.FirstOrDefault(e => e.Name == name);
            ShowDetail();
        }

        AttachedToVisualTree += (_, _) =>
        {
            SyncLanguage();
            Loc.Current.PropertyChanged += Follow;
            EditorOptions.FavouritesChanged += Starred;
            GameFunctions.Ready += Read;
        };
        DetachedFromVisualTree += (_, _) =>
        {
            Loc.Current.PropertyChanged -= Follow;
            EditorOptions.FavouritesChanged -= Starred;
            GameFunctions.Ready -= Read;
        };
        _star.Click += (_, _) =>
        {
            if (Selected is { } entry) EditorOptions.ToggleFavourite(entry.Name);
        };
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
        _usages.Click += (_, _) => { if (Selected is { } entry) FindUsagesRequested?.Invoke(entry.Name); };
        _definition.Click += (_, _) =>
        {
            if (Selected is { } entry && Detail(entry).Where is { } where) DefinitionRequested?.Invoke(where, entry.Name);
        };
        Content = BuildLayout();
        Refresh();
    }

    private DockPanel BuildLayout()
    {
        // The search box is capped, so it never swallows the room of the others; the category and the filter wrap under it with a gap, so a long translation never makes them touch.
        _onlyFile.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;
        var search = new Grid { ColumnDefinitions = { new ColumnDefinition(1, GridUnitType.Star) { MaxWidth = 280 } }, Children = { _search } };
        var top = new StackPanel { Spacing = 6, Margin = new Thickness(0, 0, 0, 6), Children = { search, new WrapPanel { ItemSpacing = 12, LineSpacing = 4, Children = { _category, _onlyFile } } } };
        var buttons = new WrapPanel { ItemSpacing = 8, LineSpacing = 4, Margin = new Thickness(0, 4, 0, 8), Children = { _insert, _manual, _usages, _definition, _star } };
        // Results on the left, the detail beside them: title, buttons and text scroll together, so a short dock still reaches the buttons.
        var detail = new ScrollViewer { Margin = new Thickness(8, 0, 0, 0), Content = new StackPanel { Children = { _title, buttons, _detail } } };
        var layout = new DockPanel { Margin = new Thickness(8) };
        DockPanel.SetDock(top, Avalonia.Controls.Dock.Top);
        var body = new Grid { ColumnDefinitions = new ColumnDefinitions("3*,2*") };
        Grid.SetColumn(detail, 1);
        body.Children.Add(_list);
        body.Children.Add(detail);
        layout.Children.Add(top);
        layout.Children.Add(body);
        return layout;
    }

    // The entries the list shows now.
    public IReadOnlyList<RefEntry> Shown => [.. _list.Items.OfType<RefEntry>()];

    public RefEntry? Selected => _list.SelectedItem as RefEntry;

    /// <summary>The detail text of the chosen entry, for tests.</summary>
    public string DetailText => (_title.Text ?? "") + "\n" + (_detail.Text ?? "");

    public string SearchText => _search.Text ?? "";

    /// <summary>The game's own functions as cheap entries (names only), for the Game functions category and the search.</summary>
    public Func<IReadOnlyList<RefEntry>>? GameSource { get; set; }

    /// <summary>The full entry (parameters, description) of a game function by name; null when there is none.</summary>
    public Func<string, RefEntry?>? GameDetail { get; set; }

    /// <summary>Turns the code entry a game function is defined in into what the interface shows (its nickname); null shows the entry name.</summary>
    public Func<string, string>? PlaceLabel { get; set; }

    /// <summary>The text of the code open now; with "Only this file" on, the list shows only the names that text uses. Null when no code is open.</summary>
    public Func<string?>? CurrentCode { get; set; }

    /// <summary>Whether the "Only this file" box is on.</summary>
    public bool OnlyThisFile { get => _onlyFile.IsChecked == true; set => _onlyFile.IsChecked = value; }

    [GeneratedRegex(@"[A-Za-z_]\w*")]
    private static partial Regex Identifier();

    /// <summary>The open code changed: the list follows it while "Only this file" is on.</summary>
    public void RefreshForCode() { if (OnlyThisFile) Refresh(); }

    private IReadOnlyList<RefEntry> Games => GameSource?.Invoke() ?? [];

    private RefEntry Detail(RefEntry entry) => entry.Game && GameDetail?.Invoke(entry.Name) is { } full ? full : entry;

    // Starred names with no entry (a function of another game) are skipped, not removed.
    private IEnumerable<RefEntry> Favourites() => EditorOptions.Favourites
        .Select(n => _reference.Find(n) is { Kind: RefKind.Function } found && found.Name == n ? found : Games.FirstOrDefault(g => g.Name == n))
        .OfType<RefEntry>();

    /// <summary>Text to put in the code at the caret was asked for.</summary>
    public event Action<string>? InsertRequested;

    /// <summary>A manual page was asked for: the web address.</summary>
    public event Action<string>? ManualRequested;

    /// <summary>Find usages was asked for: the name of the entry.</summary>
    public event Action<string>? FindUsagesRequested;

    /// <summary>Go to definition was asked for a game function: the Explorer key of its code and the name.</summary>
    public event Action<string, string>? DefinitionRequested;

    /// <summary>Lists the entries for a word and selects the one with exactly that name; a word without an entry is left in the search box.</summary>
    public void Show(string word)
    {
        _building = true;
        _category.SelectedIndex = 0;
        _building = false;
        _search.Text = word;
        Refresh();
        if (_list.Items.OfType<RefEntry>().FirstOrDefault(e => e.Name == word && (e.Game || _reference.Find(word) == e)) is { } found) _list.SelectedItem = found;
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
        _category.ItemsSource = new[] { Loc.Current["reference.allCategories"], Loc.Current["reference.favourites"], Loc.Current["reference.game"] }.Concat(_reference.Categories).ToList();
        _category.SelectedIndex = 0;
        _building = false;
    }

    // Takes the reference of the current language, refills the categories (the chosen one stays chosen) and the list.
    private void SyncLanguage()
    {
        var index = _category.SelectedIndex;
        var chosen = index > GameIndex ? _category.SelectedItem as string : null;
        _reference = _english.For(Loc.Current.Language);
        _search.PlaceholderText = Loc.Current["reference.search"];
        _insert.Content = Loc.Current["reference.insert"];
        _manual.Content = Loc.Current["reference.manual"];
        _onlyFile.Content = Loc.Current["reference.onlyFile"];
        FillCategories();
        if (chosen is not null && _category.ItemsSource is IList<string> items) index = items.IndexOf(chosen);
        _building = true;
        _category.SelectedIndex = Math.Max(index, 0);
        _building = false;
        Refresh();
    }

    private Grid Row(RefEntry entry)
    {
        var key = entry.Kind switch { RefKind.Function => "Syntax.BuiltinFunction", RefKind.Variable => "Syntax.BuiltinVariable", _ => "Syntax.BuiltinConstant" };
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto"), ColumnSpacing = 6, Margin = new Thickness(0, 0, 14, 0) }; // keeps the star clear of the overlay scrollbar
        row.Children.Add(new TextBlock { Text = GmlReference.Badge(entry.Kind), Width = 12, FontWeight = FontWeight.Bold, Foreground = Application.Current?.FindResource(key) as IBrush });
        // A game function lists with its name only; its parameters come from the script read in the background, and the row follows when that is done.
        string Label() => entry.Kind == RefKind.Function ? Detail(entry).Signature : entry.Name;
        var name = new TextBlock { Text = Label(), TextTrimming = TextTrimming.CharacterEllipsis, Opacity = entry.Deprecated ? 0.55 : 1 };
        if (entry.Game)
        {
            void Read(Core.Workspace.Workspace _) => name.Text = Label();
            row.AttachedToVisualTree += (_, _) => GameFunctions.Ready += Read;
            row.DetachedFromVisualTree += (_, _) => GameFunctions.Ready -= Read;
        }

        Grid.SetColumn(name, 1);
        row.Children.Add(name);
        if (entry.Game)
        {
            var tag = new TextBlock { Text = Loc.Current["code.tag.game"], Opacity = 0.5, FontSize = 10, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(tag, 2);
            row.Children.Add(tag);
        }

        if (entry.Kind == RefKind.Function)
        {
            var star = new TextBlock { Name = "RowStar", Text = EditorOptions.IsFavourite(entry.Name) ? "★" : "☆", Cursor = new Cursor(StandardCursorType.Hand), Focusable = true };
            var label = Loc.Current.Format("reference.favourite", ("name", entry.Name));
            Avalonia.Automation.AutomationProperties.SetName(star, label);
            ToolTip.SetTip(star, label);
            star.PointerPressed += (_, e) =>
            {
                EditorOptions.ToggleFavourite(entry.Name);
                e.Handled = true;
            };
            star.KeyDown += (_, e) =>
            {
                if (e.Key is not (Key.Enter or Key.Space)) return;
                EditorOptions.ToggleFavourite(entry.Name);
                e.Handled = true;
            };
            Grid.SetColumn(star, 3);
            row.Children.Add(star);
        }

        return row;
    }

    private void Refresh()
    {
        if (_building) return;
        var index = _category.SelectedIndex;
        var query = _search.Text?.Trim() ?? "";
        IEnumerable<RefEntry> pool = index switch
        {
            FavouritesIndex => Favourites(),
            GameIndex => Games,
            > GameIndex => _reference.Entries.Where(e => e.Category == _category.SelectedItem as string),
            _ => _reference.Entries.Concat(Games),
        };
        if (OnlyThisFile)
        {
            var used = Identifier().Matches(CurrentCode?.Invoke() ?? "").Select(m => m.Value).ToHashSet(StringComparer.Ordinal);
            pool = pool.Where(e => used.Contains(e.Name));
        }

        // The first view lists the game's functions with the built-in ones, so a name is found without knowing its category.
        _list.ItemsSource = query.Length == 0 && index <= 0 && !OnlyThisFile
            ? [.. Games, .. _reference.Entries.OrderBy(e => e.Category, StringComparer.Ordinal).ThenBy(e => e.Name, StringComparer.Ordinal).Take(300)]
            : GmlReference.Rank(pool, query);
        ShowDetail();
    }

    private void ShowDetail()
    {
        if (Selected is not { } chosen)
        {
            _title.Text = "";
            _detail.Text = "";
            _insert.IsEnabled = _manual.IsEnabled = _star.IsEnabled = _usages.IsEnabled = false;
            _definition.IsVisible = false;
            _star.Content = Loc.Current["reference.star"];
            return;
        }

        var entry = Detail(chosen);
        _title.Text = entry.Kind == RefKind.Function ? entry.Signature + (entry.Returns is { Length: > 0 } and not "Undefined" ? "  ->  " + entry.Returns : "") : entry.Name;
        var place = entry.Game ? Loc.Current["reference.game"] + (entry.Where is { Length: > 0 } where ? "  |  " + Loc.Current.Format("code.hover.where", ("where", GmlHover.Place(PlaceLabel?.Invoke(where) ?? where))) : "") : entry.Category;
        var lines = new List<string> { entry.Summary, place + (entry.Deprecated ? "  |  " + Loc.Current["code.hover.deprecated"] : "") };
        if (entry.Kind == RefKind.Variable) lines.Add(entry.Returns + (entry.ReadOnly ? "  |  " + Loc.Current["code.hover.readOnly"] : ""));
        foreach (var p in entry.Parameters.Where(p => p.Name.Length > 0)) lines.Add((p.Optional ? "[" + p.Name + "]" : p.Rest ? "..." + p.Name : p.Name) + (p.Type.Length > 0 ? ": " + p.Type : "") + (p.Meaning.Length > 0 ? " - " + p.Meaning : ""));
        _detail.Text = string.Join('\n', lines);
        _insert.IsEnabled = InsertText(entry) is not null;
        _manual.IsEnabled = !entry.Game;
        _usages.IsEnabled = !entry.Name.EndsWith('*'); // a constant group is a prefix, not a name
        _definition.IsVisible = entry.Game && entry.Where is { Length: > 0 };
        _star.IsEnabled = entry.Kind == RefKind.Function;
        _star.Content = Loc.Current[EditorOptions.IsFavourite(entry.Name) ? "reference.unstar" : "reference.star"];
    }

    private void InsertSelected()
    {
        if (Selected is { } entry && InsertText(Detail(entry)) is { } text) InsertRequested?.Invoke(text);
    }

    private void OpenManual()
    {
        if (Selected is { } entry) ManualRequested?.Invoke(_reference.ManualLink(entry));
    }
}
