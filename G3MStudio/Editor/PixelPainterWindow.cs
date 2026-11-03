using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

namespace G3MStudio.Editor;

/// <summary>
/// The sprite painter: draw on one frame with a pencil, eraser, fill, color picker, line and rectangles, with zoom, a grid and undo. Apply hands the
/// picture back as a PNG; the caller makes the one undoable edit of the sprite.
/// </summary>
public sealed class PixelPainterWindow : Window
{
    private static readonly string[] Palette = ["#000000", "#FFFFFF", "#FF0000", "#00FF00", "#0000FF", "#FFFF00", "#FF8000", "#8000FF", "#808080", "#804000"];

    private readonly TextBox _hex = new() { Name = "PaintColor", Width = 110, Text = "#FFFFFF" };
    private readonly Border _swatch = new() { Name = "PaintSwatch", Width = 28, Height = 28, BorderThickness = new Thickness(1) };
    private readonly Button _undoButton = new() { Name = "PaintUndo" }, _redoButton = new() { Name = "PaintRedo" };

    public PixelPainterWindow(string title, PixelBuffer picture, Action<byte[], int, int> apply)
    {
        Title = title;
        Width = 900;
        Height = 700;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Canvas = new PixelCanvas(picture) { Zoom = Math.Clamp(Math.Floor(420.0 / Math.Max(picture.Width, picture.Height)), 1, 32) };
        Closed += (_, _) => Canvas.Dispose();

        var tools = new StackPanel { Spacing = 4, Margin = new Thickness(8) };
        var buttons = new List<ToggleButton>();
        foreach (var tool in Enum.GetValues<PaintTool>())
        {
            var button = new ToggleButton { Name = "Tool_" + tool, Content = Loc.Current["painter.tool." + tool], IsChecked = tool == PaintTool.Pencil, HorizontalAlignment = HorizontalAlignment.Stretch };
            button.Click += (_, _) =>
            {
                Canvas.Tool = tool;
                foreach (var other in buttons) other.IsChecked = other == button;
            };
            buttons.Add(button);
            tools.Children.Add(button);
        }

        var size = new NumericUpDown { Name = "PaintSize", Minimum = 1, Maximum = 32, Value = 1, Increment = 1, FormatString = "0", Width = 110 };
        size.ValueChanged += (_, e) => Canvas.BrushSize = (int)(e.NewValue ?? 1);
        var grid = new CheckBox { Name = "PaintGrid", Content = Loc.Current["painter.grid"], IsChecked = true };
        grid.IsCheckedChanged += (_, _) => Canvas.ShowGrid = grid.IsChecked == true;
        tools.Children.Add(new TextBlock { Text = Loc.Current["painter.size"], Margin = new Thickness(0, 8, 0, 0) });
        tools.Children.Add(size);
        tools.Children.Add(grid);

        var swatches = new WrapPanel { MaxWidth = 150 };
        foreach (var hex in Palette)
        {
            var chip = new Button { Width = 24, Height = 24, Padding = new Thickness(0), Margin = new Thickness(1), Background = new SolidColorBrush(Avalonia.Media.Color.Parse(hex)) };
            chip.Click += (_, _) => SetColor(Rgba.Parse(hex)!.Value);
            swatches.Children.Add(chip);
        }

        tools.Children.Add(new TextBlock { Text = Loc.Current["painter.color"], Margin = new Thickness(0, 8, 0, 0) });
        tools.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { _swatch, _hex } });
        tools.Children.Add(swatches);

        _hex.TextChanged += (_, _) =>
        {
            if (Rgba.Parse(_hex.Text?.Trim()) is { } color) ApplyColor(color);
        };
        Canvas.Picked += SetColor;
        Canvas.Changed += RefreshButtons;
        _undoButton.Content = Loc.Current["painter.undo"];
        _redoButton.Content = Loc.Current["painter.redo"];
        _undoButton.Click += (_, _) => Canvas.Undo();
        _redoButton.Click += (_, _) => Canvas.Redo();
        var zoomOut = new Button { Name = "PaintZoomOut", Content = "−" };
        var zoomIn = new Button { Name = "PaintZoomIn", Content = "+" };
        var zoomText = new TextBlock { VerticalAlignment = VerticalAlignment.Center, MinWidth = 48, TextAlignment = TextAlignment.Center };
        void ShowZoom() => zoomText.Text = $"{Canvas.Zoom:0}x";
        zoomOut.Click += (_, _) =>
        {
            Canvas.Zoom -= Math.Max(1, Canvas.Zoom / 4);
            ShowZoom();
        };
        zoomIn.Click += (_, _) =>
        {
            Canvas.Zoom += Math.Max(1, Canvas.Zoom / 4);
            ShowZoom();
        };
        ShowZoom();

        var ok = new Button { Name = "PaintApply", Content = Loc.Current["painter.apply"], Classes = { "primary" } };
        var cancel = new Button { Name = "PaintCancel", Content = Loc.Current["explorer.cancel"] };
        ok.Click += (_, _) =>
        {
            apply(FramePicture.ToPng(Canvas.Picture), Canvas.Picture.Width, Canvas.Picture.Height);
            Close(true);
        };
        cancel.Click += (_, _) => Close(false);

        var width = new NumericUpDown { Name = "PaintWidth", Minimum = 1, Maximum = 2048, Value = picture.Width, Increment = 1, FormatString = "0", Width = 110 };
        var height = new NumericUpDown { Name = "PaintHeight", Minimum = 1, Maximum = 2048, Value = picture.Height, Increment = 1, FormatString = "0", Width = 110 };
        var resize = new Button { Name = "PaintResize", Content = Loc.Current["painter.resize"] };
        resize.Click += (_, _) =>
        {
            var (w, h) = ((int)(width.Value ?? picture.Width), (int)(height.Value ?? picture.Height));
            if ((w, h) == (Canvas.Picture.Width, Canvas.Picture.Height)) return;
            var next = new PixelBuffer(w, h);
            for (var y = 0; y < Math.Min(h, Canvas.Picture.Height); y++)
                for (var x = 0; x < Math.Min(w, Canvas.Picture.Width); x++) next.Set(x, y, Canvas.Picture.Get(x, y));
            Canvas.Replace(next);
        };
        tools.Children.Add(new TextBlock { Text = Loc.Current["painter.canvasSize"], Margin = new Thickness(0, 8, 0, 0) });
        tools.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { width, height } });
        tools.Children.Add(resize);
        var top = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new Thickness(8), Children = { _undoButton, _redoButton, new Border { Width = 12 }, zoomOut, zoomText, zoomIn } };
        var bottom = new DockPanel { Margin = new Thickness(8), LastChildFill = false };
        DockPanel.SetDock(ok, Avalonia.Controls.Dock.Right);
        DockPanel.SetDock(cancel, Avalonia.Controls.Dock.Right);
        bottom.Children.Add(ok);
        bottom.Children.Add(cancel);
        bottom.Children.Add(new TextBlock { Text = Loc.Current["painter.hint"], Classes = { "muted" }, VerticalAlignment = VerticalAlignment.Center });

        var layout = new DockPanel();
        DockPanel.SetDock(top, Avalonia.Controls.Dock.Top);
        DockPanel.SetDock(bottom, Avalonia.Controls.Dock.Bottom);
        DockPanel.SetDock(tools, Avalonia.Controls.Dock.Left);
        layout.Children.Add(top);
        layout.Children.Add(bottom);
        layout.Children.Add(tools);
        layout.Children.Add(new ScrollViewer { HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, Content = new Border { Padding = new Thickness(12), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Child = Canvas } });
        Content = layout;
        KeyDown += OnKey;
        ApplyColor(new Rgba(255, 255, 255, 255));
        RefreshButtons();
    }

    public PixelCanvas Canvas { get; }

    private void SetColor(Rgba color)
    {
        _hex.Text = color.ToString();
        ApplyColor(color);
    }

    private void ApplyColor(Rgba color)
    {
        Canvas.Color = color;
        _swatch.Background = new SolidColorBrush(Avalonia.Media.Color.FromArgb(color.A, color.R, color.G, color.B));
    }

    private void RefreshButtons()
    {
        _undoButton.IsEnabled = Canvas.CanUndo;
        _redoButton.IsEnabled = Canvas.CanRedo;
    }

    private void OnKey(object? sender, KeyEventArgs e)
    {
        if (e.Source is TextBox) return;
        if (e.KeyModifiers == KeyModifiers.Control && e.Key == Key.Z) Canvas.Undo();
        else if (e.KeyModifiers == KeyModifiers.Control && e.Key is Key.Y) Canvas.Redo();
        else if (e.Key == Key.Escape) Close(false);
        else return;
        e.Handled = true;
    }
}
