using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;
using G3MStudio.Theme;

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
        Width = 460;
        SizeToContent = SizeToContent.Height;
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

        var ok = DialogFrame.Action(this, Loc.Current["image.ok"], "ResizeOk", () => Close(true), DialogRole.Primary);
        _mode.SelectionChanged += (_, _) => grid.IsEnabled = _mode.SelectedIndex == 1;
        grid.IsEnabled = !scale;
        _unit.SelectionChanged += (_, _) => SetValues(_size.Width, _size.Height);
        _width.ValueChanged += (_, _) => Follow(true);
        _height.ValueChanged += (_, _) => Follow(false);
        var body = new StackPanel { Spacing = 8 };
        body.Children.Add(_mode);
        body.Children.Add(_unit);
        body.Children.Add(DialogFrame.Form((Loc.Current["image.p.width"], _width), (Loc.Current["image.p.height"], _height)));
        body.Children.Add(_keep);
        body.Children.Add(DialogFrame.Form((Loc.Current["image.anchor"], grid)));
        body.Children.Add(_all);
        if (groups.Count > 0) body.Children.Add(DialogFrame.Form((Loc.Current["image.textureGroup"], _group)));
        DialogFrame.Show(this, body, [ok, DialogFrame.Cancel(this, () => Close(false), "ResizeCancel")], scroll: DialogScroll.Vertical,
            dirty: DialogFrame.Snapshot(_mode, _unit, _width, _height, _keep, _all, _group));
    }

    private bool Percent => _unit.SelectedIndex == 1;

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
