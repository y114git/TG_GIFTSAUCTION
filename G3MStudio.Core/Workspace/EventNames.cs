using G3MLib.DataFile;
using G3MLib.DataFile.Models;
using G3MStudio.Core.Localization;

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
    public static IReadOnlyList<(string Label, uint Value)> Subtypes(EventType type, GameMakerData? data, Localizer? loc = null) => type switch
    {
        EventType.Alarm => [.. Enumerable.Range(0, 12).Select(i => (Alarm(i, loc), (uint)i))],
        EventType.Step => Of<EventSubtypeStep>(loc),
        EventType.Draw => Of<EventSubtypeDraw>(loc),
        EventType.Mouse => Of<EventSubtypeMouse>(loc),
        EventType.Keyboard or EventType.KeyPress or EventType.KeyRelease => Of<EventSubtypeKey>(loc),
        EventType.Other => Of<EventSubtypeOther>(loc),
        EventType.Gesture => Of<EventSubtypeGesture>(loc),
        EventType.Collision => data is null ? [] : [.. data.GameObjects.Select((o, i) => (o.Name.Content ?? i.ToString(System.Globalization.CultureInfo.InvariantCulture), (uint)i))],
        _ => [("", 0u)],
    };

    /// <summary>Whether the type has no choice of subtype.</summary>
    public static bool HasSubtype(EventType type) => type is not (EventType.Create or EventType.Destroy or EventType.CleanUp or EventType.PreCreate);

    /// <summary>How an event is named in lists: "Step: Begin Step", "Alarm 3", "Collision: obj_wall".</summary>
    public static string Label(uint type, uint subtype, GameMakerData? data, Localizer? loc = null)
    {
        var kind = (EventType)type;
        var title = TypeName(kind, loc);
        if (!HasSubtype(kind)) return title;
        if (data is null) return $"{title} {subtype}";
        var name = Subtypes(kind, data, loc).FirstOrDefault(s => s.Value == subtype).Label;
        return string.IsNullOrEmpty(name) ? $"{title} {subtype}" : kind is EventType.Alarm ? name : $"{title}: {name}";
    }

    /// <summary>The name of an event type in the language of <paramref name="loc"/> (<c>events.type.Name</c>), English when there is none.</summary>
    public static string TypeName(EventType type, Localizer? loc = null) => loc is not null && loc.Has("events.type." + type) ? loc["events.type." + type] : type.ToString();

    private static string Alarm(int index, Localizer? loc) => loc is null ? $"Alarm {index}" : loc.Format("events.alarm", ("n", index));

    // User0 and OutsideView3 are one text with a number (events.sub.User); every other name has its own key (events.sub.BeginStep) or reads as its words.
    private static string SubName(string name, Localizer? loc)
    {
        if (loc is null) return Spaced(name);
        if (Numbered().Match(name) is { Success: true } m) return loc.Format("events.sub." + m.Groups[1].Value, ("n", m.Groups[2].Value));
        return loc.Has("events.sub." + name) ? loc["events.sub." + name] : Spaced(name);
    }

    private static List<(string, uint)> Of<T>(Localizer? loc) where T : struct, Enum => [.. Enum.GetValues<T>().Select(v => (SubName(v.ToString(), loc), Convert.ToUInt32(v, System.Globalization.CultureInfo.InvariantCulture))).DistinctBy(x => x.Item2)];

    [System.Text.RegularExpressions.GeneratedRegex(@"^(User|OutsideView|BoundaryView)(\d+)$")]
    private static partial System.Text.RegularExpressions.Regex Numbered();

    /// <summary>The name of an enum value as words: "BeginStep" shows as "Begin Step".</summary>
    public static string Spaced(string name) => Words().Replace(name, " ");

    [System.Text.RegularExpressions.GeneratedRegex("(?<=[a-z0-9])(?=[A-Z])")]
    private static partial System.Text.RegularExpressions.Regex Words();
}
