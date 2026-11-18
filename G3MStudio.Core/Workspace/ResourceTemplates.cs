using System.Globalization;
using G3MLib.DataFile;

namespace G3MStudio.Core.Workspace;

/// <summary>The edit that makes a new resource from its template: a room of 1024 by 768 with one Instances layer (when the game uses layers), an empty object, script, path or timeline, a second of silence for a sound.</summary>
public static class ResourceTemplates
{
    public static EditCommand New(GameMakerData data, string type, string name, bool force = false)
    {
        var create = new ResourceListCommand(type, ResourceListCommand.New, name, force);
        if (type != "rooms" || !data.Rooms.Any(r => r.Layers?.Count > 0)) return create;
        return new GroupCommand([create, new RoomLayerCommand(name, RoomLayerCommand.AddInstances, 0, "Instances")]);
    }

    /// <summary>The edit that makes a new sprite named <paramref name="name"/> from the pictures (one undo step): the template sprite, then the frames as its own pictures, with the box, margins and mask of their size.</summary>
    public static EditCommand NewSprite(GameMakerData data, string name, IReadOnlyList<PixelBuffer> frames, bool force = false)
    {
        if (frames.Count == 0) throw new InvalidDataException("There are no frames to save.");
        var (w, h) = (frames[0].Width, frames[0].Height);
        if (frames.Any(f => (f.Width, f.Height) != (w, h))) throw new InvalidDataException("The frames differ in size.");
        SetPropertyCommand Set(string path, int value) => new("sprites", name, path, value.ToString(CultureInfo.InvariantCulture));
        return new GroupCommand([ResourceTemplates.New(data, "sprites", name, force), new FramesImportCommand(name, FramePicture.ToPngs(frames, FramePicture.MaxPngBytes), true),
            Set("Width", w), Set("Height", h), Set("MarginLeft", 0), Set("MarginTop", 0), Set("MarginRight", w - 1), Set("MarginBottom", h - 1), new RecalcMaskCommand(name)]);
    }
}
