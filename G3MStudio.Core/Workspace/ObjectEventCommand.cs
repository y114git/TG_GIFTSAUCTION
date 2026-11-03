using System.Globalization;
using G3MLib.DataFile;
using G3MLib.DataFile.Models;

namespace G3MStudio.Core.Workspace;

/// <summary>Adds an event with an empty code entry to an object, or removes an event with its code. Undo puts the lists back as they were.</summary>
public sealed class ObjectEventCommand : EditCommand
{
    public const string Name = "objectEvent";
    public const string Add = "add";
    public const string Remove = "remove";

    private readonly string _object, _operation;
    private readonly uint _type, _subtype;
    private List<GameMakerCode>? _code;
    private List<GameMakerCodeLocals>? _locals;
    private List<GameMakerGameObject.Event>? _events;
    private GameMakerGameObject? _owner;

    public ObjectEventCommand(string gameObject, string operation, uint type, uint subtype) => (_object, _operation, _type, _subtype) = (gameObject, operation, type, subtype);

    public override string Kind => Name;

    public override string Editor => "objects";

    public override IReadOnlyDictionary<string, string> Args => new Dictionary<string, string>
    {
        ["object"] = _object,
        ["operation"] = _operation,
        ["type"] = _type.ToString(CultureInfo.InvariantCulture),
        ["subtype"] = _subtype.ToString(CultureInfo.InvariantCulture),
    };

    public override IReadOnlyList<ProjectTouch>? Touches => [new("GameObjects", _object), new("CodeEntries", null), new("Strings", null)];

    public static ObjectEventCommand FromArgs(IReadOnlyDictionary<string, string> a) =>
        new(a["object"], a["operation"], uint.Parse(a["type"], CultureInfo.InvariantCulture), uint.Parse(a["subtype"], CultureInfo.InvariantCulture));

    /// <summary>The name GameMaker gives the code of an event.</summary>
    public static string CodeName(string gameObject, uint type, uint subtype) => $"gml_Object_{gameObject}_{(EventType)type}_{subtype}";

    public override void Apply(GameMakerData data)
    {
        var owner = data.GameObjects.FirstOrDefault(o => o.Name.Content == _object) ?? throw new InvalidOperationException($"Object '{_object}' does not exist.");
        if (_type >= owner.Events.Count) throw new InvalidOperationException($"Event type {_type} does not exist.");
        var events = owner.Events[(int)_type];
        var existing = events.FirstOrDefault(e => e.EventSubtype == _subtype);
        var (codeBefore, localsBefore, eventsBefore) = (new List<GameMakerCode>(data.Code), data.CodeLocals is { } l ? new List<GameMakerCodeLocals>(l) : null, new List<GameMakerGameObject.Event>(events));
        if (_operation == Add)
        {
            if (existing is not null) throw new InvalidOperationException($"{_object} already has that event.");
            owner.EventHandlerFor((EventType)_type, _subtype, data);
        }
        else if (_operation == Remove)
        {
            if (existing is null) throw new InvalidOperationException($"{_object} has no such event.");
            foreach (var code in existing.Actions.Select(a => a.CodeId).OfType<GameMakerCode>().ToList())
            {
                data.Code.Remove(code);
                if (data.CodeLocals is { } locals && locals.FirstOrDefault(x => x.Name == code.Name) is { } entry) locals.Remove(entry);
            }

            events.Remove(existing);
        }
        else
        {
            throw new InvalidDataException($"Unknown object event operation '{_operation}'.");
        }

        (_owner, _code, _locals, _events) = (owner, codeBefore, localsBefore, eventsBefore);
    }

    public override void Revert(GameMakerData data)
    {
        if (_owner is null || _code is null || _events is null) throw new InvalidOperationException("The edit was never applied.");
        Refill(data.Code, _code);
        if (_locals is not null && data.CodeLocals is { } locals) Refill(locals, _locals);
        Refill(_owner.Events[(int)_type], _events);
    }

    private static void Refill<T>(IList<T> list, List<T> items)
    {
        list.Clear();
        foreach (var item in items) list.Add(item);
    }
}
