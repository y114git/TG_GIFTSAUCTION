using System.Globalization;
using Avalonia.Controls;
using G3MLib.DataFile.Models;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;
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
    private readonly Dictionary<string, NumericUpDown> _props = [];
    private static readonly string[] Kinds = [RoomLayerCommand.AddInstances, RoomLayerCommand.AddAssets, RoomLayerCommand.AddBackground, RoomLayerCommand.AddTiles];
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
        var up = Tool("LayerUp", "rooms.layerUp", () => Move(-1));
        var down = Tool("LayerDown", "rooms.layerDown", () => Move(1));
        var rename = Tool("RenameLayer", "rooms.renameLayer", () => Edit(Selected is { } l && _name.Text is { Length: > 0 } text ? [Command(RoomLayerCommand.Rename, l, text)] : []));
        _kind.ItemsSource = Kinds.Select(k => Loc.Current["rooms.layerKind." + k]).ToList();
        _kind.SelectedIndex = 0;
        _kind.SelectionChanged += (_, _) => _tileSet.IsVisible = Kinds[Math.Max(0, _kind.SelectedIndex)] == RoomLayerCommand.AddTiles;
        _tileSet.ItemsSource = workspace.Data.Backgrounds.Where(b => b.GMS2TileWidth > 0 && b.GMS2TileCount > 0).Select(b => b.Name.Content).OfType<string>().ToList();
        _tileSet.SelectedIndex = _tileSet.ItemCount > 0 ? 0 : -1;
        var tools = new WrapPanel { Children = { _kind, _tileSet, add, delete, up, down, fit } };
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
        _list.SelectionChanged += (_, _) =>
        {
            if (_building) return;
            if (Selected is { } layer) _name.Text = layer.LayerName.Content;
            ShowProperties();
            SelectionChanged?.Invoke();
        };
        workspace.History.Changed += Refresh;
        DetachedFromVisualTree += (_, _) => workspace.History.Changed -= Refresh;
        Refresh();
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

    /// <summary>Adds a layer of the chosen kind named after it (Instances, Instances_1 and so on), in front of all others.</summary>
    public void AddLayer()
    {
        var kind = Kinds[Math.Max(0, _kind.SelectedIndex)];
        var names = (_room.Layers ?? []).Select(l => l.LayerName.Content).ToHashSet();
        var baseName = kind[3..];
        var name = baseName;
        for (var i = 1; names.Contains(name); i++) name = baseName + "_" + i.ToString(CultureInfo.InvariantCulture);
        var front = Math.Max(-16000, (_room.Layers ?? []).Select(l => l.LayerDepth).DefaultIfEmpty(0).Min() - 100);
        if (kind == RoomLayerCommand.AddTiles && _tileSet.SelectedItem is not string) return;
        Edit([new RoomLayerCommand(_room.Name.Content ?? "", kind, 0, name, front, _tileSet.SelectedItem as string ?? "")]);
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

    private void Refresh()
    {
        _building = true;
        var selectedId = Selected?.LayerId;
        _list.ItemsSource = Layers.Select(layer =>
        {
            var visible = new CheckBox { IsChecked = layer.IsVisible, Tag = layer };
            visible.IsCheckedChanged += (_, _) =>
            {
                if (!_building && visible.IsChecked != layer.IsVisible) Edit([Command(RoomLayerCommand.Visible, layer, visible.IsChecked == true ? "1" : "0")]);
            };
            var locked = new CheckBox { Content = Loc.Current["rooms.lock"], IsChecked = Locked.Contains(layer.LayerId), Tag = layer };
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
            var item = new ListBoxItem { Content = row, Tag = layer, MinHeight = 28, Padding = new Avalonia.Thickness(4, 0) };
            ToolTip.SetTip(item, layer.LayerType.ToString());
            return item;
        }).ToList();
        _list.SelectedItem = _list.Items.OfType<ListBoxItem>().FirstOrDefault(i => (i.Tag as Layer)?.LayerId == selectedId);
        _building = false;
        ShowProperties();
    }
}
