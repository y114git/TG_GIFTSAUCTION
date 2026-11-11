using G3MLib.DataFile;
using G3MLib.DataFile.Models;
using static G3MLib.DataFile.Models.GameMakerRoom;

namespace G3MStudio.Core.Workspace;

/// <summary>The Explorer key of the picture a thing shows, so every view can offer Edit Image on it: a layer's tile set or background sprite, a room element's sprite, an object's sprite.</summary>
public static class PictureRefs
{
    public static string? KeyOf(object? thing) => thing switch
    {
        GameMakerSprite s => Key("sprites", s),
        GameMakerBackground b => Key("tileSets", b),
        GameMakerFont f => Key("fonts", f),
        Layer { TilesData.Background: { } set } => Key("tileSets", set),
        Layer { BackgroundData.Sprite: { } sprite } => Key("sprites", sprite),
        SpriteInstance { Sprite: { } sprite } => Key("sprites", sprite),
        GameObject { ObjectDefinition: { } definition } => KeyOf(definition),
        GameMakerParticleSystemEmitter { Sprite: { } sprite } => Key("sprites", sprite),
        GameMakerSequence.Track { Keyframes: GameMakerSequence.GraphicKeyframes graphic } => KeyOf(graphic.List?.Select(k => k.Channels.FirstOrDefault()?.Value?.Resource?.Resource).FirstOrDefault(sprite => sprite is not null)),
        GameMakerGameObject { Sprite: { } sprite } => Key("sprites", sprite),
        _ => null,
    };

    /// <summary>The picture of an object's collision mask sprite, when it has its own.</summary>
    public static string? MaskKeyOf(GameMakerGameObject definition) => definition.TextureMaskId is { } sprite ? Key("sprites", sprite) : null;

    private static string? Key(string type, GameMakerNamedResource resource) => resource.Name?.Content is { Length: > 0 } name && (type != "sprites" || resource is GameMakerSprite { Textures.Count: > 0 }) ? ResourceTypes.KeyOf(type, name) : null;
}
