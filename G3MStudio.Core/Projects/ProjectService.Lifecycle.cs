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
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
            throw;
        }

        workspace.Project = link;
        return link;
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

    private static string SafeName(string name)
    {
        var text = new StringBuilder();
        foreach (var c in name) text.Append(Path.GetInvalidFileNameChars().Contains(c) || c == ' ' ? '_' : c);
        return text.Length == 0 ? "project" : text.ToString();
    }
}
