using G3MLib.Analyzer.Compiler;

namespace G3MStudio.Editor;

/// <summary>
/// One tokenized GML text, shared by every code feature (hover, brackets, outline, references, rename), so the text is tokenized once per version.
/// Lookups are binary searches or dictionary reads; the editor builds a new document after typing stops, never per keystroke.
/// </summary>
public sealed class GmlDoc
{
    private readonly Lazy<Dictionary<int, int>> _pairs;
    private readonly Lazy<Dictionary<string, List<int>>> _byName;

    private GmlDoc(string text, IReadOnlyList<GmlToken> tokens)
    {
        Text = text;
        Tokens = tokens;
        _pairs = new Lazy<Dictionary<int, int>>(BuildPairs);
        _byName = new Lazy<Dictionary<string, List<int>>>(BuildNames);
    }

    public string Text { get; }

    public IReadOnlyList<GmlToken> Tokens { get; }

    public static GmlDoc Parse(string text, bool gms2OrLater = true) => new(text, GmlTokenizer.Tokenize(text, gms2OrLater));

    public static GmlDoc FromTokens(string text, IReadOnlyList<GmlToken> tokens) => new(text, tokens);

    public int Start(int token) => Tokens[token].Span.StartOffset;

    public int End(int token) => Tokens[token].Span.EndOffset;

    public string Slice(int token) => Text[Start(token)..End(token)];

    public bool IsPunct(int token, char c) => Tokens[token].Kind is GmlTokenKind.Separator or GmlTokenKind.Operator && End(token) - Start(token) == 1 && Text[Start(token)] == c;

    /// <summary>The token that contains the offset, or the one that ends right at it (a caret behind a name). -1 when there is none.</summary>
    public int TokenAt(int offset)
    {
        int low = 0, high = Tokens.Count;
        while (low < high)
        {
            var mid = (low + high) / 2;
            if (End(mid) < offset) low = mid + 1; else high = mid;
        }

        if (low >= Tokens.Count || Start(low) > offset) return -1;
        // At the edge between two tokens the one that starts here wins, unless it is a space-less punctuation and the other one is a name.
        return low + 1 < Tokens.Count && End(low) == offset && Start(low + 1) == offset && Tokens[low + 1].Kind == GmlTokenKind.Identifier ? low + 1 : low;
    }

    /// <summary>The token that starts exactly at the offset, or -1.</summary>
    public int TokenStartingAt(int offset)
    {
        int low = 0, high = Tokens.Count;
        while (low < high)
        {
            var mid = (low + high) / 2;
            if (Start(mid) < offset) low = mid + 1; else high = mid;
        }

        return low < Tokens.Count && Start(low) == offset ? low : -1;
    }

    /// <summary>Token index of the bracket that pairs with the bracket token, or -1.</summary>
    public int PairOf(int token) => _pairs.Value.GetValueOrDefault(token, -1);

    /// <summary>Token indexes of every identifier with the name, in text order.</summary>
    public IReadOnlyList<int> Named(string name) => _byName.Value.TryGetValue(name, out var found) ? found : [];

    private Dictionary<int, int> BuildPairs()
    {
        var pairs = new Dictionary<int, int>();
        var open = new Stack<int>();
        for (var i = 0; i < Tokens.Count; i++)
        {
            if (Tokens[i].Kind != GmlTokenKind.Separator || End(i) - Start(i) != 1) continue;
            var c = Text[Start(i)];
            if (c is '(' or '[' or '{') open.Push(i);
            else if (c is ')' or ']' or '}' && open.Count > 0 && Closer(Text[Start(open.Peek())]) == c)
            {
                var o = open.Pop();
                pairs[o] = i;
                pairs[i] = o;
            }
        }

        return pairs;
    }

    private Dictionary<string, List<int>> BuildNames()
    {
        var names = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        for (var i = 0; i < Tokens.Count; i++)
        {
            if (Tokens[i].Kind != GmlTokenKind.Identifier) continue;
            var name = Slice(i);
            if (!names.TryGetValue(name, out var list)) names[name] = list = [];
            list.Add(i);
        }

        return names;
    }

    public static char Closer(char open) => open switch { '(' => ')', '[' => ']', _ => '}' };
}
