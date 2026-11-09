namespace G3MStudio.Core.Workspace;

public readonly record struct RoomBox(double Left, double Top, double Right, double Bottom)
{
    public double Width => Right - Left;

    public double Height => Bottom - Top;

    public double CenterX => (Left + Right) / 2;

    public double CenterY => (Top + Bottom) / 2;

    public static RoomBox Union(IEnumerable<RoomBox> boxes) => boxes.Aggregate((a, b) => new RoomBox(Math.Min(a.Left, b.Left), Math.Min(a.Top, b.Top), Math.Max(a.Right, b.Right), Math.Max(a.Bottom, b.Bottom)));
}

public enum AlignMode { Left, CenterHorizontal, Right, Top, MiddleVertical, Bottom }

public enum DistributeMode { Horizontal, Vertical }

/// <summary>Align and distribute for two or more instances, as pure functions over their bounds. Results are the whole-pixel move of each box, in input order.</summary>
public static class RoomAlign
{
    /// <summary>Moves every box so that the chosen edge or centre matches the one of the group's outer bounds.</summary>
    public static (int Dx, int Dy)[] Align(IReadOnlyList<RoomBox> boxes, AlignMode mode)
    {
        if (boxes.Count < 2) return [.. boxes.Select(_ => (0, 0))];
        var all = RoomBox.Union(boxes);
        return [.. boxes.Select(b => mode switch
        {
            AlignMode.Left => (Round(all.Left - b.Left), 0),
            AlignMode.Right => (Round(all.Right - b.Right), 0),
            AlignMode.CenterHorizontal => (Round(all.CenterX - b.CenterX), 0),
            AlignMode.Top => (0, Round(all.Top - b.Top)),
            AlignMode.Bottom => (0, Round(all.Bottom - b.Bottom)),
            _ => (0, Round(all.CenterY - b.CenterY)),
        })];
    }

    /// <summary>Keeps the two outermost boxes and spaces the others so that the gaps between neighbours are equal.</summary>
    public static (int Dx, int Dy)[] Distribute(IReadOnlyList<RoomBox> boxes, DistributeMode mode)
    {
        var result = new (int, int)[boxes.Count];
        if (boxes.Count < 3) return result;
        var horizontal = mode == DistributeMode.Horizontal;
        double Start(RoomBox b) => horizontal ? b.Left : b.Top;
        double Length(RoomBox b) => horizontal ? b.Width : b.Height;
        var order = Enumerable.Range(0, boxes.Count).OrderBy(i => Start(boxes[i]) + Length(boxes[i]) / 2).ToList();
        var span = Start(boxes[order[^1]]) + Length(boxes[order[^1]]) - Start(boxes[order[0]]);
        var gap = (span - order.Sum(i => Length(boxes[i]))) / (boxes.Count - 1);
        var at = Start(boxes[order[0]]);
        foreach (var i in order)
        {
            var move = Round(at - Start(boxes[i]));
            result[i] = horizontal ? (move, 0) : (0, move);
            at += Length(boxes[i]) + gap;
        }

        return result;
    }

    private static int Round(double value) => (int)Math.Round(value, MidpointRounding.AwayFromZero);
}
