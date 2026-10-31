using System.Collections;
using System.Globalization;
using System.Reflection;
using G3MLib.DataFile;
using G3MLib.DataFile.Models;

namespace G3MStudio.Core.Workspace;

public enum PropertyKind { Text, Bool, Whole, Real, Enum, GmString, Group, List, Info }

/// <summary>One line of the property editor. <see cref="Path"/> addresses it from the resource (<c>Frames[2].Width</c>); a read-only row has no setter or is an information line.</summary>
public sealed record PropertyRow(string Path, string Name, PropertyKind Kind, string Value, int Depth, bool ReadOnly, IReadOnlyList<string>? Choices = null)
{
    /// <summary>Explorer key of the entry a reference row points at (<c>sprites:spr_player</c>), so the editor can open it.</summary>
    public string? Target { get; init; }
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
                rows.Add(new PropertyRow(path, property.Name, PropertyKind.Enum, Show(names, value), depth, !settable, names));
            }
            else if (KindOf(type) is { } kind)
            {
                rows.Add(new PropertyRow(path, property.Name, kind, Format(value), depth, !settable, kind == PropertyKind.Enum ? Enum.GetNames(Nullable.GetUnderlyingType(type) ?? type) : null));
            }
            else if (value is null)
            {
                rows.Add(new PropertyRow(path, property.Name, PropertyKind.Info, "", depth, true));
            }
            else if (value is GameMakerNamedResource or GameMakerCode)
            {
                // A reference to another entry (an object's sprite, a script's code): shown by name, edited in its own editor.
                rows.Add(new PropertyRow(path, property.Name, PropertyKind.Info, ResourceTypes.NameOf(value), depth, true) { Target = UsageSearch.TypeKeyOf(value) is { } targetType ? ResourceTypes.KeyOf(targetType, ResourceTypes.NameOf(value)) : null });
            }
            else if (value is byte[] bytes)
            {
                rows.Add(new PropertyRow(path, property.Name, PropertyKind.Info, $"{bytes.Length} bytes", depth, true));
            }
            else if (value is IList { Count: 0 })
            {
                // An empty list has nothing to show or edit, so it gets no row.
            }
            else if (value is IList list)
            {
                rows.Add(new PropertyRow(path, property.Name, PropertyKind.List, $"{list.Count} items", depth, true));
                if (depth < MaxDepth)
                    for (var i = 0; i < Math.Min(list.Count, MaxListItems); i++)
                        Describe(list[i], $"{path}[{i}]", $"[{i}]", depth + 1, rows, seen);
            }
            else if (value is IEnumerable and not string)
            {
                rows.Add(new PropertyRow(path, property.Name, PropertyKind.Info, type.Name, depth, true));
            }
            else if (type.IsClass && !typeof(Delegate).IsAssignableFrom(type) && !typeof(Stream).IsAssignableFrom(type))
            {
                Describe(value, path, property.Name, depth, rows, seen);
            }
        }
    }

    private static void Describe(object? item, string path, string title, int depth, List<PropertyRow> rows, HashSet<object> seen)
    {
        if (item is null || !seen.Add(item)) return;
        if (KindOf(item.GetType()) is { } simple)
        {
            rows.Add(new PropertyRow(path, title, simple, Format(item), depth, true));
            return;
        }

        rows.Add(new PropertyRow(path, title, PropertyKind.Group, "", depth, true));
        if (depth < MaxDepth) Walk(item, path + ".", depth + 1, rows, seen);
    }

    private static PropertyKind? KindOf(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (type == typeof(bool)) return PropertyKind.Bool;
        if (type == typeof(string)) return PropertyKind.Text;
        if (type == typeof(GameMakerString)) return PropertyKind.GmString;
        // A flags value is a set of names ("Regular, IsEmbedded"): no single choice fits, so it is typed as text, which Enum.Parse reads back.
        if (type.IsEnum) return type.IsDefined(typeof(FlagsAttribute), false) ? PropertyKind.Text : PropertyKind.Enum;
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

    /// <summary>The value as text, for the old value of an edit.</summary>
    public static string Read(object root, string path)
    {
        var (owner, property) = Resolve(root, path);
        var value = property.GetValue(owner);
        return NamedValues(owner, property) is { } names ? Show(names, value) : Format(value);
    }

    /// <summary>The value as the object itself, so undo puts back the very same table entry.</summary>
    public static object? ReadRaw(object root, string path)
    {
        var (owner, property) = Resolve(root, path);
        return property.GetValue(owner);
    }

    public static void WriteRaw(object root, string path, object? value)
    {
        var (owner, property) = Resolve(root, path);
        property.SetValue(owner, value);
    }

    /// <summary>Sets a property from text. Throws <see cref="FormatException"/> or <see cref="OverflowException"/> for text the type cannot hold.</summary>
    public static void Set(GameMakerData data, object root, string path, string text)
    {
        var (owner, property) = Resolve(root, path);
        var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        if (NamedValues(owner, property) is { } names && Array.IndexOf(names, text) is var named and >= 0) text = named.ToString(CultureInfo.InvariantCulture);
        var value = type switch
        {
            _ when type == typeof(string) => text,
            _ when type == typeof(GameMakerString) => data.Strings.MakeString(text, out _),
            _ when type == typeof(bool) => bool.Parse(text),
            _ when type.IsEnum => Enum.Parse(type, text),
            _ => Convert.ChangeType(text, type, CultureInfo.InvariantCulture),
        };
        property.SetValue(owner, value);
    }
}
