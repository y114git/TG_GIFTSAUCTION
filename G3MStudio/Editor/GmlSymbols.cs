using G3MLib.Analyzer.Compiler;

namespace G3MStudio.Editor;

/// <summary>A span of the text to replace. Edits of one plan never overlap.</summary>
public readonly record struct TextEdit(int Offset, int Length, string Text);

/// <summary>What a rename of a name in one code entry would do, and what to tell the user first.</summary>
/// <param name="Edits">The changes, in text order.</param>
/// <param name="Warnings">Language keys of the problems found; the user may still go on (warn, then allow).</param>
public sealed record RenamePlan(IReadOnlyList<TextEdit> Edits, IReadOnlyList<string> Warnings);

/// <summary>Finding and renaming names inside one GML text, from tokens only: strings and comments are never touched.</summary>
public static class GmlSymbols
{
    private static readonly HashSet<string> Reserved = new(StringComparer.Ordinal) { "var", "static", "globalvar", "function", "if", "else", "for", "while", "do", "until", "repeat", "switch", "case", "default", "break", "continue", "return", "exit", "with", "new", "delete", "try", "catch", "finally", "throw", "enum", "self", "other", "all", "noone", "global", "true", "false", "undefined", "not", "and", "or", "xor", "div", "mod", "begin", "end", "then" };

    /// <summary>True when the compiler reads the name as one plain identifier: ASCII letters, digits and underscore only, and not a keyword (the tokenizer has the last word, so the list cannot drift).</summary>
    public static bool IsValidName(string name) =>
        name.Length > 0 && !char.IsDigit(name[0]) && name.All(c => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '_') && !Reserved.Contains(name)
        && GmlTokenizer.Tokenize(name, true) is [{ Kind: GmlTokenKind.Identifier }];

    /// <summary>Every place the name is used as a plain name (not after a dot), as source spans. For a global, the member of global.name counts too.</summary>
    public static IReadOnlyList<(int Start, int End)> References(GmlDoc doc, string name, bool includeMembers = false)
    {
        var found = new List<(int, int)>();
        foreach (var token in doc.Named(name))
        {
            var member = token > 0 && doc.IsPunct(token - 1, '.');
            if (!member || includeMembers || IsGlobalMember(doc, token)) found.Add((doc.Start(token), doc.End(token)));
        }

        return found;
    }

    /// <summary>String literals that are exactly the name, as spans of the whole literal including its quotes (variable_instance_get(id, "hp")).</summary>
    public static IReadOnlyList<(int Start, int End)> StringReferences(GmlDoc doc, string name)
    {
        var found = new List<(int, int)>();
        for (var i = 0; i < doc.Tokens.Count; i++)
        {
            if (doc.Tokens[i].Kind != GmlTokenKind.String) continue;
            var literal = doc.Slice(i);
            if (literal.Length == name.Length + 2 && literal[0] == '"' && literal[^1] == '"' && literal.AsSpan(1, name.Length).SequenceEqual(name)) found.Add((doc.Start(i), doc.End(i)));
        }

        return found;
    }

    /// <summary>Like <see cref="References"/> for a rename across all code, split into the uses that are safe to rename and the ones that cannot be resolved (other.name, instance.name, Enum.name, struct keys, enum members) and need review.</summary>
    public static (IReadOnlyList<(int Start, int End)> Rename, IReadOnlyList<(int Start, int End)> Review) AcrossReferences(GmlDoc doc, string name)
    {
        var rename = new List<(int, int)>();
        var review = new List<(int, int)>();
        var hasLocal = GmlScopes.Declarations(doc.Text, doc.Tokens).Locals.ContainsKey(name);
        foreach (var token in doc.Named(name))
        {
            var span = (doc.Start(token), doc.End(token));
            var member = token > 0 && doc.IsPunct(token - 1, '.');
            // self.name is the plain name, unless a local of that name makes the two different variables.
            var safeMember = IsGlobalMember(doc, token) || token > 1 && !hasLocal && doc.Slice(token - 2) == "self" && !(token > 2 && doc.IsPunct(token - 3, '.'));
            ((member ? !safeMember : IsStructKey(doc, token) || EnumOfMember(doc, token) is not null) ? review : rename).Add(span);
        }

        return (rename, review);
    }

