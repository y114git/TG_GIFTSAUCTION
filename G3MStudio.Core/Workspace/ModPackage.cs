using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace G3MStudio.Core.Workspace;

/// <summary>Writes a G3M mod zip (config version 2.0.0): <c>mod_config.json</c> plus one patch file applied to the game's data file.</summary>
public static partial class ModPackage
{
    public const string ConfigVersion = "2.0.0";

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    /// <summary>A lowercase id of letters, digits, '_' and '-' that starts with a letter, at most 50 characters.</summary>
    public static string MakeId(string name)
    {
        var id = Invalid().Replace(name.Trim().ToLowerInvariant(), "_").Trim('_', '-');
        if (id.Length == 0 || !char.IsAsciiLetter(id[0])) id = "mod_" + id;
        return id.Length > 50 ? id[..50] : id;
    }

    public static string ConfigJson(string name, string id, string version, string game, string dataFileName) =>
        JsonSerializer.Serialize(new
        {
            config_version = ConfigVersion,
            id,
            name = name.Length > 50 ? name[..50] : name,
            version,
            authors = Array.Empty<string>(),
            game,
            files = new[] { new { source = "${mod_path}/mod.g3mpatch", target = "${game_path}/" + dataFileName, type = "patch" } },
        }, Json) + "\n";

    /// <summary>Writes the zip under a temporary name and moves it into place, so a failure leaves no half file.</summary>
    public static void Write(string zipPath, string patchPath, string name, string game, string dataFileName, string version = "1.0.0")
    {
        var temp = zipPath + ".tmp";
        try
        {
            using (var zip = ZipFile.Open(temp, ZipArchiveMode.Create))
            {
                zip.CreateEntryFromFile(patchPath, "mod.g3mpatch", CompressionLevel.Optimal);
                var entry = zip.CreateEntry("mod_config.json", CompressionLevel.Optimal);
                using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
                writer.Write(ConfigJson(name, MakeId(name), version, MakeId(game), dataFileName));
            }

            File.Move(temp, zipPath, true);
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }

    [GeneratedRegex("[^a-z0-9_-]+")]
    private static partial Regex Invalid();
}
