using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaEdit.Rendering;
using G3MLib.Analyzer.Compiler;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

namespace G3MStudio.Editor;

// Overlays (brackets, same name, indent guides, squiggles, colour swatches, bookmarks), hover and the context menu.
// Everything reads one GmlDoc per text version; between a key press and the next tokenize the overlays skip drawing.
public sealed partial class CodeEditorTab : IOverlayHost
{
    private readonly DispatcherTimer _hover = new() { Interval = TimeSpan.FromMilliseconds(350) };
    private readonly BookmarkMargin _bookmarks = new();
    private GmlDoc? _doc;
    private int _textVersion, _docVersion = -1, _problemsVersion = -1, _hoverRun;
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

    IReadOnlyList<Problem> IOverlayHost.ShownProblems => _problemsVersion == _textVersion ? [.. _workspace.Problems.All.Where(p => p.Source == Entry)] : [];

    /// <summary>The pictures for hover: given the Explorer key of a resource, a picture or null.</summary>
    public Func<string, Task<IImage?>>? ThumbnailLoader { get; set; }

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
        AttachedToVisualTree += (_, _) =>
        {
            EditorOptions.Changed += Guides;
            Loc.Current.PropertyChanged += Relabel;
        };
        DetachedFromVisualTree += (_, _) =>
        {
            EditorOptions.Changed -= Guides;
            Loc.Current.PropertyChanged -= Relabel;
            _hover.Stop();
        };
        Editor.TextArea.TextEntering += OnTextEntering;
        Editor.TextArea.TextEntered += OnTextEntered;
        _hover.Tick += (_, _) => _ = ShowHoverAsync();
        view.PointerMoved += (_, e) =>
        {
            _hover.Stop();
            _hoverRun++;
            ToolTip.SetIsOpen(Editor, false);
            _hoverPoint = e.GetPosition(Editor);
            _hover.Start();
        };
        view.PointerExited += (_, _) => _hover.Stop();
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
            var local = word is not null && GmlSymbols.DeclarationOf(Doc, word) >= 0;
            if (word is not null) Editor.CaretOffset = offset;
            menu.Items.Clear();
            menu.Items.Add(Item("code.menu.goToDefinition", () => Run("code.goToDefinition"), target is not null || local));
            menu.Items.Add(Item("code.menu.peekDefinition", () => Run("code.peekDefinition"), target is not null || local));
            menu.Items.Add(Item("code.menu.findUsages", () => FindUsagesRequested?.Invoke(word ?? ""), word is not null));
            menu.Items.Add(Item("code.menu.rename", RenameSymbol, word is not null));
            menu.Items.Add(Item("code.menu.quickFix", () => Run("code.quickFix")));
            menu.Items.Add(Item("code.menu.copyName", () => _ = TopLevel.GetTopLevel(this)?.Clipboard?.SetTextAsync(word), word is not null));
            menu.Items.Add(new Separator());
            menu.Items.Add(Item("code.menu.toggleComment", () => Run("code.toggleComment"), !Editor.IsReadOnly));
            menu.Items.Add(new Separator());
            menu.Items.Add(Item("code.menu.cut", Editor.Cut, !Editor.IsReadOnly));
            menu.Items.Add(Item("code.menu.copy", Editor.Copy));
            menu.Items.Add(Item("code.menu.paste", Editor.Paste, !Editor.IsReadOnly));
        };
        return menu;
    }

    private async Task ShowHoverAsync()
    {
        _hover.Stop();
        var run = _hoverRun;
        if (Editor.GetPositionFromPoint(_hoverPoint) is not { } position || ((IOverlayHost)this).FreshDoc is null) return;
        await ShowHoverAtAsync(Editor.Document.GetOffset(position.Location), run);
    }

    /// <summary>Shows the hover for an offset: the text at once, then the picture of a resource (a sprite) when the loader has one. A newer hover (run) cancels an older picture.</summary>
    public async Task ShowHoverAtAsync(int offset, int run)
    {
        if (((IOverlayHost)this).FreshDoc is not { } doc) return;
        var workspace = _workspace;
        var tip = HoverTextAt(offset);
        if (tip is null) return;
        ToolTip.SetTip(Editor, tip);
        ToolTip.SetIsOpen(Editor, true);
        var token = doc.TokenAt(offset);
        if (token < 0 || ThumbnailLoader is null || GmlNames.Definitions(workspace)(doc.Slice(token)) is not { } key) return;
        var picture = await ThumbnailLoader(key);
        if (picture is null || run != _hoverRun) return;
        var content = new StackPanel { Spacing = 4, Children = { new Image { Source = picture, MaxWidth = 96, MaxHeight = 96, Stretch = Stretch.Uniform }, new TextBlock { Text = tip } } };
        ToolTip.SetTip(Editor, content);
    }

    /// <summary>What the hover shows at an offset: the compiler's message when a problem is there, else the text for the token. Null when there is nothing to say.</summary>
    public string? HoverTextAt(int offset)
    {
        var line = Editor.Document.GetLineByOffset(offset);
        var column = offset - line.Offset + 1;
        var problem = ((IOverlayHost)this).ShownProblems.FirstOrDefault(p => p.Line == line.LineNumber && Math.Abs(p.Column - column) <= 8);
        if (problem is not null) return $"{problem.Code}  {problem.Message}";
        var doc = Doc;
        var token = doc.TokenAt(offset);
        return token < 0 ? null : HoverText(_workspace, doc, token);
    }

    /// <summary>The hover text of a token: a problem is handled by the caller. A colour, a macro or enum, a function signature, or what kind of name it is. Null when there is nothing to say.</summary>
    private string? HoverText(Workspace workspace, GmlDoc doc, int token)
    {
        var word = doc.Slice(token);
        var kind = doc.Tokens[token].Kind;
        if (GmlColors.Parse(word, kind) is { } color) return GmlColors.Describe(color);
        if (kind != GmlTokenKind.Identifier) return null;
        if (Outline(doc).FirstOrDefault(o => o.Name == word && o.Kind is "macro" or "enum") is { } item)
            return Preview(doc.Text, item.Offset, item.End);
        var signature = GmlSignatures.FromDoc(doc, word) ?? GmlNames.SignatureOf(workspace, word);
        if (signature is not null) return signature.Label;
        if (GmlSymbols.DeclarationOf(doc, word) is var declaration and >= 0) return Loc.Current["code.hover.local"] + " " + word + "  (" + Preview(doc.Text, LineStartOf(doc.Text, doc.Start(declaration)), doc.End(declaration)).Trim() + ")";
        var named = GmlNames.For(workspace)(word);
        return named is null ? null : named + " " + word;
    }

    private static int LineStartOf(string text, int offset) => offset <= 0 ? 0 : text.LastIndexOf('\n', offset - 1) + 1;

    // At most 160 characters of the source from start, cut at the end of the first line for a macro.
    private static string Preview(string text, int start, int end)
    {
        var slice = text[start..Math.Min(end, start + 160)];
        var line = slice.IndexOf('\n', StringComparison.Ordinal);
        return line < 0 ? slice : slice[..line].TrimEnd() + " ...";
    }
}
