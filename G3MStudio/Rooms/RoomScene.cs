using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using G3MLib.DataFile.Models;
using G3MLib.DataFile.Util;
using G3MStudio.Localization;
using static G3MLib.DataFile.Models.GameMakerRoom;

namespace G3MStudio.Rooms;

/// <summary>Counterclockwise degrees about a pivot in room pixels, opacity from 0 to 1, and the grid when the picture is repeated.</summary>
public readonly record struct ItemLook(double Rotation, Point Pivot, double Alpha, TileGrid? Grid = null);

/// <summary>A picture repeated over a grid: copy (column, row) is the item's rectangle moved by (column * CellWidth, row * CellHeight). One item instead of one per copy, so the room covers any size.</summary>
public readonly record struct TileGrid(int Columns, int Rows, double CellWidth, double CellHeight);

/// <summary>One textured rectangle of a room: a source rectangle on a texture page and where it lands in room pixels. With a <see cref="TileGrid"/> it is the first copy of a repeated picture.</summary>
public readonly record struct RoomItem(Bitmap Page, Rect Source, Rect Dest, object? Element = null, int Flags = 0, ItemLook? Look = null)
{
    public TileGrid? Grid => Look?.Grid;

    /// <summary>Everything the item covers: its rectangle, or the first and last copy of a grid.</summary>
    public Rect Extent => ExtentAt(Dest);

    private Rect ExtentAt(Rect dest) => Grid is { } g ? dest.Union(dest.Translate(new Vector((g.Columns - 1) * g.CellWidth, (g.Rows - 1) * g.CellHeight))) : dest;

    /// <summary>Game Maker rotates counterclockwise about the instance's position; the item's pivot moves with the item when it is dragged (<paramref name="dest"/> is where the item is now).</summary>
    public Matrix TurnTo(Rect dest) =>
        Matrix.CreateTranslation(-Pivot.X, -Pivot.Y) * Matrix.CreateRotation(-Rotation * Math.PI / 180) * Matrix.CreateTranslation(Pivot.X + dest.X - Dest.X, Pivot.Y + dest.Y - Dest.Y);

    /// <summary>The room rectangle the item is drawn in at <paramref name="dest"/>: its rectangle turned about the pivot.</summary>
    public Rect Seen(Rect dest) => Rotation == 0 ? ExtentAt(dest) : Extent.TransformToAABB(TurnTo(dest));

    /// <summary>True when the room point lies on the item as drawn (a turned item is hit where it is turned).</summary>
    public bool Hit(Point room) => Rotation == 0 || !TurnTo(Dest).TryInvert(out var back) ? Extent.Contains(room) : Extent.Contains(back.Transform(room));

    public double Rotation => Look?.Rotation ?? 0;

    public Point Pivot => Look?.Pivot ?? default;

    public double Alpha => Look?.Alpha ?? 1;

    /// <summary>Bit 0 mirrors, bit 1 flips, bit 2 rotates a quarter turn (the tile flags of a tile map, bits 28 to 30 of a tile value).</summary>
    public GameObject? Instance => Element as GameObject;
}

/// <summary>
/// A text item laid out with its font: the text, where its pivot is in the room, the origin inside the text, the scale and the counterclockwise turn in degrees.
/// The room rectangle of the unturned text is the marker's bounds.
/// </summary>
public sealed record RoomText(FormattedText Layout, Point Pivot, Point Origin, double ScaleX, double ScaleY, double Rotation)
{
    /// <summary>Text and pivot moved by <paramref name="delta"/> (a drag in progress).</summary>
    public Matrix At(Vector delta) =>
        Matrix.CreateTranslation(-Origin.X, -Origin.Y) * Matrix.CreateScale(ScaleX, ScaleY) * Matrix.CreateRotation(-Rotation * Math.PI / 180) * Matrix.CreateTranslation(Pivot.X + delta.X, Pivot.Y + delta.Y);
}

/// <summary>A note on the room that is not an element (an effect layer, a layer that scrolls) has no <see cref="Element"/> and cannot be picked. An element drawn as a labelled box so it can be seen and selected (a sequence or a particle system), or as its own text (a text item with <see cref="Text"/>). The caption is laid out once.</summary>
public readonly record struct RoomMarker(Rect Bounds, string Label, object? Element, FormattedText? Caption = null, RoomText? Text = null)
{
    public const double CaptionSize = 11;

    internal static FormattedText MakeCaption(string label) =>
        new(label, System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, Typeface.Default, CaptionSize, Brushes.White);
}

