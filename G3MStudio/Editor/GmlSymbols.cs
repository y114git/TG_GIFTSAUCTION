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
    private static readonly HashSet<string> Reserved = new(StringComparer.Ordinal) { "var", "static", "globalvar", "function", "if", "else", "for", "while", "do", "until", "repeat", "switch", "case", "default", "break", "continue", "return", "exit", "with", "new", "delete", "try", "catch", "finally", "throw", "enum", "self", "other", "all", "noone", "global", "true", "false", "undefined", "not", "and", "or", "xor", "div", "mod" };

    public static bool IsValidName(string name) => name.Length > 0 && !char.IsDigit(name[0]) && name.All(c => char.IsLetterOrDigit(c) || c == '_') && !Reserved.Contains(name);

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

    private static bool IsGlobalMember(GmlDoc doc, int token) => token > 1 && doc.IsPunct(token - 1, '.') && doc.Slice(token - 2) == "global";

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
        var edits = References(doc, old, includeMembers: member && !isGlobal).Select(r => new TextEdit(r.Start, r.End - r.Start, newName)).ToList();
        return new RenamePlan(edits, warnings);
    }

    /// <summary>Applies non-overlapping edits to a text.</summary>
    public static string Apply(string text, IEnumerable<TextEdit> edits)
    {
        var builder = new System.Text.StringBuilder(text);
        foreach (var edit in edits.OrderByDescending(e => e.Offset)) builder.Remove(edit.Offset, edit.Length).Insert(edit.Offset, edit.Text);
        return builder.ToString();
    }
}
