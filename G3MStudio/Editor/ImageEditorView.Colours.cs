using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;
using G3MStudio.Theme;

namespace G3MStudio.Editor;

public sealed partial class ImageEditorView
{
    // A swatch with its name: the colour a closed shape takes for its outline (the primary colour) or its fill (the secondary one).
    private static StackPanel ColourKey(Border swatch, string key)
    {
        var name = Loc.Current[key];
        AutomationProperties.SetName(swatch, name);
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, VerticalAlignment = VerticalAlignment.Center, Children = { swatch, new TextBlock { Text = name, Classes = { "muted" }, VerticalAlignment = VerticalAlignment.Center } } };
        ToolTip.SetTip(panel, name);
        return panel;
    }

    // Only the colours the style uses are shown, and the swatches of the Colours panel say which job they have for the tool in hand.
    private void ShowShapeColours()
    {
        _outlineKey.IsVisible = Canvas.Style != ShapeStyle.Filled;
        _fillKey.IsVisible = Canvas.Style != ShapeStyle.Outline;
        var shape = Tool is ImageTool.Rectangle or ImageTool.Ellipse or ImageTool.Polygon;
        var (first, second) = shape ? ("image.outlineColour", "image.fillColour") : ("image.primaryColour", "image.secondaryColour");
        _primary.Labeled(first);
        _secondary.Labeled(second);
    }

    private StackPanel Brushes()
    {
        var panel = new WrapPanel { Name = "ImageBrushes" };
        foreach (var (shape, size) in new[] { (BrushShape.Square, 1), (BrushShape.Square, 2), (BrushShape.Square, 3), (BrushShape.Square, 5), (BrushShape.Square, 8), (BrushShape.Round, 3), (BrushShape.Round, 5), (BrushShape.Round, 8), (BrushShape.Round, 12), (BrushShape.Round, 16) })
        {
            var button = new Button { Width = 36, Height = 36, Padding = new Thickness(0), Margin = new Thickness(1), Content = BrushPreview(shape, size) };
            var brushName = $"{Loc.Current["image.shape." + shape]} {size}";
            ToolTip.SetTip(button, brushName);
            Avalonia.Automation.AutomationProperties.SetName(button, brushName);
            button.Click += (_, _) =>
            {
                Canvas.Shape = shape;
                SetSize(size);
                Canvas.Focus();
            };
            panel.Children.Add(button);
        }

        return Section("image.brushes", panel, false); // the size box and slider above cover the common case; folded, the colours and recent row stay in view
    }

    private static Border BrushPreview(BrushShape shape, int size)
    {
        var side = Math.Min(24, Math.Max(3, size * 2));
        return new Border { Width = side, Height = side, Background = G3MStudio.Theme.CanvasTheme.Handle, CornerRadius = shape == BrushShape.Round ? new CornerRadius(side / 2.0) : default };
    }

    private StackPanel Colours()
    {
        var swatches = new WrapPanel { Name = "ImagePalette" };
        foreach (var hex in Palette) swatches.Children.Add(Chip(Rgba.Parse(hex) ?? Rgba.Clear));
        _hex.TextChanged += (_, _) =>
        {
            if (Rgba.Parse(_hex.Text?.Trim()) is { } colour && colour != Canvas.Primary) SetColour(colour, false);
        };
        var swap = Small("ImageSwap", Loc.Current["image.swap"], SwapColours);
        var pair = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { _primary, swap, _secondary } };
        return Section("image.colours", new StackPanel { Spacing = 4, Children = { pair, _hex, Label("image.recent"), _recentPanel, swatches } });
    }

    private Border Chip(Rgba colour)
    {
        var chip = new Border { Width = 22, Height = 22, Margin = new Thickness(1), Background = Brush(colour) };
        ToolTip.SetTip(chip, colour.ToString());
        chip.PointerPressed += (_, e) =>
        {
            SetColour(colour, e.GetCurrentPoint(chip).Properties.IsRightButtonPressed);
            e.Handled = true;
        };
        return chip;
    }

    private static SolidColorBrush Brush(Rgba c) => new(Avalonia.Media.Color.FromArgb(c.A, c.R, c.G, c.B));

    private void ShowColours()
    {
        _primary.Background = Brush(Canvas.Primary);
        _secondary.Background = Brush(Canvas.Secondary);
        _outlineSwatch.Background = _primary.Background;
        _fillSwatch.Background = _secondary.Background;
        _primary.BorderBrush = _secondary.BorderBrush = _outlineSwatch.BorderBrush = _fillSwatch.BorderBrush = G3MStudio.Theme.CanvasTheme.Muted;
        if (_hex.Text != Canvas.Primary.ToString()) _hex.Text = Canvas.Primary.ToString();
        _recentPanel.Children.Clear();
        foreach (var colour in _recent) _recentPanel.Children.Add(Chip(colour));
    }

    public void SwapColours()
    {
        (Canvas.Primary, Canvas.Secondary) = (Canvas.Secondary, Canvas.Primary);
        ShowColours();
    }

}