/// <summary>
/// Draw list of one room, ordered back to front, GMS1 and GMS2 layouts: background layers (colour, sprite, tiled, stretched, blend alpha), tiles with
/// their flags, instances and asset sprites (scale, rotation, alpha), legacy tiles and nine-slice sprites (stretched, repeated or mirrored piece by piece). Sequences, particle systems and text items are boxes.
/// Not drawn: effect layers, a colour layer that is not the deepest drawn one.
/// </summary>
public sealed partial class RoomScene : IDisposable
{
    private const uint TileIndexMask = 0x0007FFFF;

    public required double Width { get; init; }

    public required double Height { get; init; }

    public required List<RoomItem> Items { get; init; }

    public int Skipped { get; init; }

    /// <summary>The room's own background color (Game Maker Studio 1, as 0x00BBGGRR), or null for black.</summary>
    public uint? FillColor { get; init; }

    public List<RoomMarker> Markers { get; init; } = [];

    /// <summary>True when the scene decoded its own pages, and so frees them. Pages from a <see cref="TextureBitmaps"/> belong to it.</summary>
    public bool OwnsPages { get; init; }

    /// <summary>The colour-tinted crops the scene made of texture pages (instance, tile and layer blend colours). They always belong to the scene.</summary>
    public List<Bitmap> Tinted { get; init; } = [];

    private Dictionary<object, Rect>? _bounds;

    /// <summary>The drawn bounds of an element (instance, tile or asset), as drawn: a turned element has the box of its turned picture, or null when it is not drawn. The index is built on first use, so a lookup costs the same on a room with 200,000 items.</summary>
    public Rect? BoundsOf(object element)
    {
        if (_bounds is null)
        {
            _bounds = new Dictionary<object, Rect>(ReferenceEqualityComparer.Instance);
            foreach (var item in Items)
                if (item.Element is { } owner) _bounds[owner] = _bounds.TryGetValue(owner, out var seen) ? seen.Union(item.Seen(item.Dest).Normalize()) : item.Seen(item.Dest).Normalize();
            foreach (var marker in Markers)
                if (marker.Element is { } owner) _bounds[owner] = marker.Bounds;
        }

        return _bounds.TryGetValue(element, out var found) ? found : null;
    }

    /// <summary>Frees the texture pages the scene decoded itself.</summary>
    public void Dispose()
    {
        foreach (var crop in Tinted) crop.Dispose();
        if (!OwnsPages) return;
        foreach (var page in Items.Select(i => i.Page).Distinct().Except(Tinted)) page.Dispose();
    }

    /// <summary>Builds the scene with pages shared through <paramref name="textures"/>; the slow decoding happens off the UI thread.</summary>
    public static async Task<RoomScene> BuildAsync(GameMakerRoom room, TextureBitmaps textures)
    {
        var ready = new Dictionary<GameMakerEmbeddedTexture, Bitmap?>();
        foreach (var texture in PagesUsedBy(room)) ready[texture] = await textures.GetAsync(texture);
        return Build(room, texture => ready.GetValueOrDefault(texture), ownsPages: false);
    }

    public static RoomScene Build(GameMakerRoom room) => Build(room, ToBitmap, ownsPages: true);

    // A first pass over the room that decodes nothing and only collects the texture pages its items draw from.
    internal static HashSet<GameMakerEmbeddedTexture> PagesUsedBy(GameMakerRoom room)
    {
        var used = new HashSet<GameMakerEmbeddedTexture>();
        using var probe = Build(room, texture => { used.Add(texture); return null; }, ownsPages: false);
        return used;
    }

