using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using G3MStudio.Core.Workspace;
using G3MStudio.Theme;

namespace G3MStudio.Explorer;

/// <summary>Visual cues of the list: the flash on the row of a new item and the highlight of the row a drag would drop onto.</summary>
public sealed partial class ExplorerView
{
    private ListBoxItem? _dropRow;

    /// <summary>Flashes the row of the item with this tab key once it is on screen (a single row only; nothing happens when the row is not realized).</summary>
    public void Flash(string key)
    {
        if (ResourceTypes.SplitKey(key) is not var (type, name)) return;
        Dispatcher.UIThread.Post(() =>
        {
            var index = Model.Rows.ToList().FindIndex(r => r.IsItem && r.Key == type && r.Text == name);
            if (index >= 0 && List.ContainerFromIndex(index) is { } container) Motion.Flash(container);
        }, DispatcherPriority.Loaded);
    }

    // The row under the pointer shows that it takes the drop; leaving or dropping clears it.
    private void MarkDrop(DragEventArgs e)
    {
        var row = DropTarget(e) is null ? null : (e.Source as Visual)?.FindAncestorOfType<ListBoxItem>();
        if (ReferenceEquals(row, _dropRow)) return;
        ClearDrop();
        _dropRow = row;
        _dropRow?.Classes.Add("dropTarget");
    }

    private void ClearDrop()
    {
        _dropRow?.Classes.Remove("dropTarget");
        _dropRow = null;
    }
}
