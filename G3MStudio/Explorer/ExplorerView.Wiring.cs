using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Explorer;

public partial class ExplorerView
{
    private PointerPressedEventArgs? _press;
    private readonly DispatcherTimer _filterTimer = new() { Interval = TimeSpan.FromMilliseconds(150) };

    /// <summary>Applies the text of the filter box to the model now, instead of when typing pauses.</summary>
    public void FlushFilter()
    {
        _filterTimer.Stop();
        Model.SetFilter(FilterBox.Text ?? "");
    }

    private void WireList()
    {
        // The rebuild of a 200,000 entry list takes 30 to 100 ms: it waits until typing pauses. Anything but typing calls FlushFilter.
        _filterTimer.Tick += (_, _) => FlushFilter();
        FilterBox.PropertyChanged += (_, e) =>
        {
            if (e.Property != TextBox.TextProperty) return;
            _filterTimer.Stop();
            _filterTimer.Start();
        };
        List.Tapped += (_, e) =>
        {
            if ((e.Source as Control)?.DataContext is not ExplorerRow row) return;
            // Ctrl and Shift clicks only change the selection; they must not open a tab.
            if (e.KeyModifiers is KeyModifiers.Control or KeyModifiers.Shift) return;
            if (row.IsItem) RequestOpen(row, true);
            else if (row.Kind == ExplorerRowKind.Search) { FilterBox.Text = row.Text; FlushFilter(); }
            else if (row.IsHeader)
            {
                Model.Toggle(row.Key);
                G3MStudio.Theme.Motion.Fold(List);
            }
        };
        List.SelectionMode = SelectionMode.Multiple;
        // The tooltip is made per row when the row appears; what it shows is looked up and its picture fetched only when the tip opens.
        List.ContainerPrepared += (_, e) =>
        {
            if (e.Container.DataContext is not ExplorerRow row) return;
            HoverCards.Attach(e.Container, row.IsItem ? ResourceTypes.KeyOf(row.Key, row.Text) : null, row.Tip);
        };
        List.SelectionChanged += (_, _) =>
        {
            ShowSelected();
            KeepSelectedClearOfSticky();
            SelectionChanged?.Invoke();
        };
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
            if ((row.IsItem || row.IsHeader) && List.SelectedItems?.Contains(row) != true) List.SelectedItem = row;
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
        List.AddHandler(DragDrop.DragOverEvent, (_, e) =>
        {
            // Ctrl lists the entry in the folder as well instead of moving it.
            e.DragEffects = e.DataTransfer.TryGetValue(ResourceDrag.Keys) is not null && DropTarget(e) is { } target ? (e.KeyModifiers.HasFlag(KeyModifiers.Control) && target.Kind == ExplorerRowKind.Folder ? DragDropEffects.Copy : DragDropEffects.Move) : DragDropEffects.None;
            MarkDrop(e);
        });
        List.AddHandler(DragDrop.DragLeaveEvent, (_, _) => ClearDrop());
        List.AddHandler(DragDrop.DropEvent, (_, e) =>
        {
            ClearDrop();
            if (e.DataTransfer.TryGetValue(ResourceDrag.Keys) is not { } text || DropTarget(e) is not { } target) return;
            DropOnto?.Invoke(target, [.. text.Split('\n', StringSplitOptions.RemoveEmptyEntries)], e.KeyModifiers.HasFlag(KeyModifiers.Control));
            e.Handled = true;
        });
        List.AddHandler(TextInputEvent, OnTextInput, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        // Tunnel: the list handles the arrow keys itself and marks them handled before a bubbling handler would run.
        List.AddHandler(KeyDownEvent, OnKeyDown, Avalonia.Interactivity.RoutingStrategies.Tunnel);
    }
}
