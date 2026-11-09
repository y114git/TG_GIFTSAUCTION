using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using G3MStudio.Core.ModPack;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

namespace G3MStudio.Mods;

public sealed partial class DiffWindow
{
    /// <summary>What the detail tabs show for one item, read once off the UI thread.</summary>
    private sealed record Detail(IReadOnlyList<PropertyChange> Properties, string? LeftCode, string? RightCode, IReadOnlyList<PixelBuffer?> LeftFrames, IReadOnlyList<PixelBuffer?> RightFrames, IReadOnlyList<InstanceChange> Instances);

    private readonly TabControl _tabs = new() { Name = "DiffTabs" };
    private readonly TabItem _propsTab = new() { Name = "DiffTabProperties", Header = Loc.Current["mods.diff.tab.properties"] };
    private readonly TabItem _codeTab = new() { Name = "DiffTabCode", Header = Loc.Current["mods.diff.tab.code"] };
    private readonly TabItem _pictureTab = new() { Name = "DiffTabPicture", Header = Loc.Current["mods.diff.tab.picture"] };
    private readonly TabItem _roomTab = new() { Name = "DiffTabRoom", Header = Loc.Current["mods.diff.tab.room"] };
    private readonly ListBox _props = new() { Name = "DiffProperties" };
    private readonly ListBox _code = new() { Name = "DiffCode", FontFamily = (FontFamily?)Application.Current?.FindResource("Font.Code") ?? FontFamily.Default };
    private readonly ListBox _room = new() { Name = "DiffInstances" };
    private readonly ToggleButton _unified = new() { Name = "DiffUnified" };
    private readonly CheckBox _ignoreSpace = new() { Name = "DiffIgnoreSpace", Content = Loc.Current["mods.diff.ignoreSpace"] };
    private readonly ComboBox _mode = new() { Name = "DiffPictureMode" };
    private readonly NumericUpDown _frame = new() { Name = "DiffFrame", Minimum = 0, Maximum = 0, Value = 0, Width = 100 };
    private readonly Slider _slider = new() { Name = "DiffSlider", Minimum = 0, Maximum = 100, Value = 50, MinWidth = 200 };
    private readonly Image _image = new() { Name = "DiffImage", Stretch = Stretch.Uniform, MinHeight = 160 };
    private readonly TextBlock _pictureNote = new() { Name = "DiffPictureNote", Classes = { "muted" } };
    private IReadOnlyList<PropertyChange> _changes = [];
    private Detail? _detail;

    private TabControl Details()
    {
        _unified.Content = Loc.Current[_options.Unified ? "mods.diff.unified" : "mods.diff.sideBySide"];
        _unified.IsChecked = _options.Unified;
        _ignoreSpace.IsChecked = _options.IgnoreWhitespace;
        foreach (var key in new[] { "mods.diff.pictureOverlay", "mods.diff.pictureSwipe", "mods.diff.pictureDifference" }) _mode.Items.Add(Loc.Current[key]);
        _mode.SelectedIndex = _options.PictureMode switch { "swipe" => 1, "difference" => 2, _ => 0 };
        _unified.IsCheckedChanged += (_, _) =>
        {
            _unified.Content = Loc.Current[_unified.IsChecked == true ? "mods.diff.unified" : "mods.diff.sideBySide"];
            RenderCode();
        };
        _ignoreSpace.IsCheckedChanged += (_, _) => RenderCode();
        _mode.SelectionChanged += (_, _) => RenderPicture();
        _frame.ValueChanged += (_, _) => RenderPicture();
        _slider.PropertyChanged += (_, e) => { if (e.Property == RangeBase.ValueProperty) RenderPicture(); };
        RenderOptions.SetBitmapInterpolationMode(_image, BitmapInterpolationMode.None);
        var codeBar = new WrapPanel { ItemSpacing = ModsUi.Space(2), LineSpacing = ModsUi.Space(2), Children = { _unified, _ignoreSpace } };
        var pictureBar = new WrapPanel { ItemSpacing = ModsUi.Space(2), LineSpacing = ModsUi.Space(2), Children = { _mode, new TextBlock { Text = Loc.Current["mods.diff.frame"], VerticalAlignment = VerticalAlignment.Center }, _frame, _slider } };
        _propsTab.Content = _props;
        _codeTab.Content = Stack(codeBar, _code);
        _pictureTab.Content = Stack(pictureBar, _pictureNote, _image);
        _roomTab.Content = _room;
        _tabs.Items.Add(_propsTab);
        _tabs.Items.Add(_codeTab);
        _tabs.Items.Add(_pictureTab);
        _tabs.Items.Add(_roomTab);
        return _tabs;
    }

    // The controls before the last one sit on top in order; the last one takes the rest.
    private static DockPanel Stack(params Control[] controls)
    {
        var panel = new DockPanel();
        foreach (var top in controls[..^1]) DockPanel.SetDock(top, Avalonia.Controls.Dock.Top);
        panel.Children.AddRange(controls);
        return panel;
    }

    private PropertyChange? SelectedProperty() => ReferenceEquals(_tabs.SelectedItem, _propsTab) && _props.SelectedIndex >= 0 && _props.SelectedIndex < _changes.Count ? _changes[_props.SelectedIndex] : null;

