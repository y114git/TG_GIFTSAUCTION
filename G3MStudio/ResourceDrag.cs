using Avalonia.Input;

namespace G3MStudio;

/// <summary>The data the Explorer puts on a drag: the tab key of the resource, so editors can accept it.</summary>
public static class ResourceDrag
{
    public static readonly DataFormat<string> Format = DataFormat.CreateStringApplicationFormat("g3mstudio.resource");
}
