namespace G3MStudio.Core.Workspace;

public sealed partial class UserData
{
    // The folders that keep one file per data file, named by KeyFor(path): dock layout and tabs, layer stacks, folders, locks, nicknames, run options, auto-tile sets.
    // The journal and the backups are not here: they belong to the content and the path of the file they were made for.
    private static readonly string[] PerFileFolders = ["view", "organize", "locks", "nicknames", "runoptions", "autotile"];

    // The file names one data file has in those folders: <key>.json everywhere, plus <key>.layers.json in view. Nothing else is adopted (no .tmp, .bak or other leftovers).
    private static readonly string[] PerFileSuffixes = [".json", ".layers.json"];

    /// <summary>
    /// Gives the data file at <paramref name="to"/> everything Studio remembers about the one at <paramref name="from"/> (layout, tabs, sizes, folders, locks,
    /// nicknames, run options), so a moved, renamed or replaced file keeps its settings. A file that already has its own stays unless <paramref name="overwrite"/>.
    /// All or nothing: when a copy fails, the files already written are taken back and the previous ones restored.
    /// </summary>
    public int AdoptFileData(string from, string to, bool overwrite = false)
    {
        var (oldKey, newKey) = (KeyFor(from), KeyFor(to));
        if (oldKey == newKey) return 0;
        var plan = new List<(string Source, string Target)>();
        foreach (var folder in PerFileFolders.Select(f => Combine(f)).Where(Directory.Exists))
        {
            foreach (var suffix in PerFileSuffixes)
            {
                var (source, target) = (Path.Combine(folder, oldKey + suffix), Path.Combine(folder, newKey + suffix));
                if (File.Exists(source) && (overwrite || !File.Exists(target))) plan.Add((source, target));
            }
        }

        var restore = SnapshotFileData(to);
        try
        {
            foreach (var (source, target) in plan) File.Copy(source, target, true);
        }
        catch
        {
            restore();
            throw;
        }

        return plan.Count;
    }

    private IEnumerable<string> FileDataOf(string key) => PerFileFolders.SelectMany(folder => PerFileSuffixes.Select(suffix => Combine(folder, key + suffix)));

    /// <summary>
    /// Takes what Studio remembers about a data file as it is now. The returned action puts it back exactly (files that did not exist are removed again),
    /// for a change that has to be taken back when what followed it failed. Best effort: a file that cannot be written back is skipped.
    /// </summary>
    public Action SnapshotFileData(string path)
    {
        var saved = FileDataOf(KeyFor(path)).Select(file => (file, bytes: File.Exists(file) ? File.ReadAllBytes(file) : null)).ToList();
        return () =>
        {
            foreach (var (file, bytes) in saved)
            {
                try
                {
                    if (bytes is null) File.Delete(file);
                    else File.WriteAllBytes(file, bytes);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    // the error that made the change fail is the one to report
                }
            }
        };
    }

    /// <summary>Forgets the dock layout, open tabs and room views kept for a data file, so it opens with the default layout. Folders, locks and run options stay.</summary>
    public void ForgetLayout(string path)
    {
        if (Combine("view") is var folder && Directory.Exists(folder))
            foreach (var file in Directory.EnumerateFiles(folder, KeyFor(path) + ".json")) File.Delete(file);
    }
}
