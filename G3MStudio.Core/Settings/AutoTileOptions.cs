using G3MStudio.Core.Workspace;

namespace G3MStudio.Core.Settings;

/// <summary>The defaults of the auto-tile brush and rule sets, set in Settings.</summary>
public sealed record AutoTileOptions
{
    /// <summary>Cells beyond the layer edge count as terrain, so terrain painted at the edge continues past it.</summary>
    public bool OutsideIsTerrain { get; init; }

    public AutoTileLayout DefaultLayout { get; init; } = AutoTileLayout.Blob47;

    /// <summary>Choose the first rule set of a tile set as soon as a layer of that tile set is selected.</summary>
    public bool StartOn { get; init; }

    internal AutoTileOptions Clamped() => this with { DefaultLayout = Enum.IsDefined(DefaultLayout) ? DefaultLayout : AutoTileLayout.Blob47 };
}
