using System.Globalization;
using G3MLib.DataFile;
using G3MLib.DataFile.Models;

namespace G3MStudio.Core.Workspace;

/// <summary>
/// Adds an action that runs an existing code entry to a moment of a timeline, or removes one action from a moment. A new action carries the fixed
/// values GameMaker writes for a code action (library 1, id 603, kind 7, applies to the instance).
/// </summary>
#pragma warning disable CA1001 // the action belongs to the moment or is put back into it; the model disposes it
public sealed class TimelineActionCommand : EditCommand
{
    public const string Name = "timelineAction";
    public const string Add = "add";
    public const string Remove = "remove";

    private readonly string _timeline;
    private readonly string _operation;
    private readonly int _moment;
    private readonly int _index;
    private readonly string _code;
    private GameMakerGameObject.EventAction? _action;
    private int _at;

    // moment: the moment to change. index: the action to remove. code: the code entry a new action runs.
    public TimelineActionCommand(string timeline, string operation, int moment, int index = -1, string code = "")
        => (_timeline, _operation, _moment, _index, _code) = (timeline, operation, moment, index, code);

    public override string Kind => Name;

    public override string Editor => "timelines";

    public override IReadOnlyDictionary<string, string> Args => new Dictionary<string, string>
    {
        ["timeline"] = _timeline,
        ["operation"] = _operation,
        ["moment"] = _moment.ToString(CultureInfo.InvariantCulture),
        ["index"] = _index.ToString(CultureInfo.InvariantCulture),
        ["code"] = _code,
    };

    public override IReadOnlyList<ProjectTouch>? Touches => [new("Timelines", _timeline)];

    public static TimelineActionCommand FromArgs(IReadOnlyDictionary<string, string> a) =>
        new(a["timeline"], a["operation"], int.Parse(a["moment"], CultureInfo.InvariantCulture), int.Parse(a["index"], CultureInfo.InvariantCulture), a["code"]);

    private GameMakerPointerList<GameMakerGameObject.EventAction> Actions(GameMakerData data)
    {
        var timeline = data.Timelines.FirstOrDefault(t => t.Name.Content == _timeline) ?? throw new InvalidOperationException($"Timeline '{_timeline}' does not exist.");
        return _moment >= 0 && _moment < timeline.Moments.Count ? timeline.Moments[_moment].Event : throw new InvalidOperationException($"Timeline '{_timeline}' has no moment {_moment}.");
    }

    public override void Apply(GameMakerData data)
    {
        var actions = Actions(data);
        if (_operation == Add)
        {
            var code = data.Code.FirstOrDefault(c => c.Name.Content == _code) ?? throw new InvalidOperationException($"Code entry '{_code}' does not exist.");
            _action = new GameMakerGameObject.EventAction
            {
                LibID = 1,
                ID = 603,
                Kind = 7,
                UseApplyTo = true,
                ExeType = 2,
                ActionName = data.Strings.MakeString("", out _),
                CodeId = code,
                ArgumentCount = 1,
                Who = -1,
            };
            _at = actions.Count;
            actions.Add(_action);
            return;
        }

        if (_operation != Remove) throw new InvalidDataException($"Unknown timeline operation '{_operation}'.");
        if (_index < 0 || _index >= actions.Count) throw new InvalidOperationException($"Moment {_moment} has no action {_index}.");
        (_action, _at) = (actions[_index], _index);
        actions.RemoveAt(_index);
    }

    public override void Revert(GameMakerData data)
    {
        var actions = Actions(data);
        if (_action is null) throw new InvalidOperationException("The edit was never applied.");
        if (_operation == Add) actions.Remove(_action);
        else actions.Insert(_at, _action);
    }
}
#pragma warning restore CA1001
