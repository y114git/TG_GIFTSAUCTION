using System.Globalization;
using G3MLib.DataFile;
using G3MLib.DataFile.Models;

namespace G3MStudio.Core.Workspace;

/// <summary>
/// Copies the code of one event to an event of another object, or to another subtype of the same object (duplicate). The target event is created,
/// so it must not exist yet. One undo step removes it again.
/// </summary>
public sealed class ObjectEventCopyCommand(string from, uint type, uint subtype, string to, uint toSubtype) : EditCommand
{
    public const string Name = "objectEventCopy";

    private ObjectEventCommand? _add;
    private EditCodeCommand? _code;

    public override string Kind => Name;

    public override string Editor => "objects";

    public override IReadOnlyDictionary<string, string> Args { get; } = new Dictionary<string, string>
    {
        ["from"] = from,
        ["type"] = type.ToString(CultureInfo.InvariantCulture),
        ["subtype"] = subtype.ToString(CultureInfo.InvariantCulture),
        ["to"] = to,
        ["toSubtype"] = toSubtype.ToString(CultureInfo.InvariantCulture),
    };

    public override IReadOnlyList<ProjectTouch>? Touches { get; } = [new("GameObjects", to), new("CodeEntries", null), new("Strings", null), new("Variables", null), new("Functions", null)];

    public static ObjectEventCopyCommand FromArgs(IReadOnlyDictionary<string, string> a) =>
        new(a["from"], uint.Parse(a["type"], CultureInfo.InvariantCulture), uint.Parse(a["subtype"], CultureInfo.InvariantCulture), a["to"], uint.Parse(a["toSubtype"], CultureInfo.InvariantCulture));

    /// <summary>The first subtype of the type after <paramref name="subtype"/> (wrapping round) that the object has no event for, or null when all are taken.</summary>
    public static uint? FreeSubtype(GameMakerData data, GameMakerGameObject owner, EventType kind, uint subtype)
    {
        var all = EventNames.Subtypes(kind, data).Select(s => s.Value).ToList();
        var used = owner.Events[(int)kind].Select(e => e.EventSubtype).ToHashSet();
        var start = Math.Max(0, all.IndexOf(subtype));
        return Enumerable.Range(1, all.Count).Select(i => all[(start + i) % all.Count]).Where(v => !used.Contains(v)).Select(v => (uint?)v).FirstOrDefault();
    }

    public override void Apply(GameMakerData data)
    {
        var source = data.GameObjects.FirstOrDefault(o => o.Name.Content == from) ?? throw new InvalidOperationException($"Object '{from}' does not exist.");
        var entry = source.Events.ElementAtOrDefault((int)type)?.FirstOrDefault(e => e.EventSubtype == subtype)?.Actions.Select(a => a.CodeId?.Name?.Content).OfType<string>().FirstOrDefault()
            ?? throw new InvalidOperationException($"{from} has no such event.");
        var text = CodeText.Decompile(data, entry);
        var add = new ObjectEventCommand(to, ObjectEventCommand.Add, type, toSubtype);
        add.Apply(data);
        var code = new EditCodeCommand(ObjectEventCommand.CodeName(to, type, toSubtype), text);
        try
        {
            code.Apply(data);
        }
        catch
        {
            add.Revert(data);
            throw;
        }

        (_add, _code) = (add, code);
    }

    public override void Revert(GameMakerData data)
    {
        if (_add is null || _code is null) throw new InvalidOperationException("The edit was never applied.");
        _code.Revert(data);
        _add.Revert(data);
    }
}

/// <summary>Moves an event up or down inside the list of its type, which decides which of two events of a type is listed first in the file.</summary>
public sealed class ObjectEventOrderCommand(string gameObject, uint type, uint subtype, int delta) : EditCommand
{
    public const string Name = "objectEventOrder";

    private int _from = -1, _to = -1;

    public override string Kind => Name;

    public override string Editor => "objects";

    public override bool ChangesNames => false;

