using G3MLib.DataFile;
using G3MLib.DataFile.Models;

namespace G3MStudio.Core.Workspace;

/// <summary>What happens to one item when it meets the target data file.</summary>
public enum CopyAction
{
    /// <summary>The name is free in the target: the item is added as it is.</summary>
    Add,

    /// <summary>The name is taken: the item is added under a new name (name_copy, name_copy2).</summary>
    Rename,

    /// <summary>The name is taken by an item of the same type: the target item takes the values of the copy.</summary>
    Replace,

    /// <summary>The name is taken: the item stays out and whatever refers to it uses the target's item of that name.</summary>
    Skip,

    /// <summary>The item cannot be copied; <see cref="PlanEntry.Refusal"/> says why.</summary>
    Refused,
}

/// <summary>How a name that exists in the target is handled.</summary>
public enum CollisionMode
{
    Rename,
    Skip,
    Replace,
}

/// <param name="Type">Explorer type key (sprites, objects, scripts...).</param>
/// <param name="Name">Name in the source file.</param>
/// <param name="TargetName">Name in the target file after the copy (differs from Name only for Rename).</param>
/// <param name="Action">What the copy does with the item.</param>
/// <param name="Refusal">Why the item cannot be copied, or null.</param>
/// <param name="Facts">How the item got into the plan and how it meets the target.</param>
public sealed record PlanEntry(string Type, string Name, string TargetName, CopyAction Action, string? Refusal, CopyFacts Facts)
{
    public string Key => ResourceTypes.KeyOf(Type, Name);

    /// <summary>True when the user did not select the item: something that is copied uses it.</summary>
    public bool IsDependency => Facts.IsDependency;

    /// <summary>True when the target already uses the name (for any type).</summary>
    public bool Collides => Facts.Collides;

    /// <summary>True when the item the name collides with has the same type, so Replace is possible.</summary>
    public bool SameType => Facts.SameType;

    /// <summary>For a dependency, the items that use it (the first few).</summary>
    public IReadOnlyList<string> UsedBy => Facts.UsedBy;
}

public sealed record CopyFacts(bool IsDependency, bool Collides, bool SameType, IReadOnlyList<string> UsedBy);

/// <summary>The preview of a copy: every item that would be copied, skipped or refused, and what could not be followed.</summary>
public sealed record CopyPlan(IReadOnlyList<PlanEntry> Entries, IReadOnlyList<string> Warnings, string? Refusal)
{
    public IEnumerable<PlanEntry> Applied => Entries.Where(e => e.Action is CopyAction.Add or CopyAction.Rename or CopyAction.Replace);

    public bool CanApply => Refusal is null && Applied.Any();
}

public sealed record CopyVersion(byte Bytecode, uint Major, uint Minor, uint Release, uint Build)
{
    public static CopyVersion Of(GameMakerData data) =>
        data.GeneralInfo is { } info ? new CopyVersion(info.BytecodeVersion, info.Major, info.Minor, info.Release, info.Build) : new CopyVersion(0, 0, 0, 0, 0);

    private ulong Order => ((ulong)Major << 40) + ((ulong)Minor << 20) + Release;

    public override string ToString() => $"{Major}.{Minor}.{Release}.{Build} (bytecode {Bytecode})";

    /// <summary>Null when items of this version can go into <paramref name="target"/>; else the reason they cannot.</summary>
    public string? RefusalFor(CopyVersion target)
    {
        if (Bytecode != target.Bytecode) return $"The bytecode versions differ (source {Bytecode}, target {target.Bytecode}); code and events would not run in the target.";
        if ((Major == 1) != (target.Major == 1)) return $"One file is GameMaker Studio 1 and the other is not (source {this}, target {target}); their data layouts differ.";
        return null;
    }

    /// <summary>A note when the target is detected as older than the source: the library adapts the items to the target, but features it lacks are dropped.</summary>
    public string? WarningFor(CopyVersion target) =>
        target.Order < Order
            ? $"The target ({target}) is older than the source ({this}); properties the target version does not have are not copied."
            : null;
}

/// <summary>Builds the copy preview: which items are copied, what they pull in, and how names that exist in the target are handled.</summary>
public static class CopyPlanner
{
    /// <summary>Types the copy supports. Extensions are refused: the library can only export all of them, with their files.</summary>
    public static readonly string[] Supported =
        ["sprites", "tileSets", "sounds", "paths", "scripts", "shaders", "fonts", "timelines", "objects", "rooms", "sequences", "animationCurves", "particleSystems", "code"];

