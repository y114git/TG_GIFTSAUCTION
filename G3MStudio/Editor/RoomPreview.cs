using G3MStudio.Core.Commands;
using Avalonia;
using Avalonia.Controls;
using G3MLib.DataFile.Models;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;
using G3MStudio.Rooms;

namespace G3MStudio.Editor;

/// <summary>
/// A room drawn above its properties: tiles and instances with their sprites. Instances can be selected (also several at once), dragged,
/// nudged, flipped, copied, pasted, deleted and added; each gesture is one undoable edit and the picture is rebuilt from the model afterwards.
/// The pages come in off the UI thread.
/// </summary>
public sealed partial class RoomPreview : DockPanel
{
    private readonly GameMakerRoom _room;
    private readonly ContentControl _elementHost = new() { Name = "RoomElementProps", MaxHeight = 300 };
    private readonly TextureBitmaps _textures;
    private readonly Workspace? _workspace;
    private List<(string Object, int X, int Y, float ScaleX, float ScaleY, float Rotation)> _clipboard = [];
    private readonly TextBlock _placeHint = new() { Name = "RoomPlaceHint", Classes = { "muted" }, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, Margin = new Thickness(8, 0), IsVisible = false };
    private readonly ComboBox _tool = new() { Name = "RoomTool", MinWidth = 120 };
    private readonly Border _paletteHost = new() { Name = "TilePaletteHost", IsVisible = false };
    private TilePalette? _palette;
    private readonly InstanceInspector? _inspector;
    private readonly RoomElementList _elements;
    private readonly RoomLayersPanel? _layers;
    private bool _syncing;
    private bool _fitted;
    private uint? _shownLayer;

    public RoomPreview(GameMakerRoom room, TextureBitmaps textures, Workspace? workspace = null, Action? creationOrder = null, Action? creationCode = null, Action<GameMakerRoom.GameObject>? instanceCode = null)
    {
        (_room, _textures, _workspace, _instanceCode) = (room, textures, workspace, instanceCode);
        Name = "RoomPreview";
        _elements = new RoomElementList(room);
        MinHeight = 200;
        void ApplyGrid()
        {
            (View.GridWidth, View.GridHeight) = RoomOptions.GridFor(room.GridWidth, room.GridHeight);
            SyncGridBoxes();
        }

        ApplyGrid();
        AttachedToVisualTree += (_, _) =>
        {
            RoomOptions.Changed += ApplyGrid;
            if (RoomOptions.GridOverride || !_gridEdited) ApplyGrid();
        };
        DetachedFromVisualTree += (_, _) => RoomOptions.Changed -= ApplyGrid;
        ShowCameras();
        View.ShowViews = View.Views.Count > 0;
        ClipToBounds = true;
        if (workspace is not null)
        {
            _tool.ItemsSource = Enum.GetValues<RoomTool>().Select(t => Loc.Current["rooms.tool." + t]).ToList();
            _tool.SelectedIndex = 0;
            _tool.SelectionChanged += (_, _) =>
            {
                View.Tool = (RoomTool)Math.Max(0, _tool.SelectedIndex);
                RefreshPlaceTarget();
            };
            View.TileKeyPressed += TileKey;
            View.PlaceRequested += at =>
            {
                if (PlaceableSource() is { } key) Place(key, at);
            };
            var bar = new WrapPanel { ItemSpacing = 6, LineSpacing = 4, Children = { _tool, EditButtons(), ViewButtons(), _placeHint } };
            SetDock(bar, Avalonia.Controls.Dock.Top);
            Children.Add(bar);
            if (room.Layers is not null)
            {
                var layers = _layers = new RoomLayersPanel(room, workspace);
                layers.LockChanged += ApplyLocks;
                _inspector = new InstanceInspector(workspace, RoomName, instanceCode);
                SidePanel = new StackPanel { Name = "RoomSidePanel", Spacing = 8, Children = { layers, _inspector, _elementHost, _paletteHost } };
                if (creationOrder is not null && creationCode is not null) ((StackPanel)SidePanel).Children.Insert(1, CodeButtons(creationOrder, creationCode));

                View.SelectionChanged += () =>
                {
                    _inspector.Show([.. View.Selection.OfType<GameMakerRoom.GameObject>()]);
                    ShowElementProperties();
                };
                layers.SelectionChanged += () =>
                {
                    ChooseTileLayer(layers.Selected);
                    _shownLayer = layers.Selected?.LayerId;
                    _elements.Show(layers.Selected);
                };
                var search = new TextBox { Name = "RoomElementSearch", PlaceholderText = Loc.Current["rooms.elementSearch"] };
                search.TextChanged += (_, _) => _elements.Filter = search.Text ?? "";
                var listPanel = new DockPanel { Children = { search, _elements } };
                DockPanel.SetDock(search, Avalonia.Controls.Dock.Top);
                SetDock(listPanel, Avalonia.Controls.Dock.Bottom);
                Children.Add(listPanel);
                LinkElementList();
            }

            WireEdits(workspace);
        }

        AddCanvas();
        Ready = BuildAsync();
    }