    public override IReadOnlyDictionary<string, string> Args { get; } = new Dictionary<string, string>
    {
        ["object"] = gameObject,
        ["type"] = type.ToString(CultureInfo.InvariantCulture),
        ["subtype"] = subtype.ToString(CultureInfo.InvariantCulture),
        ["delta"] = delta.ToString(CultureInfo.InvariantCulture),
    };

    public override IReadOnlyList<ProjectTouch>? Touches { get; } = [new("GameObjects", gameObject)];

    public static ObjectEventOrderCommand FromArgs(IReadOnlyDictionary<string, string> a) =>
        new(a["object"], uint.Parse(a["type"], CultureInfo.InvariantCulture), uint.Parse(a["subtype"], CultureInfo.InvariantCulture), int.Parse(a["delta"], CultureInfo.InvariantCulture));

    private GameMakerPointerList<GameMakerGameObject.Event> List(GameMakerData data) =>
        (data.GameObjects.FirstOrDefault(o => o.Name.Content == gameObject) ?? throw new InvalidOperationException($"Object '{gameObject}' does not exist.")).Events[(int)type];

    public override void Apply(GameMakerData data)
    {
        var list = List(data);
        _from = list.ToList().FindIndex(e => e.EventSubtype == subtype);
        if (_from < 0) throw new InvalidOperationException($"{gameObject} has no such event.");
        _to = Math.Clamp(_from + delta, 0, list.Count - 1);
        Move(list, _from, _to);
    }

    public override void Revert(GameMakerData data)
    {
        if (_from < 0) throw new InvalidOperationException("The edit was never applied.");
        Move(List(data), _to, _from);
    }

    private static void Move(GameMakerPointerList<GameMakerGameObject.Event> list, int from, int to)
    {
        var item = list[from];
        list.RemoveAt(from);
        list.Insert(to, item);
    }
}

/// <summary>Sets or clears the parent of an object. A parent that would make the object its own ancestor is refused.</summary>
public sealed class ObjectParentCommand(string gameObject, string parent) : EditCommand
{
    public const string Name = "objectParent";

    private GameMakerGameObject? _old;
    private bool _done;

    public override string Kind => Name;

    public override string Editor => "objects";

    public override bool ChangesNames => false;

    public override IReadOnlyDictionary<string, string> Args { get; } = new Dictionary<string, string> { ["object"] = gameObject, ["parent"] = parent };

    public override IReadOnlyList<ProjectTouch>? Touches { get; } = [new("GameObjects", gameObject)];

    public static ObjectParentCommand FromArgs(IReadOnlyDictionary<string, string> a) => new(a["object"], a["parent"]);

    /// <summary>True when <paramref name="candidate"/> is the object or one of its descendants, so it cannot be the object's parent.</summary>
    public static bool WouldLoop(GameMakerGameObject owner, GameMakerGameObject candidate)
    {
        for (var at = candidate; at is not null; at = at.ParentId)
            if (ReferenceEquals(at, owner)) return true;
        return false;
    }

    public override void Apply(GameMakerData data)
    {
        var owner = data.GameObjects.FirstOrDefault(o => o.Name.Content == gameObject) ?? throw new InvalidOperationException($"Object '{gameObject}' does not exist.");
        GameMakerGameObject? chosen = null;
        if (parent.Length > 0)
        {
            chosen = data.GameObjects.FirstOrDefault(o => o.Name.Content == parent) ?? throw new InvalidOperationException($"Object '{parent}' does not exist.");
            if (WouldLoop(owner, chosen)) throw new InvalidOperationException($"{parent} is {gameObject} or one of its children, so it cannot be the parent.");
        }

        (_old, _done) = (owner.ParentId, true);
        owner.ParentId = chosen;
    }

    public override void Revert(GameMakerData data)
    {
        if (!_done) throw new InvalidOperationException("The edit was never applied.");
        var owner = data.GameObjects.FirstOrDefault(o => o.Name.Content == gameObject) ?? throw new InvalidOperationException($"Object '{gameObject}' does not exist.");
        owner.ParentId = _old;
    }
}
