using G3MLib.DataFile.Models;
using G3MLib.DataFile.References;

namespace G3MStudio.Core.Workspace;

/// <summary>The project resources a rename touches, found from what it changed.</summary>
public sealed partial class RenameResourceCommand
{
    /// <summary>
    /// The project resources a rename changes: whatever holds a changed name (the resource, its code entries) and whatever refers to the
    /// resource or to its name (rooms, objects, code). Each is listed under its new and its old name so the old folder is removed.
    /// Null (refresh everything) when an owner has no project type.
    /// </summary>
    private static IReadOnlyList<ProjectTouch>? ProjectTouches(RenameChangeSet changes, IEnumerable<Reference> resourceUses, ReferenceIndex index)
    {
        var owners = resourceUses.Concat(changes.Uses).Concat(changes.SelectMany(c => index.FindUsages(c.String))).Select(r => r.Owner)
            .Distinct(ReferenceEqualityComparer.Instance).ToList();
        var touches = new HashSet<ProjectTouch> { new("Strings", null) };
        foreach (var owner in owners)
        {
            var types = ProjectTypes(owner);
            if (types is null) return null;
            var now = ReferenceIndex.NameOf(owner);
            var names = new List<string?> { now };
            names.AddRange(changes.Where(c => c.NewValue == now).Select(c => c.OldValue));
            foreach (var type in types)
                foreach (var name in WholeFile.Contains(type) ? [null] : names)
                    touches.Add(new ProjectTouch(type, name));
        }

        return [.. touches];
    }

    // Types whose project files hold the whole list or section, so a name does not select anything.
    internal static readonly HashSet<string> WholeFile = ["Strings", "Functions", "Variables", "GeneralInfo", "Options"];

    internal static string[]? ProjectTypes(object owner) => owner switch
    {
        GameMakerSprite => ["Sprites"],
        GameMakerBackground => ["Backgrounds", "Tilesets"],
        GameMakerFont => ["Fonts"],
        GameMakerSound => ["Sounds"],
        GameMakerPath => ["Paths"],
        GameMakerShader => ["Shaders"],
        GameMakerTimeline => ["Timelines"],
        GameMakerGameObject => ["GameObjects"],
        GameMakerAnimationCurve => ["AnimationCurves"],
        GameMakerParticleSystem => ["ParticleSystems"],
        GameMakerParticleSystemEmitter => ["ParticleSystemEmitters"],
        GameMakerSequence => ["Sequences"],
        GameMakerRoom => ["Rooms"],
        GameMakerExtension => ["Extensions"],
        GameMakerScript => ["Scripts"],
        GameMakerCode or GameMakerCodeLocals => ["CodeEntries"],
        GameMakerFunction => ["Functions"],
        GameMakerVariable => ["Variables"],
        GameMakerString => ["Strings"],
        GameMakerGeneralInfo => ["GeneralInfo"],
        GameMakerOptions => ["Options"],
        _ => null,
    };
}
