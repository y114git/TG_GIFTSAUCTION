using G3MStudio.Core.Workspace;

namespace G3MStudio.Core.Projects;

/// <summary>The checks and clean-up around a UTMT conversion: destination rules, code sources, warnings and Git.</summary>
public sealed partial class UtmtProjectImporter
{
    // The export writes the decompiled code of every entry; the project's own source keeps its comments and layout, and it is what the entry was compiled from.
    private static void KeepCodeSources(string utmtFolder, string destination)
    {
        var code = Path.Combine(utmtFolder, "code");
        if (!Directory.Exists(code)) return;
        foreach (var source in Directory.EnumerateFiles(code, "*.gml", SearchOption.AllDirectories))
        {
            var name = Path.GetFileNameWithoutExtension(source);
            var target = Path.Combine(destination, "code", name, name + ".gml");
            if (File.Exists(target)) File.Copy(source, target, true);
        }
    }

    // The destination may be new or empty, or hold only the files of a repository the user prepared.
    private static HashSet<string> CheckDestination(UtmtProjectInfo info, string baseFile, string destination)
    {
        if (Inside(destination, info.Folder) || Inside(info.Folder, destination)) throw new UtmtImportException("The new project folder and the UTMT project folder must be different folders that do not contain each other. The UTMT project is never changed.");
        if (File.Exists(destination)) throw new UtmtImportException($"\"{destination}\" is a file. Choose a folder.");
        string[] allowed = [".git", ".gitignore", ".gitattributes"];
        var existing = Directory.Exists(destination) ? Directory.EnumerateFileSystemEntries(destination).Select(Path.GetFileName).OfType<string>().ToHashSet(PathRules.Comparer) : [];
        if (existing.Any(e => !allowed.Contains(e, PathRules.Comparer))) throw new UtmtImportException($"\"{destination}\" is not empty. Choose a new or empty folder.");
        if (Inside(baseFile, destination)) throw new UtmtImportException("The data file is inside the new project folder. Choose another folder.");
        return existing;
    }

    private static bool Inside(string path, string folder)
    {
        var relative = Path.GetRelativePath(folder, path);
        return relative == "." || (!relative.StartsWith("..", StringComparison.Ordinal) && !Path.IsPathRooted(relative));
    }

    // A failed or cancelled conversion leaves the destination as it was.
    private static void RemoveNew(string destination, HashSet<string> before)
    {
        if (!Directory.Exists(destination)) return;
        foreach (var entry in Directory.EnumerateFileSystemEntries(destination).Where(e => !before.Contains(Path.GetFileName(e), PathRules.Comparer)))
        {
            if (File.Exists(entry)) TryDeleteFile(entry);
            else CopyPackage.TryDelete(entry);
        }

        if (before.Count == 0 && !Directory.EnumerateFileSystemEntries(destination).Any()) CopyPackage.TryDelete(destination);
    }

    private static void TryDeleteFile(string file)
    {
        try
        {
            File.Delete(file);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // a file another program holds must not hide the error that ended the conversion
        }
    }

    private static List<UtmtWarning> Notes(UtmtProjectInfo info, IEnumerable<string> library, IEnumerable<string> export)
    {
        List<UtmtWarning> warnings = [];
        if (info.Options.Count > 0) warnings.Add(new("options", string.Join(", ", info.Options)));
        if (info.Scripts.Count > 0) warnings.Add(new("scripts", string.Join(", ", info.Scripts)));
        if (info.IgnoredFolders.Count > 0) warnings.Add(new("ignoredFolders", string.Join(", ", info.IgnoredFolders)));
        if (info.InGitRepository) warnings.Add(new("history", info.Folder));
        warnings.AddRange(library.Select(w => new UtmtWarning("library", w)));
        warnings.AddRange(export.Select(w => new UtmtWarning("export", w)));
        return warnings;
    }

    // A repository the user prepared is kept as it is; it only gets the ignore and attribute files when it has none. A new one gets a first commit.
    private async Task<UtmtGitState> SetUpGitAsync(string folder, bool init, string name, List<UtmtWarning> warnings, CancellationToken cancellationToken)
    {
        var git = new GitService(folder, gitEnvironment);
        var existing = git.IsRepository;
        if (!existing && !init) return UtmtGitState.None;
        if (!await GitService.IsInstalledAsync(cancellationToken))
        {
            warnings.Add(new("gitMissing", ""));
            return UtmtGitState.None;
        }

        var result = await git.InitAsync(cancellationToken);
        if (result.Ok && !existing) result = await git.StageAllAsync(cancellationToken);
        if (result.Ok && !existing) result = await git.CommitAsync($"Import UTMT project {name}", cancellationToken);
        if (!result.Ok) warnings.Add(new("gitFailed", result.Error));
        return existing ? UtmtGitState.Existing : result.Ok ? UtmtGitState.Initialized : UtmtGitState.None;
    }
}
