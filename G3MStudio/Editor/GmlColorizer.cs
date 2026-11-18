using Avalonia;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Folding;
using AvaloniaEdit.Rendering;
using G3MLib.Analyzer.Compiler;

namespace G3MStudio.Editor;

/// <summary>
/// Colors a text view from G3MLib's GML tokens. The tokens of the whole text are computed once; each visible line then
/// looks up its tokens by binary search, so scrolling costs per line, not per document. An identifier is colored by what it names
/// (function, variable, constant, asset) when a <see cref="Classifier"/> is set.
/// An edit does not blank or misplace the colours: the tokens keep the offsets of the text they were made from, and every edit made since is replayed
/// on them when a line is drawn, so untouched text keeps its colours until the next Tokenize swaps in the new tokens in one step.
/// shortcut: the text an edit adds has no colour of its own until the next Tokenize (it takes the token it grew from), a local re-tokenize could remove that.
/// </summary>
public sealed class GmlColorizer : DocumentColorizingTransformer
{
    private static readonly GmlTokenKind[] Kinds = [GmlTokenKind.Keyword, GmlTokenKind.Boolean, GmlTokenKind.String, GmlTokenKind.TemplateString, GmlTokenKind.Number, GmlTokenKind.Comment, GmlTokenKind.Operator, GmlTokenKind.Preprocessor];

    /// <summary>What an identifier can name; each has a Syntax.* brush of the same name.</summary>
    public static readonly string[] Names = ["Function", "Variable", "Constant", "Asset", GmlScopes.Local, GmlScopes.Global];

    private readonly Func<string, IBrush?> _resolve;
    private readonly Dictionary<GmlTokenKind, IBrush> _brushes = [];
    private readonly Dictionary<string, IBrush> _nameBrushes = [];
    private Dictionary<int, string> _scopes = [];
    private readonly IBrush?[] _bracketBrushes = new IBrush?[3];
    private Dictionary<int, int> _bracketDepth = [];

    // The text the tokens were made from, and the edits made to the document since (in the order they happened).
    private string _text = "";
    private readonly List<(int Offset, int Removed, int Inserted)> _edits = [];

    // Map replays every edit for every token drawn, so past this many (a Replace All) the colours wait for the next Tokenize instead of shifting.
    private const int MaxEdits = 256;
    private bool _stale;

    public GmlColorizer(Func<string, IBrush?> brush)
    {
        _resolve = brush;
        Reload();
    }

    /// <summary>Tells what a name stands for: one of <see cref="Names"/>, or null for a name that is just a name (a local variable).</summary>
    public Func<string, string?>? Classifier { get; set; }

    // The tokens of the last Tokenize call, for the other features that read the same text.
    public IReadOnlyList<GmlToken> Tokens { get; private set; } = [];

    /// <summary>Looks the brushes up again, after the user changed a color.</summary>
    public void Reload()
    {
        _brushes.Clear();
        _nameBrushes.Clear();
        foreach (var kind in Kinds)
            if (_resolve("Syntax." + (kind == GmlTokenKind.TemplateString ? "String" : kind)) is { } found) _brushes[kind] = found;
        foreach (var name in Names)
            if (_resolve("Syntax." + name) is { } found) _nameBrushes[name] = found;
        for (var i = 0; i < _bracketBrushes.Length; i++) _bracketBrushes[i] = _resolve("Syntax.Bracket" + (i + 1));
    }

    /// <summary>Tells the colorizer the document changed; the colours follow the edit until the next <see cref="Tokenize"/>.</summary>
    public void Edited(int offset, int removed, int inserted)
    {
        // Typing is a run of insertions, each right after the last: one edit stands for the run.
        if (removed == 0 && _edits.Count > 0 && _edits[^1] is { Removed: 0 } last && offset == last.Offset + last.Inserted) _edits[^1] = last with { Inserted = last.Inserted + inserted };
        else if (_edits.Count < MaxEdits) _edits.Add((offset, removed, inserted));
        else _stale = true;
    }

