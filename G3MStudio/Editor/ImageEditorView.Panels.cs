using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

namespace G3MStudio.Editor;

public sealed partial class ImageEditorView
{
    private static readonly string[] Palette =
    [
        "#000000", "#202020", "#404040", "#606060", "#808080", "#A0A0A0", "#C0C0C0", "#FFFFFF",
        "#800000", "#FF0000", "#FF8000", "#FFC000", "#FFFF00", "#80FF00", "#00FF00", "#00FF80",
        "#00FFFF", "#0080FF", "#0000FF", "#8000FF", "#FF00FF", "#FF0080", "#804000", "#408000",
        "#004080", "#400080", "#FFC0C0", "#C0FFC0", "#C0C0FF", "#FFE0A0", "#A0E0FF", "#E0A0FF",
    ];

    private readonly List<(ImageTool Tool, ToggleButton Button)> _toolButtons = [];
    private readonly Button _undoButton = new() { Name = "ImageUndo" };
    private readonly Button _redoButton = new() { Name = "ImageRedo" };
    private readonly Border _primary = new() { Name = "PrimarySwatch", Width = 40, Height = 40, BorderThickness = new Thickness(2) };
    private readonly Border _secondary = new() { Name = "SecondarySwatch", Width = 40, Height = 40, BorderThickness = new Thickness(2) };
    private readonly TextBox _hex = new() { Name = "ImageColour", Width = 100 };
    private readonly WrapPanel _recentPanel = new() { MaxWidth = 200 };
    private readonly NumericUpDown _size = new() { Name = "ImageSize", Minimum = 1, Maximum = 256, Value = 1, Increment = 1, FormatString = "0", Width = 100 };
    private readonly ComboBox _style = new() { Name = "ImageStyle", Width = 110 };
    private readonly StackPanel _frames = new() { Name = "ImageFrames", Width = 120, Spacing = 4, Margin = new Thickness(6) };

    private void BuildLayout()
    {
        var top = Options();
        var bottom = new DockPanel { Margin = new Thickness(8, 2), Children = { _status, _hint } };
        SetDock(top, Avalonia.Controls.Dock.Top);
        SetDock(bottom, Avalonia.Controls.Dock.Bottom);
        var right = new ScrollViewer { Width = 232, Content = new StackPanel { Margin = new Thickness(6), Spacing = 6, Children = { Brushes(), Colours(), Toolbox() } } };
        SetDock(right, Avalonia.Controls.Dock.Right);
        SetDock(_frames, Avalonia.Controls.Dock.Left);
        BuildFrames();
        Children.Add(top);
        Children.Add(bottom);
        Children.Add(right);
        Children.Add(_frames);
        Children.Add(Canvas);
    }

    private static Button Small(string name, string text, Action click)
    {
        var button = new Button { Name = name, Content = text, Padding = new Thickness(6, 2) };
        button.Click += (_, _) => click();
        return button;
    }

    private WrapPanel Options()
    {
        _size.ValueChanged += (_, e) => Canvas.BrushSize = (int)(e.NewValue ?? 1);
        foreach (var style in Enum.GetValues<ShapeStyle>()) _style.Items.Add(Loc.Current["image.style." + style]);
        _style.SelectedIndex = 0;
        _style.SelectionChanged += (_, _) => Canvas.Style = (ShapeStyle)Math.Max(0, _style.SelectedIndex);
        var round = new ToggleButton { Name = "ImageRound", Content = Loc.Current["image.round"] };
        round.IsCheckedChanged += (_, _) => Canvas.Shape = round.IsChecked == true ? BrushShape.Round : BrushShape.Square;
        var tolerance = new NumericUpDown { Name = "ImageTolerance", Minimum = 0, Maximum = 255, Value = 0, Increment = 5, FormatString = "0", Width = 100 };
        tolerance.ValueChanged += (_, e) => Canvas.Tolerance = (int)(e.NewValue ?? 0);
        var contiguous = new CheckBox { Name = "ImageContiguous", Content = Loc.Current["image.contiguous"], IsChecked = true };
        contiguous.IsCheckedChanged += (_, _) => Canvas.Contiguous = contiguous.IsChecked == true;
        var mirrorX = Toggle("ImageMirrorX", "image.symmetryH", on => Canvas.Symmetry = on ? Canvas.Symmetry | SymmetryMode.Horizontal : Canvas.Symmetry & ~SymmetryMode.Horizontal);
        var mirrorY = Toggle("ImageMirrorY", "image.symmetryV", on => Canvas.Symmetry = on ? Canvas.Symmetry | SymmetryMode.Vertical : Canvas.Symmetry & ~SymmetryMode.Vertical);
        var grid = Toggle("ImageGrid", "image.grid", on => Canvas.ShowGrid = on, true);
        var tiles = Toggle("ImageTiles", "image.tiles", on => Canvas.TilePreview = on);
        var onion = Toggle("ImageOnion", "image.onion", on =>
        {
            _onionOn = on;
            UpdateOnion();
        });
        _undoButton.Content = Loc.Current["image.undo"];
        _redoButton.Content = Loc.Current["image.redo"];
        _undoButton.Click += (_, _) => Undo();
        _redoButton.Click += (_, _) => Redo();
        var zoom = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { Small("ImageZoomOut", "-", () => Canvas.ZoomBy(0.8)), Small("ImageZoomIn", "+", () => Canvas.ZoomBy(1.25)), Small("ImageFit", Loc.Current["image.fit"], Canvas.Fit), Small("ImageActual", "1:1", Canvas.ActualSize) } };
        var bar = new WrapPanel { Margin = new Thickness(6, 4) };
        foreach (var item in new Control[] { Menus(), _undoButton, _redoButton, Label("image.sizeLabel"), _size, round, _style, Label("image.tolerance"), tolerance, contiguous, mirrorX, mirrorY, grid, tiles, onion, zoom })
        {
            item.Margin = new Thickness(0, 0, 8, 2);
            item.VerticalAlignment = VerticalAlignment.Center;
            bar.Children.Add(item);
        }

