using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using AvaloniaEdit.Rendering;
using G3MLib.Analyzer.Compiler;
using G3MStudio.Core.Workspace;
using G3MStudio.Explorer;
using G3MStudio.Localization;

namespace G3MStudio.Editor;

// Overlays (brackets, same name, indent guides, squiggles, colour swatches, bookmarks), hover and the context menu.
// Everything reads one GmlDoc per text version; between a key press and the next tokenize the overlays skip drawing.
public sealed partial class CodeEditorTab : IOverlayHost
{
    private readonly DispatcherTimer _hover = new() { Interval = TimeSpan.FromMilliseconds(EditorOptions.HoverDelayMs) };
    private readonly BookmarkMargin _bookmarks = new();
    private GmlDoc? _doc;
    private int _textVersion, _docVersion = -1, _problemsVersion = -1;
    private Point _hoverPoint;

    /// <summary>The tokens of the text in the editor. Built now when the text changed since the last tokenize (a command, not a key press).</summary>
    public GmlDoc Doc
    {
        get
        {
            if (_doc is null || _docVersion != _textVersion)
            {
                _doc = GmlDoc.Parse(Editor.Text);
                _docVersion = _textVersion;
            }

            return _doc;
        }
    }

    GmlDoc? IOverlayHost.FreshDoc => _doc is not null && _docVersion == _textVersion ? _doc : null;

    IReadOnlyList<Problem> IOverlayHost.ShownProblems => _problemsVersion == _textVersion ? [.. _workspace.Problems.All.Where(p => p.Source == Target)] : [];

    /// <summary>Find usages of a name was asked for (context menu or Shift+F12).</summary>
    public event Action<string>? FindUsagesRequested;

    /// <summary>Rename of a resource name was asked for: Explorer type and name.</summary>
    public event Action<string, string>? RenameResourceRequested;

    /// <summary>Rename of a name that is not local and not a resource (a global, a variable, a function) in all code.</summary>
    public event Action<string>? RenameAcrossRequested;

    private void InstallIntel(Workspace workspace)
    {
        var view = Editor.TextArea.TextView;
        view.BackgroundRenderers.Add(new HighlightRenderer(this));
        view.BackgroundRenderers.Add(new IndentGuideRenderer(this));
        view.BackgroundRenderers.Add(new SquiggleRenderer(this));
        view.ElementGenerators.Add(new ColorSwatchGenerator(this));
        Editor.TextArea.LeftMargins.Insert(0, _bookmarks);
        Editor.TextChanged += (_, _) => _textVersion++;
        Editor.TextArea.Caret.PositionChanged += (_, _) =>
        {
            view.InvalidateLayer(KnownLayer.Selection);
            UpdateBreadcrumb();
            UpdateSignatureHint();
        };
        // Static events are held only while the tab is shown, so a closed tab can be collected.
        void Guides() => view.InvalidateLayer(KnownLayer.Background);
        void Relabel(object? sender, System.ComponentModel.PropertyChangedEventArgs e) => _outlineButton.Content = Loc.Current["code.outlineButton"];
        void GameRead(Workspace read) { if (ReferenceEquals(read, workspace)) UpdateSignatureHint(); }
        AttachedToVisualTree += (_, _) =>
        {
            GameFunctions.Ready += GameRead;
            EditorOptions.Changed += Guides;
        };
        LanguageWatch.Follow(this, Relabel);
        DetachedFromVisualTree += (_, _) =>
        {
            GameFunctions.Ready -= GameRead;
            EditorOptions.Changed -= Guides;
            _hover.Stop();
        };
        Editor.TextArea.TextEntering += OnTextEntering;
        Editor.TextArea.TextEntered += OnTextEntered;
        _hover.Tick += (_, _) => ShowHover();
        view.PointerMoved += (_, e) =>
        {
            // Opening the card makes the platform repeat the last pointer position; that is no movement and must not close the card.
            if (e.GetPosition(Editor) == _hoverPoint) return;
            _hover.Stop();
            ToolTip.SetIsOpen(Editor, false);
            _hoverPoint = e.GetPosition(Editor);
            _hover.Interval = TimeSpan.FromMilliseconds(EditorOptions.HoverDelayMs);
            _hover.Start();
        };
        void HideCard()
        {
            _hover.Stop();
            ToolTip.SetIsOpen(Editor, false);
        }

        view.PointerExited += (_, _) => HideCard();
        // Typing closes the card and keeps it away until the pointer moves again.
        Editor.TextArea.TextEntering += (_, _) => HideCard();
        Editor.TextArea.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape && _peek is { IsOpen: true } peek)
            {
                peek.IsOpen = false;
                e.Handled = true;
            }

