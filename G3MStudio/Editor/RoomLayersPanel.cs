using System.Globalization;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Avalonia.Controls.Primitives;
using G3MLib.DataFile.Models;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;
using G3MStudio.Theme;
using static G3MLib.DataFile.Models.GameMakerRoom;

namespace G3MStudio.Editor;

/// <summary>
/// The layers of a room, closest to the viewer first (lowest depth on top). Each row has a visibility check box; the buttons add an instance
/// layer, delete, move up or down (the depths of the two neighbours swap) and rename the selected layer. Every change is one undoable edit.
/// </summary>
public sealed class RoomLayersPanel : DockPanel
{
    private readonly GameMakerRoom _room;
    private readonly Workspace _workspace;
    private readonly ListBox _list = new() { Name = "RoomLayers", MinHeight = 80, MaxHeight = 170 };
    private readonly TextBox _name = new() { Name = "LayerName" };
    private readonly ComboBox _kind = new() { Name = "LayerKind", MinWidth = 100 };
    private readonly ComboBox _tileSet = new() { Name = "LayerTileSet", MinWidth = 100, IsVisible = false };
    private readonly ComboBox _effect = new() { Name = "LayerEffect", MinWidth = 100, IsVisible = false, ItemsSource = EffectTypes, SelectedIndex = 0 };
    private readonly Dictionary<string, NumericUpDown> _props = [];
    private static readonly DataFormat<string> LayerDrag = DataFormat.CreateStringApplicationFormat("g3mstudio.roomlayer");
    private static readonly string[] EffectTypes = ["_filter_blur", "_filter_tint", "_filter_vignette"];
    private Avalonia.Input.PointerPressedEventArgs? _press;
    private static readonly string[] Kinds = [RoomLayerCommand.AddInstances, RoomLayerCommand.AddAssets, RoomLayerCommand.AddBackground, RoomLayerCommand.AddTiles, RoomLayerCommand.AddPath, RoomLayerCommand.AddEffect, UiLayerCommand.AddUi];
    private static readonly (string Operation, string Label)[] Props =
    [
        (RoomLayerCommand.Depth, "rooms.layerProp.depth"), (RoomLayerCommand.XOffset, "rooms.layerProp.xoffset"), (RoomLayerCommand.YOffset, "rooms.layerProp.yoffset"),
        (RoomLayerCommand.HSpeed, "rooms.layerProp.hspeed"), (RoomLayerCommand.VSpeed, "rooms.layerProp.vspeed"),
    ];

    private bool _building;
    private readonly ContentControl _layerGrid = new() { Name = "LayerData", MaxHeight = 260 };

