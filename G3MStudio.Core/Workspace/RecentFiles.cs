using System.Text.Json;

namespace G3MStudio.Core.Workspace;

/// <summary><paramref name="Size"/> and <paramref name="Md5"/> are those of the file at its last save or open; they identify it again when it is lost, moved or renamed (0 and null in older lists).</summary>
public sealed record RecentEntry(string Path, string Name, string Version, DateTimeOffset LastOpened, bool Pinned, long Size = 0, string? Md5 = null);

/// <summary>Recent and pinned data files, stored as <c>recent.json</c> in the user data folder (design/shell.md, start page).</summary>
public sealed class RecentFiles
{
    public const int MaxUnpinned = 50;

    private static readonly StringComparer PathComparer = PathRules.Comparer;

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
            list._items = Read(list._file) ?? [];
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // A list that cannot be read right now (a virus scan, another instance) is left alone: nothing is written over it until it can be read.
            list._unread = true;
        }
        catch (JsonException)
        {
            // A damaged list is kept aside for the user, and the list starts empty.
            try { File.Move(list._file, list._file + ".invalid", overwrite: true); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }

        return list;
    }

    private bool _unread;

    private static List<RecentEntry>? Read(string file) => File.Exists(file) ? JsonSerializer.Deserialize<List<RecentEntry>>(File.ReadAllText(file)) : null;

    public RecentEntry? Find(string path) => _items.FirstOrDefault(e => PathComparer.Equals(e.Path, Path.GetFullPath(path)));

    public void Add(string path, string name, string version, DateTimeOffset now, DataFileIdentity? identity = null)
    {
        var full = Path.GetFullPath(path);
        var pinned = _items.Any(e => PathComparer.Equals(e.Path, full) && e.Pinned);
        _items.RemoveAll(e => PathComparer.Equals(e.Path, full));
        _items.Add(new RecentEntry(full, name, version, now, pinned, identity?.Size ?? 0, identity?.Md5));
        var extra = _items.Where(e => !e.Pinned).OrderByDescending(e => e.LastOpened).Skip(MaxUnpinned).ToList();
        _items.RemoveAll(extra.Contains);
        Commit();
    }

    /// <summary>A save changed the file: its entry keeps its place and takes the new size and hash. A move (the identity's path is new) is not an entry here; the caller adds it.</summary>
    public void Refresh(DataFileIdentity identity)
    {
        var index = _items.FindIndex(e => PathComparer.Equals(e.Path, identity.Path));
        if (index < 0) return;
        _items[index] = _items[index] with { Size = identity.Size, Md5 = identity.Md5 };
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
        if (_unread)
        {
            // The entries of the file that could not be read come back now, so pinned ones are not lost; while it stays unreadable only the memory changes.
            try
            {
                var stored = Read(_file) ?? [];
                _items.AddRange(stored.Where(old => !_items.Any(e => PathComparer.Equals(e.Path, old.Path))));
                _unread = false;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
            {
                Changed?.Invoke();
                return;
            }
        }

        UserData.WriteAllTextAtomic(_file, JsonSerializer.Serialize(_items));
        Changed?.Invoke();
    }
}
