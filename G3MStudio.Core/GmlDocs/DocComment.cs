namespace G3MStudio.Core.GmlDocs;

/// <summary>One @param line of a doc comment. The type is empty when the comment gives none.</summary>
public sealed record DocParam(string Name, string Type, string Meaning, bool Optional);

/// <summary>What the /// lines above a declaration say: a description, the parameters and what is returned.</summary>
public sealed record DocInfo(string Summary, IReadOnlyList<DocParam> Parameters, string Returns);

/// <summary>Reads GameMaker style doc comments (/// @param {real} x The position, /// @returns {bool} ...).</summary>
public static class DocComment
{
    /// <summary>The doc comment on the lines directly above the line that holds the offset, or null when there is none.</summary>
    public static DocInfo? Before(string text, int offset)
    {
        var end = offset <= 0 ? 0 : text.LastIndexOf('\n', Math.Min(offset, text.Length) - 1) + 1;
        var lines = new List<string>();
        while (end > 0)
        {
            var start = end < 2 ? 0 : text.LastIndexOf('\n', end - 2) + 1;
            var line = text[start..end].Trim();
            if (!line.StartsWith("///", StringComparison.Ordinal)) break;
            lines.Insert(0, line[3..].Trim());
            end = start;
        }

        return lines.Count == 0 ? null : Parse(lines);
    }

    public static DocInfo Parse(IEnumerable<string> lines)
    {
        var summary = new List<string>();
        var parameters = new List<DocParam>();
        var returns = "";
        foreach (var line in lines)
        {
            var (tag, rest) = Split(line);
            switch (tag)
            {
                case "@param" or "@arg" or "@argument": parameters.Add(Param(rest)); break;
                case "@returns" or "@return": returns = Type(ref rest); break;
                case "@description" or "@desc": if (rest.Length > 0) summary.Add(rest); break;
                case null: if (line.Length > 0) summary.Add(line); break;
            }
        }

        return new DocInfo(string.Join(' ', summary), parameters, returns);
    }

    private static (string? Tag, string Text) Split(string line)
    {
        if (!line.StartsWith('@')) return (null, line);
        var space = line.IndexOf(' ', StringComparison.Ordinal);
        return space < 0 ? (line, "") : (line[..space], line[(space + 1)..].Trim());
    }

    // "{type} name text", "name {type} text" or "name text"; [name] and [name=1] mark an optional parameter.
    private static DocParam Param(string rest)
    {
        var type = Type(ref rest);
        var space = rest.IndexOf(' ', StringComparison.Ordinal);
        var name = space < 0 ? rest : rest[..space];
        rest = space < 0 ? "" : rest[(space + 1)..].Trim();
        if (type.Length == 0) type = Type(ref rest);
        var optional = name.StartsWith('[');
        name = name.Trim('[', ']');
        if (name.IndexOf('=', StringComparison.Ordinal) is var eq and >= 0) name = name[..eq];
        return new DocParam(name, type, rest.TrimStart('-', ' '), optional);
    }

    // Takes a leading {type} off the text.
    private static string Type(ref string rest)
    {
        if (!rest.StartsWith('{') || rest.IndexOf('}', StringComparison.Ordinal) is not (var close and > 0)) return "";
        var type = rest[1..close].Trim();
        rest = rest[(close + 1)..].Trim();
        return type;
    }
}
