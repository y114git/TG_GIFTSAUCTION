using G3MLib.DataFile;

namespace G3MStudio.Core.Workspace;

/// <summary>The edit that makes a new resource from its template: a room of 1024 by 768 with one Instances layer (when the game uses layers), an empty object, script, path or timeline, a second of silence for a sound.</summary>
public static class ResourceTemplates
{
    public static EditCommand New(GameMakerData data, string type, string name)
    {
        var create = new ResourceListCommand(type, ResourceListCommand.New, name);
        if (type != "rooms" || !data.Rooms.Any(r => r.Layers?.Count > 0)) return create;
        return new GroupCommand([create, new RoomLayerCommand(name, RoomLayerCommand.AddInstances, 0, "Instances")]);
    }
}
