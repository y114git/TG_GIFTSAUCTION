namespace G3MStudio.Editor;

/// <summary>Takes the declaration of one function out of the GML of a larger script.</summary>
public static class FunctionSource
{
    /// <summary>
    /// The text without the declaration of <paramref name="name"/> (function name() {}, or var, static, self. or a plain name = function() {};), or null when it cannot be done safely:
    /// the name is declared more or less than once, the function sits inside another function, or it has no body.
    /// </summary>
    public static string? Remove(string text, string name)
    {
        var doc = GmlDoc.Parse(text);
        var all = GmlLint.Functions(doc);
        // The library leaves a member assignment unnamed; self.name = function() {} is the one form of it that names a function of the script.
        bool SelfAssigned(GmlFunction f) => f.Name < 0 && f.Keyword >= 4 && doc.IsPunct(f.Keyword - 1, '=') && doc.Slice(f.Keyword - 2) == name && doc.IsPunct(f.Keyword - 3, '.') && doc.Slice(f.Keyword - 4) == "self";
        var found = all.Where(f => f.Name >= 0 && doc.Slice(f.Name) == name || SelfAssigned(f)).ToList();
        if (found.Count != 1 || found[0] is not { Body: >= 0, End: >= 0 } function) return null;
        if (all.Any(o => o != function && o.Body >= 0 && o.End >= 0 && o.Keyword < function.Keyword && o.End > function.End)) return null;
        var assigned = function.Name < function.Keyword;
        // The statement starts at its modifier or target: var, static, self.
        var first = !assigned ? function.Keyword : SelfAssigned(function) ? function.Keyword - 4 : function.Name > 0 && doc.Slice(function.Name - 1) is "var" or "static" ? function.Name - 1 : function.Name;
        var start = doc.Start(first);
        var end = doc.End(function.End);
        if (assigned && end < text.Length && text[end] == ';') end++;
        // The line the declaration leaves empty goes with it.
        var lineStart = start == 0 ? 0 : text.LastIndexOf('\n', start - 1) + 1;
        if (string.IsNullOrWhiteSpace(text[lineStart..start]))
        {
            var lineEnd = text.IndexOf('\n', end);
            if (lineEnd < 0 ? string.IsNullOrWhiteSpace(text[end..]) : string.IsNullOrWhiteSpace(text[end..lineEnd])) (start, end) = (lineStart, lineEnd < 0 ? text.Length : lineEnd + 1);
        }

        return text.Remove(start, end - start);
    }
}