    /// <summary>True for the key of a struct literal ({ name: value }), which is a member of the struct and not a plain name.</summary>
    public static bool IsStructKey(GmlDoc doc, int token) =>
        token > 0 && token + 1 < doc.Tokens.Count && (doc.IsPunct(token - 1, '{') || doc.IsPunct(token - 1, ',')) && doc.IsPunct(token + 1, ':');

    private static bool IsGlobalMember(GmlDoc doc, int token) => token > 1 && doc.IsPunct(token - 1, '.') && doc.Slice(token - 2) == "global";

    /// <summary>The enum and the member token of an enum member the text declares (enum State { idle, walk }), or null. With <paramref name="enumName"/>, only a member of that enum.</summary>
    public static (string Enum, int Token)? EnumMemberOf(GmlDoc doc, string name, string? enumName = null)
    {
        foreach (var token in doc.Named(name))
            if (EnumOfMember(doc, token) is { } found && (enumName is null || found == enumName)) return (found, token);
        return null;
    }

    /// <summary>The enum the token is declared in as a member (enum State { idle, walk }), or null.</summary>
    private static string? EnumOfMember(GmlDoc doc, int token)
    {
        if (token < 1 || !(doc.IsPunct(token - 1, '{') || doc.IsPunct(token - 1, ','))) return null;
        // Walk back to the { of the block and check that enum Name stands before it.
        var depth = 0;
        for (var j = token - 1; j >= 2; j--)
        {
            if (doc.IsPunct(j, '}') || doc.IsPunct(j, ')') || doc.IsPunct(j, ']')) depth++;
            else if (doc.IsPunct(j, '(') || doc.IsPunct(j, '[')) depth--;
            else if (doc.IsPunct(j, '{') && depth-- == 0)
                return doc.Tokens[j - 1].Kind == GmlTokenKind.Identifier && doc.Slice(j - 2) == "enum" && doc.PairOf(j) > token ? doc.Slice(j - 1) : null;
        }

        return null;
    }

    /// <summary>The uses of an enum member: its declaration and every Enum.member.</summary>
    public static IReadOnlyList<(int Start, int End)> EnumMemberReferences(GmlDoc doc, string enumName, string member)
    {
        var own = EnumMemberOf(doc, member, enumName)?.Token ?? -1;
        return [.. doc.Named(member).Where(t => t == own || t > 1 && doc.IsPunct(t - 1, '.') && doc.Slice(t - 2) == enumName).Select(t => (doc.Start(t), doc.End(t)))];
    }

    /// <summary>The token where a local, argument or global of the entry is declared, or -1 for a name that is declared nowhere in the text.</summary>
    public static int DeclarationOf(GmlDoc doc, string name)
    {
        var (locals, globals) = GmlScopes.Declarations(doc.Text, doc.Tokens);
        return locals.TryGetValue(name, out var local) ? local : globals.GetValueOrDefault(name, -1);
    }

