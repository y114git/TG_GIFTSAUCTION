using G3MStudio.Core.Settings;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Core.Projects;

/// <summary>Turns Project mode on and off for a data file and finds the project of a file that is opened (ADR-0010, Q48 to Q50).</summary>
public sealed partial class ProjectService(UserData userData, SettingsStore? settings = null)
{
    private readonly UserData _userData = userData;

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

        return workspace.Project = new ProjectLink(workspace, record, Registry, _userData, changed);
    }

    /// <summary>The one-time offer to enable a project: true the first time a data file is opened without a project.</summary>
    public bool ShouldOffer(Workspace.Workspace workspace)
    {
        if (workspace.Project is not null || Registry.WasOffered(workspace.Path)) return false;
        Registry.MarkOffered(workspace.Path);
        return true;
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
        var snapshots = _userData.Combine("projects", link.Record.Id);
        if (Directory.Exists(snapshots)) Directory.Delete(snapshots, true);
    }

    /// <summary>The newest rotating backup of a data file, or null.</summary>
    public string? NewestBackup(string dataFile)
    {
        var pipeline = new SavePipeline(_userData);
        return Enumerable.Range(1, pipeline.BackupCount).Select(slot => pipeline.BackupSlot(dataFile, slot)).FirstOrDefault(File.Exists);
    }
}
