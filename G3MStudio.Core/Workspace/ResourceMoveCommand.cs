using System.Collections;
using G3MLib.DataFile;

namespace G3MStudio.Core.Workspace;

/// <summary>
/// Moves a resource one place up or down in its list by swapping it with the neighbour (UTMT-TREE-08). Asset indexes in GML follow the list order, so this changes them;
/// references are by object, not by position, so nothing else needs to follow. Undo swaps them back.
/// </summary>
public sealed class ResourceMoveCommand(string type, string name, int by) : EditCommand
{
    public const string Name = "move";

    private int _from = -1, _to = -1;

    public override string Kind => Name;

    public override string Editor => "explorer";

    public override IReadOnlyDictionary<string, string> Args { get; } = new Dictionary<string, string> { ["type"] = type, ["name"] = name, ["by"] = by.ToString(System.Globalization.CultureInfo.InvariantCulture) };

    public static ResourceMoveCommand FromArgs(IReadOnlyDictionary<string, string> a) => new(a["type"], a["name"], int.Parse(a["by"], System.Globalization.CultureInfo.InvariantCulture));

    /// <summary>The list of a type that can be reordered, or null (data types and the Game group are not reordered).</summary>
    private static IList? ListOf(GameMakerData data, string type) =>
        ResourceTypes.All.FirstOrDefault(t => t.Key == type && !t.IsData && t.Key != "general") is { List: { } list } ? list(data) : null;

    /// <summary>The positions of the entry and of the entry it swaps with, or null at the end of the list (null entries of the list are stepped over).</summary>
    private static (int From, int To)? Places(IList list, string name, int by)
    {
        var from = -1;
        for (var i = 0; i < list.Count; i++)
            if (list[i] is { } item && ResourceTypes.NameOf(item) == name) from = i;
        if (from < 0 || by == 0) return null;
        for (var to = from + Math.Sign(by); to >= 0 && to < list.Count; to += Math.Sign(by))
            if (list[to] is not null) return (from, to);
        return null;
    }

    public static bool CanMove(GameMakerData data, string type, string name, int by) => ListOf(data, type) is { } list && Places(list, name, by) is not null;

    public override void Apply(GameMakerData data)
    {
        var list = ListOf(data, type) ?? throw new InvalidOperationException($"{type} entries cannot be reordered.");
        (_from, _to) = Places(list, name, by) ?? throw new InvalidOperationException($"'{name}' cannot move further {(by < 0 ? "up" : "down")}.");
        (list[_from], list[_to]) = (list[_to], list[_from]);
    }

    public override void Revert(GameMakerData data)
    {
        if (ListOf(data, type) is not { } list || _from < 0) return;
        (list[_from], list[_to]) = (list[_to], list[_from]);
    }
}
