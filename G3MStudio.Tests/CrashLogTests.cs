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

    [Fact]
    public void AFileThatCannotBePrunedStillLeavesTheWrittenPath()
    {
        using var dir = new TempDir();
        var data = new UserData(dir.File("user"));
        var folder = Directory.CreateDirectory(data.Combine("crashes")).FullName;
        var locked = Path.Combine(folder, "crash-00000000-000000-000-0000.txt");
        File.WriteAllText(locked, "old");
        for (var i = 1; i <= CrashLog.Keep; i++) File.WriteAllText(Path.Combine(folder, $"crash-0000000{i % 10}-000000-{i:000}-0000.txt"), "old");
        using var hold = new FileStream(locked, FileMode.Open, FileAccess.Read, FileShare.Read); // a delete of this file fails on Windows

        var path = CrashLog.Write(data, new InvalidOperationException("boom"));
        Assert.NotNull(path);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void ADataFolderThatCannotBeCreatedFallsBackToATempFolder()
    {
        using var dir = new TempDir();
        var blocker = dir.File("file");
        File.WriteAllText(blocker, "x");
        var data = UserData.OpenOrTemp(Path.Combine(blocker, "G3MStudio")); // a folder under a file cannot be created
        Assert.StartsWith(Path.GetTempPath(), data.Root, StringComparison.Ordinal);
        Assert.Contains(Environment.UserName.Where(char.IsAsciiLetterOrDigit).Aggregate("", (a, c) => a + c), data.Root, StringComparison.Ordinal);
        Assert.True(Directory.Exists(data.Root));
        Assert.NotNull(data.Problem);
    }

    [Fact]
    public void WhenTheTempFolderFailsToo_TheSessionStillStartsWithoutAFolder()
    {
        using var dir = new TempDir();
        var blocker = dir.File("file");
        File.WriteAllText(blocker, "x");
        var data = UserData.OpenOrTemp(Path.Combine(blocker, "G3MStudio"), blocker); // neither folder can be created
        Assert.NotNull(data.Problem);
        Assert.False(Directory.Exists(data.Root));
    }
}
