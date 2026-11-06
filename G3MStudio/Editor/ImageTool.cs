using Avalonia.Input;

namespace G3MStudio.Editor;

public enum ImageTool { Pencil, Eraser, Replace, Remove, Fill, Line, Rectangle, Ellipse, Polygon, Curve, Picker, Select, Lasso, Wand, Move }

/// <summary>How a closed shape is drawn: only its outline, only its inside, or both.</summary>
public enum ShapeStyle { Outline, Filled, Both }

/// <summary>Tool keys as GameMaker's image editor has them (Hotkeys/ImageEditorHotkeys). A shifted shape key picks the filled style.</summary>
public static class ImageTools
{
    public static readonly IReadOnlyList<ImageTool> All = Enum.GetValues<ImageTool>();

    // Simple 24 x 24 outlines for the PathIcon of each toolbox button.
    public static string Icon(ImageTool tool) => tool switch
    {
        ImageTool.Pencil => "M3 21v-4l12-12 4 4L7 21zM17 3l4 4-2 2-4-4z",
        ImageTool.Eraser => "M16 3l5 5-9 9H7l-4-4zM4 20h16v1H4z",
        ImageTool.Replace => "M4 4h7v7H4zM13 13h7v7h-7zM14 5l5 0 0 5M10 19l-5 0 0-5",
        ImageTool.Remove => "M4 4h16v16H4zM8 8l8 8M16 8l-8 8",
        ImageTool.Fill => "M12 2l9 9-8 8-9-9zM21 15c2 3 2 5 0 5s-2-2 0-5z",
        ImageTool.Line => "M3 20L20 3l1 1L4 21z",
        ImageTool.Rectangle => "M3 5h18v14H3zM5 7v10h14V7z",
        ImageTool.Ellipse => "M12 4c5 0 9 3 9 8s-4 8-9 8-9-3-9-8 4-8 9-8zm0 2c-4 0-7 2-7 6s3 6 7 6 7-2 7-6-3-6-7-6z",
        ImageTool.Polygon => "M12 2l9 7-3 12H6L3 9zM12 5L6 9l2 9h8l2-9z",
        ImageTool.Curve => "M3 20C3 8 9 4 21 4v2C10 6 5 10 5 20z",
        ImageTool.Picker => "M20 4l-3-1-4 4-1-1-2 2 1 1-7 7v3h3l7-7 1 1 2-2-1-1 4-4zM6 19v-1l7-7 1 1-7 7z",
        ImageTool.Select => "M3 3h4v2H5v2H3zM10 3h4v2h-4zM17 3h4v4h-2V5h-2zM3 10h2v4H3zM19 10h2v4h-2zM3 17h2v2h2v2H3zM10 19h4v2h-4zM17 19h2v-2h2v4h-4z",
        ImageTool.Lasso => "M12 3C6 3 2 6 2 10s4 7 10 7c1 0 2 0 3-.3L19 21l1-2-3-3c3-1 5-3 5-6 0-4-4-7-10-7z",
        ImageTool.Wand => "M3 20l11-11 2 2L5 22zM16 2l1 3 3 1-3 1-1 3-1-3-3-1 3-1z",
        _ => "M12 2l4 4h-3v5h5V8l4 4-4 4v-3h-5v5h3l-4 4-4-4h3v-5H6v3l-4-4 4-4v3h5V6H8z",
    };

    public static Key KeyOf(ImageTool tool) => tool switch
    {
        ImageTool.Pencil => Key.D,
        ImageTool.Eraser => Key.E,
        ImageTool.Replace => Key.V,
        ImageTool.Remove => Key.H,
        ImageTool.Fill => Key.F,
        ImageTool.Line => Key.L,
        ImageTool.Rectangle => Key.R,
        ImageTool.Ellipse => Key.C,
        ImageTool.Polygon => Key.P,
        ImageTool.Curve => Key.A,
        ImageTool.Picker => Key.O,
        ImageTool.Select => Key.S,
        ImageTool.Wand => Key.W,
        ImageTool.Move => Key.M,
        _ => Key.Q,
    };

    /// <summary>The tool a key chooses (I also picks a colour, as the task's shortcut list has it), or null.</summary>
    public static ImageTool? FromKey(Key key, bool shift) => key == Key.I ? ImageTool.Picker : key == Key.B ? ImageTool.Pencil : All.Cast<ImageTool?>().FirstOrDefault(t => t is { } tool && KeyOf(tool) == key && (!shift || tool is ImageTool.Rectangle or ImageTool.Ellipse or ImageTool.Polygon));
}
