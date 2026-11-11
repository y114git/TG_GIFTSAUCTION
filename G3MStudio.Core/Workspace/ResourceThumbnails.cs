using G3MLib.DataFile.Models;
using static G3MLib.DataFile.Models.GameMakerRoom;

namespace G3MStudio.Core.Workspace;

/// <summary>What a thumbnail of a resource is made from.</summary>
public enum ThumbnailKind
{
    /// <summary>No picture: the row shows the icon of its type.</summary>
    Icon,
    /// <summary>A rectangle of a texture page: sprite frame 0, tile set, object sprite, page item, embedded image, sequence or particle sprite.</summary>
    Item,
    /// <summary>The sample "Ag" cut from the glyph sheet of a font.</summary>
    Font,
    /// <summary>A whole texture page, scaled.</summary>
    Page,
    /// <summary>The room drawn small, with its fill, tiles and instances.</summary>
    Room,
    Path,
    /// <summary>One line per channel of an animation curve.</summary>
    Curve,
}

/// <summary>Which picture stands for a resource in the Explorer: the model-side part, with no drawing (the drawing is in the app's thumbnail service).</summary>
public static class ResourceThumbnails
{
    private const int Depth = 8;

    /// <summary>The type keys whose resources can have a picture; every other type shows its icon and is never looked up.</summary>
    public static readonly IReadOnlySet<string> PictureTypes = new HashSet<string>
    {
        "sprites", "tileSets", "fonts", "rooms", "objects", "sequences", "particleSystems", "paths", "animationCurves", "texturePageItems", "embeddedTextures", "embeddedImages",
    };

    public static bool CanHavePicture(string type) => PictureTypes.Contains(type);

    public static GameMakerTexturePageItem? ItemOf(object? resource) => resource switch
    {
        GameMakerSprite sprite => sprite.Textures.FirstOrDefault()?.Texture,
        GameMakerBackground background => background.Texture,
        GameMakerFont font => font.Texture,
        GameMakerTexturePageItem item => item,
        GameMakerEmbeddedImage image => image.TextureEntry,
        GameMakerGameObject gameObject => SpriteOf(gameObject)?.Textures.FirstOrDefault()?.Texture,
        GameMakerSequence sequence => FirstSprite(sequence, Depth)?.Textures.FirstOrDefault()?.Texture,
        GameMakerParticleSystem particles => particles.Emitters?.Select(e => e.Resource?.Sprite).FirstOrDefault(s => s is not null)?.Textures.FirstOrDefault()?.Texture,
        _ => null,
    };

    /// <summary>The sprite of an object, or the one of the nearest parent that has one.</summary>
    public static GameMakerSprite? SpriteOf(GameMakerGameObject gameObject)
    {
        var current = gameObject;
        for (var i = 0; i < Depth * 4 && current is not null; i++, current = current.ParentId)
            if (current.Sprite is { Textures.Count: > 0 } sprite) return sprite;
        return null;
    }

    // The first sprite a sequence shows on a graphic track, looking into sub-tracks and sub-sequences.
    private static GameMakerSprite? FirstSprite(GameMakerSequence sequence, int depth) => depth == 0 ? null : FirstSprite(sequence.Tracks, depth);

    private static GameMakerSprite? FirstSprite(IEnumerable<GameMakerSequence.Track>? tracks, int depth)
    {
        foreach (var track in tracks ?? [])
        {
            switch (track.Keyframes)
            {
                case GameMakerSequence.GraphicKeyframes graphic:
                    foreach (var key in graphic.List ?? [])
                        foreach (var channel in key.Channels ?? [])
                            if (channel.Value?.Resource?.Resource is { Textures.Count: > 0 } sprite) return sprite;
                    break;
                case GameMakerSequence.SequenceKeyframes nested:
                    foreach (var key in nested.List ?? [])
                        foreach (var channel in key.Channels ?? [])
                            if (channel.Value?.Resource?.Resource is { } inner && FirstSprite(inner, depth - 1) is { } found) return found;
                    break;
            }

            if (FirstSprite(track.Tracks, depth) is { } below) return below;
        }

        return null;
    }

    public static ThumbnailKind KindOf(object? resource) => resource switch
    {
        GameMakerRoom => ThumbnailKind.Room,
        GameMakerFont { Texture: not null } => ThumbnailKind.Font,
        GameMakerEmbeddedTexture { TextureData.Image: not null } => ThumbnailKind.Page,
        GameMakerPath { Points.Count: > 1 } => ThumbnailKind.Path,
        GameMakerAnimationCurve curve when CurvePoints(curve).Any(p => p.Count > 1) => ThumbnailKind.Curve,
        _ => ItemOf(resource) is not null ? ThumbnailKind.Item : ThumbnailKind.Icon,
    };

