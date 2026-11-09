using System.Text;
using G3MLib.DataFile;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Core.ModPack;

public enum PackState { Apply, AlreadyApplied, Conflict, Missing }

/// <summary>How a conflict is settled: keep the target as it is, take the pack's version, or (code only) use the three-way merge.</summary>
public enum Resolution { KeepMine, KeepTheirs, MergeCode }

/// <summary>One pack entry against the target. Note says why in English; Merge is, for a code conflict, the three-way merge of the target's text and the pack's text against the pack's base; InTarget tells that the target has a resource of this name.</summary>
public sealed record PackItemPlan(PackEntry Entry, PackState State, string Note, MergeResult? Merge, bool InTarget)
{
    public string Key => Entry.Change.Key;
}

/// <summary>The dry run of a pack on a target file: nothing has changed yet.</summary>
public sealed record ReapplyPlan(ModPackContent Pack, IReadOnlyList<PackItemPlan> Items, string? Refusal)
{
    public IEnumerable<PackItemPlan> Conflicts => Items.Where(i => i.State == PackState.Conflict);

    public int Count(PackState state) => Items.Count(i => i.State == state);

    /// <summary>What the pack would do, one line per item, for the report of the dry run.</summary>
    public string Report(IReadOnlyDictionary<string, Resolution>? choices = null)
    {
        var text = new StringBuilder();
        text.AppendLine(System.FormattableString.Invariant($"{Pack.Manifest.Name} {Pack.Manifest.Version}: {Count(PackState.Apply)} apply, {Count(PackState.AlreadyApplied)} already there, {Count(PackState.Conflict)} conflicts, {Count(PackState.Missing)} cannot apply"));
        if (Refusal is not null) text.AppendLine(Refusal);
        foreach (var item in Items)
        {
            var choice = item.State == PackState.Conflict ? " -> " + ModPackApplier.ChoiceOf(item, choices) : "";
            text.AppendLine(System.FormattableString.Invariant($"{item.State,-14} {item.Entry.Change.Op,-8} {item.Entry.Change.Type} {item.Entry.Change.Name}: {item.Note}{choice}"));
        }

        return text.ToString();
    }
}

