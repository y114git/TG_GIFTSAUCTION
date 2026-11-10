using System.Globalization;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;
using G3MStudio.Theme;
using StringUse = G3MLib.DataFile.References.StringUse;

namespace G3MStudio;

/// <summary>The string table of the data file: filter it, pick an entry and change its text for every place that uses it (one undo step).</summary>
public sealed partial class StringsWindow : Window
{
    private readonly Workspace _workspace;
    private G3MLib.DataFile.References.ReferenceIndex? _index;
    private int _indexVersion = -1;
    private bool _building;
    private readonly TextBox _filter = new() { Name = "StringFilter", PlaceholderText = Loc.Current["strings.filter"] };
    private readonly CheckBox _regex = new() { Name = "StringRegex", Content = Loc.Current["strings.regex"] };
    private readonly ListBox _list = new() { Name = "StringList", MinHeight = 160 };
    private readonly TextBox _text = new() { Name = "StringText", AcceptsReturn = true, TextWrapping = Avalonia.Media.TextWrapping.Wrap, MinHeight = 90 };
    private readonly Button _apply = new() { Name = "StringApply", Content = Loc.Current["strings.apply"], IsEnabled = false };
    private readonly TextBlock _count = new() { Name = "StringCount", Classes = { "muted" } };
    private readonly TextBlock _uses = new() { Name = "StringUses", Classes = { "muted" } };
    private readonly TextBox _replaceWith = new() { Name = "StringReplaceWith", PlaceholderText = Loc.Current["strings.replaceWith"] };
    private readonly Button _replaceAll = new() { Name = "StringReplaceAll", Content = Loc.Current["strings.replaceAll"] };
    private readonly TextBox _newText = new() { Name = "StringNewText", PlaceholderText = Loc.Current["strings.newText"] };
    private readonly Button _add = new() { Name = "StringAdd", Content = Loc.Current["strings.add"] };
    private readonly Button _delete = new() { Name = "StringDelete", Content = Loc.Current["strings.delete"], IsEnabled = false };
    private readonly Button _export = new() { Name = "StringExport", Content = Loc.Current["strings.export"] };
    private readonly Button _import = new() { Name = "StringImport", Content = Loc.Current["strings.import"] };
    private readonly TextBlock _note = new() { Name = "StringNote", Classes = { "muted" }, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private int _countVersion;
    private Row? _selected;
    private bool _refreshing;

    // A CSV is read whole; a table of 100,000 strings is a few megabytes.
    private const long MaxCsvBytes = 64L * 1024 * 1024;

    /// <summary>True while the program runs in low-memory mode; the kind column then uses the names and no index is built.</summary>
    public Func<bool>? IsLowMemory { get; set; }

    /// <summary>Frees the reference index; the next refresh builds it again unless memory is low.</summary>
    public void DropIndex() => (_index, _indexVersion) = (null, -1);

    public StringsWindow(Workspace workspace)
    {
        _workspace = workspace;
        Title = Loc.Current["strings.title"];
        Width = 560;
        Height = 720;
        MinWidth = 420;
        MinHeight = 480;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var top = new DockPanel { Children = { _regex, _filter } };
        DockPanel.SetDock(_regex, Avalonia.Controls.Dock.Right);
        var replaceRow = new DockPanel { Children = { _replaceAll, _replaceWith } };
        DockPanel.SetDock(_replaceAll, Avalonia.Controls.Dock.Right);
        var addRow = new DockPanel { Children = { _add, _newText } };
        DockPanel.SetDock(_add, Avalonia.Controls.Dock.Right);
        _add.Margin = _replaceAll.Margin = new Avalonia.Thickness(6, 0, 0, 0);
        var tools = new WrapPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Children = { _delete, _export, _import } };
        foreach (var button in tools.Children) button.Margin = new Avalonia.Thickness(0, 0, 6, 0);
        ToolTip.SetShowOnDisabled(_delete, true);
        var bottom = new StackPanel { Spacing = 6, Margin = new Avalonia.Thickness(0, 8, 0, 0), Children = { _uses, _text, replaceRow, addRow, tools, _note } };
        // The kind sits in a fixed column, so a long name of it in another language cannot push the text out.
        _list.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<Row>((row, _) =>
        {
            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("56,110,*") };
            if (row is null) return grid;
            var kind = new TextBlock { Text = row.Kind, Classes = { "muted" }, TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis, Margin = new Avalonia.Thickness(0, 0, 8, 0) };
            ToolTip.SetTip(kind, row.Kind);
            var text = new TextBlock { Text = row.Text.ReplaceLineEndings(" "), TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis };
            Grid.SetColumn(kind, 1);
            Grid.SetColumn(text, 2);
            grid.Children.Add(new TextBlock { Text = row.Index.ToString(CultureInfo.InvariantCulture) });
            grid.Children.Add(kind);
            grid.Children.Add(text);
            return grid;
        });
        // The minimum height keeps the list usable and the controls below it in view.
        var panel = new Grid { MinHeight = 480, RowDefinitions = new RowDefinitions("Auto,*,Auto") };
        top.Margin = new Avalonia.Thickness(0, 0, 0, 12);
        _regex.Margin = new Avalonia.Thickness(12, 0, 0, 0);
        Grid.SetRow(_list, 1);
        Grid.SetRow(bottom, 2);
        panel.Children.Add(top);
        panel.Children.Add(_list);
        panel.Children.Add(bottom);
        _apply.Classes.Add("primary");
        _count.TextWrapping = Avalonia.Media.TextWrapping.Wrap;
        // A star row inside a scroller measures with unlimited height and the list would take all of it, so the form is as tall as the window, and only a window below its minimum scrolls.
        var scroller = new ScrollViewer { Name = "DialogScroll", Content = panel, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto };
        scroller.SizeChanged += (_, e) => panel.Height = Math.Max(panel.MinHeight, e.NewSize.Height);
        DialogFrame.Show(this, scroller, [_apply, DialogFrame.Close(this, isDefault: false)], _count, dirty: () => _selected is not null && _text.Text != _selected.Text);
        _filter.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty) Refresh();
        };
        _regex.IsCheckedChanged += (_, _) => Refresh();
        _list.SelectionChanged += (_, _) => OnSelected();
        _apply.Click += (_, _) => Apply();
        _add.Click += (_, _) => Add();
        _delete.Click += (_, _) => DeleteSelected();
        _replaceAll.Click += async (_, _) => await ReplaceAllAsync();
        _export.Click += async (_, _) => await ExportAsync();
        _import.Click += async (_, _) => await ImportAsync();
        workspace.History.Changed += Refresh;
        _indexTimer.Tick += (_, _) =>
        {
            _indexTimer.Stop();
            BuildIndex();
        };
        Closed += (_, _) =>
        {
            workspace.History.Changed -= Refresh;
            _indexTimer.Stop();
        };
        Refresh();
    }

    private void OnSelected()
    {
        var next = _list.SelectedItem as Row;
        if (_refreshing) _selected = next; // the list was rebuilt: Refresh puts back what was typed
        else
        {
            var previous = _selected;
            var typed = _text.Text;
            _selected = next;
            _text.Text = next?.Text ?? "";
            // Moving to another string keeps the edit as its own step instead of dropping it.
            if (previous is not null && next is not null && next.Index != previous.Index && typed is not null && typed != previous.Text)
                TryEdit(new EditStringCommand(previous.Index, typed), "strings.noChange");
            _selected = _list.SelectedItem as Row; // an applied edit rebuilds the list
            _text.Text = _selected?.Text ?? "";
        }

        _apply.IsEnabled = _delete.IsEnabled = _selected is not null;
        UpdateDeleteTip();
        _ = ShowUsesAsync();
    }

    private bool IndexCurrent => _index is not null && _indexVersion == _workspace.History.Version;

    private void UpdateDeleteTip() => ToolTip.SetTip(_delete, _selected is null ? Loc.Current["strings.deleteNeedsPick"] : _selected.Use == StringUse.Unused ? null : DeleteRefusal(_selected));

    private string DeleteRefusal(Row row) => row.Use is null ? Loc.Current[IsLowMemory?.Invoke() == true ? "strings.deleteUnavailable" : "strings.deleteUnknown"] : Loc.Current["strings.deleteUsed"];

    private async Task ShowUsesAsync()
    {
        var version = ++_countVersion;
        _uses.Text = "";
        if (_selected is null) return;
        var count = await UsageSearch.CountAsync(_workspace, _workspace.Data.Strings[_selected.Index]);
        if (version == _countVersion) _uses.Text = Loc.Current.Format("strings.uses", ("count", count)); // a newer selection replaced this count otherwise
    }

    private void Apply()
    {
        if (_selected is null || _text.Text is not { } text || text == _selected.Text) return;
        _workspace.Edit(new EditStringCommand(_selected.Index, text));
    }

    private IReadOnlyList<Row> Shown => _list.ItemsSource as IReadOnlyList<Row> ?? [];

    // A failed batch shows its reason and changes nothing: the commands undo themselves.
    private bool TryEdit(EditCommand? command, string noneKey)
    {
        if (command is null)
        {
            _note.Text = Loc.Current[noneKey];
            return false;
        }

        try
        {
            _workspace.Edit(command);
            return true;
        }
        catch (InvalidOperationException e)
        {
            _note.Text = e.Message;
            return false;
        }
    }

    internal void Add()
    {
        if (_newText.Text is not { Length: > 0 } text)
        {
            _note.Text = Loc.Current["strings.addNeedsText"];
            _newText.Focus();
            return;
        }

        if (!TryEdit(new StringAddCommand(text), "strings.noChange")) return;
        _note.Text = Loc.Current["strings.added"];
        _newText.Text = "";
    }

    // Only a string nothing uses is deleted, and only when the reference index of the current data says so; without it the use is unknown and the string stays.
    internal void DeleteSelected()
    {
        if (_selected is null)
        {
            _note.Text = Loc.Current["strings.deleteNeedsPick"];
            return;
        }

        if (_selected.Use != StringUse.Unused || !IndexCurrent)
        {
            _note.Text = DeleteRefusal(IndexCurrent ? _selected : _selected with { Use = null });
            return;
        }

        if (TryEdit(new StringDeleteCommand(_selected.Index), "strings.noChange")) _note.Text = Loc.Current["strings.deleted"];
    }

    private void Refresh()
    {
        var needle = _filter.Text ?? "";
        Func<string, bool> match = _ => true;
        if (needle.Length > 0)
        {
            if (_regex.IsChecked == true)
            {
                try
                {
                    var pattern = new Regex(needle, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(200));
                    match = pattern.IsMatch;
                }
                catch (ArgumentException)
                {
                    match = _ => false;
                }
            }
            else
            {
                // Ordinal: the culture-aware comparison is about ten times slower, which shows on a table of 100,000 strings.
                match = t => t.Contains(needle, StringComparison.OrdinalIgnoreCase);
            }
        }

        // With the reference index the kind comes from where the string is used; until it is built (it is made off the UI thread) a string equal to the name of a resource is a name and the rest is text.
        EnsureIndex();
        var index = _indexVersion == _workspace.History.Version ? _index : null;
        var names = index is null ? ResourceNames() : [];
        string kindName = Loc.Current["strings.kindName"], kindText = Loc.Current["strings.kindText"];
        var kinds = new Dictionary<G3MLib.DataFile.References.StringUse, string>();
        var rows = new List<Row>();
        for (var i = 0; i < _workspace.Data.Strings.Count; i++)
        {
            var text = _workspace.Data.Strings[i].Content ?? "";
            try
            {
                if (!match(text)) continue;
                // Use stays null while the index is missing or older than the data: the string is then neither used nor unused as far as anyone knows.
                StringUse? use = index is not null ? index.ClassifyString(_workspace.Data.Strings[i]) : names.Contains(text) ? StringUse.ResourceName : null;
                rows.Add(new Row(i, text, index is not null && use is { } known ? KindOf(known, kinds) : use is null ? kindText : kindName, use));
            }
            catch (RegexMatchTimeoutException)
            {
                break;
            }
        }

        var keep = _selected?.Index;
        var typed = _selected is not null && _text.Text != _selected.Text ? _text.Text : null;
        _refreshing = true;
        try
        {
            _list.ItemsSource = rows;
            _list.SelectedItem = rows.FirstOrDefault(r => r.Index == keep);
        }
        finally
        {
            _refreshing = false;
        }

        if (typed is not null && _selected?.Index == keep) _text.Text = typed; // an unapplied edit survives a new filter or an edit elsewhere
        UpdateDeleteTip();
        _count.Text = Loc.Current.Format("strings.count", ("shown", rows.Count.ToString("N0", System.Globalization.CultureInfo.CurrentCulture)), ("total", _workspace.Data.Strings.Count.ToString("N0", System.Globalization.CultureInfo.CurrentCulture)));
    }

    // The names of the resources, for the kind column until the index is ready; kept while the data is the same.
    private HashSet<string>? _names;
    private int _namesVersion = -1;

    private HashSet<string> ResourceNames()
    {
        if (_names is not null && _namesVersion == _workspace.History.Version) return _names;
        _namesVersion = _workspace.History.Version;
        return _names = ResourceTypes.All.Where(t => !t.IsData).SelectMany(t => t.Items(_workspace.Data) ?? []).Select(ResourceTypes.NameOf).ToHashSet(StringComparer.Ordinal);
    }

    private static string KindOf(G3MLib.DataFile.References.StringUse use, Dictionary<G3MLib.DataFile.References.StringUse, string> cache) =>
        cache.TryGetValue(use, out var known) ? known : cache[use] = KindOf(use);

    private static string KindOf(G3MLib.DataFile.References.StringUse use) => Loc.Current[use switch
    {
        G3MLib.DataFile.References.StringUse.ResourceName => "strings.kindName",
        G3MLib.DataFile.References.StringUse.CodeLiteral => "strings.kindCode",
        G3MLib.DataFile.References.StringUse.VariableName => "strings.kindVariable",
        G3MLib.DataFile.References.StringUse.FunctionName => "strings.kindFunction",
        G3MLib.DataFile.References.StringUse.Unused => "strings.kindUnused",
        _ => "strings.kindOther",
    }];

    // The index is built once per state of the data, on a pool thread under the read lock, and the list is filled again when it is ready.
    // An edit drops the index. Building it takes the read lock for a quarter of a second, and the next edit has to wait for that lock, so after the first build
    // the new index is made only once edits have paused; until then the kind column uses the names.
    private readonly Avalonia.Threading.DispatcherTimer _indexTimer = new() { Interval = TimeSpan.FromMilliseconds(1500) };

    private void EnsureIndex()
    {
        var version = _workspace.History.Version;
        if (_building || _indexVersion == version || IsLowMemory?.Invoke() == true) return;
        if (_index is not null)
        {
            _indexTimer.Stop();
            _indexTimer.Start();
            return;
        }

        BuildIndex();
    }

    private void BuildIndex()
    {
        var version = _workspace.History.Version;
        if (_building || _indexVersion == version || IsLowMemory?.Invoke() == true) return;
        _building = true;
        _ = Task.Run(() =>
        {
            using (_workspace.Lock.Read()) return ReferenceCache.Of(_workspace.Data).Get(_workspace.Data);
        }).ContinueWith(task =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                _building = false;
                if (!task.IsCompletedSuccessfully) return;
                // The task has completed successfully here, so Result does not block.
                (_index, _indexVersion) = (task.Result, version);
                Refresh();
            });
        }, TaskScheduler.Default);
    }

    private sealed record Row(int Index, string Text, string Kind, StringUse? Use)
    {
        public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{Index}  [{Kind}]  ") + Text.ReplaceLineEndings(" ");
    }
}
