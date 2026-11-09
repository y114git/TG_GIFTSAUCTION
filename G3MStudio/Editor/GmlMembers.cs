using G3MLib.Analyzer.Compiler;

namespace G3MStudio.Editor;

/// <summary>What the member guess may ask the open file: the variables the Create code of an object assigns (null for a name that is no object), and the object that owns the code being edited.</summary>
public sealed record GmlMemberContext(Func<string, IReadOnlyList<string>?> ObjectVariables, string? OwnerObject = null);

/// <summary>
/// The names offered after a dot. No type system exists, so the guess is cheap and reads the text: global. lists the globals used in the text,
/// a name made with "new Constructor(" lists what that constructor assigns, a name holding a struct literal lists its keys, and an object (by its name, by
/// self in its own code or in a with block, or a variable set from instance_create_* and similar calls) lists the variables of its Create code plus the built-in instance variables.
/// Anything else lists the built-in instance variables and every member name already used after a dot.
/// </summary>
public static class GmlMembers
{
    // The call argument (0 based) that names the object, per function.
    private static readonly Dictionary<string, int> ObjectArgument = new(StringComparer.Ordinal)
    {
        ["instance_create_layer"] = 3,
        ["instance_create_depth"] = 3,
        ["instance_create"] = 2,
        ["instance_find"] = 0,
        ["instance_nearest"] = 2,
        ["instance_furthest"] = 2,
        ["instance_place"] = 2,
        ["instance_position"] = 2,
    };

    private static readonly GmlMemberContext NoObjects = new(_ => null);

    public static IReadOnlyList<KeyValuePair<string, string>> After(GmlDoc doc, int dot, IEnumerable<string> instanceVariables, GmlMemberContext? context = null)
    {
        var left = doc.TokenAt(dot - 1);
        var name = left >= 0 && doc.Tokens[left].Kind == GmlTokenKind.Identifier ? doc.Slice(left) : "";
        var found = new SortedSet<string>(StringComparer.Ordinal);
        if (name == "global")
        {
            foreach (var (owner, member) in Members(doc))
                if (owner == "global") found.Add(member);
            return [.. found.Select(n => new KeyValuePair<string, string>(n, "Variable"))];
        }

        var instance = instanceVariables.ToList();
        if (Typed(doc, left, dot, context ?? NoObjects, instance) is { } typed)
            return [.. typed.Select(n => new KeyValuePair<string, string>(n, "Variable"))];

        foreach (var variable in instance) found.Add(variable);
        foreach (var (owner, member) in Members(doc))
            if (owner != "global") found.Add(member);
        return [.. found.Select(n => new KeyValuePair<string, string>(n, "Variable"))];
    }

    // The members of the type that the text gives the left side of the dot, or null when nothing is known.
    private static SortedSet<string>? Typed(GmlDoc doc, int left, int dot, GmlMemberContext context, List<string> instanceVariables)
    {
        if (left < 0 || doc.Tokens[left].Kind != GmlTokenKind.Identifier) return null;
        var chain = ChainStart(doc, left);
        var key = string.Concat(Enumerable.Range(chain, left - chain + 1).Select(doc.Slice));
        var members = new SortedSet<string>(StringComparer.Ordinal);
        string? obj = null;
        if (key == "self") obj = WithTarget(doc, dot, context) ?? context.OwnerObject;
        else if (left == chain && context.ObjectVariables(key) is not null) obj = key;
        else
        {
            var value = AssignedValue(doc, chain, left);
            if (value < 0) return null;
            if (doc.Slice(value) == "new" && value + 1 < doc.Tokens.Count && GmlStructure.Outline(doc).FirstOrDefault(o => o.Kind == "function" && o.Name == doc.Slice(value + 1)) is { } constructor)
            {
                foreach (var member in Assigned(doc, constructor)) members.Add(member);
                return members.Count > 0 ? members : null;
            }

            if (doc.IsPunct(value, '{'))
            {
                foreach (var member in StructKeys(doc, value)) members.Add(member);
                return members.Count > 0 ? members : null;
            }

            obj = ObjectOfValue(doc, value, context);
        }

        if (obj is null || context.ObjectVariables(obj) is not { } variables) return null;
        foreach (var variable in variables.Concat(instanceVariables)) members.Add(variable);
        // The code being edited is often the Create code of its own object; what it assigns counts at once.
        if (key == "self" && obj == context.OwnerObject)
            foreach (var member in Assigned(doc, new OutlineItem("", "", 0, doc.Text.Length))) members.Add(member);
        return members;
    }

    // The first token of the dotted name that ends at token last: global.target, self.a.b, name.
    private static int ChainStart(GmlDoc doc, int last)
    {
        var first = last;
        while (first >= 2 && doc.IsPunct(first - 1, '.') && doc.Tokens[first - 2].Kind == GmlTokenKind.Identifier) first -= 2;
        return first;
    }

