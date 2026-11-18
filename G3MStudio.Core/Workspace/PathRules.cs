namespace G3MStudio.Core.Workspace;

/// <summary>
/// How two file paths are told apart. Windows and macOS fold the case by default, Linux does not; but a folder can differ from its system (a case sensitive folder
/// on Windows, a case sensitive volume on macOS). So two paths that differ only in case are one file unless the folder really holds both of them: then they are two files
/// and are opened, journaled and backed up separately, on any system.
/// </summary>
public static class PathRules
{
    public static StringComparison Comparison { get; } = OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

    public static StringComparer Comparer { get; } = new PathComparer();

    public static bool Same(string a, string b)
    {
        try { return Comparer.Equals(Path.GetFullPath(a), Path.GetFullPath(b)); }
        catch (ArgumentException) { return false; } // an empty or damaged remembered path is no path of any file
    }

    /// <summary>True when the folder holds another entry whose name differs from this one only in case, so the case tells two files apart there.</summary>
    public static bool HasCaseTwin(string fullPath)
    {
        try
        {
            if (Path.GetDirectoryName(fullPath) is not { } folder || !Directory.Exists(folder)) return false;
            var name = Path.GetFileName(fullPath);
            return Directory.EnumerateFileSystemEntries(folder).Count(e => string.Equals(Path.GetFileName(e), name, StringComparison.OrdinalIgnoreCase)) > 1;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return false;
        }
    }

    private sealed class PathComparer : StringComparer
    {
        public override bool Equals(string? x, string? y)
        {
            if (string.Equals(x, y, StringComparison.Ordinal)) return true;
            if (x is null || y is null || OperatingSystem.IsLinux()) return false;
            return string.Equals(x, y, StringComparison.OrdinalIgnoreCase) && !HasCaseTwin(x) && !HasCaseTwin(y);
        }

        public override int Compare(string? x, string? y) => OrdinalIgnoreCase.Compare(x, y);

        public override int GetHashCode(string obj) => OrdinalIgnoreCase.GetHashCode(obj);
    }
}
