using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using G3MLib.DataFile.Models;
using G3MLib.DataFile.Util;
using static G3MLib.DataFile.Models.GameMakerRoom;

namespace G3MStudio.Rooms;

/// <summary>One textured rectangle of a room: a source rectangle on a texture page and where it lands in room pixels.</summary>
public readonly record struct RoomItem(Bitmap Page, Rect Source, Rect Dest, object? Element = null, int Flags = 0)
{
    /// <summary>Bit 0 mirrors, bit 1 flips, bit 2 rotates a quarter turn (the tile flags of a tile map, bits 28 to 30 of a tile value).</summary>
    public GameObject? Instance => Element as GameObject;
}

/// <summary>An element with no picture to draw (a sequence, particle system or text item): drawn as a labelled box so it can still be seen and selected.</summary>
public readonly record struct RoomMarker(Rect Bounds, string Label, object Element);

/// <summary>
/// Draw list of one room, ordered back to front. Spike scope: tiles and instances with a sprite, GMS1 and GMS2 layouts.
/// Skipped: backgrounds, views, asset and effect layers, tile mirror and rotate flags, instance rotation and color.
/// </summary>
public sealed class RoomScene : IDisposable
{
    private const uint TileIndexMask = 0x0007FFFF;

    public required double Width { get; init; }

    public required double Height { get; init; }

    public required List<RoomItem> Items { get; init; }

    public int Skipped { get; init; }

    public List<RoomMarker> Markers { get; init; } = [];

    /// <summary>True when the scene decoded its own pages, and so frees them. Pages from a <see cref="TextureBitmaps"/> belong to it.</summary>
    public bool OwnsPages { get; init; }

    /// <summary>Frees the texture pages the scene decoded itself.</summary>
    public void Dispose()
    {
        if (!OwnsPages) return;
        foreach (var page in Items.Select(i => i.Page).Distinct()) page.Dispose();
    }

    /// <summary>Builds the scene with pages shared through <paramref name="textures"/>; the slow decoding happens off the UI thread.</summary>
    public static async Task<RoomScene> BuildAsync(GameMakerRoom room, TextureBitmaps textures)
    {
        var ready = new Dictionary<GameMakerEmbeddedTexture, Bitmap?>();
        foreach (var texture in PagesUsedBy(room)) ready[texture] = await textures.GetAsync(texture);
        return Build(room, texture => ready.GetValueOrDefault(texture), ownsPages: false);
    }

    public static RoomScene Build(GameMakerRoom room) => Build(room, ToBitmap, ownsPages: true);

    /// <summary>A first pass over the room that decodes nothing and only collects the texture pages its items draw from.</summary>
    private static HashSet<GameMakerEmbeddedTexture> PagesUsedBy(GameMakerRoom room)
    {
        var used = new HashSet<GameMakerEmbeddedTexture>();
        using var probe = Build(room, texture => { used.Add(texture); return null; }, ownsPages: false);
        return used;
    }

