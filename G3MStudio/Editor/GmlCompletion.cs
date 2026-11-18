using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using G3MStudio.Core.GmlDocs;
using G3MStudio.Explorer;
using G3MStudio.Localization;

namespace G3MStudio.Editor;

/// <summary>
/// Completion for the GML editor: after two typed letters, or on Ctrl+Space, a list of the built-in names, the resources of the open file, keywords and templates
/// that start with what was typed; after a dot, the members the text already uses. A function is inserted as name(param, param) with a tab stop on each required parameter.
/// </summary>
public sealed class GmlCompletion
{
    private const int MostShown = 200;

    // Words that start a statement or a clause.
    public static readonly string[] Keywords =
    [
        "if", "else", "for", "while", "do", "until", "repeat", "switch", "case", "default", "break", "continue", "return", "exit", "function", "var", "globalvar", "static",
        "enum", "with", "new", "delete", "try", "catch", "finally", "throw", "constructor", "then", "begin", "end", "not", "and", "or", "xor", "mod", "div",
    ];

    private readonly TextEditor _editor;
    private readonly Func<IReadOnlyList<KeyValuePair<string, string>>> _names;
    private readonly Func<string, Signature?> _signature;
    private readonly Func<GmlDoc, int, IReadOnlyList<KeyValuePair<string, string>>>? _members;
    private readonly Func<string, bool> _isGame;
    private readonly Func<IReadOnlyDictionary<string, string>>? _aliases;
    private readonly Func<string, string>? _place;
    private readonly Func<GmlDoc?>? _doc;
    private (GmlDoc Doc, IReadOnlyList<KeyValuePair<string, string>> Names)? _own;
    private CompletionWindow? _window;
    private TabStops? _stops;

