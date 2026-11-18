using System.Text.RegularExpressions;

namespace G3MStudio.Core.Workspace;

public enum RenameMode { Prefix, Suffix, Replace, Regex }

/// <summary>How a batch rename builds the new name. Prefix and Suffix add <see cref="Text"/>; Replace and Regex swap <see cref="Find"/> for <see cref="Text"/> ($1 works in Regex).</summary>
public sealed record RenameSpec(RenameMode Mode, string Find = "", string Text = "");

/// <summary>One line of the preview: the new name, and why it cannot run (a <see cref="BulkRename"/> problem code) or null.</summary>
public sealed record RenameStep(string Type, string Old, string New, string? Problem)
{
    public bool Changes => Old != New;

    public bool Runs => Changes && Problem is null;
}

/// <summary>Plans a rename of many resources by pattern. The plan is a preview; <see cref="Command"/> turns it into one undo step.</summary>
public static class BulkRename
{
    public const string BadPattern = "pattern", Empty = "empty", Invalid = "invalid", Duplicate = "duplicate", Taken = "taken";

    /// <param name="items">The resources to rename, type key and name.</param>
    /// <param name="spec">How the new names are made.</param>
    /// <param name="exists">Whether a resource of that type already has that name.</param>
    public static IReadOnlyList<RenameStep> Plan(IReadOnlyList<(string Type, string Name)> items, RenameSpec spec, Func<string, string, bool> exists)
    {
        Regex? regex = null;
        string? patternProblem = null;
        if (spec.Mode == RenameMode.Regex && spec.Find.Length > 0)
        {
            try { regex = new Regex(spec.Find, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(200)); }
            catch (ArgumentException) { patternProblem = BadPattern; }
        }

        var steps = new List<(string Type, string Old, string New, string? Problem)>();
        foreach (var (type, name) in items)
        {
            var problem = patternProblem;
            string next;
            try
            {
                next = spec.Mode switch
                {
                    RenameMode.Prefix => spec.Text + name,
                    RenameMode.Suffix => name + spec.Text,
                    RenameMode.Replace => spec.Find.Length == 0 ? name : name.Replace(spec.Find, spec.Text, StringComparison.Ordinal),
                    _ => regex is null ? name : regex.Replace(name, spec.Text),
                };
            }
            catch (Exception e) when (e is RegexMatchTimeoutException or ArgumentException)
            {
                (next, problem) = (name, BadPattern);
            }

            steps.Add((type, name, next, problem));
        }

        var duplicated = steps.Where(s => s.Old != s.New && s.Problem is null).GroupBy(s => (s.Type, s.New)).Where(g => g.Skip(1).Any()).Select(g => g.Key).ToHashSet();
        // A name that another rename of the batch gives up is still refused: the order of such a chain or swap cannot be made safe.
        var leaving = steps.Where(s => s.Old != s.New).Select(s => (s.Type, s.Old)).ToHashSet();
        return [.. steps.Select(s => new RenameStep(s.Type, s.Old, s.New, s.Problem ?? Check(s, duplicated, leaving, exists)))];
    }

    private static string? Check((string Type, string Old, string New, string? Problem) s, HashSet<(string, string)> duplicated, HashSet<(string, string)> leaving, Func<string, string, bool> exists)
    {
        if (s.Old == s.New) return null;
        if (s.New.Trim().Length == 0) return Empty;
        if (s.New.Any(c => char.IsWhiteSpace(c) || char.IsControl(c))) return Invalid;
        if (duplicated.Contains((s.Type, s.New))) return Duplicate;
        return exists(s.Type, s.New) || leaving.Contains((s.Type, s.New)) ? Taken : null;
    }

    /// <summary>One undo step for every rename that can run; null when none can.</summary>
    public static EditCommand? Command(IEnumerable<RenameStep> steps)
    {
        var run = steps.Where(s => s.Runs).Select(s => (EditCommand)new RenameResourceCommand(s.Type, s.Old, s.New)).ToList();
        return run.Count == 0 ? null : run.Count == 1 ? run[0] : new GroupCommand(run);
    }
}
