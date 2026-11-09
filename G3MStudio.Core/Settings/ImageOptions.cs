namespace G3MStudio.Core.Settings;

/// <summary>
/// The defaults of the image editor, set in Settings and read when an editor opens. Colour texts are #RRGGBB or #RRGGBBAA; empty keeps the theme colour.
/// </summary>
public sealed record ImageOptions
{
    public int BrushSize { get; init; } = 1;

    public bool BrushRound { get; init; }

    /// <summary>Anti-aliased brush, line and shapes (GameMaker's Smooth).</summary>
    public bool Smooth { get; init; }

    /// <summary>Colour tolerance of fill, replace, remove and the magic wand, 0 to 255.</summary>
    public int Tolerance { get; init; }

    public bool Contiguous { get; init; } = true;

    public int TextSize { get; init; } = 16;

    /// <summary>Font family of the Text tool; empty is the interface font.</summary>
    public string TextFont { get; init; } = "";

    public bool ShowGrid { get; init; } = true;

    /// <summary>Pixels between the heavier lines of the grid; 0 turns them off.</summary>
    public int GridCell { get; init; } = 16;

    public string GridColour { get; init; } = "";

    /// <summary>How many frames before and after the current one the onion skin shows.</summary>
    public int OnionFrames { get; init; } = 1;

    /// <summary>Percent.</summary>
    public int OnionOpacity { get; init; } = 35;

    public string CheckerLight { get; init; } = "";

    public string CheckerDark { get; init; } = "";

    public string CanvasBackground { get; init; } = "";

    /// <summary>Size in pixels of the control point markers of polygons, curves and the lasso.</summary>
    public int HandleSize { get; init; } = 3;

    /// <summary>Write the layer stacks to disk shortly after every edit, so a crash loses none (otherwise they are written when the file closes).</summary>
    public bool LayerAutosave { get; init; } = true;

    internal ImageOptions Clamped() => this with
    {
        BrushSize = Math.Clamp(BrushSize, 1, 256),
        Tolerance = Math.Clamp(Tolerance, 0, 255),
        TextSize = Math.Clamp(TextSize, 4, 256),
        TextFont = TextFont ?? "",
        GridCell = Math.Clamp(GridCell, 0, 1024),
        OnionFrames = Math.Clamp(OnionFrames, 1, 8),
        OnionOpacity = Math.Clamp(OnionOpacity, 5, 100),
        HandleSize = Math.Clamp(HandleSize, 1, 12),
        GridColour = Valid(GridColour),
        CheckerLight = Valid(CheckerLight),
        CheckerDark = Valid(CheckerDark),
        CanvasBackground = Valid(CanvasBackground),
    };

    private static string Valid(string? text) => text?.Trim() is { } trimmed && Workspace.Rgba.Parse(trimmed) is not null ? trimmed : "";
}