    /// <param name="editor">The editor to complete in.</param>
    /// <param name="source">Where the names, signatures and members come from.</param>
    public GmlCompletion(TextEditor editor, GmlCompletionSource source)
    {
        (_aliases, _place, _doc) = (source.Aliases, source.Place, source.Doc);
        (_editor, _names, _signature, _members, _isGame) = (editor, source.Names, source.Signature ?? (_ => null), source.Members, source.IsGame ?? (_ => false));
        editor.TextArea.TextEntered += (_, e) =>
        {
            // One key sends one character; pasted or composed text may send several: the last one decides.
            if (e.Text is not { Length: > 0 } text || _window is not null || !EditorOptions.AutoComplete) return;
            if (IsWordChar(text[^1])) Show(false);
            else if (text[^1] == '.') Show(true);
        };
        editor.TextArea.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Space || e.KeyModifiers != KeyModifiers.Control) return;
            Show(true);
            e.Handled = true;
        };
        editor.TextArea.AddHandler(InputElement.KeyDownEvent, OnTab, Avalonia.Interactivity.RoutingStrategies.Tunnel);
    }

    /// <summary>Whether a call was inserted and its parameters are still being walked with Tab, for tests.</summary>
    public bool InParameters => _stops is not null;

    /// <summary>The names that start with the text (the exact case first), then those that contain it, built-in kinds after the file's own.</summary>
    public static IReadOnlyList<KeyValuePair<string, string>> Match(IEnumerable<KeyValuePair<string, string>> names, string prefix) =>
    [.. names
        .Where(n => n.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && n.Key.Length > prefix.Length)
        .OrderByDescending(n => n.Key.StartsWith(prefix, StringComparison.Ordinal))
        .ThenBy(n => n.Key.Length)
        .ThenBy(n => n.Key, StringComparer.Ordinal)
        .Take(MostShown)];

    /// <summary>The list for a typed prefix: names, then keywords and templates. Pure, so it can be tested without a window.</summary>
    public static IReadOnlyList<KeyValuePair<string, string>> Candidates(IEnumerable<KeyValuePair<string, string>> names, string prefix) =>
        Match(names.Concat(Keywords.Select(k => new KeyValuePair<string, string>(k, "Keyword"))).Concat(GmlSnippets.All.Select(s => new KeyValuePair<string, string>(s.Name, "Snippet"))), prefix);

    /// <summary>The names the text declares: locals and parameters as variables, its functions, its macros and enums as constants.</summary>
    public static IReadOnlyList<KeyValuePair<string, string>> DocNames(GmlDoc doc)
    {
        var found = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var item in GmlStructure.Outline(doc))
            if (item.Kind != "region") found.TryAdd(item.Name, item.Kind == "function" ? "Function" : "Constant");
        var (locals, globals) = GmlScopes.Declarations(doc.Text, doc.Tokens);
        foreach (var name in locals.Keys.Concat(globals.Keys)) found.TryAdd(name, "Variable");
        return [.. found];
    }

    private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_';

    private void Show(bool force)
    {
        var area = _editor.TextArea;
        var offset = area.Caret.Offset;
        var start = offset;
        while (start > 0 && IsWordChar(_editor.Document.GetCharAt(start - 1))) start--;
        var prefix = _editor.Document.GetText(start, offset - start);
        var afterDot = start > 0 && _editor.Document.GetCharAt(start - 1) == '.';
        if (prefix.Length == 0 ? !force : prefix.Length < 2 && !force && !afterDot) return;
        // A number, a string or a comment is not a name: the colorizer's tokens are the judge, but a leading digit is enough to skip the common case.
        if (prefix.Length > 0 && char.IsDigit(prefix[0])) return;
        IReadOnlyList<KeyValuePair<string, string>> matches;
        if (afterDot)
        {
            if (_members is null || start >= 2 && char.IsDigit(_editor.Document.GetCharAt(start - 2))) return;
            // shortcut: the text is tokenized again for each dot, fine for a code entry; share the tab's token document if it ever shows.
            var members = _members(GmlDoc.Parse(_editor.Text), start - 1);
            matches = prefix.Length == 0 ? [.. members.Take(MostShown)] : Match(members, prefix);
        }
        else
        {
            // What this text declares (locals, parameters, its functions, macros and enums) comes first; a game name with the same spelling is the same entry.
            // The tokens of the last typing pause: a name declared since then comes with the next pause, and a key press never tokenizes the text.
            var doc = _doc?.Invoke() ?? GmlDoc.Parse(_editor.Text);
            if (!ReferenceEquals(_own?.Doc, doc)) _own = (doc, DocNames(doc));
            var own = _own.Value.Names;
            var keys = own.Select(o => o.Key).ToHashSet(StringComparer.Ordinal);
            matches = Candidates(own.Concat(_names().Where(n => !keys.Contains(n.Key))), prefix);
        }

        // A nickname that is one word completes too, and inserts the real name of its resource.
        var nicknames = afterDot || prefix.Length == 0 || _aliases is null ? [] : _aliases().Where(a => a.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && a.Key.Length > prefix.Length).OrderBy(a => a.Key.Length).ThenBy(a => a.Key, StringComparer.Ordinal).Take(MostShown).ToList();
        if (matches.Count == 0 && nicknames.Count == 0) return;
        var window = new CompletionWindow(area) { StartOffset = start, CloseWhenCaretAtBeginning = true };
        foreach (var (name, kind) in matches) window.CompletionList.CompletionData.Add(Make(name, kind));
        foreach (var (nick, real) in nicknames) window.CompletionList.CompletionData.Add(new Entry(nick, _names().FirstOrDefault(n => n.Key == real).Value ?? "Asset", false, () => null, this, real));
        window.Closed += (_, _) => _window = null;
        _window = window;
        window.Show();
    }

    private Entry Make(string name, string kind) => new(name, kind, kind == "Function" && _isGame(name), kind == "Function" ? () => _signature(name) : () => null, this);

    private void OnTab(object? sender, KeyEventArgs e)
    {
        if (_stops is null) return;
        if (e.Key is Key.Tab && e.KeyModifiers is KeyModifiers.None or KeyModifiers.Shift)
        {
            // A caret that left the call (a click elsewhere) ends the walk and the key does its normal work.
            if (!_stops.Contains(_editor.CaretOffset)) EndStops();
            else e.Handled = _stops.Step(e.KeyModifiers == KeyModifiers.Shift ? -1 : 1);
            if (_stops?.Done == true) EndStops();
        }
        else if (e.Key == Key.Escape) EndStops();
    }

    internal void StartStops(CallSnippet snippet, int origin)
    {
        EndStops();
        _stops = new TabStops(_editor, snippet, origin, EndStops);
        if (_stops.Done) EndStops();
    }

    private void EndStops()
    {
        _stops?.Detach();
        _stops = null;
    }

    /// <summary>The tab stops of an inserted call: anchors that follow typing, so a parameter replaced by a longer text keeps the next stop in place.</summary>
    private sealed class TabStops
    {
        private readonly Action _ended;
        private readonly TextEditor _editor;
        private readonly List<(TextAnchor Start, TextAnchor End)> _stops = [];
        private readonly TextAnchor _end;
        private int _index = -1;

        public TabStops(TextEditor editor, CallSnippet snippet, int origin, Action ended)
        {
            (_editor, _ended) = (editor, ended);
            var document = editor.Document;
            foreach (var (start, length) in snippet.Stops)
            {
                var a = document.CreateAnchor(origin + start);
                a.MovementType = AnchorMovementType.BeforeInsertion;
                var b = document.CreateAnchor(origin + start + length);
                b.MovementType = AnchorMovementType.AfterInsertion;
                _stops.Add((a, b));
            }

            _end = document.CreateAnchor(origin + snippet.End);
            Step(1);
            if (Done) return;
            document.Changed += OnChanged;
            editor.TextArea.Caret.PositionChanged += OnCaret;
        }

        public bool Done { get; private set; }

        /// <summary>Whether the offset is inside the inserted call.</summary>
        public bool Contains(int offset) => Alive && offset >= _stops[0].Start.Offset && offset <= _end.Offset;

        // The anchors are deleted when the text around them is replaced (a load).
        private bool Alive => !_stops[0].Start.IsDeleted && !_end.IsDeleted;

        private void OnChanged(object? sender, DocumentChangeEventArgs e)
        {
            if (!Alive || e.Offset < _stops[0].Start.Offset || e.Offset + e.InsertionLength > _end.Offset) _ended();
        }

        private void OnCaret(object? sender, EventArgs e)
        {
            if (!Contains(_editor.CaretOffset)) _ended();
        }

        public void Detach()
        {
            _editor.Document.Changed -= OnChanged;
            _editor.TextArea.Caret.PositionChanged -= OnCaret;
        }

        /// <summary>Selects the next (or previous) stop; past the last one the caret goes after the closing bracket. False when there was no stop to go to.</summary>
        public bool Step(int direction)
        {
            var next = _index + direction;
            if (next < 0) return false; // Shift+Tab at the first stop is left to the editor
            if (next >= _stops.Count)
            {
                _editor.CaretOffset = _end.Offset;
                Done = true;
                return true;
            }

            _index = next;
            var (a, b) = _stops[next];
            _editor.Select(a.Offset, Math.Max(0, b.Offset - a.Offset));
            return true;
        }
    }

    private sealed class Entry(string name, string kind, bool game, Func<Signature?> signatureOf, GmlCompletion owner, string? insert = null) : ICompletionData
    {
        // Read when the row is shown or the entry is chosen: a game function reads its code for the parameter names.
        private readonly Lazy<Signature?> _signature = new(signatureOf);

        private RefEntry? Doc => game ? _signature.Value?.Doc : kind is "Keyword" or "Snippet" ? null : GmlNames.Reference.Find(name);

        public IImage? Image => null;

        public string Text => name;

        public object Content => Row();

        public object Description => kind == "Asset" && HoverCards.KeyOfName?.Invoke(insert ?? name) is { } key ? HoverCards.Card(key, kind)
            : Doc is { } doc ? GmlHelp.Describe(doc, name, owner._place) : game ? Loc.Current["code.hover.gameFunction"] : kind;

        public double Priority => 0;

        private StackPanel Row()
        {
            var badge = kind switch { "Function" => "f", "Variable" => "v", "Constant" => "c", "Asset" => "a", "Keyword" => "k", "Snippet" => "s", _ => "?" };
            var key = kind switch { "Function" => "Syntax.Function", "Variable" => "Syntax.Variable", "Constant" => "Syntax.Constant", "Asset" => "Syntax.Asset", "Keyword" => "Syntax.Keyword", _ => "Syntax.Operator" };
            var row = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 6 };
            row.Children.Add(new TextBlock { Text = badge, Width = 12, FontWeight = FontWeight.Bold, Foreground = Application.Current?.FindResource(key) as IBrush });
            row.Children.Add(new TextBlock { Text = name });
            if (insert is not null) row.Children.Add(new TextBlock { Text = "-> " + insert, Opacity = 0.6 });
            else if ((kind == "Asset" || game) && HoverCards.KeyOfName?.Invoke(name) is { } own && HoverCards.Describe?.Invoke(own)?.Nickname is { } nickname) row.Children.Add(new TextBlock { Text = nickname, Opacity = 0.6 });
            var doc = Doc;
            var hint = _signature.Value is not null && doc is not null ? doc.Signature[name.Length..] : doc?.Summary;
            if (hint is { Length: > 0 }) row.Children.Add(new TextBlock { Text = hint, Opacity = 0.6, MaxWidth = 360, TextTrimming = TextTrimming.CharacterEllipsis });
            if (kind == "Function") row.Children.Add(new TextBlock { Text = Loc.Current[game ? "code.tag.game" : "code.tag.builtin"], Opacity = 0.5, FontSize = 10, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center });
            if (EditorOptions.IsFavourite(name)) row.Children.Add(new TextBlock { Text = "★", VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center });
            return row;
        }

        public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
        {
            var document = textArea.Document;
            if (insert is not null)
            {
                document.Replace(completionSegment, insert);
                return;
            }

            if (kind == "Snippet")
            {
                var snippet = GmlSnippets.All.First(s => s.Name == name);
                var (insert, caret) = GmlSnippets.Expand(snippet, document.Text, completionSegment.Offset);
                document.Replace(completionSegment, insert);
                textArea.Caret.Offset = caret;
                return;
            }

            var end = completionSegment.EndOffset;
            var called = end < document.TextLength && document.GetCharAt(end) == '(';
            var signature = _signature.Value;
            if (signature is null || called)
            {
                document.Replace(completionSegment, name);
                return;
            }

            var call = CallSnippet.For(signature);
            var origin = completionSegment.Offset;
            document.Replace(completionSegment, call.Text);
            owner.StartStops(call, origin);
        }
    }
}

