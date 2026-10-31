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
/// shortcut: no incremental re-tokenize after an edit (call Tokenize again), add a debounced background pass when editing is built.
/// </summary>
public sealed class GmlColorizer : DocumentColorizingTransformer
{
    private static readonly GmlTokenKind[] Kinds = [GmlTokenKind.Keyword, GmlTokenKind.Boolean, GmlTokenKind.String, GmlTokenKind.TemplateString, GmlTokenKind.Number, GmlTokenKind.Comment, GmlTokenKind.Operator, GmlTokenKind.Preprocessor];

    /// <summary>What an identifier can name; each has a Syntax.* brush of the same name.</summary>
    public static readonly string[] Names = ["Function", "Variable", "Constant", "Asset"];

    private readonly Func<string, IBrush?> _resolve;
    private readonly Dictionary<GmlTokenKind, IBrush> _brushes = [];
    private readonly Dictionary<string, IBrush> _nameBrushes = [];
    private IReadOnlyList<GmlToken> _tokens = [];

    public GmlColorizer(Func<string, IBrush?> brush)
    {
        _resolve = brush;
        Reload();
    }

    /// <summary>Tells what a name stands for: one of <see cref="Names"/>, or null for a name that is just a name (a local variable).</summary>
    public Func<string, string?>? Classifier { get; set; }

    /// <summary>Looks the brushes up again, after the user changed a color.</summary>
    public void Reload()
    {
        _brushes.Clear();
        _nameBrushes.Clear();
        foreach (var kind in Kinds)
            if (_resolve("Syntax." + (kind == GmlTokenKind.TemplateString ? "String" : kind)) is { } found) _brushes[kind] = found;
        foreach (var name in Names)
            if (_resolve("Syntax." + name) is { } found) _nameBrushes[name] = found;
    }

    public void Tokenize(string text, bool gms2OrLater = true)
    {
        _tokens = GmlTokenizer.Tokenize(text, gms2OrLater);
        Foldings = BraceFoldings(text, _tokens);
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

    protected override void ColorizeLine(DocumentLine line)
    {
        int low = 0, high = _tokens.Count;
        while (low < high)
        {
            var mid = (low + high) / 2;
            if (_tokens[mid].Span.EndOffset <= line.Offset) low = mid + 1; else high = mid;
        }
        for (var i = low; i < _tokens.Count && _tokens[i].Span.StartOffset < line.EndOffset; i++)
        {
            var brush = BrushOf(_tokens[i]);
            if (brush is null) continue;
            var start = Math.Max(_tokens[i].Span.StartOffset, line.Offset);
            var end = Math.Min(_tokens[i].Span.EndOffset, line.EndOffset);
            if (end > start) ChangeLinePart(start, end, element => element.TextRunProperties.SetForegroundBrush(brush));
        }
    }

    private IBrush? BrushOf(GmlToken token)
    {
        if (token.Kind == GmlTokenKind.Identifier)
        {
            if (Classifier is null) return null;
            var span = token.Span;
            return Classifier(CurrentContext.Document.GetText(span.StartOffset, span.EndOffset - span.StartOffset)) is { } name && _nameBrushes.TryGetValue(name, out var named) ? named : null;
        }

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
        EditorOptions.ApplyTo(editor);
        editor.TextArea.TextView.LineTransformers.Add(colorizer);
        // A color the user changes in Settings shows in an open editor at once.
        var shown = colorizer;
        void Recolor()
        {
            shown.Reload();
            editor.TextArea.TextView.Redraw();
        }

        void Restyle() => EditorOptions.ApplyTo(editor);
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
