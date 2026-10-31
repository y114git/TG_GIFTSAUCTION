using System.Text.Json;
using G3MLib.DataFile.Project;

namespace G3MStudio.Core.Projects;

public sealed record SnapshotUnit(string Path, string Type, string Hash);

public sealed record Snapshot(string Id, DateTimeOffset Time, string Label, List<SnapshotUnit> Units);

/// <summary>
/// Local history of a project (ADR-0010, Q49 and Q52): one snapshot per sync, content-addressed per resource, so a resource
/// that did not change since an earlier snapshot is not stored again. Lives in the user data folder, not in the project.
/// shortcut: no size, count or age limit yet (the default is none); add pruning of unreferenced objects when the setting exists.
/// </summary>
public sealed class SnapshotStore(string root)
{
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
            Delete(temp);
            Copy(project.GetPath(resource), temp);
            if (Directory.Exists(temp)) Directory.Move(temp, target); else File.Move(temp, target);
        }

        var snapshot = new Snapshot(now.ToString("yyyyMMddHHmmssfff", System.Globalization.CultureInfo.InvariantCulture), now, label, units);
        Directory.CreateDirectory(Path.GetDirectoryName(ManifestFile(snapshot.Id)) ?? root);
        File.WriteAllText(ManifestFile(snapshot.Id), JsonSerializer.Serialize(snapshot));
        return snapshot;
    }

    /// <summary>Oldest first.</summary>
    public IReadOnlyList<Snapshot> List() =>
        Directory.Exists(Path.Combine(root, "snapshots"))
            ? [.. Directory.EnumerateFiles(Path.Combine(root, "snapshots"), "*.json").Order(StringComparer.Ordinal).Select(f => JsonSerializer.Deserialize<Snapshot>(File.ReadAllText(f))).OfType<Snapshot>()]
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
        Delete(target);
        Copy(Path.Combine(ObjectDir(unit.Hash), Path.GetFileName(path)), target);
    }

    private static void Delete(string path)
    {
        if (Directory.Exists(path)) Directory.Delete(path, true);
        else if (File.Exists(path)) File.Delete(path);
    }

    private static void Copy(string source, string target)
    {
        if (File.Exists(source))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(target) ?? ".");
            File.Copy(source, target, true);
            return;
        }

        Directory.CreateDirectory(target);
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var to = Path.Combine(target, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(to)!);
            File.Copy(file, to, true);
        }
    }
}
