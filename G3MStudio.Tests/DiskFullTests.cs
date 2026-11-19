using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

public sealed class DiskFullTests
{
    private static readonly int FullCode = OperatingSystem.IsWindows() ? 112 : 28;

    [Fact]
    public void OnlyAnIoErrorOfAFullDriveCounts()
    {
        Assert.True(DiskFull.Is(new IOException("No space left on device", FullCode)));
        Assert.False(DiskFull.Is(new IOException("in use", OperatingSystem.IsWindows() ? 32 : 11)));
        Assert.False(DiskFull.Is(new InvalidOperationException()));
    }
}
