using System.Globalization;
using Avalonia.Controls;
using G3MStudio.Localization;

namespace G3MStudio.Editor;

// Help on the word under the caret, go to line and bracket, expand and shrink selection, and the line and case edits added to the registry.
public sealed partial class CodeEditorTab
{
    /// <summary>F1: the function reference should show the word under the caret (empty when there is none).</summary>
    public event Action<string>? HelpRequested;

    /// <summary>A manual page was asked for (Open in manual): the web address.</summary>
    public event Action<string>? ManualRequested;

    private readonly List<(int Start, int End)> _grown = [];
    private bool _growing;

    private void RunMore(string id, string text, int start, int end)
    {
        var writable = !Editor.IsReadOnly;
        switch (id)
        {
            case "code.helpOnWord": HelpRequested?.Invoke(GmlNames.WordAt(text, Editor.CaretOffset) ?? ""); break;
            case "code.openInManual": ManualRequested?.Invoke(ManualLinkAtCaret()); break;
            case "code.goToLine": _ = AskLineAsync(); break;
            case "code.goToBracket": GoToMatchingBracket(); break;
            case "code.expandSelection": ExpandSelection(); break;
            case "code.shrinkSelection": ShrinkSelection(); break;
            case "code.deleteLine" when writable: Replace(GmlLineEdits.DeleteLines(text, start, end)); break;
            case "code.joinLines" when writable: if (GmlLineEdits.JoinLines(text, start, end) is { } joined) Replace(joined); break;
            case "code.sortLines" when writable: if (GmlLineEdits.SortLines(text, start, end) is { } sorted) Replace(sorted); break;
            case "code.trimWhitespace" when writable: ApplyEdits(GmlLineEdits.TrimTrailingWhitespace(text, start, end)); break;
            case "code.upperCase" when writable: ConvertCase(text, start, end, GmlLineEdits.CaseMode.Upper); break;
            case "code.lowerCase" when writable: ConvertCase(text, start, end, GmlLineEdits.CaseMode.Lower); break;
            case "code.titleCase" when writable: ConvertCase(text, start, end, GmlLineEdits.CaseMode.Title); break;
            default: break;
        }
    }

    private void ConvertCase(string text, int start, int end, GmlLineEdits.CaseMode mode)
    {
        if (GmlLineEdits.ConvertCase(text, start, end, mode) is { } edit) Replace(edit);
    }

    /// <summary>Puts text at the caret in place of the selection (the Insert of the function reference).</summary>
    public void InsertAtCaret(string text)
    {
        if (Editor.IsReadOnly) return;
        Editor.Document.Replace(Editor.SelectionLength > 0 ? Editor.SelectionStart : Editor.CaretOffset, Editor.SelectionLength, text);
        Editor.Focus();
    }

    /// <summary>The manual page of the word under the caret, or the manual index when the word has no entry.</summary>
    public string ManualLinkAtCaret()
    {
        var reference = GmlNames.Reference;
        return reference.Find(GmlNames.WordAt(Editor.Text, Editor.CaretOffset) ?? "") is { } entry ? reference.ManualLink(entry) : reference.Manual.TrimEnd('/') + ".htm";
    }

    private async Task AskLineAsync()
    {
        if (TopLevel.GetTopLevel(this) is not Window owner) return;
        var lines = Editor.Document.LineCount;
        var label = Loc.Current.Format("code.goToLine.label", ("max", lines));
        await new RenameWindow(Editor.TextArea.Caret.Line.ToString(CultureInfo.InvariantCulture), text => GoToLineText(text) ? null : Loc.Current.Format("code.goToLine.bad", ("max", lines)), Loc.Current["cmd.code.goToLine"], label, Loc.Current["code.goToLine.ok"]).AskAsync(owner);
    }

    /// <summary>Moves the caret to "line" or "line:column" (one based). False when the text is not a line of this code.</summary>
    public bool GoToLineText(string text)
    {
        var parts = text.Split(':', 2, StringSplitOptions.TrimEntries);
        if (!int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var line) || line < 1 || line > Editor.Document.LineCount) return false;
        var column = 1;
        if (parts.Length == 2 && !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out column)) return false;
        GoTo(line, Math.Max(1, column));
        return true;
    }

    /// <summary>Moves the caret to the bracket that pairs with the one at the caret. False when the caret is not at a bracket.</summary>
    public bool GoToMatchingBracket()
    {
        if (GmlStructure.MatchingBrackets(Doc, Editor.CaretOffset) is not { } pair) return false;
        // From an opening bracket the caret lands after the closing one, from a closing bracket it lands before the opening one.
        MoveCaret(pair.Second > pair.First ? pair.Second + 1 : pair.Second);
        return true;
    }

    /// <summary>Selects the next larger syntax range around the selection.</summary>
    public void ExpandSelection()
    {
        var (start, end) = (Editor.SelectionStart, Editor.SelectionStart + Editor.SelectionLength);
        if (Editor.SelectionLength == 0) (start, end) = (Editor.CaretOffset, Editor.CaretOffset);
        if (GmlSelection.Expand(Doc, start, end) is not { } next) return;
        _grown.Add((start, end));
        _growing = true;
        try
        {
            Editor.Select(next.Start, next.End - next.Start);
        }
        finally
        {
            _growing = false;
        }
    }

    // The stack of earlier selections is only valid for the text and the selection Expand left behind.
    private void InstallGrowth()
    {
        Editor.TextChanged += (_, _) => { if (!_growing) _grown.Clear(); };
        Editor.TextArea.SelectionChanged += (_, _) => { if (!_growing) _grown.Clear(); };
    }

    /// <summary>Goes back to the selection before the last expand.</summary>
    public void ShrinkSelection()
    {
        if (_grown.Count == 0) return;
        var (start, end) = _grown[^1];
        _grown.RemoveAt(_grown.Count - 1);
        var length = Editor.Document.TextLength;
        start = Math.Clamp(start, 0, length);
        end = Math.Clamp(end, start, length);
        _growing = true;
        try
        {
            Editor.Select(start, end - start);
        }
        finally
        {
            _growing = false;
        }
    }
}
