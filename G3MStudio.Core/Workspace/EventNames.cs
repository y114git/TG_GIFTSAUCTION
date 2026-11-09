using G3MLib.DataFile;
using G3MLib.DataFile.Models;

namespace G3MStudio.Core.Workspace;

/// <summary>The event types an object can have and the subtypes each type offers, with the names GameMaker shows.</summary>
public static partial class EventNames
{
    /// <summary>The types the Add event row offers, in the order GameMaker lists them.</summary>
    public static readonly EventType[] Types =
    [
        EventType.Create, EventType.Destroy, EventType.CleanUp, EventType.Alarm, EventType.Step, EventType.Collision, EventType.Mouse,
        EventType.Keyboard, EventType.KeyPress, EventType.KeyRelease, EventType.Other, EventType.Draw, EventType.Gesture, EventType.PreCreate,
    ];

    /// <summary>The subtypes of a type as (label, value). A collision lists the objects of the file, so it needs the data.</summary>
    public static IReadOnlyList<(string Label, uint Value)> Subtypes(EventType type, GameMakerData? data) => type switch
    {
        EventType.Alarm => [.. Enumerable.Range(0, 12).Select(i => ($"Alarm {i}", (uint)i))],
        EventType.Step => Of<EventSubtypeStep>(),
        EventType.Draw => Of<EventSubtypeDraw>(),
        EventType.Mouse => Of<EventSubtypeMouse>(),
        EventType.Keyboard or EventType.KeyPress or EventType.KeyRelease => Of<EventSubtypeKey>(),
        EventType.Other => Of<EventSubtypeOther>(),
        EventType.Gesture => Of<EventSubtypeGesture>(),
        EventType.Collision => data is null ? [] : [.. data.GameObjects.Select((o, i) => (o.Name.Content ?? i.ToString(System.Globalization.CultureInfo.InvariantCulture), (uint)i))],
        _ => [("", 0u)],
    };

    /// <summary>Whether the type has no choice of subtype.</summary>
    public static bool HasSubtype(EventType type) => type is not (EventType.Create or EventType.Destroy or EventType.CleanUp or EventType.PreCreate);

    /// <summary>How an event is named in lists: "Step: Begin Step", "Alarm 3", "Collision: obj_wall".</summary>
    public static string Label(uint type, uint subtype, GameMakerData? data)
    {
        var kind = (EventType)type;
        if (!HasSubtype(kind)) return kind.ToString();
        if (data is null) return $"{kind} {subtype}";
        var name = Subtypes(kind, data).FirstOrDefault(s => s.Value == subtype).Label;
        return string.IsNullOrEmpty(name) ? $"{kind} {subtype}" : kind is EventType.Alarm ? name : $"{kind}: {name}";
    }

    private static List<(string, uint)> Of<T>() where T : struct, Enum => [.. Enum.GetValues<T>().Select(v => (Spaced(v.ToString()), Convert.ToUInt32(v, System.Globalization.CultureInfo.InvariantCulture))).DistinctBy(x => x.Item2)];

    private static string Spaced(string name) => Words().Replace(name, " ");

    [System.Text.RegularExpressions.GeneratedRegex("(?<=[a-z0-9])(?=[A-Z])")]
    private static partial System.Text.RegularExpressions.Regex Words();
}