    // A position of the tokenized text in the text now. A token end at an insertion point moves with the insertion, so a typed letter joins the word before it.
    private int Map(int position, bool end)
    {
        foreach (var (offset, removed, inserted) in _edits)
        {
            if (position < offset) continue;
            if (position == offset) position = end && removed == 0 ? offset + inserted : offset;
            else if (position >= offset + removed) position += inserted - removed;
            else position = end ? offset : offset + inserted;
        }

        return position;
    }

    public void Tokenize(string text, bool gms2OrLater = true)
    {
        _text = text;
        _edits.Clear();
        _stale = false;
        Tokens = GmlTokenizer.Tokenize(text, gms2OrLater);
        _scopes = GmlScopes.Find(text, Tokens);
        Foldings = BraceFoldings(text, Tokens);
        _bracketDepth = BracketDepths(text, Tokens);
    }

    /// <summary>The { } blocks of the last tokenized text that span more than one line, in offset order. Braces inside strings and comments are not tokens of their own.</summary>
    public IReadOnlyList<NewFolding> Foldings { get; private set; } = [];

    private static List<NewFolding> BraceFoldings(string text, IReadOnlyList<GmlToken> tokens)
    {
        var open = new Stack<int>();
        var found = new List<NewFolding>();
        foreach (var token in tokens)
        {
            if (token.Kind != GmlTokenKind.Separator || token.Span.EndOffset - token.Span.StartOffset != 1) continue;
            if (text[token.Span.StartOffset] == '{') open.Push(token.Span.StartOffset);
            else if (text[token.Span.StartOffset] == '}' && open.Count > 0)
            {
                var start = open.Pop();
                if (text.AsSpan(start, token.Span.EndOffset - start).Contains((char)10)) found.Add(new NewFolding(start, token.Span.EndOffset));
            }
        }

        found.Sort((a, b) => a.StartOffset.CompareTo(b.StartOffset));
        return found;
    }

    /// <summary>The nesting level of each bracket token (opening and closing alike), counted over all three bracket kinds; brackets in strings and comments are not tokens.</summary>
    internal static Dictionary<int, int> BracketDepths(string text, IReadOnlyList<GmlToken> tokens)
    {
        var depths = new Dictionary<int, int>();
        var open = new Stack<int>();
        for (var i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];
            if (token.Kind != GmlTokenKind.Separator || token.Span.EndOffset - token.Span.StartOffset != 1) continue;
            var c = text[token.Span.StartOffset];
            if (c is '(' or '[' or '{')
            {
                depths[i] = open.Count;
                open.Push(i);
            }
            else if (c is ')' or ']' or '}' && open.Count > 0)
            {
                depths[i] = depths[open.Pop()];
            }
        }

