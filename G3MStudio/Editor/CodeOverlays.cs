using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Rendering;
using G3MLib.Analyzer.Compiler;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Editor;

/// <summary>What the overlays need from the tab. They draw only from a token document of the current text, so typing never waits for them.</summary>
internal interface IOverlayHost
{
    AvaloniaEdit.TextEditor Editor { get; }

    /// <summary>The token document when it matches the text in the editor, else null (between a key press and the next tokenize).</summary>
    GmlDoc? FreshDoc { get; }

    /// <summary>The problems of the entry that still match the text, as one based line and column.</summary>
    IReadOnlyList<Problem> ShownProblems { get; }
}

internal static class OverlayBrush
{
    public static Color Of(string key, Color fallback) => (Application.Current?.FindResource(key) as ISolidColorBrush)?.Color ?? fallback;

    /// <summary>The document range that the lines on screen cover, or null when the view is not laid out.</summary>
    public static (int Start, int End)? Visible(TextView view) =>
        view.VisualLinesValid && view.VisualLines.Count > 0 ? (view.VisualLines[0].FirstDocumentLine.Offset, view.VisualLines[^1].LastDocumentLine.EndOffset) : null;
}

/// <summary>Highlights the bracket that pairs with the one at the caret, and every other use of the name at the caret.</summary>
internal sealed class HighlightRenderer(IOverlayHost host) : IBackgroundRenderer
{
    private (int Caret, GmlDoc? Doc, List<(int, int)> Spans) _word = (-1, null, []);

    public KnownLayer Layer => KnownLayer.Selection;

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (host.FreshDoc is not { } doc || OverlayBrush.Visible(textView) is not var (from, to)) return;
        var caret = host.Editor.CaretOffset;
        var accent = OverlayBrush.Of("Selection.Bg", Colors.Gray);
        if (GmlStructure.MatchingBrackets(doc, caret) is { } pair)
            foreach (var offset in new[] { pair.First, pair.Second }) Fill(textView, drawingContext, offset, 1, new SolidColorBrush(accent, 0.55));
        if (_word.Caret != caret || !ReferenceEquals(_word.Doc, doc)) _word = (caret, doc, WordSpans(doc, caret));
        var soft = new SolidColorBrush(accent, 0.25);
        foreach (var (start, length) in _word.Spans.Where(s => s.Item1 >= from && s.Item1 <= to)) Fill(textView, drawingContext, start, length, soft);
    }

    // Only a name counts (not a keyword or a number), and only when it is used more than once.
    private static List<(int, int)> WordSpans(GmlDoc doc, int caret)
    {
        var token = doc.TokenAt(caret);
        if (token < 0 || doc.Tokens[token].Kind != GmlTokenKind.Identifier) return [];
        var same = doc.Named(doc.Slice(token));
        return same.Count < 2 ? [] : [.. same.Select(t => (doc.Start(t), doc.End(t) - doc.Start(t)))];
    }

    private static void Fill(TextView view, DrawingContext context, int offset, int length, IBrush brush)
    {
        foreach (var rect in BackgroundGeometryBuilder.GetRectsForSegment(view, new TextSegment { StartOffset = offset, Length = length })) context.DrawRectangle(brush, null, rect);
    }
}

/// <summary>Thin vertical lines at each indent level of the lines on screen.</summary>
internal sealed class IndentGuideRenderer(IOverlayHost host) : IBackgroundRenderer
{
    private const int Unit = 4;

    public KnownLayer Layer => KnownLayer.Background;

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (!EditorOptions.IndentGuides || host.Editor.Document is not { } document || !textView.VisualLinesValid) return;
        var pen = new Pen(new SolidColorBrush(OverlayBrush.Of("Text.Muted", Colors.Gray), 0.3), 1);
        foreach (var line in textView.VisualLines)
        {
            var text = document.GetText(line.FirstDocumentLine);
            var (columns, tabs) = Indent(text);
            for (var column = Unit; column <= columns - 1 && column < text.Length; column += Unit)
            {
                var at = tabs ? column / Unit : column;
                if (at >= text.Length) break;
                var rects = BackgroundGeometryBuilder.GetRectsForSegment(textView, new TextSegment { StartOffset = line.FirstDocumentLine.Offset + at, Length = 1 });
                foreach (var rect in rects.Take(1)) drawingContext.DrawLine(pen, new Point(rect.X, rect.Y), new Point(rect.X, rect.Bottom));
            }
        }
    }

    /// <summary>The width of the leading white space in columns (a tab counts as one unit) and whether it is made of tabs.</summary>
    public static (int Columns, bool Tabs) Indent(string line)
    {
        var tabs = false;
        var columns = 0;
        foreach (var c in line)
        {
            if (c == '\t') { columns += Unit; tabs = true; }
            else if (c == ' ') columns++;
            else break;
        }

        return (columns, tabs);
    }
}

