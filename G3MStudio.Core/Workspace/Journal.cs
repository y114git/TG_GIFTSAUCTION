using System.Text.Json;

namespace G3MStudio.Core.Workspace;

/// <summary>
/// One journal line. Op is "do" (Kind and Args set), "undo" or "redo". On disk a large argument is moved out of Args into Blobs
/// (argument name to the hash of its side file); a record read back always has its values in Args and no Blobs.
/// </summary>
public sealed record JournalRecord(string Op, string? Kind = null, Dictionary<string, string>? Args = null, Dictionary<string, string>? Blobs = null);

/// <summary>
/// Append-only edit journal in the user data folder (F-WS-4): a header line naming the data file state the edits start from,
/// then one line per command. Every line is flushed to disk when written, which is stricter than the 1 second the design allows.
/// It never touches the user's file.
/// </summary>
public sealed class Journal : IDisposable
{
    /// <summary>Argument values longer than this (a picture, an audio file) go to a side file, so a line stays short and a large edit is stored once.</summary>
    internal const int BlobThreshold = 64 * 1024;

    private readonly string _file;
    private FileStream _stream;

    private Journal(string file, DataFileIdentity identity)
    {
        _file = file;
        _stream = Create(file, identity, []);
    }

    public static string FileFor(UserData userData, string dataFile) => userData.Combine("journal", UserData.KeyFor(dataFile), "journal.jsonl");

    /// <summary>
    /// Opens the journal of a data file. A journal that starts from this exact file state is kept and its records returned;
    /// any other journal belongs to a different file state (saved, patched or replaced since) and is set aside as <c>.stale</c>.
    /// </summary>
    public static (Journal Journal, IReadOnlyList<JournalRecord> Recovered) Open(UserData userData, DataFileIdentity identity)
    {
        var file = FileFor(userData, identity.Path);
        var recovered = Read(file, identity);
        if (recovered is null && File.Exists(file)) File.Move(file, file + ".stale", overwrite: true);
        var journal = new Journal(file, identity);
        // Rewriting also drops a half-written last line, which a later append would otherwise glue onto.
        if (recovered is not null) journal.Reset(identity, recovered);
        return (journal, recovered ?? []);
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
        WriteLine(_stream, Externalize(record));
        _stream.Flush(true);
    }

    /// <summary>Starts over from a new data file state (after a save) or from the records that could be replayed.</summary>
    public void Reset(DataFileIdentity identity, IEnumerable<JournalRecord> records)
    {
        _stream.Dispose();
        var temp = _file + ".new";
        var kept = records.Select(Externalize).ToList();
        using (var stream = Create(temp, identity, kept, FileMode.Create)) stream.Flush(true);
        File.Move(temp, _file, overwrite: true);
        DropUnusedBlobs(kept);
        _stream = Create(_file, identity, []);
    }

    private void DropUnusedBlobs(List<JournalRecord> kept)
    {
        if (!Directory.Exists(BlobFolder(_file))) return;
        var used = kept.SelectMany(r => r.Blobs?.Values ?? Enumerable.Empty<string>()).ToHashSet();
        foreach (var blob in Directory.EnumerateFiles(BlobFolder(_file)))
            if (!used.Contains(Path.GetFileName(blob))) File.Delete(blob);
    }

    public void Dispose() => _stream.Dispose();

    private static FileStream Create(string file, DataFileIdentity identity, IEnumerable<JournalRecord> records, FileMode mode = FileMode.OpenOrCreate)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(file) ?? ".");
        var stream = new FileStream(file, mode, FileAccess.Write, FileShare.Read);
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
        if (header is null || header.Size != identity.Size || header.Md5 != identity.Md5) return null;

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
