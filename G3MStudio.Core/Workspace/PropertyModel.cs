using System.Collections;
using System.Globalization;
using System.Reflection;
using G3MLib.DataFile;
using G3MLib.DataFile.Models;

namespace G3MStudio.Core.Workspace;

public enum PropertyKind { Text, Bool, Whole, Real, Enum, GmString, Group, List, Info, Flags, Color }

/// <summary>One line of the property editor. <see cref="Path"/> addresses it from the resource (<c>Frames[2].Width</c>); a read-only row has no setter or is an information line.</summary>
public sealed record PropertyRow(string Path, string Name, PropertyKind Kind, string Value, int Depth, bool ReadOnly)
{
    /// <summary>On a choice or flags row: the names to pick from.</summary>
    public IReadOnlyList<string>? Choices { get; init; }

    /// <summary>Explorer key of the entry a reference row points at (<c>sprites:spr_player</c>), so the editor can open it.</summary>
    public string? Target { get; init; }

    /// <summary>On a list of strings: the list can take a new entry (the add row of UTMT-CTRL-06).</summary>
    public bool Addable { get; init; }

    /// <summary>On a list or collection: its entry count. The value text is the bare number, the grid adds the localized label.</summary>
    public int? Count { get; init; }

    /// <summary>On an entry of an editable list: it can be removed.</summary>
    public bool Removable { get; init; }

    /// <summary>On an entry of an editable list: it has a neighbour above or below to swap places with.</summary>
    public bool CanMoveUp { get; init; }

    public bool CanMoveDown { get; init; }

    /// <summary>On a reference to a resource or code entry: it can be set by text, the Explorer key of the target (empty clears it). The grid takes a dropped Explorer item and has a clear button.</summary>
    public bool Reference { get; init; }
}

/// <summary>
/// Describes and edits the properties of a G3MLib model object by reflection, so one editor covers every resource type.
/// Strings of the string table are never changed in place (other entries may share them): setting one picks or adds the table entry.
/// </summary>
public static class PropertyModel
{
    public const int MaxDepth = 3, MaxListItems = 50;

    private static readonly HashSet<Type> Numbers = [typeof(byte), typeof(sbyte), typeof(short), typeof(ushort), typeof(int), typeof(uint), typeof(long), typeof(ulong)];

    /// <summary>Members the library adds for its project asset interface; they repeat the name and type and are not data file fields.</summary>
    private static readonly HashSet<string> InternalMembers = ["ProjectName", "ProjectAssetType", "ProjectExportable", "OriginXWrapper", "OriginYWrapper"]; // the wrappers only mirror OriginX and OriginY for the sequence flag

    private static readonly HashSet<Type> Reals = [typeof(float), typeof(double), typeof(decimal)];

    /// <summary>The object a resource type key and name stand for: a resource, or the singletons <c>general:info</c> and <c>general:options</c>.</summary>
    public static object? FindRoot(GameMakerData data, string type, string name) => ResourceTypes.Find(data, ResourceTypes.KeyOf(type, name));

    public static IReadOnlyList<PropertyRow> Describe(object root)
    {
        var rows = new List<PropertyRow>();
        Walk(root, "", 0, rows, new HashSet<object>(ReferenceEqualityComparer.Instance) { root });
        return rows;
    }

