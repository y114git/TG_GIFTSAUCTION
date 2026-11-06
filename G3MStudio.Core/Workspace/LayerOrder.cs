namespace G3MStudio.Core.Workspace;

/// <summary>Turns a drag in the layer list (front layer first, so ascending depth) into the depth changes that produce the new order.</summary>
public static class LayerOrder
{
    private const int MaxDepth = 16000;
    private const int Step = 100;

    /// <summary>
    /// The depth changes (layer id, new depth) that move the layer at <paramref name="from"/> to position <paramref name="to"/> of the list.
    /// Normally only the moved layer changes, to a depth between its new neighbours; when there is no room between them all layers are spaced again.
    /// </summary>
    public static List<(uint Id, int Depth)> Move(IReadOnlyList<(uint Id, int Depth)> layers, int from, int to)
    {
        if (from < 0 || from >= layers.Count || to < 0 || to >= layers.Count || from == to) return [];
        var moved = layers[from];
        var others = layers.Where((_, i) => i != from).ToList();
        var depth = Between(to > 0 ? others[to - 1].Depth : null, to < others.Count ? others[to].Depth : null);
        if (depth is { } free) return [(moved.Id, free)];
        others.Insert(to, moved);
        return Respace(others, layers);
    }

    private static int? Between(int? before, int? after) => (before, after) switch
    {
        (null, null) => 0,
        (null, { } a) => a - Step >= -MaxDepth ? a - Step : null,
        ({ } b, null) => b + Step <= MaxDepth ? b + Step : null,
        ({ } b, { } a) => a - b >= 2 ? b + (a - b) / 2 : null,
    };

    private static List<(uint Id, int Depth)> Respace(List<(uint Id, int Depth)> order, IReadOnlyList<(uint Id, int Depth)> original)
    {
        var step = Math.Min(Step, 2 * MaxDepth / Math.Max(1, order.Count));
        var start = -step * (order.Count - 1) / 2;
        var old = original.ToDictionary(l => l.Id, l => l.Depth);
        return [.. order.Select((l, i) => (l.Id, Depth: start + i * step)).Where(l => old[l.Id] != l.Depth)];
    }
}
