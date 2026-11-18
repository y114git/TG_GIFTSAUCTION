using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Threading;
using System.Text.RegularExpressions;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

namespace G3MStudio.Editor;

// The editor commands of the registry (code.*): each id is rebindable on the Keyboard Shortcuts page and runs here.
public sealed partial class CodeEditorTab
{
    private (GmlDoc? Doc, IReadOnlyList<OutlineItem> Items) _outline;

    /// <summary>The outline of the text, built once per token document.</summary>
    public IReadOnlyList<OutlineItem> Outline(GmlDoc doc) => ReferenceEquals(_outline.Doc, doc) ? _outline.Items : (_outline = (doc, GmlStructure.Outline(doc))).Items;

    /// <summary>Runs a command of the registry on this editor. Unknown ids are ignored.</summary>
    public void Run(string id)
    {
        var text = Editor.Text;
        var (start, end) = Editor.SelectionLength == 0 ? (Editor.CaretOffset, Editor.CaretOffset) : (Editor.SelectionStart, Editor.SelectionStart + Editor.SelectionLength);
        switch (id)
        {
            case "code.goToDefinition": GoToDefinition(Editor.CaretOffset); break;
            case "code.peekDefinition": _ = PeekAsync(); break;
            case "code.parameterHints": UpdateSignatureHint(force: true); break;
            case "code.insertSnippet": ShowSnippets(); break;
            case "code.quickFix": ShowQuickFixes(); break;
            case "code.toggleBookmark": _bookmarks.AddOrRemove(Editor.Document, Editor.TextArea.Caret.Line); break;
            case "code.nextBookmark": GoToBookmark(forward: true); break;
            case "code.previousBookmark": GoToBookmark(forward: false); break;
            case "code.toggleDiff": ToggleDiff(); break;
            case "code.nextProblem": GoToProblem(forward: true); break;
            case "code.previousProblem": GoToProblem(forward: false); break;
            case "code.outline": ShowOutline(); break;
            case "code.toggleComment" when !Editor.IsReadOnly: Replace(GmlLineEdits.ToggleLineComment(text, start, end)); break;
            case "code.blockComment" when !Editor.IsReadOnly: Replace(GmlLineEdits.ToggleBlockComment(text, start, end)); break;
            case "code.indent" when !Editor.IsReadOnly: Replace(GmlLineEdits.Indent(text, start, end)); break;
            case "code.outdent" when !Editor.IsReadOnly: Replace(GmlLineEdits.Outdent(text, start, end)); break;
            case "code.duplicateLine" when !Editor.IsReadOnly: Replace(GmlLineEdits.DuplicateLines(text, start, end)); break;
            case "code.moveLineUp" when !Editor.IsReadOnly: if (GmlLineEdits.MoveLines(text, start, end, up: true) is { } up) Replace(up); break;
            case "code.moveLineDown" when !Editor.IsReadOnly: if (GmlLineEdits.MoveLines(text, start, end, up: false) is { } down) Replace(down); break;
            default: RunMore(id, text, start, end); break;
        }
    }

    // The built-in instance variables offered after a dot when the owner is not known.
    private static readonly string[] InstanceVariables = [.. GmlNames.Reference.Entries.Where(e => e.Kind == G3MStudio.Core.GmlDocs.RefKind.Variable && e.Category is "Instance" or "Movement" or "Sprite").Select(e => e.Name)];

    /// <summary>One document change (one undo step) for a line edit, then the selection it asks for.</summary>
    private void Replace(LineEdit edit)
    {
        var document = Editor.Document;
        document.BeginUpdate();
        try
        {
            document.Replace(edit.Offset, edit.Length, edit.Text);
        }
        finally
        {
            document.EndUpdate();
        }

        Editor.Select(edit.SelectionStart, Math.Max(0, edit.SelectionEnd - edit.SelectionStart));
    }

