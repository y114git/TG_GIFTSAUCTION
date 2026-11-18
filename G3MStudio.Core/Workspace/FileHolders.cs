using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.InteropServices.Marshalling;

namespace G3MStudio.Core.Workspace;

/// <summary>Names the programs that hold a file open. Known on Windows (Restart Manager); null elsewhere or when nothing is found.</summary>
public static partial class FileHolders
{
    private const int Success = 0;

    public static string? Find(string path)
    {
        if (!OperatingSystem.IsWindows()) return null;
        try
        {
            Span<char> key = stackalloc char[33];
            if (RmStartSession(out var session, 0, key) != Success) return null;
            try
            {
                if (RmRegisterResources(session, 1, [path], 0, 0, 0, 0) != Success) return null;
                uint count = 0, reasons = 0;
                _ = RmGetList(session, out var needed, ref count, [], ref reasons); // the first call only asks how many there are
                if (needed == 0) return null;
                var info = new RmProcessInfo[needed];
                count = needed;
                if (RmGetList(session, out _, ref count, info, ref reasons) != Success) return null;
                return string.Join(", ", info.Take((int)count).Select(p => ((ReadOnlySpan<char>)p.AppName).TrimEnd('\0').ToString()).Distinct());
            }
            finally
            {
                _ = RmEndSession(session); // nothing to do if closing fails
            }
        }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException or MarshalDirectiveException)
        {
            return null;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RmUniqueProcess
    {
        public int ProcessId;
        public FILETIME ProcessStartTime;
    }

    [InlineArray(256)]
    private struct AppNameBuffer
    {
        private char _first;
    }

    [InlineArray(64)]
    private struct ServiceNameBuffer
    {
        private char _first;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RmProcessInfo
    {
        public RmUniqueProcess Process;
        public AppNameBuffer AppName;
        public ServiceNameBuffer ServiceShortName;
        public int ApplicationType;
        public uint AppStatus;
        public uint SessionId;
        public int Restartable;
    }

    [LibraryImport("rstrtmgr.dll", StringMarshalling = StringMarshalling.Utf16)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial int RmStartSession(out uint session, int flags, Span<char> key);

    [LibraryImport("rstrtmgr.dll", StringMarshalling = StringMarshalling.Utf16)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial int RmRegisterResources(uint session, uint files, [MarshalUsing(CountElementName = nameof(files))] string[] fileNames, uint apps, nint appList, uint services, nint serviceNames);

    [LibraryImport("rstrtmgr.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial int RmGetList(uint session, out uint needed, ref uint count, Span<RmProcessInfo> info, ref uint reasons);

    [LibraryImport("rstrtmgr.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial int RmEndSession(uint session);
}
