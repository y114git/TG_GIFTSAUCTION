using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using AvaloniaEdit.Rendering;

namespace G3MStudio.Editor;

/// <summary>
/// Turns font ligatures off while <see cref="EditorOptions.Ligatures"/> is false. AvaloniaEdit gives its text runs no font features, so each run of two or
/// more symbol characters (-> == != and the like, where code fonts put their ligatures) becomes an element whose run carries the features that switch ligatures off.
/// </summary>
internal sealed class NoLigatureGenerator : VisualLineElementGenerator
{
    private static readonly System.Buffers.SearchValues<char> Symbols = System.Buffers.SearchValues.Create("<>=!-+*/|&:.~#%^?$@\\");

    private static readonly FontFeatureCollection Off = [FontFeature.Parse("liga=0"), FontFeature.Parse("clig=0"), FontFeature.Parse("calt=0")];

    /// <summary>The first run of at least two symbol characters in text[from..to], as start and length, or null.</summary>
    public static (int Start, int Length)? RunIn(string text, int from, int to)
    {
        for (var i = from; i < to; i++)
        {
            if (!Symbols.Contains(text[i])) continue;
            var end = i + 1;
            while (end < to && Symbols.Contains(text[end])) end++;
            if (end - i >= 2) return (i, end - i);
            i = end;
        }

        return null;
    }

    public override int GetFirstInterestedOffset(int startOffset)
    {
        if (EditorOptions.Ligatures) return -1;
        var line = CurrentContext.VisualLine.LastDocumentLine;
        if (startOffset > line.EndOffset) return -1;
        var text = CurrentContext.Document.GetText(startOffset, line.EndOffset - startOffset);
        return RunIn(text, 0, text.Length) is var (start, _) ? startOffset + start : -1;
    }

    public override VisualLineElement? ConstructElement(int offset)
    {
        var line = CurrentContext.VisualLine.LastDocumentLine;
        var text = CurrentContext.Document.GetText(offset, line.EndOffset - offset);
        return RunIn(text, 0, text.Length) is var (start, length) && start == 0 ? new NoLigatureText(CurrentContext.VisualLine, length) : null;
    }

    private sealed class NoLigatureText(VisualLine line, int length) : VisualLineText(line, length)
    {
        public override TextRun CreateTextRun(int startVisualColumn, ITextRunConstructionContext context)
        {
            var run = base.CreateTextRun(startVisualColumn, context);
            return run is TextCharacters characters ? new TextCharacters(characters.Text, new NoLigatureProperties(characters.Properties)) : run;
        }

        protected override VisualLineText CreateInstance(int length) => new NoLigatureText(ParentVisualLine, length);
    }

    private sealed class NoLigatureProperties(TextRunProperties inner) : TextRunProperties
    {
        public override Typeface Typeface => inner.Typeface;

        public override double FontRenderingEmSize => inner.FontRenderingEmSize;

        public override TextDecorationCollection? TextDecorations => inner.TextDecorations;

        public override IBrush? ForegroundBrush => inner.ForegroundBrush;

        public override IBrush? BackgroundBrush => inner.BackgroundBrush;

        public override System.Globalization.CultureInfo? CultureInfo => inner.CultureInfo;

        public override BaselineAlignment BaselineAlignment => inner.BaselineAlignment;

        public override FontFeatureCollection? FontFeatures => Off;
    }
}
