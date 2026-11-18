using Dock.Model.Core;
using Dock.Model.Avalonia.Controls;

namespace G3MStudio.Docking;

/// <summary>Closing, pinning and splitting editor tabs.</summary>
public sealed partial class StudioDock
{
    /// <summary>Asked before a tab closes by the user: false holds the close back (the owner asks, then closes the tab again). Closes of a whole group (Close All) skip it.</summary>
    public Func<string, bool>? CloseGuard { get; set; }

    /// <summary>Closes a tab; <paramref name="immediate"/> skips the close guard and the fade, for a tab whose resource is gone.</summary>
    public void CloseTab(string id, bool immediate = false)
    {
        if (DocumentOf(id) is not { } doc) return;
        if (immediate) CloseNow(doc);
        else Factory.CloseDockable(doc);
    }

    /// <summary>Closes every tab that is not pinned.</summary>
    public void CloseAll()
    {
        foreach (var doc in Documents().Where(d => Tabs.Get(d.Id)?.Pinned != true).ToList()) CloseNow(doc);
    }

    /// <summary>Closes every tab that is not pinned except the one named.</summary>
    public void CloseOthers(string keep)
    {
        foreach (var doc in Documents().Where(d => d.Id != keep && Tabs.Get(d.Id)?.Pinned != true).ToList()) CloseNow(doc);
    }

    public void SetPinned(string id, bool pinned)
    {
        Tabs.SetPinned(id, pinned);
        if (DocumentOf(id) is not { Owner: IDock group } doc || Tabs.Get(id) is not { } tab) return;
        Mark(doc, tab);
        // Pinned tabs sit first in the strip (design/shell.md 9.1).
        Factory.RemoveDockable(doc, false);
        var items = group.VisibleDockables ?? [];
        var index = pinned ? items.Count(d => Tabs.Get(d.Id ?? "")?.Pinned == true) : items.Count;
        Factory.InsertDockable(group, doc, index);
        Activate(doc);
        Changed?.Invoke();
    }

    /// <summary>Builds the editor of a tab again in its host, so texts that were read once (a language change) are read anew.</summary>
    public void Rebuild(string id)
    {
        if (DocumentOf(id) is { } doc && Tabs.Get(id) is { } tab) Show(doc, tab);
    }

    public void KeepOpen(string id)
    {
        Tabs.KeepOpen(id);
        if (DocumentOf(id) is { } doc && Tabs.Get(id) is { } tab) Mark(doc, tab);
        Changed?.Invoke();
    }

    /// <summary>Moves the tab into a new group beside its group; ignored beyond <see cref="MaxGroups"/> groups.</summary>
    public void SplitTab(string id, DockOperation side)
    {
        if (DocumentOf(id) is not { Owner: IDock from } doc || from.VisibleDockables?.Count < 2 || Groups().Count() >= MaxGroups) return;
        // SplitToDock wraps a dockable in a plain proportional dock; handing it an empty group makes the group the sibling.
        var group = new DocumentDock { CanCreateDocument = false, VisibleDockables = Factory.CreateList<IDockable>() };
        Factory.SplitToDock(from, group, side);
        Factory.MoveDockable(from, group, doc, null);
        Activate(doc);
    }
}
