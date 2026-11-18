using System.Runtime.InteropServices;

namespace G3MStudio.Core.Links;

/// <summary>
/// Where the Windows file classes are written: the file classes of the current user (<c>Software\Classes</c>). Registration code only talks to this
/// interface, so tests use <see cref="MemoryClasses"/> and never touch the real registry or the owner's file associations.
/// </summary>
public interface IClassesStore
{
    void Set(string key, string? name, string value);

    string? Get(string key, string? name);

    void DeleteValue(string key, string? name);

    void DeleteTree(string key);

    /// <summary>Tells the shell that associations changed, so icons and menus refresh.</summary>
    void Changed();
}

/// <summary>The real store: HKEY_CURRENT_USER\Software\Classes, no administrator needed. Windows only.</summary>
public sealed partial class UserClasses : IClassesStore
{
    private const string Root = @"Software\Classes";

    public void Set(string key, string? name, string value)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("The registry exists only on Windows.");
        using var sub = Microsoft.Win32.Registry.CurrentUser.CreateSubKey($@"{Root}\{key}");
        sub.SetValue(name ?? "", value);
    }

    public string? Get(string key, string? name)
    {
        if (!OperatingSystem.IsWindows()) return null;
        using var sub = Microsoft.Win32.Registry.CurrentUser.OpenSubKey($@"{Root}\{key}");
        return sub?.GetValue(name ?? "") as string;
    }

    public void DeleteValue(string key, string? name)
    {
        if (!OperatingSystem.IsWindows()) return;
        using var sub = Microsoft.Win32.Registry.CurrentUser.OpenSubKey($@"{Root}\{key}", writable: true);
        sub?.DeleteValue(name ?? "", false);
    }

    public void DeleteTree(string key)
    {
        if (OperatingSystem.IsWindows()) Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree($@"{Root}\{key}", false);
    }

    public void Changed()
    {
        if (OperatingSystem.IsWindows()) SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero); // SHCNE_ASSOCCHANGED
    }

    [LibraryImport("shell32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial void SHChangeNotify(int eventId, int flags, IntPtr item1, IntPtr item2);
}

/// <summary>An in-memory store for tests.</summary>
public sealed class MemoryClasses : IClassesStore
{
    private readonly Dictionary<(string Key, string Name), string> _values = [];

    public int Notified { get; private set; }

    /// <summary>Every key that holds a value, for tests that check nothing was written outside the expected places.</summary>
    public IEnumerable<string> Keys => _values.Keys.Select(k => k.Key).Distinct();

    private static string Trim(string key) => key.Trim('\\').ToUpperInvariant();

    public void Set(string key, string? name, string value) => _values[(Trim(key), (name ?? "").ToUpperInvariant())] = value;

    public string? Get(string key, string? name) => _values.GetValueOrDefault((Trim(key), (name ?? "").ToUpperInvariant()));

    public void DeleteValue(string key, string? name) => _values.Remove((Trim(key), (name ?? "").ToUpperInvariant()));

    public void DeleteTree(string key)
    {
        var tree = Trim(key);
        foreach (var id in _values.Keys.Where(k => k.Key == tree || k.Key.StartsWith(tree + "\\", StringComparison.Ordinal)).ToList()) _values.Remove(id);
    }

    public void Changed() => Notified++;
}
