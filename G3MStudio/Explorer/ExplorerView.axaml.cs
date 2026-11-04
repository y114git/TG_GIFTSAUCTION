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
    private bool _legacy;

    public ExplorerView()
    {
        InitializeComponent();
        // A GameMaker Studio 1 game has backgrounds, not tile sets (UTMT-TREE-10).
        Model = new ExplorerModel(key => key == "null" ? Loc.Current["explorer.nullEntry"] : Loc.Current["explorer.type." + (key == "tileSets" && _legacy ? "backgrounds" : key)]);
        Model.Changed += ShowRows;
        List.ItemsSource = _rows;
        FilterBox.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) Model.SetFilter(FilterBox.Text ?? ""); };
        List.Tapped += (_, e) =>
        {
            if ((e.Source as Control)?.DataContext is not ExplorerRow row) return;
            // Ctrl and Shift clicks only change the selection; they must not open a tab.
            if (e.KeyModifiers is KeyModifiers.Control or KeyModifiers.Shift) return;
            if (row.IsItem) RequestOpen(row, true);
            else if (row.IsHeader) Model.Toggle(row.Key);
        };
        List.SelectionMode = SelectionMode.Multiple;
        // The tooltip is made per row when the row appears; its picture is fetched only when the tip opens.
        List.ContainerPrepared += (_, e) =>
        {
            if (e.Container.DataContext is not ExplorerRow row) return;
            var key = row.IsItem ? ResourceTypes.KeyOf(row.Key, row.Text) : null;
            Func<Task<Avalonia.Media.IImage?>>? load = key is null ? null : () => Thumbnail?.Invoke(key) ?? Task.FromResult<Avalonia.Media.IImage?>(null);
            if (ToolTip.GetTip(e.Container) is ExplorerRowTip tip) tip.Retarget(row.Text, load);
            else ToolTip.SetTip(e.Container, new ExplorerRowTip(row.Text, load));
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
        List.AddHandler(PointerPressedEvent, (_, e) =>
        {
            if (!e.GetCurrentPoint(List).Properties.IsRightButtonPressed || e.Source is not Control { DataContext: ExplorerRow row } source) return;
            if (row.IsItem && !List.SelectedItems!.Contains(row)) List.SelectedItem = row;
            MenuRequested?.Invoke(row, source);
            e.Handled = true;
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        // A middle click opens the entry in a tab of its own, like a double click (UTMT-TREE-02).
        List.AddHandler(PointerPressedEvent, (_, e) =>
        {
            if (!e.GetCurrentPoint(List).Properties.IsMiddleButtonPressed || e.Source is not Control { DataContext: ExplorerRow { IsItem: true } row }) return;
            RequestOpen(row, false);
            e.Handled = true;
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        List.DoubleTapped += (_, e) => { if (e.Source is Control { DataContext: ExplorerRow { IsItem: true } row }) RequestOpen(row, false); };
        // Tunnel: the list handles the arrow keys itself and marks them handled before a bubbling handler would run.
        List.AddHandler(KeyDownEvent, OnKeyDown, Avalonia.Interactivity.RoutingStrategies.Tunnel);
    }

    private PointerPressedEventArgs? _press;

    private readonly ExplorerRowList _rows = [];

    public ExplorerModel Model { get; }

    /// <summary>An entry was clicked (preview true) or double-clicked or confirmed with Enter (preview false).</summary>
    public event Action<ExplorerRow, bool>? OpenRequested;

    public event Action? SelectionChanged;

    /// <summary>A right click on a row (already selected): the row and the control to open the menu at.</summary>
    public event Action<ExplorerRow, Control>? MenuRequested;

    /// <summary>Loads the picture for the tooltip of the entry with this tab key; null when the entry has none.</summary>
    public Func<string, Task<Avalonia.Media.IImage?>>? Thumbnail { get; set; }

    public void RequestOpen(ExplorerRow row, bool preview) => OpenRequested?.Invoke(row, preview);

    /// <summary>Tab key of the selected entry, or null.</summary>
    public string? SelectedKey => List.SelectedItem is ExplorerRow { IsItem: true } row ? ResourceTypes.KeyOf(row.Key, row.Text) : null;

    /// <summary>Tab keys of every selected entry (Ctrl and Shift clicks select several), in list order; headings are left out.</summary>
    public IReadOnlyList<string> SelectedKeys => [.. List.SelectedItems?.OfType<ExplorerRow>().Where(r => r.IsItem).Select(r => ResourceTypes.KeyOf(r.Key, r.Text)) ?? []];

    /// <summary>Selects the entry with the given tab key, opening its type first.</summary>
    public void Select(string? key)
    {
        if (key is null || ResourceTypes.SplitKey(key) is not var (type, name)) return;
        if (Model.Rows.Any(r => r is { IsHeader: true, Expanded: false } && r.Key == type)) Model.Toggle(type);
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
        _legacy = data is not null && !data.IsGameMaker2();
        FilterBox.Text = "";
        Model.Load(data);
        ShowHeader();
    }

    /// <summary>Names changed (rename, undo) or the dirty mark moved.</summary>
    public void Refresh(bool dirty, bool namesChanged = true)
    {
        _dirty = dirty;
        ShowHeader();
        if (namesChanged) Model.Refresh();
    }

    private void ShowHeader() => Header.Text = (_dirty ? "● " : "") + _name;

    private void ShowRows()
    {
        // Opening or closing a type is shown as an insertion or removal, which keeps the shown rows and the scroll position. A list that is replaced
        // (a filter) resets the scroll position; it is put back so a language change or an edit does not jump.
        var scroll = List.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
        var offset = scroll?.Offset ?? default;
        if (_rows.Update(Model.Rows) && scroll is not null && !Model.IsFiltering) Dispatcher.UIThread.Post(() => scroll.Offset = offset, DispatcherPriority.Loaded);

        Footer.Text = Model.IsFiltering
            ? Loc.Current.Format("explorer.matches", ("matches", Model.Matches.ToString("N0", System.Globalization.CultureInfo.CurrentCulture)), ("total", Model.Total.ToString("N0", System.Globalization.CultureInfo.CurrentCulture)))
            : Loc.Current.Format("explorer.count", ("count", Model.Total.ToString("N0", System.Globalization.CultureInfo.CurrentCulture)));
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

        if (List.SelectedItem is not ExplorerRow { IsHeader: true } row) return;
        var toggle = e.Key is Key.Enter or Key.Space || e.Key == Key.Right && !row.Expanded || e.Key == Key.Left && row.Expanded;
        if (!toggle) return;
        Model.Toggle(row.Key);
        e.Handled = true;
    }
}