    /// <summary>The text spans to change to rename the name under the offset, with the warnings the rename deserves. Null when the offset is not on a name.</summary>
    public static RenamePlan? PlanRename(GmlDoc doc, int offset, string newName, Func<string, bool> isKnownName)
    {
        var token = doc.TokenAt(offset);
        if (token < 0 || doc.Tokens[token].Kind != GmlTokenKind.Identifier) return null;
        var old = doc.Slice(token);
        var warnings = new List<string>();
        if (!IsValidName(newName)) warnings.Add("code.rename.invalid");
        if (newName == old) warnings.Add("code.rename.same");
        var member = token > 0 && doc.IsPunct(token - 1, '.');
        var isGlobal = IsGlobalMember(doc, token);
        // A member of some other object is a different variable on every instance; a rename here would only change this entry.
        if (member && !isGlobal) warnings.Add("code.rename.member");
        var (locals, globals) = GmlScopes.Declarations(doc.Text, doc.Tokens);
        if (!member && !locals.ContainsKey(old) && !globals.ContainsKey(old)) warnings.Add("code.rename.notLocal");
        if (newName != old && (locals.ContainsKey(newName) || globals.ContainsKey(newName) || doc.Named(newName).Count > 0 || isKnownName(newName))) warnings.Add("code.rename.collision");
        var edits = (member && !isGlobal ? References(doc, old, includeMembers: true) : ScopedReferences(doc, token, old)).Select(r => new TextEdit(r.Start, r.End - r.Start, newName)).ToList();
        return new RenamePlan(edits, warnings);
    }

    /// <summary>The uses of the name that mean the same variable as the one at the token. A local belongs to its function (nested functions do not see it); a name that is not local there means the instance or global variable, which the functions that declare a local of that name do not use.</summary>
    private static List<(int Start, int End)> ScopedReferences(GmlDoc doc, int token, string name)
    {
        var owners = FunctionOwners(doc);
        var declares = new Dictionary<int, bool>();
        bool DeclaresLocal(int owner)
        {
            if (declares.TryGetValue(owner, out var known)) return known;
            var own = doc.Tokens.Where((_, i) => owners[i] == owner).ToList();
            return declares[owner] = GmlScopes.Declarations(doc.Text, own).Locals.ContainsKey(name);
        }

        var mine = owners[token];
        var local = DeclaresLocal(mine);
        var found = new List<(int, int)>();
        foreach (var t in doc.Named(name))
        {
            var member = t > 0 && doc.IsPunct(t - 1, '.');
            if (member && (local || !IsGlobalMember(doc, t)) || IsStructKey(doc, t)) continue;
            if (local ? owners[t] == mine : !DeclaresLocal(owners[t])) found.Add((doc.Start(t), doc.End(t)));
        }

        return found;
    }

    /// <summary>Per token, the token index of the innermost function keyword it stands in, or -1 for the code outside every function.</summary>
    private static int[] FunctionOwners(GmlDoc doc)
    {
        var owners = new int[doc.Tokens.Count];
        Array.Fill(owners, -1);
        for (var i = 0; i < owners.Length; i++)
        {
            if (doc.Tokens[i].Kind != GmlTokenKind.Keyword || doc.Slice(i) != "function") continue;
            var open = i + 1 < owners.Length && doc.Tokens[i + 1].Kind == GmlTokenKind.Identifier ? i + 2 : i + 1;
            if (open >= owners.Length || !doc.IsPunct(open, '(') || doc.PairOf(open) is not (var close and >= 0)) continue;
            var body = close + 1;
            while (body < owners.Length && !doc.IsPunct(body, '{') && !doc.IsPunct(body, ';')) body = doc.IsOpener(body) && doc.PairOf(body) > body ? doc.PairOf(body) + 1 : body + 1;
            if (body >= owners.Length || !doc.IsPunct(body, '{') || doc.PairOf(body) is not (var end and >= 0)) continue;
            for (var j = i; j <= end; j++) owners[j] = i; // outer functions come first, so an inner one overwrites its own range
        }

        return owners;
    }

    /// <summary>Applies non-overlapping edits to a text.</summary>
    public static string Apply(string text, IEnumerable<TextEdit> edits)
    {
        var builder = new System.Text.StringBuilder(text);
        foreach (var edit in edits.OrderByDescending(e => e.Offset)) builder.Remove(edit.Offset, edit.Length).Insert(edit.Offset, edit.Text);
        return builder.ToString();
    }
}
