namespace G3MStudio.Core.Navigation;

/// <summary>Scores how well a typed text matches a name: higher is better, below zero is no match.</summary>
public static class Fuzzy
{
    public const int Exact = 1000;

    /// <summary>Exact, then start of the name, then a substring (better at a word start), then the letters in order (better when close together and on word starts).</summary>
    public static int Score(string needle, string text)
    {
        if (needle.Length == 0) return 0;
        if (text.Equals(needle, StringComparison.OrdinalIgnoreCase)) return Exact;
        if (text.StartsWith(needle, StringComparison.OrdinalIgnoreCase)) return 900 - Math.Min(text.Length - needle.Length, 99);
        var at = text.IndexOf(needle, StringComparison.OrdinalIgnoreCase);
        if (at >= 0) return 700 - Math.Min(at, 99) + (IsWordStart(text, at) ? 100 : 0);
        return Letters(needle, text);
    }

    // shortcut: the first letter that fits is taken, so a later and tighter run is missed; upgrade when ranking of long names looks wrong.
    private static int Letters(string needle, string text)
    {
        var next = 0;
        var first = -1;
        var bonus = 0;
        for (var i = 0; i < text.Length && next < needle.Length; i++)
        {
            if (char.ToLowerInvariant(text[i]) != char.ToLowerInvariant(needle[next])) continue;
            if (first < 0) first = i;
            if (IsWordStart(text, i)) bonus += 15;
            next++;
            if (next == needle.Length) return Math.Max(1, 300 + bonus - Math.Min(i - first + 1 - needle.Length, 250));
        }

        return -1;
    }

    // Word starts: after _ - space . / : or a lower-to-upper change.
    private static bool IsWordStart(string text, int at) =>
        at == 0 || text[at - 1] is '_' or '-' or ' ' or '.' or '/' or ':' || char.IsUpper(text[at]) && !char.IsUpper(text[at - 1]);
}
