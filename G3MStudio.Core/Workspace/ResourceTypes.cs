using G3MLib.DataFile;

namespace G3MStudio.Core.Workspace;

/// <summary>One kind of entry the Explorer lists. <see cref="Items"/> is null when the data file has no such chunk.</summary>
public sealed record ResourceType(string Key, bool IsData, Func<GameMakerData, IEnumerable<object>?> Items);

/// <summary>The resource types of design/shell.md 5.2 items 3 and 4 that are plain lists in G3MLib. Null entries (GameMaker 2024.11) are skipped.</summary>
public static class ResourceTypes
{
    private static ResourceType Res<T>(string key, Func<GameMakerData, IList<T>?> list, bool isData = false) where T : class =>
        new(key, isData, data => list(data)?.Where(item => item is not null).Cast<object>());

    /// <summary>The names of the two entries of the Game group; they are the keys of their tabs, not translated.</summary>
    public const string GeneralInfoName = "General Info", GameOptionsName = "Game Options";

    public static readonly IReadOnlyList<ResourceType> All =
    [
        new("general", false, data => new object?[] { data.GeneralInfo, data.Options }.OfType<object>()),
        Res("sprites", d => d.Sprites),
        Res("tileSets", d => d.Backgrounds),
        Res("sounds", d => d.Sounds),
        Res("paths", d => d.Paths),
        Res("scripts", d => d.Scripts),
        Res("shaders", d => d.Shaders),
        Res("fonts", d => d.Fonts),
        Res("timelines", d => d.Timelines),
        Res("objects", d => d.GameObjects),
        Res("rooms", d => d.Rooms),
        Res("extensions", d => d.Extensions),
        Res("sequences", d => d.Sequences),
        Res("animationCurves", d => d.AnimationCurves),
        Res("particleSystems", d => d.ParticleSystems),
        Res("code", d => d.Code, true),
        Res("strings", d => d.Strings, true),
        Res("variables", d => d.Variables, true),
        Res("functions", d => d.Functions, true),
        Res("codeLocals", d => d.CodeLocals, true),
        Res("texturePageItems", d => d.TexturePageItems, true),
        Res("embeddedTextures", d => d.EmbeddedTextures, true),
        Res("embeddedImages", d => d.EmbeddedImages, true),
        Res("embeddedAudio", d => d.EmbeddedAudio, true),
        Res("textureGroups", d => d.TextureGroupInfo, true),
        Res("audioGroups", d => d.AudioGroups, true),
        Res("filterEffects", d => d.FilterEffects, true),
    ];

    public static string NameOf(object item) => item switch
    {
        GameMakerNamedResource named => named.Name?.Content ?? "",
        G3MLib.DataFile.Models.GameMakerString text => text.Content ?? "",
        G3MLib.DataFile.Models.GameMakerGeneralInfo => GeneralInfoName,
        G3MLib.DataFile.Models.GameMakerOptions => GameOptionsName,
        _ => item.ToString() ?? "",
    };

    /// <summary>Tab key of an entry: its type key and name.</summary>
    public static string KeyOf(string type, string name) => type + ":" + name;

    public static (string Type, string Name)? SplitKey(string key) => key.IndexOf(':', StringComparison.Ordinal) is var i and > 0 ? (key[..i], key[(i + 1)..]) : null;

    /// <summary>The entry a tab key names, or null when the data file has no such entry.</summary>
    public static object? Find(GameMakerData data, string key) =>
        SplitKey(key) is var (type, name) ? All.FirstOrDefault(t => t.Key == type)?.Items(data)?.FirstOrDefault(i => NameOf(i) == name) : null;

    /// <summary>Entry count per type key; the save pipeline compares these before and after the validation read.</summary>
    public static Dictionary<string, int> Counts(GameMakerData data)
    {
        var counts = new Dictionary<string, int>();
        foreach (var type in All)
            if (type.Items(data) is { } items) counts[type.Key] = items.Count();
        return counts;
    }
}
