using System.Text.Json;

namespace G3MStudio.Core.Workspace;

/// <summary>One editor tab: the resource it shows and its state. Placement (group, order) lives in the dock layout.</summary>
public sealed record TabInfo(string Id, string Kind, string Key, bool Preview = false, bool Pinned = false);

/// <summary>The tab rules of design/shell.md 9.1 and 9.2 without any UI: one tab per resource, preview replacement, pinning.</summary>
public sealed class EditorTabs
{
    private const int MaxHistory = 100;
    private readonly List<TabInfo> _tabs = [];
    private readonly List<(string Kind, string Key)> _closed = [];
    private readonly Dictionary<string, TabVisits> _visits = [];

    /// <summary>The resources one tab showed, in order, and which of them it shows now (Back and Forward are per tab, like in a browser).</summary>
    private sealed class TabVisits
    {
        public List<(string Kind, string Key)> Items { get; init; } = [];

        public int At { get; set; } = -1;
    }

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
        // A preview tab that replaces another goes on from its history.
        if (replaced is not null && _visits.Remove(replaced.Id, out var carried)) _visits[tab.Id] = carried;
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
        for (var i = 0; i < _closed.Count; i++)
            if (_closed[i].Key == oldKey) _closed[i] = (_closed[i].Kind, newKey);
        foreach (var visits in _visits.Values)
            for (var i = 0; i < visits.Items.Count; i++)
                if (visits.Items[i].Key == oldKey) visits.Items[i] = (visits.Items[i].Kind, newKey);
    }

    /// <summary>Closes a tab; its resource goes on the list Reopen Closed Tab takes from.</summary>
    public void Remove(string id)
    {
        if (Get(id) is { } tab)
        {
            _closed.Add((tab.Kind, tab.Key));
            if (_closed.Count > MaxHistory) _closed.RemoveAt(0);
        }

        _tabs.RemoveAll(t => t.Id == id);
        _visits.Remove(id);
    }

    /// <summary>The most recently closed resource that <paramref name="usable"/> accepts and that has no open tab; older ones stay for the next call.</summary>
    public (string Kind, string Key)? PopClosed(Func<string, string, bool> usable)
    {
        while (_closed.Count > 0)
        {
            var last = _closed[^1];
            _closed.RemoveAt(_closed.Count - 1);
            if (Find(last.Kind, last.Key) is null && usable(last.Kind, last.Key)) return last;
        }

        return null;
    }

    /// <summary>Notes that a tab showed a resource, for its Back and Forward; showing it again right away adds nothing, and a new visit drops the forward part.</summary>
    public void Visit(string id, string kind, string key)
    {
        if (!_visits.TryGetValue(id, out var v)) _visits[id] = v = new TabVisits();
        if (v.At >= 0 && v.Items[v.At] == (kind, key)) return;
        v.Items.RemoveRange(v.At + 1, v.Items.Count - v.At - 1);
        v.Items.Add((kind, key));
        if (v.Items.Count > MaxHistory) v.Items.RemoveAt(0);
        v.At = v.Items.Count - 1;
    }

    public bool CanGoBack(string id) => _visits.TryGetValue(id, out var v) && v.At > 0;

    public bool CanGoForward(string id) => _visits.TryGetValue(id, out var v) && v.At < v.Items.Count - 1;

    /// <summary>
    /// Moves a tab one step back (-1) or forward (1) to the nearest visit that <paramref name="usable"/> accepts and that no other tab shows, and makes the tab show it;
    /// resources that are gone are skipped. Null at the ends.
    /// </summary>
    public TabInfo? Step(string id, int direction, Func<string, string, bool> usable)
    {
        if (!_visits.TryGetValue(id, out var v) || Get(id) is not { } tab) return null;
        for (var i = v.At + direction; i >= 0 && i < v.Items.Count; i += direction)
        {
            var (kind, key) = v.Items[i];
            if (!usable(kind, key) || (Find(kind, key) is { } other && other.Id != id)) continue;
            v.At = i;
            return Update(tab with { Kind = kind, Key = key });
        }

        return null;
    }

    /// <summary>Puts a saved list back (workspace restore).</summary>
    public void Load(IEnumerable<TabInfo> tabs)
    {
        _tabs.Clear();
        _tabs.AddRange(tabs);
        _closed.Clear();
        _visits.Clear();
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