    private static WrapPanel CodeButtons(Action creationOrder, Action creationCode)
    {
        Button Command(string name, string id, Action run)
        {
            var button = new Button { Name = name, Content = Loc.Current[AppCommands.TitleKey(id)] };
            button.Click += (_, _) => run();
            return button;
        }

        return new WrapPanel { ItemSpacing = 6, LineSpacing = 6, Children = { Command("RoomCreationCode", "room.creationCode", creationCode), Command("RoomCreationOrder", "room.creationOrder", creationOrder) } };
    }

    /// <summary>Keeps the element list under the canvas and the canvas selection in step, each change guarded against echoing back.</summary>
    private void LinkElementList()
    {
        _elements.PickedChanged += picked =>
        {
            if (_syncing) return;
            _syncing = true;
            View.Select(picked);
            _syncing = false;
        };
        View.SelectionChanged += () =>
        {
            if (_syncing) return;
            _syncing = true;
            _elements.Mark(View.Selection);
            _syncing = false;
        };
        _elements.Show(null);
    }

    private void WireEdits(Workspace workspace)
    {
        View.CameraEdited += (slot, rect) => Edit([new GroupCommand([.. new (string Path, double Value)[] { ("ViewX", rect.X), ("ViewY", rect.Y), ("ViewWidth", rect.Width), ("ViewHeight", rect.Height) }
            .Select(c => new SetPropertyCommand("rooms", RoomName, $"Views[{slot}].{c.Path}", ((int)c.Value).ToString(System.Globalization.CultureInfo.InvariantCulture)))])]);
        workspace.History.Changed += ShowCameras; // an undo or redo puts the outlines back
        View.Moved += (elements, dx, dy) => Edit(elements.Select(e => ChangeOf(e, RoomInstanceCommand.Move, RoomElementCommand.Move, dx, dy)).OfType<EditCommand>());
        View.DeleteRequested += elements => Edit(elements.Select(e => ChangeOf(e, RoomInstanceCommand.Delete, RoomElementCommand.Delete, 0, 0)).OfType<EditCommand>());
        View.FlipRequested += (elements, horizontal) => Edit(elements.OfType<GameMakerRoom.GameObject>().Select(i => new RoomInstanceCommand(RoomName, horizontal ? RoomInstanceCommand.FlipX : RoomInstanceCommand.FlipY, i.InstanceID)));
        View.ResourceDropped += Place;
        View.CopyRequested += Copy;
        View.PasteRequested += Paste;
        View.CopiesPainted += PaintCopies;
        View.TilesPainted += Paint;
        WireRoomEditing();
        workspace.History.Changed += OnHistoryChanged;
        _seenVersion = workspace.History.Version;
        DetachedFromVisualTree += (_, _) => workspace.History.Changed -= OnHistoryChanged;
        AttachedToVisualTree += (_, _) =>
        {
            workspace.History.Changed -= OnHistoryChanged;
            workspace.History.Changed += OnHistoryChanged;
            if (_seenVersion != workspace.History.Version) OnHistoryChanged();
        };
    }

    private void AddCanvas()
    {
        var hint = new TextBlock { Name = "RoomHint", Classes = { "muted" } };
        SetDock(hint, Avalonia.Controls.Dock.Bottom);
        Children.Add(hint);
        View.Hovered += (at, snapped, element) => hint.Text = string.Create(System.Globalization.CultureInfo.InvariantCulture, $"x {(int)at.X}, y {(int)at.Y}   {Loc.Current["rooms.snapped"]} {(int)snapped.X}, {(int)snapped.Y}")
            + (element is null ? "" : "   " + RoomElementList.Describe(element));
        var canvas = new Grid { RowDefinitions = new RowDefinitions("Auto,*"), ColumnDefinitions = new ColumnDefinitions("Auto,*") };
        var top = new RoomRuler(View, Avalonia.Layout.Orientation.Horizontal);
        var left = new RoomRuler(View, Avalonia.Layout.Orientation.Vertical);
        Grid.SetColumn(top, 1);
        Grid.SetRow(left, 1);
        Grid.SetRow(View, 1);
        Grid.SetColumn(View, 1);
        canvas.Children.Add(top);
        canvas.Children.Add(left);
        canvas.Children.Add(View);
        Children.Add(canvas);
    }

