using G3MLib.DataFile;

namespace G3MStudio.Core.Workspace;

/// <summary>
/// A refusal the owner may overrule: the window asks with the texts under <paramref name="titleKey"/> and <paramref name="bodyKey"/> ({reason} is the message)
/// and runs the edit again with its force flag set.
/// </summary>
public class RiskyRefusalException(string titleKey, string bodyKey, string message) : InvalidOperationException(message)
{
    public string TitleKey { get; } = titleKey;

    public string BodyKey { get; } = bodyKey;
}

/// <summary>A delete is refused because code uses the resource; the owner may delete anyway.</summary>
public sealed class CodeUseException(string message) : RiskyRefusalException("explorer.deleteCodeTitle", "explorer.deleteCode", message);

/// <summary>
/// Sets every property that points at a resource (an object's sprite, a parent, a room's background) to none and takes it out of every list that holds it (a texture
/// group, the room order), so the resource can be deleted. A use in code cannot be cleared safely: the command refuses with <see cref="CodeUseException"/> and names the place,
/// unless <paramref name="keepCodeUses"/> is set (the owner chose to delete anyway; the code then keeps its stale reference). Undo puts everything back.
/// </summary>
public sealed class ClearReferencesCommand(string type, string name, bool keepCodeUses = false) : EditCommand
{
    public const string Name = "clearReferences";

    private G3MLib.DataFile.References.ClearedReferences? _cleared;

    public override string Kind => Name;

    public override string Editor => type;

    public override IReadOnlyDictionary<string, string> Args { get; } = BuildArgs(type, name, keepCodeUses);

    private static Dictionary<string, string> BuildArgs(string type, string name, bool keepCode)
    {
        var args = new Dictionary<string, string> { ["type"] = type, ["name"] = name };
        if (keepCode) args["keepCode"] = "1";
        return args;
    }

    public override IReadOnlyList<ProjectTouch>? Touches => null; // any resource may hold one of the cleared references

    public static ClearReferencesCommand FromArgs(IReadOnlyDictionary<string, string> a) => new(a["type"], a["name"], a.ContainsKey("keepCode"));

    public override void Apply(GameMakerData data)
    {
        var target = PropertyModel.FindRoot(data, type, name) ?? throw new InvalidOperationException($"'{name}' was not found in {type}.");
        var index = ReferenceCache.Of(data).Get(data);
        var inCode = index.FindUsages(target).Where(u => u.InstructionOffset >= 0).ToList();
        if (!keepCodeUses && inCode.Count > 0) throw new CodeUseException($"'{name}' is used in code ({inCode.Count} places, first in {inCode[0].OwnerName}): change that code first, or allow risky actions in Settings > Advanced to delete it anyway.");
        _cleared = index.ClearUsages(target);
    }

    public override void Revert(GameMakerData data)
    {
        if (_cleared is not { } cleared) return;
        cleared.Undo();
        ReferenceCache.Of(data).Get(data).Update(cleared.ChangedOwners);
        _cleared = null;
    }
}
