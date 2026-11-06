using System.Collections;
using G3MLib.DataFile;
using G3MLib.DataFile.Models;

namespace G3MStudio.Core.Workspace;

/// <summary>
/// Moves sprites, fonts or tile sets (<paramref name="kind"/> is "sprites", "fonts" or "tilesets") into another texture group. A resource sits in one
/// group at a time, so it leaves the group it was in. Undo puts every list back as it was.
/// </summary>
public sealed class TextureGroupMoveCommand(string kind, IReadOnlyList<string> names, string group) : EditCommand
{
    public const string Name = "textureGroupMove";

    private (IList List, object?[] Items)[] _before = [];

    public override string Kind => Name;

    public override string Editor => "textureGroups";

    public override bool ChangesNames => false;

    public override IReadOnlyDictionary<string, string> Args { get; } = new Dictionary<string, string>
    {
        ["kind"] = kind,
        ["names"] = string.Join('\n', names),
        ["group"] = group,
    };

    public override IReadOnlyList<ProjectTouch>? Touches => null;

    public static TextureGroupMoveCommand FromArgs(IReadOnlyDictionary<string, string> a) => new(a["kind"], a["names"].Split('\n', StringSplitOptions.RemoveEmptyEntries), a["group"]);

    public static IList? Members(GameMakerTextureGroupInfo info, string kind) => kind switch
    {
        "sprites" => info.Sprites,
        "fonts" => info.Fonts,
        "tilesets" => info.Tilesets,
        _ => null,
    };

    public static object? ResourceOf(object? entry) => entry?.GetType().GetProperty("Resource")?.GetValue(entry);

    public static string? NameOf(object? entry) => ResourceOf(entry) is { } resource ? ResourceTypes.NameOf(resource) : null;

    public override void Apply(GameMakerData data)
    {
        var groups = data.TextureGroupInfo ?? throw new InvalidOperationException("The data file has no texture groups.");
        var target = groups.FirstOrDefault(g => g.Name.Content == group) ?? throw new InvalidOperationException($"Texture group '{group}' does not exist.");
        var lists = groups.Select(g => Members(g, kind) ?? throw new InvalidDataException($"Unknown resource kind '{kind}'.")).Where(l => l is not null).ToList();
        _before = [.. lists.Select(l => (l, l.Cast<object?>().ToArray()))];
        var targetList = Members(target, kind) ?? throw new InvalidDataException($"Unknown resource kind '{kind}'.");
        foreach (var name in names)
        {
            object? moved = null;
            foreach (var list in lists)
            {
                for (var i = list.Count - 1; i >= 0; i--)
                {
                    if (NameOf(list[i]) != name) continue;
                    moved ??= list[i];
                    list.RemoveAt(i);
                }
            }

            moved ??= Create(data, targetList, kind, name);
            targetList.Add(moved);
        }
    }

    private static object Create(GameMakerData data, IList list, string kind, string name)
    {
        IEnumerable<object> pool = kind switch { "sprites" => data.Sprites, "fonts" => data.Fonts, _ => data.Backgrounds };
        var resource = pool.FirstOrDefault(r => ResourceTypes.NameOf(r) == name) ?? throw new InvalidOperationException($"{name} does not exist.");
        var entryType = list.GetType().BaseType?.GetGenericArguments()[0] ?? throw new InvalidOperationException("Unknown member list.");
        var entry = Activator.CreateInstance(entryType) ?? throw new InvalidOperationException($"Cannot create a {entryType.Name}.");
        entry.GetType().GetProperty("Resource")?.SetValue(entry, resource);
        return entry;
    }

    public override void Revert(GameMakerData data)
    {
        foreach (var (list, items) in _before)
        {
            list.Clear();
            foreach (var item in items) list.Add(item);
        }
    }
}

/// <summary>Puts sounds into another audio group. Undo restores each sound's previous group.</summary>
public sealed class AudioGroupMoveCommand(IReadOnlyList<string> names, string group) : EditCommand
{
    public const string Name = "audioGroupMove";

    private (GameMakerSound Sound, GameMakerAudioGroup? Group)[] _before = [];

    public override string Kind => Name;

    public override string Editor => "audioGroups";

    public override bool ChangesNames => false;

    public override IReadOnlyDictionary<string, string> Args { get; } = new Dictionary<string, string> { ["names"] = string.Join('\n', names), ["group"] = group };

    public override IReadOnlyList<ProjectTouch>? Touches { get; } = [new("Sounds", null)];

    public static AudioGroupMoveCommand FromArgs(IReadOnlyDictionary<string, string> a) => new(a["names"].Split('\n', StringSplitOptions.RemoveEmptyEntries), a["group"]);

    public override void Apply(GameMakerData data)
    {
        var target = data.AudioGroups?.FirstOrDefault(g => g.Name.Content == group) ?? throw new InvalidOperationException($"Audio group '{group}' does not exist.");
        var sounds = names.Select(n => data.Sounds.FirstOrDefault(s => s.Name.Content == n) ?? throw new InvalidOperationException($"Sound '{n}' does not exist.")).ToList();
        _before = [.. sounds.Select(s => (s, s.AudioGroup))];
        foreach (var sound in sounds) sound.AudioGroup = target;
    }

    public override void Revert(GameMakerData data)
    {
        foreach (var (sound, previous) in _before)
        {
#pragma warning disable CS8601 // a sound may have had no audio group; the library stores that as null
            sound.AudioGroup = previous;
#pragma warning restore CS8601
        }
    }
}