    public RoomView View { get; } = new();

    /// <summary>The layers, the selected layer's fields, the instance fields and the tile palette. The window shows it in the Inspector panel while this room is in front, so the canvas keeps the whole tab.</summary>
    public Control? SidePanel { get; private set; }

    /// <summary>The zoom and the top-left room position of the canvas, to be restored with <see cref="RestoreViewAsync"/>.</summary>
    public double[] ViewState => [View.Zoom, View.Offset.X, View.Offset.Y, View.SnapToGrid ? 1 : 0, View.ShowGrid ? 1 : 0, View.GridWidth, View.GridHeight];

    /// <summary>Puts a saved zoom and position back once the scene is drawn.</summary>
    public async Task RestoreViewAsync(double[] state)
    {
        if (state is not [var zoom, var x, var y, ..]) return;
        await Ready;
        (View.Zoom, View.Offset) = (zoom, new Point(x, y));
        if (state is not [_, _, _, var snap, var grid, var gridW, var gridH]) return;
        (View.SnapToGrid, View.ShowGrid) = (snap > 0, grid > 0);
        if (RoomOptions.GridOverride || gridW <= 0 || gridH <= 0) return;
        (View.GridWidth, View.GridHeight, _gridEdited) = (gridW, gridH, true);
        SyncGridBoxes();
    }

    /// <summary>Completes when the scene is drawn (or could not be built).</summary>
    public Task Ready { get; private set; }

    private string RoomName => _room.Name.Content ?? "";

    /// <summary>A single edit goes in as it is; several become one undo step.</summary>
    /// <summary>A single selected asset-layer element (a sprite, sequence, particle system or text item) gets all its fields in a property list, so a text item's text, font and colour can be edited.</summary>
    private void ShowElementProperties()
    {
        _elementHost.Content = null;
        if (_workspace is null || View.Selection.Count != 1 || View.Selection.First() is not { } element || RoomElements.KindOf(element) is not { } kind) return;
        var layers = _room.Layers ?? [];
        var layer = RoomElements.LayerOf(_room, element);
        var list = layer is null ? null : RoomElements.ListOf(layer, kind);
        var member = kind switch { RoomElements.Sprite => "Sprites", RoomElements.Sequence => "Sequences", RoomElements.Particle => "ParticleSystems", _ => "TextItems" };
        if (layer is null || list is null) return;
        _elementHost.Content = new PropertyGridTab(_workspace, "rooms", RoomName, null, $"Layers[{layers.IndexOf(layer)}].AssetsData.{member}[{list.IndexOf(element)}]");
    }

    private void ShowCameras()
    {
        var enabled = _room.Views.Select((v, i) => (v, i)).Where(p => p.v.Enabled).ToList();
        View.ViewSlots = [.. enabled.Select(p => p.i)];
        View.Views = [.. enabled.Select(p => new Avalonia.Rect(p.v.ViewX, p.v.ViewY, p.v.ViewWidth, p.v.ViewHeight))];
    }

    private void Edit(IEnumerable<EditCommand> commands)
    {
        var list = commands.ToList();
        if (list.Count == 0) return;
        _workspace?.Edit(list.Count == 1 ? list[0] : new GroupCommand(list));
    }

