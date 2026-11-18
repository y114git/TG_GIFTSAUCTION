using System.Globalization;
using G3MLib.DataFile;
using G3MLib.DataFile.Models;

namespace G3MStudio.Core.Workspace;

/// <summary>A point position (X, Value), or the four Bezier handle values, or for a type change X holds the curve type.</summary>
public readonly record struct CurveValue(float X = 0, float Value = 0, float[]? Handles = null);

/// <summary>
/// Edits the points of one channel of an animation curve: add a point, delete one, move one (the list stays sorted by X), set the four Bezier handle
/// values of one point, or set the channel's curve type. Undo puts back the list and the values.
/// </summary>
public sealed class CurvePointCommand(string curve, int channel, string operation, int index, CurveValue at = default) : EditCommand
{
    public const string Name = "curvePoint";
    public const string Add = "add";
    public const string Delete = "delete";
    public const string Move = "move";
    public const string Handles = "handles";

    /// <summary>Replaces the channel's points with a preset shape; x holds the preset number (see <see cref="CurvePresets"/>).</summary>
    public const string Preset = "preset";

    /// <summary>Sets the channel's curve type; x holds 0 (linear), 1 (smooth) or 2 (Bezier).</summary>
    public const string Type = "type";

    private GameMakerAnimationCurve.Channel.Point[]? _before;
    private (float X, float Value, float B0, float B1, float B2, float B3)[] _values = [];
    private GameMakerAnimationCurve.Channel.CurveType _type;

    public override string Kind => Name;

    public override string Editor => "curves";

    public override IReadOnlyDictionary<string, string> Args { get; } = new Dictionary<string, string>
    {
        ["curve"] = curve,
        ["channel"] = channel.ToString(CultureInfo.InvariantCulture),
        ["operation"] = operation,
        ["index"] = index.ToString(CultureInfo.InvariantCulture),
        ["x"] = at.X.ToString("R", CultureInfo.InvariantCulture),
        ["value"] = at.Value.ToString("R", CultureInfo.InvariantCulture),
        ["handles"] = string.Join(',', (at.Handles ?? []).Select(h => h.ToString("R", CultureInfo.InvariantCulture))),
    };

    public override IReadOnlyList<ProjectTouch>? Touches { get; } = [new("AnimationCurves", curve)];

    public override bool ChangesNames => false;

    public static CurvePointCommand FromArgs(IReadOnlyDictionary<string, string> a) => new(a["curve"], int.Parse(a["channel"], CultureInfo.InvariantCulture), a["operation"],
        int.Parse(a["index"], CultureInfo.InvariantCulture), new CurveValue(float.Parse(a["x"], CultureInfo.InvariantCulture), float.Parse(a["value"], CultureInfo.InvariantCulture),
        a["handles"].Length == 0 ? null : [.. a["handles"].Split(',').Select(s => float.Parse(s, CultureInfo.InvariantCulture))]));

    private GameMakerAnimationCurve.Channel Find(GameMakerData data)
    {
        var owner = data.AnimationCurves?.FirstOrDefault(c => c.Name.Content == curve) ?? throw new InvalidOperationException($"Animation curve '{curve}' does not exist.");
        return owner.Channels.ElementAtOrDefault(channel) ?? throw new InvalidOperationException($"Curve '{curve}' has no channel {channel}.");
    }

    public override void Apply(GameMakerData data)
    {
        var target = Find(data);
        var points = target.Points;
        _before = [.. points];
        _values = [.. points.Select(p => (p.X, p.Value, p.BezierX0, p.BezierY0, p.BezierX1, p.BezierY1))];
        _type = target.Curve;
        CurveOps.Run(operation, target, index, at);
    }

    public override void Revert(GameMakerData data)
    {
        if (_before is null) throw new InvalidOperationException("The edit was never applied.");
        var target = Find(data);
        target.Points.Clear();
        foreach (var (point, (px, pv, b0, b1, b2, b3)) in _before.Zip(_values))
        {
            (point.X, point.Value, point.BezierX0, point.BezierY0, point.BezierX1, point.BezierY1) = (px, pv, b0, b1, b2, b3);
            target.Points.Add(point);
        }

        target.Curve = _type;
    }
}

/// <summary>Ordering of curve points.</summary>
public static class CurveOrder
{
    /// <summary>Where a point at <paramref name="x"/> belongs in a list sorted by X: after every point at or before it.</summary>
    public static int SortedIndex(IEnumerable<float> xs, float x) => xs.Count(v => v <= x);
}