/// <summary>
/// Matches a pack to a target file by resource name and content hash, then builds the one edit that applies it. A resource that is as the pack's base
/// takes the change; one that is already as the pack's result is skipped; one that is neither changed on both sides, and the owner decides.
/// </summary>
public static class ModPackApplier
{
    public static ReapplyPlan Plan(ModPackContent pack, GameMakerData target, CancellationToken cancellationToken = default)
    {
        var refusal = pack.Manifest.GameVersion.RefusalFor(CopyVersion.Of(target));
        if (refusal is not null) return new ReapplyPlan(pack, [], refusal);
        using var print = new Fingerprinter(target);
        return new ReapplyPlan(pack, [.. pack.Entries.Select(e =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Match(e, print.Of(e.Change.Type, e.Change.Name), target);
        })], null);
    }

    private static PackItemPlan Match(PackEntry entry, string? now, GameMakerData target)
    {
        var c = entry.Change;
        PackItemPlan Make(PackState state, string note, MergeResult? merge = null) => new(entry, state, note, merge, now is not null);
        if (c.Op == "remove")
        {
            if (now is null) return Make(PackState.AlreadyApplied, "already gone");
            return now == c.BaseHash ? Make(PackState.Apply, "will be removed") : Make(PackState.Conflict, "changed here, removed by the pack");
        }

        if (now is null) return c.Op == "code" ? Make(PackState.Missing, "the code entry does not exist here") : Make(PackState.Apply, "will be added");
        if (now == c.NewHash) return Make(PackState.AlreadyApplied, "already as the pack has it");
        if (now == c.BaseHash) return Make(PackState.Apply, c.Op == "code" ? "code will change" : "will be replaced");
        if (c.Op != "code") return Make(PackState.Conflict, c.Op == "add" ? "exists here with other content" : "changed here and in the pack");
        var merge = TextMerge.Merge(entry.BaseCode ?? "", DataDiff.CodeOf(target, c.Name) ?? "", entry.NewCode ?? "");
        return Make(PackState.Conflict, merge.Clean ? "changed here and in the pack, merges cleanly" : $"changed here and in the pack, {merge.Conflicts} clashing parts", merge);
    }

    /// <summary>The choice for a conflict; keeping the target is the default, as it loses nothing.</summary>
    public static Resolution ChoiceOf(PackItemPlan item, IReadOnlyDictionary<string, Resolution>? choices) =>
        choices is not null && choices.TryGetValue(item.Key, out var choice) && (choice != Resolution.MergeCode || item.Merge is { Clean: true }) ? choice : Resolution.KeepMine;

    private static readonly string[] Order = [.. CopyPlanner.Supported];

    /// <summary>
    /// One edit (one undo step) for everything the plan applies: added and replaced resources in the order of the Explorer, then code, then removals.
    /// Null when nothing is left to do. <paramref name="keepCodeUses"/> goes with a removal that code still uses, once the owner agreed.
    /// </summary>
    public static EditCommand? Build(ReapplyPlan plan, GameMakerData target, IReadOnlyDictionary<string, Resolution>? choices = null, bool keepCodeUses = false)
    {
        if (plan.Refusal is not null) throw new InvalidOperationException(plan.Refusal);
        var next = ResourceCopier.FirstFreeInstanceId(target);
        var chosen = plan.Items.Select(i => (Item: i, Take: i.State == PackState.Apply || (i.State == PackState.Conflict && ChoiceOf(i, choices) != Resolution.KeepMine))).Where(p => p.Take).Select(p => p.Item).ToList();
        static int Rank(PackItemPlan i) => i.Entry.Change.Op switch { "remove" => 3, "code" => 2, _ => 0 } * 100 + Array.IndexOf(Order, i.Entry.Change.Type);
        var commands = new List<EditCommand>();
        foreach (var item in chosen.OrderBy(Rank))
        {
            var text = item.State == PackState.Conflict && ChoiceOf(item, choices) == Resolution.MergeCode ? item.Merge?.Text : null;
            if (Command(item.Entry, item.InTarget, text, plan.Pack.Manifest.GameVersion, ref next, keepCodeUses) is { } command) commands.Add(command);
        }

        return commands.Count == 0 ? null : new GroupCommand(commands);
    }

    /// <summary>
    /// The edit that makes one item of <paramref name="mine"/> like <paramref name="other"/> ("Apply this change to my file"): a single property when one is given,
    /// else the whole item (added, replaced or removed, code as text). Null, with the reason in <paramref name="warning"/>, when it cannot travel.
    /// </summary>
    public static EditCommand? ForItem(Side mine, Side other, DiffItem item, PropertyChange? property, bool keepCodeUses, out string? warning)
    {
        warning = null;
        if (property is not null) return new SetPropertyCommand(item.Type, item.Name, property.Path, property.Right ?? "");
        if (ModPackBuilder.Entry(mine, other, item, CancellationToken.None, out warning) is not { } entry) return null;
        var next = ResourceCopier.FirstFreeInstanceId(mine.Data);
        return Command(entry, item.Kind != ChangeKind.Added, null, CopyVersion.Of(other.Data), ref next, keepCodeUses);
    }

    /// <summary>The edit for one entry on a target; the pack's version wins. Null when there is nothing to do (a removal of what is not there).</summary>
    public static EditCommand? Command(PackEntry entry, bool inTarget, string? codeText, CopyVersion version, ref uint nextInstanceId, bool keepCodeUses)
    {
        var c = entry.Change;
        if (c.Op == "code") return new EditCodeCommand(c.Name, codeText ?? entry.NewCode ?? throw new InvalidDataException("The code change has no text."));
        if (c.Op == "remove") return inTarget ? BulkResources.Delete([(c.Type, c.Name, true)], keepCodeUses) : null;
        var folder = Directory.CreateTempSubdirectory("g3mstudio-pack-").FullName;
        try
        {
            CopyPackage.Unzip(entry.Payload ?? throw new InvalidDataException("The change has no payload."), folder);
            var last = CopyPackage.Prepare(folder, new Dictionary<string, string>(), nextInstanceId);
            nextInstanceId = Math.Max(nextInstanceId, last + 1);
            return new CopyResourcesCommand(CopyPackage.Zip(folder), version, [new CopyStep(c.Type, c.Name, inTarget)], last);
        }
        finally
        {
            CopyPackage.TryDelete(folder);
        }
    }
}
