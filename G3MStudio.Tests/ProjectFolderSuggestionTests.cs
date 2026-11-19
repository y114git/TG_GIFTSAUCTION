using G3MStudio.Core.Projects;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

public sealed class ProjectFolderSuggestionTests
{
    [Fact]
    public void ASuggestedProjectFolderLivesInTheProjectsFolderAndNeverOnAFilledOne()
    {
        using var dir = new TempDir();
        var root = dir.File("projects");
        var service = new ProjectService(new UserData(dir.File("user"))) { DefaultRoot = root };
        Assert.Equal(Path.Combine(root, "My_Mod"), service.FreeFolder("My Mod"));

        var first = Directory.CreateDirectory(Path.Combine(root, "My_Mod"));
        Assert.Equal(first.FullName, service.FreeFolder("My Mod")); // an empty folder is fine
        File.WriteAllText(Path.Combine(first.FullName, "x.txt"), "x");
        Assert.Equal(Path.Combine(root, "My_Mod-2"), service.FreeFolder("My Mod"));

        service.Registry.RememberRoot(dir.File("elsewhere"));
        Directory.CreateDirectory(dir.File("elsewhere"));
        Assert.Equal(Path.Combine(dir.File("elsewhere"), "My_Mod"), service.FreeFolder("My Mod"));
    }
}
