using System.Text.Json;

namespace G3MStudio.Core.Workspace;

/// <summary>
/// One journal line. Op is "do" (Kind and Args set), "undo" or "redo". On disk a large argument is moved out of Args into Blobs
/// (argument name to the hash of its side file); a record read back always has its values in Args and no Blobs.
/// </summary>
public sealed record JournalRecord(string Op, string? Kind = null, Dictionary<string, string>? Args = null, Dictionary<string, string>? Blobs = null);

/// <summary>
/// Append-only edit journal in the user data folder (F-WS-4): a header line naming the data file state the edits start from,
/// then one line per command. Every line is handed to the operating system when written (a crash of the app loses nothing); the slow disk sync
/// is coalesced to once per <see cref="SyncDelay"/> of activity, and always done by <see cref="Flush"/>, <see cref="Reset"/> and disposing.
/// It never touches the user's file.
/// </summary>
public sealed class Journal : IDisposable
{
    /// <summary>Argument values longer than this (a picture, an audio file) go to a side file, so a line stays short and a large edit is stored once.</summary>
    internal const int BlobThreshold = 64 * 1024;

    /// <summary>How long edits wait for the disk sync after the first unsynced one.</summary>
    public static readonly TimeSpan SyncDelay = TimeSpan.FromMilliseconds(500);

    private readonly string _file;
    private readonly Lock _gate = new();
    private readonly Timer _timer;
    private FileStream _stream;
    private bool _dirty;

    private Journal(string file, DataFileIdentity identity)
    {
        _file = file;
        _stream = Create(file, identity, []);
        _timer = new Timer(_ => SyncQuietly());
    }

    public static string FileFor(UserData userData, string dataFile) => userData.Combine("journal", UserData.KeyFor(dataFile), "journal.jsonl");

    /// <summary>
    /// Opens the journal of a data file. A journal that starts from this exact file state is kept and its records returned;
    /// any other journal belongs to a different file state (saved, patched or replaced since) and is set aside as <c>.stale</c>.
    /// </summary>
    public static (Journal Journal, IReadOnlyList<JournalRecord> Recovered) Open(UserData userData, DataFileIdentity identity)
    {
        var file = FileFor(userData, identity.Path);
        try
        {
            var recovered = Read(file, identity);
            if (recovered is null && File.Exists(file)) File.Move(file, FreeStaleName(file), overwrite: true);
            var journal = new Journal(file, identity);
            // Rewriting also drops a half-written last line, which a later append would otherwise glue onto.
            if (recovered is not null) journal.Reset(identity, recovered);
            return (journal, recovered ?? []);
        }
        catch (IOException e) when (IsHeld(e))
        {
            // The journal is held for writing by another running G3MStudio that has this file open.
            throw new DataFileInUseException(identity.Path, e);
        }
    }

    /// <summary>
    /// A sharing violation: on Windows the error codes of a file open for writing, elsewhere the "would block" error of the lock that <see cref="Create"/> takes.
    /// Any other failure (a full disk, a read-only folder) is a problem of the data folder, not a second window.
    /// </summary>
    internal static bool IsHeld(IOException e) =>
        OperatingSystem.IsWindows() ? (e.HResult & 0xFFFF) is 32 or 33 : e.GetType() == typeof(IOException) && e.HResult == (OperatingSystem.IsMacOS() ? 35 : 11);

    /// <summary>The older journal of a file is kept: a second one is set aside under a numbered name instead of replacing the first. After nine, the first is replaced.</summary>
    private static string FreeStaleName(string file)
    {
        var name = file + ".stale";
        for (var n = 2; File.Exists(name) && n <= 9; n++) name = $"{file}.stale{n}";
        return File.Exists(name) ? file + ".stale" : name;
    }

    private static string BlobFolder(string file) => file + ".blobs";

