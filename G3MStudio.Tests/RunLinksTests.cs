using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

public sealed class RunLinksTests
{
    [Fact]
    public void LinesAreParsedAndUnsafeNamesAreDropped()
    {
        var parsed = RunLinks.Parse("../mus\n# note\n  ../lang => language \n../x => ..\n\n", Path.GetFullPath("C:/g/chapter1"));
        Assert.Equal(["mus", "language"], [.. parsed.Select(p => p.Name)]);
    }

    [Fact]
    public void AFolderAndAFileAreMadeAvailableForTheRunAndTakenBackAndMissingOnesAreReported()
    {
        using var dir = new TempDir();
        var chapter = dir.File("game/chapter1");
        Directory.CreateDirectory(chapter);
        Directory.CreateDirectory(dir.File("game/mus"));
        File.WriteAllText(dir.File("game/mus/a.ogg"), "x");
        File.WriteAllText(dir.File("game/extra.txt"), "y");
        Assert.Equal(["mus"], RunLinks.Missing(chapter));

        var (added, failed) = RunLinks.Apply(chapter, chapter, "../mus\n../extra.txt\n../nope");
        Assert.Equal(2, added.Count);
        Assert.Single(failed);
        Assert.True(File.Exists(Path.Combine(chapter, "mus", "a.ogg")));
        Assert.True(File.Exists(Path.Combine(chapter, "extra.txt")));
        Assert.Empty(RunLinks.Missing(chapter));

        RunLinks.Remove(added);
        Assert.False(Path.Exists(Path.Combine(chapter, "mus")));
        Assert.False(Path.Exists(Path.Combine(chapter, "extra.txt")));
        Assert.True(File.Exists(dir.File("game/mus/a.ogg"))); // the original is never touched
    }
}
