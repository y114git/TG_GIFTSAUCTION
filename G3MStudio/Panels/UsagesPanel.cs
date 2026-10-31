using Avalonia.Controls;
using Avalonia.Controls.Templates;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

namespace G3MStudio.Panels;

/// <summary>Usages: where the resource asked for is used. A double click asks the window to open the entry that holds the reference.</summary>
public sealed class UsagesPanel : UserControl
{
    private readonly TextBlock _status = new() { Name = "UsagesStatus", Classes = { "muted" }, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private readonly ListBox _list = new() { Name = "UsagesList" };
    private int _generation;

    public UsagesPanel()
    {
        _list.ItemTemplate = new FuncDataTemplate<UsageHit>((hit, _) => new TextBlock { Text = hit is null ? "" : $"{hit.OwnerName}  {hit.Where}", TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis });
        _list.DoubleTapped += (_, _) => { if (_list.SelectedItem is UsageHit hit) OpenRequested?.Invoke(hit); };
        var layout = new DockPanel { Margin = new Avalonia.Thickness(8) };
        DockPanel.SetDock(_status, Avalonia.Controls.Dock.Top);
        layout.Children.Add(_status);
        layout.Children.Add(_list);
        Content = layout;
    }

    public IReadOnlyList<UsageHit> Hits => [.. _list.Items.OfType<UsageHit>()];

    public string Status => _status.Text ?? "";

    public event Action<UsageHit>? OpenRequested;

    public void Clear()
    {
        _generation++; // a search that is still running must not fill the panel afterwards
        _list.ItemsSource = null;
        _status.Text = "";
    }

    /// <summary>Shows the usages of <paramref name="name"/>; a search that fails shows why.</summary>
    public async Task ShowAsync(string name, Task<IReadOnlyList<UsageHit>> search)
    {
        var generation = ++_generation;
        _list.ItemsSource = null;
        _status.Text = Loc.Current.Format("usages.searching", ("name", name));
        try
        {
            var hits = await search;
            if (generation != _generation) return;
            _list.ItemsSource = hits;
            _status.Text = Loc.Current.Format(hits.Count == 0 ? "usages.none" : "usages.found", ("name", name), ("count", hits.Count));
        }
        catch (Exception e) when (e is InvalidOperationException or OperationCanceledException && generation == _generation)
        {
            _status.Text = Loc.Current.Format("usages.failed", ("name", name), ("reason", e.Message));
        }
    }
}
