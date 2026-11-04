using Avalonia.Input;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;

namespace G3MStudio.Editor;

/// <summary>Name completion for the GML editor: after two typed letters, or on Ctrl+Space, a list of the built-in names and the resources of the open file that start with what was typed.</summary>
public sealed class GmlCompletion
{
    private const int MostShown = 200;

    private readonly TextEditor _editor;
    private readonly Func<IReadOnlyList<KeyValuePair<string, string>>> _names;
    private CompletionWindow? _window;

    public GmlCompletion(TextEditor editor, Func<IReadOnlyList<KeyValuePair<string, string>>> names)
    {
        (_editor, _names) = (editor, names);
        editor.TextArea.TextEntered += (_, e) =>
        {
            // One key sends one character; pasted or composed text may send several: the last one decides.
            if (e.Text is { Length: > 0 } text && IsWordChar(text[^1]) && _window is null && EditorOptions.AutoComplete) Show(false);
        };
        editor.TextArea.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Space || e.KeyModifiers != KeyModifiers.Control) return;
            Show(true);
            e.Handled = true;
        };
    }

    /// <summary>The names that start with the text (the exact case first), then those that contain it, built-in kinds after the file's own.</summary>
    public static IReadOnlyList<KeyValuePair<string, string>> Match(IEnumerable<KeyValuePair<string, string>> names, string prefix) =>
    [.. names
        .Where(n => n.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && n.Key.Length > prefix.Length)
        .OrderByDescending(n => n.Key.StartsWith(prefix, StringComparison.Ordinal))
        .ThenBy(n => n.Key.Length)
        .ThenBy(n => n.Key, StringComparer.Ordinal)
        .Take(MostShown)];

    private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_';

    private void Show(bool force)
    {
        var area = _editor.TextArea;
        var offset = area.Caret.Offset;
        var start = offset;
        while (start > 0 && IsWordChar(_editor.Document.GetCharAt(start - 1))) start--;
        var prefix = _editor.Document.GetText(start, offset - start);
        if (prefix.Length == 0 ? !force : prefix.Length < 2 && !force) return;
        // A number, a string or a comment is not a name: the colorizer's tokens are the judge, but a leading digit is enough to skip the common case.
        if (prefix.Length > 0 && char.IsDigit(prefix[0])) return;
        var matches = Match(_names(), prefix);
        if (matches.Count == 0) return;
        var window = new CompletionWindow(area) { StartOffset = start, CloseWhenCaretAtBeginning = true };
        foreach (var (name, kind) in matches) window.CompletionList.CompletionData.Add(new Entry(name, kind));
        window.Closed += (_, _) => _window = null;
        _window = window;
        window.Show();
    }

    private sealed class Entry(string name, string kind) : ICompletionData
    {
        public IImage? Image => null;

        public string Text => name;

        public object Content => name;

        public object Description => kind;

        public double Priority => 0;

        public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs) => textArea.Document.Replace(completionSegment, name);
    }
}