    private void ApplyEdits(IEnumerable<TextEdit> edits)
    {
        var document = Editor.Document;
        document.BeginUpdate();
        try
        {
            foreach (var edit in edits.OrderByDescending(e => e.Offset)) document.Replace(edit.Offset, edit.Length, edit.Text);
        }
        finally
        {
            document.EndUpdate();
        }
    }

    private void MoveCaret(int offset)
    {
        Editor.CaretOffset = Math.Clamp(offset, 0, Editor.Document.TextLength);
        Editor.ScrollToLine(Editor.TextArea.Caret.Line);
        Editor.TextArea.Caret.BringCaretToView();
    }

    /// <summary>F12: a local, a parameter or a function declared in this text moves the caret there; a resource or a script opens its tab.</summary>
    public void GoToDefinition(int offset)
    {
        if (GmlNames.WordAt(Editor.Text, offset) is not { } word) return;
        if (DeclarationOffset(word) is { } declared) MoveCaret(declared);
        else if (GmlNames.Definitions(_workspace)(word) is { } key) GoToRequested?.Invoke(key);
    }

    /// <summary>Puts the caret on the declaration of a name in this text; the caret stays when the text does not declare it.</summary>
    public void GoToDeclaration(string name)
    {
        if (DeclarationOffset(name) is { } declared) MoveCaret(declared);
    }

    /// <summary>Where a name is declared in this text: a function block or a var, static, global or parameter, as an offset. Null when it is not.</summary>
    public int? DeclarationOffset(string name)
    {
        var doc = Doc;
        if (Outline(doc).FirstOrDefault(o => o.Name == name && o.Kind is "function" or "enum" or "macro") is { } item) return item.Offset;
        if (GmlSymbols.DeclarationOf(doc, name) is var token and >= 0) return doc.Start(token);
        return GmlSymbols.EnumMemberOf(doc, name) is { } member ? doc.Start(member.Token) : null;
    }

    /// <summary>
    /// The uses of a var local or a parameter of the function around the caret (or of the code outside every function), for Find Usages: such a name belongs
    /// to that function alone. Null for any other name (macros, enums, statics and the rest are seen by all code), which is searched in all code.
    /// </summary>
    public IReadOnlyList<NameHit>? LocalUsages(string name)
    {
        var doc = Doc;
        var text = doc.Text;
        var functions = GmlLint.Functions(doc);
        // The innermost function holding the token, or -1; functions come in text order, so a nested one is found last.
        int Owner(int token)
        {
            var found = -1;
            for (var k = 0; k < functions.Count; k++)
                if (functions[k].Keyword <= token && token <= (functions[k].End >= 0 ? functions[k].End : functions[k].Close)) found = k;
            return found;
        }

        var caret = doc.TokenAt(Editor.CaretOffset);
        var scope = caret < 0 ? -1 : Owner(caret);
        var vars = GmlScopes.VarTokens(text, doc.Tokens);
        var own = doc.Named(name).Where(t => Owner(t) == scope).ToList();
        var parameter = scope >= 0 && GmlLint.Parameters(doc, functions[scope]).Any(p => doc.Slice(p.Token) == name);
        if (!parameter && !own.Any(vars.Contains)) return null;
        var spans = own.Where(t => t == 0 || !doc.IsPunct(t - 1, '.')).Select(t => (Start: doc.Start(t), End: doc.End(t)));
        var lines = text.Split('\n');
        return [.. spans.Select(s =>
        {
            var line = text.AsSpan(0, s.Start).Count('\n');
            return new NameHit(Target, line + 1, lines[line].Trim(), false);
        })];
    }

    /// <summary>The whole text last shown by Peek Definition (its own line numbers are the real ones), for tests.</summary>
    public string? PeekText { get; private set; }

    /// <summary>The one-based line of the definition in <see cref="PeekText"/>, for tests.</summary>
    public int PeekLine { get; private set; }

    private Popup? _peek;
    private AvaloniaEdit.TextEditor? _peekEditor;

    /// <summary>The first line shown in the Peek Definition popup (one-based), for tests; 0 when no peek is open.</summary>
    public int PeekTopLine => _peekEditor is { } e && e.TextArea.TextView.GetDocumentLineByVisualTop(e.VerticalOffset) is { } line ? line.LineNumber : 0;

