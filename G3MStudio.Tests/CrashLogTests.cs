using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

public sealed class CrashLogTests
{
    [Fact]
    public void ACrashIsWrittenToTheUserDataFolderAndOnlyTheNewestFilesStay()
    {
        using var dir = new TempDir();
        var data = new UserData(dir.File("user"));
        var path = CrashLog.Write(data, new InvalidOperationException("boom"));
        Assert.NotNull(path);
        Assert.Contains("boom", File.ReadAllText(path), StringComparison.Ordinal);

        for (var i = 0; i < CrashLog.Keep + 3; i++) CrashLog.Write(data, new InvalidOperationException("again"));
        Assert.Equal(CrashLog.Keep, Directory.GetFiles(data.Combine("crashes")).Length);
    }
}
