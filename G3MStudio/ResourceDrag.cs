using Avalonia.Input;

namespace G3MStudio;

/// <summary>The data the Explorer puts on a drag: the tab key of the resource, so editors can accept it.</summary>
public static class ResourceDrag
{
    public static readonly DataFormat<string> Format = DataFormat.CreateStringApplicationFormat("g3mstudio.resource");

    /// <summary>All dragged keys of an Explorer selection, one per line; <see cref="Format"/> holds the one under the pointer.</summary>
    public static readonly DataFormat<string> Keys = DataFormat.CreateStringApplicationFormat("g3mstudio.resources");
}
