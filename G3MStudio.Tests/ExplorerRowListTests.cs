using System.Collections.Specialized;
using G3MStudio.Core.Workspace;
using G3MStudio.Explorer;

namespace G3MStudio.Tests;

public sealed class ExplorerRowListTests
{
    private static ExplorerRow Item(string type, int i) => new(ExplorerRowKind.Item, type, "n" + i, 2);

    private static List<ExplorerRow> Rows(bool openA, bool openB, int a = 500, int b = 500)
    {
        var rows = new List<ExplorerRow> { new(ExplorerRowKind.Type, "a", "A", 1) { Count = a, Expanded = openA } };
        if (openA) rows.AddRange(Enumerable.Range(0, a).Select(i => Item("a", i)));
        rows.Add(new ExplorerRow(ExplorerRowKind.Type, "b", "B", 1) { Count = b, Expanded = openB });
        if (openB) rows.AddRange(Enumerable.Range(0, b).Select(i => Item("b", i)));
        return rows;
    }

    [Fact]
    public void OpeningAndClosingATypeIsAnInsertionAndARemovalNotAReset()
    {
        var list = new ExplorerRowList();
        var actions = new List<NotifyCollectionChangedAction>();
        list.Update(Rows(false, false));
        list.CollectionChanged += (_, e) => actions.Add(e.Action);

        var open = Rows(true, false);
        Assert.False(list.Update(open));
        Assert.Equal(open, list);
        Assert.Equal([NotifyCollectionChangedAction.Replace, NotifyCollectionChangedAction.Add], actions);

        actions.Clear();
        var close = Rows(false, false);
        Assert.False(list.Update(close));
        Assert.Equal(close, list);
        Assert.Equal([NotifyCollectionChangedAction.Replace, NotifyCollectionChangedAction.Remove], actions);
    }

    [Fact]
    public void ANewRowSetStaysEqualWhateverChanged()
    {
        var list = new ExplorerRowList();
        list.Update(Rows(true, true));
        foreach (var next in new[] { Rows(false, true), Rows(true, false), Rows(true, true, 3, 900), Rows(false, false, 0, 0), Rows(true, true, 2, 2) })
        {
            list.Update(next);
            Assert.Equal(next, list);
        }

        var before = list.ToList();
        Assert.False(list.Update(before));
    }
}
