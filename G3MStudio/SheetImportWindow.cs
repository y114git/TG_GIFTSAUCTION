using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

namespace G3MStudio;

/// <summary>
/// Cuts a picture into sprite frames: by frame count (columns and rows) or by frame size, with an offset and gaps. A file named *_stripN starts as N frames in one row.
/// An animated GIF brings its own frames and is not cut. The summary shows what will be imported; Replace takes the frame size for the sprite.
/// </summary>
public sealed class SheetImportWindow : Window
{
    private readonly IReadOnlyList<PixelBuffer> _source;
    private readonly (int Width, int Height) _sprite;
    private readonly bool _gif;
    private readonly ComboBox _mode = new() { Name = "SheetMode", MinWidth = 160 };
    private readonly NumericUpDown _columns = Box("SheetColumns", 1, 1, 256), _rows = Box("SheetRows", 1, 1, 256), _width = Box("SheetWidth", 1, 1, 8192), _height = Box("SheetHeight", 1, 1, 8192),
        _count = Box("SheetCount", 0, 0, 1024), _offsetX = Box("SheetOffsetX", 0, 0, 8192), _offsetY = Box("SheetOffsetY", 0, 0, 8192), _gapX = Box("SheetGapX", 0, 0, 8192), _gapY = Box("SheetGapY", 0, 0, 8192);
    private readonly CheckBox _replace = new() { Name = "SheetReplace" };
    private readonly TextBlock _summary = new() { Name = "SheetSummary", TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private readonly Button _ok = new() { Name = "SheetOk", Classes = { "primary" } };
    private bool _busy;
    private readonly Avalonia.Threading.DispatcherTimer _debounce = new() { Interval = TimeSpan.FromMilliseconds(150) };
    private IReadOnlyList<PixelBuffer> _frames = [];

    /// <param name="source">The frames the file gave: one picture to cut, or the frames of a GIF.</param>
    /// <param name="stripCount">The count in the file name (_stripN), or null.</param>
    /// <param name="sprite">The size of the sprite the frames go into.</param>
    public SheetImportWindow(IReadOnlyList<PixelBuffer> source, int? stripCount, (int Width, int Height) sprite)
    {
        (_source, _sprite, _gif) = (source, sprite, source.Count > 1);
        Title = Loc.Current["sprites.sheetTitle"];
        SizeToContent = SizeToContent.WidthAndHeight;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var picture = source[0];
        _mode.ItemsSource = new[] { Loc.Current["sprites.sheetByCount"], Loc.Current["sprites.sheetBySize"] };
        _mode.SelectedIndex = stripCount is null ? 1 : 0;
        _columns.Value = stripCount ?? 1;
        _width.Value = stripCount is null ? Math.Min(picture.Width, picture.Height) : SheetSplit.SizeForCount(picture.Width, picture.Height, stripCount.Value, 1, 0, 0, 0, 0).Width;
        _height.Value = stripCount is null ? Math.Min(picture.Width, picture.Height) : picture.Height;
        _replace.Content = Loc.Current["sprites.sheetReplace"];
        _replace.IsChecked = (_gif ? picture.Width : (int)_width.Value, _gif ? picture.Height : (int)_height.Value) != sprite;
        _ok.Content = Loc.Current["image.ok"];
        var cancel = new Button { Name = "SheetCancel", Content = Loc.Current["explorer.cancel"] };
        // Typing in a box fires a change per keystroke; the cut runs once the typing pauses (reading Frames or pressing OK runs it at once).
        _debounce.Tick += (_, _) => Flush();
        foreach (var box in new[] { _columns, _rows, _width, _height, _count, _offsetX, _offsetY, _gapX, _gapY })
            box.ValueChanged += (_, _) =>
            {
                if (_busy) return;
                _debounce.Stop();
                _debounce.Start();
            };
        _mode.SelectionChanged += (_, _) => Refresh();
        _replace.IsCheckedChanged += (_, _) => Refresh();
        _ok.Click += (_, _) =>
        {
            Flush();
            if (_ok.IsEnabled) Close(true);
        };
        Closed += (_, _) => _debounce.Stop();
        cancel.Click += (_, _) => Close(false);
        var body = new StackPanel { Spacing = 6, Margin = new Thickness(12) };
        if (!_gif)
        {
            body.Children.Add(_mode);
            body.Children.Add(Row("sprites.sheetColumns", _columns, "sprites.sheetRows", _rows));
            body.Children.Add(Row("sprites.sheetFrameWidth", _width, "sprites.sheetFrameHeight", _height));
            body.Children.Add(Row("sprites.sheetOffsetX", _offsetX, "sprites.sheetOffsetY", _offsetY));
            body.Children.Add(Row("sprites.sheetGapX", _gapX, "sprites.sheetGapY", _gapY));
            body.Children.Add(Row("sprites.sheetCount", _count));
        }

        body.Children.Add(_replace);
        body.Children.Add(_summary);
        body.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Children = { cancel, _ok } });
        Content = body;
        Refresh();
    }

    public IReadOnlyList<PixelBuffer> Frames
    {
        get
        {
            Flush();
            return _frames;
        }
    }

    private void Flush()
    {
        if (!_debounce.IsEnabled) return;
        _debounce.Stop();
        Refresh();
    }

    public bool Replace => _replace.IsChecked == true;

    private static NumericUpDown Box(string name, int value, int min, int max) => new() { Name = name, Minimum = min, Maximum = max, Value = value, Increment = 1, FormatString = "0", Width = 150 };

    private static StackPanel Row(string labelA, Control a, string? labelB = null, Control? b = null)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        row.Children.Add(new TextBlock { Text = Loc.Current[labelA], Width = 110, VerticalAlignment = VerticalAlignment.Center });
        row.Children.Add(a);
        if (labelB is not null && b is not null)
        {
            row.Children.Add(new TextBlock { Text = Loc.Current[labelB], Width = 110, VerticalAlignment = VerticalAlignment.Center });
            row.Children.Add(b);
        }

        return row;
    }

    private static int Number(NumericUpDown box) => (int)(box.Value ?? 0);

    private void Refresh()
    {
        if (_busy) return;
        _busy = true;
        try
        {
            var bySize = _mode.SelectedIndex == 1;
            (_columns.IsEnabled, _rows.IsEnabled, _width.IsEnabled, _height.IsEnabled) = (!bySize, !bySize, bySize, bySize);
            var picture = _source[0];
            if (_gif)
            {
                _frames = _source;
            }
            else
            {
                if (!bySize)
                {
                    var size = SheetSplit.SizeForCount(picture.Width, picture.Height, Number(_columns), Number(_rows), Number(_offsetX), Number(_offsetY), Number(_gapX), Number(_gapY));
                    (_width.Value, _height.Value) = (size.Width, size.Height);
                }

                var count = Number(_count) > 0 ? Number(_count) : bySize ? 0 : Number(_columns) * Number(_rows);
                _frames = SheetSplit.Cut(picture, new FrameGrid(Number(_width), Number(_height), Number(_offsetX), Number(_offsetY), Number(_gapX), Number(_gapY)), count);
            }

            var (w, h) = _frames.Count > 0 ? (_frames[0].Width, _frames[0].Height) : (0, 0);
            var text = Loc.Current.Format(_gif ? "sprites.sheetGif" : "sprites.sheetSummary", ("count", _frames.Count), ("width", w), ("height", h));
            var mismatch = _frames.Count > 0 && !Replace && (w, h) != _sprite;
            if (mismatch) text += " " + Loc.Current.Format("sprites.sheetMismatch", ("width", _sprite.Width), ("height", _sprite.Height));
            _summary.Text = text;
            _ok.IsEnabled = _frames.Count > 0 && !mismatch;
        }
        finally
        {
            _busy = false;
        }
    }

    /// <summary>True when the user accepted; <see cref="Frames"/> and <see cref="Replace"/> then hold the choice.</summary>
    public async Task<bool> AskAsync(Window owner) => await ShowDialog<bool?>(owner) ?? false;
}