    /// <summary>The grid, snap and view switches and the zoom buttons: what the keys G, Shift+G, V, Ctrl+plus, Ctrl+minus and Ctrl+0 do.</summary>
    private StackPanel ViewButtons()
    {
        Avalonia.Controls.Primitives.ToggleButton Switch(string name, string key, string shortcut, Func<bool> get, Action<bool> set)
        {
            var button = new Avalonia.Controls.Primitives.ToggleButton { Name = name, Content = Loc.Current[key], IsChecked = get() };
            ToolTip.SetTip(button, Loc.Current[key] + " (" + shortcut + ")");
            button.IsCheckedChanged += (_, _) =>
            {
                if (button.IsChecked != get()) set(button.IsChecked == true);
            };
            View.OptionsChanged += () => button.IsChecked = get();
            return button;
        }

        Button Action(string name, string text, string tip, Action run)
        {
            var button = new Button { Name = name, Content = text };
            ToolTip.SetTip(button, tip);
            button.Click += (_, _) => run();
            return button;
        }

        return new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Spacing = 4,
            Margin = new Thickness(8, 0),
            Children =
            {
                Switch("RoomGrid", "rooms.grid", "G", () => View.ShowGrid, on => View.ShowGrid = on),
                Switch("RoomSnap", "rooms.snap", "Shift+G", () => View.SnapToGrid, on => View.SnapToGrid = on),
                Switch("RoomViews", "rooms.views", "V", () => View.ShowViews, on => View.ShowViews = on),
                Action("RoomZoomOut", "−", Loc.Current["rooms.zoomOut"] + " (Ctrl+−)", () => View.Zoom /= 1.25),
                Action("RoomZoomIn", "+", Loc.Current["rooms.zoomIn"] + " (Ctrl++)", () => View.Zoom *= 1.25),
                Action("RoomFit", Loc.Current["rooms.fit"], Loc.Current["rooms.fit"] + " (Ctrl+0)", View.FitRoom),
                Action("RoomActual", "100%", Loc.Current["rooms.actual"], () => View.Zoom = 1),
            },
        };
    }

    /// <summary>Gives the Place tool the resource to place: the key of the object or sprite selected in the Explorer, or null.</summary>
    public Func<string?>? PlaceSource { get; set; }

    private string? PlaceableSource() => PlaceSource?.Invoke() is { } key && ResourceTypes.SplitKey(key) is (("objects" or "sprites"), _) ? key : null;

    /// <summary>Shows what the Place tool would place now, or says to select an object or a sprite first.</summary>
    public void RefreshPlaceTarget()
    {
        _placeHint.IsVisible = View.Tool == RoomTool.Place;
        _placeHint.Text = PlaceableSource() is { } key && ResourceTypes.SplitKey(key) is var (_, name) ? Loc.Current.Format("rooms.placing", ("name", name)) : Loc.Current["rooms.placeNone"];
    }

    /// <summary>Adds an instance of an object, or a sprite element of a sprite, at a room position.</summary>
    private void Place(string key, Point at)
    {
        if (ResourceTypes.SplitKey(key) is not var (type, name)) return;
        if (type == "objects") Edit([new RoomInstanceCommand(RoomName, RoomInstanceCommand.Add, 0, (int)at.X, (int)at.Y, name)]);
        else if (type == "sprites") AddSpriteElement(name, at);
    }

    /// <summary>Pastes the copied instances so the first one lands on <paramref name="at"/> and the others keep their distance from it.</summary>
    public void Paste(Point at)
    {
        if (_clipboard.Count == 0) return;
        var (ax, ay) = (_clipboard[0].X, _clipboard[0].Y);
        Edit(_clipboard.Select(c => new RoomInstanceCommand(RoomName, RoomInstanceCommand.Add, 0, (int)at.X + c.X - ax, (int)at.Y + c.Y - ay, c.Object) { ScaleX = c.ScaleX, ScaleY = c.ScaleY, Rotation = c.Rotation }));
    }

    /// <summary>One copy of the instance at each place, as one undo step.</summary>
    public void PaintCopies(GameMakerRoom.GameObject source, IReadOnlyList<Point> places)
    {
        if (source.ObjectDefinition?.Name.Content is not { } name) return;
        Edit(places.Select(p => new RoomInstanceCommand(RoomName, RoomInstanceCommand.Add, 0, (int)p.X, (int)p.Y, name) { ScaleX = source.ScaleX, ScaleY = source.ScaleY, Rotation = source.Rotation }));
    }

    /// <summary>Remembers the instances (object, position, scale) for a later paste.</summary>
    public void Copy(IReadOnlyList<object> elements)
    {
        _clipboard = [];
        foreach (var i in elements.OfType<GameMakerRoom.GameObject>())
            if (i.ObjectDefinition?.Name.Content is { } name) _clipboard.Add((name, i.X, i.Y, i.ScaleX, i.ScaleY, i.Rotation));
    }

    /// <summary>Shows the tile set of the chosen layer as the palette and makes it the layer that painting changes; other layers leave painting off.</summary>
    /// <summary>The mirror (1), flip (2) and quarter turn (4) flags put on painted tiles; 0 without a tile layer.</summary>
    public int TileFlags => _palette?.Flags ?? 0;

    public void ChooseTileLayer(GameMakerRoom.Layer? layer)
    {
        View.TileLayer = layer?.TilesData?.Background is not null ? layer : null;
        _palette = layer?.TilesData?.Background is { } set ? new TilePalette(set, _textures) : null;
        _paletteHost.Child = _palette is null ? null : new StackPanel
        {
            Spacing = 4,
            Children = { FlagBox(_palette, 1, "rooms.flagMirror"), FlagBox(_palette, 2, "rooms.flagFlip"), FlagBox(_palette, 4, "rooms.flagRotate"), _palette },
        };
        _paletteHost.IsVisible = _palette is not null;
        _tileBar.IsVisible = View.TileLayer is not null;
        RefreshRules(RoomOptions.AutoTile.StartOn && TileSetName is { } name ? _autoTiles?.At(name, 0)?.Name : null);
    }

    private static CheckBox FlagBox(TilePalette palette, int bit, string key)
    {
        var box = new CheckBox { Content = Loc.Current[key] };
        box.IsCheckedChanged += (_, _) => palette.Flags = box.IsChecked == true ? palette.Flags | bit : palette.Flags & ~bit;
        palette.FlagsChanged += () => box.IsChecked = (palette.Flags & bit) != 0;
        return box;
    }

    /// <summary>Keys of the tile editor: B brush, E erase, G fill, R rectangle, P pick; X, Y and Z turn the mirror, flip and quarter turn flags of the tiles painted on or off.</summary>
    private bool TileKey(Avalonia.Input.Key key)
    {
        if (_palette is null) return false;
        RoomTool? tool = key switch { Avalonia.Input.Key.B => RoomTool.Paint, Avalonia.Input.Key.E => RoomTool.Erase, Avalonia.Input.Key.G => RoomTool.Fill, Avalonia.Input.Key.R => RoomTool.Rectangle, Avalonia.Input.Key.P => RoomTool.Pick, Avalonia.Input.Key.L => RoomTool.Line, Avalonia.Input.Key.S => RoomTool.Select, _ => null };
        if (tool is { } chosen)
        {
            _tool.SelectedIndex = (int)chosen;
            return true;
        }

        var bit = key switch { Avalonia.Input.Key.X => 1, Avalonia.Input.Key.Y => 2, Avalonia.Input.Key.Z => 4, _ => 0 };
        if (bit == 0) return false;
        _palette.Flags ^= bit;
        return true;
    }

    /// <summary>Turns a finished stroke into one edit: paint sets the chosen tile, erase empties the cells, fill replaces the connected area.</summary>
    public void Paint(GameMakerRoom.Layer layer, RoomTool tool, IReadOnlyList<(int X, int Y)> cells)
    {
        if (tool == RoomTool.Pick && layer.TilesData is { TileData: { } source } && cells.Count > 0 && _palette is not null)
        {
            var picked = source[cells[0].Y][cells[0].X];
            _palette.Select((int)(picked & 0x0007FFFF));
            _palette.Flags = (int)((picked >> 28) & 7);
            return;
        }

        if (tool == RoomTool.Select)
        {
            SelectTiles(cells);
            return;
        }

        cells = tool is RoomTool.Paint or RoomTool.Erase or RoomTool.Line ? Brushed(layer, cells) : cells;
        if (AutoChanges(layer, tool, cells) is { } auto)
        {
            CommitTiles(layer, auto);
            return;
        }

        var value = tool == RoomTool.Erase ? 0u : _palette?.TileValue ?? 0;
        List<(int X, int Y, uint Value)> changes = tool == RoomTool.Fill && layer.TilesData is { TileData: { } rows } && cells.Count > 0
            ? RoomTilesCommand.FloodFill(rows, cells[0].X, cells[0].Y, value)
            : [.. cells.Select(c => (c.X, c.Y, value))];
        if (changes.Count > 0) _workspace?.Edit(new RoomTilesCommand(RoomName, layer.LayerId, changes));
    }

    /// <summary>The edit that moves (by dx, dy) or deletes one element: an instance by its id, an asset-layer element by layer, kind and name. Null for what has no edit. An old-style tile is found by its instance id.</summary>
    private EditCommand? ChangeOf(object element, string instanceOperation, string elementOperation, int dx, int dy)
    {
        if (element is GameMakerRoom.GameObject instance)
            return new RoomInstanceCommand(RoomName, instanceOperation, instance.InstanceID, instance.X + dx, instance.Y + dy);
        if (element is GameMakerRoom.Tile tile)
            return new RoomTileCommand(RoomName, instanceOperation == RoomInstanceCommand.Delete ? RoomTileCommand.Delete : RoomTileCommand.Move, tile.InstanceID, tile.X + dx, tile.Y + dy);
        if (RoomElements.LayerOf(_room, element) is { } layer && RoomElements.KindOf(element) is { } kind && RoomElements.NameOf(element) is { } name)
            return new RoomElementCommand(new ElementRef(RoomName, layer.LayerId, kind, name), elementOperation, RoomElements.X(element) + dx, RoomElements.Y(element) + dy);
        return null;
    }

    /// <summary>Adds a sprite element of <paramref name="sprite"/> at <paramref name="at"/> on the first assets layer.</summary>
    private void AddSpriteElement(string sprite, Point at)
    {
        if ((_room.Layers ?? []).FirstOrDefault(l => l.AssetsData is not null) is not { } layer) return;
        var names = RoomElements.ListOf(layer, RoomElements.Sprite)?.Cast<object>().Select(RoomElements.NameOf).ToHashSet() ?? [];
        var name = "graphic_1";
        for (var i = 2; names.Contains(name); i++) name = "graphic_" + i.ToString(System.Globalization.CultureInfo.InvariantCulture);
        Edit([new RoomElementCommand(new ElementRef(RoomName, layer.LayerId, RoomElements.Sprite, name), RoomElementCommand.Add, (int)at.X, (int)at.Y, sprite)]);
    }

    private void ApplyLocks()
    {
        if (_layers is null) return;
        View.Locked.Clear();
        View.Locked.UnionWith((_room.Layers ?? []).Where(l => _layers.Locked.Contains(l.LayerId)).SelectMany(RoomElementList.Of));
        if (View.Selection.Any(View.Locked.Contains)) View.Select([.. View.Selection.Where(e => !View.Locked.Contains(e))]);
    }

    private int _seenVersion;

    private void OnHistoryChanged()
    {
        _seenVersion = _workspace?.History.Version ?? 0;
        ApplyLocks();
        _elements.Show((_room.Layers ?? []).FirstOrDefault(l => l.LayerId == _shownLayer));
        Ready = BuildAsync();
    }

    private async Task BuildAsync()
    {
        try
        {
            var selectedIds = View.Selection.OfType<GameMakerRoom.GameObject>().Select(i => i.InstanceID).ToHashSet();
            var selectedAssets = View.Selection.Where(e => RoomElements.KindOf(e) is not null).Select(e => (RoomElements.KindOf(e), RoomElements.NameOf(e))).ToHashSet();
            var (offset, zoom) = (View.Offset, View.Zoom);
            View.Scene = await RoomScene.BuildAsync(_room, _textures);
            (View.Zoom, View.Offset) = (zoom, offset);
            // The first time the room is shown it is fitted to the canvas, like GameMaker does; a saved view put back afterwards replaces that.
            if (!_fitted)
            {
                _fitted = true;
                Avalonia.Threading.Dispatcher.UIThread.Post(View.FitRoom, Avalonia.Threading.DispatcherPriority.Loaded);
            }

            var assets = (_room.Layers ?? []).Where(l => l.AssetsData is not null).SelectMany(l => new[] { RoomElements.Sprite, RoomElements.Sequence, RoomElements.Particle, RoomElements.Text }.SelectMany(k => RoomElements.ListOf(l, k)?.Cast<object>() ?? []));
            selectedIds = _pendingSelect ?? selectedIds;
            _pendingSelect = null;
            View.Select(RoomInstanceCommand.Lists(_room).SelectMany(l => l).Where(i => selectedIds.Contains(i.InstanceID)).DistinctBy(i => i.InstanceID).Cast<object>()
                .Concat(assets.Where(e => selectedAssets.Contains((RoomElements.KindOf(e), RoomElements.NameOf(e))))));
        }
        catch (Exception e) when (e is InvalidOperationException or NullReferenceException or ArgumentException)
        {
            // A room the preview cannot read still has its properties below; the picture just stays black.
            View.Scene = null;
        }
    }
}