    private static void Walk(object owner, string prefix, int depth, List<PropertyRow> rows, HashSet<object> seen)
    {
        foreach (var property in owner.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance).OrderBy(p => p.MetadataToken))
        {
            if (!property.CanRead || property.GetIndexParameters().Length > 0 || InternalMembers.Contains(property.Name)) continue;
            object? value;
            try { value = property.GetValue(owner); }
            catch (Exception e) when (e is TargetInvocationException or InvalidOperationException or NotSupportedException) { continue; }
            var path = prefix + property.Name;
            var settable = property.SetMethod is { IsPublic: true } && !(property.Name == "Name" && owner is GameMakerNamedResource); // a rename must also update what refers to the name
            var type = property.PropertyType;
            if (NamedValues(owner, property) is { } names)
            {
                rows.Add(new PropertyRow(path, property.Name, PropertyKind.Enum, Show(names, value), depth, !settable) { Choices = names });
            }
            else if (KindOf(type) is { } kind)
            {
                if (kind == PropertyKind.Whole && type == typeof(uint) && IsColorName(property.Name)) kind = PropertyKind.Color;
                rows.Add(new PropertyRow(path, property.Name, kind, Format(value), depth, !settable) { Choices = kind == PropertyKind.Enum ? Enum.GetNames(Nullable.GetUnderlyingType(type) ?? type) : kind == PropertyKind.Flags ? FlagNames(Nullable.GetUnderlyingType(type) ?? type) : null });
            }
            else if (value is null && IsReferenceType(type))
            {
                rows.Add(new PropertyRow(path, property.Name, PropertyKind.Info, "", depth, true) { Reference = settable });
            }
            else if (value is null)
            {
                rows.Add(new PropertyRow(path, property.Name, PropertyKind.Info, "", depth, true));
            }
            else if (value is GameMakerNamedResource or GameMakerCode)
            {
                // A reference to another entry (an object's sprite, a script's code): shown by name, edited in its own editor.
                rows.Add(new PropertyRow(path, property.Name, PropertyKind.Info, ResourceTypes.NameOf(value), depth, true) { Reference = settable, Target = UsageSearch.TypeKeyOf(value) is { } targetType ? ResourceTypes.KeyOf(targetType, ResourceTypes.NameOf(value)) : null });
            }
            else if (value is byte[] bytes)
            {
                rows.Add(new PropertyRow(path, property.Name, PropertyKind.Info, $"{bytes.Length} bytes", depth, true));
            }
            else if (value is IList { Count: 0 } empty && !CanAdd(empty, property.Name))
            {
                // An empty list has nothing to show or edit, so it gets no row.
            }
            else if (value is IList list)
            {
                // An editable list takes new entries and removes or moves its own; a list with a dedicated editor (frames, events, layers) is only shown.
                var strings = value is IList<GameMakerString>;
                var editable = IsEditable(list, property.Name);
                rows.Add(new PropertyRow(path, property.Name, PropertyKind.List, list.Count.ToString(System.Globalization.CultureInfo.InvariantCulture), depth, true) { Addable = CanAdd(list, property.Name), Count = list.Count });
                if (depth < MaxDepth)
                    for (var i = 0; i < Math.Min(list.Count, MaxListItems); i++)
                    {
                        var at = rows.Count;
                        if (strings) rows.Add(new PropertyRow($"{path}[{i}]", $"[{i}]", PropertyKind.GmString, Format(list[i]), depth + 1, false));
                        else Describe(list[i], $"{path}[{i}]", $"[{i}]", depth + 1, rows, seen, editable);
                        if (editable && rows.Count > at) rows[at] = rows[at] with { Removable = true, CanMoveUp = i > 0, CanMoveDown = i < list.Count - 1 };
                    }
            }
            else if (value is IEnumerable and not string)
            {
                rows.Add(new PropertyRow(path, property.Name, PropertyKind.Info, value is ICollection collection ? collection.Count.ToString(System.Globalization.CultureInfo.InvariantCulture) : type.Name, depth, true) { Count = (value as ICollection)?.Count });
            }
            else if (type.IsClass && !typeof(Delegate).IsAssignableFrom(type) && !typeof(Stream).IsAssignableFrom(type))
            {
                Describe(value, path, property.Name, depth, rows, seen);
            }
        }
    }

    private static void Describe(object? item, string path, string title, int depth, List<PropertyRow> rows, HashSet<object> seen, bool editable = false)
    {
        if (item is null) return;
        if (KindOf(item.GetType()) is { } simple)
        {
            var type = item.GetType();
            rows.Add(new PropertyRow(path, title, simple, Format(item), depth, !editable) { Choices = simple == PropertyKind.Enum ? Enum.GetNames(type) : simple == PropertyKind.Flags ? FlagNames(type) : null });
            return;
        }

        if (!seen.Add(item)) return;

        rows.Add(new PropertyRow(path, title, PropertyKind.Group, "", depth, true));
        if (depth < MaxDepth) Walk(item, path + ".", depth + 1, rows, seen);
    }

    /// <summary>The unsigned fields that hold a color as 0xAABBGGRR; the others named like a color (a depth) are plain numbers.</summary>
    public static bool IsColorName(string name) => name.EndsWith("Color", StringComparison.Ordinal) && name != "ColorDepth";

    /// <summary>The red, green and blue bytes of a GameMaker color, whose low byte is red.</summary>
    public static (byte R, byte G, byte B) Rgb(uint color) => ((byte)(color & 0xFF), (byte)((color >> 8) & 0xFF), (byte)((color >> 16) & 0xFF));

    /// <summary>The color with new red, green and blue bytes; the top byte (alpha or unused) stays as it was.</summary>
    public static uint WithRgb(uint color, byte r, byte g, byte b) => (color & 0xFF000000u) | r | ((uint)g << 8) | ((uint)b << 16);

    /// <summary>The names of the single flags of a flags enum (one bit each); combinations and zero are left out.</summary>
    public static string[] FlagNames(Type type) => [.. Enum.GetValues(type).Cast<object>().Where(v => Convert.ToUInt64(v, System.Globalization.CultureInfo.InvariantCulture) is var bits && bits != 0 && (bits & (bits - 1)) == 0).Select(v => v.ToString() ?? "")];

    /// <summary>The text of a set of flags for <paramref name="names"/> (the form Enum.Parse reads); "0" when none is set.</summary>
    public static string FlagsText(IEnumerable<string> names) => string.Join(", ", names) is { Length: > 0 } text ? text : "0";

    private static PropertyKind? KindOf(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (type == typeof(bool)) return PropertyKind.Bool;
        if (type == typeof(string)) return PropertyKind.Text;
        if (type == typeof(GameMakerString)) return PropertyKind.GmString;
        // A flags value is a set of names ("Regular, IsEmbedded"): a box of check boxes, one per single flag; the text form, which Enum.Parse reads back, is what is committed.
        if (type.IsEnum) return type.IsDefined(typeof(FlagsAttribute), false) ? PropertyKind.Flags : PropertyKind.Enum;
        if (Numbers.Contains(type)) return PropertyKind.Whole;
        if (Reals.Contains(type)) return PropertyKind.Real;
        return null;
    }

    /// <summary>Whole-number fields that GameMaker shows as a list of choices, so they are edited by name.</summary>
    private static string[]? NamedValues(object owner, PropertyInfo property) => owner is GameMakerSprite && property.Name == nameof(GameMakerSprite.BBoxMode) ? ["Automatic", "FullImage", "Manual"] : null;

    private static string Show(string[] names, object? value) => uint.TryParse(Format(value), out var index) && index < names.Length ? names[index] : Format(value);

    private static string Format(object? value) => value switch
    {
        null => "",
        GameMakerString text => text.Content ?? "",
        IFormattable number => number.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? "",
    };

    /// <summary>Walks a path such as <c>Frames[2].Width</c> to the object that owns the last property.</summary>
    private static (object Owner, PropertyInfo Property) Resolve(object root, string path)
    {
        var current = root;
        var segments = path.Split('.');
        for (var s = 0; s < segments.Length; s++)
        {
            var segment = segments[s];
            var open = segment.IndexOf('[', StringComparison.Ordinal);
            var name = open < 0 ? segment : segment[..open];
            var property = current.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance) ?? throw new InvalidOperationException($"'{name}' was not found.");
            if (s == segments.Length - 1 && open < 0) return (current, property);
            current = property.GetValue(current) ?? throw new InvalidOperationException($"'{name}' is empty.");
            if (open >= 0) current = ((IList)current)[int.Parse(segment[(open + 1)..^1], CultureInfo.InvariantCulture)] ?? throw new InvalidOperationException($"'{segment}' is empty.");
        }

        throw new InvalidOperationException("The path does not end in a property.");
    }

    /// <summary>True for a path that ends in a list index (<c>Tags[2]</c>), with the list's own path and the index.</summary>
    private static bool IsEntry(string path, out string list, out int index)
    {
        (list, index) = ("", -1);
        var open = path.LastIndexOf('[');
        if (open <= 0 || !path.EndsWith(']')) return false;
        list = path[..open];
        return int.TryParse(path[(open + 1)..^1], NumberStyles.None, CultureInfo.InvariantCulture, out index);
    }

    /// <summary>Lists that have their own commands or hold ids other data points at; a generic add, remove or move would break those links.</summary>
    private static readonly HashSet<string> OwnedLists = ["Textures", "CollisionMasks", "Events", "Actions", "Layers", "GameObjects", "Tiles", "LegacyTiles", "Moments", "Tracks", "Emitters", "Glyphs", "Files", "Functions", "Arguments", "Options", "Instructions", "ChildEntries", "Locals", "Sequences", "Children", "RoomOrder", "Instances", "InstanceIDs", "TileData", "Sprites", "NineSlices", "ParticleSystems", "TextItems", "BroadcastMessages", "Channels", "Points", "FunctionIDs"];

    /// <summary>A list whose entries can be removed and moved: growable, and not one of <see cref="OwnedLists"/>. A string list is always editable.</summary>
    public static bool IsEditable(IList list, string name) => !list.IsFixedSize && !list.IsReadOnly && (list is IList<GameMakerString> || !OwnedLists.Contains(name));

    /// <summary>A list that can also take a new entry: the entry type has a blank form (a string, a number, a class with a plain constructor).</summary>
    public static bool CanAdd(IList list, string name) => IsEditable(list, name) && EntryType(list) is { } type && (type == typeof(GameMakerString) || type.IsValueType || (type is { IsClass: true, IsAbstract: false } && !typeof(GameMakerResource).IsAssignableFrom(type) && !typeof(GameMakerCode).IsAssignableFrom(type) && type.GetConstructor(Type.EmptyTypes) is not null));

    private static Type? EntryType(IList list) => list.GetType().GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IList<>))?.GenericTypeArguments[0];

    /// <summary>A blank entry for the end of <paramref name="list"/>: an empty table string, zero, or a new object whose string fields are empty table strings.</summary>
    public static object NewEntry(GameMakerData data, IList list, string name)
    {
        if (!CanAdd(list, name) || EntryType(list) is not { } type) throw new InvalidOperationException($"'{name}' cannot take a new entry.");
        if (type == typeof(GameMakerString)) return data.Strings.MakeString("", out _);
        var entry = Activator.CreateInstance(type) ?? throw new InvalidOperationException($"'{name}' cannot take a new entry.");
        foreach (var p in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            if (p.PropertyType == typeof(GameMakerString) && p is { CanRead: true, SetMethod.IsPublic: true } && p.GetValue(entry) is null) p.SetValue(entry, data.Strings.MakeString("", out _));
        return entry;
    }

    private static bool IsReferenceType(Type type) => typeof(GameMakerNamedResource).IsAssignableFrom(type) || typeof(GameMakerCode).IsAssignableFrom(type);

    private static object Parse(GameMakerData data, Type type, string text)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        return type switch
        {
            _ when type == typeof(string) => text,
            _ when type == typeof(GameMakerString) => data.Strings.MakeString(text, out _),
            _ when type == typeof(bool) => bool.Parse(text),
            _ when type.IsEnum => Enum.Parse(type, text),
            _ => Convert.ChangeType(text, type, CultureInfo.InvariantCulture),
        };
    }

    private static IList EntriesOf(object root, string path) => ReadRaw(root, path) as IList ?? throw new InvalidOperationException($"'{path}' is not a list.");

    /// <summary>The value as text, for the old value of an edit.</summary>
    public static string Read(object root, string path)
    {
        if (IsEntry(path, out var entries, out var at)) return Format(EntriesOf(root, entries)[at]);
        var (owner, property) = Resolve(root, path);
        var value = property.GetValue(owner);
        return NamedValues(owner, property) is { } names ? Show(names, value) : Format(value);
    }

    /// <summary>The value as the object itself, so undo puts back the very same table entry.</summary>
    public static object? ReadRaw(object root, string path)
    {
        if (IsEntry(path, out var entries, out var at)) return EntriesOf(root, entries)[at];
        var (owner, property) = Resolve(root, path);
        return property.GetValue(owner);
    }

    public static void WriteRaw(object root, string path, object? value)
    {
        if (IsEntry(path, out var entries, out var at))
        {
            EntriesOf(root, entries)[at] = value;
            return;
        }

        var (owner, property) = Resolve(root, path);
        property.SetValue(owner, value);
    }

    /// <summary>Sets a property from text. Throws <see cref="FormatException"/> or <see cref="OverflowException"/> for text the type cannot hold.</summary>
    public static void Set(GameMakerData data, object root, string path, string text)
    {
        if (IsEntry(path, out var entries, out var at))
        {
            var list = EntriesOf(root, entries);
            list[at] = Parse(data, EntryType(list) ?? throw new InvalidOperationException($"'{path}' is not a plain value."), text);
            return;
        }

        var (owner, property) = Resolve(root, path);
        var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        if (IsReferenceType(type))
        {
            // The text is the Explorer key of the target; the target must be of the type the field holds.
            var target = text.Length == 0 ? null : ResourceTypes.Find(data, text) ?? throw new ArgumentException($"'{text}' was not found.");
            if (target is not null && !type.IsInstanceOfType(target)) throw new ArgumentException($"'{text}' is not a {type.Name}.");
            property.SetValue(owner, target);
            return;
        }

        if (NamedValues(owner, property) is { } names && Array.IndexOf(names, text) is var named and >= 0) text = named.ToString(CultureInfo.InvariantCulture);
        property.SetValue(owner, Parse(data, type, text));
    }
}
