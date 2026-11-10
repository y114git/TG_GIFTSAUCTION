using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;
using G3MStudio.Rooms;
using static G3MLib.DataFile.Models.GameMakerRoom;

namespace G3MStudio.Editor;

/// <summary>Editing commands of the room canvas that act on a selection: transform, align, duplicate, tile blocks, the context menu and the hover tip.</summary>
public sealed partial class RoomPreview
{
    private readonly Action<GameObject>? _instanceCode;
    private readonly Button _alignButton = new() { Name = "RoomAlign", IsEnabled = false };
    private readonly StackPanel _tileBar = new() { Name = "RoomTileBar", Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 4, IsVisible = false };
    private readonly NumericUpDown _brush = new() { Name = "RoomBrushSize", Minimum = 1, Maximum = 8, Value = 1, FormatString = "0", MinWidth = 90 };
    private readonly NumericUpDown _gridW = new() { Name = "RoomGridWidth", Minimum = 1, Maximum = 4096, FormatString = "0", MinWidth = 90 };
    private readonly NumericUpDown _gridH = new() { Name = "RoomGridHeight", Minimum = 1, Maximum = 4096, FormatString = "0", MinWidth = 90 };
    private TileBlock? _tileClip;
    private HashSet<uint>? _pendingSelect;
    private bool _gridEdited;
    private bool _gridSyncing;
    private object? _tipFor;

