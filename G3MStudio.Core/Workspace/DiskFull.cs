namespace G3MStudio.Core.Workspace;

/// <summary>A write that failed because the drive is full. The settings and lists the program keeps for itself are saved as the user works, so this must not end the program.</summary>
public static class DiskFull
{
    // ENOSPC is 28 on Linux and macOS (the HResult of an IOException there is the error number); Windows has ERROR_HANDLE_DISK_FULL (39) and ERROR_DISK_FULL (112).
    public static bool Is(Exception e) => e is IOException io && (OperatingSystem.IsWindows() ? (io.HResult & 0xFFFF) is 39 or 112 : io.HResult == 28);
}
