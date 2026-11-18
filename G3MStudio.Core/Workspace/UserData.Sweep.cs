using System.Text.RegularExpressions;

namespace G3MStudio.Core.Workspace;

public sealed partial class UserData
{
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
