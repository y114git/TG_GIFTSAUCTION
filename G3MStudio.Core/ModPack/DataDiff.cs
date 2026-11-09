using G3MLib.DataFile;
using G3MLib.DataFile.Models;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Core.ModPack;

/// <summary>What happened to a resource going from the left file to the right file.</summary>
public enum ChangeKind { Added, Removed, Changed }

/// <summary>One resource that differs. Type is the Explorer type key (sprites, objects, code...); a hash is the content hash in that file, null when it is not there.</summary>
public sealed record DiffItem(string Type, string Name, ChangeKind Kind, string? LeftHash, string? RightHash)
{
    public string Key => ResourceTypes.KeyOf(Type, Name);
}

/// <summary>One property whose value differs; a side that lacks the property has null.</summary>
public sealed record PropertyChange(string Path, string? Left, string? Right);

/// <summary>One room instance that differs, found by its instance id.</summary>
public sealed record InstanceChange(uint Id, string ObjectName, ChangeKind Kind, string Detail);

/// <summary>Which items to show: types (empty means all), kinds (empty means all) and a text the name contains.</summary>
public sealed record DiffFilter(IReadOnlyCollection<string> Types, IReadOnlyCollection<ChangeKind> Kinds, string Text = "");

public sealed record DiffResult(IReadOnlyList<DiffItem> Items)
{
    public int Count(ChangeKind kind) => Items.Count(i => i.Kind == kind);

    public IReadOnlyList<DiffItem> Where(DiffFilter filter) =>
        [.. Items.Where(i => (filter.Types.Count == 0 || filter.Types.Contains(i.Type)) && (filter.Kinds.Count == 0 || filter.Kinds.Contains(i.Kind))
            && (filter.Text.Length == 0 || i.Name.Contains(filter.Text, StringComparison.OrdinalIgnoreCase)))];

    /// <summary>Items grouped by type in the order of the Explorer.</summary>
    public static IEnumerable<IGrouping<string, DiffItem>> ByType(IEnumerable<DiffItem> items) =>
        items.GroupBy(i => i.Type).OrderBy(g => Array.IndexOf(CopyPlanner.Supported, g.Key));
}

/// <summary>Compares two loaded data files resource by resource (the types a copy supports) and, for one resource, in detail.</summary>
public static class DataDiff
{
    /// <summary>Hold the read locks of both files when called off the UI thread.</summary>
    public static DiffResult Compare(GameMakerData left, GameMakerData right, CancellationToken cancellationToken = default)
    {
        using var l = new Fingerprinter(left);
        using var r = new Fingerprinter(right);
        var items = new List<DiffItem>();
        foreach (var type in CopyPlanner.Supported)
        {
            var (a, b) = (Names(left, type), Names(right, type));
            foreach (var name in a.Union(b))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var (hashLeft, hashRight) = (a.Contains(name) ? l.Of(type, name) : null, b.Contains(name) ? r.Of(type, name) : null);
                if (hashLeft == hashRight) continue;
                items.Add(new DiffItem(type, name, hashLeft is null ? ChangeKind.Added : hashRight is null ? ChangeKind.Removed : ChangeKind.Changed, hashLeft, hashRight));
            }
        }

        return new DiffResult(items);
    }

    private static HashSet<string> Names(GameMakerData data, string type) =>
        [.. ResourceTypes.All.First(t => t.Key == type).Items(data)?.Select(ResourceTypes.NameOf).Where(n => n.Length > 0) ?? []];

    /// <summary>The properties that differ, matched by path.</summary>
    public static IReadOnlyList<PropertyChange> Properties(GameMakerData left, GameMakerData right, string type, string name)
    {
        Dictionary<string, string> Rows(GameMakerData d) => PropertyModel.FindRoot(d, type, name) is { } root
            ? PropertyModel.Describe(root).GroupBy(r => r.Path).ToDictionary(g => g.Key, g => g.First().Value) : [];
        var (a, b) = (Rows(left), Rows(right));
        return [.. a.Keys.Union(b.Keys).Where(k => a.GetValueOrDefault(k) != b.GetValueOrDefault(k))
            .Select(k => new PropertyChange(k, a.GetValueOrDefault(k), b.GetValueOrDefault(k)))];
    }

    /// <summary>The text of a code entry for the code view: GML when the decompiler can read it, else the assembly. Null when the file has no such entry.</summary>
    public static string? CodeOf(GameMakerData data, string entry)
    {
        if (data.Code.ByName(entry) is null) return null;
        try
        {
            return CodeText.Decompile(data, entry);
        }
        catch (Exception e) when (e is InvalidOperationException or NotSupportedException or ArgumentException or IndexOutOfRangeException)
        {
            return CodeText.Disassemble(data, entry);
        }
    }

    /// <summary>The instances that were added, removed or changed (moved, scaled, turned, another object) between two versions of a room.</summary>
    public static IReadOnlyList<InstanceChange> Instances(GameMakerData left, GameMakerData right, string room)
    {
        Dictionary<uint, GameMakerRoom.GameObject> Of(GameMakerData d) => d.Rooms.FirstOrDefault(r => r.Name.Content == room) is { } found
            ? Fingerprinter.InstancesOf(found).ToDictionary(i => i.InstanceID) : [];
        var (a, b) = (Of(left), Of(right));
        var changes = new List<InstanceChange>();
        foreach (var id in a.Keys.Union(b.Keys).Order())
        {
            var (x, y) = (a.GetValueOrDefault(id), b.GetValueOrDefault(id));
            if (x is null || y is null) changes.Add(new InstanceChange(id, Describe(x ?? y), x is null ? ChangeKind.Added : ChangeKind.Removed, At(x ?? y)));
            else if (Describe(x) != Describe(y) || At(x) != At(y)) changes.Add(new InstanceChange(id, Describe(y), ChangeKind.Changed, $"{Describe(x)} {At(x)} > {Describe(y)} {At(y)}"));
        }

        return changes;
    }

    private static string Describe(GameMakerRoom.GameObject? instance) => instance?.ObjectDefinition?.Name?.Content ?? "";

    private static string At(GameMakerRoom.GameObject? i) => i is null ? "" : FormattableString.Invariant($"({i.X}, {i.Y}) x{i.ScaleX}/{i.ScaleY} r{i.Rotation}");
}