/// <summary>What the completion reads.</summary>
/// <param name="Names">The names to complete (name and kind: Function, Variable, Constant, Asset).</param>
/// <param name="Signature">The signature of a function name, for the inserted parameters; a null result inserts the bare name.</param>
/// <param name="Members">The names after a dot, given the text and the offset of the dot; null turns member completion off.</param>
/// <param name="IsGame">Whether a function name is defined by the game, not by GameMaker; the list tells them apart.</param>
/// <param name="Aliases">Nicknames that are one word, each with the name of its resource; they complete and insert the name. Null leaves them out.</param>
/// <param name="Place">The code entry a game function is defined in as the interface shows it (its nickname); null shows the entry name.</param>
/// <param name="Doc">The tokens of the text from the last typing pause, so the names the text declares are not tokenized per key; null tokenizes the text.</param>
public sealed record GmlCompletionSource(
    Func<IReadOnlyList<KeyValuePair<string, string>>> Names,
    Func<string, Signature?>? Signature = null,
    Func<GmlDoc, int, IReadOnlyList<KeyValuePair<string, string>>>? Members = null,
    Func<string, bool>? IsGame = null,
    Func<IReadOnlyDictionary<string, string>>? Aliases = null,
    Func<string, string>? Place = null,
    Func<GmlDoc?>? Doc = null);
