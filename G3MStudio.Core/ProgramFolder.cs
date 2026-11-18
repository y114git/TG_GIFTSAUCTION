namespace G3MStudio.Core;

/// <summary>
/// The folder of the program file. A single-file build runs from an extraction folder, so AppContext.BaseDirectory is not the folder the user put the program in;
/// the Scripts and projects folders and the updater work beside the file.
/// </summary>
public static class ProgramFolder
{
    /// <summary>The folder with a trailing separator, like AppContext.BaseDirectory.</summary>
    public static string Path { get; } = Resolve(AppContext.BaseDirectory, Environment.ProcessPath);

    // A run through the dotnet host has the host as its process path, so only a program that runs as its own file trusts it; a bundle that extracted its assemblies has a base directory of its own.
    internal static string Resolve(string baseDirectory, string? processPath) =>
        System.IO.Path.GetFileNameWithoutExtension(processPath) is { Length: > 0 } name && !name.Equals("dotnet", StringComparison.OrdinalIgnoreCase)
        && System.IO.Path.GetDirectoryName(processPath) is { Length: > 0 } folder ? System.IO.Path.TrimEndingDirectorySeparator(folder) + System.IO.Path.DirectorySeparatorChar : baseDirectory;
}
