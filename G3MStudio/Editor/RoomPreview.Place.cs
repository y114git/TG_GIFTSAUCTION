using Avalonia;
using G3MLib.DataFile.Models;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;
using G3MStudio.Rooms;

namespace G3MStudio.Editor;

public sealed partial class RoomPreview
{
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
}
