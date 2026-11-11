using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using G3MLib.DataFile.Models;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;
using G3MStudio.Rooms;

namespace G3MStudio.Editor;

/// <summary>
/// The sprite tools panel: the origin preset list, the collision mask section (mode, shape, tolerance, box) and the nine-slice section.
/// <see cref="Canvas"/> is the one picture with the draggable origin, box and guides; <see cref="SpriteEditorView"/> places it. Every change is one undoable command.
/// </summary>
public sealed class SpriteToolsView : StackPanel
{
    private static readonly string[] PresetKeys =
    [
        "editors.origin.topLeft", "editors.origin.topCenter", "editors.origin.topRight",
        "editors.origin.middleLeft", "editors.origin.center", "editors.origin.middleRight",
        "editors.origin.bottomLeft", "editors.origin.bottomCenter", "editors.origin.bottomRight",
        "editors.origin.custom",
    ];

    private static readonly string[] ModeKeys = ["editors.mask.auto", "editors.mask.full", "editors.mask.manual"];

    private static readonly string[] ShapeKeys = ["editors.mask.rectangle", "editors.mask.rotated", "editors.mask.ellipse", "editors.mask.diamond", "editors.mask.precise", "editors.mask.preciseFrames"];

    private static readonly string[] TileKeys = ["editors.tile.stretch", "editors.tile.repeat", "editors.tile.mirror", "editors.tile.blankRepeat", "editors.tile.hide"];

    private readonly GameMakerSprite _sprite;
    private readonly Workspace? _workspace;
    private readonly ComboBox _preset = new() { Name = "SpriteOriginPreset" };
    private readonly ComboBox _mode = new() { Name = "SpriteMaskMode" };
    private readonly ComboBox _shape = new() { Name = "SpriteMaskShape" };
    private readonly NumericUpDown _tolerance = Box("SpriteMaskTolerance", 0, 255, 110);
    private readonly NumericUpDown[] _bounds = [Box("SpriteMaskLeft", 0, 65535, 96), Box("SpriteMaskTop", 0, 65535, 96), Box("SpriteMaskRight", 0, 65535, 96), Box("SpriteMaskBottom", 0, 65535, 96)];
    private readonly CheckBox _sliceOn = new() { Name = "SpriteSliceOn", Content = Loc.Current["editors.slice.enabled"] };
    private readonly NumericUpDown[] _edges = [Box("SpriteSliceLeft", 0, 65535, 96), Box("SpriteSliceTop", 0, 65535, 96), Box("SpriteSliceRight", 0, 65535, 96), Box("SpriteSliceBottom", 0, 65535, 96)];
    private readonly ComboBox[] _tiles = [.. Enumerable.Range(0, 5).Select(i => new ComboBox { Name = $"SpriteSliceTile{i}", ItemsSource = TileKeys.Select(k => Loc.Current[k]).ToList() })];
    private readonly NumericUpDown _frame = Box("SpriteToolsFrame", 0, 0);
    private readonly Button _editMask = new() { Name = "SpriteMaskEdit", Content = Loc.Current["sprites.editMask"] };
    private bool _building;

    public SpriteToolsView(GameMakerSprite sprite, TextureBitmaps textures, Workspace? workspace)
    {
        (_sprite, _workspace) = (sprite, workspace);
        Name = "SpriteTools";
        Spacing = 6;
        Margin = new Thickness(8, 0, 8, 8);
        Canvas = new SpriteCanvas(sprite, textures);
        _preset.ItemsSource = PresetKeys.Select(k => Loc.Current[k]).ToList();
        _mode.ItemsSource = ModeKeys.Select(k => Loc.Current[k]).ToList();
        _shape.ItemsSource = ShapeKeys.Select(k => Loc.Current[k]).ToList();
        _frame.Maximum = Math.Max(0, sprite.Textures.Count - 1);
        _frame.Value = 0; // an unset box shows blank; the tolerance is not stored in the file, so it starts at 0
        _tolerance.Value = 0;
        var slice = _sprite.V3NineSlice;
        Children.Add(Row(Label("editors.frame"), _frame));
        Children.Add(Section("editors.origin.title", true, Row(_preset)));
        _editMask.Click += (_, _) => EditMaskRequested?.Invoke(Math.Clamp((int)(_frame.Value ?? 0), 0, Math.Max(0, _sprite.CollisionMasks.Count - 1)));
        Children.Add(Section("editors.mask.title", true, MaskForm(), Row(_editMask)));
        Children.Add(Section("editors.slice.enabled", slice?.Enabled == true, Row([_sliceOn, .. _edges]), Row([.. _tiles])));
        Wire();
        Show();
        Ready = Canvas.ShowFrameAsync(0);
        if (workspace is null) return;
        void Refresh() => Show();
        AttachedToVisualTree += (_, _) => workspace.History.Changed += Refresh;
        DetachedFromVisualTree += (_, _) => workspace.History.Changed -= Refresh;
    }

