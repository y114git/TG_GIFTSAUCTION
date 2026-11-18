namespace G3MStudio.Editor;

public enum ImageTool { Pencil, Eraser, Replace, Remove, Fill, Line, Rectangle, Ellipse, Polygon, Curve, Text, Picker, Select, Lasso, Wand, Move, Hand }

/// <summary>How a closed shape is drawn: only its outline, only its inside, or both.</summary>
public enum ShapeStyle { Outline, Filled, Both }

/// <summary>The tools of the image editor with their icons. Their keys are commands ("img.tool.*", see AppCommands.ImageCommands), so they can be rebound.</summary>
public static class ImageTools
{
    public static readonly IReadOnlyList<ImageTool> All = Enum.GetValues<ImageTool>();

    /// <summary>The sparkle of the wand icon; its middle (13, 10) lies on the axis of the wand body, so the wand points at it.</summary>
    public const string WandStar = "M13 6l1 3 3 1-3 1-1 3-1-3-3-1 3-1z";

    /// <summary>Curved arrows for the toolbar; the redo one is the undo one mirrored about x = 12.</summary>
    public const string UndoIcon = "M9 4L3 10l6 6v-4h6a3 3 0 0 1 0 6h-4v2h4a5 5 0 0 0 0-10H9z";

    public const string RedoIcon = "M15 4l6 6-6 6v-4H9a3 3 0 0 0 0 6h4v2H9a5 5 0 0 1 0-10h6z";

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
        ImageTool.Text => "M5 4h14v4h-2V6h-4v12h2v2H9v-2h2V6H7v2H5z",
        ImageTool.Picker => "M20 4l-3-1-4 4-1-1-2 2 1 1-7 7v3h3l7-7 1 1 2-2-1-1 4-4zM6 19v-1l7-7 1 1-7 7z",
        ImageTool.Select => "M3 3h4v2H5v2H3zM10 3h4v2h-4zM17 3h4v4h-2V5h-2zM3 10h2v4H3zM19 10h2v4h-2zM3 17h2v2h2v2H3zM10 19h4v2h-4zM17 19h2v-2h2v4h-4z",
        ImageTool.Lasso => "M3 9.5a9 6.5 0 0 1 18 0a9 6.5 0 0 1-18 0zM5.5 9.5a6.5 4 0 0 0 13 0a6.5 4 0 0 0-13 0zM9 16h3l3 6h-3z", // a ring (the inner loop runs the other way, so the inside stays empty) with a tail
        ImageTool.Wand => "M2 19l6-6 2 2-6 6z" + WandStar,
        ImageTool.Move => "M12 2l4 4h-3v5h5V8l4 4-4 4v-3h-5v5h3l-4 4-4-4h3v-5H6v3l-4-4 4-4v3h5V6H8z",
        _ => "M9 11V5a1.5 1.5 0 013 0v5h1V3.5a1.5 1.5 0 013 0V10h1V5.5a1.5 1.5 0 013 0V15c0 4-3 7-7 7-3 0-5-1-7-4l-3-5a1.5 1.5 0 012.5-1.5L9 15z",
    };

    /// <summary>The command that chooses a tool, such as "img.tool.pencil".</summary>
    public static string CommandOf(ImageTool tool) => "img.tool." + char.ToLowerInvariant(tool.ToString()[0]) + tool.ToString()[1..];
}