        return depths;
    }

    protected override void ColorizeLine(DocumentLine line)
    {
        if (_stale) return;
        int low = 0, high = Tokens.Count;
        while (low < high)
        {
            var mid = (low + high) / 2;
            if (Map(Tokens[mid].Span.EndOffset, true) <= line.Offset) low = mid + 1; else high = mid;
        }
        for (var i = low; i < Tokens.Count && Map(Tokens[i].Span.StartOffset, false) < line.EndOffset; i++)
        {
            var brush = BrushOf(i);
            if (brush is null) continue;
            var start = Math.Max(Map(Tokens[i].Span.StartOffset, false), line.Offset);
            var end = Math.Min(Map(Tokens[i].Span.EndOffset, true), Math.Min(line.EndOffset, CurrentContext.Document.TextLength));
            if (end > start) ChangeLinePart(start, end, element => element.TextRunProperties.SetForegroundBrush(brush));
        }
    }

    internal IBrush? BrushOf(int index)
    {
        var token = Tokens[index];
        if (token.Kind == GmlTokenKind.Identifier)
        {
            if (_scopes.TryGetValue(index, out var scope) && _nameBrushes.TryGetValue(scope, out var scoped)) return scoped;
            // A name nothing classifies is still a variable: it must not share the colour of operators and punctuation.
            _nameBrushes.TryGetValue("Variable", out var variable);
            if (Classifier is null) return variable;
            var span = token.Span;
            return Classifier(_text[span.StartOffset..span.EndOffset]) is { } name && _nameBrushes.TryGetValue(name, out var named) ? named : variable;
        }

        if (token.Kind == GmlTokenKind.Separator && EditorOptions.BracketColors && _bracketDepth.TryGetValue(index, out var depth)) return _bracketBrushes[depth % _bracketBrushes.Length];
        return _brushes.GetValueOrDefault(token.Kind);
    }

    /// <summary>A highlighted GML view of the text, using the Syntax.* brushes of the theme. Read-only unless asked otherwise.</summary>
    public static TextEditor CreateEditor(string text, bool readOnly = true) => CreateEditor(text, readOnly, out _);

    /// <summary>As above, and hands back the colorizer so an editable view can tokenize again after a change.</summary>
    public static TextEditor CreateEditor(string text, bool readOnly, out GmlColorizer colorizer)
    {
        colorizer = new GmlColorizer(Resource<IBrush>);
        colorizer.Tokenize(text);
        var editor = new TextEditor
        {
            IsReadOnly = readOnly,
            ShowLineNumbers = true,
            FontFamily = Resource<FontFamily>("Font.Code") ?? FontFamily.Default,
            Document = new TextDocument(text),
        };
        EditorOptions.ApplyTo(editor, Resource<FontFamily>("Font.Code") ?? FontFamily.Default);
        editor.TextArea.TextView.LineTransformers.Add(colorizer);
        // The colours follow every edit until the tokens are made again, also on a document that replaces this one.
        var tracked = colorizer;
        void Track(object? sender, DocumentChangeEventArgs e) => tracked.Edited(e.Offset, e.RemovalLength, e.InsertionLength);
        editor.Document.Changed += Track;
        editor.DocumentChanged += (_, e) =>
        {
            if (e.OldDocument is { } old) old.Changed -= Track;
            if (e.NewDocument is { } fresh) fresh.Changed += Track;
        };
        editor.TextArea.TextView.ElementGenerators.Add(new NoLigatureGenerator());
        // Ctrl + wheel changes the size of the text in every code view (UTMT-CODE-11).
        editor.AddHandler(Avalonia.Input.InputElement.PointerWheelChangedEvent, (_, e) =>
        {
            if (e.KeyModifiers != Avalonia.Input.KeyModifiers.Control || e.Delta.Y == 0) return;
            EditorOptions.Zoom(e.Delta.Y > 0 ? 1 : -1);
            e.Handled = true;
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        // A color the user changes in Settings shows in an open editor at once.
        var shown = colorizer;
        void Recolor()
        {
            shown.Reload();
            editor.TextArea.TextView.Redraw();
        }

        void Restyle()
        {
            EditorOptions.ApplyTo(editor, Resource<FontFamily>("Font.Code") ?? FontFamily.Default);
            editor.TextArea.TextView.Redraw(); // bracket colors
        }
        editor.AttachedToVisualTree += (_, _) =>
        {
            ThemeChanged += Recolor;
            EditorOptions.Changed += Restyle;
            Restyle();
        };
        editor.DetachedFromVisualTree += (_, _) =>
        {
            ThemeChanged -= Recolor;
            EditorOptions.Changed -= Restyle;
        };
        return editor;
    }

    /// <summary>Raised after the colors of the theme changed.</summary>
    public static event Action? ThemeChanged;

    public static void NotifyThemeChanged() => ThemeChanged?.Invoke();

    private static T? Resource<T>(string key) where T : class => Application.Current is { } app && app.TryGetResource(key, app.ActualThemeVariant, out var found) ? found as T : null;
}
