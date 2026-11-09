using System.Globalization;
using System.Text.RegularExpressions;
using Avalonia.Controls;
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
        var bottom = new StackPanel { Spacing = 6, Margin = new Avalonia.Thickness(0, 8, 0, 0), Children = { _uses, _text } };
        var panel = new DockPanel();
        top.Margin = new Avalonia.Thickness(0, 0, 0, 8);
        DockPanel.SetDock(top, Avalonia.Controls.Dock.Top);
        DockPanel.SetDock(bottom, Avalonia.Controls.Dock.Bottom);
        panel.Children.Add(top);
        panel.Children.Add(bottom);
        panel.Children.Add(_list);
        _apply.Classes.Add("primary");
        _count.TextWrapping = Avalonia.Media.TextWrapping.Wrap;
        DialogFrame.Show(this, panel, [_apply, DialogFrame.Close(this, isDefault: false)], _count, dirty: () => _selected is not null && _text.Text != _selected.Text);
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
            _ = ShowUsesAsync();
        };
        _apply.Click += (_, _) => Apply();
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