    /// <summary>The align button, the brush size, the tile menu and the grid size boxes of the tool bar.</summary>
    private StackPanel EditButtons()
    {
        _alignButton.Content = Loc.Current["roomEditor.alignGroup"];
        _alignButton.Click += (_, _) => new ContextMenu { ItemsSource = AlignItems() }.Open(_alignButton);
        ToolTip.SetTip(_brush, Loc.Current["roomEditor.brushSize"]);
        _brush.ValueChanged += (_, e) => View.BrushSize = (int)(e.NewValue ?? 1);
        var tiles = new Button { Name = "RoomTileMenu", Content = Loc.Current["rooms.tool.Select"] };
        tiles.Click += (_, _) => new ContextMenu { ItemsSource = TileItems() }.Open(tiles);
        _tileBar.Children.Add(_brush);
        _tileBar.Children.Add(_rules);
        _tileBar.Children.Add(tiles);
        foreach (var (box, key) in new[] { (_gridW, "roomEditor.gridW"), (_gridH, "roomEditor.gridH") })
        {
            ToolTip.SetTip(box, Loc.Current[key]);
            box.ValueChanged += (_, _) => GridBoxChanged();
        }

        SyncGridBoxes();
        return new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 4, Margin = new Thickness(8, 0), Children = { _alignButton, _tileBar, _gridW, _gridH } };
    }

    private void SyncGridBoxes()
    {
        _gridSyncing = true;
        (_gridW.Value, _gridH.Value) = ((decimal)View.GridWidth, (decimal)View.GridHeight);
        _gridSyncing = false;
    }

    private void GridBoxChanged()
    {
        if (_gridSyncing || _gridW.Value is not { } w || _gridH.Value is not { } h) return;
        (View.GridWidth, View.GridHeight, _gridEdited) = ((double)w, (double)h, true);
    }

    private void WireRoomEditing()
    {
        View.Transformed += changes => Edit(changes.SelectMany(c => SetPlacement(c.Instance, c.To)));
        View.DuplicateRequested += Duplicate;
        View.TileOperation += TileOperation;
        View.TilePasteRequested += () =>
        {
            if (_tileClip is null) return false;
            View.StartFloating(_tileClip);
            return true;
        };
        View.FloatingDropped += DropBlock;
        View.SelectionChanged += () => _alignButton.IsEnabled = View.Selection.OfType<GameObject>().Count() >= 2;
        View.Hovered += (at, _, element) => ShowTip(at, element);
        var menu = new ContextMenu();
        menu.Opening += (_, e) =>
        {
            var items = MenuItems();
            e.Cancel = items.Count == 0;
            menu.ItemsSource = items;
        };
        View.ContextMenu = menu;
    }

    private static string Whole(double value) => Math.Round(value).ToString(CultureInfo.InvariantCulture);

    private IEnumerable<EditCommand> SetPlacement(GameObject i, Placement to)
    {
        (string Field, string Value, bool Same)[] fields =
        [
            ("x", Whole(to.X), (int)Math.Round(to.X) == i.X),
            ("y", Whole(to.Y), (int)Math.Round(to.Y) == i.Y),
            ("scalex", ((float)to.ScaleX).ToString("R", CultureInfo.InvariantCulture), (float)to.ScaleX == i.ScaleX),
            ("scaley", ((float)to.ScaleY).ToString("R", CultureInfo.InvariantCulture), (float)to.ScaleY == i.ScaleY),
            ("rotation", ((float)to.Rotation).ToString("R", CultureInfo.InvariantCulture), (float)to.Rotation == i.Rotation),
        ];
        return fields.Where(f => !f.Same).Select(f => new RoomInstanceCommand(RoomName, RoomInstanceCommand.Set, i.InstanceID, objectName: f.Field) { Value = f.Value });
    }

    private List<(GameObject Instance, RoomBox RoomBox)> SelectedBoxes()
    {
        var items = new List<(GameObject, RoomBox)>();
        foreach (var instance in View.Selection.OfType<GameObject>())
            if (View.Scene?.BoundsOf(instance) is { } r) items.Add((instance, new RoomBox(r.Left, r.Top, r.Right, r.Bottom)));
        return items;
    }

    private void ApplyMoves(List<(GameObject Instance, RoomBox RoomBox)> items, (int Dx, int Dy)[] moves) =>
        Edit(items.Zip(moves).Where(z => z.Second != (0, 0)).Select(z => new RoomInstanceCommand(RoomName, RoomInstanceCommand.Move, z.First.Instance.InstanceID, z.First.Instance.X + z.Second.Dx, z.First.Instance.Y + z.Second.Dy)));

    public void AlignSelected(AlignMode mode)
    {
        var items = SelectedBoxes();
        if (items.Count >= 2) ApplyMoves(items, RoomAlign.Align([.. items.Select(i => i.RoomBox)], mode));
    }

    public void DistributeSelected(DistributeMode mode)
    {
        var items = SelectedBoxes();
        if (items.Count >= 3) ApplyMoves(items, RoomAlign.Distribute([.. items.Select(i => i.RoomBox)], mode));
    }

    private static MenuItem Item(string key, Action run, bool enabled = true)
    {
        var item = new MenuItem { Header = Loc.Current[key], IsEnabled = enabled };
        item.Click += (_, _) => run();
        return item;
    }

    private List<Control> AlignItems()
    {
        var many = View.Selection.OfType<GameObject>().Count() >= 3;
        return
        [
            Item("roomEditor.alignLeft", () => AlignSelected(AlignMode.Left)),
            Item("roomEditor.alignCenter", () => AlignSelected(AlignMode.CenterHorizontal)),
            Item("roomEditor.alignRight", () => AlignSelected(AlignMode.Right)),
            Item("roomEditor.alignTop", () => AlignSelected(AlignMode.Top)),
            Item("roomEditor.alignMiddle", () => AlignSelected(AlignMode.MiddleVertical)),
            Item("roomEditor.alignBottom", () => AlignSelected(AlignMode.Bottom)),
            Item("roomEditor.distributeH", () => DistributeSelected(DistributeMode.Horizontal), many),
            Item("roomEditor.distributeV", () => DistributeSelected(DistributeMode.Vertical), many),
        ];
    }

    private List<Control> TileItems() =>
    [
        Item("roomEditor.tileCut", () => View.RequestTileOperation(TileOp.Cut), View.TileSelection is not null),
        Item("roomEditor.tileCopy", () => View.RequestTileOperation(TileOp.Copy), View.TileSelection is not null),
        Item("roomEditor.tilePaste", () => View.RequestTilePaste(), _tileClip is not null),
        Item("roomEditor.tileDelete", () => View.RequestTileOperation(TileOp.Delete), View.TileSelection is not null),
        Item("roomEditor.tileFlipH", () => View.RequestTileOperation(TileOp.FlipHorizontal), View.TileSelection is not null),
        Item("roomEditor.tileFlipV", () => View.RequestTileOperation(TileOp.FlipVertical), View.TileSelection is not null),
        Item("roomEditor.tileRetile", () => RetileSelection(), View.TileSelection is not null && _autoTiles?.For(TileSetName ?? "").Count > 0),
    ];

    private List<Control> MenuItems()
    {
        var items = new List<Control>();
        var instances = View.Selection.OfType<GameObject>().ToList();
        if (instances.Count == 0 && View.Selection.OfType<ParticleSystemInstance>().Any())
            items.Add(Item("roomEditor.duplicate", () => Duplicate([.. View.Selection], new Point(View.SnapToGrid ? View.GridWidth : 16, View.SnapToGrid ? View.GridHeight : 16))));
        if (instances.Count > 0)
        {
            items.Add(Item("roomEditor.duplicate", () => Duplicate(instances, new Point(View.SnapToGrid ? View.GridWidth : 16, View.SnapToGrid ? View.GridHeight : 16))));
            items.Add(Item("roomEditor.selectSame", SelectSameObject));
            if (instances.Count == 1 && _instanceCode is not null) items.Add(Item("roomEditor.editInstanceCode", () => _instanceCode(instances[0])));
            items.Add(Item("roomEditor.deleteSelection", () => Edit(instances.Select(i => ChangeOf(i, RoomInstanceCommand.Delete, RoomElementCommand.Delete, 0, 0)).OfType<EditCommand>())));
        }

        if (instances.Count >= 2)
        {
            items.Add(new Separator());
            items.AddRange(AlignItems());
        }

        if (View.TileLayer is not null)
        {
            if (items.Count > 0) items.Add(new Separator());
            items.AddRange(TileItems());
        }

        if (ResourceMenu?.Invoke() is { Count: > 0 } resource)
        {
            if (items.Count > 0) items.Add(new Separator());
            items.AddRange(resource);
        }

        return items;
    }

    /// <summary>The resource commands (Rename, Duplicate, Copy name, ...) the window adds at the end of the menu; made again each time the menu opens.</summary>
    public Func<IReadOnlyList<Control>>? ResourceMenu { get; set; }

    /// <summary>Selects every instance of the objects of the selected instances.</summary>
    public void SelectSameObject()
    {
        var objects = View.Selection.OfType<GameObject>().Select(i => i.ObjectDefinition).ToHashSet();
        View.Select(RoomInstanceCommand.Lists(_room).SelectMany(l => l).Where(i => objects.Contains(i.ObjectDefinition) && !View.Locked.Contains(i)).DistinctBy(i => i.InstanceID));
    }

    /// <summary>Puts a copy of each selected instance at an offset and selects the copies.</summary>
    public void Duplicate(IReadOnlyList<object> elements, Point offset)
    {
        var commands = new List<RoomInstanceCommand>();
        foreach (var i in elements.OfType<GameObject>())
            if (i.ObjectDefinition?.Name.Content is { } name)
                commands.Add(new RoomInstanceCommand(RoomName, RoomInstanceCommand.Add, 0, i.X + (int)offset.X, i.Y + (int)offset.Y, name) { ScaleX = i.ScaleX, ScaleY = i.ScaleY, Rotation = i.Rotation });
        // A pasted particle system instance gets a random name, as GameMaker names the ones it makes.
        var copies = elements.OfType<ParticleSystemInstance>().Where(p => p.ParticleSystem?.Name.Content is not null && RoomElements.LayerOf(_room, p) is not null)
            .Select(p => new RoomElementCommand(new ElementRef(RoomName, RoomElements.LayerOf(_room, p)!.LayerId, RoomElements.Particle, RoomElements.NewParticleName(_room)), RoomElementCommand.Add, p.X + (int)offset.X, p.Y + (int)offset.Y, p.ParticleSystem!.Name.Content!)).ToList();
        if (commands.Count == 0 && copies.Count == 0) return;
        Edit([.. commands, .. copies]);
        if (commands.Count == 0) return;
        _pendingSelect = [.. commands.Select(c => uint.Parse(c.Args["id"], CultureInfo.InvariantCulture))];
    }

    private void TileOperation(TileOp op)
    {
        if (View.TileLayer is not { TilesData.TileData: { } rows } layer || View.TileSelection is not { } s) return;
        if (op is TileOp.Copy or TileOp.Cut) _tileClip = TileBlocks.Copy(rows, s.X, s.Y, s.Width, s.Height);
        var changes = op switch
        {
            TileOp.Cut or TileOp.Delete => TileBlocks.Clear(rows, s.X, s.Y, s.Width, s.Height),
            TileOp.FlipHorizontal => TileBlocks.FlipInPlace(rows, s.X, s.Y, s.Width, s.Height, true),
            TileOp.FlipVertical => TileBlocks.FlipInPlace(rows, s.X, s.Y, s.Width, s.Height, false),
            _ => [],
        };
        CommitTiles(layer, changes);
    }

    private void DropBlock(TileBlock block, int x, int y)
    {
        if (View.TileLayer is not { TilesData.TileData: { } rows } layer) return;
        CommitTiles(layer, TileBlocks.Paste(rows, block, x, y));
        View.TileSelection = (x, y, block.Width, block.Height);
    }

    private void CommitTiles(Layer layer, List<(int X, int Y, uint Value)> changes)
    {
        if (changes.Count > 0) _workspace?.Edit(new RoomTilesCommand(RoomName, layer.LayerId, changes));
    }

    private void SelectTiles(IReadOnlyList<(int X, int Y)> cells)
    {
        if (cells.Count == 0) return;
        var (x0, y0, x1, y1) = (cells.Min(c => c.X), cells.Min(c => c.Y), cells.Max(c => c.X), cells.Max(c => c.Y));
        View.TileSelection = (x0, y0, x1 - x0 + 1, y1 - y0 + 1);
    }

    /// <summary>The cells a brush of the chosen size covers around the stroke cells, kept inside the layer.</summary>
    private IReadOnlyList<(int X, int Y)> Brushed(Layer layer, IReadOnlyList<(int X, int Y)> cells)
    {
        if (View.BrushSize <= 1 || layer.TilesData is not { TileData: { } rows }) return cells;
        return [.. cells.SelectMany(c => TileBlocks.Brush(c, View.BrushSize)).Where(c => c.Y >= 0 && c.Y < rows.Length && c.X >= 0 && c.X < rows[c.Y].Length).Distinct()];
    }

    private void ShowTip(Point at, object? element)
    {
        if (element is null && View.TileLayer is { TilesData.TileData: { } rows } && View.TileCell(at) is var (x, y))
        {
            ToolTip.SetTip(View, Loc.Current.Format("roomEditor.tipTile", ("index", rows[y][x] & 0x0007FFFF), ("x", x), ("y", y)));
            _tipFor = null;
            return;
        }

        if (ReferenceEquals(element, _tipFor)) return;
        _tipFor = element;
        ToolTip.SetTip(View, element switch
        {
            null => null,
            GameObject i => Loc.Current.Format("roomEditor.tipInstance", ("name", i.ObjectDefinition?.Name.Content ?? "?"), ("id", i.InstanceID), ("x", i.X), ("y", i.Y),
                ("depth", (_room.Layers ?? []).FirstOrDefault(l => l.InstancesData?.Instances.Contains(i) == true)?.LayerDepth ?? 0),
                ("code", Loc.Current[i.CreationCode is null ? "roomEditor.codeNo" : "roomEditor.codeYes"])),
            _ => RoomElementList.Describe(element),
        });
    }
}
