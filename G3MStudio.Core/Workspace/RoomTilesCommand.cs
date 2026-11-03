using System.Globalization;
using G3MLib.DataFile;

namespace G3MStudio.Core.Workspace;

/// <summary>Sets cells of a tile layer to tile values (0 is empty; the low 19 bits are the tile index, the rest are flip and rotate flags). One stroke or one fill is one command.</summary>
public sealed class RoomTilesCommand : EditCommand
{
    public const string Name = "roomTiles";

    private readonly string _room;
    private readonly uint _layerId;
    private readonly IReadOnlyList<(int X, int Y, uint Value)> _cells;
    private uint[] _old = [];

    public RoomTilesCommand(string room, uint layerId, IReadOnlyList<(int X, int Y, uint Value)> cells)
    {
        (_room, _layerId, _cells) = (room, layerId, cells);
        Args = new Dictionary<string, string>
        {
            ["room"] = room,
            ["layer"] = layerId.ToString(CultureInfo.InvariantCulture),
            ["cells"] = string.Join(';', cells.Select(c => string.Create(CultureInfo.InvariantCulture, $"{c.X},{c.Y},{c.Value}"))),
        };
    }

    public override string Kind => Name;

    public override string Editor => "rooms";

    public override IReadOnlyDictionary<string, string> Args { get; }

    public override IReadOnlyList<ProjectTouch>? Touches => [new("Rooms", _room)];

    public static RoomTilesCommand FromArgs(IReadOnlyDictionary<string, string> a) => new(a["room"], uint.Parse(a["layer"], CultureInfo.InvariantCulture),
        [.. a["cells"].Split(';', StringSplitOptions.RemoveEmptyEntries).Select(c => c.Split(',')).Select(p => (int.Parse(p[0], CultureInfo.InvariantCulture), int.Parse(p[1], CultureInfo.InvariantCulture), uint.Parse(p[2], CultureInfo.InvariantCulture)))]);

    private uint[][] Rows(GameMakerData data, out Action changed)
    {
        var room = data.Rooms.FirstOrDefault(r => r.Name.Content == _room) ?? throw new InvalidOperationException($"Room '{_room}' does not exist.");
        var tiles = room.Layers?.FirstOrDefault(l => l.LayerId == _layerId)?.TilesData ?? throw new InvalidOperationException($"Room '{_room}' has no tile layer {_layerId}.");
        changed = tiles.TileDataUpdated;
        return tiles.TileData;
    }

    public override void Apply(GameMakerData data)
    {
        var rows = Rows(data, out var changed);
        foreach (var (x, y, _) in _cells)
            if (y < 0 || y >= rows.Length || x < 0 || x >= rows[y].Length) throw new InvalidOperationException($"Cell {x},{y} is outside the tile layer.");
        _old = [.. _cells.Select(c => rows[c.Y][c.X])];
        for (var i = 0; i < _cells.Count; i++) rows[_cells[i].Y][_cells[i].X] = _cells[i].Value;
        changed();
    }

    public override void Revert(GameMakerData data)
    {
        var rows = Rows(data, out var changed);
        for (var i = _cells.Count - 1; i >= 0; i--) rows[_cells[i].Y][_cells[i].X] = _old[i];
        changed();
    }

    /// <summary>The cells a flood fill from (x, y) would change: the connected cells that hold the same tile index as the start (flags ignored), up to the layer edges.</summary>
    public static List<(int X, int Y, uint Value)> FloodFill(uint[][] rows, int x, int y, uint value)
    {
        const uint indexMask = 0x0007FFFF;
        var result = new List<(int, int, uint)>();
        if (y < 0 || y >= rows.Length || x < 0 || x >= rows[y].Length) return result;
        var target = rows[y][x] & indexMask;
        if (target == (value & indexMask) && rows[y][x] == value) return result;
        var seen = new HashSet<(int, int)> { (x, y) };
        var queue = new Queue<(int X, int Y)>([(x, y)]);
        while (queue.Count > 0)
        {
            var (cx, cy) = queue.Dequeue();
            result.Add((cx, cy, value));
            foreach (var (nx, ny) in new[] { (cx + 1, cy), (cx - 1, cy), (cx, cy + 1), (cx, cy - 1) })
                if (ny >= 0 && ny < rows.Length && nx >= 0 && nx < rows[ny].Length && (rows[ny][nx] & indexMask) == target && seen.Add((nx, ny))) queue.Enqueue((nx, ny));
        }

        return result;
    }
}
