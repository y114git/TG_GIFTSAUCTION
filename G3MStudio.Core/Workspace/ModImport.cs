using System.IO.Compression;
using System.Text.Json;

namespace G3MStudio.Core.Workspace;

/// <summary>One operation of a G3M mod that changes a data file.</summary>
public sealed record ModPatch(string Source, string Target, string Type)
{
    /// <summary>The name of the data file the operation targets, the last part of its target path.</summary>
    public string TargetFile => Path.GetFileName(Target.Replace('\\', '/').TrimEnd('/'));

    /// <summary>The archive member of the payload, when the source lives inside the mod (<c>${mod_path}/name</c>); otherwise null.</summary>
    public string? Member => Source.StartsWith("${mod_path}/", StringComparison.Ordinal) ? Source["${mod_path}/".Length..] : null;

    /// <summary>Studio applies .g3mpatch payloads; xdelta and vcdiff need the xdelta program and .csx a script run, which G3MTool and Tools > Run Script do.</summary>
    public bool Supported => Type == "patch" && Member is not null && Path.GetExtension(Member).Equals(".g3mpatch", StringComparison.OrdinalIgnoreCase);
}

/// <summary>What a mod ZIP says about itself (mod_config.json, config version 2.0.0): its name, version, game and the operations that patch a data file.</summary>
public sealed record ModInfo(string Name, string Version, string Game, IReadOnlyList<ModPatch> Patches);

/// <summary>Reads a G3M mod ZIP without unpacking it: the config is read in memory and a payload is copied out by name only, to a file name with no folders.</summary>
public static class ModImport
{
    public const long MaxConfigBytes = 4 * 1024 * 1024;
    public const long MaxPayloadBytes = 4L * 1024 * 1024 * 1024;

    public static ModInfo Read(string zipPath)
    {
        using var zip = ZipFile.OpenRead(zipPath);
        var entry = zip.GetEntry("mod_config.json") ?? throw new InvalidDataException("The ZIP has no mod_config.json at its root.");
        if (entry.Length > MaxConfigBytes) throw new InvalidDataException("mod_config.json is larger than 4 MiB.");
        using var stream = entry.Open();
        using var document = JsonDocument.Parse(stream, new JsonDocumentOptions { MaxDepth = 64 });
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object) throw new InvalidDataException("mod_config.json is not an object.");
        string Text(string name) => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";
        var patches = new List<ModPatch>();
        if (root.TryGetProperty("files", out var files) && files.ValueKind == JsonValueKind.Array) Collect(files, patches, 0);
        return new ModInfo(Text("name"), Text("version"), Text("game"), patches);
    }

    // Groups are objects with one key whose value is an operation array; operations have a type. Only patch operations matter here.
    private static void Collect(JsonElement array, List<ModPatch> into, int depth)
    {
        if (depth > 32) return;
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;
            if (item.TryGetProperty("type", out var type) && type.ValueKind == JsonValueKind.String)
            {
                if (type.GetString() == "patch" && item.TryGetProperty("source", out var source) && source.ValueKind == JsonValueKind.String && item.TryGetProperty("target", out var target) && target.ValueKind == JsonValueKind.String)
                    into.Add(new ModPatch(source.GetString() ?? "", target.GetString() ?? "", "patch"));
                continue;
            }

            foreach (var group in item.EnumerateObject())
                if (group.Value.ValueKind == JsonValueKind.Array) Collect(group.Value, into, depth + 1);
        }
    }

    /// <summary>The patch operations of the mod that target a data file of this name (case does not matter), in the order the mod lists them.</summary>
    public static IReadOnlyList<ModPatch> ForDataFile(ModInfo mod, string dataFilePath) =>
        [.. mod.Patches.Where(p => p.TargetFile.Equals(Path.GetFileName(dataFilePath), StringComparison.OrdinalIgnoreCase))];

    /// <summary>Copies one payload out of the ZIP into the folder, under its file name only. Returns the path.</summary>
    public static string Extract(string zipPath, ModPatch patch, string folder)
    {
        var member = patch.Member ?? throw new InvalidDataException("The payload is not inside the mod.");
        if (member.Contains("..", StringComparison.Ordinal)) throw new InvalidDataException("The payload path leaves the mod.");
        using var zip = ZipFile.OpenRead(zipPath);
        var entry = zip.GetEntry(member) ?? throw new InvalidDataException("The payload " + member + " is not in the ZIP.");
        if (entry.Length > MaxPayloadBytes) throw new InvalidDataException("The payload is larger than 4 GiB.");
        Directory.CreateDirectory(folder);
        var target = Path.Combine(folder, Path.GetFileName(member));
        entry.ExtractToFile(target, true);
        return target;
    }
}
