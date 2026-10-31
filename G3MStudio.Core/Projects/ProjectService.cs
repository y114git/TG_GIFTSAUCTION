using System.Text;
using G3MStudio.Core.Settings;
using G3MLib.DataFile;
using G3MLib.DataFile.Project;
using G3MLib.Modding.Resources;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Core.Projects;

/// <summary>Turns Project mode on and off for a data file and finds the project of a file that is opened (ADR-0010, Q48 to Q50).</summary>
public sealed class ProjectService(UserData userData, SettingsStore? settings = null)
{
    public SettingsStore Settings { get; } = settings ?? new SettingsStore(userData);

    public ProjectRegistry Registry { get; } = ProjectRegistry.Load(userData);

    /// <summary>The default projects folder: next to the program (Q48).</summary>
    public string DefaultRoot { get; init; } = Path.Combine(AppContext.BaseDirectory, "projects");

    /// <summary>What the location question shows first.</summary>
    public string SuggestedRoot => Registry.RememberedRoot ?? DefaultRoot;

    /// <summary>
    /// Opens the project of an opened data file, matched by path, then by size and MD5 (ADR-0010 scenarios). Null when there is none or
    /// its folder is gone (Enable project exports it again). The data file is compared with the last synced state.
    /// </summary>
    public ProjectLink? Attach(Workspace.Workspace workspace)
    {
        if (Registry.Find(workspace.Identity) is not { } record || !File.Exists(Path.Combine(record.Folder, record.Name + ".g3mproj"))) return null;
        var changed = !string.Equals(record.Synced.Md5, workspace.Identity.Md5, StringComparison.OrdinalIgnoreCase);
        if (!string.Equals(record.Synced.Path, workspace.Identity.Path, StringComparison.OrdinalIgnoreCase) && !changed)
        {
            // The same game file opened from another path (a copy): the project follows it.
            record = record with { Synced = workspace.Identity };
            Registry.Put(record);
        }

        return workspace.Project = new ProjectLink(workspace, record, Registry, userData, changed);
    }

    /// <summary>The one-time offer to enable a project: true the first time a data file is opened without a project.</summary>
    public bool ShouldOffer(Workspace.Workspace workspace)
    {
        if (workspace.Project is not null || Registry.WasOffered(workspace.Path)) return false;
        Registry.MarkOffered(workspace.Path);
        return true;
    }

    /// <summary>
    /// Exports the open model into a new project in the background. Edits made meanwhile wait and run afterwards.
    /// <paramref name="root"/> is the answer to the location question; without one the remembered folder is used.
    /// </summary>
    public async Task<ProjectLink> EnableAsync(Workspace.Workspace workspace, string? root = null, bool remember = false, IProgress<BatchProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        if (workspace.Project is not null) throw new InvalidOperationException("The project is already enabled.");
        root ??= Registry.NeedsLocationQuestion ? throw new InvalidOperationException("The projects folder has to be chosen first.") : Registry.RememberedRoot;
        root = Path.GetFullPath(root!);
        if (remember) Registry.RememberRoot(root);

        // An older record whose folder is gone is replaced.
        if (Registry.Find(workspace.Identity) is { } stale) Registry.Remove(stale.Id);
        var id = Guid.NewGuid().ToString("N")[..8];
        var name = SafeName(workspace.Name);
        var folder = Path.Combine(root, $"{name}-{id}");
        Directory.CreateDirectory(folder);
        var record = new ProjectRecord(id, name, folder, workspace.Identity, workspace.Identity, Settings.Current.Project);
        Registry.Put(record);
        var link = new ProjectLink(workspace, record, Registry, userData, false);
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

    /// <summary>Ends Project mode for the open file. The project is archived (it can be enabled again) or deleted; the data file is not touched.</summary>
    public void Disable(Workspace.Workspace workspace, bool deleteProject)
    {
        if (workspace.Project is not { } link) return;
        workspace.Project = null;
        if (!deleteProject)
        {
            Registry.Put(link.Record with { Archived = true });
            return;
        }

        Registry.Remove(link.Record.Id);
        if (Directory.Exists(link.Folder)) Directory.Delete(link.Folder, true);
        var snapshots = userData.Combine("projects", link.Record.Id);
        if (Directory.Exists(snapshots)) Directory.Delete(snapshots, true);
    }

    /// <summary>The newest rotating backup of a data file, or null.</summary>
    public string? NewestBackup(string dataFile)
    {
        var pipeline = new SavePipeline(userData);
        return Enumerable.Range(1, pipeline.BackupCount).Select(slot => pipeline.BackupSlot(dataFile, slot)).FirstOrDefault(File.Exists);
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
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
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