    private async Task SelectAsync()
    {
        _selected = (_tree.SelectedItem as TreeViewItem)?.Tag as DiffItem;
        _status.Text = "";
        if (_selected is not { } item) return;
        var detail = await Task.Run(() => Read(item));
        if (_selected != item) return;
        _detail = detail;
        _changes = detail.Properties;
        _props.ItemsSource = detail.Properties.Select(p => new TextBlock { Text = Loc.Current.Format("mods.diff.propRow", ("path", p.Path), ("left", p.Left ?? Loc.Current["mods.diff.none"]), ("right", p.Right ?? Loc.Current["mods.diff.none"])), Foreground = ModsUi.Ink(Kind(p)) }).ToList();
        var isCode = item.Type == "code";
        var isPicture = item.Type is "sprites" or "tileSets";
        (_propsTab.IsVisible, _codeTab.IsVisible, _pictureTab.IsVisible, _roomTab.IsVisible) = (!isCode, isCode, isPicture, item.Type == "rooms");
        _tabs.SelectedItem = isCode ? _codeTab : isPicture ? _pictureTab : item.Type == "rooms" ? _roomTab : _propsTab;
        _frame.Maximum = Math.Max(Math.Max(detail.LeftFrames.Count, detail.RightFrames.Count) - 1, 0);
        _frame.Value = 0;
        RenderCode();
        RenderPicture();
        _room.ItemsSource = detail.Instances.Select(i => new TextBlock { Text = Loc.Current.Format("mods.diff.instance", ("kind", ModsUi.KindName(i.Kind)), ("id", i.Id), ("object", i.ObjectName), ("detail", i.Detail)), Foreground = ModsUi.Ink(i.Kind) }).ToList();
    }

    private static ChangeKind Kind(PropertyChange change) => change.Left is null ? ChangeKind.Added : change.Right is null ? ChangeKind.Removed : ChangeKind.Changed;

    private Detail Read(DiffItem item)
    {
        using (_left.Lock.Read())
        using (_right.Lock.Read())
        {
            var picture = item.Type is "sprites" or "tileSets";
            return new Detail(DataDiff.Properties(_left.Data, _right.Data, item.Type, item.Name),
                item.Type == "code" ? DataDiff.CodeOf(_left.Data, item.Name) : null, item.Type == "code" ? DataDiff.CodeOf(_right.Data, item.Name) : null,
                picture ? PictureDiff.Frames(_left.Data, item.Type, item.Name) : [], picture ? PictureDiff.Frames(_right.Data, item.Type, item.Name) : [],
                item.Type == "rooms" ? DataDiff.Instances(_left.Data, _right.Data, item.Name) : []);
        }
    }

    private void RenderCode()
    {
        if (_detail is null || _selected?.Type != "code") return;
        var lines = CodeView.Lines(_detail.LeftCode ?? "", _detail.RightCode ?? "", _ignoreSpace.IsChecked == true);
        _code.ItemsSource = _unified.IsChecked == true
            ? CodeView.Unified(lines, _options.ContextLines).Split('\n').Select(UnifiedRow).ToList()
            : CodeView.SideBySide(lines).Select(SideRowView).ToList();
    }

    private static TextBlock UnifiedRow(string text) => new() { Text = text, Foreground = text.StartsWith('+') ? ModsUi.Ink(ChangeKind.Added) : text.StartsWith('-') ? ModsUi.Ink(ChangeKind.Removed) : null };

    private static Grid SideRowView(SideRow row)
    {
        var grid = new Grid { ColumnDefinitions = { new ColumnDefinition(1, GridUnitType.Star), new ColumnDefinition(1, GridUnitType.Star) } };
        grid.Children.Add(Cell(row.Left, ChangeKind.Removed, 0));
        grid.Children.Add(Cell(row.Right, ChangeKind.Added, 1));
        return grid;
    }

    private static Border Cell(DiffLine? line, ChangeKind kind, int column)
    {
        var cell = new Border { BorderThickness = new Thickness(0), Background = line is null || line.Kind == DiffKind.Same ? null : ModsUi.Tint(kind), Child = new TextBlock { Text = line?.Text ?? "", TextWrapping = TextWrapping.NoWrap } };
        Grid.SetColumn(cell, column);
        return cell;
    }

    private void RenderPicture()
    {
        if (_detail is null || _selected?.Type is not ("sprites" or "tileSets")) return;
        var at = (int)(_frame.Value ?? 0);
        var (a, b) = (_detail.LeftFrames.ElementAtOrDefault(at), _detail.RightFrames.ElementAtOrDefault(at));
        if (a is null && b is null)
        {
            _image.Source = null;
            _pictureNote.Text = Loc.Current["mods.diff.noPicture"];
            return;
        }

        _pictureNote.Text = "";
        var percent = _slider.Value / 100;
        PixelBuffer shown;
        switch (_mode.SelectedIndex)
        {
            case 1:
                shown = PictureDiff.Swipe(a, b, (int)(Math.Max(a?.Width ?? 0, b?.Width ?? 0) * percent));
                break;
            case 2:
                shown = PictureDiff.Difference(a, b, out var count);
                _pictureNote.Text = Loc.Current.Format("mods.diff.differing", ("count", count));
                break;
            default:
                shown = PictureDiff.Overlay(a, b, percent);
                break;
        }

        _image.Source = Bitmap(shown);
    }

    private static WriteableBitmap Bitmap(PixelBuffer picture)
    {
        var bitmap = new WriteableBitmap(new PixelSize(picture.Width, picture.Height), new Vector(96, 96), PixelFormat.Rgba8888, AlphaFormat.Unpremul);
        using var frame = bitmap.Lock();
        foreach (var row in Enumerable.Range(0, picture.Height)) System.Runtime.InteropServices.Marshal.Copy(picture.Data, row * picture.Width * 4, frame.Address + row * frame.RowBytes, picture.Width * 4);
        return bitmap;
    }
}
