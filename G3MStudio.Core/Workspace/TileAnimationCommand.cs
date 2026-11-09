using System.Globalization;
using G3MLib.DataFile;
using G3MLib.DataFile.Models;

namespace G3MStudio.Core.Workspace;

/// <summary>
/// Edits the animated tiles of a tile set. The file keeps <c>ItemsPerTileCount</c> tile ids for every tile: the frames the tile cycles through.
/// Frames sets the frames of one tile (a shorter list repeats its last frame, a longer one grows the count for every tile); Speed sets the frame
/// length in microseconds. Undo restores the id list, the count and the length.
/// </summary>
public sealed class TileAnimationCommand(string set, string operation, int tile = 0, IReadOnlyList<int>? frames = null, long length = 0) : EditCommand
{
    public const string Name = "tileAnimation";
    public const string Frames = "frames";
    public const string Speed = "speed";

    private uint[]? _ids;
    private uint _items;
    private long _length;

    public override string Kind => Name;

    public override string Editor => "tilesets";

    public override bool ChangesNames => false;

    public override IReadOnlyDictionary<string, string> Args { get; } = new Dictionary<string, string>
    {
        ["set"] = set,
        ["operation"] = operation,
        ["tile"] = tile.ToString(CultureInfo.InvariantCulture),
        ["frames"] = string.Join(',', frames ?? []),
        ["length"] = length.ToString(CultureInfo.InvariantCulture),
    };

    public override IReadOnlyList<ProjectTouch>? Touches { get; } = [new("Backgrounds", set)];

    public static TileAnimationCommand FromArgs(IReadOnlyDictionary<string, string> a) => new(a["set"], a["operation"], int.Parse(a["tile"], CultureInfo.InvariantCulture),
        a["frames"].Length == 0 ? null : [.. a["frames"].Split(',').Select(s => int.Parse(s, CultureInfo.InvariantCulture))], long.Parse(a["length"], CultureInfo.InvariantCulture));

    /// <summary>The frames of every tile as rows of <paramref name="items"/> ids, or null when the list does not hold a whole number of rows.</summary>
    public static List<uint[]>? Rows(IReadOnlyList<uint> ids, int items)
    {
        if (items < 1 || ids.Count % items != 0) return null;
        return [.. Enumerable.Range(0, ids.Count / items).Select(r => ids.Skip(r * items).Take(items).ToArray())];
    }

    /// <summary>A row of exactly <paramref name="items"/> ids made from <paramref name="frames"/>, the last frame repeated to fill it.</summary>
    public static uint[] Pad(IReadOnlyList<int> frames, int items) => [.. Enumerable.Range(0, items).Select(i => (uint)frames[Math.Min(i, frames.Count - 1)])];

    private GameMakerBackground Find(GameMakerData data) => data.Backgrounds.FirstOrDefault(b => b.Name.Content == set) ?? throw new InvalidOperationException($"Tile set '{set}' does not exist.");

    public override void Apply(GameMakerData data)
    {
        var target = Find(data);
        _ids = [.. target.GMS2TileIds.Select(t => t.ID)];
        (_items, _length) = (target.GMS2ItemsPerTileCount, target.GMS2FrameLength);
        if (operation == Speed)
        {
            if (length <= 0) throw new InvalidOperationException("The frame length must be above zero.");
            target.GMS2FrameLength = length;
            return;
        }

        if (operation != Frames) throw new InvalidDataException($"Unknown tile animation operation '{operation}'.");
        if (frames is null or { Count: 0 }) throw new InvalidOperationException("A tile needs at least one frame.");
        if (frames.Any(f => f < 0)) throw new InvalidOperationException("Frame ids cannot be negative.");
        var rows = Rows(_ids, (int)_items) ?? throw new InvalidOperationException("The tile id list does not match the frames per tile.");
        if (tile < 0 || tile >= rows.Count) throw new InvalidOperationException($"Tile set '{set}' has no tile {tile}.");
        var items = Math.Max((int)_items, frames.Count);
        var padded = rows.Select(r => Pad([.. r.Select(v => (int)v)], items)).ToList();
        padded[tile] = Pad(frames, items);
        target.GMS2TileIds.Clear();
        foreach (var id in padded.SelectMany(r => r)) target.GMS2TileIds.Add(new GameMakerBackground.TileID { ID = id });
        target.GMS2ItemsPerTileCount = (uint)items;
    }

    public override void Revert(GameMakerData data)
    {
        if (_ids is null) throw new InvalidOperationException("The edit was never applied.");
        var target = Find(data);
        target.GMS2TileIds.Clear();
        foreach (var id in _ids) target.GMS2TileIds.Add(new GameMakerBackground.TileID { ID = id });
        (target.GMS2ItemsPerTileCount, target.GMS2FrameLength) = (_items, _length);
    }
}