        RefreshButtons();
        return bar;
    }

    private static TextBlock Label(string key) => new() { Text = Loc.Current[key], Classes = { "muted" } };

    private static ToggleButton Toggle(string name, string key, Action<bool> changed, bool on = false)
    {
        var button = new ToggleButton { Name = name, Content = Loc.Current[key], IsChecked = on };
        button.IsCheckedChanged += (_, _) => changed(button.IsChecked == true);
        return button;
    }

    public void SetSize(int size) => _size.Value = Math.Clamp(size, 1, 256);

    public void SetStyle(ShapeStyle style) => _style.SelectedIndex = (int)style;

    private void RefreshButtons()
    {
        _undoButton.IsEnabled = _session.CanUndo;
        _redoButton.IsEnabled = _session.CanRedo;
    }

    private StackPanel Brushes()
    {
        var panel = new WrapPanel { Name = "ImageBrushes" };
        foreach (var (shape, size) in new[] { (BrushShape.Square, 1), (BrushShape.Square, 2), (BrushShape.Square, 3), (BrushShape.Square, 5), (BrushShape.Square, 8), (BrushShape.Round, 3), (BrushShape.Round, 5), (BrushShape.Round, 8), (BrushShape.Round, 12), (BrushShape.Round, 16) })
        {
            var button = new Button { Width = 36, Height = 36, Padding = new Thickness(0), Margin = new Thickness(1), Content = BrushPreview(shape, size) };
            ToolTip.SetTip(button, $"{Loc.Current["image.shape." + shape]} {size}");
            button.Click += (_, _) =>
            {
                Canvas.Shape = shape;
                SetSize(size);
                Canvas.Focus();
            };
            panel.Children.Add(button);
        }

        return Section("image.brushes", panel);
    }

    private static Border BrushPreview(BrushShape shape, int size)
    {
        var side = Math.Min(24, Math.Max(3, size * 2));
        return new Border { Width = side, Height = side, Background = Avalonia.Media.Brushes.White, CornerRadius = shape == BrushShape.Round ? new CornerRadius(side / 2.0) : default };
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
        return Section("image.colours", new StackPanel { Spacing = 4, Children = { swatches, pair, _hex, Label("image.recent"), _recentPanel } });
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
        _primary.BorderBrush = _secondary.BorderBrush = Avalonia.Media.Brushes.Gray;
        if (_hex.Text != Canvas.Primary.ToString()) _hex.Text = Canvas.Primary.ToString();
        _recentPanel.Children.Clear();
        foreach (var colour in _recent) _recentPanel.Children.Add(Chip(colour));
    }

    public void SwapColours()
    {
        (Canvas.Primary, Canvas.Secondary) = (Canvas.Secondary, Canvas.Primary);
        ShowColours();
    }

    private StackPanel Toolbox()
    {
        var panel = new WrapPanel { Name = "ImageToolbox" };
        foreach (var tool in ImageTools.All)
        {
            var button = new ToggleButton { Name = "Tool_" + tool, Width = 44, Height = 40, Margin = new Thickness(1), Padding = new Thickness(0), Content = new PathIcon { Data = Geometry.Parse(ImageTools.Icon(tool)), Width = 20, Height = 20 } };
            ToolTip.SetTip(button, $"{Loc.Current["image.tool." + tool]} ({ImageTools.KeyOf(tool)})");
            button.Click += (_, _) =>
            {
                SetTool(tool);
                Canvas.Focus();
            };
            _toolButtons.Add((tool, button));
            panel.Children.Add(button);
        }

        return Section("image.toolbox", panel);
    }

    private static StackPanel Section(string key, Control body) => new() { Spacing = 2, Children = { new TextBlock { Text = Loc.Current[key], FontWeight = FontWeight.SemiBold }, body } };

    private void BuildFrames()
    {
        var play = new ToggleButton { Name = "ImagePlay", Content = Loc.Current["image.play"] };
        play.IsCheckedChanged += (_, _) =>
        {
            if (play.IsChecked == true && _session.FrameCount > 1) _play.Start();
            else _play.Stop();
        };
        _frames.Children.Add(new TextBlock { Text = Loc.Current["image.frames"], FontWeight = FontWeight.SemiBold });
        _frames.Children.Add(Small("ImagePrev", Loc.Current["image.prev"], () => ShowFrame(Frame - 1)));
        _frames.Children.Add(Small("ImageNext", Loc.Current["image.next"], () => ShowFrame(Frame + 1)));
        _frames.Children.Add(Small("ImageAddFrame", Loc.Current["image.addFrame"], () => ChangeFrames(FrameListCommand.Duplicate)));
        _frames.Children.Add(Small("ImageRemoveFrame", Loc.Current["image.removeFrame"], () => ChangeFrames(FrameListCommand.Remove)));
        _frames.Children.Add(play);
    }

    private void ChangeFrames(string operation)
    {
        if (!_session.ChangeFrames(operation, Frame)) return;
        Reload();
    }
}
