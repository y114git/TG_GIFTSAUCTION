using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Text;

namespace G3MStudio.Core.Workspace;

/// <summary>The per-user data folder (settings, journals, backups). Tests pass a temp folder.</summary>
public sealed partial class UserData(string root)
{
    public string Root { get; } = Directory.CreateDirectory(root).FullName;

    /// <summary>The app data folder; <c>G3MSTUDIO_DATA</c> replaces it (tests use this so they never touch the real settings).</summary>
    public static UserData Default()
    {
        var data = new UserData(Environment.GetEnvironmentVariable("G3MSTUDIO_DATA") is { Length: > 0 } custom ? custom : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "G3MStudio"));
        data.SweepStaleTemp(TimeSpan.FromDays(1));
        data.SweepTrash(TimeSpan.FromDays(30));
        return data;
    }

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

    /// <summary>Deletes the included-files trash batches (folders named by the delete time) that are older than <paramref name="age"/>.</summary>
    public void SweepTrash(TimeSpan age)
    {
        var trash = Combine("trash");
        if (!Directory.Exists(trash)) return;
        try
        {
            foreach (var batch in Directory.EnumerateDirectories(trash))
            {
                if (!DateTime.TryParseExact(Path.GetFileName(batch), "yyyyMMdd-HHmmss-fff", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var deleted) || deleted > DateTime.Now - age) continue;
                try
                {
                    Directory.Delete(batch, true);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    // in use or locked: try again next start
                }
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // an unreadable folder is skipped
        }
    }

    [GeneratedRegex(@"\.[0-9a-f]{32}\.tmp$")]
    private static partial Regex TempName();

    /// <summary>Deletes temp files that a crashed <see cref="WriteAllTextAtomic"/> left behind and that are older than <paramref name="age"/>.</summary>
    public void SweepStaleTemp(TimeSpan age)
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(Root, "*.tmp", SearchOption.AllDirectories))
            {
                if (!TempName().IsMatch(file) || File.GetLastWriteTimeUtc(file) > DateTime.UtcNow - age) continue;
                try
                {
                    File.Delete(file);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    // in use or locked: try again next start
                }
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // an unreadable folder is skipped
        }
    }
}
