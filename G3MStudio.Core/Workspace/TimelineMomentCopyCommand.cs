using System.Globalization;
using G3MLib.DataFile;
using G3MLib.DataFile.Models;

namespace G3MStudio.Core.Workspace;

#pragma warning disable CA1001 // the moment belongs to a timeline or is put back into one; the model disposes it
/// <summary>
/// Copies a moment of a timeline to the same or another timeline, or moves it (cut). The new moment has the same step and runs the same code entries,
/// so an edit of that code shows in both moments. With <c>copyCode</c> the copy gets code entries of its own (the GML is decompiled and compiled again, new
/// entries named after the target timeline and step), so the two moments are independent. One undo step either way.
/// </summary>
public sealed class TimelineMomentCopyCommand(string from, int index, string to, bool cut, bool copyCode = false) : EditCommand
{
    public const string Name = "timelineMomentCopy";

    private GameMakerTimeline.GameMakerTimelineMoment? _copy, _removed;
    private int _removedAt;
    private EditCodeCommand.Snapshot? _codeBefore;

    public override string Kind => Name;

    public override string Editor => "timelines";

    public override IReadOnlyDictionary<string, string> Args { get; } = new Dictionary<string, string>
    {
        ["from"] = from,
        ["index"] = index.ToString(CultureInfo.InvariantCulture),
        ["to"] = to,
        ["cut"] = cut ? "true" : "false",
        ["copyCode"] = copyCode ? "true" : "false",
    };

    public override IReadOnlyList<ProjectTouch>? Touches { get; } = copyCode
        ? [new("Timelines", from), new("Timelines", to), new("CodeEntries", null), new("Strings", null), new("Variables", null), new("Functions", null)]
        : [new("Timelines", from), new("Timelines", to)];

    public override bool ChangesNames => false;

    public static TimelineMomentCopyCommand FromArgs(IReadOnlyDictionary<string, string> a) =>
        new(a["from"], int.Parse(a["index"], CultureInfo.InvariantCulture), a["to"], a["cut"] == "true", a.GetValueOrDefault("copyCode") == "true");

    private static GameMakerTimeline Find(GameMakerData data, string name) => data.Timelines.FirstOrDefault(t => t.Name.Content == name) ?? throw new InvalidOperationException($"Timeline '{name}' does not exist.");

    public override void Apply(GameMakerData data)
    {
        var source = Find(data, from).Moments;
        var target = Find(data, to).Moments;
        if (index < 0 || index >= source.Count) throw new InvalidOperationException($"Timeline '{from}' has no moment {index}.");
        var moment = source[index];
        var actions = (moment.Event ?? []).Select(a => Clone(a, data)).ToList();
        var copy = new GameMakerTimeline.GameMakerTimelineMoment(moment.Step, []);
        foreach (var action in actions) copy.Event.Add(action);
        if (copyCode) _codeBefore = MomentCodeCopy.Run(data, actions, from, to, copy.Step);
        if (cut)
        {
            (_removed, _removedAt) = (moment, index);
            source.RemoveAt(index);
        }

        target.Insert(target.Count(m => m.Step <= copy.Step), copy); // moments stay sorted by step
        _copy = copy;
    }

    private static GameMakerGameObject.EventAction Clone(GameMakerGameObject.EventAction a, GameMakerData data) => new()
    {
        LibID = a.LibID,
        ID = a.ID,
        Kind = a.Kind,
        UseRelative = a.UseRelative,
        IsQuestion = a.IsQuestion,
        UseApplyTo = a.UseApplyTo,
        ExeType = a.ExeType,
        ActionName = a.ActionName ?? data.Strings.MakeString("", out _),
        CodeId = a.CodeId,
        ArgumentCount = a.ArgumentCount,
        Who = a.Who,
        Relative = a.Relative,
    };

    public override void Revert(GameMakerData data)
    {
        if (_copy is null) throw new InvalidOperationException("The edit was never applied.");
        Find(data, to).Moments.Remove(_copy);
        if (_removed is not null) Find(data, from).Moments.Insert(_removedAt, _removed);
        _codeBefore?.Restore();
    }
}
#pragma warning restore CA1001