// Draws a zigzag under the places the compiler reported.
internal sealed class SquiggleRenderer(IOverlayHost host) : IBackgroundRenderer
{
    public KnownLayer Layer => KnownLayer.Text;

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (host.Editor.Document is not { } document || OverlayBrush.Visible(textView) is not var (from, to)) return;
        foreach (var problem in host.ShownProblems)
        {
            if (problem.Line < 1 || problem.Line > document.LineCount) continue;
            var line = document.GetLineByNumber(problem.Line);
            var start = Math.Min(line.Offset + Math.Max(problem.Column - 1, 0), line.EndOffset);
            if (start < from || start > to) continue;
            var end = start;
            while (end < line.EndOffset && (char.IsLetterOrDigit(document.GetCharAt(end)) || document.GetCharAt(end) == '_')) end++;
            if (end == start) end = Math.Min(start + 1, line.EndOffset);
            var color = OverlayBrush.Of(problem.Severity == "Error" ? "Danger" : "Warning", Colors.Red);
            foreach (var rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, new TextSegment { StartOffset = start, Length = end - start })) Zigzag(drawingContext, rect, color);
        }
    }

    private static void Zigzag(DrawingContext context, Rect rect, Color color)
    {
        var y = rect.Bottom - 2;
        var points = new List<Point>();
        for (var x = rect.Left; x <= rect.Right + 2; x += 2) points.Add(new Point(x, points.Count % 2 == 0 ? y : y + 2));
        for (var i = 1; i < points.Count; i++) context.DrawLine(new Pen(new SolidColorBrush(color), 1), points[i - 1], points[i]);
    }
}

// Puts a small square of the colour in front of a colour literal.
internal sealed class ColorSwatchGenerator(IOverlayHost host) : VisualLineElementGenerator
{
    public override int GetFirstInterestedOffset(int startOffset)
    {
        if (host.FreshDoc is not { } doc) return -1;
        var index = doc.NextColor(startOffset);
        if (index < 0) return -1;
        var offset = doc.Colors[index].Offset;
        return offset <= CurrentContext.VisualLine.LastDocumentLine.EndOffset ? offset : -1;
    }

    public override VisualLineElement? ConstructElement(int offset)
    {
        if (host.FreshDoc is not { } doc) return null;
        var index = doc.NextColor(offset);
        if (index < 0 || doc.Colors[index].Offset != offset) return null;
        var (r, g, b) = doc.Colors[index].Color;
        var swatch = new Border { Width = 10, Height = 10, Margin = new Thickness(0, 0, 3, 0), Background = new SolidColorBrush(Color.FromRgb(r, g, b)), BorderThickness = new Thickness(0), VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
        return new InlineObjectElement(0, swatch);
    }
}

/// <summary>The bookmarks of one code tab: a dot in the gutter. A click on the gutter toggles one; the lines follow edits.</summary>
public sealed class BookmarkMargin : AbstractMargin
{
    private const double MarkWidth = 14;
    private readonly List<TextAnchor> _marks = [];

    /// <summary>One based lines that carry a bookmark, in order.</summary>
    public IReadOnlyList<int> Lines => [.. _marks.Where(m => !m.IsDeleted).Select(m => m.Line).Distinct().Order()];

    protected override Size MeasureOverride(Size availableSize) => new(MarkWidth, 0);

    protected override void OnTextViewChanged(TextView? oldTextView, TextView? newTextView)
    {
        oldTextView?.VisualLinesChanged -= OnLinesChanged;
        base.OnTextViewChanged(oldTextView, newTextView);
        newTextView?.VisualLinesChanged += OnLinesChanged;
    }

    private void OnLinesChanged(object? sender, EventArgs e) => InvalidateVisual();

    public override void Render(DrawingContext context)
    {
        if (TextView is not { } view || !view.VisualLinesValid) return;
        var brush = new SolidColorBrush(OverlayBrush.Of("Text.Accent", Colors.Green));
        var lines = Lines.ToHashSet();
        foreach (var line in view.VisualLines)
        {
            if (!lines.Contains(line.FirstDocumentLine.LineNumber)) continue;
            var y = line.GetTextLineVisualYPosition(line.TextLines[0], VisualYPosition.LineMiddle) - view.VerticalOffset;
            context.DrawEllipse(brush, null, new Point(MarkWidth / 2, y), 4, 4);
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (TextView is not { } view || view.Document is not { } document) return;
        var visual = view.GetDocumentLineByVisualTop(e.GetPosition(view).Y + view.VerticalOffset);
        if (visual is null) return;
        AddOrRemove(document, visual.LineNumber);
        e.Handled = true;
    }

    /// <summary>Puts a bookmark on the line, or takes it away when there is one.</summary>
    public void AddOrRemove(TextDocument document, int line)
    {
        if (_marks.RemoveAll(m => m.IsDeleted || m.Line == line) == 0) _marks.Add(document.CreateAnchor(document.GetLineByNumber(line).Offset));
        InvalidateVisual();
    }

    /// <summary>The next bookmark line after <paramref name="line"/>, wrapping round; null when there are none.</summary>
    public int? Next(int line, bool forward)
    {
        var all = Lines;
        if (all.Count == 0) return null;
        return forward ? all.FirstOrDefault(l => l > line, all[0]) : all.LastOrDefault(l => l < line, all[^1]);
    }
}
