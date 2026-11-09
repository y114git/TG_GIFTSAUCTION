using G3MLib.DataFile.Models;
using G3MLib.DataFile.References;

namespace G3MStudio.Core.Workspace;

/// <param name="OwnerType">Explorer type key of the entry that holds the reference, or null for a section of the file.</param>
/// <param name="OwnerName">Name of that entry.</param>
/// <param name="Where">How it is held: a property, or the kind of use in code with its instruction offset.</param>
public sealed record UsageHit(string? OwnerType, string OwnerName, string Where);

/// <summary>Find usages of a resource (design/shell.md, Edit menu): every place that holds a reference to it, from the reverse reference index of the library.</summary>
public static class UsageSearch
{
    public static async Task<IReadOnlyList<UsageHit>> FindAsync(Workspace workspace, string type, string name, CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            using (workspace.Lock.Read())
            {
                var target = ResourceTypes.Find(workspace.Data, ResourceTypes.KeyOf(type, name)) ?? throw new InvalidOperationException($"'{name}' was not found in {type}.");
                var index = ReferenceCache.Of(workspace.Data).Get(workspace.Data, cancellationToken);
                return (IReadOnlyList<UsageHit>)[.. index.FindUsages(target)
                    .Select(r => new UsageHit(TypeKeyOf(r.Owner), OwnerNameOf(r),  r.InstructionOffset >= 0 ? $"{r.Member} @{r.InstructionOffset}" : r.Member))
                    .OrderBy(h => h.OwnerType, StringComparer.Ordinal).ThenBy(h => h.OwnerName, StringComparer.Ordinal).ThenBy(h => h.Where, StringComparer.Ordinal)];
            }
        }, cancellationToken);
    }

    /// <summary>The types Find Unreferenced Assets looks through: the assets a game makes, not the sections of the file.</summary>
    public static readonly string[] AssetTypes = ["sprites", "tileSets", "sounds", "paths", "scripts", "shaders", "fonts", "timelines", "objects", "rooms", "sequences", "animationCurves", "particleSystems"];

    /// <summary>Every asset of the types that no entry of the file refers to (UTMT-SRCH-04), from one reverse reference index. A room or an object the game starts with by position still counts as unreferenced.</summary>
    public static async Task<IReadOnlyList<UsageHit>> FindUnreferencedAsync(Workspace workspace, CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            using (workspace.Lock.Read())
            {
                var index = ReferenceCache.Of(workspace.Data).Get(workspace.Data, cancellationToken);
                var found = new List<UsageHit>();
                foreach (var type in ResourceTypes.All.Where(t => AssetTypes.Contains(t.Key)))
                    foreach (var item in type.Items(workspace.Data) ?? [])
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (index.FindUsages(item).Count == 0) found.Add(new UsageHit(type.Key, ResourceTypes.NameOf(item), string.Empty));
                    }

                return (IReadOnlyList<UsageHit>)found;
            }
        }, cancellationToken);
    }

    /// <summary>True when the owner of a reference is the resource itself or one of its code entries (an object's event code, a script's code, a room's creation code).</summary>
    public static bool OwnedBy(object owner, object resource, string type, string name)
    {
        if (ReferenceEquals(owner, resource)) return true;
        if (owner is not GameMakerCode code) return false;
        var entry = (code.ParentEntry ?? code).Name?.Content ?? "";
        return type switch
        {
            "objects" => entry.StartsWith($"gml_Object_{name}_", StringComparison.Ordinal),
            "scripts" => entry == "gml_Script_" + name,
            "rooms" => entry.StartsWith($"gml_Room_{name}_", StringComparison.Ordinal) || entry.StartsWith($"gml_RoomCC_{name}_", StringComparison.Ordinal),
            "timelines" => entry.StartsWith($"gml_Timeline_{name}_", StringComparison.Ordinal),
            _ => false,
        };
    }

    /// <summary>What the resource uses: the assets that its own properties and its code refer to, one hit per asset (OwnerType and OwnerName name the asset, so a double click opens it; Where is the first way it is used).</summary>
    public static async Task<IReadOnlyList<UsageHit>> FindUsesAsync(Workspace workspace, string type, string name, CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            using (workspace.Lock.Read())
            {
                var resource = ResourceTypes.Find(workspace.Data, ResourceTypes.KeyOf(type, name)) ?? throw new InvalidOperationException($"'{name}' was not found in {type}.");
                var index = ReferenceCache.Of(workspace.Data).Get(workspace.Data, cancellationToken);
                var found = new List<UsageHit>();
                foreach (var other in ResourceTypes.All.Where(t => AssetTypes.Contains(t.Key)))
                {
                    foreach (var item in other.Items(workspace.Data) ?? [])
                    {
                        if (ReferenceEquals(item, resource) || ResourceTypes.NameOf(item) is not { } itemName) continue;
                        var use = index.FindUsages(item).FirstOrDefault(r => OwnedBy(r.Owner, resource, type, name));
                        if (use.Owner is not null) found.Add(new UsageHit(other.Key, itemName, use.Member));
                    }
                }

                return (IReadOnlyList<UsageHit>)[.. found.OrderBy(h => h.OwnerType, StringComparer.Ordinal).ThenBy(h => h.OwnerName, StringComparer.Ordinal)];
            }
        }, cancellationToken);
    }

    /// <summary>The objects whose parent is the object <paramref name="name"/> (UTMT-SRCH-06).</summary>
    public static async Task<IReadOnlyList<UsageHit>> FindChildrenAsync(Workspace workspace, string name, CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            using (workspace.Lock.Read())
                return (IReadOnlyList<UsageHit>)[.. workspace.Data.GameObjects.Where(o => o.ParentId?.Name?.Content == name).Select(o => new UsageHit("objects", ResourceTypes.NameOf(o), string.Empty))];
        }, cancellationToken);
    }

    /// <summary>The tile layers of rooms that place tile <paramref name="index"/> of the tile set <paramref name="set"/> (UTMT-SRCH-07); the low 19 bits of a tile cell are its index, the rest are flip and rotate flags.</summary>
    public static async Task<IReadOnlyList<UsageHit>> FindTileAsync(Workspace workspace, string set, int index, CancellationToken cancellationToken = default)
    {
        // Index 0 of a tile set is the empty tile, so no layer uses it.
        if (index <= 0) return [];
        return await Task.Run(() =>
        {
            using (workspace.Lock.Read())
                return (IReadOnlyList<UsageHit>)[.. workspace.Data.Rooms.SelectMany(r => (r.Layers ?? []).Where(l => l.TilesData is { Background: { } b } t && b.Name.Content == set && t.TileData.Any(row => row.Any(c => (c & 0x7FFFF) == index)))
                    .Select(l => new UsageHit("rooms", ResourceTypes.NameOf(r), l.LayerName?.Content ?? string.Empty)))];
        }, cancellationToken);
    }

    /// <summary>How many places hold a reference to the object itself (for a string table entry, its number of uses).</summary>
    public static async Task<int> CountAsync(Workspace workspace, object target, CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            using (workspace.Lock.Read()) return ReferenceCache.Of(workspace.Data).Get(workspace.Data, cancellationToken).FindUsages(target).Count;
        }, cancellationToken);
    }

    private static string OwnerNameOf(Reference reference) => reference.Owner switch
    {
        GameMakerGeneralInfo => ResourceTypes.GeneralInfoName,
        GameMakerOptions => ResourceTypes.GameOptionsName,
        GameMakerTags => ResourceTypes.TagsName,
        GameMakerFeatureFlags => ResourceTypes.FeatureFlagsName,
        GameMakerLanguage => ResourceTypes.LanguageName,
        _ => reference.OwnerName,
    };

    /// <summary>The Explorer type of an entry, so a usage can be opened.</summary>
    public static string? TypeKeyOf(object owner) => owner switch
    {
        GameMakerSprite => "sprites",
        GameMakerBackground => "tileSets",
        GameMakerSound => "sounds",
        GameMakerPath => "paths",
        GameMakerScript => "scripts",
        GameMakerShader => "shaders",
        GameMakerFont => "fonts",
        GameMakerTimeline => "timelines",
        GameMakerGameObject => "objects",
        GameMakerRoom => "rooms",
        GameMakerExtension => "extensions",
        GameMakerSequence => "sequences",
        GameMakerAnimationCurve => "animationCurves",
        GameMakerParticleSystem => "particleSystems",
        GameMakerCode => "code",
        GameMakerGeneralInfo or GameMakerOptions => "general",
        _ => null,
    };
}
