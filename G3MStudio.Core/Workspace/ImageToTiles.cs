using System.Security.Cryptography;
using G3MLib.DataFile;
using G3MLib.DataFile.Models;

namespace G3MStudio.Core.Workspace;

/// <summary>What a conversion found: the cells of the new layer (0 is empty), how many cells hold a tile, and the cells with pixels that match no tile.</summary>
public sealed record TileMatch(uint[][] Cells, int Matched, IReadOnlyList<(int X, int Y)> Unmatched, bool Cropped);

/// <summary>
/// Builds a tile layer from a picture by matching 1:1: every cell of the layer (one tile in size, from the top left of the room) is compared pixel by pixel with the tiles of a tile set.
/// A cell with no visible pixel stays empty, and so does a cell that matches no tile (it is reported). Tile 0 is not matched because a cell value of 0 means empty.
/// Flipped and rotated tiles are not tried, and a picture larger than the room is cut at the room edge.
/// </summary>
public static class ImageToTiles
{
    public static TileMatch Match(GameMakerRoom room, GameMakerBackground set, PixelBuffer picture)
    {
        var (tw, th) = ((int)set.GMS2TileWidth, (int)set.GMS2TileHeight);
        if (tw < 1 || th < 1 || set.Texture is not { } item) throw new InvalidOperationException($"'{set.Name.Content}' is not a tile set with a picture.");
        var sheet = FramePicture.ReadItem(item, set.Name.Content ?? "");
        var known = new Dictionary<string, int>();
        for (var i = (int)set.GMS2TileCount - 1; i >= 1; i--)
        {
            var (x, y, w, h) = TileGeometry.RectOf(set, i);
            if (x + w <= sheet.Width && y + h <= sheet.Height && Key(sheet, x, y, w, h) is { } key) known[key] = i; // the lowest index wins for equal tiles
        }

        var (columns, rows) = (Math.Max(1, (int)Math.Ceiling((double)room.Width / tw)), Math.Max(1, (int)Math.Ceiling((double)room.Height / th)));
        var cells = new uint[rows][];
        var unmatched = new List<(int, int)>();
        var matched = 0;
        for (var y = 0; y < rows; y++)
        {
            cells[y] = new uint[columns];
            for (var x = 0; x < columns; x++)
            {
                if (Key(picture, x * tw, y * th, tw, th) is not { } key) continue;
                if (known.TryGetValue(key, out var index))
                {
                    cells[y][x] = (uint)index;
                    matched++;
                }
                else
                {
                    unmatched.Add((x, y));
                }
            }
        }

        return new TileMatch(cells, matched, unmatched, picture.Width > columns * tw || picture.Height > rows * th);
    }

    // The hash of a tile-sized block (pixels outside the picture count as clear; the colour of a clear pixel is ignored), or null when no pixel is visible.
    private static string? Key(PixelBuffer p, int x, int y, int w, int h)
    {
        var block = new byte[w * h * 4];
        var visible = false;
        for (var r = 0; r < h; r++)
            for (var c = 0; c < w; c++)
            {
                if (!p.Contains(x + c, y + r)) continue;
                var at = ((y + r) * p.Width + x + c) * 4;
                if (p.Data[at + 3] == 0) continue;
                Buffer.BlockCopy(p.Data, at, block, (r * w + c) * 4, 4);
                visible = true;
            }

        return visible ? Convert.ToHexString(SHA256.HashData(block)) : null;
    }

    /// <summary>The edit that adds the tile layer and fills it, as one undo step. The layer goes in front of the others, like Add Layer in the room editor.</summary>
    public static EditCommand Build(GameMakerData data, string room, string tileSet, string layerName, TileMatch match)
    {
        var target = data.Rooms.FirstOrDefault(r => r.Name.Content == room) ?? throw new InvalidOperationException($"Room '{room}' does not exist.");
        var layers = target.Layers ?? throw new InvalidOperationException($"Room '{room}' has no layers.");
        var id = layers.Select(l => l.LayerId).DefaultIfEmpty().Max() + 1;
        var front = Math.Max(-16000, layers.Select(l => l.LayerDepth).DefaultIfEmpty(0).Min() - 100);
        var cells = new List<(int, int, uint)>();
        foreach (var (row, y) in match.Cells.Select((row, y) => (row, y)))
            foreach (var (cell, x) in row.Select((cell, x) => (cell, x)))
                if (cell != 0) cells.Add((x, y, cell));
        var add = new RoomLayerCommand(room, RoomLayerCommand.AddTiles, id, layerName, front, tileSet);
        return cells.Count == 0 ? add : new GroupCommand([add, new RoomTilesCommand(room, id, cells)]);
    }

    /// <summary>A layer name no layer of the room has: the base name, then base_1, base_2 and so on.</summary>
    public static string FreeLayerName(GameMakerRoom room, string baseName)
    {
        var taken = (room.Layers ?? []).Select(l => l.LayerName.Content).ToHashSet();
        var name = baseName;
        for (var i = 1; taken.Contains(name); i++) name = $"{baseName}_{i}";
        return name;
    }
}