    private static DockPanel PeekWithHeader(string title, Control peek)
    {
        var header = new TextBlock { Text = title, Classes = { "muted" }, Margin = new Thickness(6, 2), TextTrimming = TextTrimming.CharacterEllipsis };
        DockPanel.SetDock(header, Avalonia.Controls.Dock.Top);
        return new DockPanel { Children = { header, peek } };
    }

    private async Task PeekAsync()
    {
        if (GmlNames.WordAt(Editor.Text, Editor.CaretOffset) is not { } word) return;
        string shown;
        int at;
        string? title = null; // the entry the code comes from, when a nickname makes its name differ from what the code says
        if (DeclarationOffset(word) is { } own) (shown, at) = (Editor.Text, own);
        else if (GmlNames.Definitions(_workspace)(word) is { } key && ResourceTypes.SplitKey(key) is ("code", var entry))
        {
            title = _workspace.Nicknames.CodeLabel(entry) is var label && label != entry ? $"{label} ({entry})" : null;
            shown = await Task.Run(() => { using (_workspace.Lock.Read()) return CodeText.Decompile(_workspace.Data, entry, _workspace.DecompileContext()); });
            at = DefinitionIn(shown, GmlNames.Real(_workspace, word));
        }
        else { GoToDefinition(Editor.CaretOffset); return; }
        PeekText = shown;
        PeekLine = LineOf(shown, at);
        var peek = GmlColorizer.CreateEditor(shown, readOnly: true, out var peekColors);
        peekColors.Classifier = GmlNames.For(_workspace); // functions and assets get the colours they have in the file itself
        peek.Width = 560;
        peek.Height = 220;
        var popup = new Popup { PlacementTarget = Editor.TextArea, Placement = PlacementMode.BottomEdgeAlignedLeft, PlacementRect = CaretRect(), IsLightDismissEnabled = true, Child = new Border { Child = title is null ? peek : PeekWithHeader(title, peek), Background = (IBrush?)Application.Current?.FindResource("Bg.Surface"), BorderThickness = new Thickness(2), BorderBrush = (IBrush?)Application.Current?.FindResource("Text.Accent") } };
        _peek = popup;
        _peekEditor = peek;
        Children.Insert(0, popup); // the editor stays the last child, so it keeps filling the tab
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) };
        popup.Closed += (_, _) =>
        {
            timer.Stop(); // the scroll loop below must not hold the closed editor
            if (ReferenceEquals(_peek, popup)) (_peek, _peekEditor) = (null, null);
            Dispatcher.UIThread.Post(() => Children.Remove(popup)); // removed at once it breaks the tree walk of a window that closes with the peek open
            Editor.TextArea.Focus(); // a closed popup leaves no focus, and keys would go nowhere
        };
        popup.IsOpen = true;
        // The whole file is shown, scrolled so the definition is the first line; the text above stays reachable.
        var line = PeekLine;
        // ScrollTo centres a line, and the editor only takes a scroll once it is laid out, so the definition is brought to the top by centring the line half a view below it.
        // The scroll is tried again for a few frames until it holds.
        var tries = 0;
        timer.Tick += (_, _) =>
        {
            var below = (int)(peek.Height / 2 / Math.Max(1, peek.TextArea.TextView.DefaultLineHeight));
            peek.CaretOffset = peek.Document.GetLineByNumber(line).Offset;
            peek.ScrollTo(Math.Min(line + below, peek.Document.LineCount), 1);
            // Done once scrolled; also when there is nothing to scroll (the definition is on line 1 or the text fits), so the view is not pulled back for 40 frames.
            var fits = peek.ExtentHeight > 0 && peek.ExtentHeight <= peek.ViewportHeight;
            if (peek.VerticalOffset > 0 || line <= 1 || fits || ++tries > 40 || !popup.IsOpen) timer.Stop();
        };
        timer.Start();
    }

    // The offset of "function name" or "name = function" in a decompiled script; the start when the text declares it another way.
    private static int DefinitionIn(string text, string name)
    {
        var at = Regex.Match(text, @"\bfunction\s+" + Regex.Escape(name) + @"\b|\b" + Regex.Escape(name) + @"\s*=\s*function\b");
        return at.Success ? at.Index : 0;
    }

    private static int LineOf(string text, int offset) => 1 + text.AsSpan(0, Math.Clamp(offset, 0, text.Length)).Count('\n');

    private Rect CaretRect()
    {
        var view = Editor.TextArea.TextView;
        var top = view.GetVisualPosition(Editor.TextArea.Caret.Position, AvaloniaEdit.Rendering.VisualYPosition.LineTop) - view.ScrollOffset;
        return new Rect(top.X, top.Y, 1, Math.Max(view.DefaultLineHeight, 12));
    }

    private readonly Popup _signaturePopup = new() { IsLightDismissEnabled = false, Placement = PlacementMode.Bottom };
    private readonly TextBlock _signatureText = new() { Name = "SignatureText" };
    private readonly TextBlock _signatureDoc = new() { Name = "SignatureDoc", Opacity = 0.75, MaxWidth = 420, TextWrapping = TextWrapping.Wrap };

    /// <summary>The description line under the hint (active parameter, then what the function does), for tests. Empty for a function without documentation.</summary>
    public string SignatureDocShown => _signatureDoc.Text ?? "";
    private bool _signatureWanted;

    /// <summary>The signature hint shown now, as text segments (the active parameter is marked), for tests. Empty when no hint is open.</summary>
    public IReadOnlyList<(string Text, bool Active)> SignatureShown { get; private set; } = [];

    /// <summary>The call at the caret and its signature, from the last 3000 characters before the caret (a text this size is tokenized at a keystroke).</summary>
    public (Signature Signature, int Argument)? CallAtCaret()
    {
        var caret = Editor.CaretOffset;
        var from = Math.Max(0, caret - 3000);
        var window = Editor.Document.GetText(from, caret - from);
        if (GmlSignatures.CallAt(GmlDoc.Parse(window), window.Length) is not { } call) return null;
        // A function of this text keeps its /// doc comment: the hint shows the text of each @param.
        var own = ((IOverlayHost)this).FreshDoc is { } fresh && GameFunctions.FromDoc(fresh, call.Name, null) is { } entry ? Signature.FromReference(entry) : null;
        return (own ?? GmlNames.SignatureOf(_workspace, call.Name), call.Argument) is ({ } sig, var argument) ? (sig, argument) : null;
    }

    private void UpdateSignatureHint(bool force = false)
    {
        if (force) _signatureWanted = true;
        if (!_signatureWanted && SignatureShown.Count == 0) return;
        var call = Editor.IsReadOnly ? null : CallAtCaret();
        if (call is not var (signature, argument))
        {
            _signatureWanted = false;
            SignatureShown = [];
            _signatureDoc.Text = "";
            _signaturePopup.IsOpen = false;
            return;
        }

        _signatureWanted = true;
        SignatureShown = signature.Segments(argument);
        _signatureDoc.Text = GmlHelp.ActiveParameter(signature, argument);
        _signatureDoc.IsVisible = _signatureDoc.Text.Length > 0;
        _signatureText.Inlines?.Clear();
        foreach (var (piece, active) in SignatureShown) _signatureText.Inlines?.Add(new Avalonia.Controls.Documents.Run(piece) { FontWeight = active ? FontWeight.Bold : FontWeight.Normal });
        _signaturePopup.PlacementRect = CaretRect();
        _signaturePopup.IsOpen = true;
    }

    /// <summary>Ctrl+J: a list of code templates at the caret.</summary>
    public void ShowSnippets()
    {
        if (Editor.IsReadOnly) return;
        var window = new CompletionWindow(Editor.TextArea) { StartOffset = Editor.CaretOffset, EndOffset = Editor.CaretOffset };
        foreach (var snippet in GmlSnippets.All) window.CompletionList.CompletionData.Add(new SnippetEntry(snippet));
        window.Show();
    }

    private sealed class SnippetEntry(Snippet snippet) : ICompletionData
    {
        public Avalonia.Media.IImage? Image => null;

        public string Text => snippet.Name;

        public object Content => snippet.Name;

        public object Description => snippet.Body.Replace("$0", "", StringComparison.Ordinal);

        public double Priority => 0;

        public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
        {
            var (insert, caret) = GmlSnippets.Expand(snippet, textArea.Document.Text, completionSegment.Offset);
            textArea.Document.Replace(completionSegment, insert);
            textArea.Caret.Offset = caret;
        }
    }

    /// <summary>The fixes offered at the caret: spelling of an unknown name, declaring it, commenting out a line that has a problem.</summary>
    public IReadOnlyList<QuickFix> QuickFixesAtCaret()
    {
        var line = Editor.TextArea.Caret.Line;
        var onLine = ((IOverlayHost)this).ShownProblems.Any(p => p.Line == line);
        var fixes = GmlQuickFixes.For(Doc, Editor.CaretOffset, name => GmlNames.For(_workspace)(name) is not null, GmlNames.Completions(_workspace)().Select(c => c.Key), onLine);
        // The code check's own fixes come first; a suggestion both make is listed once.
        return [.. LintFixesAt(Editor.CaretOffset).Concat(fixes).DistinctBy(f => (f.Key, f.Argument))];
    }

    /// <summary>The menu text of a fix: a suggested name that is a resource with a nickname shows the nickname and the name behind it; the edit still writes the name.</summary>
    internal string QuickFixTitle(QuickFix fix) => Loc.Current.Format(fix.Key, ("name", fix.Key == "code.fix.rename" ? _workspace.Nicknames.BothNamed(fix.Argument) : fix.Argument));

    private void ShowQuickFixes()
    {
        if (Editor.IsReadOnly) return;
        var menu = new ContextMenu();
        var fixes = QuickFixesAtCaret();
        var version = _textVersion;
        foreach (var fix in fixes)
        {
            var item = new MenuItem { Header = QuickFixTitle(fix) };
            item.Click += (_, _) => { if (version == _textVersion) ApplyEdits(fix.Edits); }; // the offsets belong to the text the menu was made for
            menu.Items.Add(item);
        }

        if (fixes.Count == 0) menu.Items.Add(new MenuItem { Header = Loc.Current["code.fix.none"], IsEnabled = false });
        menu.Open(Editor.TextArea);
    }

    /// <summary>One based lines with a bookmark.</summary>
    public IReadOnlyList<int> BookmarkLines => _bookmarks.Lines;

    private void GoToBookmark(bool forward)
    {
        if (_bookmarks.Next(Editor.TextArea.Caret.Line, forward) is { } line) GoTo(line, 1);
    }

    private void GoToProblem(bool forward)
    {
        var all = _workspace.Problems.All.Where(p => p.Source == Target).OrderBy(p => p.Line).ThenBy(p => p.Column).ToList();
        if (all.Count == 0) return;
        var (line, column) = (Editor.TextArea.Caret.Line, Editor.TextArea.Caret.Column);
        var target = forward ? all.FirstOrDefault(p => (p.Line, p.Column).CompareTo((line, column)) > 0, all[0]) : all.LastOrDefault(p => (p.Line, p.Column).CompareTo((line, column)) < 0, all[^1]);
        GoTo(target.Line, target.Column);
    }

    /// <summary>F2 in the text: a local is renamed here with a preview; a resource name goes to the resource rename; any other name in all code (strings too).</summary>
    public void RenameSymbol()
    {
        if (Editor.IsReadOnly || GmlNames.WordAt(Editor.Text, Editor.CaretOffset) is not { } word) return;
        var doc = Doc;
        var token = doc.TokenAt(Editor.CaretOffset);
        var member = token > 0 && doc.IsPunct(token - 1, '.') && !(token > 1 && doc.Slice(token - 2) == "global");
        if (!member && GmlSymbols.DeclarationOf(doc, word) >= 0) _ = RenameLocalAsync(doc, Editor.CaretOffset, word);
        else if (GmlUsages.Resolve(_workspace, word) is var (type, name)) RenameResourceRequested?.Invoke(type, name);
        else RenameAcrossRequested?.Invoke(word);
    }

    /// <summary>The preview line of a local rename: the warnings, then how many places change.</summary>
    public string RenamePreview(GmlDoc doc, int offset, string newName)
    {
        var known = GmlNames.For(_workspace);
        if (GmlSymbols.PlanRename(doc, offset, newName, name => known(name) is not null) is not { } plan) return "";
        var lines = plan.Warnings.Select(w => Loc.Current[w]).Append(Loc.Current.Format("code.rename.count", ("count", plan.Edits.Count)));
        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>Renames the local at the offset in the editor text: one undo step in the editor, applied to the data with Apply. Returns false when there is nothing to rename.</summary>
    public bool RenameLocalNow(int offset, string newName)
    {
        var known = GmlNames.For(_workspace);
        if (GmlSymbols.PlanRename(Doc, offset, newName, name => known(name) is not null) is not { Edits.Count: > 0 } plan) return false;
        ApplyEdits(plan.Edits);
        return true;
    }

    private async Task RenameLocalAsync(GmlDoc doc, int offset, string word)
    {
        if (TopLevel.GetTopLevel(this) is not Window owner) return;
        await new RenameWindow(word, newName => RenameLocalNow(offset, newName) ? null : Loc.Current["code.rename.none"], Loc.Current["code.rename.title"], options: new RenameOptions(newName => RenamePreview(doc, offset, newName))).AskAsync(owner);
    }

    private readonly TextBlock _crumbText = new() { Name = "BreadcrumbText", Classes = { "muted" }, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
    private readonly Button _outlineButton = new() { Name = "OutlineButton" };

    internal StackPanel Crumbs { get; private set; } = new();

    /// <summary>The names around the caret, outermost first, joined with " > ".</summary>
    public string Breadcrumb => _crumbText.Text ?? "";

    private void BuildNavigation()
    {
        _outlineButton.Content = Loc.Current["code.outlineButton"];
        _outlineButton.Click += (_, _) => ShowOutline();
        Crumbs = new StackPanel { Name = "Breadcrumb", Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 8, Margin = new Thickness(8, 0), Children = { _crumbText } };
        _signaturePopup.Child = new Border { Child = new StackPanel { Spacing = 2, Children = { _signatureText, _signatureDoc } }, Padding = new Thickness(6, 2), Background = (IBrush?)Application.Current?.FindResource("Bg.Raised") };
        _signaturePopup.PlacementTarget = Editor.TextArea;
        Children.Add(_signaturePopup);
    }

    private void UpdateBreadcrumb()
    {
        if (((IOverlayHost)this).FreshDoc is not { } doc) return;
        _crumbText.Text = string.Join("  >  ", GmlStructure.Around(Outline(doc), Editor.CaretOffset).Select(i => i.Name));
    }

    /// <summary>The functions, enums, macros and regions of the text, in order, as the outline list shows them.</summary>
    public IReadOnlyList<OutlineItem> OutlineItems => Outline(Doc);

    private static string KindText(string kind) => Loc.Current["code.outlineKind." + kind] is var text && text != "code.outlineKind." + kind ? text : kind;

    private void ShowOutline()
    {
        var menu = new ContextMenu();
        foreach (var item in OutlineItems)
        {
            var entry = new MenuItem { Header = $"{KindText(item.Kind)}  {item.Name}" };
            var offset = item.Offset;
            entry.Click += (_, _) => MoveCaret(offset);
            menu.Items.Add(entry);
        }

        if (menu.Items.Count == 0) menu.Items.Add(new MenuItem { Header = Loc.Current["code.outlineEmpty"], IsEnabled = false });
        menu.Placement = PlacementMode.Bottom;
        menu.Open(_outlineButton);
    }
}
