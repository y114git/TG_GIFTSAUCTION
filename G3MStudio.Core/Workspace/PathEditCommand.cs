using System.Globalization;
using G3MLib.DataFile;
using G3MLib.DataFile.Models;

namespace G3MStudio.Core.Workspace;

/// <summary>
/// Edits several points of a path in one undo step: reverse the order, flip or mirror the picked points about their own centre, move them by (x, y),
/// delete them, or set the closed or smooth flag (x is 1 or 0). An empty selection means every point. A path keeps at least one point.
/// </summary>
public sealed class PathEditCommand(string path, string operation, IReadOnlyList<int> selection, float x = 0, float y = 0) : EditCommand
{
    public const string Name = "pathEdit";
    public const string Reverse = "reverse";

    /// <summary>Turns the picked points over left to right, about the centre of their bounds.</summary>
    public const string FlipX = "flipX";

    /// <summary>Turns the picked points over top to bottom, about the centre of their bounds.</summary>
    public const string FlipY = "flipY";
    public const string Shift = "shift";
    public const string Delete = "delete";
    public const string Closed = "closed";
    public const string Smooth = "smooth";

    private GameMakerPath.PathPoint[]? _before;
    private (float X, float Y)[] _positions = [];
    private (bool Closed, bool Smooth) _flags;

    public override string Kind => Name;

    public override string Editor => "paths";

    public override IReadOnlyDictionary<string, string> Args { get; } = new Dictionary<string, string>
    {
        ["path"] = path,
        ["operation"] = operation,
        ["selection"] = string.Join(',', selection),
        ["x"] = x.ToString("R", CultureInfo.InvariantCulture),
        ["y"] = y.ToString("R", CultureInfo.InvariantCulture),
    };

    public override IReadOnlyList<ProjectTouch>? Touches { get; } = [new("Paths", path)];

    public override bool ChangesNames => false;

    public static PathEditCommand FromArgs(IReadOnlyDictionary<string, string> a) => new(a["path"], a["operation"],
        a["selection"].Length == 0 ? [] : [.. a["selection"].Split(',').Select(s => int.Parse(s, CultureInfo.InvariantCulture))],
        float.Parse(a["x"], CultureInfo.InvariantCulture), float.Parse(a["y"], CultureInfo.InvariantCulture));

    public override void Apply(GameMakerData data)
    {
        var target = data.Paths.FirstOrDefault(p => p.Name.Content == path) ?? throw new InvalidOperationException($"Path '{path}' does not exist.");
        var points = target.Points;
        _before = [.. points];
        _positions = [.. points.Select(p => (p.X, p.Y))];
        _flags = (target.IsClosed, target.IsSmooth);
        var picked = (selection.Count == 0 ? Enumerable.Range(0, points.Count) : selection).Distinct().Where(i => i >= 0 && i < points.Count).Select(i => points[i]).ToList();
        if (picked.Count == 0 && operation != Closed && operation != Smooth) throw new InvalidOperationException("No point is picked.");
        PathOps.Run(operation, target, picked, x, y);
    }

    public override void Revert(GameMakerData data)
    {
        if (_before is null) throw new InvalidOperationException("The edit was never applied.");
        var target = data.Paths.FirstOrDefault(p => p.Name.Content == path) ?? throw new InvalidOperationException($"Path '{path}' does not exist.");
        target.Points.Clear();
        foreach (var (point, at) in _before.Zip(_positions))
        {
            (point.X, point.Y) = at;
            target.Points.Add(point);
        }

        (target.IsClosed, target.IsSmooth) = _flags;
    }
}

/// <summary>Turns points over about the centre of their own bounds.</summary>
public static class PathTurn
{
    /// <summary>The coordinate of a point after turning it over about the centre of <paramref name="low"/> to <paramref name="high"/>.</summary>
    public static float Flip(float value, float low, float high) => low + high - value;

    public static void Over(List<GameMakerPath.PathPoint> picked, bool horizontal)
    {
        var (low, high) = horizontal ? (picked.Min(p => p.X), picked.Max(p => p.X)) : (picked.Min(p => p.Y), picked.Max(p => p.Y));
        foreach (var p in picked)
        {
            if (horizontal) p.X = Flip(p.X, low, high);
            else p.Y = Flip(p.Y, low, high);
        }
    }
}

/// <summary>The point operations of <see cref="PathEditCommand"/>.</summary>
internal static class PathOps
{
    public static void Run(string operation, GameMakerPath target, List<GameMakerPath.PathPoint> picked, float x, float y)
    {
        var points = target.Points;
        switch (operation)
        {
            case PathEditCommand.Reverse:
                var reversed = points.Reverse().ToList();
                points.Clear();
                foreach (var p in reversed) points.Add(p);
                break;
            case PathEditCommand.FlipX:
                PathTurn.Over(picked, true);
                break;
            case PathEditCommand.FlipY:
                PathTurn.Over(picked, false);
                break;
            case PathEditCommand.Shift:
                foreach (var p in picked)
                {
                    p.X += x;
                    p.Y += y;
                }

                break;
            case PathEditCommand.Delete:
                if (picked.Count >= points.Count) throw new InvalidOperationException("A path needs at least one point.");
                foreach (var p in picked) points.Remove(p);
                break;
            case PathEditCommand.Closed:
                target.IsClosed = x != 0;
                break;
            case PathEditCommand.Smooth:
                target.IsSmooth = x != 0;
                break;
            default:
                throw new InvalidDataException($"Unknown path operation '{operation}'.");
        }
    }
}
