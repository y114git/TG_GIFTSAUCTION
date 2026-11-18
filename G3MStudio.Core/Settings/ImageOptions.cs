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

    /// <summary>Advanced choices of the Resize dialogs: a page may grow, a new page may be made, the largest page side (0: the largest page of the file, at least 2048).</summary>
    public bool FitGrow { get; init; } = true;

    public bool FitNewPage { get; init; } = true;

    public int FitMaxSide { get; init; }

    /// <summary>What the Resize dialog held when it was last applied: Resize image (false: Resize canvas), smooth scaling, keep aspect, percent, the anchor (0 to 8), the fill of new area (0 clear, 1 black, 2 white), and the modifier key that makes a handle drag apply to every frame (the scope of a dialog is never kept).</summary>
    public bool ResizeScale { get; init; } = true;

    public bool ResizeSmooth { get; init; }

    public bool ResizeKeep { get; init; } = true;

    public bool ResizePercent { get; init; }

    public int ResizeAnchor { get; init; }

    public int ResizeFill { get; init; }

    public string ResizeAllKey { get; init; } = "Alt";

    /// <summary>Resize image also scales the collision box and the origin of a sprite (off: they stay where they are), and the last width and height in percent.</summary>
    public bool ResizeBox { get; init; } = true;

    public int ResizePercentW { get; init; } = 100;

    public int ResizePercentH { get; init; } = 100;

    /// <summary>The Import Sheet dialog cuts by frame size (false: by frame count).</summary>
    public bool SheetBySize { get; init; }

    /// <summary>Every edit of a sprite in the image editor goes straight into the workspace (off: edits stay in the editor until Apply writes them as one step).</summary>
    public bool ApplyImmediately { get; init; }

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
        ResizeAnchor = Math.Clamp(ResizeAnchor, 0, 8),
        ResizeFill = Math.Clamp(ResizeFill, 0, 2),
        ResizePercentW = Math.Clamp(ResizePercentW, 1, 4096),
        ResizePercentH = Math.Clamp(ResizePercentH, 1, 4096),
        FitMaxSide = FitMaxSide <= 0 ? 0 : Math.Clamp(FitMaxSide, 64, Workspace.TextureFit.MaxPageSide),
        GridColour = Valid(GridColour),
        CheckerLight = Valid(CheckerLight),
        CheckerDark = Valid(CheckerDark),
        CanvasBackground = Valid(CanvasBackground),
    };

    private static string Valid(string? text) => text?.Trim() is { } trimmed && Workspace.Rgba.Parse(trimmed) is not null ? trimmed : "";
}
