namespace G3MStudio.Core.Workspace;

/// <summary>Mirror drawing: a stroke is also drawn mirrored across the middle of the picture.</summary>
[Flags]
public enum SymmetryMode { None = 0, Horizontal = 1, Vertical = 2 }

public static class Symmetry
{
    /// <summary>The points themselves and their mirror images (two copies for one axis, four for both).</summary>
    public static IReadOnlyList<List<(int X, int Y)>> Copies(IReadOnlyList<(int X, int Y)> points, int width, int height, SymmetryMode mode)
    {
        List<List<(int X, int Y)>> copies = [[.. points]];
        if (mode.HasFlag(SymmetryMode.Horizontal)) copies.Add([.. points.Select(p => (width - 1 - p.X, p.Y))]);
        if (mode.HasFlag(SymmetryMode.Vertical)) copies.AddRange(copies.ToList().Select(c => c.Select(p => (p.X, height - 1 - p.Y)).ToList()));
        return copies;
    }
}
