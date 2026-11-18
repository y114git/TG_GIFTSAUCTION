using G3MLib.Analyzer.Compiler;
using G3MStudio.Localization;

namespace G3MStudio.Editor;

/// <summary>The pieces of the info card the editor shows when the pointer rests on a name: the card text and what can be read about a variable from its first assignment.</summary>
public static class GmlHover
{
    /// <summary>A card: the kind and name (with the value type when known), the doc comment, then where it is defined and how often this code uses it.</summary>
    public static string Card(string kind, string word, string? type = null, string? comment = null, string? where = null, int uses = 0)
    {
        var lines = new List<string> { kind + " " + word + (type is { Length: > 0 } ? ": " + type : "") };
        if (comment is { Length: > 0 }) lines.Add(comment);
        var tail = new List<string>();
        if (where is { Length: > 0 }) tail.Add(Loc.Current.Format("code.hover.where", ("where", where)));
        if (uses > 0) tail.Add(Loc.Current.Format("code.hover.uses", ("count", uses)));
        if (tail.Count > 0) lines.Add(string.Join("  |  ", tail));
        return string.Join('\n', lines);
    }

    /// <summary>An Explorer key (sprites:spr_x) as the card writes it.</summary>
    public static string Place(string key, G3MStudio.Core.Workspace.Nicknames? nicknames = null) =>
        nicknames?.Of(key) is { } nick && key.IndexOf(':', StringComparison.Ordinal) is var colon and >= 0 ? key[..colon] + ": " + nick : key.Replace(":", ": ", StringComparison.Ordinal);

    /// <summary>The first token that assigns the name (name = ...), as a plain name or as global.name; -1 when the text never assigns it.</summary>
    public static int FirstAssignment(GmlDoc doc, string word, bool global)
    {
        foreach (var token in doc.Named(word))
        {
            var member = token > 0 && doc.IsPunct(token - 1, '.');
            var isGlobal = member && token > 1 && doc.Slice(token - 2) == "global";
            if (global ? isGlobal : !member) if (token + 1 < doc.Tokens.Count && doc.IsPunct(token + 1, '=')) return token;
        }

        return -1;
    }

    /// <summary>The type of the value assigned after the name token (name = 5), judged from the literal: real, string, bool, array, struct, function or a constructor name. Null when it cannot be told.</summary>
    public static string? ValueType(GmlDoc doc, int name)
    {
        var value = name + 2;
        if (name < 0 || value >= doc.Tokens.Count || !doc.IsPunct(name + 1, '=')) return null;
        var text = doc.Slice(value);
        return doc.Tokens[value].Kind switch
        {
            GmlTokenKind.Number => "real",
            GmlTokenKind.String or GmlTokenKind.TemplateString => "string",
            GmlTokenKind.Boolean => "bool",
            GmlTokenKind.Keyword when text == "new" && value + 1 < doc.Tokens.Count => doc.Slice(value + 1),
            GmlTokenKind.Keyword when text is "function" or "undefined" => text,
            _ when text == "[" => "array",
            _ when text == "{" => "struct",
            _ => null,
        };
    }

    /// <summary>The line of a token, counted from one.</summary>
    public static int LineOf(GmlDoc doc, int token) => doc.Text.AsSpan(0, doc.Start(token)).Count('\n') + 1;
}
