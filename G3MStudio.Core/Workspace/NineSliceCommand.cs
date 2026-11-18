using System.Globalization;
using G3MLib.DataFile;
using G3MLib.DataFile.Models;

namespace G3MStudio.Core.Workspace;

/// <summary>The four nine-slice distances from the edges of the sprite.</summary>
public readonly record struct SliceEdges(int Left, int Top, int Right, int Bottom);

/// <summary>
/// Sets the nine-slice guides of a sprite: the distances from the four edges, whether the slice is on, and the tile mode of the five parts
/// (left, top, right, bottom, centre). Only sprites saved as version 3 or newer keep a nine-slice block.
/// </summary>
public sealed class NineSliceCommand(string sprite, bool enabled, SliceEdges edges, IReadOnlyList<int>? tiles = null) : EditCommand
{
    public const string Name = "nineSlice";

    private GameMakerSprite.NineSlice? _old;
    private (int, int, int, int, bool, GameMakerSprite.NineSlice.TileMode[]) _oldValues;

    public override string Kind => Name;

    public override string Editor => "sprites";

    public override IReadOnlyDictionary<string, string> Args { get; } = new Dictionary<string, string>
    {
        ["sprite"] = sprite,
        ["enabled"] = enabled ? "1" : "0",
        ["edges"] = string.Join(',', edges.Left, edges.Top, edges.Right, edges.Bottom),
        ["tiles"] = string.Join(',', tiles ?? []),
    };

    public override IReadOnlyList<ProjectTouch>? Touches { get; } = [new("Sprites", sprite)];

    public override bool ChangesNames => false;

    public static NineSliceCommand FromArgs(IReadOnlyDictionary<string, string> a)
    {
        var e = a["edges"].Split(',').Select(s => int.Parse(s, CultureInfo.InvariantCulture)).ToArray();
        var tiles = a["tiles"].Length == 0 ? null : a["tiles"].Split(',').Select(s => int.Parse(s, CultureInfo.InvariantCulture)).ToArray();
        return new(a["sprite"], a["enabled"] == "1", new SliceEdges(e[0], e[1], e[2], e[3]), tiles);
    }

    private static GameMakerSprite Find(GameMakerData data, string name) => data.Sprites.FirstOrDefault(s => s.Name.Content == name) ?? throw new InvalidOperationException($"Sprite '{name}' does not exist.");

    public override void Apply(GameMakerData data)
    {
        var target = Find(data, sprite);
        if (target.SVersion < 3) throw new InvalidOperationException($"Sprite '{sprite}' is saved in a format without nine-slice data.");
        _old = target.V3NineSlice;
        if (_old is { } o) _oldValues = (o.Left, o.Top, o.Right, o.Bottom, o.Enabled, [.. o.TileModes]);
        var slice = target.V3NineSlice ??= new GameMakerSprite.NineSlice();
        (slice.Left, slice.Top, slice.Right, slice.Bottom, slice.Enabled) = (edges.Left, edges.Top, edges.Right, edges.Bottom, enabled);
        foreach (var (mode, i) in (tiles ?? []).Take(slice.TileModes.Length).Select((v, i) => (v, i))) slice.TileModes[i] = (GameMakerSprite.NineSlice.TileMode)mode;
    }

    public override void Revert(GameMakerData data)
    {
        var target = Find(data, sprite);
        if (_old is not { } old)
        {
#pragma warning disable CS8625 // the library declares the block non-nullable but reads and writes null for "no nine-slice"
            target.V3NineSlice = null;
#pragma warning restore CS8625
            return;
        }

        (old.Left, old.Top, old.Right, old.Bottom, old.Enabled) = (_oldValues.Item1, _oldValues.Item2, _oldValues.Item3, _oldValues.Item4, _oldValues.Item5);
        _oldValues.Item6.CopyTo(old.TileModes, 0);
        target.V3NineSlice = old;
    }
}
