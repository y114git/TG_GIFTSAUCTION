namespace G3MStudio.Core.ModPack;

/// <param name="Text">The merged text; a part both sides changed differently is written between conflict markers.</param>
/// <param name="Conflicts">How many such parts there are; 0 means the merge is clean.</param>
public sealed record MergeResult(string Text, int Conflicts)
{
    public bool Clean => Conflicts == 0;
}

/// <summary>A three-way line merge (the "merge code" choice): the changes of both sides against the common base are combined, and a part both changed differently is marked.</summary>
public static class TextMerge
{
    private sealed record Hunk(int Start, int Count, string[] New, bool Mine)
    {
        public int End => Start + Count;
    }

    public static MergeResult Merge(string baseText, string mine, string theirs)
    {
        var (b, a, t) = (Lines(baseText), Lines(mine), Lines(theirs));
        var hunks = Hunks(baseText, mine, true).Concat(Hunks(baseText, theirs, false)).OrderBy(h => h.Start).ThenBy(h => h.Mine ? 0 : 1).ToList();
        var output = new List<string>();
        var (position, conflicts) = (0, 0);
        foreach (var cluster in Clusters(hunks))
        {
            var (start, end) = (cluster[0].Start, cluster.Max(h => h.End));
            output.AddRange(b[position..start]);
            position = end;
            if (cluster.All(h => h.Mine == cluster[0].Mine))
            {
                output.AddRange(Region(b, cluster, cluster[0].Mine, start, end));
                continue;
            }

            var (ours, others) = (Region(b, cluster, true, start, end), Region(b, cluster, false, start, end));
            if (ours.SequenceEqual(others))
            {
                output.AddRange(ours);
                continue;
            }

            conflicts++;
            output.AddRange(["<<<<<<< mine", .. ours, "=======", .. others, ">>>>>>> theirs"]);
        }

        output.AddRange(b[position..]);
        return new MergeResult(string.Join('\n', output), conflicts);
    }

    private static string[] Lines(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');

    // Hunks that overlap (or start at the same place) are one cluster; the two sides meet inside a cluster.
    private static IEnumerable<List<Hunk>> Clusters(List<Hunk> hunks)
    {
        List<Hunk> current = [];
        var end = 0;
        foreach (var hunk in hunks)
        {
            if (current.Count > 0 && hunk.Start >= end && hunk.Start != current[0].Start)
            {
                yield return current;
                current = [];
            }

            current.Add(hunk);
            end = Math.Max(end, hunk.End);
        }

        if (current.Count > 0) yield return current;
    }

    // One side's version of the base lines start..end: its own hunks applied, the rest as in the base.
    private static List<string> Region(string[] b, List<Hunk> cluster, bool mine, int start, int end)
    {
        var lines = new List<string>();
        var position = start;
        foreach (var hunk in cluster.Where(h => h.Mine == mine))
        {
            lines.AddRange(b[position..hunk.Start]);
            lines.AddRange(hunk.New);
            position = hunk.End;
        }

        lines.AddRange(b[position..end]);
        return lines;
    }

    private static List<Hunk> Hunks(string baseText, string other, bool mine)
    {
        var hunks = new List<Hunk>();
        var (consumed, start, removed) = (0, -1, 0);
        List<string> added = [];
        void Close()
        {
            if (start >= 0) hunks.Add(new Hunk(start, removed, [.. added], mine));
            (start, removed) = (-1, 0);
            added = [];
        }

        foreach (var line in LineDiff.Compute(baseText.Replace("\r\n", "\n", StringComparison.Ordinal), other.Replace("\r\n", "\n", StringComparison.Ordinal)))
        {
            if (line.Kind == DiffKind.Same)
            {
                Close();
                consumed++;
                continue;
            }

            if (start < 0) start = consumed;
            if (line.Kind == DiffKind.Removed)
            {
                removed++;
                consumed++;
            }
            else
            {
                added.Add(line.Text);
            }
        }

        Close();
        return hunks;
    }
}
