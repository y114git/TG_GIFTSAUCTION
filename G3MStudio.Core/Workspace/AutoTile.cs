namespace G3MStudio.Core.Workspace;

/// <summary>How a rule set keys its slots: the 47 blob shapes, the 16 edge shapes, or every one of the 256 neighbour masks.</summary>
public enum AutoTileLayout { Blob47, Edge16, Blob256 }

/// <summary>
/// Studio-side auto-tile rules for one tile set: a neighbour mask slot maps to a tile index. The data file stores no such rules, so they live in
/// Studio's own sidecar (<see cref="AutoTileStore"/>). <see cref="Terrain"/> groups rule sets: a cell of any rule set with the same terrain counts as
/// a neighbour, a cell of another terrain does not, so several rule sets on one layer do not fight.
/// </summary>
public sealed record AutoTileRuleSet(string Name, string TileSet, AutoTileLayout Layout, int Terrain, IReadOnlyDictionary<int, int> Slots)
{
    /// <summary>Tile index of a slot, or null when the slot has none.</summary>
    public int? Tile(int slot) => Slots.TryGetValue(slot, out var tile) && tile > 0 ? tile : null;

    /// <summary>The same rule set with one slot set; a tile of 0 or less clears the slot.</summary>
    public AutoTileRuleSet With(int slot, int tile)
    {
        var slots = new Dictionary<int, int>(Slots);
        if (tile > 0) slots[slot] = tile;
        else slots.Remove(slot);
        return this with { Slots = slots };
    }

    public static AutoTileRuleSet Empty(string name, string tileSet, AutoTileLayout layout, int terrain = 1) => new(name, tileSet, layout, terrain, new Dictionary<int, int>());
}

/// <summary>
/// The neighbour mask maths and the pure operations behind the auto-tile brush. Mask bits: N 1, NE 2, E 4, SE 8, S 16, SW 32, W 64, NW 128.
/// Tile index 0 is the empty cell, so a rule set cannot use it. Results are cell lists for <see cref="RoomTilesCommand"/>; flags are never written.
/// </summary>
public static class AutoTiler
{
    public const int N = 1, NE = 2, E = 4, SE = 8, S = 16, SW = 32, W = 64, NW = 128;
    private const uint IndexMask = 0x0007FFFF;
    private static readonly (int Dx, int Dy, int Bit)[] Around = [(0, -1, N), (1, -1, NE), (1, 0, E), (1, 1, SE), (0, 1, S), (-1, 1, SW), (-1, 0, W), (-1, -1, NW)];

    /// <summary>A corner only counts when both edges beside it are set; this folds the 256 masks into the 47 blob shapes.</summary>
    public static int Canonical(int mask)
    {
        var result = mask & (N | E | S | W);
        if ((mask & NE) != 0 && (mask & N) != 0 && (mask & E) != 0) result |= NE;
        if ((mask & SE) != 0 && (mask & E) != 0 && (mask & S) != 0) result |= SE;
        if ((mask & SW) != 0 && (mask & S) != 0 && (mask & W) != 0) result |= SW;
        if ((mask & NW) != 0 && (mask & W) != 0 && (mask & N) != 0) result |= NW;
        return result;
    }

    /// <summary>The four edges as a 4-bit number: N 1, E 2, S 4, W 8.</summary>
    public static int EdgeIndex(int mask) => (mask & N) | (mask & E) >> 1 | (mask & S) >> 2 | (mask & W) >> 3;

    public static int SlotOf(AutoTileLayout layout, int mask) => layout switch
    {
        AutoTileLayout.Blob47 => Canonical(mask),
        AutoTileLayout.Edge16 => EdgeIndex(mask),
        _ => mask & 0xFF,
    };

    /// <summary>Every slot of a layout in ascending order (47, 16 or 256 of them).</summary>
    public static IReadOnlyList<int> Slots(AutoTileLayout layout) => layout switch
    {
        AutoTileLayout.Blob47 => [.. Enumerable.Range(0, 256).Select(Canonical).Distinct().Order()],
        AutoTileLayout.Edge16 => [.. Enumerable.Range(0, 16)],
        _ => [.. Enumerable.Range(0, 256)],
    };

    /// <summary>Columns of the standard sheet of a layout: slots in ascending order, row by row.</summary>
    public static int TemplateColumns(AutoTileLayout layout) => layout switch { AutoTileLayout.Blob47 => 8, AutoTileLayout.Edge16 => 4, _ => 16 };

    /// <summary>
    /// Fills the slots from a tile set drawn like the standard sheet: the first slot is the tile <paramref name="first"/>, the next slots follow to the right
    /// and wrap after <paramref name="templateColumns"/> tiles, one row of the set down each time. Slots whose tile lies outside the set stay empty.
    /// </summary>
    public static AutoTileRuleSet AutoFill(AutoTileRuleSet rules, int first, int setColumns, int tileCount, int templateColumns = 0)
    {
        var columns = templateColumns > 0 ? templateColumns : TemplateColumns(rules.Layout);
        var slots = new Dictionary<int, int>();
        var list = Slots(rules.Layout);
        for (var i = 0; i < list.Count; i++)
        {
            var tile = first + i / columns * Math.Max(1, setColumns) + i % columns;
            if (tile > 0 && tile < tileCount) slots[list[i]] = tile;
        }

        return rules with { Slots = slots };
    }

