using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Explorer;

public partial class ExplorerView
{
    private PointerPressedEventArgs? _press;

    private void WireList()
    {
        FilterBox.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) Model.SetFilter(FilterBox.Text ?? ""); };
        List.Tapped += (_, e) =>
        {
            if ((e.Source as Control)?.DataContext is not ExplorerRow row) return;
            // Ctrl and Shift clicks only change the selection; they must not open a tab.
            if (e.KeyModifiers is KeyModifiers.Control or KeyModifiers.Shift) return;
            if (row.IsItem) RequestOpen(row, true);
            else if (row.Kind == ExplorerRowKind.Search) FilterBox.Text = row.Text;
            else if (row.IsHeader) Model.Toggle(row.Key);
        };
        List.SelectionMode = SelectionMode.Multiple;
        // The tooltip is made per row when the row appears; its picture is fetched only when the tip opens.
        List.ContainerPrepared += (_, e) =>
        {
            if (e.Container.DataContext is not ExplorerRow row) return;
            var key = row.IsItem && ResourceThumbnails.CanHavePicture(row.Key) ? ResourceTypes.KeyOf(row.Key, row.Text) : null;
            Func<Task<Avalonia.Media.IImage?>>? load = key is null ? null : () => Thumbnail?.Invoke(key) ?? Task.FromResult<Avalonia.Media.IImage?>(null);
            if (ToolTip.GetTip(e.Container) is ExplorerRowTip tip) tip.Retarget(row.Text, load);
            else ToolTip.SetTip(e.Container, new ExplorerRowTip(row.Text, load));
        };
        List.SelectionChanged += (_, _) => SelectionChanged?.Invoke();
    }

    private void WireThumbnails()
    {
        ZoomSlider.PropertyChanged += (_, e) =>
        {
            if (e.Property == Avalonia.Controls.Primitives.RangeBase.ValueProperty) ApplyZoom();
        };
        ThumbToggle.IsCheckedChanged += (_, _) => ApplyZoom();
        List.SizeChanged += (_, e) => { if (Thumbnails && e.WidthChanged) ApplyZoom(); };
        List.ContainerPrepared += (_, e) => Dispatcher.UIThread.Post(() => Dress(e.Container), DispatcherPriority.Loaded);
        // A row that scrolls away stops waiting for its picture.
        List.ContainerClearing += (_, e) =>
        {
            if (e.Container is ListBoxItem item && item.GetVisualDescendants().OfType<Image>().FirstOrDefault(i => i.Name == "Thumb") is { } thumb) CancelThumb(thumb);
        };
    }

    private void WireDrag()
    {
        // A drag starts after the pointer has moved a few pixels with the button down on an entry.
        List.AddHandler(PointerPressedEvent, (_, e) => _press = e.GetCurrentPoint(List).Properties.IsLeftButtonPressed && e.Source is Control { DataContext: ExplorerRow { IsItem: true } } ? e : null, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        List.PointerReleased += (_, _) => _press = null;
        List.PointerMoved += async (_, e) =>
        {
            if (_press is not { } press || (e.GetPosition(List) - press.GetPosition(List)).X is var dx && (e.GetPosition(List) - press.GetPosition(List)).Y is var dy && dx * dx + dy * dy < 64) return;
            _press = null;
            if (press.Source is not Control { DataContext: ExplorerRow { IsItem: true } row }) return;
            var item = new DataTransferItem();
            var key = ResourceTypes.KeyOf(row.Key, row.Text);
            item.Set(ResourceDrag.Format, key);
            // Dragging one of several selected rows takes them all.
            item.Set(ResourceDrag.Keys, string.Join('\n', List.SelectedItems?.Contains(row) == true && SelectedKeys.Count > 1 ? SelectedKeys : [key]));
            var data = new DataTransfer();
            data.Add(item);
            await DragDrop.DoDragDropAsync(press, data, DragDropEffects.Copy | DragDropEffects.Move); // the target answers Move, which the allowed effects must include
        };
    }

    private void WireClicks()
    {
        List.AddHandler(PointerPressedEvent, (_, e) =>
        {
            if (!e.GetCurrentPoint(List).Properties.IsRightButtonPressed || e.Source is not Control { DataContext: ExplorerRow row } source) return;
            if (row.IsItem && List.SelectedItems?.Contains(row) != true) List.SelectedItem = row;
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
        List.DoubleTapped += (_, e) =>
        {
            if (e.Source is not Control { DataContext: ExplorerRow row }) return;
            if (row.IsItem) RequestOpen(row, false);
            else if (ExplorerModel.ScopeOf(row) is { } scope) Model.Scope = scope;
        };
    }

    private void WireDrop()
    {
        DragDrop.SetAllowDrop(List, true);
        List.AddHandler(DragDrop.DragOverEvent, (_, e) => e.DragEffects = e.DataTransfer.TryGetValue(ResourceDrag.Keys) is not null && DropTarget(e) is not null ? DragDropEffects.Move : DragDropEffects.None);
        List.AddHandler(DragDrop.DropEvent, (_, e) =>
        {
            if (e.DataTransfer.TryGetValue(ResourceDrag.Keys) is not { } text || DropTarget(e) is not { } target) return;
            DropOnto?.Invoke(target, [.. text.Split('\n', StringSplitOptions.RemoveEmptyEntries)]);
            e.Handled = true;
        });
        List.AddHandler(TextInputEvent, OnTextInput, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        // Tunnel: the list handles the arrow keys itself and marks them handled before a bubbling handler would run.
        List.AddHandler(KeyDownEvent, OnKeyDown, Avalonia.Interactivity.RoutingStrategies.Tunnel);
    }
}
