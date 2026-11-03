using G3MLib.DataFile;
using G3MLib.DataFile.Models;
using G3MLib.DataFile.Util;
using ImageMagick;

namespace G3MStudio.Core.Workspace;

/// <summary>
/// The template of a new sprite (owner decision Q58): one transparent frame of 32 by 32 pixels, origin at the top left, a rectangle collision box over the whole frame.
/// The frame goes on a small texture page of its own that is added to the sprite's texture group; no other page is packed again, so it is quick on any game and
/// <see cref="ResourceListCommand"/> can undo it (the library's whole-group repack stops on some games).
/// </summary>
public static class SpriteTemplate
{
    public const int Size = 32;
    private const int PageSize = 64;

    /// <summary>The texture group the new page joins: the group of the first page kept in the data file, or null when the file has none (older games).</summary>
    public static GameMakerTextureGroupInfo? GroupOf(GameMakerData data) => data.EmbeddedTextures.FirstOrDefault(t => !t.TextureExternal)?.TextureInfo;

    /// <summary>
    /// Puts a picture on a texture page of its own (a power of two, at least 64 pixels, transparent around it) that joins the first group, and returns its page item.
    /// <see cref="RemoveItem"/> takes it out again.
    /// </summary>
    public static GameMakerTexturePageItem AddItem(GameMakerData data, MagickImage picture)
    {
        var group = GroupOf(data); // null in older games, which keep no texture groups
        var template = data.EmbeddedTextures.First(t => !t.TextureExternal);
        var (w, h) = (checked((ushort)picture.Width), checked((ushort)picture.Height));
        var side = Math.Max(PageSize, (int)System.Numerics.BitOperations.RoundUpToPowerOf2(Math.Max(picture.Width, picture.Height)));
        using var canvas = new MagickImage(MagickColors.Transparent, (uint)side, (uint)side);
        canvas.Composite(picture, 0, 0, CompositeOperator.Copy);
        var page = new GameMakerEmbeddedTexture
        {
            TextureData = new GameMakerEmbeddedTexture.TexData { Image = GMImage.FromMagickImage(canvas).ConvertToFormat(template.TextureData.Image.Format) },
            TextureInfo = group!,
            IndexInGroup = group?.TexturePages.Count ?? 0,
            TextureWidth = side,
            TextureHeight = side,
            Scaled = template.Scaled,
            GeneratedMips = template.GeneratedMips,
            Name = new GameMakerString($"Texture {data.EmbeddedTextures.Count}"),
        };
        data.EmbeddedTextures.Add(page);
        group?.TexturePages.Add(new(page));
        var item = new GameMakerTexturePageItem { TexturePage = page, SourceX = 0, SourceY = 0, SourceWidth = w, SourceHeight = h, TargetX = 0, TargetY = 0, TargetWidth = w, TargetHeight = h, BoundingWidth = w, BoundingHeight = h };
        data.TexturePageItems.Add(item);
        return item;
    }

    /// <summary>Takes a page item made by <see cref="AddItem"/>, its page and its place in the group out again.</summary>
    public static void RemoveItem(GameMakerData data, GameMakerTexturePageItem item)
    {
        if (item.TexturePage is not { } page) return;
        data.EmbeddedTextures.Remove(page);
        if (page.TextureInfo?.TexturePages is { Count: > 0 } pages) pages.RemoveAt(pages.Count - 1);
        data.TexturePageItems.Remove(item);
    }

    /// <summary>Adds the sprite, its page and its page item to the data file.</summary>
    public static GameMakerSprite Create(GameMakerData data, string name)
    {
        using var blank = new MagickImage(MagickColors.Transparent, Size, Size);
        var item = AddItem(data, blank);

        var sprite = new GameMakerSprite
        {
            Name = data.Strings.MakeString(name),
            Width = Size,
            Height = Size,
            MarginLeft = 0,
            MarginRight = Size - 1,
            MarginTop = 0,
            MarginBottom = Size - 1,
            BBoxMode = 0,
            Smooth = false,
            Transparent = false,
            Preload = false,
            SepMasks = GameMakerSprite.SepMaskType.AxisAlignedRect,
            OriginX = 0,
            OriginY = 0,
            IsSpecialType = true,
            SSpriteType = GameMakerSprite.SpriteType.Normal,
            SVersion = data.Sprites.Where(s => s.IsSpecialType).Select(s => s.SVersion).DefaultIfEmpty(data.IsVersionAtLeast(2023, 2) ? 3u : 2u).Max(),
            GMS2PlaybackSpeed = 30,
        };
        sprite.Textures.Add(new GameMakerSprite.TextureEntry { Texture = item });
        sprite.V2Sequence = NewSequence(sprite, data);
        sprite.V3NineSlice = sprite.SVersion >= 3 ? new GameMakerSprite.NineSlice() : null!;
        data.Sprites.Add(sprite);
        return sprite;
    }

    // The sequence of a sprite of GameMaker 2.3 and later: one track of sprite frames with one key.
    private static GameMakerSequence NewSequence(GameMakerSprite sprite, GameMakerData data)
    {
        GameMakerSequence.Track track = new()
        {
            ModelName = data.Strings.MakeString("GMSpriteFramesTrack"),
            Name = data.Strings.MakeString("frames"),
            Tags = [],
            Tracks = [],
            OwnedResources = [],
            Keyframes = new GameMakerSequence.SpriteFramesKeyframes { List = [] },
        };
        var keyframes = (GameMakerSequence.SpriteFramesKeyframes)track.Keyframes;
        GameMakerSequence.Keyframe<GameMakerSequence.SpriteFramesKeyframes.Data> key = new() { Key = 0, Length = 1 };
        key.Channels.Add(new() { Channel = 0, Value = new() { Value = 0 } });
        keyframes.List.Add(key);
        GameMakerSequence sequence = new()
        {
            Name = sprite.Name,
            Playback = (GameMakerSequence.PlaybackType)1,
            PlaybackSpeed = sprite.GMS2PlaybackSpeed,
            PlaybackSpeedType = sprite.GMS2PlaybackSpeedType,
            Length = 1,
            OriginX = sprite.OriginX,
            OriginY = sprite.OriginY,
            Volume = 1,
            Width = sprite.Width,
            Height = sprite.Height,
            BroadcastMessages = [],
            Moments = [],
            Tracks = [],
            FunctionIDs = [],
        };
        sequence.Tracks.Add(track);
        return sequence;
    }
}
