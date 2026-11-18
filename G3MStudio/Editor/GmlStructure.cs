using AvaloniaEdit.Folding;
using G3MLib.Analyzer.Compiler;

namespace G3MStudio.Editor;

/// <summary>One named block of the file: a function, an enum, a macro or a region.</summary>
public sealed record OutlineItem(string Name, string Kind, int Offset, int End);

/// <summary>The outline (functions, enums, regions, macros), the breadcrumb of the caret, the fold ranges and bracket pairs, all read from tokens.</summary>
public static class GmlStructure
{
    public static IReadOnlyList<OutlineItem> Outline(GmlDoc doc)
    {
        var items = new List<OutlineItem>();
        var regions = new Stack<(string Name, int Offset)>();
        for (var i = 0; i < doc.Tokens.Count; i++)
        {
            var kind = doc.Tokens[i].Kind;
            var word = kind is GmlTokenKind.Keyword or GmlTokenKind.Preprocessor ? doc.Slice(i) : "";
            var hasNext = i + 1 < doc.Tokens.Count;
            if (word == "#region") regions.Push((NameAfter(doc, i), doc.Start(i)));
            else if (word == "#endregion" && regions.Count > 0) Close(items, regions.Pop(), doc.End(i));
            else if (word == "#macro" && hasNext) items.Add(new OutlineItem(doc.Slice(i + 1), "macro", doc.Start(i), doc.End(i + 1)));
            else if (word == "enum" && hasNext && doc.Tokens[i + 1].Kind == GmlTokenKind.Identifier) items.Add(Block(doc, i + 1, "enum", doc.Start(i)));
            else if (word == "function" && FunctionName(doc, i) is { } named) items.Add(Block(doc, named, "function", doc.Start(i)));
        }

        foreach (var open in regions) Close(items, open, doc.Text.Length);
        return [.. items.OrderBy(i => i.Offset).ThenByDescending(i => i.End)];
    }

    private static void Close(List<OutlineItem> items, (string Name, int Offset) region, int end) => items.Add(new OutlineItem(region.Name, "region", region.Offset, end));

    private static string NameAfter(GmlDoc doc, int token)
    {
        var line = doc.Text.IndexOf('\n', doc.End(token));
        var rest = doc.Text[doc.End(token)..(line < 0 ? doc.Text.Length : line)].Trim();
        return rest.Length == 0 ? "region" : rest;
    }

    // function name(...) or name = function(...): the identifier that names it, as a token index.
    private static int? FunctionName(GmlDoc doc, int keyword)
    {
        if (keyword + 1 < doc.Tokens.Count && doc.Tokens[keyword + 1].Kind == GmlTokenKind.Identifier) return keyword + 1;
        return keyword >= 2 && doc.Slice(keyword - 1) == "=" && doc.Tokens[keyword - 2].Kind == GmlTokenKind.Identifier ? keyword - 2 : null;
    }

    private static OutlineItem Block(GmlDoc doc, int name, string kind, int start)
    {
        // The body is the first { after the name that is not inside the parameter brackets.
        var end = doc.End(name);
        for (var j = name; j < doc.Tokens.Count && j < name + 400; j++)
        {
            if (doc.IsPunct(j, '(') && doc.PairOf(j) is var close and >= 0) j = close;
            else if (doc.IsPunct(j, '{'))
            {
                end = doc.PairOf(j) is var pair and >= 0 ? doc.End(pair) : doc.End(j);
                break;
            }
            else if (doc.IsPunct(j, ';')) break;
        }

        return new OutlineItem(doc.Slice(name), kind, start, end);
    }

    /// <summary>The blocks around the offset, outermost first (the breadcrumb).</summary>
    public static IReadOnlyList<OutlineItem> Around(IReadOnlyList<OutlineItem> outline, int offset) =>
        [.. outline.Where(i => i.Kind != "macro" && i.Offset <= offset && offset <= i.End).OrderBy(i => i.Offset)];

    /// <summary>The ranges that fold: multi-line braces, regions and multi-line block comments, in offset order.</summary>
    public static IReadOnlyList<NewFolding> Foldings(GmlDoc doc)
    {
        var found = new List<NewFolding>();
        for (var i = 0; i < doc.Tokens.Count; i++)
        {
            var (start, end) = (doc.Start(i), doc.End(i));
            if (doc.IsPunct(i, '{') && doc.PairOf(i) is var close and >= 0 && doc.End(close) is var closeEnd && HasNewline(doc.Text, start, closeEnd)) found.Add(new NewFolding(start, closeEnd) { Name = "{...}" });
            else if (doc.Tokens[i].Kind == GmlTokenKind.Comment && end - start > 2 && doc.Text.AsSpan(start, 2).SequenceEqual("/*") && HasNewline(doc.Text, start, end)) found.Add(new NewFolding(start, end) { Name = "/*...*/" });
        }

        foreach (var item in Outline(doc).Where(o => o.Kind == "region" && HasNewline(doc.Text, o.Offset, o.End))) found.Add(new NewFolding(item.Offset, item.End) { Name = "#region " + item.Name });
        found.Sort((a, b) => a.StartOffset.CompareTo(b.StartOffset));
        return found;
    }

    private static bool HasNewline(string text, int start, int end) => text.AsSpan(start, end - start).Contains('\n');

    /// <summary>The offsets of the two brackets that pair with the bracket at or before the caret, or null. A bracket right after the caret wins over one right before it.</summary>
    public static (int First, int Second)? MatchingBrackets(GmlDoc doc, int caret)
    {
        foreach (var offset in new[] { caret, caret - 1 })
        {
            var token = offset < 0 ? -1 : doc.TokenStartingAt(offset);
            if (token < 0 || doc.Tokens[token].Kind != GmlTokenKind.Separator) continue;
            if (doc.PairOf(token) is var pair and >= 0) return (doc.Start(token), doc.Start(pair));
        }

        return null;
    }
}
