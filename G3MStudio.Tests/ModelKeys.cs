using System.Reflection;
using G3MLib.DataFile;
using G3MLib.DataFile.Models;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

/// <summary>Every property name, enum value and event subtype the editors can show for the data model, found by reflection.</summary>
public static class ModelKeys
{
    private static readonly string[] Hidden = ["ProjectName", "ProjectAssetType", "ProjectExportable", "OriginXWrapper", "OriginYWrapper"];

    // The data file properties the editors open (the resource types of ResourceTypes.All). Entries that other entries point at show as a name, so their fields are only reached from their own list.
    private static readonly string[] Roots = ["GeneralInfo", "Options", "Tags", "FeatureFlags", "Language", "Sprites", "Backgrounds", "Sounds", "Paths", "Scripts", "Shaders", "Fonts", "Timelines", "GameObjects", "Rooms", "Extensions", "Sequences", "AnimationCurves", "ParticleSystems", "Code", "Strings", "Variables", "Functions", "CodeLocals", "TexturePageItems", "EmbeddedTextures", "EmbeddedImages", "EmbeddedAudio", "TextureGroupInfo", "AudioGroups", "FilterEffects"];

    /// <summary>The property names (Fields), enum values (Choices) and single flags of flags enums (Flags) reachable from the file data, as the property grid lists them. A name that is an enum value somewhere is a choice only.</summary>
    public static (SortedSet<string> Fields, SortedSet<string> Choices, SortedSet<string> Flags) Model()
    {
        var fields = new SortedSet<string>(StringComparer.Ordinal);
        var choices = new SortedSet<string>(StringComparer.Ordinal) { "Automatic", "FullImage", "Manual" }; // BBoxMode is a number in the file; the grid names its values
        var flags = new SortedSet<string>(StringComparer.Ordinal);
        var seen = new HashSet<Type>();
        var all = typeof(GameMakerData).Assembly.GetExportedTypes();
        foreach (var name in Roots) Visit(typeof(GameMakerData).GetProperty(name)!.PropertyType, all, seen, fields, choices, flags, true);
        Visit(typeof(IncludedFilesEntry), all, seen, fields, choices, flags, true);
        foreach (var node in new IGameMakerUINodeData[] { new GameMakerUILayer(), new GameMakerUIFlexPanel(), new GameMakerUITextItemInstance() })
        {
            foreach (var prop in UiLayerProps.For(node).Where(p => p.Choices is not null)) choices.UnionWith(Enum.GetNames(prop.Choices!));
        }

        flags.ExceptWith(choices);
        return (fields, choices, flags);
    }

    private static void Visit(Type type, Type[] all, HashSet<Type> seen, SortedSet<string> fields, SortedSet<string> choices, SortedSet<string> flags, bool root = false)
    {
        if (Nullable.GetUnderlyingType(type) is { } inner) type = inner;
        if (type.IsGenericType && type.GetGenericArguments().Length == 1) type = type.GetGenericArguments()[0];
        if (type.IsEnum)
        {
            // A flags field lists its single flags only.
            if (type.IsDefined(typeof(FlagsAttribute), false)) flags.UnionWith(PropertyModel.FlagNames(type));
            else choices.UnionWith(Enum.GetNames(type));
            return;
        }

        if (type == typeof(string) || type.IsArray || type.Namespace?.StartsWith("G3M", StringComparison.Ordinal) != true || (!root && typeof(GameMakerNamedResource).IsAssignableFrom(type)) || !seen.Add(type)) return;
        if (type.IsAbstract || type.IsInterface)
        {
            foreach (var impl in all.Where(t => t.IsClass && !t.IsAbstract && type.IsAssignableFrom(t))) Visit(impl, all, seen, fields, choices, flags, root);
            return;
        }

        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.GetIndexParameters().Length > 0 || Hidden.Contains(property.Name)) continue;
            fields.Add(property.Name);
            Visit(property.PropertyType, all, seen, fields, choices, flags);
        }
    }

    /// <summary>The names the event menus show: each subtype enum without its aliases. A numbered name (User3) shares the key of its prefix (<c>events.sub.User</c>).</summary>
    public static SortedSet<string> Subtypes() => new(
        new[] { typeof(EventSubtypeStep), typeof(EventSubtypeDraw), typeof(EventSubtypeMouse), typeof(EventSubtypeKey), typeof(EventSubtypeOther), typeof(EventSubtypeGesture) }
            .SelectMany(t => Enum.GetValues(t).Cast<object>().DistinctBy(v => Convert.ToUInt32(v, System.Globalization.CultureInfo.InvariantCulture)).Select(v => v.ToString()!))
            .Where(n => !Numbered(n)).Append("User").Append("OutsideView").Append("BoundaryView"), StringComparer.Ordinal);

    private static bool Numbered(string name) => (name.StartsWith("User", StringComparison.Ordinal) || name.StartsWith("OutsideView", StringComparison.Ordinal) || name.StartsWith("BoundaryView", StringComparison.Ordinal)) && char.IsDigit(name[^1]);
}
