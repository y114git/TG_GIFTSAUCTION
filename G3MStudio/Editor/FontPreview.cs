using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using G3MLib.DataFile.Models;
using G3MStudio.Theme;

namespace G3MStudio.Editor;

/// <summary>One drawn character: its rectangle on the sheet and where it lands, in sheet pixels.</summary>
public readonly record struct Placed(Rect Source, Rect Target);

public enum FontBackdrop { Chequer, Dark, Light, Green }

/// <summary>A text drawn from the glyph picture of a font, the way the game draws it: each character is its rectangle of the sheet, placed by its offset and advanced by its shift.</summary>
public sealed class FontPreview : Control
{
    private readonly GameMakerFont _font;
    private readonly Func<IImage?> _sheet;
    /// <summary>A line wraps before it is wider than this many screen pixels, so a text of thousands of glyphs stays a block, not a strip millions of pixels wide.</summary>
    private const double MaxLineWidth = 4096;

    private Size _size;
    private ScrollViewer? _host;

    public FontPreview(GameMakerFont font, Func<IImage?> sheet)
    {
        (_font, _sheet) = (font, sheet);
        Name = "GlyphPreview";
        RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.None);
        ClipToBounds = true;
    }

    public string Text { get; set { field = value; Relayout(); } } = "";

    /// <summary>Whole-number zoom of 1 to 8.</summary>
    public int Scale { get; set { field = Math.Clamp(value, 1, 8); Relayout(); InvalidateMeasure(); InvalidateVisual(); } } = 2;

    public FontBackdrop Background { get; set { field = value; InvalidateVisual(); } }

    /// <summary>Outlines every glyph cell and the line height, to see offsets and shifts.</summary>
    public bool Grid { get; set { field = value; InvalidateVisual(); } }

    /// <summary>The characters that have a glyph and were placed, for the tests.</summary>
    public IReadOnlyList<Placed> Glyphs { get; private set; } = [];

    /// <summary>Draws the text again after the text, the font or its sheet changed.</summary>
    public void Refresh()
    {
        Relayout();
        InvalidateMeasure();
        InvalidateVisual();
    }

    private void Relayout() => Glyphs = Layout(_font, Text, out _size, MaxLineWidth / Scale);

    /// <summary>
    /// The glyph rectangles of a text: a new line on \n, characters without a glyph skipped. A line wraps before a glyph that would end past <paramref name="wrap"/>;
    /// glyphs with a negative offset move the whole text right, so none lies left of the start.
    /// </summary>
    public static IReadOnlyList<Placed> Layout(GameMakerFont font, string text, out Size size, double wrap = double.PositiveInfinity)
    {
        var byCharacter = new Dictionary<int, GameMakerFont.Glyph>();
        foreach (var g in font.Glyphs) byCharacter.TryAdd(g.Character, g);
        var line = Math.Max(1, font.Glyphs.Select(g => (int)g.SourceHeight).DefaultIfEmpty(1).Max());
        var placed = new List<Placed>();
        double x = 0, y = 0, width = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            if (rune.Value == '\n')
            {
                (x, y) = (0, y + line);
                continue;
            }

            if (!byCharacter.TryGetValue(rune.Value, out var g)) continue;
            if (x > 0 && x + g.Offset + g.SourceWidth > wrap) (x, y) = (0, y + line);
            placed.Add(new Placed(new Rect(g.SourceX, g.SourceY, g.SourceWidth, g.SourceHeight), new Rect(x + g.Offset, y, g.SourceWidth, g.SourceHeight)));
            x += g.Shift;
            width = Math.Max(width, Math.Max(x, x + g.Offset + g.SourceWidth));
        }

        var left = Math.Min(0, placed.Count == 0 ? 0 : placed.Min(p => p.Target.X));
        if (left < 0)
        {
            placed = [.. placed.Select(p => p with { Target = p.Target.Translate(new Vector(-left, 0)) })];
            width -= left;
        }

        size = new Size(Math.Max(width, 1), y + line);
        return placed;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _host = this.FindAncestorOfType<ScrollViewer>();
        _host?.ScrollChanged += Scrolled;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _host?.ScrollChanged -= Scrolled;
        _host = null;
        base.OnDetachedFromVisualTree(e);
    }

    // Only the glyphs in view are drawn, so a scroll draws again.
    private void Scrolled(object? sender, ScrollChangedEventArgs e) => InvalidateVisual();

    protected override Size MeasureOverride(Size availableSize) => new(_size.Width * Scale + 8, _size.Height * Scale + 8);

    public override void Render(DrawingContext context)
    {
        var area = new Rect(Bounds.Size);
        switch (Background)
        {
            case FontBackdrop.Chequer: Chequer.Draw(context, area, 8); break;
            case FontBackdrop.Dark: context.FillRectangle(Brushes.Black, area); break;
            case FontBackdrop.Light: context.FillRectangle(Brushes.White, area); break;
            default: context.FillRectangle(new SolidColorBrush(Color.FromRgb(0, 120, 60)), area); break;
        }

        var origin = new Vector(4, 4);
        var sheet = _sheet();
        var view = _host is { } host ? new Rect(host.Offset.X, host.Offset.Y, host.Viewport.Width, host.Viewport.Height) : area;
        var guide = Grid ? new Pen(CanvasTheme.Guide, 1) : null;
        foreach (var p in Glyphs)
        {
            var target = new Rect(p.Target.X * Scale + origin.X, p.Target.Y * Scale + origin.Y, p.Target.Width * Scale, p.Target.Height * Scale);
            if (!target.Intersects(view)) continue;
            if (sheet is not null) context.DrawImage(sheet, p.Source, target);
            if (guide is not null) context.DrawRectangle(null, guide, target);
        }

        if (Grid) context.DrawRectangle(null, new Pen(CanvasTheme.Line, 1), new Rect(origin.X, origin.Y, _size.Width * Scale, _size.Height * Scale));
    }
}
