using System.Globalization;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

namespace G3MStudio;

/// <summary>The string table of the data file: filter it, pick an entry and change its text for every place that uses it (one undo step).</summary>
public sealed class StringsWindow : Window
{
    private readonly Workspace _workspace;
    private readonly TextBox _filter = new() { Name = "StringFilter", PlaceholderText = Loc.Current["strings.filter"] };
    private readonly CheckBox _regex = new() { Name = "StringRegex", Content = Loc.Current["strings.regex"] };
    private readonly ListBox _list = new() { Name = "StringList", MinHeight = 200 };
    private readonly TextBox _text = new() { Name = "StringText", AcceptsReturn = true, TextWrapping = Avalonia.Media.TextWrapping.Wrap, MinHeight = 90 };
    private readonly Button _apply = new() { Name = "StringApply", Content = Loc.Current["strings.apply"], IsEnabled = false };
    private readonly TextBlock _count = new() { Name = "StringCount", Classes = { "muted" } };
    private readonly TextBlock _uses = new() { Name = "StringUses", Classes = { "muted" } };
    private int _countVersion;
    private Row? _selected;

    public StringsWindow(Workspace workspace)
    {
        _workspace = workspace;
        Title = Loc.Current["strings.title"];
        Width = 560;
        Height = 560;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var top = new DockPanel { Children = { _regex, _filter } };
        DockPanel.SetDock(_regex, Avalonia.Controls.Dock.Right);
        var bottom = new StackPanel { Spacing = 6, Children = { _uses, _text, new DockPanel { Children = { _apply, _count } } } };
        DockPanel.SetDock(_apply, Avalonia.Controls.Dock.Right);
        var panel = new DockPanel { Margin = new Avalonia.Thickness(12) };
        DockPanel.SetDock(top, Avalonia.Controls.Dock.Top);
        DockPanel.SetDock(bottom, Avalonia.Controls.Dock.Bottom);
        panel.Children.Add(top);
        panel.Children.Add(bottom);
        panel.Children.Add(_list);
        Content = panel;
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
        Closed += (_, _) => workspace.History.Changed -= Refresh;
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
                match = t => t.Contains(needle, StringComparison.CurrentCultureIgnoreCase);
            }
        }

        // A string equal to the name of a resource is a name; everything else is text (code and event strings).
        var names = ResourceTypes.All.Where(t => !t.IsData).SelectMany(t => t.Items(_workspace.Data) ?? []).Select(ResourceTypes.NameOf).ToHashSet(StringComparer.Ordinal);
        var rows = new List<Row>();
        for (var i = 0; i < _workspace.Data.Strings.Count; i++)
        {
            var text = _workspace.Data.Strings[i].Content ?? "";
            try
            {
                if (match(text)) rows.Add(new Row(i, text, names.Contains(text) ? Loc.Current["strings.kindName"] : Loc.Current["strings.kindText"]));
            }
            catch (RegexMatchTimeoutException)
            {
                break;
            }
        }

        var keep = _selected?.Index;
        _list.ItemsSource = rows;
        _list.SelectedItem = rows.FirstOrDefault(r => r.Index == keep);
        _count.Text = Loc.Current.Format("strings.count", ("shown", rows.Count), ("total", _workspace.Data.Strings.Count));
    }

    private sealed record Row(int Index, string Text, string Kind)
    {
        public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{Index}  [{Kind}]  ") + Text.ReplaceLineEndings(" ");
    }
}
