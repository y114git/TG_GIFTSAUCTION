using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

public sealed class RuntimeFinderTests
{
    [Fact]
    public void FindsRunnersInBothLayoutsNewestFirst()
    {
        using var dir = new TempDir();
        Touch(dir.File("rt/runtime-2022.0.3.99/windows/x64/Runner.exe"));
        Touch(dir.File("rt/runtime-2024.13.1.242/windows/x64/Runner.exe"));
        Touch(dir.File("rt/runtime-1.4.9999/windows/Runner.exe"));
        Directory.CreateDirectory(dir.File("rt/runtime-9.9.9/windows")); // no runner inside
        Directory.CreateDirectory(dir.File("rt/other"));

        var found = RuntimeFinder.Find([dir.File("rt"), dir.File("missing"), ""]);

        Assert.Equal(["2024.13.1.242", "2022.0.3.99", "1.4.9999"], found.Select(r => r.Version));
        Assert.All(found, r => Assert.True(File.Exists(r.Runner)));
    }

    private static void Touch(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "");
    }
}
