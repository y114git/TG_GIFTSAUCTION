using System.Text.Json;

namespace G3MStudio.Core.Workspace;

/// <summary>One editor tab: the resource it shows and its state. Placement (group, order) lives in the dock layout.</summary>
public sealed record TabInfo(string Id, string Kind, string Key, bool Preview = false, bool Pinned = false);

/// <summary>The tab rules of design/shell.md 9.1 and 9.2 without any UI: one tab per resource, preview replacement, pinning.</summary>
public sealed class EditorTabs
{
    private readonly List<TabInfo> _tabs = [];

    public IReadOnlyList<TabInfo> Tabs => _tabs;

    public TabInfo? Get(string id) => _tabs.Find(t => t.Id == id);

    public TabInfo? Find(string kind, string key) => _tabs.Find(t => t.Kind == kind && t.Key == key);

    /// <summary>
    /// Focuses the tab of the resource or creates it. A normal open turns an existing preview tab into a normal one.
    /// A new preview tab replaces the preview tab of the same group, which <paramref name="sameGroup"/> identifies.
    /// </summary>
    public (TabInfo Tab, TabInfo? Replaced, bool Created) Open(string kind, string key, bool preview, Func<TabInfo, bool> sameGroup)
    {
        if (Find(kind, key) is { } existing)
        {
            if (existing.Preview && !preview) existing = Update(existing with { Preview = false });
            return (existing, null, false);
        }

        var replaced = preview ? _tabs.Find(t => t.Preview && sameGroup(t)) : null;
        if (replaced is not null) _tabs.Remove(replaced);
        var tab = new TabInfo(Guid.NewGuid().ToString("N")[..12], kind, key, preview);
        _tabs.Add(tab);
        return (tab, replaced, true);
    }

    public void KeepOpen(string id)
    {
        if (Get(id) is { Preview: true } tab) Update(tab with { Preview = false });
    }

    public void SetPinned(string id, bool pinned)
    {
        if (Get(id) is { } tab) Update(tab with { Pinned = pinned, Preview = !pinned && tab.Preview });
    }

    /// <summary>A resource was renamed: tabs of the old key follow it.</summary>
    public void Rekey(string oldKey, string newKey)
    {
        for (var i = 0; i < _tabs.Count; i++)
            if (_tabs[i].Key == oldKey) _tabs[i] = _tabs[i] with { Key = newKey };
    }

    public void Remove(string id) => _tabs.RemoveAll(t => t.Id == id);

    /// <summary>Puts a saved list back (workspace restore).</summary>
    public void Load(IEnumerable<TabInfo> tabs)
    {
        _tabs.Clear();
        _tabs.AddRange(tabs);
    }

    /// <summary>Tabs of resources that no longer exist are dropped; returns them.</summary>
    public List<TabInfo> Prune(Func<TabInfo, bool> exists)
    {
        var gone = _tabs.Where(t => !exists(t)).ToList();
        _tabs.RemoveAll(gone.Contains);
        return gone;
    }

    private TabInfo Update(TabInfo tab)
    {
        _tabs[_tabs.FindIndex(t => t.Id == tab.Id)] = tab;
        return tab;
    }
}

/// <summary>What is restored when a data file is reopened: the dock layout text, the tabs and the Explorer selection.</summary>
/// <summary>Rooms maps a room tab's resource key to its zoom, left and top (the view of the canvas).</summary>
public sealed record ViewState(string Dock, List<TabInfo> Tabs, string? Selected = null, Dictionary<string, double[]>? Rooms = null);

/// <summary>Per data file view state in the user data folder, keyed like the journal (OI-073).</summary>
public sealed class ViewStateStore(UserData userData)
{
    private string FileFor(string dataFile) => userData.Combine("view", UserData.KeyFor(dataFile) + ".json");

    public void Save(string dataFile, ViewState state) => UserData.WriteAllTextAtomic(FileFor(dataFile), JsonSerializer.Serialize(state));

    /// <summary>The saved state, or null when there is none or the file is damaged (only a lost convenience).</summary>
    public ViewState? Load(string dataFile)
    {
        try
        {
            return JsonSerializer.Deserialize<ViewState>(File.ReadAllText(FileFor(dataFile)));
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
