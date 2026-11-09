using G3MLib.DataFile;

namespace G3MStudio.Core.Workspace;

public sealed partial class IncludedFiles
{
    /// <summary>Copies the files into the folder. A name that exists is skipped and listed in the result, so nothing is overwritten by accident; a file that cannot be copied is listed as failed and the rest go on.</summary>
    public (IReadOnlyList<string> Added, IReadOnlyList<string> Skipped, IReadOnlyList<string> Failed) Add(IEnumerable<string> sources, string subFolder = "")
    {
        List<string> added = [], skipped = [], failed = [];
        foreach (var source in sources)
        {
            var relative = (subFolder.Length > 0 ? subFolder.TrimEnd('/') + "/" : "") + Path.GetFileName(source);
            try
            {
                var target = Resolve(relative);
                if (File.Exists(target) || Directory.Exists(target))
                {
                    skipped.Add(relative);
                    continue;
                }

                Directory.CreateDirectory(ParentOf(target));
                File.Copy(source, target);
                added.Add(relative);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                failed.Add(relative);
            }
        }

        return (added, skipped, failed);
    }

    private static string ParentOf(string file) => Path.GetDirectoryName(file) ?? throw new InvalidOperationException($"'{file}' has no folder.");

    /// <summary>Overwrites a file with the content of another one; the new content is written beside it first, so a failure keeps the old file.</summary>
    public void Replace(string relative, string source)
    {
        var target = Resolve(relative);
        if (!File.Exists(target)) throw new FileNotFoundException($"'{relative}' does not exist.");
        var temp = target + TempSuffix;
        try
        {
            File.Copy(source, temp, true);
            File.Move(temp, target, true);
        }
        catch
        {
            try
            {
                File.Delete(temp);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // the original error is the one to report
            }

            throw;
        }
    }

    public void Export(string relative, string destination) => File.Copy(Resolve(relative), destination, true);

    /// <summary>Renames the file within its folder. Throws <see cref="IncludedFileUseException"/> when a sound or the strings use the name, unless <paramref name="force"/>.</summary>
    public string Rename(string relative, string newName, GameMakerData? data, bool force)
    {
        if (newName.Length == 0 || newName != Path.GetFileName(newName) || newName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) throw new InvalidOperationException($"'{newName}' is not a valid file name.");
        var source = Resolve(relative);
        var target = Path.Combine(ParentOf(source), newName);
        Resolve(Relative(target));
        // A case-only rename ("Mus.ogg" to "mus.ogg") finds the file itself on a case-insensitive disk.
        if ((File.Exists(target) || Directory.Exists(target)) && !string.Equals(source, target, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException($"'{newName}' already exists.");
        if (!force) CheckUse(relative, data, true);
        File.Move(source, target);
        return Relative(target);
    }

    /// <summary>Moves the file to the trash folder of the user data (or deletes it when there is none). Same use check as <see cref="Rename"/>.</summary>
    public void Delete(string relative, GameMakerData? data, bool force)
    {
        var source = Resolve(relative);
        if (!File.Exists(source)) throw new FileNotFoundException($"'{relative}' does not exist.");
        if (!force) CheckUse(relative, data, false);
        if (home is null)
        {
            File.Delete(source);
            return;
        }

        var target = home.Combine("trash", DateTime.Now.ToString("yyyyMMdd-HHmmss-fff", System.Globalization.CultureInfo.InvariantCulture), relative);
        Directory.CreateDirectory(ParentOf(target));
        File.Move(source, target);
    }

    /// <summary>What refers to the file by name: sounds that stream it, and the string table when a string equals the name.</summary>
    public static IReadOnlyList<string> UsedBy(GameMakerData? data, string relative)
    {
        if (data is null) return [];
        var name = Path.GetFileName(relative);
        bool Same(string? text) => text is not null && (text.Equals(name, StringComparison.OrdinalIgnoreCase) || text.Equals(relative, StringComparison.OrdinalIgnoreCase));
        List<string> uses = [.. data.Sounds.Where(s => s is not null && Same(s.File?.Content)).Select(s => s.Name?.Content ?? "")];
        if (data.Strings.Any(s => s is not null && Same(s.Content))) uses.Add("strings");
        return uses;
    }

    private static void CheckUse(string relative, GameMakerData? data, bool rename)
    {
        if (UsedBy(data, relative) is { Count: > 0 } uses)
            throw new IncludedFileUseException(rename, $"'{Path.GetFileName(relative)}' is used by {string.Join(", ", uses)}.");
    }
}