    public SpriteCanvas Canvas { get; }

    public event Action<int>? EditMaskRequested;

    /// <summary>Shows a frame on the canvas and in the frame box.</summary>
    public void ShowFrame(int frame) => _frame.Value = frame;

    public Task Ready { get; }

    // Mode and shape side by side, the tolerance with its label, then the four box edges under their label; nothing wraps at the panel's usual width.
    private Grid MaskForm()
    {
        var form = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto"), RowSpacing = 6, ColumnSpacing = 6 };
        var tolerance = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(_tolerance, Avalonia.Controls.Dock.Right);
        _tolerance.Width = double.NaN;
        _tolerance.MinWidth = 110;
        var toleranceLabel = Label("editors.mask.tolerance");
        toleranceLabel.Margin = new Thickness(0, 0, 8, 0);
        DockPanel.SetDock(toleranceLabel, Avalonia.Controls.Dock.Left);
        tolerance.Children.Add(toleranceLabel);
        tolerance.Children.Add(_tolerance);
        var edges = new Avalonia.Controls.Primitives.UniformGrid { Columns = 2 };
        foreach (var box in _bounds)
        {
            box.Width = double.NaN;
            box.Margin = new Thickness(0, 0, 4, 4);
            edges.Children.Add(box);
        }

        var boxLabel = Label("editors.mask.box");
        boxLabel.TextWrapping = Avalonia.Media.TextWrapping.Wrap;
        foreach (var (control, row, column, span) in new (Control, int, int, int)[] { (_mode, 0, 0, 1), (_shape, 0, 1, 1), (tolerance, 1, 0, 2), (boxLabel, 2, 0, 2), (edges, 3, 0, 2) })
        {
            control.HorizontalAlignment = HorizontalAlignment.Stretch;
            Grid.SetRow(control, row);
            Grid.SetColumn(control, column);
            Grid.SetColumnSpan(control, span);
            form.Children.Add(control);
        }

        return form;
    }

    private static NumericUpDown Box(string name, int min, int max, double width = 150) => new() { Name = name, FormatString = "0", Minimum = min, Maximum = max, Width = width };

    private static TextBlock Label(string key) => new() { Text = Loc.Current[key], VerticalAlignment = VerticalAlignment.Center };

    // A row that wraps onto the next line when the panel is narrow.
    private static WrapPanel Row(params Control[] items)
    {
        var row = new WrapPanel { ItemSpacing = 6, LineSpacing = 4 };
        foreach (var item in items) item.VerticalAlignment = VerticalAlignment.Center;
        row.Children.AddRange(items);
        return row;
    }

    private static Expander Section(string titleKey, bool open, params Control[] rows)
    {
        var body = new StackPanel { Spacing = 4 };
        body.Children.AddRange(rows);
        return new Expander { Header = Loc.Current[titleKey], IsExpanded = open, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch, Content = body };
    }