    /// <summary>Call with the source model read lock held and the target model not changing.</summary>
    public static CopyPlan Build(GameMakerData source, GameMakerData target, IReadOnlyList<(string Type, string Name)> items, bool withDependencies,
        CollisionMode defaultMode, IReadOnlyDictionary<string, CollisionMode>? overrides, CancellationToken cancellationToken = default)
    {
        var whole = CopyVersion.Of(source).RefusalFor(CopyVersion.Of(target));
        if (whole is not null) return new CopyPlan([.. items.Select(i => Refused(i.Type, i.Name, false, whole))], [], whole);

        var graph = withDependencies ? CopyGraph.Build(source, cancellationToken) : null;
        var warnings = new List<string>();
        var found = new List<(string Type, string Name, bool Dependency, string? Refusal, List<string> UsedBy)>();
        var seen = new Dictionary<string, int>(StringComparer.Ordinal);
        var queue = new Queue<int>();
        void Add(string type, string name, bool dependency, string? usedBy)
        {
            var key = ResourceTypes.KeyOf(type, name);
            if (seen.TryGetValue(key, out var at))
            {
                if (usedBy is not null && found[at].UsedBy.Count < 4 && !found[at].UsedBy.Contains(usedBy)) found[at].UsedBy.Add(usedBy);
                return;
            }

            seen[key] = found.Count;
            found.Add((type, name, dependency, ItemRefusal(source, target, type, name), usedBy is null ? [] : [usedBy]));
            queue.Enqueue(found.Count - 1);
        }

        foreach (var (type, name) in items) Add(type, name, false, null);
        while (queue.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (type, name, dependency, refusal, _) = found[queue.Dequeue()];
            if (refusal is not null || graph is null) continue;
            var key = ResourceTypes.KeyOf(type, name);
            var inTarget = Exists(target, type, name);
            // A dependency the target already has stays as it is, unless the user asks for it to be replaced.
            if (dependency && inTarget && (overrides is null || !overrides.TryGetValue(key, out var chosen) || chosen != CollisionMode.Replace)) continue;
            foreach (var use in graph.Uses(type, name))
            {
                if (!Supported.Contains(use.Type)) warnings.Add($"{name} uses {use.Name} ({use.Type}), which cannot be copied; it stays as it is in the target.");
                else Add(use.Type, use.Name, true, name);
            }
        }

        if (CopyVersion.Of(source).WarningFor(CopyVersion.Of(target)) is { } older) warnings.Add(older);
        return new CopyPlan(Decide(target, found, defaultMode, overrides), [.. warnings.Distinct()], null);
    }

    private static PlanEntry Refused(string type, string name, bool dependency, string reason) =>
        new(type, name, name, CopyAction.Refused, reason, new CopyFacts(dependency, false, false, []));

    private static List<PlanEntry> Decide(GameMakerData target, List<(string Type, string Name, bool Dependency, string? Refusal, List<string> UsedBy)> found,
        CollisionMode defaultMode, IReadOnlyDictionary<string, CollisionMode>? overrides)
    {
        // Names that stay as they are are taken first, so a copy never lands on the name of another item of the batch.
        var taken = new HashSet<string>(StringComparer.Ordinal);
        var modes = new Dictionary<string, CollisionMode>(StringComparer.Ordinal);
        foreach (var item in found)
        {
            var key = ResourceTypes.KeyOf(item.Type, item.Name);
            var mode = overrides is not null && overrides.TryGetValue(key, out var chosen) ? chosen : item.Dependency ? CollisionMode.Skip : defaultMode;
            var sameType = Exists(target, item.Type, item.Name);
            if (mode == CollisionMode.Replace && !sameType) mode = CollisionMode.Rename;
            modes[key] = mode;
            if (item.Refusal is null && (!NameTaken(target, item.Type, item.Name) || mode == CollisionMode.Replace)) taken.Add(item.Name);
        }

        var entries = new List<PlanEntry>();
        foreach (var item in found)
        {
            var collides = NameTaken(target, item.Type, item.Name);
            var sameType = Exists(target, item.Type, item.Name);
            var mode = modes[ResourceTypes.KeyOf(item.Type, item.Name)];
            if (item.Refusal is not null) entries.Add(Refused(item.Type, item.Name, item.Dependency, item.Refusal));
            else if (!collides) entries.Add(new PlanEntry(item.Type, item.Name, item.Name, CopyAction.Add, null, new CopyFacts(item.Dependency, false, false, item.UsedBy)));
            else if (mode == CollisionMode.Skip) entries.Add(new PlanEntry(item.Type, item.Name, item.Name, CopyAction.Skip, null, new CopyFacts(item.Dependency, true, sameType, item.UsedBy)));
            else if (mode == CollisionMode.Replace) entries.Add(new PlanEntry(item.Type, item.Name, item.Name, CopyAction.Replace, null, new CopyFacts(item.Dependency, true, true, item.UsedBy)));
            else entries.Add(new PlanEntry(item.Type, item.Name, FreeName(target, item.Type, item.Name, taken), CopyAction.Rename, null, new CopyFacts(item.Dependency, true, sameType, item.UsedBy)));
        }

        return entries;
    }

