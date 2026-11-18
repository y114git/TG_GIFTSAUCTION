using System.Globalization;
using G3MLib.DataFile;
using G3MLib.DataFile.Models;

namespace G3MStudio.Core.Workspace;

/// <summary>Moves every moment of a timeline at or after a step by a number of steps (negative moves earlier, never below step 0). One undo step.</summary>
public sealed class TimelineShiftCommand(string timeline, uint from, int delta) : EditCommand
{
    public const string Name = "timelineShift";

    private uint[] _before = [];

    public override string Kind => Name;

    public override string Editor => "timelines";

    public override IReadOnlyDictionary<string, string> Args { get; } = new Dictionary<string, string>
    {
        ["timeline"] = timeline,
        ["from"] = from.ToString(CultureInfo.InvariantCulture),
        ["delta"] = delta.ToString(CultureInfo.InvariantCulture),
    };

    public override IReadOnlyList<ProjectTouch>? Touches { get; } = [new("Timelines", timeline)];

    public override bool ChangesNames => false;

    public static TimelineShiftCommand FromArgs(IReadOnlyDictionary<string, string> a) =>
        new(a["timeline"], uint.Parse(a["from"], CultureInfo.InvariantCulture), int.Parse(a["delta"], CultureInfo.InvariantCulture));

    private static GameMakerTimeline Find(GameMakerData data, string name) => data.Timelines.FirstOrDefault(t => t.Name.Content == name) ?? throw new InvalidOperationException($"Timeline '{name}' does not exist.");

    public override void Apply(GameMakerData data)
    {
        var moments = Find(data, timeline).Moments;
        _before = [.. moments.Select(m => m.Step)];
        var moved = moments.Where(m => m.Step >= from).ToList();
        if (moved.Count == 0) return;
        // A shift to the left stops at the moment in front, so the list stays sorted and no two moments share a step.
        var floor = moments.Where(m => m.Step < from).Select(m => (long)m.Step + 1).DefaultIfEmpty(0).Max();
        var shift = Math.Max(delta, floor - moved.Min(m => (long)m.Step));
        foreach (var moment in moved) moment.Step = (uint)(moment.Step + shift);
    }

    public override void Revert(GameMakerData data)
    {
        var moments = Find(data, timeline).Moments;
        if (_before.Length != moments.Count) throw new InvalidOperationException("The edit was never applied.");
        foreach (var (moment, step) in moments.Zip(_before)) moment.Step = step;
    }
}
