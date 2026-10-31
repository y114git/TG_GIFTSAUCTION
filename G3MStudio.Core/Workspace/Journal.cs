using System.Text.Json;

namespace G3MStudio.Core.Workspace;

/// <summary>One journal line. Op is "do" (Kind and Args set), "undo" or "redo".</summary>
public sealed record JournalRecord(string Op, string? Kind = null, Dictionary<string, string>? Args = null);

/// <summary>
/// Append-only edit journal in the user data folder (F-WS-4): a header line naming the data file state the edits start from,
/// then one line per command. Every line is flushed to disk when written, which is stricter than the 1 second the design allows.
/// It never touches the user's file.
/// </summary>
public sealed class Journal : IDisposable
{
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

    public void Append(JournalRecord record)
    {
        WriteLine(_stream, record);
        _stream.Flush(true);
    }

    /// <summary>Starts over from a new data file state (after a save) or from the records that could be replayed.</summary>
    public void Reset(DataFileIdentity identity, IEnumerable<JournalRecord> records)
    {
        _stream.Dispose();
        var temp = _file + ".new";
        using (var stream = Create(temp, identity, records, FileMode.Create)) stream.Flush(true);
        File.Move(temp, _file, overwrite: true);
        _stream = Create(_file, identity, []);
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
            try { records.Add(JsonSerializer.Deserialize<JournalRecord>(line) ?? throw new JsonException()); }
            catch (JsonException) { break; }
        }

        return records;
    }
}
