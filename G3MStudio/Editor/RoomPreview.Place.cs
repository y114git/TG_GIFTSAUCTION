using Avalonia;
using G3MLib.DataFile.Models;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;
using G3MStudio.Rooms;

namespace G3MStudio.Editor;

public sealed partial class RoomPreview
{
    private readonly CopiedSet _ownCopies = new();

    // Copied instances are kept per open data file, not per room tab, so a copy in one room pastes into another room of the same file.
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Workspace, CopiedSet> Clips = [];

    private sealed class CopiedSet
    {
        public List<(string Object, int X, int Y, float ScaleX, float ScaleY, float Rotation)> Items = [];
    }

    private List<(string Object, int X, int Y, float ScaleX, float ScaleY, float Rotation)> Clipboard
    {
        get => (_workspace is null ? _ownCopies : Clips.GetOrCreateValue(_workspace)).Items;
        set => (_workspace is null ? _ownCopies : Clips.GetOrCreateValue(_workspace)).Items = value;
    }

    /// <summary>Gives the Place tool the resource to place: the key of the object or sprite selected in the Explorer, or null.</summary>
    public Func<string?>? PlaceSource { get; set; }

    private string? PlaceableSource() => PlaceSource?.Invoke() is { } key && ResourceTypes.SplitKey(key) is (("objects" or "sprites"), _) ? key : null;

    /// <summary>Shows what the Place tool would place now, or says to select an object or a sprite first.</summary>
    public void RefreshPlaceTarget()
    {
        _placeHint.IsVisible = View.Tool == RoomTool.Place;
        _placeHint.Text = PlaceableSource() is { } key && ResourceTypes.SplitKey(key) is var (type, name) ? Loc.Current.Format("rooms.placing", ("name", _workspace?.Nicknames.Label(type, name) ?? name)) : Loc.Current["rooms.placeNone"];
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
        if (Clipboard.Count == 0) return;
        var (ax, ay) = (Clipboard[0].X, Clipboard[0].Y);
        Edit(Clipboard.Select(c => new RoomInstanceCommand(RoomName, RoomInstanceCommand.Add, 0, (int)at.X + c.X - ax, (int)at.Y + c.Y - ay, c.Object) { ScaleX = c.ScaleX, ScaleY = c.ScaleY, Rotation = c.Rotation }));
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
        Clipboard = [];
        foreach (var i in elements.OfType<GameMakerRoom.GameObject>())
            if (i.ObjectDefinition?.Name.Content is { } name) Clipboard.Add((name, i.X, i.Y, i.ScaleX, i.ScaleY, i.Rotation));
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
        var tooLarge = cells.Count > RoomTilesCommand.MaxCells;
        List<(int X, int Y, uint Value)> changes = tooLarge ? []
            : tool == RoomTool.Fill && layer.TilesData is { TileData: { } rows } && cells.Count > 0
                ? RoomTilesCommand.FloodFill(rows, cells[0].X, cells[0].Y, value, out tooLarge)
                : [.. cells.Select(c => (c.X, c.Y, value))];
        if (tooLarge)
        {
            _placeHint.Text = Loc.Current.Format("rooms.tooManyTiles", ("count", RoomTilesCommand.MaxCells));
            _placeHint.IsVisible = true;
        }

        if (changes.Count > 0) _workspace?.Edit(new RoomTilesCommand(RoomName, layer.LayerId, changes));
    }
}
