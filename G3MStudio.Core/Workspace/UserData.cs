using System.Security.Cryptography;
using System.Text;

namespace G3MStudio.Core.Workspace;

/// <summary>The per-user data folder (settings, journals, backups). Tests pass a temp folder.</summary>
public sealed class UserData(string root)
{
    public string Root { get; } = Directory.CreateDirectory(root).FullName;

    /// <summary>The app data folder; <c>G3MSTUDIO_DATA</c> replaces it (tests use this so they never touch the real settings).</summary>
    public static UserData Default() => new(Environment.GetEnvironmentVariable("G3MSTUDIO_DATA") is { Length: > 0 } custom ? custom : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "G3MStudio"));

    public string Combine(params string[] parts) => Path.Combine([Root, .. parts]);

    /// <summary>Stable folder name for one data file path; unchanged by saves, so journals and backups survive them.</summary>
    public static string KeyFor(string path)
    {
        var full = Path.GetFullPath(path);
        if (!OperatingSystem.IsLinux()) full = full.ToUpperInvariant();
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(full)))[..16];
    }

    /// <summary>Writes next to the target and renames over it, so a crash leaves the old or the new file.</summary>
    public static void WriteAllTextAtomic(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        var temp = path + ".tmp";
        File.WriteAllText(temp, text);
        File.Move(temp, path, overwrite: true);
    }
}
