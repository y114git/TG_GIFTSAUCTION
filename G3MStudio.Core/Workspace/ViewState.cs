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
        Rewrite(_tabs, t => t.Key == oldKey ? t with { Key = newKey } : t);
        Rewrite(_closed, c => c.Key == oldKey ? (c.Kind, newKey) : c);
        foreach (var visits in _visits.Values) Rewrite(visits.Items, v => v.Key == oldKey ? (v.Kind, newKey) : v);
    }

    private static void Rewrite<T>(List<T> list, Func<T, T> map)
    {
        var mapped = list.ConvertAll(item => map(item));
        list.Clear();
        list.AddRange(mapped);
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
/// <summary>Rooms maps a room tab's resource key to its zoom, left and top (the view of the canvas). Layers holds the layer stacks of sprite frames (see <see cref="LayerStore"/>).</summary>
public sealed record ViewState(string Dock, List<TabInfo> Tabs, string? Selected = null, Dictionary<string, double[]>? Rooms = null, Dictionary<string, string>? Layers = null, QuickAccess? Quick = null);

/// <summary>Per data file view state in the user data folder, keyed like the journal (OI-073).</summary>
public sealed class ViewStateStore(UserData userData)
{
    private const long MaxBytes = 512L * 1024 * 1024;

    private string FileFor(string dataFile) => userData.Combine("view", UserData.KeyFor(dataFile) + ".json");

    public void Save(string dataFile, ViewState state) => UserData.WriteAllTextAtomic(FileFor(dataFile), JsonSerializer.Serialize(state));

    private string LayersFileFor(string dataFile) => userData.Combine("view", UserData.KeyFor(dataFile) + ".layers.json");

    /// <summary>Writes the layer stacks of the data file beside its view state, atomically (temp file, then move), so a crash leaves the old or the new file.</summary>
    public void SaveLayers(string dataFile, IReadOnlyDictionary<string, string> layers) => UserData.WriteAllTextAtomic(LayersFileFor(dataFile), JsonSerializer.Serialize(layers));

    /// <summary>Raised with the path of the kept copy when <see cref="LoadLayers"/> found a damaged or oversize sidecar and set it aside.</summary>
    public event Action<string>? LayersDamaged;

    /// <summary>
    /// The layer stacks saved by <see cref="SaveLayers"/>, or null when there are none or the file cannot be used. A damaged or oversize file is moved to
    /// <c>.bak</c> (an older one is replaced) before the next autosave can overwrite it, and <see cref="LayersDamaged"/> is raised.
    /// </summary>
    public Dictionary<string, string>? LoadLayers(string dataFile)
    {
        var path = LayersFileFor(dataFile);
        try
        {
            if (!File.Exists(path)) return null;
            if (new FileInfo(path).Length > MaxBytes) return SetAside(path);
            return JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path))?.Where(p => p.Value is not null).ToDictionary();
        }
        catch (JsonException)
        {
            return SetAside(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private Dictionary<string, string>? SetAside(string path)
    {
        try
        {
            var kept = path + ".bak";
            File.Move(path, kept, true);
            LayersDamaged?.Invoke(kept);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // still in use: it stays where it is
        }

        return null;
    }

    private static ViewState Sanitized(ViewState state) => state with
    {
        Tabs = [.. state.Tabs.Where(t => t is not null)],
        Rooms = state.Rooms?.Where(p => p.Value is { Length: >= 3 } && p.Value.All(double.IsFinite)).ToDictionary(),
        Layers = state.Layers?.Where(p => p.Value is not null).ToDictionary(),
        Quick = state.Quick is { Recent: not null, Favourites: not null, Searches: not null, Tags: not null, Filter: not null } q && q.Tags.Values.All(v => v is not null) ? q : null,
    };

    /// <summary>The saved state, or null when there is none or the file is damaged (only a lost convenience).</summary>
    public ViewState? Load(string dataFile)
    {
        try
        {
            var path = FileFor(dataFile);
            if (new FileInfo(path).Length > MaxBytes) return null;
            // A hand-edited or foreign file may hold nulls where the type promises values.
            return JsonSerializer.Deserialize<ViewState>(File.ReadAllText(path)) is { Dock: not null, Tabs: not null } state ? Sanitized(state) : null;
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
