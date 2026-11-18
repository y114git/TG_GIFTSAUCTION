using Avalonia;
using Avalonia.Media;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Folding;
using AvaloniaEdit.Rendering;
using G3MStudio.Theme;

namespace G3MStudio.Editor;

/// <summary>
/// The gutter of foldable lines with thin chevrons instead of the library's square boxes and guide lines. The library draws its markers in an internal
/// class, so they are made invisible (they still take the clicks) and the chevrons are drawn here from the theme tokens: muted when open, green when folded.
/// </summary>
public sealed class ChevronFoldingMargin : FoldingMargin
{
    public ChevronFoldingMargin()
    {
        FoldingMarkerBrush = FoldingMarkerBackgroundBrush = SelectedFoldingMarkerBrush = SelectedFoldingMarkerBackgroundBrush = Brushes.Transparent;
    }

    /// <summary>Swaps the margin <see cref="FoldingManager.Install"/> added for this one.</summary>
    public static void Replace(TextArea area, FoldingManager manager)
    {
        var at = area.LeftMargins.ToList().FindIndex(m => m is FoldingMargin);
        if (at < 0) return;
        area.LeftMargins.RemoveAt(at);
        area.LeftMargins.Insert(at, new ChevronFoldingMargin { FoldingManager = manager });
    }

    public override void Render(DrawingContext drawingContext)
    {
        if (TextView is not { VisualLinesValid: true } view || FoldingManager is not { } manager) return;
        var open = new Pen(CanvasTheme.Get("Text.Muted"), 1.5, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        var folded = new Pen(CanvasTheme.Get("Text.Accent"), 1.5, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        foreach (var line in view.VisualLines)
        {
            var section = manager.GetNextFolding(line.FirstDocumentLine.Offset);
            if (section is null || section.StartOffset > line.LastDocumentLine.Offset + line.LastDocumentLine.Length) continue;
            var column = line.GetVisualColumn(section.StartOffset - line.FirstDocumentLine.Offset);
            var y = line.GetTextLineVisualYPosition(line.GetTextLine(column), VisualYPosition.TextMiddle) - view.VerticalOffset;
            var x = Bounds.Width / 2;
            if (section.IsFolded)
            {
                drawingContext.DrawLine(folded, new Point(x - 1.5, y - 3.5), new Point(x + 1.5, y));
                drawingContext.DrawLine(folded, new Point(x + 1.5, y), new Point(x - 1.5, y + 3.5));
            }
            else
            {
                drawingContext.DrawLine(open, new Point(x - 3.5, y - 1.5), new Point(x, y + 1.5));
                drawingContext.DrawLine(open, new Point(x, y + 1.5), new Point(x + 3.5, y - 1.5));
            }
        }
    }

}
