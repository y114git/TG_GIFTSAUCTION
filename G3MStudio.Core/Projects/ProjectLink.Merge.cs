using G3MLib.DataFile;
using G3MLib.DataFile.Project;
using G3MLib.Modding.Resources;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Core.Projects;

/// <summary>Which version of a resource wins a conflict.</summary>
public enum MergeSide { Ours, Theirs }

/// <summary>A resource that changed in the project and in the data file since the last sync.</summary>
public sealed record MergeConflict(string Path, string Type, string Name);

/// <summary>
/// The resource level comparison of ADR-0010: base is the project at the last sync, ours the project now, theirs the data file that
/// changed outside. Conflicts are the resources that changed on both sides in different ways.
/// </summary>
public sealed class MergePlan
{
    internal Dictionary<string, (string Type, string Name)> Info { get; init; } = [];

    /// <summary>Changed only in the data file (or a list rebuilt from it): the project takes them.</summary>
    internal List<string> TakeTheirs { get; init; } = [];

    /// <summary>Removed only in the project: the data file has to lose them.</summary>
    internal List<string> OursDeleted { get; init; } = [];

    public List<MergeConflict> Conflicts { get; init; } = [];

    public int TheirsOnly => TakeTheirs.Count;

    /// <summary>Resource paths for the three-way decision. A missing base (a project from before merges) makes every difference a conflict.</summary>
    internal static MergePlan Classify(G3MProject? baseProject, G3MProject ours, G3MProject theirs)
    {
        var plan = new MergePlan();
        var paths = ours.Resources.Concat(theirs.Resources).Concat(baseProject?.Resources ?? []).Select(r => r.Path).Distinct().Order(StringComparer.Ordinal);
        foreach (var path in paths)
        {
            G3MProjectResource? o = ours.Find(path), t = theirs.Find(path), b = baseProject?.Find(path);
            if (Same(o, t)) continue;
            var info = o ?? t ?? b!;
            plan.Info[path] = (info.Type, info.Name);
            if (baseProject is not null && Same(t, b))
            {
                if (o is null) plan.OursDeleted.Add(path);
            }
            else if (baseProject is not null && Same(o, b) || IsDerivedList(path))
            {
                plan.TakeTheirs.Add(path);
            }
            else
            {
                plan.Conflicts.Add(new MergeConflict(path, info.Type, info.Name));
            }
        }

        return plan;
    }

    private static bool Same(G3MProjectResource? a, G3MProjectResource? b) => a?.Hash == b?.Hash;

    // Strings, variables and functions follow the resources and the code, so they are never asked about.
    private static bool IsDerivedList(string path) => path.StartsWith("strings/", StringComparison.Ordinal) || path.StartsWith("variables/", StringComparison.Ordinal) || path.StartsWith("functions/", StringComparison.Ordinal);
}

/// <summary>Three-way merge of a data file that changed outside, and replacing it from the project (ADR-0010 scenarios).</summary>
public sealed partial class ProjectLink
{
    private string MergeDir => _userData.Combine("projects", Record.Id, "merge");

    /// <summary>
    /// Compares the three states. The open model is the data file as it is on disk (a changed file opens without journal edits), so its
    /// resources are exported to a scratch project and hashed like the project is.
    /// </summary>
    public async Task<MergePlan> PlanMergeAsync(IProgress<BatchProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var theirsDir = Path.Combine(MergeDir, "theirs");
        if (Directory.Exists(theirsDir)) Directory.Delete(theirsDir, true);
        var theirsFile = await Exclusive(() => G3MProjectService.Export(_workspace.Data, theirsDir, null, new G3MProjectOptions { Name = Record.Name }, progress, cancellationToken).Path, cancellationToken);
        try
        {
            var theirs = await G3MProjectService.OpenAsync(theirsFile, null, cancellationToken);
            var ours = await G3MProjectService.OpenAsync(ProjectFile, null, cancellationToken);
            var baseProject = File.Exists(BaseFile) ? await G3MProjectService.OpenAsync(BaseFile, null, cancellationToken) : null;
            return MergePlan.Classify(baseProject, ours, theirs);
        }
        finally
        {
            Directory.Delete(theirsDir, true);
        }
    }

