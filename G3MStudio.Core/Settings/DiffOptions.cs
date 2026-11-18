namespace G3MStudio.Core.Settings;

/// <summary>How the comparison of two data files shows differences; set in Settings, read when the window opens. The colours are theme colours (Diff.Added, Diff.Removed, Diff.Changed).</summary>
public sealed record DiffOptions
{
    /// <summary>Lines that differ only in white space count as equal in the code view.</summary>
    public bool IgnoreWhitespace { get; init; }

    /// <summary>Unchanged lines kept around a change in the unified view, 0 to 50.</summary>
    public int ContextLines { get; init; } = 3;

    /// <summary>Open the code view as unified text instead of side by side.</summary>
    public bool Unified { get; init; }

    /// <summary>Picture view when a picture item opens: overlay, swipe or difference.</summary>
    public string PictureMode { get; init; } = "overlay";

    internal DiffOptions Clamped() => this with
    {
        ContextLines = Math.Clamp(ContextLines, 0, 50),
        PictureMode = PictureMode is "overlay" or "swipe" or "difference" ? PictureMode : "overlay",
    };
}
