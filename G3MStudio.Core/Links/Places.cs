using G3MStudio.Core.Workspace;

namespace G3MStudio.Core.Links;

/// <summary>
/// Every place the registration code writes to, so tests point them at a temp folder and a <see cref="MemoryClasses"/> and the real registry, desktop
/// entries and associations of the machine stay as they are. <see cref="RunTools"/> is false in tests: no xdg or macOS tool is started.
/// </summary>
public sealed record Places(IClassesStore Classes, string Applications, string Mime, string Config, string Icons, bool RunTools)
{
    /// <summary>The real places of the current user: the registry on Windows, the XDG folders on Linux, the user data folder for the icon file.</summary>
    public static Places Real(UserData userData)
    {
        if (OperatingSystem.IsWindows()) return new(new UserClasses(), "", "", "", userData.Combine("icons"), true);
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var share = Environment.GetEnvironmentVariable("XDG_DATA_HOME") is { Length: > 0 } x ? x : Path.Combine(home, ".local", "share");
        var config = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") is { Length: > 0 } c ? c : Path.Combine(home, ".config");
        return new(new MemoryClasses(), Path.Combine(share, "applications"), Path.Combine(share, "mime"), config, Path.Combine(share, "icons", "hicolor", "scalable", "mimetypes"), true);
    }

    /// <summary>Folders and a store inside <paramref name="root"/> that nothing else reads, for tests.</summary>
    public static Places Sandbox(string root, IClassesStore? classes = null) =>
        new(classes ?? new MemoryClasses(), Path.Combine(root, "applications"), Path.Combine(root, "mime"), Path.Combine(root, "config"), Path.Combine(root, "icons"), false);
}
