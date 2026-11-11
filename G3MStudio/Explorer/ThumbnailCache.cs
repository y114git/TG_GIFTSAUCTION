using Avalonia.Media;

namespace G3MStudio.Explorer;

/// <summary>A cached picture (null: none), the edit version it was made at and the fingerprint of its resource.</summary>
public readonly record struct ThumbnailEntry(int Version, int Signature, IImage? Picture);

/// <summary>
/// A bounded least-recently-used cache of finished thumbnails by tab key. An entry carries the edit version it was made at and the fingerprint of its
/// resource: a lookup at another version is a miss that the service settles by comparing fingerprints, so an edit refreshes only what it changed.
/// A null picture is kept too (the resource has none), so it is not looked up again.
/// </summary>
public sealed class ThumbnailCache(int capacity)
{
    private readonly Dictionary<string, LinkedListNode<(string Key, ThumbnailEntry ThumbnailEntry)>> _map = [];
    private readonly LinkedList<(string Key, ThumbnailEntry ThumbnailEntry)> _order = new();

    public int Count => _map.Count;

    public int Capacity { get; } = capacity;

    /// <summary>The entry for the key whatever its version, and it counts as used.</summary>
    public bool TryGet(string key, out ThumbnailEntry entry)
    {
        if (_map.TryGetValue(key, out var node))
        {
            _order.Remove(node);
            _order.AddFirst(node);
            entry = node.Value.ThumbnailEntry;
            return true;
        }

        entry = default;
        return false;
    }

    public void Put(string key, ThumbnailEntry entry)
    {
        if (_map.TryGetValue(key, out var node)) _order.Remove(node);
        _map[key] = _order.AddFirst((key, entry));
        while (_map.Count > Capacity && _order.Last is { } oldest)
        {
            _map.Remove(oldest.Value.Key);
            _order.RemoveLast();
        }
    }

    public void Clear()
    {
        _map.Clear();
        _order.Clear();
    }
}
