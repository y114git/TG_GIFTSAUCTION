using System.Text;
using G3MLib.DataFile;
using G3MLib.DataFile.Project;
using G3MLib.Modding.Resources;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Core.Projects;

public sealed partial class ProjectService
{
    /// <summary>
    /// Exports the open model into a new project in the background. Edits made meanwhile wait and run afterwards.
    /// <paramref name="root"/> is the answer to the location question; without one the remembered folder is used.
    /// </summary>
    public async Task<ProjectLink> EnableAsync(Workspace.Workspace workspace, string? root = null, bool remember = false, IProgress<BatchProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        if (workspace.Project is not null) throw new InvalidOperationException("The project is already enabled.");
        root ??= Registry.NeedsLocationQuestion ? throw new InvalidOperationException("The projects folder has to be chosen first.") : Registry.RememberedRoot;
        root = Path.GetFullPath(root ?? throw new InvalidOperationException("The projects folder has to be chosen first."));
        if (remember) Registry.RememberRoot(root);

        // An older record whose folder is gone is replaced.
        if (Registry.Find(workspace.Identity) is { } stale) Registry.Remove(stale.Id);
        var id = Guid.NewGuid().ToString("N")[..8];
        var name = SafeName(workspace.Name);
        var folder = Path.Combine(root, $"{name}-{id}");
        Directory.CreateDirectory(folder);
        var record = new ProjectRecord(id, name, folder, workspace.Identity, workspace.Identity, Settings.Current.Project);
        Registry.Put(record);
        var link = new ProjectLink(workspace, record, Registry, _userData, false);
        try
        {
            await link.ExportAllAsync(progress, cancellationToken);
        }
        catch
        {
            Registry.Remove(id);
            CopyPackage.TryDelete(folder);
            throw;
        }

        workspace.Project = link;
        return link;
    }

    /// <summary>
    /// Registers a project folder that was written outside Enable project (a converted UTMT project) for the data file it was built with,
    /// and keeps its project file as the base of the first three-way merge, as the first export does.
    /// </summary>
    internal ProjectRecord Adopt(string folder, string name, DataFileIdentity identity)
    {
        var record = new ProjectRecord(Guid.NewGuid().ToString("N")[..8], name, folder, identity, identity, Settings.Current.Project);
        var baseFile = _userData.Combine("projects", record.Id, "base.g3mproj");
        Directory.CreateDirectory(Path.GetDirectoryName(baseFile) ?? ".");
        File.Copy(Path.Combine(folder, name + G3MProjectService.FileExtension), baseFile, true);
        Registry.Put(record);
        return record;
    }

    /// <summary>
    /// Registers a project folder for a data file: an existing record of the folder is pointed at the file and made active again, otherwise a new record is made.
    /// </summary>
    public ProjectRecord Link(string folder, string name, DataFileIdentity identity)
    {
        folder = Path.GetFullPath(folder);
        if (Registry.Projects.FirstOrDefault(p => Workspace.PathRules.Same(p.Folder, folder)) is not { } record) return Adopt(folder, name, identity);
        record = record with { Synced = identity, Archived = false };
        Registry.Put(record);
        return record;
    }

    /// <summary>
    /// The project file of a closed or unregistered project that belongs to an opened data file: one made for the same file, one in its own folder, or one in the folder
    /// above (the build layout) that was made from a file of the same name or content. Null when there is none. The folders are read off the calling thread.
    /// </summary>
    public async Task<string?> FindClosedAsync(Workspace.Workspace workspace, CancellationToken cancellationToken = default)
    {
        var closed = Registry.Projects.FirstOrDefault(p => p.Archived && (Workspace.PathRules.Same(p.Synced.Path, workspace.Path) || (p.Synced.Md5 == workspace.Identity.Md5 && p.Synced.Size == workspace.Identity.Size)));
        if (closed is not null) return Path.Combine(closed.Folder, closed.Name + G3MProjectService.FileExtension);
        var folder = Path.GetDirectoryName(workspace.Path);
        foreach (var dir in new[] { folder, folder is null ? null : Path.GetDirectoryName(folder) }.OfType<string>())
        {
            if (Registry.Projects.Any(p => !p.Archived && Workspace.PathRules.Same(p.Folder, dir))) continue;
            var files = await Task.Run(() => Directory.Exists(dir) ? Directory.EnumerateFiles(dir, "*" + G3MProjectService.FileExtension).ToList() : [], cancellationToken);
            foreach (var file in files)
                if (dir == folder || await MadeForAsync(file, workspace, cancellationToken)) return file;
        }

        return null;
    }

    // A project file in a parent folder may belong to another game: it counts when its base file has the name or the content of the data file.
    private static async Task<bool> MadeForAsync(string projectFile, Workspace.Workspace workspace, CancellationToken cancellationToken)
    {
        try
        {
            var recorded = (await G3MProjectService.OpenAsync(projectFile, null, cancellationToken)).BaseFile;
            return string.Equals(recorded.Name, Path.GetFileName(workspace.Path), Workspace.PathRules.Comparison)
                || recorded.Size == workspace.Identity.Size && string.Equals(recorded.Md5, workspace.Identity.Md5, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception e) when (e is ProjectException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>
    /// Writes a lost or unreadable data file again from its project (G3MLib Build). The build needs a data file to start from: the one
    /// given, else the newest rotating backup. A file that is in the way is kept as <c>.corrupt</c>. The project record then points at
    /// the new file, so opening it finds the project.
    /// </summary>
    public async Task<string> RebuildAsync(ProjectRecord record, string? baseFile = null, CancellationToken cancellationToken = default)
    {
        var target = record.Synced.Path;
        baseFile ??= NewestBackup(target) ?? throw new FileNotFoundException("There is no backup of the data file. Choose a copy of the game's data file to build on.");
        var temp = target + SavePipeline.NextSuffix;
        Directory.CreateDirectory(Path.GetDirectoryName(target) ?? ".");
        await Task.Run(() => G3MProjectService.Build(Path.Combine(record.Folder, record.Name + G3MProjectService.FileExtension), baseFile, temp), cancellationToken);
        try
        {
            using (await GameMakerIO.ReadAsync(temp, NoProgress.Instance, cancellationToken)) { }
            if (File.Exists(target)) File.Move(target, target + ".corrupt", true);
            File.Move(temp, target);
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }

        Registry.Put(record with { Synced = await DataFileIdentity.ComputeAsync(target, cancellationToken) });
        return target;
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(\..*)?$", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant)]
    private static partial System.Text.RegularExpressions.Regex ReservedDeviceName();

    internal static string SafeName(string name)
    {
        var text = new StringBuilder();
        foreach (var c in name) text.Append(Path.GetInvalidFileNameChars().Contains(c) || c == ' ' ? '_' : c);
        var safe = text.ToString().TrimEnd('.');
        if (safe.Length > 60) safe = safe[..60].TrimEnd('.');
        if (safe.Length == 0) return "project";
        // CON, NUL, COM1 and the like cannot be folder names on Windows, with or without an extension.
        return ReservedDeviceName().IsMatch(safe) ? "_" + safe : safe;
    }
}
