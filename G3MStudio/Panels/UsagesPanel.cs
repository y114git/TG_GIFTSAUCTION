using Avalonia.Controls;
using Avalonia.Controls.Templates;
using G3MStudio.Core.Workspace;
using G3MStudio.Explorer;
using G3MStudio.Localization;
using G3MStudio.Theme;

namespace G3MStudio.Panels;

/// <summary>Usages: where the resource asked for is used. A double click asks the window to open the entry that holds the reference.</summary>
public sealed class UsagesPanel : UserControl
{
    private readonly TextBlock _status = new() { Name = "UsagesStatus", Classes = { "muted" }, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private readonly ListBox _list = new() { Name = "UsagesList", SelectionMode = SelectionMode.Multiple };
    private readonly Button _remove = new Button { Name = "UsagesRemove", IsVisible = false }.Tr(ContentControl.ContentProperty, "usages.removeSelected");
    private readonly Button _exclude = new Button { Name = "UsagesExclude", IsVisible = false }.Tr(ContentControl.ContentProperty, "usages.excludeSelected");
    private readonly TextBlock _note = new TextBlock { Name = "UsagesNote", Classes = { "muted" }, [HelpText.KindProperty] = HelpKind.Detail, TextWrapping = Avalonia.Media.TextWrapping.Wrap, IsVisible = false }.Tr(TextBlock.TextProperty, "usages.unrefNote");
    private readonly TextBlock _excludeNote = new TextBlock { Name = "UsagesExcludeNote", Classes = { "muted" }, [HelpText.KindProperty] = HelpKind.Detail, TextWrapping = Avalonia.Media.TextWrapping.Wrap, IsVisible = false }.Tr(TextBlock.TextProperty, "usages.excludeNote");
    private readonly TextBlock _empty = new TextBlock { Name = "UsagesEmpty", Classes = { "muted" }, [HelpText.KindProperty] = HelpKind.Hint, TextWrapping = Avalonia.Media.TextWrapping.Wrap }.Tr(TextBlock.TextProperty, "usages.empty");
    private int _generation;

    public UsagesPanel()
    {
        // The hint shows until a search puts a line of its own into the status.
        _status.PropertyChanged += (_, e) => { if (e.Property == TextBlock.TextProperty) _empty.IsVisible = string.IsNullOrEmpty(_status.Text); };
        _list.ItemTemplate = new FuncDataTemplate<UsageHit>((hit, scope) =>
        {
            var text = new TextBlock { Text = hit is null ? "" : $"{OwnerLabel?.Invoke(hit) ?? hit.OwnerName}  {hit.Where}".TrimEnd(), TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis };
            if (hit is null) return text;
            HoverCards.Attach(text, hit.OwnerType is null ? null : ResourceTypes.KeyOf(hit.OwnerType, hit.OwnerName), text.Text, hit.Where);
            if (hit.OwnerType == "code" && hit.Where.Contains(" @", StringComparison.Ordinal) && DetailOf is { } detail) _ = ShowDetailAsync(text, hit, detail);
            return text;
        });
        _list.DoubleTapped += (_, _) => { if (_list.SelectedItem is UsageHit hit) OpenRequested?.Invoke(hit); };
        _list.KeyDown += (_, e) => { if (e.Key == Avalonia.Input.Key.Enter && _list.SelectedItem is UsageHit hit) { e.Handled = true; OpenRequested?.Invoke(hit); } };
        _remove.Click += (_, _) => RemoveSelected();
        _exclude.Click += (_, _) => ExcludeSelected();
        var layout = new DockPanel { Margin = new Avalonia.Thickness(8) };
        DockPanel.SetDock(_status, Avalonia.Controls.Dock.Top);
        DockPanel.SetDock(_remove, Avalonia.Controls.Dock.Top);
        DockPanel.SetDock(_exclude, Avalonia.Controls.Dock.Top);
        DockPanel.SetDock(_note, Avalonia.Controls.Dock.Top);
        DockPanel.SetDock(_excludeNote, Avalonia.Controls.Dock.Top);
        DockPanel.SetDock(_empty, Avalonia.Controls.Dock.Top);
        layout.Children.Add(_status);
        layout.Children.Add(_empty);
        layout.Children.Add(_note);
        layout.Children.Add(_remove);
        layout.Children.Add(_exclude);
        layout.Children.Add(_excludeNote);
        layout.Children.Add(_list);
        Content = layout;
    }

    /// <summary>The text a row shows for the entry that holds a reference (its nickname); null shows the name.</summary>
    public Func<UsageHit, string>? OwnerLabel { get; set; }

    /// <summary>The line and text of a hit that has only an instruction offset; asked for when its row shows, so a long list is not decompiled up front.</summary>
    public Func<UsageHit, Task<string?>>? DetailOf { get; set; }

    private static async Task ShowDetailAsync(TextBlock row, UsageHit hit, Func<UsageHit, Task<string?>> detail)
    {
        try
        {
            if (await detail(hit) is { Length: > 0 } found) row.Text = $"{row.Text}  {found}";
        }
        catch (Exception e) when (e is InvalidOperationException or OperationCanceledException) { } // the row stays as it was
    }

    public IReadOnlyList<UsageHit> Hits => [.. _list.Items.OfType<UsageHit>()];

    public string Status => _status.Text ?? "";

    public event Action<UsageHit>? OpenRequested;

    /// <summary>Asked for by Remove Selected under the unreferenced assets; the window deletes them through the usual delete question.</summary>
    public event Action<IReadOnlyList<UsageHit>>? RemoveRequested;

    /// <summary>Asks to remove the selected rows of the unreferenced list.</summary>
    public void RemoveSelected()
    {
        if (_remove.IsVisible && _list.SelectedItems?.OfType<UsageHit>().ToList() is { Count: > 0 } selected) RemoveRequested?.Invoke(selected);
    }

    /// <summary>Takes the selected rows of the unreferenced list out of it, so Remove Selected and Select All leave them alone. The list is rebuilt by the next search.</summary>
    public void ExcludeSelected()
    {
        if (!_exclude.IsVisible || _list.SelectedItems?.OfType<UsageHit>().ToHashSet() is not { Count: > 0 } picked) return;
        var left = Hits.Where(h => !picked.Contains(h)).ToList();
        _list.ItemsSource = left;
        _remove.IsVisible = _exclude.IsVisible = left.Count > 0;
        _status.Text = Loc.Current.Format(left.Count == 0 ? "usages.unrefNone" : "usages.unrefFound", ("count", left.Count));
    }

    /// <summary>Selects every row of the list.</summary>
    public void SelectAllRows() => _list.SelectAll();

    public void Clear()
    {
        _generation++; // a search that is still running must not fill the panel afterwards
        _list.ItemsSource = null;
        _remove.IsVisible = _exclude.IsVisible = _note.IsVisible = false;
        _status.Text = "";
    }

    /// <summary>Shows the assets nothing refers to.</summary>
    public async Task ShowUnreferencedAsync(Task<IReadOnlyList<UsageHit>> search)
    {
        var generation = ++_generation;
        _list.ItemsSource = null;
        _remove.IsVisible = _exclude.IsVisible = false;
        _note.IsVisible = true;
        _status.Text = Loc.Current["usages.unrefSearching"];
        try
        {
            var hits = await search;
            if (generation != _generation) return;
            _list.ItemsSource = hits;
            _remove.IsVisible = _exclude.IsVisible = hits.Count > 0;
            _status.Text = Loc.Current.Format(hits.Count == 0 ? "usages.unrefNone" : "usages.unrefFound", ("count", hits.Count));
        }
        catch (Exception e) when (e is InvalidOperationException or OperationCanceledException && generation == _generation)
        {
            _status.Text = Loc.Current.Format("usages.unrefFailed", ("reason", e.Message));
        }
    }

    /// <summary>Shows what <paramref name="name"/> uses; <paramref name="shown"/> is the text the status line uses for it (a nickname).</summary>
    public Task ShowUsesAsync(string name, Task<IReadOnlyList<UsageHit>> search, string? shown = null) => ShowAsync(shown ?? name, search, "usages.usesSearching", "usages.usesFound", "usages.usesNone");

    /// <summary>Shows the usages of <paramref name="name"/>; a search that fails shows why. <paramref name="shown"/> is the text the status line uses for it (a nickname).</summary>
    public Task ShowAsync(string name, Task<IReadOnlyList<UsageHit>> search, string? shown = null)
    {
        Subject = name;
        return ShowAsync(shown ?? name, search, "usages.searching", "usages.found", "usages.none");
    }

    /// <summary>The name the list was made for, which the uses in code and in properties carry; empty for the other lists.</summary>
    public string Subject { get; private set; } = "";

    private async Task ShowAsync(string name, Task<IReadOnlyList<UsageHit>> search, string searching, string found, string none)
    {
        var generation = ++_generation;
        _list.ItemsSource = null;
        _remove.IsVisible = _exclude.IsVisible = _note.IsVisible = false;
        _status.Text = Loc.Current.Format(searching, ("name", name));
        try
        {
            var hits = await search;
            if (generation != _generation) return;
            _list.ItemsSource = hits;
            _status.Text = Loc.Current.Format(hits.Count == 0 ? none : found, ("name", name), ("count", hits.Count));
        }
        catch (Exception e) when (e is InvalidOperationException or OperationCanceledException && generation == _generation)
        {
            _status.Text = Loc.Current.Format("usages.failed", ("name", name), ("reason", e.Message));
        }
    }
}
