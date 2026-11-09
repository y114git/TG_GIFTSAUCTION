using System.IO.Compression;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using G3MLib.DataFile;
using G3MLib.DataFile.Compiler;
using G3MLib.Modding.Resources;

namespace G3MStudio.Core.Workspace;

/// <summary>
/// The copied items as the library's export layout in a ZIP: written from the source, made safe for the target (page and audio positions and
/// instance ids of the source mean nothing there, names are changed), and read by the library's import into the target.
/// </summary>
public static partial class CopyPackage
{
    /// <summary>A package larger than this is refused, because it travels in the undo record and the journal as text.</summary>
    public const long MaxBytes = 256L * 1024 * 1024;

    /// <summary>
    /// True when the name can be a file or folder name on any system and cannot leave its folder: no separators, drive or wildcard characters,
    /// not a dot name, not rooted. Names come from the data files, so they are checked before they reach the disk.
    /// </summary>
    public static bool IsSafeName(string name) =>
        name.Length is > 0 and <= 200 && name is not ("." or "..") && name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0
        && name.IndexOfAny(['\\', '/', ':']) < 0 && !Path.IsPathRooted(name) && name.Trim() == name;

    /// <summary>The path of a file inside <paramref name="directory"/>; throws when a part is not a safe name or the result is not inside.</summary>
    public static string PathInside(string directory, params string[] parts)
    {
        foreach (var part in parts)
            if (!IsSafeName(part)) throw new InvalidOperationException($"'{part}' cannot be used as a file name.");
        var root = Path.GetFullPath(directory);
        var full = Path.GetFullPath(Path.Combine([root, .. parts]));
        if (!full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal)) throw new InvalidOperationException($"'{full}' is outside the copy folder.");
        return full;
    }

    public static Dictionary<string, HashSet<string>> Selection(IEnumerable<(string Type, string Name)> items, IEnumerable<string> codeNames)
    {
        var selection = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        void Add(string folder, string name)
        {
            if (!selection.TryGetValue(folder, out var set)) selection[folder] = set = new HashSet<string>(StringComparer.Ordinal);
            set.Add(name);
        }

        foreach (var (type, name) in items)
        {
            if (type == "code") Add("CodeEntries", name);
            else foreach (var folder in ResourceTransfer.Folders[type]) Add(folder, name);
        }

        foreach (var code in codeNames) Add("CodeEntries", code);
        return selection;
    }

    /// <summary>The source's items of the selection written to a folder, in the library's layout. Throws when an item fails to export.</summary>
    public static void Export(GameMakerData data, string dataPath, string directory, Dictionary<string, HashSet<string>> selection, IProgress<BatchProgress>? progress, CancellationToken cancellationToken)
    {
        var done = 0;
        foreach (var name in selection.Values.SelectMany(n => n).Where(n => !IsSafeName(n)))
            throw new InvalidOperationException($"'{name}' cannot be used as a file name, so it cannot be copied.");
        foreach (var (folder, names) in selection)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var step = new Progress<BatchProgress>(p => progress?.Report(new BatchProgress(done, selection.Count, $"{folder}: {p.Item}")));
            var failed = ResourceTransfer.ExportFolder(new ExportSource(data, dataPath), directory, folder, names, step, cancellationToken).FirstOrDefault(r => r.Kind == ResultKind.Failed);
            if (failed is not null) throw new InvalidOperationException($"{failed.Name} could not be read: {failed.Message}");
            done++;
        }

        if (selection.TryGetValue("Sounds", out var sounds)) WriteSoundAudio(data, directory, sounds);
    }

    // The library writes the audio of a sound only when it lives in an audio group file; the audio in the data file is written here.
    private static void WriteSoundAudio(GameMakerData data, string directory, HashSet<string> names)
    {
        foreach (var sound in data.Sounds)
        {
            if (sound.Name?.Content is not { } name || !names.Contains(name) || sound.AudioFile?.Data is not { Length: > 0 } audio) continue;
            var extension = sound.Type?.Content is { Length: > 0 } type ? (type.StartsWith('.') ? type : "." + type) : ".wav";
            if (!Extension().IsMatch(extension)) throw new InvalidOperationException($"{name} has the audio type '{sound.Type?.Content}', which is not a file extension.");
            var file = PathInside(directory, "Sounds", name, name + extension);
            Directory.CreateDirectory(Path.GetDirectoryName(file) ?? directory);
            File.WriteAllBytes(file, audio);
        }
    }

    // Objects, scripts, timelines and rooms refer to code by name, and code refers to assets by name; so assets go in first, then the code, then
    // these four again, which links the code they own.
    private static readonly string[] Linked = ["Scripts", "GameObjects", "Timelines", "Rooms"];

    /// <summary>
    /// Reads the selection from the folder into the data file. The library's import does the assets; code is compiled from the GML text, because the
    /// library does not import code entries. Cancelling throws; the caller puts the model back.
    /// </summary>
    public static IReadOnlyList<ItemResult> Import(GameMakerData data, string directory, Dictionary<string, HashSet<string>> selection, IProgress<BatchProgress>? progress, CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<string, IReadOnlySet<string>> Present(Func<string, bool> keep) => selection
            .Where(p => keep(p.Key) && Directory.Exists(Path.Combine(directory, p.Key))).ToDictionary(p => p.Key, p => (IReadOnlySet<string>)p.Value);
        var results = new List<ItemResult>(ResourceImportService.Import(data, directory, Present(f => f != "CodeEntries"), progress, cancellationToken));
        cancellationToken.ThrowIfCancellationRequested();
        if (!CompileCode(data, directory, selection)) return results;
        results.AddRange(ResourceImportService.Import(data, directory, Present(Linked.Contains), progress, cancellationToken));
        return results;
    }

    private static bool CompileCode(GameMakerData data, string directory, Dictionary<string, HashSet<string>> selection)
    {
        if (!selection.TryGetValue("CodeEntries", out var names)) return false;
        var group = new CodeImportGroup(data);
        var queued = false;
        foreach (var name in names)
        {
            var file = Path.Combine(directory, "CodeEntries", name, name + ".gml");
            if (!File.Exists(file)) continue;
            group.QueueReplace(name, File.ReadAllText(file));
            queued = true;
        }

        if (!queued) return false;
        var result = group.Import(throwOnFailedCompile: false);
        if (!result.Successful) throw new InvalidOperationException("The code could not be compiled in the target: " + result.PrintAllErrors(true));
        return true;
    }

    /// <summary>Removes a temporary folder; a file another program still holds (a scanner) must not hide the real result or error of the copy, and the system cleans the temp folder later.</summary>
    public static void TryDelete(string folder)
    {
        try
        {
            Directory.Delete(folder, true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // left behind in the temp folder
        }
    }

    /// <summary>The folder's files as a ZIP.</summary>
    public static byte[] Zip(string directory, bool allowLarge = false)
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
                zip.CreateEntryFromFile(file, Path.GetRelativePath(directory, file).Replace('\\', '/'), CompressionLevel.Fastest);
        }

        if (stream.Length > MaxBytes && !allowLarge) throw new RiskyRefusalException("risky.copySizeTitle", "risky.copySize", $"The copy is {stream.Length / (1024 * 1024)} MB; copy fewer items at a time (the limit is {MaxBytes / (1024 * 1024)} MB).");
        return stream.ToArray();
    }

    private const int MaxUnpackedFiles = 100_000;
    private const long MaxUnpackedBytes = 4L * 1024 * 1024 * 1024;

    public static void Unzip(byte[] package, string directory)
    {
        using var stream = new MemoryStream(package);
        using var zip = new ZipArchive(stream);
        // ExtractToDirectory refuses names that leave the folder; the caps stop a pack that unpacks to far more than it weighs.
        if (zip.Entries.Count > MaxUnpackedFiles || zip.Entries.Sum(e => e.Length) > MaxUnpackedBytes) throw new InvalidDataException("The package holds too many files or unpacks to too much data.");
        zip.ExtractToDirectory(directory, true);
    }

    /// <summary>
    /// Makes the exported folder fit a target. Page positions, audio slots and group numbers of the source are dropped so the import adds the pictures and
    /// audio as new data; instance ids of rooms move above <paramref name="firstFreeId"/>; names change by <paramref name="renames"/>.
    /// Returns the highest instance id used.
    /// </summary>
    public static uint Prepare(string directory, IReadOnlyDictionary<string, string> renames, uint firstFreeId)
    {
        foreach (var (old, renamed) in renames)
            if (!IsSafeName(old) || !IsSafeName(renamed)) throw new InvalidOperationException($"'{renamed}' cannot be used as a file name.");
        var ids = new Dictionary<long, long>();
        var next = (long)firstFreeId;
        var rename = renames.Count == 0 ? null : Pattern(renames.Keys);
        foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories).ToList())
        {
            var kind = Path.GetExtension(file).ToLowerInvariant();
            if (kind is not (".json" or ".gml" or ".asm")) continue;
            var text = File.ReadAllText(file);
            if (kind == ".json") text = SanitizeJson(file, text, ids, ref next);
            if (rename is not null) text = RenameText(text, kind == ".json", rename, renames);
            File.WriteAllText(file, text);
        }

        if (renames.Count > 0) RenamePaths(directory, renames);
        return (uint)Math.Max(firstFreeId, next - 1);
    }

    private static string SanitizeJson(string file, string text, Dictionary<long, long> ids, ref long next)
    {
        var folder = Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(file)) ?? "");
        if (folder == "Sprites") return PageIndex().Replace(text, "\"texturePageIndex\": -1");
        if (folder == "Sounds") return GroupId().Replace(AudioId().Replace(text, "\"audioID\": -1"), "\"groupID\": 0");
        return folder == "Rooms" ? RemapIds(text, ids, ref next) : text;
    }

    private static string RemapIds(string text, Dictionary<long, long> ids, ref long next)
    {
        var node = JsonNode.Parse(text);
        var counter = next;
        long Map(long id)
        {
            if (!ids.TryGetValue(id, out var mapped)) ids[id] = mapped = counter++;
            return mapped;
        }

        RemapNode(node, Map);
        next = counter;
        return node?.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }) ?? text;
    }

    private static readonly string[] IdKeys = ["instanceID", "instanceIds", "instanceCreationOrderIDs"];

    private static void RemapNode(JsonNode? node, Func<long, long> map)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var (key, value) in obj.ToList())
                {
                    if (IdKeys.Contains(key) && value is JsonValue single && single.TryGetValue<long>(out var one)) obj[key] = map(one);
                    else if (IdKeys.Contains(key) && value is JsonArray list) RemapList(list, map);
                    else RemapNode(value, map);
                }

                break;
            case JsonArray array:
                foreach (var item in array) RemapNode(item, map);
                break;
        }
    }

    private static void RemapList(JsonArray list, Func<long, long> map)
    {
        var ids = list.Select(n => n is JsonValue v && v.TryGetValue<long>(out var id) ? map(id) : (long?)null).ToList();
        list.Clear();
        foreach (var id in ids.OfType<long>()) list.Add(id);
    }

    private static Regex Pattern(IEnumerable<string> names) =>
        new("(?<![A-Za-z0-9_])(" + string.Join('|', names.OrderByDescending(n => n.Length).Select(Regex.Escape)) + ")(?![A-Za-z0-9_])", RegexOptions.CultureInvariant);

    private static string RenameText(string text, bool json, Regex whole, IReadOnlyDictionary<string, string> renames)
    {
        // In JSON only string values change, never a key.
        if (!json) return whole.Replace(text, m => renames[m.Value]);
        var quoted = new Regex("\"(" + string.Join('|', renames.Keys.OrderByDescending(n => n.Length).Select(Regex.Escape)) + ")\"(?!\\s*:)", RegexOptions.CultureInvariant);
        return quoted.Replace(text, m => "\"" + renames[m.Groups[1].Value] + "\"");
    }

    // Item folders (Type/name or Type/name__idx0001) and the files in them that carry the name (name.json, name_0.png, name.wav, name.gml).
    private static void RenamePaths(string directory, IReadOnlyDictionary<string, string> renames)
    {
        foreach (var typeDir in Directory.GetDirectories(directory))
        {
            foreach (var itemDir in Directory.GetDirectories(typeDir))
            {
                var leaf = Path.GetFileName(itemDir);
                var index = leaf.IndexOf("__idx", StringComparison.Ordinal);
                var name = index > 0 ? leaf[..index] : leaf;
                if (!renames.TryGetValue(name, out var renamed)) continue;
                foreach (var file in Directory.GetFiles(itemDir))
                {
                    var stem = Path.GetFileNameWithoutExtension(file);
                    if (stem == name || (stem.StartsWith(name + "_", StringComparison.Ordinal) && stem[(name.Length + 1)..].All(char.IsAsciiDigit)))
                        File.Move(file, Path.Combine(itemDir, renamed + stem[name.Length..] + Path.GetExtension(file)));
                }

                Directory.Move(itemDir, Path.Combine(typeDir, renamed + (index > 0 ? leaf[index..] : "")));
            }
        }
    }

    [GeneratedRegex(@"^\.[A-Za-z0-9]{1,8}$")]
    private static partial Regex Extension();

    [GeneratedRegex("\"texturePageIndex\"\\s*:\\s*-?\\d+")]
    private static partial Regex PageIndex();

    [GeneratedRegex("\"audioID\"\\s*:\\s*-?\\d+")]
    private static partial Regex AudioId();

    [GeneratedRegex("\"groupID\"\\s*:\\s*-?\\d+")]
    private static partial Regex GroupId();
}
