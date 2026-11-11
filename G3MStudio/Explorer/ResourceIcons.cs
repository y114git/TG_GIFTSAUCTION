using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace G3MStudio.Explorer;

/// <summary>One small vector icon per resource type, on a 24 x 24 grid, drawn with a theme colour. They stand in for the picture of a row that has none.</summary>
public static class ResourceIcons
{
    private const string Fallback = "M5 3h10l4 4v14H5zM7 5v14h10V9h-4V5z";

    private static readonly Dictionary<string, string> Paths = new()
    {
        ["general"] = "M12 2a10 10 0 100 20 10 10 0 000-20zM11 7h2v2h-2zM11 11h2v6h-2z",
        ["sprites"] = "M3 3h18v18H3zM5 5v14h14V5zM8 15l3-4 2 3 2-2 3 3v1H8z",
        ["tileSets"] = "M3 3h8v8H3zM13 3h8v8h-8zM3 13h8v8H3zM13 13h8v8h-8z",
        ["sounds"] = "M3 9h4l5-4v14l-5-4H3zM15 8c2 1 3 2.5 3 4s-1 3-3 4zM17 4c3 1.500 5 4.500 5 8s-2 6.500-5 8z",
        ["paths"] = "M4 20c0-8 6-4 8-8s2-8 8-8v2c-5 0-4 4-6 8s-8 0-8 6z",
        ["scripts"] = "M8 3H5v18h3v-2H7V5h1zM16 3h3v18h-3v-2h1V5h-1zM10 8h4v2h-4zM10 12h4v2h-4z",
        ["shaders"] = "M12 2l9 5v10l-9 5-9-5V7zM12 5L6 8.500v7L12 19l6-3.500v-7z",
        ["fonts"] = "M5 20L11 4h2l6 16h-2.200l-1.400-4H8.600l-1.400 4zM9.300 14h5.400L12 6.500z",
        ["timelines"] = "M3 11h18v2H3zM5 7h2v10H5zM11 5h2v14h-2zM17 8h2v8h-2z",
        ["objects"] = "M12 2l9 5v10l-9 5-9-5V7zM12 4.300L5 8.200v7.600l7 3.900 7-3.900V8.200zM12 8a4 4 0 110 8 4 4 0 010-8z",
        ["rooms"] = "M3 3h18v18H3zM5 5v14h14V5zM7 7h4v6H7zM13 7h4v2h-4zM13 11h4v6h-4z",
        ["extensions"] = "M9 3h2v3h2V3h2v3h3v5h-2.500a1.500 1.500 0 000 3H19v5H5v-5h2.500a1.500 1.500 0 000-3H5V6h4z",
        ["sequences"] = "M3 4h18v16H3zM5 6v12h14V6zM8 8l8 4-8 4z",
        ["animationCurves"] = "M3 3h2v16h16v2H3zM7 17c4 0 4-10 8-10 2 0 3 2 5 2v2c-2 0-3-2-5-2-4 0-4 10-8 10z",
        ["particleSystems"] = "M5 5a2 2 0 110 4 2 2 0 010-4zM15 3a2 2 0 110 4 2 2 0 010-4zM10 10a3 3 0 110 6 3 3 0 010-6zM19 12a2 2 0 110 4 2 2 0 010-4zM6 17a2 2 0 110 4 2 2 0 010-4zM16 18a2 2 0 110 4 2 2 0 010-4z",
        ["code"] = "M9 6l-6 6 6 6 1.400-1.400L5.800 12l4.600-4.600zM15 6l-1.400 1.400 4.600 4.600-4.600 4.600L15 18l6-6z",
        ["strings"] = "M3 5h18v2H3zM3 9h14v2H3zM3 13h18v2H3zM3 17h10v2H3z",
        ["variables"] = "M4 4h4v2H6v12h2v2H4zM20 4h-4v2h2v12h-2v2h4zM10 9l2 2.500L14 9l1 1-2.200 2.800L15 15.500 14 16.500l-2-2.500-2 2.500-1-1 2.200-2.700L9 10z",
        ["functions"] = "M14 3c-3 0-4 1.500-4.500 4L9 9H6v2h2.600L7.500 16c-.3 1.500-1 2-2 2H5v2h1c2.500 0 3.500-1 4-3.500L11 11h3V9h-2.600l.2-1.500c.2-1.200.7-1.500 1.600-1.500h.8V3zM14 13l1 -1 2.500 2.500L20 12l1 1-2.500 2.500L21 18l-1 1-2.500-2.500L15 19l-1-1 2.500-2.500z",
        ["codeLocals"] = "M4 4h16v2H4zM4 9h10v2H4zM4 14h16v2H4zM4 19h10v2H4zM17 8l3 3-3 3z",
        ["texturePageItems"] = "M3 3h18v18H3zM5 5v14h14V5zM7 7h6v5H7zM14 13h3v4h-3z",
        ["embeddedTextures"] = "M3 3h18v18H3zM5 5v6h6V5zM13 5v6h6V5zM5 13v6h6v-6zM13 13v6h6v-6z",
        ["embeddedImages"] = "M3 5h18v14H3zM5 7v10h14V7zM7 15l3-4 2 2.500 2-3 3 4.500z",
        ["embeddedAudio"] = "M4 10h2v4H4zM8 7h2v10H8zM12 4h2v16h-2zM16 8h2v8h-2zM20 11h1v2h-1z",
        ["textureGroups"] = "M3 7h14v14H3zM5 9v10h10V9zM7 3h14v14h-2V5H7z",
        ["audioGroups"] = "M3 8h3l4-3v14l-4-3H3zM13 9h2v6h-2zM17 7h2v10h-2z",
        ["filterEffects"] = "M3 4h18l-7 8v7l-4 2v-9z",
        ["uiLayers"] = "M12 3l9 5-9 5-9-5zM3 12l9 5 9-5v2.500l-9 5-9-5zM3 16.500l9 5 9-5V19l-9 5-9-5z",
    };

    private static readonly Dictionary<(string Type, Color Colour), DrawingImage> Made = [];

    /// <summary>The type keys that have an icon of their own; the others get a plain document icon.</summary>
    public static IEnumerable<string> Keys => Paths.Keys;

    public static string DataOf(string type) => Paths.GetValueOrDefault(type, Fallback);

    /// <summary>The icon of a type in the colour, shared between rows. Ask from the UI thread.</summary>
    public static DrawingImage Of(string type, Color colour)
    {
        if (Made.TryGetValue((type, colour), out var icon)) return icon;
        var drawing = new GeometryDrawing { Brush = new ImmutableSolidColorBrush(colour), Geometry = Geometry.Parse(DataOf(type)) };
        // The group holds a clip to the 24 x 24 grid, so every icon scales to the same box.
        var group = new DrawingGroup { ClipGeometry = new RectangleGeometry(new Avalonia.Rect(0, 0, 24, 24)) };
        group.Children.Add(drawing);
        return Made[(type, colour)] = new DrawingImage(group);
    }
}