            // Pressing Ctrl or Shift alone (to click a link, to select) is no typing.
            if (e.Key is not (Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin)) HideCard();
        };
        Editor.ContextMenu = BuildMenu(workspace);
        BuildNavigation();
    }

    private void RedrawOverlays() => Editor.TextArea.TextView.InvalidateLayer(KnownLayer.Text);

    private void OnTextEntering(object? sender, TextInputEventArgs e)
    {
        if (e.Text is not { Length: 1 } typed || Editor.IsReadOnly || Editor.SelectionLength > 0) return;
        var offset = Editor.CaretOffset;
        if (!GmlLineEdits.StepsOver(Editor.Text, offset, typed[0])) return;
        Editor.CaretOffset = offset + 1;
        e.Handled = true;
    }

    private void OnTextEntered(object? sender, TextInputEventArgs e)
    {
        if (e.Text is not { Length: 1 } typed || Editor.IsReadOnly) return;
        var offset = Editor.CaretOffset;
        if (typed[0] is '(' or ',') UpdateSignatureHint();
        // The typed character is already in the text; judge by the text without it.
        var before = Editor.Text.Remove(offset - 1, 1);
        if (GmlLineEdits.ClosingFor(before, offset - 1, typed[0]) is not { } closing) return;
        Editor.Document.Insert(offset, closing);
        Editor.CaretOffset = offset;
    }

    private int OffsetAt(Point point) => Editor.GetPositionFromPoint(point) is { } position ? Editor.Document.GetOffset(position.Location) : Editor.CaretOffset;

    /// <summary>The resource commands (Rename, Duplicate, Copy name, ...) the window adds at the end of the menu; made again each time the menu opens.</summary>
    public Func<IReadOnlyList<Control>>? ResourceMenu { get; set; }

    private ContextMenu BuildMenu(Workspace workspace)
    {
        var menu = new ContextMenu();
        MenuItem Item(string key, Action run, bool enabled = true)
        {
            var item = new MenuItem { Header = Loc.Current[key], IsEnabled = enabled };
            item.Click += (_, _) => run();
            return item;
        }

        var definitions = GmlNames.Definitions(workspace);
        menu.Opening += (_, _) =>
        {
            var offset = OffsetAt(_hoverPoint);
            var word = GmlNames.WordAt(Editor.Text, offset);
            var target = word is null ? null : definitions(word);
            var local = word is not null && DeclarationOffset(word) is not null; // the same test Go to Definition and Peek use, so a function block of this file enables them
            if (word is not null) Editor.CaretOffset = offset;
            menu.Items.Clear();
            menu.Items.Add(Item("code.menu.goToDefinition", () => Run("code.goToDefinition"), target is not null || local));
            menu.Items.Add(Item("code.menu.peekDefinition", () => Run("code.peekDefinition"), target is not null || local));
            menu.Items.Add(Item("code.menu.findUsages", () => FindUsagesRequested?.Invoke(word ?? ""), word is not null));
            menu.Items.Add(Item("code.menu.rename", RenameSymbol, word is not null));
            menu.Items.Add(Item("code.menu.quickFix", () => Run("code.quickFix")));
            menu.Items.Add(Item("code.menu.help", () => Run("code.helpOnWord"), word is not null && GmlNames.Reference.Find(word) is not null));
            menu.Items.Add(new Separator());
            menu.Items.Add(Item("code.menu.toggleComment", () => Run("code.toggleComment"), !Editor.IsReadOnly));
            menu.Items.Add(Item("code.menu.tabIn", () => Run("code.indent"), !Editor.IsReadOnly));
            menu.Items.Add(Item("code.menu.tabOut", () => Run("code.outdent"), !Editor.IsReadOnly));
            menu.Items.Add(new Separator());
            menu.Items.Add(Item("code.menu.cut", Editor.Cut, !Editor.IsReadOnly));
            menu.Items.Add(Item("code.menu.copy", Editor.Copy));
            menu.Items.Add(Item("code.menu.paste", Editor.Paste, !Editor.IsReadOnly));
            menu.Items.Add(Item("code.menu.selectAll", Editor.SelectAll));
            menu.Items.Add(Item("code.menu.undo", () => Editor.Undo(), Editor.CanUndo && !Editor.IsReadOnly));
            menu.Items.Add(Item("code.menu.redo", () => Editor.Redo(), Editor.CanRedo && !Editor.IsReadOnly));
            if (ResourceMenu?.Invoke() is { Count: > 0 } resource)
            {
                menu.Items.Add(new Separator());
                foreach (var entry in resource) menu.Items.Add(entry);
            }
        };
        return menu;
    }

    private void ShowHover()
    {
        _hover.Stop();
        if (Editor.GetPositionFromPoint(_hoverPoint) is not { } position || ((IOverlayHost)this).FreshDoc is null) return;
        ShowHoverAt(Editor.Document.GetOffset(position.Location));
    }

    /// <summary>Shows the hover for an offset: the text, and for the name of a resource the shared hover card (kind, name or nickname, ID, picture) with the text under it.</summary>
    public void ShowHoverAt(int offset)
    {
        if (((IOverlayHost)this).FreshDoc is not { } doc) return;
        var tip = HoverTextAt(offset);
        if (tip is null) return;
        var token = doc.TokenAt(offset);
        // A nickname in the code stands for its resource; the card then says the resource, the text says what the code does with it.
        var word = token < 0 || doc.Tokens[token].Kind != GmlTokenKind.Identifier ? null : GmlNames.Real(_workspace, doc.Slice(token));
        var key = word is null ? null : GmlNames.CardKey(_workspace, word);
        // The card of an asset already has its name, type and ID, so the text under it keeps only how often this code uses it.
        var uses = word is not null && key is not null && ProblemAt(offset) is null && GmlNames.For(_workspace)(word) == "Asset" ? GmlSymbols.References(doc, word).Count : -1;
        HoverCards.Attach(Editor, key, tip, uses < 0 ? tip : uses > 0 ? Loc.Current.Format("code.hover.uses", ("count", uses)) : "");
        ToolTip.SetIsOpen(Editor, true);
    }

    /// <summary>What the hover shows at an offset: the compiler's message when a problem is there, else the text for the token. Null when there is nothing to say.</summary>
    public string? HoverTextAt(int offset)
    {
        if (ProblemAt(offset) is { } problem) return $"{problem.Code}  {_workspace.Nicknames.Decorate(problem.Message)}";
        var doc = Doc;
        var token = doc.TokenAt(offset);
        return token < 0 ? null : HoverText(_workspace, doc, token);
    }

    private Problem? ProblemAt(int offset)
    {
        var line = Editor.Document.GetLineByOffset(offset);
        var column = offset - line.Offset + 1;
        // A problem with a length covers exactly its text; one without covers a few columns around its start.
        return ((IOverlayHost)this).ShownProblems.FirstOrDefault(p => p.Line == line.LineNumber && (p.Length > 0 ? column >= p.Column && column < p.Column + p.Length : Math.Abs(p.Column - column) <= 8));
    }

    /// <summary>The hover text of a token: a problem is handled by the caller. A colour, a macro or enum, a function signature, or what kind of name it is. Null when there is nothing to say.</summary>
    private string? HoverText(Workspace workspace, GmlDoc doc, int token)
    {
        var word = doc.Slice(token);
        var kind = doc.Tokens[token].Kind;
        if (GmlColors.Parse(word, kind) is { } color) return GmlColors.Describe(color);
        if (kind is not (GmlTokenKind.Identifier or GmlTokenKind.Keyword)) return null;
        if (kind == GmlTokenKind.Identifier && Outline(doc).FirstOrDefault(o => o.Name == word && o.Kind is "macro" or "enum") is { } item)
            return Preview(doc.Text, item.Offset, item.End);
        // A macro or enum of another code entry: its declaring line and where it is.
        if (kind == GmlTokenKind.Identifier && !(token > 0 && doc.IsPunct(token - 1, '.')) && GmlProjectSymbols.Find(workspace, word, Target) is { Kind: "macro" or "enum" } project)
            return project.Preview + "\n" + Loc.Current.Format("code.hover.where", ("where", project.Entry));
        return CachedCard(workspace, doc, token);
    }


    // At most 160 characters of the source from start, cut at the end of the first line for a macro.
    private static string Preview(string text, int start, int end)
    {
        var slice = text[start..Math.Min(end, start + 160)];
        var line = slice.IndexOf('\n', StringComparison.Ordinal);
        return line < 0 ? slice : slice[..line].TrimEnd() + " ...";
    }
}
