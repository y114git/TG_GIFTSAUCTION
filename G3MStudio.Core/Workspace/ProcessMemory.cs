using System.Runtime;
using System.Runtime.InteropServices;

namespace G3MStudio.Core.Workspace;

/// <summary>
/// Gives memory back to the system after a big release (a file or the last tab closed). The collector decommits free pages by itself only slowly, and native
/// buffers (bitmaps, decoders) are freed by finalizers, so one explicit round does what the idle program would take minutes to do.
/// </summary>
public static partial class ProcessMemory
{
    /// <summary>
    /// Collects twice around the finalizers (the first pass queues the native owners, the second frees what they held), compacts the large object heap and, with
    /// <paramref name="trim"/>, trims the working set. Blocking: run it off the UI thread.
    /// </summary>
    public static void Reclaim(bool trim = true)
    {
        GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
        GC.Collect(2, GCCollectionMode.Forced, true, true);
        GC.WaitForPendingFinalizers();
        GC.Collect(2, GCCollectionMode.Forced, true, true);
        if (trim) TrimWorkingSet();
    }

    /// <summary>Asks the system to take back the pages of this process that nothing touches now; they come back on demand. A no-op outside Windows.</summary>
    public static void TrimWorkingSet()
    {
        if (OperatingSystem.IsWindows()) _ = K32EmptyWorkingSet(GetCurrentProcess());
    }

    [LibraryImport("kernel32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial IntPtr GetCurrentProcess();

    [LibraryImport("kernel32.dll", EntryPoint = "K32EmptyWorkingSet")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool K32EmptyWorkingSet(IntPtr process);
}
