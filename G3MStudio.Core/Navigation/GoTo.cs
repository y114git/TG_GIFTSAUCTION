namespace G3MStudio.Core.Navigation;

public enum GoToKind { Resource, Code, Text, Function, Variable, Command, Setting, Tab, Reference }

/// <summary>One thing Go To can open. <see cref="Key"/> is what the owner needs to open it: a tab key, a command id or a settings page name. <see cref="Alt"/> is extra text that also matches (the job of a removed duplicate command).</summary>
public sealed record GoToEntry(GoToKind Kind, string Name, string Key, string Type = "", string Detail = "", string Alt = "");

/// <summary>The typed text split into the kinds it narrows to, a resource type and the words to match.</summary>
public sealed record GoToQuery(IReadOnlySet<GoToKind>? Kinds, string Type, string Needle)
{
    private static readonly Dictionary<string, GoToKind[]> Marks = new(StringComparer.OrdinalIgnoreCase)
    {
        [">"] = [GoToKind.Command],
        ["@"] = [GoToKind.Function, GoToKind.Variable],
        ["#"] = [GoToKind.Text],
        ["f:"] = [GoToKind.Function, GoToKind.Reference],
        ["v:"] = [GoToKind.Variable],
        ["c:"] = [GoToKind.Code],
        ["t:"] = [GoToKind.Tab],
        ["p:"] = [GoToKind.Setting],
    };

    // Short forms of the Explorer prefixes; the full ones (spr: obj: room: ...) work as in the Explorer filter.
    private static readonly Dictionary<string, string> Types = new(StringComparer.OrdinalIgnoreCase)
    {
        ["o"] = "objects",
        ["s"] = "sprites",
        ["r"] = "rooms",
        ["x"] = "scripts",
        ["a"] = "sounds",
        ["n"] = "fonts",
        ["spr"] = "sprites",
        ["snd"] = "sounds",
        ["scr"] = "scripts",
        ["shd"] = "shaders",
        ["fnt"] = "fonts",
        ["tl"] = "timelines",
        ["obj"] = "objects",
        ["room"] = "rooms",
        ["seq"] = "sequences",
        ["curve"] = "animationCurves",
        ["ps"] = "particleSystems",
        ["path"] = "paths",
        ["bg"] = "tileSets",
        ["ext"] = "extensions",
    };

    public static GoToQuery Parse(string text)
    {
        text = text.TrimStart();
        foreach (var (mark, kinds) in Marks)
            if (text.StartsWith(mark, StringComparison.OrdinalIgnoreCase)) return new GoToQuery(new HashSet<GoToKind>(kinds), "", text[mark.Length..].Trim());
        var colon = text.IndexOf(':', StringComparison.Ordinal);
        if (colon > 0 && Types.TryGetValue(text[..colon], out var type)) return new GoToQuery(new HashSet<GoToKind> { GoToKind.Resource }, type, text[(colon + 1)..].Trim());
        return new GoToQuery(null, "", text.Trim());
    }

    public bool IsPrefixed => Kinds is not null;
}

/// <summary>Go To (Ctrl+T): one fuzzy search over every kind of entry. Without a prefix the big lists (strings, functions, variables) stay out until two letters are typed.</summary>
public static class GoToSearch
{
    public const int DefaultLimit = 60;

    /// <summary>Best score first; ties keep the recent order (a key seen last comes first), then the kind order. With no text the recent keys are listed.</summary>
    public static IReadOnlyList<GoToEntry> Run(IReadOnlyList<GoToEntry> all, string text, IReadOnlyList<string> recent, int limit = DefaultLimit)
    {
        var query = GoToQuery.Parse(text);
        var order = new Dictionary<string, int>();
        for (var i = 0; i < recent.Count; i++) order.TryAdd(recent[i], i);
        int Recency(GoToEntry e) => order.GetValueOrDefault(e.Key, int.MaxValue);

        var pool = all.Where(e => (query.Kinds is null ? !IsBig(e.Kind) || query.Needle.Length >= 2 : query.Kinds.Contains(e.Kind)) && (query.Type.Length == 0 || e.Type == query.Type));
        if (query.Needle.Length == 0 && !query.IsPrefixed) return [.. pool.Where(e => order.ContainsKey(e.Key)).OrderBy(Recency).DistinctBy(e => e.Key).Take(limit)];
        return [.. pool
            .Select(e => (Entry: e, Score: Math.Max(Fuzzy.Score(query.Needle, e.Name), e.Alt.Length > 0 ? Fuzzy.Score(query.Needle, e.Alt) : -1)))
            .Where(x => x.Score >= 0)
            .OrderBy(x => !query.IsPrefixed && IsBig(x.Entry.Kind) ? 1 : 0) // names of things first; strings, functions and variables follow (the prefixes # @ f: v: list them alone)
            .ThenByDescending(x => x.Score)
            .ThenBy(x => Recency(x.Entry))
            .ThenBy(x => x.Entry.Kind)
            .ThenBy(x => x.Entry.Name, StringComparer.OrdinalIgnoreCase)
            .DistinctBy(x => query.IsPrefixed ? (x.Entry.Kind, x.Entry.Key) : (GoToKind.Resource, x.Entry.Key))
            .Take(limit)
            .Select(x => x.Entry)];
    }

    private static bool IsBig(GoToKind kind) => kind is GoToKind.Text or GoToKind.Function or GoToKind.Variable or GoToKind.Reference;
}
