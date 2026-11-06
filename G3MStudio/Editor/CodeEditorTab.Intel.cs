using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

namespace G3MStudio.Editor;

// Keyboard line commands, bracket highlight, hover and the context menu. All of them read one GmlDoc per text version.
public sealed partial class CodeEditorTab
{
    private readonly DispatcherTimer _hover = new() { Interval = TimeSpan.FromMilliseconds(350) };
    private Point _hoverPoint;

    /// <summary>The tokens of the text in the editor, built once per text version.</summary>
    public GmlDoc Doc => field is { } known && string.Equals(known.Text, Editor.Text, StringComparison.Ordinal) ? known : field = GmlDoc.Parse(Editor.Text);

    /// <summary>Find usages of a name was asked for (context menu or Shift+F12).</summary>
    public event Action<string>? FindUsagesRequested;

    private void InstallIntel(Workspace workspace)
    {
        Editor.TextArea.TextView.BackgroundRenderers.Add(new BracketRenderer(this));
        Editor.TextArea.Caret.PositionChanged += (_, _) => Editor.TextArea.TextView.InvalidateLayer(KnownLayer.Selection);
        Editor.TextArea.KeyDown += OnLineKey;
        Editor.TextArea.TextEntering += OnTextEntering;
        Editor.TextArea.TextEntered += OnTextEntered;
        _hover.Tick += (_, _) => ShowHover(workspace);
        Editor.TextArea.TextView.PointerMoved += (_, e) =>
        {
            _hover.Stop();
            ToolTip.SetIsOpen(Editor, false);
            _hoverPoint = e.GetPosition(Editor);
            _hover.Start();
        };
        Editor.TextArea.TextView.PointerExited += (_, _) => _hover.Stop();
        Editor.ContextMenu = BuildMenu(workspace);
    }

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

    private void OnLineKey(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.F12 && e.KeyModifiers == KeyModifiers.Shift)
        {
            if (GmlNames.WordAt(Editor.Text, Editor.CaretOffset) is { } word) FindUsagesRequested?.Invoke(word);
            e.Handled = true;
            return;
        }

        if (Editor.IsReadOnly) return;
        var (start, end) = Editor.SelectionLength == 0 ? (Editor.CaretOffset, Editor.CaretOffset) : (Editor.SelectionStart, Editor.SelectionStart + Editor.SelectionLength);
        var text = Editor.Text;
        LineEdit? edit = (e.Key, e.KeyModifiers) switch
        {
            (Key.Oem2, KeyModifiers.Control) => GmlLineEdits.ToggleLineComment(text, start, end),
            (Key.A, KeyModifiers.Shift | KeyModifiers.Alt) => GmlLineEdits.ToggleBlockComment(text, start, end),
            (Key.Down, KeyModifiers.Shift | KeyModifiers.Alt) => GmlLineEdits.DuplicateLines(text, start, end),
            (Key.Up, KeyModifiers.Alt) => GmlLineEdits.MoveLines(text, start, end, up: true),
            (Key.Down, KeyModifiers.Alt) => GmlLineEdits.MoveLines(text, start, end, up: false),
            _ => null,
        };
        if (edit is { } found) Replace(found);
        e.Handled |= edit is not null || e.Key is Key.Up or Key.Down && e.KeyModifiers == KeyModifiers.Alt;
    }

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
        // The typed character is already in the text; judge by the text without it.
        var before = Editor.Text.Remove(offset - 1, 1);
        if (GmlLineEdits.ClosingFor(before, offset - 1, typed[0]) is not { } closing) return;
        Editor.Document.Insert(offset, closing);
        Editor.CaretOffset = offset;
    }

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
            var word = GmlNames.WordAt(Editor.Text, Editor.CaretOffset);
            var target = word is null ? null : definitions(word);
            menu.Items.Clear();
            menu.Items.Add(Item("code.menu.goToDefinition", () => GoToRequested?.Invoke(target ?? ""), target is not null));
            menu.Items.Add(Item("code.menu.findUsages", () => FindUsagesRequested?.Invoke(word ?? ""), word is not null));
            menu.Items.Add(Item("code.menu.copyName", () => _ = TopLevel.GetTopLevel(this)?.Clipboard?.SetTextAsync(word), word is not null));
            menu.Items.Add(new Separator());
            menu.Items.Add(Item("code.menu.toggleComment", () => Replace(GmlLineEdits.ToggleLineComment(Editor.Text, Editor.SelectionStart, Editor.SelectionStart + Editor.SelectionLength)), !Editor.IsReadOnly));
            menu.Items.Add(new Separator());
            menu.Items.Add(Item("code.menu.cut", Editor.Cut, !Editor.IsReadOnly));
            menu.Items.Add(Item("code.menu.copy", Editor.Copy));
            menu.Items.Add(Item("code.menu.paste", Editor.Paste, !Editor.IsReadOnly));
        };
        return menu;
    }

    private void ShowHover(Workspace workspace)
    {
        _hover.Stop();
        if (Editor.GetPositionFromPoint(_hoverPoint) is not { } position) return;
        var doc = Doc;
        var token = doc.TokenAt(Editor.Document.GetOffset(position.Location));
        if (token < 0 || HoverText(workspace, doc, token) is not { } tip) return;
        ToolTip.SetTip(Editor, tip);
        ToolTip.SetIsOpen(Editor, true);
    }

    /// <summary>The hover text of a token: a colour, a function signature or what kind of name it is. Null when there is nothing to say.</summary>
    private static string? HoverText(Workspace workspace, GmlDoc doc, int token)
    {
        var word = doc.Slice(token);
        if (GmlColors.Parse(word, doc.Tokens[token].Kind) is { } color) return GmlColors.Describe(color);
        if (doc.Tokens[token].Kind != G3MLib.Analyzer.Compiler.GmlTokenKind.Identifier) return null;
        var kind = GmlNames.For(workspace)(word);
        if (kind == "Function") return (GmlNames.SignatureOf(workspace, word)?.Label ?? word) + "  (" + kind + ")";
        return kind is null ? null : kind + " " + word;
    }

    private sealed class BracketRenderer(CodeEditorTab tab) : IBackgroundRenderer
    {
        public KnownLayer Layer => KnownLayer.Selection;

        public void Draw(TextView textView, DrawingContext drawingContext)
        {
            if (tab.Editor.Document is null || tab.Editor.Document.TextLength > 2_000_000) return;
            if (GmlStructure.MatchingBrackets(tab.Doc, tab.Editor.CaretOffset) is not { } pair) return;
            var color = (Application.Current?.FindResource("Selection.Bg") as ISolidColorBrush)?.Color ?? Colors.Gray;
            foreach (var offset in new[] { pair.First, pair.Second })
            {
                var builder = new BackgroundGeometryBuilder { AlignToWholePixels = true };
                builder.AddSegment(textView, new TextSegment { StartOffset = offset, Length = 1 });
                if (builder.CreateGeometry() is { } geometry) drawingContext.DrawGeometry(new SolidColorBrush(color, 0.45), null, geometry);
            }
        }
    }
}
