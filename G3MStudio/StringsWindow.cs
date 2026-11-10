using System.Globalization;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;
using G3MStudio.Theme;

namespace G3MStudio;

/// <summary>The string table of the data file: filter it, pick an entry and change its text for every place that uses it (one undo step).</summary>
public sealed class StringsWindow : Window
{
    private readonly Workspace _workspace;
    private G3MLib.DataFile.References.ReferenceIndex? _index;
    private int _indexVersion = -1;
    private bool _building;
    private readonly TextBox _filter = new() { Name = "StringFilter", PlaceholderText = Loc.Current["strings.filter"] };
    private readonly CheckBox _regex = new() { Name = "StringRegex", Content = Loc.Current["strings.regex"] };
    private readonly ListBox _list = new() { Name = "StringList", MinHeight = 200 };
    private readonly TextBox _text = new() { Name = "StringText", AcceptsReturn = true, TextWrapping = Avalonia.Media.TextWrapping.Wrap, MinHeight = 90 };
    private readonly Button _apply = new() { Name = "StringApply", Content = Loc.Current["strings.apply"], IsEnabled = false };
    private readonly TextBlock _count = new() { Name = "StringCount", Classes = { "muted" } };
    private readonly TextBlock _uses = new() { Name = "StringUses", Classes = { "muted" } };
    private readonly TextBox _replaceWith = new() { Name = "StringReplaceWith", PlaceholderText = Loc.Current["strings.replaceWith"] };
    private readonly Button _replaceAll = new() { Name = "StringReplaceAll", Content = Loc.Current["strings.replaceAll"] };
    private readonly Button _add = new() { Name = "StringAdd", Content = Loc.Current["strings.add"] };
    private readonly Button _delete = new() { Name = "StringDelete", Content = Loc.Current["strings.delete"], IsEnabled = false };
    private readonly Button _export = new() { Name = "StringExport", Content = Loc.Current["strings.export"] };
    private readonly Button _import = new() { Name = "StringImport", Content = Loc.Current["strings.import"] };
    private readonly TextBlock _note = new() { Name = "StringNote", Classes = { "muted" }, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private int _countVersion;
    private Row? _selected;

    /// <summary>True while the program runs in low-memory mode; the kind column then uses the names and no index is built.</summary>
    public Func<bool>? IsLowMemory { get; set; }

    /// <summary>Frees the reference index; the next refresh builds it again unless memory is low.</summary>
    public void DropIndex() => (_index, _indexVersion) = (null, -1);

    public StringsWindow(Workspace workspace)
    {
        _workspace = workspace;
        Title = Loc.Current["strings.title"];
        Width = 560;
        Height = 560;
        MinWidth = 420;
        MinHeight = 480;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var top = new DockPanel { Children = { _regex, _filter } };
        DockPanel.SetDock(_regex, Avalonia.Controls.Dock.Right);
        var replaceRow = new DockPanel { Children = { _replaceAll, _replaceWith } };
        DockPanel.SetDock(_replaceAll, Avalonia.Controls.Dock.Right);
        var tools = new WrapPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Children = { _add, _delete, _export, _import } };
        foreach (var button in tools.Children) button.Margin = new Avalonia.Thickness(0, 0, 6, 0);
        var bottom = new StackPanel { Spacing = 6, Margin = new Avalonia.Thickness(0, 8, 0, 0), Children = { _uses, _text, replaceRow, tools, _note } };
        // The minimum height keeps the list usable; a smaller window scrolls the whole form instead of pushing the list out of view.
        var panel = new Grid { MinHeight = 520, RowDefinitions = new RowDefinitions("Auto,*,Auto") };
        top.Margin = new Avalonia.Thickness(0, 0, 0, 8);
        Grid.SetRow(_list, 1);
        Grid.SetRow(bottom, 2);
        panel.Children.Add(top);
        panel.Children.Add(_list);
        panel.Children.Add(bottom);
        _apply.Classes.Add("primary");
        _count.TextWrapping = Avalonia.Media.TextWrapping.Wrap;
        DialogFrame.Show(this, panel, [_apply, DialogFrame.Close(this, isDefault: false)], _count, DialogScroll.Vertical, dirty: () => _selected is not null && _text.Text != _selected.Text);
        _filter.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty) Refresh();
        };
        _regex.IsCheckedChanged += (_, _) => Refresh();
        _list.SelectionChanged += (_, _) =>
        {
            _selected = _list.SelectedItem as Row;
            _text.Text = _selected?.Text ?? "";
            _apply.IsEnabled = _selected is not null;
            _delete.IsEnabled = _selected is not null;
            _ = ShowUsesAsync();
        };
        _apply.Click += (_, _) => Apply();
        _add.Click += (_, _) => Add();
        _delete.Click += (_, _) => DeleteSelected();
        _replaceAll.Click += (_, _) => ReplaceAll();
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
        if (_text.Text is not { Length: > 0 } text) return;
        if (TryEdit(new StringAddCommand(text), "strings.noChange")) _note.Text = Loc.Current["strings.added"];
    }

    // Only a string nothing uses is deleted: the kind column shows "unused" once the reference index is ready.
    internal void DeleteSelected()
    {
        if (_selected is null) return;
        if (_selected.Kind != Loc.Current["strings.kindUnused"])
        {
            _note.Text = Loc.Current["strings.deleteUsed"];
            return;
        }

        if (TryEdit(new StringDeleteCommand(_selected.Index), "strings.noChange")) _note.Text = Loc.Current["strings.deleted"];
    }

    internal void ReplaceAll()
    {
        if (_filter.Text is not { Length: > 0 } needle)
        {
            _note.Text = Loc.Current["strings.replaceNeedsFilter"];
            return;
        }

        try
        {
            var pattern = new Regex(_regex.IsChecked == true ? needle : Regex.Escape(needle), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(2));
            var with = _regex.IsChecked == true ? _replaceWith.Text ?? "" : (_replaceWith.Text ?? "").Replace("$", "$$", StringComparison.Ordinal);
            var edit = StringTable.ReplaceEdits(_workspace.Data, Shown.Select(r => r.Index), pattern, with);
            var count = Shown.Count(r => pattern.Replace(r.Text, with) != r.Text);
            if (TryEdit(edit, "strings.noChange")) _note.Text = Loc.Current.Format("strings.replaced", ("count", count));
        }
        catch (Exception e) when (e is ArgumentException or RegexMatchTimeoutException)
        {
            _note.Text = e.Message;
        }
    }

    /// <summary>Writes the shown strings as CSV (index, text).</summary>
    internal void ExportTo(string path)
    {
        File.WriteAllText(path, StringTable.ToCsv(Shown.Select(r => (r.Index, r.Text))), new System.Text.UTF8Encoding(true));
        _note.Text = Loc.Current.Format("strings.exported", ("count", Shown.Count), ("name", Path.GetFileName(path)));
    }

    /// <summary>Sets the text of the strings the CSV names by number, as one undo step.</summary>
    internal void ImportFrom(string path)
    {
        try
        {
            var rows = StringTable.ParseCsv(File.ReadAllText(path));
            var edit = StringTable.ImportEdits(_workspace.Data, rows, out var skipped);
            var changed = rows.Count(r => r.Index >= 0 && r.Index < _workspace.Data.Strings.Count && _workspace.Data.Strings[r.Index].Content != r.Text);
            if (TryEdit(edit, "strings.noChange")) _note.Text = Loc.Current.Format("strings.imported", ("count", changed), ("skipped", skipped));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            _note.Text = Loc.Current.Format("strings.importFailed", ("reason", e.Message));
        }
    }

    private async Task ExportAsync()
    {
        var file = await StorageProvider.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions
        {
            Title = Loc.Current["strings.export"].TrimEnd('.'),
            DefaultExtension = "csv",
            SuggestedFileName = "string_table.csv",
            FileTypeChoices = [new Avalonia.Platform.Storage.FilePickerFileType("CSV") { Patterns = ["*.csv"] }],
        });
        if (file?.TryGetLocalPath() is { } path) ExportTo(path);
    }

    private async Task ImportAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
        {
            Title = Loc.Current["strings.import"].TrimEnd('.'),
            AllowMultiple = false,
            FileTypeFilter = [new Avalonia.Platform.Storage.FilePickerFileType("CSV") { Patterns = ["*.csv"] }],
        });
        if (files.Count == 1 && files[0].TryGetLocalPath() is { } path) ImportFrom(path);
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
                if (match(text)) rows.Add(new Row(i, text, index is not null ? KindOf(index.ClassifyString(_workspace.Data.Strings[i]), kinds) : names.Contains(text) ? kindName : kindText));
            }
            catch (RegexMatchTimeoutException)
            {
                break;
            }
        }

        var keep = _selected?.Index;
        _list.ItemsSource = rows;
        _list.SelectedItem = rows.FirstOrDefault(r => r.Index == keep);
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

    private sealed record Row(int Index, string Text, string Kind)
    {
        public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{Index}  [{Kind}]  ") + Text.ReplaceLineEndings(" ");
    }
}
