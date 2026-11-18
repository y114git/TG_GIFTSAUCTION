using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

public sealed class PathRulesTests
{
    [Fact]
    public void TwoPathsDifferingOnlyInCaseAreOneFileExactlyWhereTheFileSystemFoldsCase()
    {
        var upper = Path.Combine(Path.GetTempPath(), "Game", "Data.win");
        var lower = Path.Combine(Path.GetTempPath(), "Game", "data.win");
        Assert.Equal(!OperatingSystem.IsLinux(), PathRules.Same(upper, lower));
        Assert.Equal(!OperatingSystem.IsLinux(), UserData.KeyFor(upper) == UserData.KeyFor(lower)); // one journal and one set of backups for one file, two for two files
        Assert.True(PathRules.Same(upper, Path.Combine(Path.GetTempPath(), "Game", "..", "Game", "Data.win")));
    }

    [Fact]
    public void TwoFilesThatDifferOnlyInCaseStayTwoFilesWhereTheFolderCanHoldBoth()
    {
        using var dir = new TempDir();
        var upper = dir.File("Data.win");
        var lower = dir.File("data.win");
        File.WriteAllText(upper, "a");
        File.WriteAllText(lower, "b"); // on a folder that folds the case this writes the same file again
        var both = Directory.EnumerateFiles(dir.File(".")).Count(f => Path.GetFileName(f).Equals("data.win", StringComparison.OrdinalIgnoreCase)) == 2;
        Assert.Equal(!both, PathRules.Same(upper, lower));
        Assert.Equal(!both, UserData.KeyFor(upper) == UserData.KeyFor(lower));
        Assert.Equal(!both, PathRules.Comparer.Equals(upper, lower));
        Assert.True(PathRules.Same(upper, upper));
    }
}