    private static string FreeName(GameMakerData target, string type, string name, HashSet<string> taken)
    {
        var copy = name + "_copy";
        for (var n = 2; NameTaken(target, type, copy) || taken.Contains(copy); n++) copy = $"{name}_copy{n}";
        taken.Add(copy);
        return copy;
    }

    /// <summary>True when the target has an item of this type with the name.</summary>
    public static bool Exists(GameMakerData data, string type, string name) =>
        type == "code" ? data.Code.ByName(name) is not null : ResourceTypes.All.FirstOrDefault(t => t.Key == type)?.Items(data)?.Any(i => ResourceTypes.NameOf(i) == name) == true;

    /// <summary>True when the target uses the name for any asset (GameMaker wants one name per asset) or, for code, for a code entry.</summary>
    public static bool NameTaken(GameMakerData data, string type, string name) =>
        type == "code" ? data.Code.ByName(name) is not null : ResourceListCommand.NameTaken(data, name);

    // Emitters are shared by name, so one that exists in the target under a system of another name would be overwritten.
    private static string? EmitterClash(GameMakerParticleSystem system, GameMakerData target)
    {
        if (target.ParticleSystems.Any(p => p.Name.Content == system.Name.Content)) return null;
        var taken = target.ParticleSystems.SelectMany(p => p.Emitters ?? []).Select(e => e.Resource?.Name?.Content).OfType<string>().ToHashSet(StringComparer.Ordinal);
        return (system.Emitters ?? []).Select(e => e.Resource?.Name?.Content).OfType<string>().FirstOrDefault(taken.Contains);
    }

    // The first code entry of the script (its own, then the function entries that are its children) that the target already has: compiling it would
    // replace the target's entry by a new one, and the target's global init list would still point at the old one.
    private static string? FunctionClash(GameMakerData source, GameMakerData target, GameMakerCode code)
    {
        if (code.Name?.Content is { } own && target.Code.ByName(own) is not null) return own;
        return source.Scripts.Where(w => ReferenceEquals(w.Code?.ParentEntry, code)).Select(w => w.Name?.Content)
            .FirstOrDefault(n => n is not null && (target.Scripts.ByName(n) is not null || target.Code.ByName(n) is not null));
    }

    /// <summary>Null when the item can be copied; else the reason it cannot.</summary>
    public static string? ItemRefusal(GameMakerData source, GameMakerData target, string type, string name)
    {
        if (!Supported.Contains(type)) return type == "extensions" ? "Extensions cannot be copied one by one: the library can only move all of them with their files." : $"A {type} entry cannot be copied.";
        if (!CopyPackage.IsSafeName(name)) return $"The name '{name}' cannot be used as a file name, so the item cannot be copied.";
        var list = ResourceTypes.All.FirstOrDefault(t => t.Key == type);
        if (type != "code" && list?.Items(target) is null) return $"The target data file has no {type} section.";
        var item = type == "code" ? source.Code.ByName(name) : list?.Items(source)?.FirstOrDefault(i => ResourceTypes.NameOf(i) == name);
        if (item is null) return $"'{name}' was not found in the source.";
        return item switch
        {
            GameMakerSound { GroupID: > 0 } => $"{name} is in an audio group file; its audio is not in the data file, so it cannot be copied.",
            GameMakerSound { AudioFile: null } => $"{name} has no embedded audio (it plays from a file next to the data file).",
            // A code entry or function entry of the same name in the target would be replaced or doubled by the compile.
            GameMakerScript { Code: { } code } when !Exists(target, "scripts", name) && FunctionClash(source, target, code) is { } clash =>
                $"{name} has the code entry {clash}, which the target already has under another script.",
            GameMakerScript { Code.ParentEntry: not null } => $"The code of {name} is part of a larger code entry, so it cannot be copied on its own.",
            GameMakerParticleSystem system when EmitterClash(system, target) is { } clash => $"{name} has the emitter '{clash}', which the target already has in another particle system.",
            GameMakerCode { ParentEntry: not null } => $"{name} is part of a larger code entry, so it cannot be copied on its own.",
            _ => null,
        };
    }
}
