using System.Text.Json;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Core.Projects;

/// <summary>Finding and reading a UTMT project folder, without the data file.</summary>
public sealed partial class UtmtProjectImporter
{
    // Folder names of the 13 asset types UTMT writes (the library's SerializableAssetType); the first five hold one folder per asset.
    private static readonly string[] AssetFolders = ["backgrounds", "fonts", "shaders", "sounds", "sprites", "animcurves", "code", "objects", "particlesystems", "paths", "rooms", "scripts", "sequences"];

    private static readonly string[] OptionKeys = ["Flags", "VerifyStrings", "ErrorOnWarnings", "AssetsDataFilePath", "ExcludeDirectories", "SubProjects", "Patches", "ExternalFiles", "FileCopies", "FileDeletes", "PreImportScripts", "PreAssetImportScripts", "PostImportScripts"];

    private static readonly JsonDocumentOptions Lenient = new() { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };

    public const string MainFileName = "project.json";

    /// <summary>The main file of the UTMT project at a folder or file path, or null when the path is not a UTMT project.</summary>
    public static string? Detect(string path)
    {
        if (File.Exists(path)) return IsMainFile(path) ? Path.GetFullPath(path) : null;
        if (!Directory.Exists(path)) return null;
        var named = Path.Combine(path, MainFileName);
        if (File.Exists(named) && IsMainFile(named)) return Path.GetFullPath(named);
        var found = Directory.EnumerateFiles(path, "*.json").Where(IsMainFile).Take(2).ToList();
        return found.Count == 1 ? Path.GetFullPath(found[0]) : null;
    }

    private static bool IsMainFile(string file)
    {
        if (!string.Equals(Path.GetExtension(file), ".json", StringComparison.OrdinalIgnoreCase)) return false;
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(file), Lenient);
            return document.RootElement is { ValueKind: JsonValueKind.Object } root && root.TryGetProperty("Name", out var name) && name.ValueKind == JsonValueKind.String
                && OptionKeys.Any(key => root.TryGetProperty(key, out _));
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>Reads and checks the project. Throws <see cref="UtmtImportException"/> when it is not a UTMT project or holds nothing to convert.</summary>
    public static UtmtProjectInfo Inspect(string path)
    {
        var main = Detect(path) ?? throw new UtmtImportException($"\"{path}\" is not a UTMT project: there is no project.json with a project name and options.");
        JsonElement root;
        try
        {
            root = JsonElement.Parse(File.ReadAllText(main), Lenient);
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException)
        {
            throw new UtmtImportException($"The project file \"{main}\" cannot be read: {e.Message}", e);
        }

        var folder = Path.GetDirectoryName(main) ?? "";
        var excluded = Strings(root, "ExcludeDirectories").ToHashSet(StringComparer.Ordinal);
        var resources = new Dictionary<string, int>();
        foreach (var name in AssetFolders.Where(n => !excluded.Contains(n) && Directory.Exists(Path.Combine(folder, n))))
        {
            var files = Directory.EnumerateFiles(Path.Combine(folder, name), "*", SearchOption.AllDirectories);
            var count = name == "code"
                ? files.Where(f => Path.GetExtension(f) is ".gml" or ".json").Select(f => Path.GetRelativePath(folder, Path.ChangeExtension(f, null))).Distinct().Count()
                : files.Count(f => string.Equals(Path.GetExtension(f), ".json", StringComparison.OrdinalIgnoreCase));
            if (count > 0) resources[name] = count;
        }

        var hasGeneral = File.Exists(Path.Combine(folder, "general-info-options.json"));
        if (resources.Count == 0 && !hasGeneral) throw new UtmtImportException($"The project \"{main}\" holds no resources: none of the resource folders has a file.");

        List<string> options = [];
        foreach (var key in new[] { "Patches", "ExternalFiles", "FileCopies", "FileDeletes", "SubProjects", "ExcludeDirectories" })
            if (root.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.Array && value.GetArrayLength() > 0) options.Add(key);
        if (Strings(root, "Flags").Any()) options.Add("Flags");
        List<string> scripts = [.. Strings(root, "PreImportScripts"), .. Strings(root, "PreAssetImportScripts"), .. Strings(root, "PostImportScripts")];
        var ignored = Directory.EnumerateDirectories(folder).Select(d => Path.GetFileName(d)).Where(d => !d.StartsWith('.') && !AssetFolders.Contains(d) && !excluded.Contains(d)).Order(StringComparer.Ordinal).ToList();

        long? size = null;
        string? md5 = null;
        if (root.TryGetProperty("G3M", out var g3m) && g3m.ValueKind == JsonValueKind.Object)
        {
            if (g3m.TryGetProperty("BaseFileSize", out var s) && s.ValueKind == JsonValueKind.Number) size = s.GetInt64();
            if (g3m.TryGetProperty("BaseFileMd5", out var m) && m.ValueKind == JsonValueKind.String) md5 = m.GetString();
        }

        return new UtmtProjectInfo
        {
            MainFile = main,
            Name = root.GetProperty("Name").GetString() is { Length: > 0 } title ? title : Path.GetFileName(folder),
            Resources = resources,
            HasGeneralInfo = hasGeneral,
            Scripts = scripts,
            Options = options,
            IgnoredFolders = ignored,
            BaseSize = size,
            BaseMd5 = md5,
            InGitRepository = InRepository(folder),
        };
    }

    private static IEnumerable<string> Strings(JsonElement root, string key) =>
        root.TryGetProperty(key, out var list) && list.ValueKind == JsonValueKind.Array ? list.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.String).Select(e => e.GetString() ?? "") : [];

    private static bool InRepository(string folder)
    {
        for (var dir = new DirectoryInfo(folder); dir is not null; dir = dir.Parent)
            if (Directory.Exists(Path.Combine(dir.FullName, ".git")) || File.Exists(Path.Combine(dir.FullName, ".git"))) return true;
        return false;
    }

    /// <summary>Compares a data file with the identity a G3MLib project recorded. A project written by UndertaleModTool records none.</summary>
    public static async Task<UtmtBaseCheck> CheckBaseAsync(UtmtProjectInfo info, string dataFile, CancellationToken cancellationToken = default)
    {
        if (info.BaseMd5 is null) return UtmtBaseCheck.Unrecorded;
        var identity = await DataFileIdentity.ComputeAsync(dataFile, cancellationToken);
        return identity.Size == info.BaseSize && string.Equals(identity.Md5, info.BaseMd5, StringComparison.OrdinalIgnoreCase) ? UtmtBaseCheck.Match : UtmtBaseCheck.Mismatch;
    }
}
