using G3MLib.DataFile;
using G3MLib.DataFile.References;

namespace G3MStudio.Core.Workspace;

/// <summary>
/// Copies a resource under a new name inside the data file with the library clone: the copy owns its events, layers, frames and code entries. Undo removes it again.
/// The copy shares the texture page items of the source until <see cref="UnsharePicturesCommand"/> gives it pixels of its own.
/// </summary>
public sealed class CloneResourceCommand(string type, string source, string copy) : EditCommand
{
    public const string Name = "cloneResource";

    private ResourceClone? _clone;

    public override string Kind => Name;

    public override string Editor => type;

    public override IReadOnlyDictionary<string, string> Args { get; } = new Dictionary<string, string> { ["type"] = type, ["source"] = source, ["copy"] = copy };

    public override IReadOnlyList<ProjectTouch>? Touches => null; // the copy adds code entries, locals, strings and instance ids besides the resource

    public static CloneResourceCommand FromArgs(IReadOnlyDictionary<string, string> a) => new(a["type"], a["source"], a["copy"]);

    public override void Apply(GameMakerData data)
    {
        var from = ResourceTypes.Find(data, ResourceTypes.KeyOf(type, source)) as GameMakerNamedResource ?? throw new InvalidOperationException($"'{source}' was not found in {type}.");
        try
        {
            _clone = ResourceCloneService.Clone(data, from, copy);
        }
        catch (Exception e) when (e is RenameException or NotSupportedException)
        {
            throw new InvalidOperationException(e.Message, e);
        }
    }

    public override void Revert(GameMakerData data)
    {
        _clone?.Undo();
        _clone = null;
    }
}

/// <summary>
/// Builds the one undoable edit that copies a resource under a new name: the library clone, then new texture page items with a copy of the pixels for a sprite, font or
/// tile set, so editing the copy never changes the original. The other links of the copy (sprite, parent, texture group) point at the same assets as the original.
/// </summary>
public static class ResourceDuplicate
{
    /// <summary>Every type that can be duplicated.</summary>
    public static readonly string[] All = ["objects", "scripts", "sprites", "tileSets", "sounds", "paths", "shaders", "fonts", "timelines", "rooms", "sequences", "animationCurves", "particleSystems"];

    /// <summary>Refuses a taken name.</summary>
    public static void CheckCopyFree(GameMakerData data, string type, string copy)
    {
        if (CopyPlanner.NameTaken(data, type, copy)) throw new InvalidOperationException($"A resource or code entry named '{copy}' exists already. Pick another name.");
    }

    /// <summary>Refuses a name that cannot be a file name, and an unusual one unless <paramref name="force"/>: the same rule as a new resource.</summary>
    public static void CheckCopyName(string copy, bool force = false) => ResourceListCommand.CheckPlainName(copy, force);

    public static EditCommand Build(string type, string source, string copy)
    {
        if (!All.Contains(type)) throw new InvalidOperationException($"A {type} entry cannot be duplicated.");
        return type is "sprites" or "fonts" or "tileSets" ? new GroupCommand([new CloneResourceCommand(type, source, copy), new UnsharePicturesCommand(type, copy)]) : new CloneResourceCommand(type, source, copy);
    }
}
