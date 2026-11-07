namespace G3MStudio.Editor;

/// <summary>A code template. <paramref name="Body"/> uses \n for line breaks and $0 for the caret position.</summary>
public sealed record Snippet(string Name, string Body);

/// <summary>The built-in GML templates and how one is put into a text: its lines take the indentation of the line it starts on.</summary>
public static class GmlSnippets
{
    public static readonly IReadOnlyList<Snippet> All =
    [
        new("if", "if ($0)\n{\n    \n}"),
        new("ifelse", "if ($0)\n{\n    \n}\nelse\n{\n    \n}"),
        new("for", "for (var i = 0; i < $0; i++)\n{\n    \n}"),
        new("foreach array", "for (var i = 0; i < array_length($0); i++)\n{\n    \n}"),
        new("while", "while ($0)\n{\n    \n}"),
        new("repeat", "repeat ($0)\n{\n    \n}"),
        new("switch", "switch ($0)\n{\n    case 0:\n        break;\n    default:\n        break;\n}"),
        new("with", "with ($0)\n{\n    \n}"),
        new("function", "function $0()\n{\n    \n}"),
        new("trycatch", "try\n{\n    $0\n}\ncatch (e)\n{\n    show_debug_message(e.message);\n}"),
        new("enum", "enum $0\n{\n    a,\n    b\n}"),
        new("region", "#region $0\n\n#endregion"),
    ];

    /// <summary>The text to insert at <paramref name="offset"/> and where the caret goes inside it (an offset into the new full text).</summary>
    public static (string Text, int Caret) Expand(Snippet snippet, string document, int offset)
    {
        var lineStart = offset <= 0 ? 0 : document.LastIndexOf('\n', offset - 1) + 1;
        var indent = new string([.. document[lineStart..offset].TakeWhile(c => c is ' ' or '\t')]);
        var nl = document.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var body = snippet.Body.Replace("\n", nl + indent, StringComparison.Ordinal);
        var marker = body.IndexOf("$0", StringComparison.Ordinal);
        return marker < 0 ? (body, offset + body.Length) : (body.Remove(marker, 2), offset + marker);
    }
}
