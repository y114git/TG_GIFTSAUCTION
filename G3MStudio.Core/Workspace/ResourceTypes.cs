using G3MLib.DataFile;

namespace G3MStudio.Core.Workspace;

/// <summary>One kind of entry the Explorer lists. <see cref="Items"/> is null when the data file has no such chunk.</summary>
/// <summary>Raw is the list as the data file has it, null entries included, for what is found by position (an asset index in GML).</summary>
public sealed record ResourceType(string Key, bool IsData, Func<GameMakerData, IEnumerable<object>?> Items, Func<GameMakerData, IEnumerable<object?>?>? Raw = null, Func<GameMakerData, System.Collections.IList?>? List = null);

/// <summary>The resource types of design/shell.md 5.2 items 3 and 4 that are plain lists in G3MLib. Null entries (GameMaker 2024.11) are skipped.</summary>
public static class ResourceTypes
{
    private static ResourceType Res<T>(string key, Func<GameMakerData, IList<T>?> list, bool isData = false) where T : class =>
        new(key, isData, data => list(data)?.Where(item => item is not null).Cast<object>(), data => list(data)?.Cast<object?>(), data => list(data) as System.Collections.IList);

    /// <summary>The names of the two entries of the Game group; they are the keys of their tabs, not translated.</summary>
    public const string GeneralInfoName = "General Info", GameOptionsName = "Game Options", TagsName = "Tags", FeatureFlagsName = "Feature Flags", LanguageName = "Language", IncludedFilesName = "Included Files";

    public static readonly IReadOnlyList<ResourceType> All =
    [
        new("general", false, data => new object?[] { data.GeneralInfo, data.Options, data.Tags, data.FeatureFlags, data.Language, IncludedFilesEntry.Instance }.OfType<object>()),
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
        Res("uiLayers", d => d.FORM.UILR?.List, true),
    ];

    public static string NameOf(object item) => item switch
    {
        GameMakerNamedResource named => named.Name?.Content ?? "",
        G3MLib.DataFile.Models.GameMakerString text => text.Content ?? "",
        G3MLib.DataFile.Models.GameMakerGeneralInfo => GeneralInfoName,
        G3MLib.DataFile.Models.GameMakerOptions => GameOptionsName,
        G3MLib.DataFile.Models.GameMakerTags => TagsName,
        G3MLib.DataFile.Models.GameMakerFeatureFlags => FeatureFlagsName,
        G3MLib.DataFile.Models.GameMakerLanguage => LanguageName,
        _ => item.ToString() ?? "",
    };

    /// <summary>
    /// The type key a reference field of this CLR type points at (<c>sprites</c>, <c>texturePageItems</c>, <c>functions</c>), or null when the type is no entry the Explorer lists.
    /// Unlike <see cref="UsageSearch.TypeKeyOf"/> it also covers the data tables, so every link can be followed and set.
    /// </summary>
    public static string? TypeKeyOfType(Type type)
    {
        foreach (var (match, key) in LinkTypes)
            if (match.IsAssignableFrom(type)) return key;
        return null;
    }

    private static readonly (Type Type, string Key)[] LinkTypes =
    [
        (typeof(G3MLib.DataFile.Models.GameMakerSprite), "sprites"),
        (typeof(G3MLib.DataFile.Models.GameMakerBackground), "tileSets"),
        (typeof(G3MLib.DataFile.Models.GameMakerSound), "sounds"),
        (typeof(G3MLib.DataFile.Models.GameMakerPath), "paths"),
        (typeof(G3MLib.DataFile.Models.GameMakerScript), "scripts"),
        (typeof(G3MLib.DataFile.Models.GameMakerShader), "shaders"),
        (typeof(G3MLib.DataFile.Models.GameMakerFont), "fonts"),
        (typeof(G3MLib.DataFile.Models.GameMakerTimeline), "timelines"),
        (typeof(G3MLib.DataFile.Models.GameMakerGameObject), "objects"),
        (typeof(G3MLib.DataFile.Models.GameMakerRoom), "rooms"),
        (typeof(G3MLib.DataFile.Models.GameMakerExtension), "extensions"),
        (typeof(G3MLib.DataFile.Models.GameMakerSequence), "sequences"),
        (typeof(G3MLib.DataFile.Models.GameMakerAnimationCurve), "animationCurves"),
        (typeof(G3MLib.DataFile.Models.GameMakerParticleSystem), "particleSystems"),
        (typeof(G3MLib.DataFile.Models.GameMakerCode), "code"),
        (typeof(G3MLib.DataFile.Models.GameMakerFunction), "functions"),
        (typeof(G3MLib.DataFile.Models.GameMakerVariable), "variables"),
        (typeof(G3MLib.DataFile.Models.GameMakerCodeLocals), "codeLocals"),
        (typeof(G3MLib.DataFile.Models.GameMakerTexturePageItem), "texturePageItems"),
        (typeof(G3MLib.DataFile.Models.GameMakerEmbeddedTexture), "embeddedTextures"),
        (typeof(G3MLib.DataFile.Models.GameMakerEmbeddedImage), "embeddedImages"),
        (typeof(G3MLib.DataFile.Models.GameMakerEmbeddedAudio), "embeddedAudio"),
        (typeof(G3MLib.DataFile.Models.GameMakerTextureGroupInfo), "textureGroups"),
        (typeof(G3MLib.DataFile.Models.GameMakerAudioGroup), "audioGroups"),
        (typeof(G3MLib.DataFile.Models.GameMakerFilterEffect), "filterEffects"),
        (typeof(G3MLib.DataFile.Models.GameMakerUIRootNode), "uiLayers"),
    ];

    /// <summary>Tab key of an entry: its type key and name.</summary>
    public static string KeyOf(string type, string name) => type + ":" + name;

    public static (string Type, string Name)? SplitKey(string key) => key.IndexOf(':', StringComparison.Ordinal) is var i and > 0 ? (key[..i], key[(i + 1)..]) : null;

    /// <summary>The entry a tab key names, or null when the data file has no such entry.</summary>
    public static object? Find(GameMakerData data, string key) =>
        SplitKey(key) is var (type, name) ? All.FirstOrDefault(t => t.Key == type)?.Items(data)?.FirstOrDefault(i => NameOf(i) == name) : null;

    /// <summary>The number of the entry a key names inside its list, from 0 (the ID the status bar and the hover card show), or null when the data file has no such entry.</summary>
    public static int? IdOf(GameMakerData data, string key)
    {
        if (SplitKey(key) is not var (type, name) || All.FirstOrDefault(t => t.Key == type)?.Items(data) is not { } items) return null;
        var index = 0;
        foreach (var item in items)
        {
            if (NameOf(item) == name) return index;
            index++;
        }

        return null;
    }

    /// <summary>The key of the resource that has this name (any resource type, not the data tables), or null when none has it.</summary>
    public static string? KeyOfName(GameMakerData data, string name)
    {
        foreach (var type in All)
            if (!type.IsData && type.Items(data)?.Any(i => NameOf(i) == name) == true) return KeyOf(type.Key, name);
        return null;
    }

    /// <summary>Entry count per type key; the save pipeline compares these before and after the validation read.</summary>
    public static Dictionary<string, int> Counts(GameMakerData data)
    {
        var counts = new Dictionary<string, int>();
        foreach (var type in All)
            if (type.Items(data) is { } items) counts[type.Key] = items.Count();
        return counts;
    }
}
