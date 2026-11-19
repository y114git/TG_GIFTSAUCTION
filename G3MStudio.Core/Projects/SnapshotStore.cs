using System.Text.Json;
using G3MLib.DataFile.Project;

namespace G3MStudio.Core.Projects;

public sealed record SnapshotUnit(string Path, string Type, string Hash);

public sealed record Snapshot(string Id, DateTimeOffset Time, string Label, List<SnapshotUnit> Units);

/// <summary>
/// Local history of a project (ADR-0011): one snapshot per sync, content-addressed per resource, so a resource
/// that did not change since an earlier snapshot is not stored again. Lives in the user data folder, not in the project.
/// shortcut: the count limit is a constant (<see cref="MaxSnapshots"/>), not a setting; make it one when users ask for a different history length.
/// </summary>
public sealed class SnapshotStore(string root)
{
    /// <summary>Autosync can take one snapshot per second of editing; older ones go past this many, with the objects only they used.</summary>
    public const int MaxSnapshots = 500;

    private string ObjectDir(string hash) => Path.Combine(root, "objects", hash);

    private string ManifestFile(string id) => Path.Combine(root, "snapshots", id + ".json");

    /// <summary>Records the project as it is now; only resources with a new hash are copied.</summary>
    public Snapshot Create(G3MProject project, string label, DateTimeOffset now)
    {
        var units = project.Resources.Select(r => new SnapshotUnit(r.Path, r.Type, r.Hash)).ToList();
        foreach (var resource in project.Resources)
        {
            var target = Path.Combine(ObjectDir(resource.Hash), Path.GetFileName(resource.Path));
            if (Directory.Exists(target) || File.Exists(target)) continue;
            var temp = target + ".tmp";
            SnapshotFiles.Delete(temp);
            SnapshotFiles.Copy(project.GetPath(resource), temp);
            if (Directory.Exists(temp)) Directory.Move(temp, target); else File.Move(temp, target);
        }

        var snapshot = new Snapshot(now.ToString("yyyyMMddHHmmssfff", System.Globalization.CultureInfo.InvariantCulture), now, label, units);
        Directory.CreateDirectory(Path.GetDirectoryName(ManifestFile(snapshot.Id)) ?? root);
        // Written beside the manifest and moved in, so a crash never leaves a half-written manifest.
        var manifest = ManifestFile(snapshot.Id);
        File.WriteAllText(manifest + ".tmp", JsonSerializer.Serialize(snapshot));
        File.Move(manifest + ".tmp", manifest, true);
        try
        {
            Prune(MaxSnapshots);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // an old snapshot a scanner holds is dropped by the next prune; the new one is already stored
        }

        return snapshot;
    }

    /// <summary>Drops the oldest snapshots beyond <paramref name="keep"/>, then every stored object no remaining snapshot refers to.</summary>
    public void Prune(int keep)
    {
        var all = List();
        if (all.Count <= keep) return;
        foreach (var old in all.Take(all.Count - keep)) File.Delete(ManifestFile(old.Id));
        var used = all.Skip(all.Count - keep).SelectMany(x => x.Units).Select(u => u.Hash).ToHashSet(StringComparer.Ordinal);
        var objects = Path.Combine(root, "objects");
        if (!Directory.Exists(objects)) return;
        foreach (var dir in Directory.EnumerateDirectories(objects).Where(d => !used.Contains(Path.GetFileName(d))))
            SnapshotFiles.Delete(dir);
    }

    /// <summary>Oldest first. A manifest that cannot be read (cut short by a crash) is skipped.</summary>
    public IReadOnlyList<Snapshot> List() =>
        Directory.Exists(Path.Combine(root, "snapshots"))
            ? [.. Directory.EnumerateFiles(Path.Combine(root, "snapshots"), "*.json").Order(StringComparer.Ordinal).Select(SnapshotFiles.ReadManifest).OfType<Snapshot>()]
            : [];

    /// <summary>Paths of resources that differ between the snapshot and the project: changed, added or removed.</summary>
    public static IReadOnlyList<string> Compare(Snapshot snapshot, G3MProject project)
    {
        var then = snapshot.Units.ToDictionary(u => u.Path, u => u.Hash);
        var now = project.Resources.ToDictionary(r => r.Path, r => r.Hash);
        return [.. then.Keys.Union(now.Keys).Where(p => !then.TryGetValue(p, out var a) || !now.TryGetValue(p, out var b) || a != b).Order(StringComparer.Ordinal)];
    }

    /// <summary>Puts one resource of the snapshot back into the project folder. The project file is not touched.</summary>
    public void Restore(Snapshot snapshot, string path, string projectFolder)
    {
        var unit = snapshot.Units.FirstOrDefault(u => u.Path == path) ?? throw new InvalidOperationException($"'{path}' is not in the snapshot.");
        var target = Path.Combine(projectFolder, path.Replace('/', Path.DirectorySeparatorChar));
        // Copied beside the target first: a missing object or a failed copy leaves the current resource as it is.
        var temp = target + ".restoring";
        SnapshotFiles.Delete(temp);
        try
        {
            SnapshotFiles.Copy(Path.Combine(ObjectDir(unit.Hash), Path.GetFileName(path)), temp);
            SnapshotFiles.Swap(temp, target);
        }
        finally
        {
            SnapshotFiles.Delete(temp);
        }
    }
}
