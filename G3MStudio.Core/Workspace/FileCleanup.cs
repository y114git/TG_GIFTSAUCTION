namespace G3MStudio.Core.Workspace;

/// <summary>Removes leftovers where a failure must never hide the real result or error: a file a scanner or sync tool still holds stays for the system or the next sweep.</summary>
public static class FileCleanup
{
    public static void TryDeleteFile(string file)
    {
        try
        {
            File.Delete(file);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // left behind
        }
    }
}