    /// <summary>The record with its large argument values moved to side files named by their hash; the same value is stored once.</summary>
    private JournalRecord Externalize(JournalRecord record)
    {
        if (record.Args is null || !record.Args.Values.Any(v => v.Length > BlobThreshold)) return record;
        var args = new Dictionary<string, string>(record.Args);
        var blobs = new Dictionary<string, string>();
        foreach (var (key, value) in record.Args.Where(a => a.Value.Length > BlobThreshold))
        {
            var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value)));
            var target = Path.Combine(BlobFolder(_file), hash);
            if (!File.Exists(target))
            {
                Directory.CreateDirectory(BlobFolder(_file));
                File.WriteAllText(target + ".new", value);
                File.Move(target + ".new", target, overwrite: true);
            }

            args.Remove(key);
            blobs[key] = hash;
        }

        return record with { Args = args, Blobs = blobs };
    }

    /// <summary>A hash as written by <see cref="Externalize"/>: 64 upper case hex digits, so it can never name anything outside the blob folder.</summary>
    private static bool IsHash(string text) => text.Length == 64 && text.All(c => c is >= '0' and <= '9' or >= 'A' and <= 'F');

    /// <summary>The record with its side files read back into Args; false when one is missing or not a valid reference (a damaged journal).</summary>
    private static bool TryInternalize(string file, JournalRecord record, out JournalRecord result)
    {
        result = record;
        if (record.Blobs is null) return true;
        var args = new Dictionary<string, string>(record.Args ?? []);
        foreach (var (key, hash) in record.Blobs)
        {
            if (!IsHash(hash)) return false;
            var blob = Path.Combine(BlobFolder(file), hash);
            if (!File.Exists(blob)) return false;
            args[key] = File.ReadAllText(blob);
        }

        result = record with { Args = args, Blobs = null };
        return true;
    }

    public void Append(JournalRecord record)
    {
        var line = Externalize(record);
        lock (_gate)
        {
            WriteLine(_stream, line);
            _stream.Flush();
            if (_dirty) return;
            _dirty = true;
            _timer.Change(SyncDelay, Timeout.InfiniteTimeSpan);
        }
    }

    /// <summary>Syncs the lines written so far to the disk now.</summary>
    public void Flush()
    {
        Microsoft.Win32.SafeHandles.SafeFileHandle handle;
        lock (_gate)
        {
            if (!_dirty) return;
            _dirty = false;
            handle = _stream.SafeFileHandle;
        }

        // Outside the lock: a slow disk must not hold up the edits that append meanwhile.
        try { RandomAccess.FlushToDisk(handle); }
        catch (IOException)
        {
            lock (_gate) _dirty = true;
            throw;
        }
    }

    // A failed sync from the timer must not crash the app; the lines are already with the operating system.
    private void SyncQuietly()
    {
        try { Flush(); }
        catch (IOException)
        {
            try { _timer.Change(SyncDelay, Timeout.InfiniteTimeSpan); } // the sync failed: try again later
            catch (ObjectDisposedException) { return; }
        }
        catch (Exception e) when (e is UnauthorizedAccessException or ObjectDisposedException) { }
    }

    /// <summary>Starts over from a new data file state (after a save) or from the records that could be replayed.</summary>
    public void Reset(DataFileIdentity identity, IEnumerable<JournalRecord> records)
    {
        // The open stream blocks the replace, so it is closed first; when anything below fails the old file is opened again and later edits keep being journaled.
        lock (_gate) { _stream.Dispose(); _dirty = false; }
        var temp = _file + ".new";
        try
        {
            var kept = records.Select(Externalize).ToList();
            using (var stream = Create(temp, identity, kept, FileMode.Create)) stream.Flush(true);
            File.Move(temp, _file, overwrite: true);
            DropUnusedBlobs(kept);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            try { File.Delete(temp); }
            catch (Exception d) when (d is IOException or UnauthorizedAccessException) { }
            _stream = Create(_file, identity, []);
            throw;
        }

        _stream = Create(_file, identity, []);
    }

    private void DropUnusedBlobs(List<JournalRecord> kept)
    {
        if (!Directory.Exists(BlobFolder(_file))) return;
        var used = kept.SelectMany(r => r.Blobs?.Values ?? Enumerable.Empty<string>()).ToHashSet();
        foreach (var blob in Directory.EnumerateFiles(BlobFolder(_file)))
            if (!used.Contains(Path.GetFileName(blob))) FileCleanup.TryDeleteFile(blob);
    }

    public void Dispose()
    {
        _timer.Dispose();
        lock (_gate)
        {
            try { if (_dirty) _stream.Flush(true); }
            catch (IOException) { _dirty = false; } // the lines are with the operating system already; closing must not throw
            finally { _stream.Dispose(); }
        }
    }

    private static FileStream Create(string file, DataFileIdentity identity, IEnumerable<JournalRecord> records, FileMode mode = FileMode.OpenOrCreate)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(file) ?? ".");
        // Other systems have no share modes: None takes an exclusive lock on the file, which is what a second window runs into.
        var stream = new FileStream(file, mode, FileAccess.Write, OperatingSystem.IsWindows() ? FileShare.Read : FileShare.None);
        if (stream.Length == 0)
        {
            WriteLine(stream, identity);
            foreach (var record in records) WriteLine(stream, record);
            stream.Flush(true);
        }

        stream.Seek(0, SeekOrigin.End);
        return stream;
    }

    private static void WriteLine(Stream stream, object value)
    {
        var line = JsonSerializer.SerializeToUtf8Bytes(value);
        stream.Write(line);
        stream.WriteByte((byte)'\n');
    }

    /// <summary>Records of a journal that starts from <paramref name="identity"/>; null when there is none or it starts elsewhere. A torn last line is dropped.</summary>
    private static List<JournalRecord>? Read(string file, DataFileIdentity identity)
    {
        if (!File.Exists(file)) return null;
        using var reader = new StreamReader(new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
        DataFileIdentity? header;
        try { header = JsonSerializer.Deserialize<DataFileIdentity>(reader.ReadLine() ?? ""); }
        catch (JsonException) { return null; }
        if (header is null || header.Size != identity.Size || (header.Md5.Length > 0 && identity.Md5.Length > 0 && header.Md5 != identity.Md5)) return null;

        var records = new List<JournalRecord>();
        while (reader.ReadLine() is { } line)
        {
            try
            {
                // A record whose side file is gone is as good as a torn line: replay stops before it.
                if (!TryInternalize(file, JsonSerializer.Deserialize<JournalRecord>(line) ?? throw new JsonException(), out var record)) break;
                records.Add(record);
            }
            catch (JsonException) { break; }
        }

        return records;
    }
}

/// <summary>The data file is open in another running G3MStudio, which holds its journal.</summary>
public sealed class DataFileInUseException(string path, Exception inner) : IOException("The data file is already open in another G3MStudio window.", inner)
{
    public string Path { get; } = path;
}