    // The first token after "chain =" for the first assignment to the dotted name, or -1.
    private static int AssignedValue(GmlDoc doc, int chain, int last)
    {
        var length = last - chain + 1;
        foreach (var token in doc.Named(doc.Slice(chain)))
        {
            var end = token + length - 1;
            if (end + 2 >= doc.Tokens.Count || !doc.IsPunct(end + 1, '=') || doc.IsPunct(end + 2, '=')) continue;
            if (Enumerable.Range(0, length).Any(i => doc.Slice(token + i) != doc.Slice(chain + i))) continue;
            return end + 2;
        }

        return -1;
    }

    // The object a value token starts: obj_name, or a call such as instance_create_layer(x, y, "l", obj_name).
    private static string? ObjectOfValue(GmlDoc doc, int start, GmlMemberContext context)
    {
        if (doc.Tokens[start].Kind != GmlTokenKind.Identifier) return null;
        var word = doc.Slice(start);
        if (start + 1 >= doc.Tokens.Count || !doc.IsPunct(start + 1, '(')) return context.ObjectVariables(word) is not null ? word : null;
        var close = doc.PairOf(start + 1);
        if (!ObjectArgument.TryGetValue(word, out var index) || close < 0) return null;
        var argument = 0;
        for (var i = start + 2; i < close; i++)
        {
            if (IsOpener(doc, i) && doc.PairOf(i) is var pair and >= 0) { i = pair; continue; }
            if (doc.IsPunct(i, ',')) { argument++; continue; }
            if (argument == index) return doc.Tokens[i].Kind == GmlTokenKind.Identifier && context.ObjectVariables(doc.Slice(i)) is not null ? doc.Slice(i) : null;
        }

        return null;
    }

    private static bool IsOpener(GmlDoc doc, int token) => doc.Tokens[token].Kind == GmlTokenKind.Separator && doc.Slice(token) is "(" or "[" or "{";

    // The object named in the nearest with (name) block that holds the dot; the innermost block wins.
    private static string? WithTarget(GmlDoc doc, int dot, GmlMemberContext context)
    {
        string? target = null;
        for (var i = 0; i + 3 < doc.Tokens.Count && doc.Start(i) < dot; i++)
        {
            if (doc.Tokens[i].Kind != GmlTokenKind.Keyword || doc.Slice(i) != "with" || !doc.IsPunct(i + 1, '(')) continue;
            var close = doc.PairOf(i + 1);
            if (close < 0) continue;
            var body = close + 1;
            var end = body < doc.Tokens.Count && doc.IsPunct(body, '{') && doc.PairOf(body) is var pair and >= 0 ? doc.End(pair) : doc.Text.Length;
            if (dot >= doc.Start(close) && dot <= end && close == i + 3 && context.ObjectVariables(doc.Slice(i + 2)) is not null) target = doc.Slice(i + 2);
        }

        return target;
    }

    // The keys of a struct literal that starts at the { token: a name followed by a colon at its own depth.
    private static IEnumerable<string> StructKeys(GmlDoc doc, int open)
    {
        var close = doc.PairOf(open);
        for (var i = open + 1; i < close; i++)
        {
            if (IsOpener(doc, i) && doc.PairOf(i) is var pair and >= 0) { i = pair; continue; }
            if (doc.Tokens[i].Kind == GmlTokenKind.Identifier && i + 1 < close && doc.IsPunct(i + 1, ':')) yield return doc.Slice(i);
        }
    }

    /// <summary>The names a text assigns without var, as name = ... or self.name = ...; what a Create event adds to its object.</summary>
    public static IReadOnlyList<string> AssignedNames(string text)
    {
        var doc = GmlDoc.Parse(text);
        return [.. Assigned(doc, new OutlineItem("", "", 0, text.Length)).Distinct(StringComparer.Ordinal)];
    }

    // Every "owner.member" pair of the text, where the owner is a name.
    private static IEnumerable<(string Owner, string Member)> Members(GmlDoc doc)
    {
        for (var i = 2; i < doc.Tokens.Count; i++)
        {
            if (doc.Tokens[i].Kind != GmlTokenKind.Identifier || !doc.IsPunct(i - 1, '.')) continue;
            yield return (doc.Tokens[i - 2].Kind == GmlTokenKind.Identifier ? doc.Slice(i - 2) : "", doc.Slice(i));
        }
    }

    // The names assigned (name = ..., self.name = ...) inside the range of the item.
    private static IEnumerable<string> Assigned(GmlDoc doc, OutlineItem constructor)
    {
        for (var i = 0; i < doc.Tokens.Count - 1; i++)
        {
            if (doc.Start(i) < constructor.Offset || doc.End(i) > constructor.End || doc.Tokens[i].Kind != GmlTokenKind.Identifier) continue;
            if (!doc.IsPunct(i + 1, '=') || (i + 2 < doc.Tokens.Count && doc.IsPunct(i + 2, '='))) continue;
            if (i > 0 && doc.IsPunct(i - 1, '.') && !(i > 1 && doc.Slice(i - 2) == "self")) continue;
            if (i > 0 && doc.Tokens[i - 1].Kind == GmlTokenKind.Keyword && doc.Slice(i - 1) == "var") continue;
            yield return doc.Slice(i);
        }
    }
}
