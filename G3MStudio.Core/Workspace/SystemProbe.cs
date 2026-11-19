
namespace G3MStudio.Core.Workspace;

public sealed class SystemProbe : ICompatProbe
{
    public static SystemProbe Instance { get; } = new();

    public bool IsWindows => OperatingSystem.IsWindows();

    public bool IsMacOS => OperatingSystem.IsMacOS();

    public string Home => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    public bool FileExists(string path) => File.Exists(path);

    public bool DirectoryExists(string path) => Directory.Exists(path);

    public IReadOnlyList<string> Files(string folder) => List(() => Directory.GetFiles(folder));

    public IReadOnlyList<string> Folders(string folder) => List(() => Directory.GetDirectories(folder));

    public string? ReadText(string path)
    {
        try
        {
            return File.Exists(path) && new FileInfo(path).Length <= 4 * 1024 * 1024 ? File.ReadAllText(path) : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public string? Env(string name) => Environment.GetEnvironmentVariable(name);

    public string? OnPath(string name) => RunLocator.Resolve(name);

    private static string[] List(Func<string[]> read)
    {
        try
        {
            return read();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }
}