    /// <summary>The points of a path as (x, y).</summary>
    public static List<(float X, float Y)> PathPoints(GameMakerPath path) => [.. path.Points.Select(p => (p.X, p.Y))];

    /// <summary>The points of each channel of an animation curve as (time, value).</summary>
    public static List<List<(float X, float Y)>> CurvePoints(GameMakerAnimationCurve curve) =>
        [.. (curve.Channels ?? []).Select(c => (c.Points ?? []).Select(p => (p.X, p.Value)).ToList())];

    /// <summary>The glyph sheet rectangles of "A" and "g" (relative to the sheet's own origin) when the font has both, so the thumbnail can show a sample; otherwise empty and the whole sheet is shown.</summary>
    public static IReadOnlyList<(int X, int Y, int Width, int Height)> FontSample(GameMakerFont font) =>
        font.Glyphs is { } glyphs && glyphs.FirstOrDefault(g => g.Character == 'A' && g.SourceWidth > 0) is { } a && glyphs.FirstOrDefault(g => g.Character == 'g' && g.SourceWidth > 0) is { } g2
            ? [(a.SourceX, a.SourceY, a.SourceWidth, a.SourceHeight), (g2.SourceX, g2.SourceY, g2.SourceWidth, g2.SourceHeight)]
            : [];

    /// <summary>
    /// A cheap fingerprint of what the thumbnail of the resource is made from. It changes when the picture would, so a cached thumbnail whose fingerprint is
    /// unchanged after an edit stays. Read under the model's read lock.
    /// </summary>
    public static int Signature(object? resource)
    {
        var hash = new HashCode();
        hash.Add(KindOf(resource));
        switch (resource)
        {
            case GameMakerRoom room:
                AddRoom(ref hash, room);
                break;
            case GameMakerPath path:
                foreach (var p in path.Points) hash.Add((p.X, p.Y));
                break;
            case GameMakerAnimationCurve curve:
                foreach (var channel in CurvePoints(curve))
                    foreach (var p in channel) hash.Add(p);
                break;
            case GameMakerEmbeddedTexture page:
                hash.Add(Identity(page.TextureData?.Image));
                break;
            case GameMakerFont font:
                AddItem(ref hash, font.Texture);
                foreach (var r in FontSample(font)) hash.Add(r);
                break;
            default:
                AddItem(ref hash, ItemOf(resource));
                break;
        }

        return hash.ToHashCode();
    }

    private static int Identity(object? value) => value is null ? 0 : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value);

    private static void AddItem(ref HashCode hash, GameMakerTexturePageItem? item)
    {
        if (item is null) return;
        hash.Add(Identity(item));
        hash.Add((item.SourceX, item.SourceY, item.SourceWidth, item.SourceHeight));
        hash.Add(Identity(item.TexturePage));
        hash.Add(Identity(item.TexturePage?.TextureData?.Image));
    }

    private static void AddRoom(ref HashCode hash, GameMakerRoom room)
    {
        hash.Add((room.Width, room.Height, room.BackgroundColor, room.DrawBackgroundColor));
        foreach (var layer in room.Layers ?? [])
        {
            hash.Add((layer.IsVisible, layer.LayerDepth));
            foreach (var o in layer.InstancesData?.Instances ?? []) AddInstance(ref hash, o);
            if (layer.TilesData is { } tiles)
            {
                hash.Add(Identity(tiles.Background));
                foreach (var row in tiles.TileData ?? [])
                    foreach (var tile in row) hash.Add(tile);
            }

            foreach (var s in layer.AssetsData?.Sprites ?? []) hash.Add((Identity(s.Sprite), s.X, s.Y, s.ScaleX, s.ScaleY, s.FrameIndex));
        }

        foreach (var o in room.GameObjects ?? []) AddInstance(ref hash, o);
        foreach (var t in room.Tiles ?? []) hash.Add((t.X, t.Y, t.SourceX, t.SourceY, t.Width, t.Height, Identity(t.Tpag)));
        foreach (var b in room.Backgrounds ?? []) hash.Add((b.Enabled, b.Foreground, b.X, b.Y, Identity(b.BackgroundDefinition)));
    }

    private static void AddInstance(ref HashCode hash, GameObject o) => hash.Add((o.X, o.Y, o.ImageIndex, o.ScaleX, o.ScaleY, Identity(o.ObjectDefinition), Identity(o.ObjectDefinition?.Sprite)));
}
