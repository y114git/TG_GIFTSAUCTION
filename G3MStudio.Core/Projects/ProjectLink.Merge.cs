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
/// The resource level comparison of ADR-0011: base is the project at the last sync, ours the project now, theirs the data file that
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
            var info = o ?? t ?? b ?? throw new InvalidOperationException("The path has no resource in any side.");
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

/// <summary>Three-way merge of a data file that changed outside, and replacing it from the project (ADR-0011 scenarios).</summary>
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
        await Task.Run(() => CopyPackage.TryDelete(theirsDir), CancellationToken.None); // a folder of thousands of files takes seconds to delete: never on the UI thread
        if (Directory.Exists(theirsDir)) theirsDir += "-" + Guid.NewGuid().ToString("N")[..8]; // still held by a scanner: use a fresh name
        // Each phase hands its memory back before the next one starts (the export, the three project files, the build), so the peak is the largest phase and not their sum.
        var theirsFile = await Exclusive(() =>
        {
            var path = G3MProjectService.Export(_workspace.Data, theirsDir, null, new G3MProjectOptions { Name = Record.Name }, progress, cancellationToken).Path;
            ProcessMemory.Reclaim();
            return path;
        }, cancellationToken);
        try
        {
            var plan = await ClassifyAsync(theirsFile, cancellationToken);
            // The three project files are only needed for the comparison, and this method has ended with them: they are freed before the build loads the data file again.
            await Task.Run(() => ProcessMemory.Reclaim(), CancellationToken.None);
            return plan;
        }
        finally
        {
            await Task.Run(() => CopyPackage.TryDelete(theirsDir), CancellationToken.None);
        }
    }

    private async Task<MergePlan> ClassifyAsync(string theirsFile, CancellationToken cancellationToken)
    {
        var theirs = await G3MProjectService.OpenAsync(theirsFile, null, cancellationToken);
        var ours = await G3MProjectService.OpenAsync(ProjectFile, null, cancellationToken);
        var baseProject = File.Exists(BaseFile) ? await G3MProjectService.OpenAsync(BaseFile, null, cancellationToken) : null;
        return MergePlan.Classify(baseProject, ours, theirs);
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
    public Task<SaveResult> OverwriteDataFileAsync(SavePipeline pipeline, CancellationToken cancellationToken = default) => OverwriteDataFileAsync(pipeline, [], cancellationToken);

    private async Task<SaveResult> OverwriteDataFileAsync(SavePipeline pipeline, List<(string Type, string Name, string Path)> refresh, CancellationToken cancellationToken)
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

        return await ReplaceDataFileAsync([], deleted, info, pipeline, cancellationToken, refresh);
    }

    /// <summary>
    /// Takes what changed in the project folder outside (a pasted resource folder, a Git pull) into the data file. A resource whose folder differs from
    /// the last write is taken from the folder, even when the app edited it too. Other edits not synced yet are written first
    /// (a data file that changed outside is never overwritten: it goes through the merge), then the data file is built from the folder.
    /// </summary>
    public async Task<SaveResult?> BringInFolderAsync(SavePipeline pipeline, CancellationToken cancellationToken = default)
    {
        List<(string Type, string Name, string Path)> outside = [];
        if (!DataFileChanged)
        {
            // shortcut: a write of everything (_full) cannot spare single resources, so it still overwrites the pasted folder; upgrade when such edits show up in practice.
            outside = await FoldersChangedOutsideAsync(cancellationToken);
            if (outside.Count == 0 && NotSynced == 0 && !HasPendingWrite && !await HasNewFoldersAsync(cancellationToken)) return null; // nothing changed in the folder or the data file
            _touched.RemoveWhere(t => t.Name is not null && outside.Any(o => o.Type == t.Type && o.Name == t.Name));
            if (NotSynced > 0 || HasPendingWrite)
            {
                var synced = await SyncAsync(pipeline, null, cancellationToken);
                if (!synced.Saved) return synced;
            }
        }

        return await OverwriteDataFileAsync(pipeline, outside, cancellationToken);
    }

    /// <summary>A resource folder the project file does not list yet (copied in from elsewhere), looked for in the type folders the project already has.</summary>
    private async Task<bool> HasNewFoldersAsync(CancellationToken cancellationToken)
    {
        var project = await G3MProjectService.OpenAsync(ProjectFile, null, cancellationToken);
        var known = project.Resources.Select(r => r.Path).ToHashSet(StringComparer.Ordinal);
        var types = known.Select(p => p[..p.IndexOf('/', StringComparison.Ordinal)]).Distinct();
        return await Task.Run(() => types.Any(t => Directory.Exists(Path.Combine(Folder, t)) && Directory.EnumerateFileSystemEntries(Path.Combine(Folder, t)).Any(e => !known.Any(k => k == t + "/" + Path.GetFileName(e) || k.StartsWith(t + "/" + Path.GetFileName(e) + "/", StringComparison.Ordinal)))), cancellationToken);
    }

    /// <summary>Resources the project file lists whose folder is gone, found by the last <see cref="BringInFolderAsync"/>. The data file keeps them: the library cannot tell a deletion from a folder never written.</summary>
    public IReadOnlyList<string> MissingFromFolder { get; private set; } = [];

    /// <summary>Resources whose folder no longer hashes to what the project file recorded at the last write.</summary>
    private async Task<List<(string Type, string Name, string Path)>> FoldersChangedOutsideAsync(CancellationToken cancellationToken)
    {
        var project = await G3MProjectService.OpenAsync(ProjectFile, null, cancellationToken);
        MissingFromFolder = [.. project.Resources.Where(r => !Exists(Path.Combine(Folder, r.Path))).Select(r => r.Name)];
        return await Task.Run(() => project.Resources
            .Where(r => Exists(Path.Combine(Folder, r.Path)) && FolderHash(Path.Combine(Folder, r.Path)) != r.Hash)
            .Select(r => (r.Type, r.Name, r.Path)).ToList(), cancellationToken);
    }

    // As the library hashes its text files: no BOM, LF line endings, one final newline, so a line-ending-only edit is not a change.
    private static byte[] Normalized(string file)
    {
        var bytes = File.ReadAllBytes(file);
        if (!new[] { ".json", ".gml", ".asm" }.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase)) return bytes;
        var text = System.Text.Encoding.UTF8.GetString(bytes).TrimStart('﻿').Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n') + "\n";
        return System.Text.Encoding.UTF8.GetBytes(text);
    }

    private static bool Exists(string unit) => Directory.Exists(unit) || File.Exists(unit);

    // The library's hash of a resource folder or single file unit: file names and contents in name order (its text files are already normalized when it writes them).
    private static string FolderHash(string folder)
    {
        using var hash = System.Security.Cryptography.IncrementalHash.CreateHash(System.Security.Cryptography.HashAlgorithmName.SHA256);
        var root = Directory.Exists(folder) ? folder : Path.GetDirectoryName(folder) ?? folder;
        string[] files = Directory.Exists(folder) ? [.. Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)] : [folder];
        foreach (var file in files.OrderBy(f => Path.GetRelativePath(root, f).Replace(Path.DirectorySeparatorChar, '/'), StringComparer.Ordinal))
        {
            var name = System.Text.Encoding.UTF8.GetBytes(Path.GetRelativePath(root, file).Replace(Path.DirectorySeparatorChar, '/'));
            var content = Normalized(file);
            hash.AppendData(BitConverter.GetBytes(name.Length));
            hash.AppendData(name);
            hash.AppendData(BitConverter.GetBytes(content.LongLength));
            hash.AppendData(content);
        }

        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    private async Task<SaveResult> ReplaceDataFileAsync(List<string> theirs, List<string> deleted, Dictionary<string, (string Type, string Name)> info, SavePipeline pipeline, CancellationToken cancellationToken, List<(string Type, string Name, string Path)>? refresh = null)
    {
        Directory.CreateDirectory(MergeDir);
        var built = Path.Combine(MergeDir, "built.win");
        await Exclusive(() =>
        {
            // Theirs-only resources are written from the open model, which is the data file; deletions go on the project file so Build removes them.
            // Without theirs and deletions it would only record the hashes of folders copied in from outside, and Build would then skip them as unchanged.
            if (theirs.Count > 0 || deleted.Count > 0) G3MProjectService.Update(_workspace.Data, Folder, null, new G3MProjectOptions { Name = Record.Name, Only = Group(theirs, info), Deleted = Group(deleted, info) });
            G3MProjectService.Build(ProjectFile, _workspace.Path, built);
            ProcessMemory.Reclaim();
            return 0;
        }, cancellationToken);

        try
        {
            using var merged = await GameMakerIO.ReadAsync(built, NoProgress.Instance, cancellationToken);
            var result = await pipeline.SaveAsync(merged, _workspace.Path, null, cancellationToken);
            if (!result.Saved) return result;
            await Task.Run(() => ProcessMemory.Reclaim(), CancellationToken.None);
            _workspace.RememberStamp(); // the merge wrote the file itself, so it is not a change by another program
            var identity = await DataFileIdentity.ComputeAsync(_workspace.Path, CancellationToken.None);
            await Task.Run(() =>
            {
                // Clears the recorded deletions and records the new base file.
                // The folders taken from outside are written again from the merged model, which records their new hashes.
                foreach (var r in refresh ?? []) info[r.Path] = (r.Type, r.Name);
                G3MProjectService.Update(merged, Folder, identity.Path, new G3MProjectOptions { Name = Record.Name, Only = Group((refresh ?? []).Select(r => r.Path), info), Deleted = Group([], info) });
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
            FileCleanup.TryDeleteFile(built);
        }
    }

    private static Dictionary<string, IReadOnlySet<string>> Group(IEnumerable<string> paths, Dictionary<string, (string Type, string Name)> info) =>
        paths.Select(p => info[p]).GroupBy(i => i.Type).ToDictionary(g => g.Key, g => (IReadOnlySet<string>)g.Select(i => i.Name).Where(n => n.Length > 0).ToHashSet());
}
