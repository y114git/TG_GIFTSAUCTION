using System.Collections.ObjectModel;
using System.Collections.Specialized;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Explorer;

/// <summary>
/// The rows the Explorer list shows. A new row set is compared with the shown one; when only one stretch differs (a type opened or closed) that stretch is
/// reported as inserted or removed, so the list keeps its items and its scroll position instead of building every visible row again.
/// </summary>
public sealed class ExplorerRowList : ObservableCollection<ExplorerRow>
{
    // A changed stretch this long on both sides is cheaper to show as a new list than as a removal plus an insertion.
    private const int ResetAbove = 200;

    /// <summary>Makes the list equal to <paramref name="next"/>. True when the list was reset, which drops the scroll position.</summary>
    public bool Update(IReadOnlyList<ExplorerRow> next)
    {
        var shown = (List<ExplorerRow>)Items;
        var most = Math.Min(shown.Count, next.Count);
        var head = 0;
        while (head < most && Same(shown[head], next[head])) head++;
        var tail = 0;
        while (tail < most - head && Same(shown[^(tail + 1)], next[^(tail + 1)])) tail++;
        var removed = shown.Count - head - tail;
        var added = next.Count - head - tail;
        if (removed == 0 && added == 0) return false;
        if (removed > ResetAbove && added > ResetAbove)
        {
            shown.Clear();
            shown.AddRange(next);
            OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs(nameof(Count)));
            OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs("Item[]"));
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            return true;
        }

        // The heading that opens or closes changes in place; its entries come or go behind it.
        var keep = removed > 0 && added > 0 ? 1 : 0;
        if (keep == 1)
        {
            var old = shown[head];
            shown[head] = next[head];
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, next[head], old, head));
        }

        if (removed - keep > 0)
        {
            var gone = shown.GetRange(head + keep, removed - keep);
            shown.RemoveRange(head + keep, removed - keep);
            Notify(NotifyCollectionChangedAction.Remove, gone, head + keep);
        }

        if (added - keep > 0)
        {
            var come = new List<ExplorerRow>(added - keep);
            for (var i = head + keep; i < head + added; i++) come.Add(next[i]);
            shown.InsertRange(head + keep, come);
            Notify(NotifyCollectionChangedAction.Add, come, head + keep);
        }

        return false;
    }

    private void Notify(NotifyCollectionChangedAction action, List<ExplorerRow> rows, int at)
    {
        OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(action, rows, at));
    }

    private static bool Same(ExplorerRow a, ExplorerRow b) => ReferenceEquals(a, b) || a.Equals(b);
}
