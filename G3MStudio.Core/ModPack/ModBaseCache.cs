using System.Text.Json;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Core.ModPack;

/// <param name="Path">Full path of the data file the copy belongs to.</param>
/// <param name="Hash">SHA-256 of the original bytes, upper-case hex.</param>
/// <param name="Size">Size of the original in bytes.</param>
/// <param name="Saved">When the copy was made (UTC); the oldest copy goes first when the cache is full.</param>
public sealed record ModBaseEntry(string Path, string Hash, long Size, DateTime Saved);

/// <summary>
/// The original of each data file, kept in Studio's data folder as a gzip copy named by its SHA-256, so Export Mod Pack can compare against it without asking.
/// The index maps the full path of a data file to the copy; two paths with the same content share one copy.
/// </summary>
public sealed partial class ModBaseCache(UserData home)
{
    private readonly string _folder = home.Combine("modbase");
    // One lock per folder, shared by every instance: the window and the settings page each build their own cache over the same folder.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, Lock> Locks = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _lock = Locks.GetOrAdd(Path.GetFullPath(home.Combine("modbase")), _ => new Lock());

    private string IndexFile => Path.Combine(_folder, "index.json");

    private string BlobFile(string hash) => Path.Combine(_folder, hash + ".gz");

    // A missing index is an empty cache; an unreadable or damaged one is null, and nothing may be deleted on that basis.
    private Dictionary<string, ModBaseEntry>? ReadIndex()
    {
        try
        {
            return File.Exists(IndexFile) ? JsonSerializer.Deserialize<Dictionary<string, ModBaseEntry>>(File.ReadAllText(IndexFile)) ?? [] : [];
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    private void WriteIndex(Dictionary<string, ModBaseEntry> index) => UserData.WriteAllTextAtomic(IndexFile, JsonSerializer.Serialize(index));

    /// <summary>The remembered original of a data file, or null.</summary>
    public ModBaseEntry? Find(string dataFile)
    {
        lock (_lock) return ReadIndex()?.GetValueOrDefault(UserData.KeyFor(dataFile));
    }

    /// <summary>Bytes the copies take on disk.</summary>
    public long TotalBytes
    {
        get
        {
            lock (_lock) return Directory.Exists(_folder) ? Directory.EnumerateFiles(_folder, "*.gz").Sum(f => new FileInfo(f).Length) : 0;
        }
    }

    /// <summary>
    /// Keeps <paramref name="source"/> as the original of <paramref name="dataFile"/>. An existing entry stays unless <paramref name="replace"/> is set (the first open wins).
    /// Returns null when the copy would not fit under <paramref name="maxBytes"/> on its own. Older copies are dropped to make room.
    /// </summary>
    public ModBaseEntry? Remember(string dataFile, string source, long maxBytes, bool replace = false)
    {
        lock (_lock)
        {
            var index = ReadIndex() ?? throw new IOException("The mod base index cannot be read. Clear the cache in the settings.");
            var key = UserData.KeyFor(dataFile);
            if (!replace && index.TryGetValue(key, out var existing)) return existing;
            Directory.CreateDirectory(_folder);
            SweepParts(TimeSpan.FromMinutes(10));
            var (hash, size) = Compress(source, maxBytes, out var part);
            if (hash is null) return null;
            try
            {
                File.Move(part, BlobFile(hash), true);
            }
            catch (FileNotFoundException)
            {
                return null; // another Studio swept the part file meanwhile: no copy this time
            }

            var entry = new ModBaseEntry(Path.GetFullPath(dataFile), hash, size, DateTime.UtcNow);
            index[key] = entry;
            Evict(index, maxBytes, key);
            WriteIndex(index);
            return entry;
        }
    }
}