/// <summary>The point operations of <see cref="CurvePointCommand"/>.</summary>
internal static class CurveOps
{
    public static void Run(string operation, GameMakerAnimationCurve.Channel target, int index, CurveValue at)
    {
        var points = target.Points;
        if (operation == CurvePointCommand.Type)
        {
            target.Curve = (GameMakerAnimationCurve.Channel.CurveType)(int)at.X;
            return;
        }

        if (operation == CurvePointCommand.Preset)
        {
            points.Clear();
            var preset = (int)at.X;
            foreach (var (x, value) in CurvePresets.Points(preset)) points.Add(new GameMakerAnimationCurve.Channel.Point { X = x, Value = value });
            target.Curve = GameMakerAnimationCurve.Channel.CurveType.Linear;
            if (CurvePresets.Handles(preset) is not { } handles) return;
            target.Curve = GameMakerAnimationCurve.Channel.CurveType.Bezier;
            foreach (var (made, h) in points.Zip(handles)) (made.BezierX0, made.BezierY0, made.BezierX1, made.BezierY1) = (h[0], h[1], h[2], h[3]);
            return;
        }

        if (operation == CurvePointCommand.Add)
        {
            points.Insert(CurveOrder.SortedIndex(points.Select(p => p.X), at.X), new GameMakerAnimationCurve.Channel.Point { X = at.X, Value = at.Value });
            return;
        }

        if (index < 0 || index >= points.Count) throw new InvalidOperationException($"The channel has no point {index}.");
        var point = points[index];
        switch (operation)
        {
            case CurvePointCommand.Delete:
                points.RemoveAt(index);
                break;
            case CurvePointCommand.Move:
                points.RemoveAt(index);
                (point.X, point.Value) = (at.X, at.Value);
                points.Insert(CurveOrder.SortedIndex(points.Select(p => p.X), at.X), point);
                break;
            case CurvePointCommand.Handles when at.Handles is { Length: 4 } h:
                (point.BezierX0, point.BezierY0, point.BezierX1, point.BezierY1) = (h[0], h[1], h[2], h[3]);
                break;
            default:
                throw new InvalidDataException($"Unknown curve operation '{operation}'.");
        }
    }
}

/// <summary>Shapes from 0 to 1 over 0 to 1, sampled into straight segments: linear, ease in, ease out, ease in and out, step, bounce.</summary>
public static class CurvePresets
{
    public static readonly string[] Names = ["linear", "easeIn", "easeOut", "easeInOut", "step", "bounce", "bezierEaseIn", "bezierEaseOut", "bezierEaseInOut"];

    // Handle offsets (in x, in y, out x, out y) of the two points of a Bezier preset: the CSS cubic curves with their control points as offsets.
    private static readonly float[][][] BezierHandles =
    [
        [[0, 0, 0.42f, 0], [0, 0, 0, 0]],
        [[0, 0, 0, 0], [-0.42f, 0, 0, 0]],
        [[0, 0, 0.42f, 0], [-0.42f, 0, 0, 0]],
    ];

    /// <summary>The handles of a Bezier preset, or null for a preset made of straight segments.</summary>
    public static float[][]? Handles(int preset) => preset >= 6 && preset - 6 < BezierHandles.Length ? BezierHandles[preset - 6] : null;

    private const int Samples = 10;

    public static IEnumerable<(float X, float Value)> Points(int preset)
    {
        if (preset < 0 || preset >= Names.Length) throw new InvalidOperationException($"Unknown curve preset {preset}.");
        if (preset == 4) return [(0, 0), (0.5f, 0), (0.5f, 1), (1, 1)];
        if (preset >= 6) return [(0, 0), (1, 1)];
        return Enumerable.Range(0, Samples + 1).Select(i => { var t = i / (float)Samples; return (t, Ease(preset, t)); });
    }

    private static float Ease(int preset, float t) => preset switch
    {
        1 => t * t,
        2 => 1 - ((1 - t) * (1 - t)),
        3 => t < 0.5f ? 2 * t * t : 1 - (2 * (1 - t) * (1 - t)),
        5 => 1 - (MathF.Abs(MathF.Cos(t * MathF.PI * 2.5f)) * (1 - t)),
        _ => t,
    };
}
