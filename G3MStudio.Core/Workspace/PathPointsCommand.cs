using System.Globalization;
using G3MLib.DataFile;
using G3MLib.DataFile.Models;

namespace G3MStudio.Core.Workspace;

/// <summary>Adds, deletes or moves one point of a path. A path keeps at least one point.</summary>
public sealed class PathPointsCommand : EditCommand
{
    public const string Name = "pathPoints";
    public const string Add = "add";
    public const string Delete = "delete";
    public const string Move = "move";

    /// <summary>Sets the speed of one point; x holds the factor (1 is the normal path speed).</summary>
    public const string Speed = "speed";

    /// <summary>Moves every point of the path by (x, y).</summary>
    public const string Shift = "shift";

    private readonly string _path;
    private readonly string _operation;
    private readonly int _index;
    private readonly float _x;
    private readonly float _y;
    private GameMakerPath.PathPoint? _point;
    private (float X, float Y) _old;

    // index: the point to delete or move; for an add, the position the new point takes (-1 appends).
    public PathPointsCommand(string path, string operation, int index, float x = 0, float y = 0) => (_path, _operation, _index, _x, _y) = (path, operation, index, x, y);

    public override string Kind => Name;

    public override string Editor => "paths";

    public override IReadOnlyDictionary<string, string> Args => new Dictionary<string, string>
    {
        ["path"] = _path,
        ["operation"] = _operation,
        ["index"] = _index.ToString(CultureInfo.InvariantCulture),
        ["x"] = _x.ToString("R", CultureInfo.InvariantCulture),
        ["y"] = _y.ToString("R", CultureInfo.InvariantCulture),
    };

    public override IReadOnlyList<ProjectTouch>? Touches => [new("Paths", _path)];

    public static PathPointsCommand FromArgs(IReadOnlyDictionary<string, string> a) => new(a["path"], a["operation"], int.Parse(a["index"], CultureInfo.InvariantCulture),
        float.Parse(a["x"], CultureInfo.InvariantCulture), float.Parse(a["y"], CultureInfo.InvariantCulture));

    private GameMakerPath Find(GameMakerData data) => data.Paths.FirstOrDefault(p => p.Name.Content == _path) ?? throw new InvalidOperationException($"Path '{_path}' does not exist.");

    public override void Apply(GameMakerData data)
    {
        var points = Find(data).Points;
        if (_operation == Add)
        {
            _point = new GameMakerPath.PathPoint { X = _x, Y = _y };
            points.Insert(_index < 0 ? points.Count : Math.Min(_index, points.Count), _point);
            return;
        }

        if (_operation == Shift)
        {
            foreach (var each in points)
            {
                each.X += _x;
                each.Y += _y;
            }

            return;
        }

        if (_index < 0 || _index >= points.Count) throw new InvalidOperationException($"Path '{_path}' has no point {_index}.");
        _point = points[_index];
        if (_operation == Move)
        {
            _old = (_point.X, _point.Y);
            (_point.X, _point.Y) = (_x, _y);
        }
        else if (_operation == Speed)
        {
            _old = (_point.Speed, 0);
            _point.Speed = _x;
        }
        else if (_operation == Delete)
        {
            if (points.Count == 1) throw new InvalidOperationException("A path needs at least one point.");
            points.RemoveAt(_index);
        }
        else
        {
            throw new InvalidDataException($"Unknown path operation '{_operation}'.");
        }
    }

    public override void Revert(GameMakerData data)
    {
        var points = Find(data).Points;
        if (_operation == Shift)
        {
            foreach (var each in points)
            {
                each.X -= _x;
                each.Y -= _y;
            }

            return;
        }

        if (_point is null) throw new InvalidOperationException("The edit was never applied.");
        switch (_operation)
        {
            case Speed:
                _point.Speed = _old.X;
                break;
            case Move:
                (_point.X, _point.Y) = _old;
                break;
            case Delete:
                points.Insert(_index, _point);
                break;
            default:
                points.Remove(_point);
                break;
        }
    }
}
