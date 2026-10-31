using System.Text.Json;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Core.Projects;

/// <summary>When a background write starts (Q49, like VS Code): never, a delay after the last change, when the window loses focus, or on Save.</summary>
public enum Trigger { Off, AfterDelay, OnFocusLoss, OnSave }

/// <summary>Autosave writes changed resources into the project; autosync writes the data file. They are separate settings.</summary>
public sealed record ProjectSettings(Trigger AutoSave = Trigger.AfterDelay, int AutoSaveSeconds = 2, Trigger AutoSync = Trigger.OnSave, int AutoSyncSeconds = 30);

/// <summary>One project the app knows. <see cref="Original"/> is the data file at enable time, <see cref="Synced"/> the data file after the last sync.</summary>
public sealed record ProjectRecord(string Id, string Name, string Folder, DataFileIdentity Original, DataFileIdentity Synced, ProjectSettings Settings, bool Archived = false, bool Writing = false);

public enum ProjectState { Synced, NotSynced, Syncing, Exporting, DataFileChanged }

/// <summary>What to do with a data file that changed outside (ADR-0010): merge its changes into the project, replace it from the project, or keep both apart.</summary>
public enum ExternalChange { BringIn, Overwrite, KeepBoth }

/// <summary>Where the data file goes when its location changes (Q50). Recreate builds it from the project and leaves the original alone.</summary>
public enum RelocateMode { Move, Copy, Recreate }

/// <summary>Projects the app knows (<c>projects.json</c> in the user data folder) and the remembered projects folder of Q48.</summary>
public sealed class ProjectRegistry
{
    private sealed record Content(List<ProjectRecord> Projects, string? RememberedRoot, List<string> Offered);

    private static readonly StringComparer PathComparer = OperatingSystem.IsLinux() ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;

    private readonly string _file;
    private Content _content = new([], null, []);

    private ProjectRegistry(string file) => _file = file;

    public IReadOnlyList<ProjectRecord> Projects => _content.Projects;

    /// <summary>The folder chosen with "remember this choice", or null.</summary>
    public string? RememberedRoot => _content.RememberedRoot;

    /// <summary>The first Enable project asks where to save projects; it asks again when the remembered folder is gone (Q48).</summary>
    public bool NeedsLocationQuestion => RememberedRoot is null || !Directory.Exists(RememberedRoot);

    public static ProjectRegistry Load(UserData userData)
    {
        var registry = new ProjectRegistry(userData.Combine("projects.json"));
        try
        {
            registry._content = JsonSerializer.Deserialize<Content>(File.ReadAllText(registry._file)) ?? registry._content;
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            // A damaged list loses the known projects, not their folders.
        }

        return registry;
    }

    /// <summary>The active project of a data file: same path, or same size and MD5 as at enable time or at the last sync (a copy elsewhere).</summary>
    public ProjectRecord? Find(DataFileIdentity identity) =>
        _content.Projects.FirstOrDefault(p => !p.Archived && PathComparer.Equals(p.Synced.Path, identity.Path))
        ?? _content.Projects.FirstOrDefault(p => !p.Archived && (Same(p.Synced, identity) || Same(p.Original, identity)));

    /// <summary>The active project last synced to this path, for a data file that is missing or does not read.</summary>
    public ProjectRecord? FindByPath(string path) => _content.Projects.FirstOrDefault(p => !p.Archived && PathComparer.Equals(p.Synced.Path, Path.GetFullPath(path)));

    public ProjectRecord? Get(string id) => _content.Projects.FirstOrDefault(p => p.Id == id);

    public void Put(ProjectRecord record)
    {
        var i = _content.Projects.FindIndex(p => p.Id == record.Id);
        if (i < 0) _content.Projects.Add(record); else _content.Projects[i] = record;
        Save();
    }

    public void Remove(string id)
    {
        _content.Projects.RemoveAll(p => p.Id == id);
        Save();
    }

    public void RememberRoot(string? root)
    {
        _content = _content with { RememberedRoot = root };
        Save();
    }

    /// <summary>The one-time offer to enable a project for a data file path has been shown.</summary>
    public bool WasOffered(string dataFile) => _content.Offered.Contains(UserData.KeyFor(dataFile));

    public void MarkOffered(string dataFile)
    {
        if (WasOffered(dataFile)) return;
        _content.Offered.Add(UserData.KeyFor(dataFile));
        Save();
    }

    private static bool Same(DataFileIdentity a, DataFileIdentity b) => a.Size == b.Size && string.Equals(a.Md5, b.Md5, StringComparison.OrdinalIgnoreCase);

    private void Save() => UserData.WriteAllTextAtomic(_file, JsonSerializer.Serialize(_content));
}