    internal static RoomScene Build(GameMakerRoom room, Func<GameMakerEmbeddedTexture, Bitmap?> pageOf, bool ownsPages)
    {
        var build = new Builder(room, pageOf);
        if (room.Layers?.Count > 0)
        {
            // Higher depth is further back.
            foreach (var layer in room.Layers.Where(l => l is { IsVisible: true }).OrderByDescending(l => l.LayerDepth))
            {
                if (layer.InstancesData is { } instances)
                {
                    foreach (var o in instances.Instances) build.AddInstance(o);
                }
                else if (layer.AssetsData is { } assets)
                {
                    build.AddAssets(layer, assets);
                }
                else if (layer.BackgroundData is { } background)
                {
                    build.AddBackgroundLayer(layer, background);
                }
                else if (layer.LayerType == LayerType.Effect)
                {
                    build.AddNote(new Rect(0, build.Notes++ * 16, 200, 16), Loc.Current.Format("rooms.noteEffect", ("type", layer.EffectType?.Content ?? layer.EffectData?.EffectType?.Content ?? "?")));
                }
                else if (layer.TilesData is { Background.Texture: { } tpag })
                {
                    foreach (var (source, dest, flags) in TileRects(layer, tpag)) build.Add(tpag, (source, dest), null, flags);
                }
            }
        }
        else
        {
            // Game Maker Studio 1: backgrounds first (the ones set to foreground last), tiles and instances in between in one depth order.
            void AddBackground(Background background) =>
                build.AddPicture(background.BackgroundDefinition?.Texture, background.X, background.Y, background.Stretch, (background.TiledHorizontally, background.TiledVertically), 0);

            foreach (var background in (room.Backgrounds ?? []).Where(b => b is { Enabled: true, Foreground: false })) AddBackground(background);
            var drawn = (room.Tiles ?? []).Select(t => (Depth: t.TileDepth, Draw: (Action)(() => build.AddTile(t, 0, 0, t))))
                .Concat((room.GameObjects ?? []).Select(o => (Depth: o.ObjectDefinition?.Depth ?? 0, Draw: (Action)(() => build.AddInstance(o)))));
            foreach (var (_, draw) in drawn.OrderByDescending(d => d.Depth)) draw();
            foreach (var background in (room.Backgrounds ?? []).Where(b => b is { Enabled: true, Foreground: true })) AddBackground(background);
        }

        return new RoomScene { Width = room.Width, Height = room.Height, Items = build.Items, Skipped = build.Skipped, OwnsPages = ownsPages, Markers = build.Markers, Tinted = build.Tinted, FillColor = room.Layers?.Count > 0 ? build.Fill : room.DrawBackgroundColor ? room.BackgroundColor : null };
    }

    // The renderer mirrors, flips and turns a piece about its own centre. A piece that is not the whole cell must therefore sit where the transformed cell puts it:
    // its centre moves about the cell's centre in the same order (mirror, flip, quarter turn).
    private static Rect AfterFlags(Rect piece, Rect cell, int flags)
    {
        var (vx, vy) = (piece.Center.X - cell.Center.X, piece.Center.Y - cell.Center.Y);
        if ((flags & 1) != 0) vx = -vx;
        if ((flags & 2) != 0) vy = -vy;
        if ((flags & 4) != 0) (vx, vy) = (-vy, vx);
        var centre = new Point(cell.Center.X + vx, cell.Center.Y + vy);
        return new Rect(centre.X - piece.Width / 2, centre.Y - piece.Height / 2, piece.Width, piece.Height);
    }

    internal static IEnumerable<(Rect Source, Rect Dest, int Flags)> TileRects(Layer layer, GameMakerTexturePageItem tpag)
    {
        // The page may hold the sheet without its empty margin (it starts at Target): a cell is cut to what is stored and moved back by that margin.
        var stored = new Rect(tpag.TargetX, tpag.TargetY, tpag.SourceWidth, tpag.SourceHeight);
        if (layer.TilesData is not { Background: { } bg, TileData: { } rows }) yield break;
        int tw = (int)bg.GMS2TileWidth, th = (int)bg.GMS2TileHeight, bx = (int)bg.GMS2OutputBorderX, by = (int)bg.GMS2OutputBorderY, columns = Math.Max(1, (int)bg.GMS2TileColumns);
        for (var y = 0; y < rows.Length; y++)
        {
            for (var x = 0; x < rows[y].Length; x++)
            {
                var index = (int)(rows[y][x] & TileIndexMask);
                if (index == 0) continue;
                var source = new Rect(index % columns * (tw + 2 * bx) + bx, index / columns * (th + 2 * by) + by, tw, th);
                var dest = new Rect(layer.XOffset + x * tw, layer.YOffset + y * th, tw, th);
                var cut = source.Intersect(stored);
                if (cut.Width <= 0 || cut.Height <= 0) continue;
                var flags = (int)((rows[y][x] >> 28) & 7);
                var piece = new Rect(dest.X + cut.X - source.X, dest.Y + cut.Y - source.Y, cut.Width, cut.Height);
                yield return (cut.Translate(new Vector(-tpag.TargetX, -tpag.TargetY)), flags == 0 ? piece : AfterFlags(piece, dest, flags), flags);
            }
        }
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