    private static RoomScene Build(GameMakerRoom room, Func<GameMakerEmbeddedTexture, Bitmap?> pageOf, bool ownsPages)
    {
        var pages = new Dictionary<GameMakerEmbeddedTexture, Bitmap?>();
        var items = new List<RoomItem>();
        var skipped = 0;

        var markers = new List<RoomMarker>();

        void Add(GameMakerTexturePageItem? tpag, Rect source, Rect dest, object? element = null, int flags = 0)
        {
            if (tpag?.TexturePage is not { } texture) { skipped++; return; }
            if (!pages.TryGetValue(texture, out var page)) pages[texture] = page = pageOf(texture);
            if (page is null) { skipped++; return; }
            items.Add(new RoomItem(page, source.Translate(new Vector(tpag.SourceX, tpag.SourceY)), dest, element, flags));
        }

        void AddSprite(GameMakerSprite? definition, int frame, (double X, double Y, double ScaleX, double ScaleY) at, object element)
        {
            if (definition is not { Textures.Count: > 0 } sprite) { skipped++; return; }
            var tpag = sprite.Textures[Math.Abs(frame) % sprite.Textures.Count].Texture;
            if (tpag is null) { skipped++; return; }
            var dest = new Rect(at.X + (tpag.TargetX - sprite.OriginX) * at.ScaleX, at.Y + (tpag.TargetY - sprite.OriginY) * at.ScaleY, tpag.TargetWidth * at.ScaleX, tpag.TargetHeight * at.ScaleY);
            Add(tpag, new Rect(0, 0, tpag.SourceWidth, tpag.SourceHeight), dest, element);
        }

        void AddInstance(GameObject o) => AddSprite(o.ObjectDefinition?.Sprite, o.ImageIndex, (o.X, o.Y, o.ScaleX, o.ScaleY), o);

        void AddMarker(object element, string label, int x, int y) => markers.Add(new RoomMarker(new Rect(x - 16, y - 16, 32, 32), label, element));

        void AddAssets(Layer.LayerAssetsData assets)
        {
            foreach (var s in assets.Sprites ?? []) AddSprite(s.Sprite, (int)s.FrameIndex, (s.X, s.Y, s.ScaleX, s.ScaleY), s);
            foreach (var q in assets.Sequences ?? []) AddMarker(q, q.Sequence?.Name.Content ?? "sequence", q.X, q.Y);
            foreach (var p in assets.ParticleSystems ?? []) AddMarker(p, p.Name?.Content ?? "particles", p.X, p.Y);
            foreach (var t in assets.TextItems ?? []) AddMarker(t, t.Text?.Content ?? "text", t.X, t.Y);
        }

        if (room.Layers?.Count > 0)
        {
            // Higher depth is further back.
            foreach (var layer in room.Layers.Where(l => l is { IsVisible: true }).OrderByDescending(l => l.LayerDepth))
            {
                if (layer.InstancesData is { } instances)
                {
                    foreach (var o in instances.Instances) AddInstance(o);
                }
                else if (layer.AssetsData is { } assets)
                {
                    AddAssets(assets);
                }
                else if (layer.TilesData is { Background: { Texture: { } tpag } bg, TileData: { } rows })
                {
                    int tw = (int)bg.GMS2TileWidth, th = (int)bg.GMS2TileHeight, bx = (int)bg.GMS2OutputBorderX, by = (int)bg.GMS2OutputBorderY, columns = Math.Max(1, (int)bg.GMS2TileColumns);
                    for (var y = 0; y < rows.Length; y++)
                    {
                        for (var x = 0; x < rows[y].Length; x++)
                        {
                            var index = (int)(rows[y][x] & TileIndexMask);
                            if (index == 0) continue;
                            var source = new Rect(index % columns * (tw + 2 * bx) + bx, index / columns * (th + 2 * by) + by, tw, th);
                            Add(tpag, source, new Rect(layer.XOffset + x * tw, layer.YOffset + y * th, tw, th), null, (int)((rows[y][x] >> 28) & 7));
                        }
                    }
                }
            }
        }
        else
        {
            // Game Maker Studio 1: tiles and instances share one depth order.
            var drawn = (room.Tiles ?? []).Select(t => (Depth: t.TileDepth, Draw: (Action)(() =>
                {
                    var dest = new Rect(t.X, t.Y, t.Width * t.ScaleX, t.Height * t.ScaleY);
                    Add(t.Tpag, new Rect(t.SourceX, t.SourceY, t.Width, t.Height), dest);
                })))
                .Concat((room.GameObjects ?? []).Select(o => (Depth: o.ObjectDefinition?.Depth ?? 0, Draw: (Action)(() => AddInstance(o)))));
            foreach (var (_, draw) in drawn.OrderByDescending(d => d.Depth)) draw();
        }

        return new RoomScene { Width = room.Width, Height = room.Height, Items = items, Skipped = skipped, OwnsPages = ownsPages, Markers = markers };
    }

    internal static WriteableBitmap? ToBitmap(GameMakerEmbeddedTexture texture) => ToBitmap(DecodePage(texture));

    /// <summary>The slow part, decoding the page to raw pixels. It reads only the model and may run on any thread.</summary>
    internal static GMImage? DecodePage(GameMakerEmbeddedTexture texture)
    {
        var image = texture.TextureData?.Image;
        return image is null || image.Width <= 1 ? null : image.ConvertToRawBgra();
    }

    /// <summary>The cheap part, copying raw pixels into a bitmap. Needs the UI thread.</summary>
    internal static WriteableBitmap? ToBitmap(GMImage? raw)
    {
        if (raw is null) return null;
        var bitmap = new WriteableBitmap(new PixelSize(raw.Width, raw.Height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Unpremul);
        using var locked = bitmap.Lock();
        for (var row = 0; row < raw.Height; row++)
            Marshal.Copy(raw.GetData(), row * raw.Width * 4, locked.Address + row * locked.RowBytes, raw.Width * 4);
        return bitmap;
    }

    /// <summary>A generated room of columns x rows tiles of 16 px, for machines without game files.</summary>
    public static RoomScene Synthetic(int columns, int rows)
    {
        const int tile = 16;
#pragma warning disable CA2000 // owned by the scene, which disposes it
        var page = new WriteableBitmap(new PixelSize(256, tile), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Unpremul);
        using (var locked = page.Lock())
        {
            var pixels = new byte[256 * tile * 4];
            for (var i = 0; i < pixels.Length; i += 4)
            {
                var cell = i / 4 % 256 / tile;
                pixels[i] = (byte)(cell * 16);
                pixels[i + 1] = (byte)(255 - cell * 16);
                pixels[i + 2] = (byte)((i / 4 / 256 + cell) * 8);
                pixels[i + 3] = 255;
            }

            Marshal.Copy(pixels, 0, locked.Address, pixels.Length);
        }

        var items = new List<RoomItem>(columns * rows);
        for (var y = 0; y < rows; y++)
            for (var x = 0; x < columns; x++)
                items.Add(new RoomItem(page, new Rect((x * 7 + y * 3) % 16 * tile, 0, tile, tile), new Rect(x * tile, y * tile, tile, tile)));
        return new RoomScene { Width = columns * tile, Height = rows * tile, Items = items };
    }
}
