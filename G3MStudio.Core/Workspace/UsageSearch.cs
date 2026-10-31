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
                // The index is built per search (about 160 ms on a benchmark game) so it never goes stale after edits.
                var index = ReferenceIndex.Build(workspace.Data, cancellationToken);
                return (IReadOnlyList<UsageHit>)[.. index.FindUsages(target)
                    .Select(r => new UsageHit(TypeKeyOf(r.Owner), OwnerNameOf(r),  r.InstructionOffset >= 0 ? $"{r.Member} @{r.InstructionOffset}" : r.Member))
                    .OrderBy(h => h.OwnerType, StringComparer.Ordinal).ThenBy(h => h.OwnerName, StringComparer.Ordinal).ThenBy(h => h.Where, StringComparer.Ordinal)];
            }
        }, cancellationToken);
    }

    /// <summary>How many places hold a reference to the object itself (for a string table entry, its number of uses).</summary>
    public static async Task<int> CountAsync(Workspace workspace, object target, CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            using (workspace.Lock.Read()) return ReferenceIndex.Build(workspace.Data, cancellationToken).FindUsages(target).Count;
        }, cancellationToken);
    }

    private static string OwnerNameOf(Reference reference) => reference.Owner switch
    {
        GameMakerGeneralInfo => ResourceTypes.GeneralInfoName,
        GameMakerOptions => ResourceTypes.GameOptionsName,
        _ => reference.OwnerName,
    };

    /// <summary>The Explorer type of an entry, so a usage can be opened.</summary>
    internal static string? TypeKeyOf(object owner) => owner switch
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
