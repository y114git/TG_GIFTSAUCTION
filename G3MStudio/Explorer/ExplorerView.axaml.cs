using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using G3MLib.DataFile;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

namespace G3MStudio.Explorer;

/// <summary>The resource tree: a filter box over a virtualized flat list of <see cref="ExplorerRow"/> (design/shell.md section 5).</summary>
public partial class ExplorerView : UserControl
{
    public static readonly IValueConverter Indent = new FuncValueConverter<int, Thickness>(depth => new Thickness(depth * 14, 0, 0, 0));

    private string _name = "";
    private bool _dirty;

    public ExplorerView()
    {
        InitializeComponent();
        Model = new ExplorerModel(key => Loc.Current["explorer.type." + key]);
        Model.Changed += ShowRows;
        FilterBox.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) Model.SetFilter(FilterBox.Text ?? ""); };
        List.Tapped += (_, e) =>
        {
            if ((e.Source as Control)?.DataContext is not ExplorerRow row) return;
            if (row.IsItem) RequestOpen(row, true);
            else Model.Toggle(row.Key);
        };
        List.SelectionChanged += (_, _) => SelectionChanged?.Invoke();
        // A drag starts after the pointer has moved a few pixels with the button down on an entry.
        List.AddHandler(PointerPressedEvent, (_, e) => _press = e.GetCurrentPoint(List).Properties.IsLeftButtonPressed && e.Source is Control { DataContext: ExplorerRow { IsItem: true } } ? e : null, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        List.PointerReleased += (_, _) => _press = null;
        List.PointerMoved += async (_, e) =>
        {
            if (_press is not { } press || (e.GetPosition(List) - press.GetPosition(List)).X is var dx && (e.GetPosition(List) - press.GetPosition(List)).Y is var dy && dx * dx + dy * dy < 64) return;
            _press = null;
            if (press.Source is not Control { DataContext: ExplorerRow { IsItem: true } row }) return;
            var item = new DataTransferItem();
            item.Set(ResourceDrag.Format, ResourceTypes.KeyOf(row.Key, row.Text));
            var data = new DataTransfer();
            data.Add(item);
            await DragDrop.DoDragDropAsync(press, data, DragDropEffects.Copy);
        };
        List.DoubleTapped += (_, e) => { if (e.Source is Control { DataContext: ExplorerRow { IsItem: true } row }) RequestOpen(row, false); };
        // Tunnel: the list handles the arrow keys itself and marks them handled before a bubbling handler would run.
        List.AddHandler(KeyDownEvent, OnKeyDown, Avalonia.Interactivity.RoutingStrategies.Tunnel);
    }

    private PointerPressedEventArgs? _press;

    public ExplorerModel Model { get; }

    /// <summary>An entry was clicked (preview true) or double-clicked or confirmed with Enter (preview false).</summary>
    public event Action<ExplorerRow, bool>? OpenRequested;

    public event Action? SelectionChanged;

    public void RequestOpen(ExplorerRow row, bool preview) => OpenRequested?.Invoke(row, preview);

    /// <summary>Tab key of the selected entry, or null.</summary>
    public string? SelectedKey => List.SelectedItem is ExplorerRow { IsItem: true } row ? ResourceTypes.KeyOf(row.Key, row.Text) : null;

    /// <summary>Selects the entry with the given tab key, opening its type first.</summary>
    public void Select(string? key)
    {
        if (key is null || ResourceTypes.SplitKey(key) is not var (type, name)) return;
        if (Model.Rows.Any(r => r is { IsItem: false, Expanded: false } && r.Key == type)) Model.Toggle(type);
        var index = Model.Rows.ToList().FindIndex(r => r.IsItem && r.Key == type && r.Text == name);
        if (index < 0) return;
        List.SelectedIndex = index;
        List.ScrollIntoView(index);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Loc.Current.PropertyChanged += OnLocalizerChanged;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        Loc.Current.PropertyChanged -= OnLocalizerChanged;
        base.OnDetachedFromVisualTree(e);
    }

    private void OnLocalizerChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Loc.Current.Language)) Model.Reload();
    }

    public void FocusFilter() => FilterBox.Focus();

    public void Load(string name, GameMakerData? data)
    {
        _name = name;
        FilterBox.Text = "";
        Model.Load(data);
        ShowHeader();
    }

    /// <summary>Names changed (rename, undo) or the dirty mark moved.</summary>
    public void Refresh(bool dirty)
    {
        _dirty = dirty;
        ShowHeader();
        Model.Reload();
    }

    private void ShowHeader() => Header.Text = (_dirty ? "● " : "") + _name;

    private void ShowRows()
    {
        // Setting a new list resets the scroll position; put it back so expanding a type does not jump.
        var scroll = List.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
        var offset = scroll?.Offset ?? default;
        List.ItemsSource = Model.Rows;
        if (scroll is not null && !Model.IsFiltering) Dispatcher.UIThread.Post(() => scroll.Offset = offset, DispatcherPriority.Loaded);

        Footer.Text = Model.IsFiltering
            ? Loc.Current.Format("explorer.matches", ("matches", Model.Matches), ("total", Model.Total))
            : Loc.Current.Format("explorer.count", ("count", Model.Total));
        NoMatch.IsVisible = Model.IsFiltering && Model.Matches == 0;
        NoMatch.Text = Loc.Current.Format("explorer.noMatch", ("filter", Model.Filter));
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && List.SelectedItem is ExplorerRow { IsItem: true } item)
        {
            RequestOpen(item, false);
            e.Handled = true;
            return;
        }

        if (List.SelectedItem is not ExplorerRow { IsItem: false } row) return;
        var toggle = e.Key is Key.Enter or Key.Space || e.Key == Key.Right && !row.Expanded || e.Key == Key.Left && row.Expanded;
        if (!toggle) return;
        Model.Toggle(row.Key);
        e.Handled = true;
    }
}
