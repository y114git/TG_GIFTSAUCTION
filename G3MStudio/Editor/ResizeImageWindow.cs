using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

namespace G3MStudio.Editor;

/// <summary>What the Resize dialog chose.</summary>
public sealed record ResizeChoice(ResizeSpec Spec, bool AllFrames, string? Group);

/// <summary>
/// The Resize dialog of the image editor: scale the picture or change the canvas around an anchor, in pixels or percent with the aspect kept on request,
/// for this frame or every frame of the sprite, and the texture group the sprite belongs to.
/// </summary>
public sealed class ResizeImageWindow : Window
{
    private readonly (int Width, int Height) _size;
    private readonly ComboBox _mode = new() { Name = "ResizeMode", MinWidth = 200 };
    private readonly ComboBox _unit = new() { Name = "ResizeUnit", MinWidth = 200 };
    private readonly NumericUpDown _width = new() { Name = "ResizeWidth", Minimum = 1, Maximum = ResizeSpec.MaxSide, Increment = 1, FormatString = "0", Width = 150 };
    private readonly NumericUpDown _height = new() { Name = "ResizeHeight", Minimum = 1, Maximum = ResizeSpec.MaxSide, Increment = 1, FormatString = "0", Width = 150 };
    private readonly CheckBox _keep = new() { Name = "ResizeKeep", IsChecked = true };
    private readonly CheckBox _all = new() { Name = "ResizeAll", IsChecked = true };
    private readonly ComboBox _group = new() { Name = "ResizeGroup", MinWidth = 200 };
    private readonly RadioButton[] _anchors = new RadioButton[9];
    private readonly IReadOnlyList<string> _groups;
    private bool _busy;

    public ResizeImageWindow((int Width, int Height) size, int frames, IReadOnlyList<string> groups, string? group, bool scale)
    {
        (_size, _groups) = (size, groups);
        Title = Loc.Current["image.resizeTitle"];
        SizeToContent = SizeToContent.WidthAndHeight;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        _mode.ItemsSource = new[] { Loc.Current["image.resizeScale"], Loc.Current["image.resizeCanvas"] };
        _mode.SelectedIndex = scale ? 0 : 1;
        _unit.ItemsSource = new[] { Loc.Current["image.unitPixels"], Loc.Current["image.unitPercent"] };
        _unit.SelectedIndex = 0;
        _keep.Content = Loc.Current["image.keepRatio"];
        _all.Content = Loc.Current["image.allFrames"];
        _all.IsVisible = frames > 1;
        _group.ItemsSource = groups;
        _group.SelectedItem = group;
        SetValues(size.Width, size.Height);
        var grid = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,Auto"), ColumnDefinitions = new ColumnDefinitions("Auto,Auto,Auto"), HorizontalAlignment = HorizontalAlignment.Left };
        for (var i = 0; i < 9; i++)
        {
            var anchor = _anchors[i] = new RadioButton { Name = "ResizeAnchor" + i, GroupName = "ResizeAnchor", IsChecked = i == 0, Margin = new Thickness(2) };
            Grid.SetRow(anchor, i / 3);
            Grid.SetColumn(anchor, i % 3);
            grid.Children.Add(anchor);
        }

        var ok = new Button { Name = "ResizeOk", Content = Loc.Current["image.ok"], Classes = { "primary" } };
        var cancel = new Button { Name = "ResizeCancel", Content = Loc.Current["explorer.cancel"] };
        _mode.SelectionChanged += (_, _) => grid.IsEnabled = _mode.SelectedIndex == 1;
        grid.IsEnabled = !scale;
        _unit.SelectionChanged += (_, _) => SetValues(_size.Width, _size.Height);
        _width.ValueChanged += (_, _) => Follow(true);
        _height.ValueChanged += (_, _) => Follow(false);
        ok.Click += (_, _) => Close(true);
        cancel.Click += (_, _) => Close(false);
        var body = new StackPanel { Spacing = 6, Margin = new Thickness(12) };
        body.Children.Add(_mode);
        body.Children.Add(_unit);
        body.Children.Add(Row("image.p.width", _width));
        body.Children.Add(Row("image.p.height", _height));
        body.Children.Add(_keep);
        body.Children.Add(Row("image.anchor", grid));
        body.Children.Add(_all);
        if (groups.Count > 0) body.Children.Add(Row("image.textureGroup", _group));
        body.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Children = { cancel, ok } });
        Content = body;
    }

    private bool Percent => _unit.SelectedIndex == 1;

    private static StackPanel Row(string label, Control control) =>
        new() { Orientation = Orientation.Horizontal, Spacing = 8, Children = { new TextBlock { Text = Loc.Current[label], Width = 110, VerticalAlignment = VerticalAlignment.Center }, control } };

    private void SetValues(int w, int h)
    {
        _busy = true;
        (_width.Value, _height.Value) = Percent ? (100, 100) : (w, h);
        _busy = false;
    }

    // With the aspect kept, the other side follows the one the user changed.
    private void Follow(bool widthChanged)
    {
        if (_busy || _keep.IsChecked != true) return;
        _busy = true;
        if (widthChanged) _height.Value = Percent ? _width.Value : Math.Clamp((decimal)Math.Round((double)(_width.Value ?? 1) * _size.Height / _size.Width), 1, ResizeSpec.MaxSide);
        else _width.Value = Percent ? _height.Value : Math.Clamp((decimal)Math.Round((double)(_height.Value ?? 1) * _size.Width / _size.Height), 1, ResizeSpec.MaxSide);
        _busy = false;
    }

    /// <summary>What the dialog holds now.</summary>
    public ResizeChoice Choice
    {
        get
        {
            var (w, h) = ((double)(_width.Value ?? 1), (double)(_height.Value ?? 1));
            var (pw, ph) = Percent ? (ResizeSpec.Percent(_size.Width, w), ResizeSpec.Percent(_size.Height, h)) : ((int)w, (int)h);
            var anchor = Math.Max(0, Array.FindIndex(_anchors, a => a.IsChecked == true));
            return new ResizeChoice(new ResizeSpec(_mode.SelectedIndex == 0, pw, ph, anchor % 3, anchor / 3), !_all.IsVisible || _all.IsChecked == true, _groups.Count > 0 ? _group.SelectedItem as string : null);
        }
    }

    /// <summary>The choice, or null on Cancel.</summary>
    public async Task<ResizeChoice?> AskAsync(Window owner) => await ShowDialog<bool?>(owner) == true ? Choice : null;
}