    /// <summary>The mask of a cell: for each of the 8 neighbours whether the test calls it terrain.</summary>
    public static int Mask(int x, int y, Func<int, int, bool> isTerrain)
    {
        var mask = 0;
        foreach (var (dx, dy, bit) in Around)
            if (isTerrain(x + dx, y + dy)) mask |= bit;
        return mask;
    }

    /// <summary>Which rule sets count as the same terrain as <paramref name="rules"/>: same tile set and terrain value.</summary>
    private static List<AutoTileRuleSet> Group(AutoTileRuleSet rules, IEnumerable<AutoTileRuleSet> all) =>
        [.. all.Where(r => r.TileSet == rules.TileSet && r.Terrain == rules.Terrain).Append(rules).Distinct()];

    private static Dictionary<int, AutoTileRuleSet> Owners(IEnumerable<AutoTileRuleSet> group)
    {
        var owners = new Dictionary<int, AutoTileRuleSet>();
        foreach (var r in group)
            foreach (var tile in r.Slots.Values)
                if (tile > 0) owners.TryAdd(tile, r);
        return owners;
    }

    /// <summary>
    /// Paints (or erases) terrain on the cells, then retiles those cells and their 8 neighbours. A neighbour of the same terrain is retiled with the rule
    /// set that owns its tile. Cells outside the layer are ignored; they count as terrain only when <paramref name="outsideIsTerrain"/> is set.
    /// A cell whose slot has no tile in its rule set keeps its value. Returns only the cells whose value changes.
    /// </summary>
    public static List<(int X, int Y, uint Value)> Paint(uint[][] rows, AutoTileRuleSet rules, IEnumerable<AutoTileRuleSet> all, IEnumerable<(int X, int Y)> cells, bool erase, bool outsideIsTerrain = false)
    {
        var group = Group(rules, all);
        var owners = Owners(group);
        var painted = new Dictionary<(int, int), bool>();
        foreach (var (x, y) in cells)
            if (Inside(rows, x, y)) painted[(x, y)] = !erase;

        bool IsTerrain(int x, int y) => painted.TryGetValue((x, y), out var on) ? on : Inside(rows, x, y) ? IsOwned(rows[y][x], owners) : outsideIsTerrain;
        var touched = painted.Keys.SelectMany(c => Around.Select(a => (c.Item1 + a.Dx, c.Item2 + a.Dy)).Append(c)).Where(c => Inside(rows, c.Item1, c.Item2)).Distinct();
        var changes = new List<(int, int, uint)>();
        foreach (var (x, y) in touched)
        {
            if (!IsTerrain(x, y))
            {
                if (painted.ContainsKey((x, y)) && rows[y][x] != 0) changes.Add((x, y, 0u));
                continue;
            }

            var owner = painted.ContainsKey((x, y)) ? rules : owners[(int)(rows[y][x] & IndexMask)];
            if (owner.Tile(SlotOf(owner.Layout, Mask(x, y, IsTerrain))) is { } tile && Retiled(rows[y][x], tile) is var value && value != rows[y][x]) changes.Add((x, y, value));
        }

        return changes;
    }

    /// <summary>The cells a terrain fill from (x, y) would paint: the connected cells that are not terrain and hold the same tile as the start. Empty when the start is terrain.</summary>
    public static List<(int X, int Y)> FillArea(uint[][] rows, AutoTileRuleSet rules, IEnumerable<AutoTileRuleSet> all, int x, int y)
    {
        if (!Inside(rows, x, y)) return [];
        var owners = Owners(Group(rules, all));
        if (IsOwned(rows[y][x], owners)) return [];
        return [.. RoomTilesCommand.FloodFill(rows, x, y, uint.MaxValue).Select(c => (c.X, c.Y))];
    }

    /// <summary>Recomputes the tile of every terrain cell inside a rectangle of cells from the cells around it (Re-tile selection).</summary>
    public static List<(int X, int Y, uint Value)> Retile(uint[][] rows, AutoTileRuleSet rules, IEnumerable<AutoTileRuleSet> all, int x, int y, int width, int height, bool outsideIsTerrain = false)
    {
        var owners = Owners(Group(rules, all));
        bool IsTerrain(int cx, int cy) => Inside(rows, cx, cy) ? IsOwned(rows[cy][cx], owners) : outsideIsTerrain;
        var changes = new List<(int, int, uint)>();
        for (var cy = Math.Max(0, y); cy < Math.Min(rows.Length, y + height); cy++)
            for (var cx = Math.Max(0, x); cx < Math.Min(rows[cy].Length, x + width); cx++)
            {
                if (!IsTerrain(cx, cy)) continue;
                var owner = owners[(int)(rows[cy][cx] & IndexMask)];
                if (owner.Tile(SlotOf(owner.Layout, Mask(cx, cy, IsTerrain))) is { } tile && Retiled(rows[cy][cx], tile) is var value && value != rows[cy][cx]) changes.Add((cx, cy, value));
            }

        return changes;
    }

    // Only the tile index changes; the mirror, flip and rotate bits of the cell stay as they were.
    private static uint Retiled(uint old, int tile) => (old & ~IndexMask) | ((uint)tile & IndexMask);

    private static bool Inside(uint[][] rows, int x, int y) => y >= 0 && y < rows.Length && x >= 0 && x < rows[y].Length;

    private static bool IsOwned(uint value, Dictionary<int, AutoTileRuleSet> owners) => value != 0 && owners.ContainsKey((int)(value & IndexMask));
}
