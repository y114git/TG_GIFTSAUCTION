using System.Text.Json;

namespace G3MStudio.Core.Workspace;

public sealed record RecentEntry(string Path, string Name, string Version, DateTimeOffset LastOpened, bool Pinned);

/// <summary>Recent and pinned data files, stored as <c>recent.json</c> in the user data folder (design/shell.md, start page).</summary>
public sealed class RecentFiles
{
    public const int MaxUnpinned = 50;

    private static readonly StringComparer PathComparer = OperatingSystem.IsLinux() ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;

    private readonly string _file;
    private List<RecentEntry> _items = [];

    private RecentFiles(string file) => _file = file;

    public event Action? Changed;

    /// <summary>Pinned first, then the most recently opened.</summary>
    public IReadOnlyList<RecentEntry> Entries => [.. _items.OrderByDescending(e => e.Pinned).ThenByDescending(e => e.LastOpened)];

    public static RecentFiles Load(UserData userData)
    {
        var list = new RecentFiles(userData.Combine("recent.json"));
        try
        {
            list._items = JsonSerializer.Deserialize<List<RecentEntry>>(File.ReadAllText(list._file)) ?? [];
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            // A missing or damaged list is only a lost convenience; start empty.
        }

        return list;
    }

    public void Add(string path, string name, string version, DateTimeOffset now)
    {
        var full = Path.GetFullPath(path);
        var pinned = _items.Any(e => PathComparer.Equals(e.Path, full) && e.Pinned);
        _items.RemoveAll(e => PathComparer.Equals(e.Path, full));
        _items.Add(new RecentEntry(full, name, version, now, pinned));
        var extra = _items.Where(e => !e.Pinned).OrderByDescending(e => e.LastOpened).Skip(MaxUnpinned).ToList();
        _items.RemoveAll(extra.Contains);
        Commit();
    }

    public void SetPinned(string path, bool pinned)
    {
        var full = Path.GetFullPath(path);
        var index = _items.FindIndex(e => PathComparer.Equals(e.Path, full));
        if (index < 0) return;
        _items[index] = _items[index] with { Pinned = pinned };
        Commit();
    }

    public void Remove(string path)
    {
        var full = Path.GetFullPath(path);
        _items.RemoveAll(e => PathComparer.Equals(e.Path, full));
        Commit();
    }

    /// <summary>Clear Recently Opened: pinned entries stay.</summary>
    public void Clear()
    {
        _items.RemoveAll(e => !e.Pinned);
        Commit();
    }

    private void Commit()
    {
        UserData.WriteAllTextAtomic(_file, JsonSerializer.Serialize(_items));
        Changed?.Invoke();
    }
}
