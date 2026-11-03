using System.Globalization;
using G3MLib.DataFile;
using G3MLib.DataFile.Models;

namespace G3MStudio.Core.Workspace;

#pragma warning disable CA1001 // the moment belongs to the timeline or is put back into it; the model disposes it
/// <summary>Adds an empty moment at a step to a timeline, or removes one moment with its actions. Undo puts the very same moment back.</summary>
public sealed class TimelineMomentCommand : EditCommand
{
    public const string Name = "timelineMoment";
    public const string Add = "add";
    public const string Remove = "remove";

    private readonly string _timeline;
    private readonly string _operation;
    private readonly int _index;
    private readonly uint _step;
    private GameMakerTimeline.GameMakerTimelineMoment? _moment;
    private int _at;

    // index: the moment to remove. step: the step of a new moment (an add appends it).
    public TimelineMomentCommand(string timeline, string operation, int index, uint step = 0) => (_timeline, _operation, _index, _step) = (timeline, operation, index, step);

    public override string Kind => Name;

    public override string Editor => "timelines";

    public override IReadOnlyDictionary<string, string> Args => new Dictionary<string, string>
    {
        ["timeline"] = _timeline,
        ["operation"] = _operation,
        ["index"] = _index.ToString(CultureInfo.InvariantCulture),
        ["step"] = _step.ToString(CultureInfo.InvariantCulture),
    };

    public override IReadOnlyList<ProjectTouch>? Touches => [new("Timelines", _timeline)];

    public static TimelineMomentCommand FromArgs(IReadOnlyDictionary<string, string> a) =>
        new(a["timeline"], a["operation"], int.Parse(a["index"], CultureInfo.InvariantCulture), uint.Parse(a["step"], CultureInfo.InvariantCulture));

    private GameMakerTimeline Find(GameMakerData data) => data.Timelines.FirstOrDefault(t => t.Name.Content == _timeline) ?? throw new InvalidOperationException($"Timeline '{_timeline}' does not exist.");

    public override void Apply(GameMakerData data)
    {
        var moments = Find(data).Moments;
        if (_operation == Add)
        {
            _moment = new GameMakerTimeline.GameMakerTimelineMoment(_step, []);
            _at = moments.Count;
            moments.Add(_moment);
            return;
        }

        if (_operation != Remove) throw new InvalidDataException($"Unknown timeline operation '{_operation}'.");
        if (_index < 0 || _index >= moments.Count) throw new InvalidOperationException($"Timeline '{_timeline}' has no moment {_index}.");
        (_moment, _at) = (moments[_index], _index);
        moments.RemoveAt(_index);
    }

    public override void Revert(GameMakerData data)
    {
        var moments = Find(data).Moments;
        if (_moment is null) throw new InvalidOperationException("The edit was never applied.");
        if (_operation == Add) moments.Remove(_moment);
        else moments.Insert(_at, _moment);
    }
}
#pragma warning restore CA1001
