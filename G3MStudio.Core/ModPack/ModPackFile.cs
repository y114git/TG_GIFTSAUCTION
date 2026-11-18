using System.IO.Compression;
using System.Text;
using System.Text.Json;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Core.ModPack;

/// <summary>What a pack says about itself. <see cref="TargetHash"/> is the SHA-256 of the data file the changes were made against.</summary>
public sealed record PackManifest(string Format, int FormatVersion, string Name, string Version, string Game, string TargetHash, CopyVersion GameVersion, IReadOnlyList<string> Authors, DateTimeOffset Created);

/// <summary>
/// One change. Op is add, replace, remove or code. The hashes are <see cref="Fingerprinter"/> hashes: BaseHash of the resource before the change
/// (null for add), NewHash after it (null for remove). The path fields name the pack members that hold the payload and the code texts.
/// </summary>
public sealed record PackChange(string Type, string Name, string Op, string? BaseHash, string? NewHash, PackMembers? Members = null)
{
    public string Key => ResourceTypes.KeyOf(Type, Name);
}

/// <summary>The names of the pack members that hold a change: the payload zip and the two code texts.</summary>
public sealed record PackMembers(string? Payload, string? NewCode, string? BaseCode);

/// <summary>A change with its content. Payload (add, replace) is the resource in the library's export layout, zipped, with what it owns; NewCode and BaseCode (code) are the GML after and before, the base of a three-way merge.</summary>
public sealed record PackEntry(PackChange Change, byte[]? Payload = null, string? NewCode = null, string? BaseCode = null);

public sealed record ModPackContent(PackManifest Manifest, IReadOnlyList<PackEntry> Entries);

/// <summary>
/// The pack file (.g3mpack, a ZIP): <c>manifest.json</c>, <c>changes.json</c> (a list of <see cref="PackChange"/>), <c>payload/NNNN.zip</c> per added or replaced resource
/// and <c>code/NNNN.new.gml</c> with <c>code/NNNN.base.gml</c> per code change. Members are read by name in memory, never extracted to disk.
/// </summary>
public static class ModPackFile
{
    public const string Extension = ".g3mpack", FormatName = "g3mstudio-pack";
    public const int FormatVersion = 1;
    private const long MaxMemberBytes = 512L * 1024 * 1024, MaxTotalBytes = 2L * 1024 * 1024 * 1024;
    private const int MaxMembers = 100_000;

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static PackManifest NewManifest(string name, string version, string game, string targetHash, CopyVersion gameVersion, IReadOnlyList<string> authors) =>
        new(FormatName, FormatVersion, name, version, game, targetHash, gameVersion, authors, DateTimeOffset.UtcNow);

    /// <summary>Writes under a temporary name and moves into place, so a failure leaves no half file.</summary>
    public static void Write(string path, ModPackContent pack)
    {
        var temp = path + ".tmp";
        try
        {
            using (var zip = ZipFile.Open(temp, ZipArchiveMode.Create))
            {
                var changes = new List<PackChange>();
                foreach (var (entry, n) in pack.Entries.Select((e, n) => (e, n)))
                {
                    var stem = $"{n + 1:D4}";
                    var members = new PackMembers(Add(zip, $"payload/{stem}.zip", entry.Payload), Add(zip, $"code/{stem}.new.gml", Text(entry.NewCode)), Add(zip, $"code/{stem}.base.gml", Text(entry.BaseCode)));
                    changes.Add(entry.Change with { Members = members });
                }

                Add(zip, "manifest.json", JsonSerializer.SerializeToUtf8Bytes(pack.Manifest, Json));
                Add(zip, "changes.json", JsonSerializer.SerializeToUtf8Bytes(changes, Json));
            }

            File.Move(temp, path, true);
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }

    private static byte[]? Text(string? text) => text is null ? null : Encoding.UTF8.GetBytes(text);

    // Writes the member when there are bytes; returns its name, or null when the change has no such part.
    private static string? Add(ZipArchive zip, string name, byte[]? bytes)
    {
        if (bytes is null) return null;
        using var stream = zip.CreateEntry(name, CompressionLevel.Optimal).Open();
        stream.Write(bytes);
        return name;
    }

    public static ModPackContent Read(string path)
    {
        using var zip = ZipFile.OpenRead(path);
        if (zip.Entries.Count > MaxMembers) throw new InvalidDataException($"The pack has more than {MaxMembers} members.");
        var budget = MaxTotalBytes;
        var manifest = Parse<PackManifest>(zip, "manifest.json", ref budget);
        if (manifest.Format != FormatName) throw new InvalidDataException("The file is not a G3MStudio mod pack.");
        if (manifest.FormatVersion > FormatVersion) throw new InvalidDataException($"The pack uses format {manifest.FormatVersion}; this version reads up to {FormatVersion}.");
        if (manifest.Name is null || manifest.Version is null || manifest.Game is null || manifest.TargetHash is null || manifest.GameVersion is null || manifest.Authors is null)
            throw new InvalidDataException("The pack manifest is incomplete.");
        var changes = Parse<List<PackChange>>(zip, "changes.json", ref budget);
        if (changes.Count > MaxMembers) throw new InvalidDataException($"The pack lists more than {MaxMembers} changes.");
        var entries = new List<PackEntry>(changes.Count);
        foreach (var c in changes)
        {
            if (c is null || string.IsNullOrEmpty(c.Type) || string.IsNullOrEmpty(c.Name) || c.Op is not ("add" or "replace" or "remove" or "code"))
                throw new InvalidDataException("The pack has a change without a type, name or known operation.");
            entries.Add(new PackEntry(c,
                c.Members?.Payload is { } payload ? Member(zip, payload, ref budget) : null,
                c.Members?.NewCode is { } created ? Encoding.UTF8.GetString(Member(zip, created, ref budget)) : null,
                c.Members?.BaseCode is { } before ? Encoding.UTF8.GetString(Member(zip, before, ref budget)) : null));
        }

        return new ModPackContent(manifest, entries);
    }

    private static T Parse<T>(ZipArchive zip, string name, ref long budget) => JsonSerializer.Deserialize<T>(Member(zip, name, ref budget), Json) ?? throw new InvalidDataException($"{name} is empty.");

    // Counts the bytes that really come out of the member, not the size its header claims, against the budget of the whole pack.
    private static byte[] Member(ZipArchive zip, string name, ref long budget)
    {
        var entry = zip.GetEntry(name) ?? throw new InvalidDataException($"The pack has no {name}.");
        if (entry.Length > MaxMemberBytes || entry.Length > budget) throw new InvalidDataException($"{name} is too large.");
        using var stream = entry.Open();
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = stream.Read(chunk, 0, chunk.Length)) > 0)
        {
            budget -= read;
            if (budget < 0 || buffer.Length + read > MaxMemberBytes) throw new InvalidDataException($"{name} is too large.");
            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }
}
