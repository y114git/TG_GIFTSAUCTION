using System.Security.Cryptography;
using System.Text;

namespace G3MStudio.Core.Workspace;

/// <summary>The per-user data folder (settings, journals, backups). Tests pass a temp folder.</summary>
public sealed partial class UserData(string root, bool create = true)
{
    public string Root { get; } = create ? Directory.CreateDirectory(root).FullName : Path.GetFullPath(root);

    /// <summary>Why the real data folder could not be used (the session runs on a temp folder, or on one that does not exist and takes no writes); null for a normal folder.</summary>
    public string? Problem { get; private init; }

    /// <summary>The app data folder; <c>G3MSTUDIO_DATA</c> replaces it (tests use this so they never touch the real settings).</summary>
    public static UserData Default()
    {
        var data = OpenOrTemp(Environment.GetEnvironmentVariable("G3MSTUDIO_DATA") is { Length: > 0 } custom ? custom : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "G3MStudio"));
        data.SweepStaleTemp(TimeSpan.FromDays(1));
        data.SweepTrash(TimeSpan.FromDays(30));
        return data;
    }

    /// <summary>
    /// A data folder that cannot be created (read-only profile, full drive) must not stop the program from starting: the session uses a temp folder of this user
    /// and loses its settings at the next clean-up. When that fails too, the folder is only named: every write fails on its own and is reported as a data write failure.
    /// </summary>
    internal static UserData OpenOrTemp(string root, string? tempBase = null)
    {
        try
        {
            return new UserData(root);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            var user = string.Concat((Environment.UserName.Length > 0 ? Environment.UserName : "user").Select(c => char.IsAsciiLetterOrDigit(c) ? c : '_'));
            var temp = Path.Combine(tempBase ?? Path.GetTempPath(), "G3MStudio-" + user);
            try
            {
                return new UserData(temp) { Problem = e.Message };
            }
            catch (Exception inner) when (inner is IOException or UnauthorizedAccessException)
            {
                return new UserData(temp, create: false) { Problem = inner.Message };
            }
        }
    }

    public string Combine(params string[] parts) => Path.Combine([Root, .. parts]);

    /// <summary>Stable folder name for one data file path; unchanged by saves, so journals and backups survive them.</summary>
    public static string KeyFor(string path)
    {
        var full = Path.GetFullPath(path);
        if (!OperatingSystem.IsLinux() && !PathRules.HasCaseTwin(full)) full = full.ToUpperInvariant(); // a folder that holds Data.win and data.win keeps them apart
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(full)))[..16];
    }

    /// <summary>Writes next to the target and renames over it, so a crash leaves the old or the new file.</summary>
    public static void WriteAllTextAtomic(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        var temp = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(temp, text);
            File.Move(temp, path, overwrite: true);
        }
        catch
        {
            try
            {
                File.Delete(temp);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // the write error is the one to report; SweepStaleTemp removes the leftover later
            }

            throw;
        }
    }
}