    /// <summary>
    /// Brings the data file's changes into the project and writes the merged data file (backups rotate as in a save). The conflicts
    /// not listed in <paramref name="choices"/> keep the project's version. Afterwards the data file differs from the open model, so the
    /// caller opens it again.
    /// </summary>
    public Task<SaveResult> BringInAsync(MergePlan plan, IReadOnlyDictionary<string, MergeSide> choices, SavePipeline pipeline, CancellationToken cancellationToken = default)
    {
        var theirs = plan.TakeTheirs.Concat(plan.Conflicts.Where(c => choices.GetValueOrDefault(c.Path) == MergeSide.Theirs).Select(c => c.Path)).ToList();
        var deleted = plan.OursDeleted.Concat(plan.Conflicts.Where(c => choices.GetValueOrDefault(c.Path) != MergeSide.Theirs && !Directory.Exists(Path.Combine(Folder, c.Path)) && !File.Exists(Path.Combine(Folder, c.Path))).Select(c => c.Path)).ToList();
        return ReplaceDataFileAsync(theirs, deleted, plan.Info, pipeline, cancellationToken);
    }

    /// <summary>
    /// Writes the data file from the project, whatever the file on disk holds: every project resource is imported over it and the ones the
    /// project dropped since the last sync are removed. Resources only the data file has stay in it (the library cannot tell them from the game's own).
    /// </summary>
    public async Task<SaveResult> OverwriteDataFileAsync(SavePipeline pipeline, CancellationToken cancellationToken = default)
    {
        var deleted = new List<string>();
        var info = new Dictionary<string, (string Type, string Name)>();
        if (File.Exists(BaseFile))
        {
            var baseProject = await G3MProjectService.OpenAsync(BaseFile, null, cancellationToken);
            var ours = await G3MProjectService.OpenAsync(ProjectFile, null, cancellationToken);
            foreach (var gone in baseProject.Resources.Where(r => ours.Find(r.Path) is null))
            {
                deleted.Add(gone.Path);
                info[gone.Path] = (gone.Type, gone.Name);
            }
        }

        return await ReplaceDataFileAsync([], deleted, info, pipeline, cancellationToken);
    }

    private async Task<SaveResult> ReplaceDataFileAsync(List<string> theirs, List<string> deleted, Dictionary<string, (string Type, string Name)> info, SavePipeline pipeline, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(MergeDir);
        var built = Path.Combine(MergeDir, "built.win");
        await Exclusive(() =>
        {
            // Theirs-only resources are written from the open model, which is the data file; deletions go on the project file so Build removes them.
            G3MProjectService.Update(_workspace.Data, Folder, null, new G3MProjectOptions { Name = Record.Name, Only = Group(theirs, info), Deleted = Group(deleted, info) });
            G3MProjectService.Build(ProjectFile, _workspace.Path, built);
            return 0;
        }, cancellationToken);

        try
        {
            using var merged = await GameMakerIO.ReadAsync(built, NoProgress.Instance, cancellationToken);
            var result = await pipeline.SaveAsync(merged, _workspace.Path, null, cancellationToken);
            if (!result.Saved) return result;
            var identity = await DataFileIdentity.ComputeAsync(_workspace.Path, CancellationToken.None);
            await Task.Run(() =>
            {
                // Clears the recorded deletions and records the new base file.
                G3MProjectService.Update(merged, Folder, identity.Path, new G3MProjectOptions { Name = Record.Name, Only = Group([], info), Deleted = Group([], info) });
                RecordSync("merge");
            }, CancellationToken.None);
            Record = Record with { Synced = identity };
            _registry.Put(Record);
            DataFileChanged = false;
            Changed?.Invoke();
            return result;
        }
        finally
        {
            File.Delete(built);
        }
    }

    private static Dictionary<string, IReadOnlySet<string>> Group(IEnumerable<string> paths, Dictionary<string, (string Type, string Name)> info) =>
        paths.Select(p => info[p]).GroupBy(i => i.Type).ToDictionary(g => g.Key, g => (IReadOnlySet<string>)g.Select(i => i.Name).Where(n => n.Length > 0).ToHashSet());
}
