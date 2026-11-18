using System.Text.RegularExpressions;

namespace G3MStudio.Tests;

/// <summary>design/ux-standards.md section 1: a gap between controls is a step of the spacing scale, never a loose number.</summary>
public sealed partial class SpacingScaleTests
{
    private static readonly HashSet<int> Scale = [0, 2, 4, 6, 8, 12, 16, 24];

    [GeneratedRegex(@"\bSpacing\s*=\s*(\d+)\b(?!\s*[/*+.(])")]
    private static partial Regex NumberedSpacing();

    [Fact]
    public void NoStackPanelSpacingInCodeIsOffTheScale()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "G3MStudio.slnx"))) dir = dir.Parent;
        var root = Path.Combine(dir?.FullName ?? throw new InvalidOperationException("G3MStudio.slnx not found."), "G3MStudio");
        var bad = Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .SelectMany(f => NumberedSpacing().Matches(File.ReadAllText(f)).Where(m => !Scale.Contains(int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture))).Select(m => $"{Path.GetFileName(f)}: {m.Value}"))
            .ToList();
        Assert.True(bad.Count == 0, "Off-scale Spacing, use a Space token: " + string.Join("; ", bad));
    }
}
