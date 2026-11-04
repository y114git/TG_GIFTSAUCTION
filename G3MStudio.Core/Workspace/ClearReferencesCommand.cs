using G3MLib.DataFile;

namespace G3MStudio.Core.Workspace;

/// <summary>
/// Sets every property that points at a resource (an object's sprite, a parent, a room's background) to none and takes it out of every list that holds it (a texture
/// group, the room order), so the resource can be deleted. A use in code cannot be cleared safely: the command refuses and names the place. Undo puts everything back.
/// </summary>
public sealed class ClearReferencesCommand(string type, string name) : EditCommand
{
    public const string Name = "clearReferences";

    private readonly List<(object Holder, System.Reflection.PropertyInfo Property, object? Value)> _cleared = [];
    private readonly List<(System.Collections.IList List, int At, object? Item)> _removed = [];

    public override string Kind => Name;

    public override string Editor => type;

    public override IReadOnlyDictionary<string, string> Args { get; } = new Dictionary<string, string> { ["type"] = type, ["name"] = name };

    public override IReadOnlyList<ProjectTouch>? Touches => null; // any resource may hold one of the cleared references

    public static ClearReferencesCommand FromArgs(IReadOnlyDictionary<string, string> a) => new(a["type"], a["name"]);

    public override void Apply(GameMakerData data)
    {
        var target = PropertyModel.FindRoot(data, type, name) ?? throw new InvalidOperationException($"'{name}' was not found in {type}.");
        var uses = ReferenceCache.Of(data).Get(data).FindUsages(target);
        var inCode = uses.Where(u => u.InstructionOffset >= 0).ToList();
        if (inCode.Count > 0) throw new InvalidOperationException($"'{name}' is used in code ({inCode.Count} places, first in {inCode[0].OwnerName}): change that code first, then delete it.");
        _cleared.Clear();
        _removed.Clear();
        try
        {
            foreach (var use in uses)
            {
                var property = use.Holder.GetType().GetProperty(use.Member) ?? throw new InvalidOperationException($"{use.OwnerName}.{use.Member} cannot be cleared.");
                var current = property.GetValue(use.Holder);
                if (current is System.Collections.IList list)
                {
                    var at = IndexOf(list, target);
                    if (at < 0) continue; // an earlier use of the same list already took it out
                    _removed.Add((list, at, list[at]));
                    list.RemoveAt(at);
                    continue;
                }

                _cleared.Add((use.Holder, property, current));
                property.SetValue(use.Holder, null);
            }
        }
        catch (Exception e) when (e is InvalidOperationException or ArgumentException or System.Reflection.TargetInvocationException or NullReferenceException)
        {
            Revert(data);
            throw new InvalidOperationException($"A reference to '{name}' could not be cleared: {e.Message}", e);
        }
    }

    /// <summary>The place of the target in a list of resources or of reference wrappers (an entry with a Resource property).</summary>
    private static int IndexOf(System.Collections.IList list, object target)
    {
        for (var i = 0; i < list.Count; i++)
            if (ReferenceEquals(list[i], target) || (list[i]?.GetType().GetProperty("Resource")?.GetValue(list[i]) is { } inner && ReferenceEquals(inner, target))) return i;
        return -1;
    }

    public override void Revert(GameMakerData data)
    {
        foreach (var (list, at, item) in Enumerable.Reverse(_removed)) list.Insert(at, item);
        _removed.Clear();
        foreach (var (holder, property, value) in Enumerable.Reverse(_cleared)) property.SetValue(holder, value);
        _cleared.Clear();
    }
}