    private void Wire()
    {
        _frame.ValueChanged += async (_, _) =>
        {
            try
            {
                await Canvas.ShowFrameAsync((int)(_frame.Value ?? 0));
            }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                // a texture that cannot be read leaves the frame as it was instead of ending the app
            }
        };
        _preset.SelectionChanged += (_, _) => ApplyPreset();
        _mode.SelectionChanged += (_, _) => CommitMask(null);
        _shape.SelectionChanged += (_, _) => CommitMask(null);
        _tolerance.ValueChanged += (_, _) => CommitMask(null);
        foreach (var bound in _bounds) bound.ValueChanged += (_, _) => CommitMask(BoundsFromFields());
        _sliceOn.IsCheckedChanged += (_, _) => CommitSlice();
        foreach (var edge in _edges) edge.ValueChanged += (_, _) => CommitSlice();
        foreach (var tile in _tiles) tile.SelectionChanged += (_, _) => CommitSlice();
        Canvas.OriginChanged += (x, y) => _workspace?.Edit(new SpriteOriginCommand(SpriteName, x, y));
        Canvas.BoxChanged += box => CommitMask(box);
        Canvas.SliceChanged += (l, t, r, b) => _workspace?.Edit(new NineSliceCommand(SpriteName, true, new SliceEdges(l, t, r, b), Tiles()));
    }

    private string SpriteName => _sprite.Name.Content ?? "";

    private MaskBox BoundsFromFields() => new((int)(_bounds[0].Value ?? 0), (int)(_bounds[1].Value ?? 0), (int)(_bounds[2].Value ?? 0), (int)(_bounds[3].Value ?? 0));

    private int[] Tiles() => [.. _tiles.Select(t => Math.Max(0, t.SelectedIndex))];

    private void ApplyPreset()
    {
        if (_building || _workspace is null || _preset.SelectedIndex is < 0 or > 8) return;
        var (x, y) = SpriteMask.OriginPreset(_preset.SelectedIndex, (int)_sprite.Width, (int)_sprite.Height);
        if ((x, y) != (_sprite.OriginX, _sprite.OriginY)) _workspace.Edit(new SpriteOriginCommand(SpriteName, x, y));
    }

    // box: the box to keep as the manual one; null keeps the stored box, or finds it again in automatic and full mode.
    private void CommitMask(MaskBox? box)
    {
        if (_building || _workspace is null || _mode.SelectedIndex < 0 || _shape.SelectedIndex < 0) return;
        var mode = box is null ? (MaskMode)_mode.SelectedIndex : MaskMode.Manual;
        var shape = (MaskShape)_shape.SelectedIndex;
        _workspace.Edit(new SpriteMaskCommand(SpriteName, mode, shape, box ?? SpriteMaskCommand.BoxOf(_sprite), (int)(_tolerance.Value ?? 0)));
    }

    private void CommitSlice()
    {
        if (_building || _workspace is null || _sprite.SVersion < 3) return;
        _workspace.Edit(new NineSliceCommand(SpriteName, _sliceOn.IsChecked == true, new SliceEdges((int)(_edges[0].Value ?? 0), (int)(_edges[1].Value ?? 0), (int)(_edges[2].Value ?? 0), (int)(_edges[3].Value ?? 0)), Tiles()));
    }

    private void Show()
    {
        _building = true;
        var origin = (_sprite.OriginX, _sprite.OriginY);
        var match = Enumerable.Range(0, 9).Cast<int?>().FirstOrDefault(i => SpriteMask.OriginPreset(i ?? 0, (int)_sprite.Width, (int)_sprite.Height) == origin);
        _preset.SelectedIndex = match ?? 9;
        var box = SpriteMaskCommand.BoxOf(_sprite);
        _mode.SelectedIndex = (int)Math.Min(_sprite.BBoxMode, 2);
        // Ellipse and Diamond are stored as a precise mask, so a Precise reading must not undo the shape the user just chose.
        var shape = SpriteMaskCommand.ShapeOf(_sprite);
        if (!(shape == MaskShape.Precise && _shape.SelectedIndex is (int)MaskShape.Ellipse or (int)MaskShape.Diamond)) _shape.SelectedIndex = (int)shape;
        _frame.Maximum = Math.Max(0, _sprite.Textures.Count - 1);
        (_bounds[0].Value, _bounds[1].Value, _bounds[2].Value, _bounds[3].Value) = (box.Left, box.Top, box.Right, box.Bottom);
        foreach (var bound in _bounds) bound.IsEnabled = _workspace is not null && _sprite.BBoxMode == (uint)MaskMode.Manual;
        var slice = _sprite.V3NineSlice;
        var supported = _sprite.SVersion >= 3 && _workspace is not null;
        _sliceOn.IsEnabled = supported;
        _sliceOn.IsChecked = slice?.Enabled ?? false;
        (_edges[0].Value, _edges[1].Value, _edges[2].Value, _edges[3].Value) = (slice?.Left ?? 0, slice?.Top ?? 0, slice?.Right ?? 0, slice?.Bottom ?? 0);
        foreach (var (tile, i) in _tiles.Select((t, i) => (t, i))) tile.SelectedIndex = slice is null ? 0 : (int)slice.TileModes[i];
        foreach (var control in _edges.Cast<Control>().Concat(_tiles)) control.IsEnabled = supported;
        _building = false;
        Canvas.InvalidateVisual();
    }
}
