namespace G3MStudio.Core.Settings;

/// <summary>
/// The math of "slide children into place": which items moved between two layouts and by how much they must start offset.
/// A drag that ends hands in the positions the items were <i>seen</i> at (the dragged tab under the pointer, the neighbours shifted aside),
/// so a drop settles from there and never replays the trip from where the drag began.
/// </summary>
public static class LayoutShifts
{
    /// <summary>Moves of less than this many pixels are not animated.</summary>
    public const double MinShift = 1;

    /// <summary>The positions an item moved from: the ones seen at the end of a drag when there are any, else the ones of the previous layout.</summary>
    public static IReadOnlyDictionary<T, (double X, double Y)> Origin<T>(IReadOnlyDictionary<T, (double X, double Y)> lastLayout, IReadOnlyDictionary<T, (double X, double Y)>? seenAtDrop) where T : notnull =>
        seenAtDrop is { Count: > 0 } ? seenAtDrop : lastLayout;

    /// <summary>The offset each moved item starts from (its old position minus its new one). New items and items that stayed are left out.</summary>
    public static List<(T Item, double Dx, double Dy)> Between<T>(IReadOnlyDictionary<T, (double X, double Y)> from, IReadOnlyDictionary<T, (double X, double Y)> to) where T : notnull
    {
        var moves = new List<(T, double, double)>();
        foreach (var (item, now) in to)
        {
            if (!from.TryGetValue(item, out var was)) continue;
            double dx = was.X - now.X, dy = was.Y - now.Y;
            if (Math.Abs(dx) + Math.Abs(dy) >= MinShift) moves.Add((item, dx, dy));
        }

        return moves;
    }
}