    public RoomLayersPanel(GameMakerRoom room, Workspace workspace)
    {
        (_room, _workspace) = (room, workspace);
        Name = "RoomLayersPanel";
        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
        var add = Tool("AddLayer", "rooms.addLayer", AddLayer);
        var delete = Tool("DeleteLayer", "rooms.deleteLayer", () => Edit(Selected is { } l ? [Command(RoomLayerCommand.Delete, l)] : []));
        var fit = Tool("FitTiles", "rooms.fitTiles", () => Edit(Selected is { LayerType: LayerType.Tiles } l ? [Command(RoomLayerCommand.FitTiles, l)] : []));
        var duplicate = Tool("DuplicateLayer", "roomEditor.duplicateLayer", Duplicate);
        var up = Tool("LayerUp", "rooms.layerUp", () => Move(-1));
        var down = Tool("LayerDown", "rooms.layerDown", () => Move(1));
        var rename = Tool("RenameLayer", "rooms.renameLayer", () => Edit(Selected is { } l && _name.Text is { Length: > 0 } text ? [Command(RoomLayerCommand.Rename, l, text)] : []));
        add.Flyout = KindGrid(add);
        _list.KeyDown += (_, e) =>
        {
            if (e.Key == Key.F2) FocusRename();
            else if (e.Key == Key.Delete && Selected is { } doomed) Edit([Command(RoomLayerCommand.Delete, doomed)]);
            else return;
            e.Handled = true;
        };
        _list.ContextMenu = new ContextMenu { ItemsSource = new[] { Item("roomEditor.renameLayerKey", FocusRename), Item("roomEditor.duplicateLayer", Duplicate), Item("rooms.deleteLayer", () => Edit(Selected is { } l ? [Command(RoomLayerCommand.Delete, l)] : [])) } };
        _kind.ItemsSource = Kinds.Select(k => Loc.Current["rooms.layerKind." + k]).ToList();
        _kind.SelectedIndex = 0;
        _kind.SelectionChanged += (_, _) =>
        {
            _tileSet.IsVisible = Kinds[Math.Max(0, _kind.SelectedIndex)] == RoomLayerCommand.AddTiles;
            _effect.IsVisible = Kinds[Math.Max(0, _kind.SelectedIndex)] == RoomLayerCommand.AddEffect;
        };
        _tileSet.ItemsSource = workspace.Data.Backgrounds.Where(b => b.GMS2TileWidth > 0 && b.GMS2TileCount > 0).Select(b => b.Name.Content).OfType<string>().ToList();
        _tileSet.SelectedIndex = _tileSet.ItemCount > 0 ? 0 : -1;
        var tools = new WrapPanel { ItemSpacing = 6, LineSpacing = 6, Children = { _kind, _tileSet, _effect, add, duplicate, delete, up, down, fit } };
        var properties = new StackPanel { Spacing = 2 };
        foreach (var (operation, label) in Props)
        {
            var box = new NumericUpDown { Name = "LayerProp_" + operation, FormatString = operation == RoomLayerCommand.Depth ? "0" : "0.###", Increment = operation == RoomLayerCommand.Depth ? 1 : 0.5m, MinWidth = 110 };
            box.ValueChanged += (_, e) =>
            {
                if (!_building && Selected is { } layer && e.NewValue is { } value) Edit([Command(operation, layer, value.ToString(CultureInfo.InvariantCulture))]);
            };
            _props[operation] = box;
            properties.Children.Add(new DockPanel { Children = { new TextBlock { Text = Loc.Current[label], MinWidth = 80, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center }, box } });
        }

        SetDock(properties, Avalonia.Controls.Dock.Bottom);
        SetDock(_layerGrid, Avalonia.Controls.Dock.Bottom);
        Children.Add(properties);
        Children.Add(_layerGrid);
        var nameBar = new DockPanel();
        SetDock(rename, Avalonia.Controls.Dock.Right);
        nameBar.Children.Add(rename);
        nameBar.Children.Add(_name);
        SetDock(tools, Avalonia.Controls.Dock.Top);
        SetDock(nameBar, Avalonia.Controls.Dock.Bottom);
        Children.Add(tools);
        Children.Add(nameBar);
        Children.Add(_list);
        _list.AddHandler(PointerPressedEvent, (_, e) => _press = e.GetCurrentPoint(_list).Properties.IsLeftButtonPressed && e.Source is Control { DataContext: not null } ? e : null, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        _list.PointerReleased += (_, _) => _press = null;
        _list.PointerMoved += async (_, e) =>
        {
            if (_press is not { } press || (e.GetPosition(_list) - press.GetPosition(_list)) is var d && d.X * d.X + d.Y * d.Y < 64) return;
            _press = null;
            if ((press.Source as Avalonia.Visual)?.FindAncestorOfType<ListBoxItem>(true)?.Tag is not Layer dragged) return;
            var item = new DataTransferItem();
            item.Set(LayerDrag, dragged.LayerId.ToString(CultureInfo.InvariantCulture));
            var data = new DataTransfer();
            data.Add(item);
            await DragDrop.DoDragDropAsync(press, data, DragDropEffects.Move);
        };
        DragDrop.SetAllowDrop(_list, true);
        _list.AddHandler(DragDrop.DragOverEvent, (_, e) => e.DragEffects = e.DataTransfer.TryGetValue(LayerDrag) is null ? DragDropEffects.None : DragDropEffects.Move);
        _list.AddHandler(DragDrop.DropEvent, (_, e) =>
        {
            if (e.DataTransfer.TryGetValue(LayerDrag) is { } id && (e.Source as Avalonia.Visual)?.FindAncestorOfType<ListBoxItem>(true)?.Tag is Layer target) DropLayer(uint.Parse(id, CultureInfo.InvariantCulture), target);
        });
        _list.SelectionChanged += (_, _) =>
        {
            if (_building) return;
            if (Selected is { } layer) _name.Text = layer.LayerName.Content;
            ShowProperties();
            SelectionChanged?.Invoke();
        };
        Follow(workspace);
        Refresh();
    }

    // A tab that is shown again gets its subscription back (a view leaves the visual tree when its tab is hidden).
    private void Follow(Workspace workspace)
    {
        workspace.History.Changed += Refresh;
        DetachedFromVisualTree += (_, _) => workspace.History.Changed -= Refresh;
        AttachedToVisualTree += (_, _) =>
        {
            workspace.History.Changed -= Refresh;
            workspace.History.Changed += Refresh;
            Refresh();
        };
    }

    public event Action? SelectionChanged;

    /// <summary>Layers locked against picking in the canvas. This is a view setting only; the file does not store it.</summary>
    public HashSet<uint> Locked { get; } = [];

    public event Action? LockChanged;

    public Layer? Selected => (_list.SelectedItem as ListBoxItem)?.Tag as Layer;

    public IReadOnlyList<Layer> Layers => [.. (_room.Layers ?? []).OrderBy(l => l.LayerDepth)];

    private static Button Tool(string name, string key, Action run)
    {
        var button = new Button { Name = name, Content = Loc.Current[key] };
        button.Click += (_, _) => run();
        return button;
    }

    private RoomLayerCommand Command(string operation, Layer layer, string text = "") => new(_room.Name.Content ?? "", operation, layer.LayerId, text);

    private void Edit(IEnumerable<EditCommand> commands)
    {
        var list = commands.ToList();
        if (list.Count > 0) _workspace.Edit(list.Count == 1 ? list[0] : new GroupCommand(list));
    }

    private static MenuItem Item(string key, Action run)
    {
        var item = new MenuItem { Header = Loc.Current[key] };
        item.Click += (_, _) => run();
        return item;
    }

    /// <summary>Moves the focus to the name box with its text selected, as F2 does.</summary>
    public void FocusRename()
    {
        _name.Focus();
        _name.SelectAll();
    }

    /// <summary>The Add Layer popup: one square per kind with a two letter mark and the kind's name; a click adds that kind.</summary>
    private Flyout KindGrid(Button owner)
    {
        var grid = new UniformGrid { Columns = 3 };
        for (var i = 0; i < Kinds.Length; i++)
        {
            var index = i;
            var label = Loc.Current["rooms.layerKind." + Kinds[i]];
            var tile = new Button { Name = "AddKind_" + Kinds[i], Width = 84, Height = 72, Content = new StackPanel { Children = { new TextBlock { Text = label[..Math.Min(2, label.Length)].ToUpperInvariant(), FontWeight = Avalonia.Media.FontWeight.Bold, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center }, new TextBlock { Text = label, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center }.TextSize("FontSize.Caption") } } };
            tile.Click += (_, _) =>
            {
                AddLayer(index);
                (owner.Flyout as Flyout)?.Hide();
            };
            grid.Children.Add(tile);
        }

        return new Flyout { Content = grid };
    }

    /// <summary>Adds a layer of the kind at this index of the kind list.</summary>
    public void AddLayer(int kindIndex)
    {
        _kind.SelectedIndex = Math.Clamp(kindIndex, 0, Kinds.Length - 1);
        AddLayer();
    }

    /// <summary>Adds a layer of the chosen kind named after it (Instances, Instances_1 and so on), in front of all others.</summary>
    public void AddLayer()
    {
        var kind = Kinds[Math.Max(0, _kind.SelectedIndex)];
        if (kind == UiLayerCommand.AddUi)
        {
            AddUiLayer();
            return;
        }

        var names = (_room.Layers ?? []).Select(l => l.LayerName.Content).ToHashSet();
        var baseName = kind[3..];
        var name = baseName;
        for (var i = 1; names.Contains(name); i++) name = baseName + "_" + i.ToString(CultureInfo.InvariantCulture);
        var front = Math.Max(-16000, (_room.Layers ?? []).Select(l => l.LayerDepth).DefaultIfEmpty(0).Min() - 100);
        if (kind == RoomLayerCommand.AddTiles && _tileSet.SelectedItem is not string) return;
        var extra = kind == RoomLayerCommand.AddEffect ? _effect.SelectedItem as string : _tileSet.SelectedItem as string;
        Edit([new RoomLayerCommand(_room.Name.Content ?? "", kind, 0, name, front, extra ?? "")]);
    }

    /// <summary>UI layers are shared by all rooms (the UILR chunk), so this adds one to the data file, not to the room; it is edited in its own tab.</summary>
    private void AddUiLayer()
    {
        var taken = _workspace.Data.FORM.UILR?.List.Select(r => UiLayerCommand.NameOf(r.Node)).ToHashSet() ?? [];
        var name = "UILayer";
        for (var i = 1; taken.Contains(name); i++) name = "UILayer_" + i.ToString(CultureInfo.InvariantCulture);
        _workspace.Edit(new UiLayerCommand(UiLayerCommand.AddLayer, "", name));
    }

    /// <summary>Copies the selected layer in front of the original, named after it with _copy.</summary>
    public void Duplicate()
    {
        if (Selected is not { } layer) return;
        var names = (_room.Layers ?? []).Select(l => l.LayerName.Content).ToHashSet();
        var name = (layer.LayerName.Content ?? "Layer") + "_copy";
        for (var i = 2; names.Contains(name); i++) name = (layer.LayerName.Content ?? "Layer") + "_copy" + i.ToString(CultureInfo.InvariantCulture);
        Edit([RoomLayerCommand.DuplicateOf(_room.Name.Content ?? "", layer.LayerId, name, Math.Max(-16000, layer.LayerDepth - 1))]);
    }

    /// <summary>Puts the dragged layer where the target row is: the depth changes come from <see cref="LayerOrder"/>, as one undo step.</summary>
    public void DropLayer(uint draggedId, Layer target)
    {
        var layers = Layers;
        var from = layers.ToList().FindIndex(l => l.LayerId == draggedId);
        var to = layers.ToList().IndexOf(target);
        var changes = LayerOrder.Move([.. layers.Select(l => (l.LayerId, l.LayerDepth))], from, to);
        Edit(changes.Select(c => Command(RoomLayerCommand.Depth, layers.First(l => l.LayerId == c.Id), c.Depth.ToString(CultureInfo.InvariantCulture))));
    }

    /// <summary>The data of a background or effect layer in a property list under the layer fields; the other layer types keep their own tools.</summary>
    private void ShowLayerData()
    {
        var index = Selected is { } layer ? (_room.Layers ?? []).IndexOf(layer) : -1;
        var member = Selected?.LayerType switch { LayerType.Background => "BackgroundData", LayerType.Effect => "EffectData", _ => null };
        _layerGrid.Content = index < 0 || member is null ? null : new PropertyGridTab(_workspace, "rooms", _room.Name.Content ?? "", null, $"Layers[{index}].{member}");
    }

    private void ShowProperties()
    {
        ShowLayerData();
        _building = true;
        foreach (var (operation, box) in _props)
        {
            box.IsEnabled = Selected is not null;
            if (box.Parent is Control row) row.IsVisible = Selected is not null; // nothing to edit without a layer: no empty boxes
            box.Value = Selected is not { } l ? null : (decimal)(operation switch
            {
                RoomLayerCommand.Depth => l.LayerDepth,
                RoomLayerCommand.XOffset => l.XOffset,
                RoomLayerCommand.YOffset => l.YOffset,
                RoomLayerCommand.HSpeed => l.HSpeed,
                _ => l.VSpeed,
            });
        }

        _building = false;
    }

    /// <summary>Swaps the depth of the selected layer with its neighbour (-1 up, +1 down). Equal depths are pulled apart.</summary>
    public void Move(int direction)
    {
        var layers = Layers.ToList();
        if (Selected is not { } layer) return;
        var at = layers.IndexOf(layer);
        var other = at + direction;
        if (other < 0 || other >= layers.Count) return;
        var (a, b) = (layers[at].LayerDepth, layers[other].LayerDepth);
        if (a == b) b = a + direction;
        Edit([Command(RoomLayerCommand.Depth, layers[at], b.ToString(CultureInfo.InvariantCulture)), Command(RoomLayerCommand.Depth, layers[other], a.ToString(CultureInfo.InvariantCulture))]);
    }

    private const string EyeIcon = "M12 5C7 5 2.7 8.1 1 12c1.7 3.9 6 7 11 7s9.300-3.100 11-7c-1.700-3.900-6-7-11-7zm0 11a4 4 0 110-8 4 4 0 010 8zm0-6a2 2 0 100 4 2 2 0 000-4z";
    private const string LockIcon = "M18 8h-1V6a5 5 0 00-10 0v2H6a2 2 0 00-2 2v10a2 2 0 002 2h12a2 2 0 002-2V10a2 2 0 00-2-2zM9 6a3 3 0 016 0v2H9V6z";

    private static ToggleButton IconToggle(string icon, string tip, bool on, Layer layer)
    {
        var button = new ToggleButton { IsChecked = on, Tag = layer, Padding = new Avalonia.Thickness(4), Background = Avalonia.Media.Brushes.Transparent, Content = new PathIcon { Data = Avalonia.Media.Geometry.Parse(icon) } };
        ToolTip.SetTip(button, Loc.Current[tip]);
        return button;
    }

    private void Refresh()
    {
        _building = true;
        var selectedId = Selected?.LayerId;
        _list.ItemsSource = Layers.Select(layer =>
        {
            var visible = IconToggle(EyeIcon, "rooms.show", layer.IsVisible, layer);
            visible.IsCheckedChanged += (_, _) =>
            {
                if (!_building && visible.IsChecked != layer.IsVisible) Edit([Command(RoomLayerCommand.Visible, layer, visible.IsChecked == true ? "1" : "0")]);
            };
            var locked = IconToggle(LockIcon, "rooms.lock", Locked.Contains(layer.LayerId), layer);
            locked.IsCheckedChanged += (_, _) =>
            {
                if (_building || (locked.IsChecked == true) == Locked.Contains(layer.LayerId)) return;
                if (locked.IsChecked == true) Locked.Add(layer.LayerId);
                else Locked.Remove(layer.LayerId);
                LockChanged?.Invoke();
            };
            var row = new DockPanel { Children = { visible, locked, new TextBlock { Text = layer.LayerName.Content, Margin = new Avalonia.Thickness(6, 0), VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center } } };
            DockPanel.SetDock(visible, Avalonia.Controls.Dock.Left);
            DockPanel.SetDock(locked, Avalonia.Controls.Dock.Right);
            var item = new ListBoxItem { Content = row, Tag = layer, Padding = new Avalonia.Thickness(4, 0) }.BindSize(MinHeightProperty, "Control.Height");
            ToolTip.SetTip(item, layer.LayerType.ToString());
            return item;
        }).ToList();
        _list.SelectedItem = _list.Items.OfType<ListBoxItem>().FirstOrDefault(i => (i.Tag as Layer)?.LayerId == selectedId);
        _building = false;
        ShowProperties();
    }
}
