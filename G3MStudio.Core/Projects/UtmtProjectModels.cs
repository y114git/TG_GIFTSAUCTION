namespace G3MStudio.Core.Projects;

/// <summary>A UTMT project that cannot be read or converted. The message says why and is shown as it is.</summary>
public sealed class UtmtImportException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>Whether the chosen data file is the one a UTMT project was made from. Only projects written by G3MLib record it.</summary>
public enum UtmtBaseCheck { Match, Mismatch, Unrecorded }

/// <summary>
/// What a UTMT project folder holds, read without touching the data file. <c>Resources</c> counts per folder (<c>sprites</c>, <c>objects</c>, ...) only the folders
/// that hold something. <c>Scripts</c> run while the project is imported, with the rights of the program. <c>Options</c> names the project options the
/// G3MStudio project cannot store (patches, external files, ...). <c>IgnoredFolders</c> are top-level folders that UTMT does not read as resources.
/// </summary>
public sealed record UtmtProjectInfo
{
    public required string MainFile { get; init; }

    public required string Name { get; init; }

    public required IReadOnlyDictionary<string, int> Resources { get; init; }

    public bool HasGeneralInfo { get; init; }

    public IReadOnlyList<string> Scripts { get; init; } = [];

    public IReadOnlyList<string> Options { get; init; } = [];

    public IReadOnlyList<string> IgnoredFolders { get; init; } = [];

    public long? BaseSize { get; init; }

    public string? BaseMd5 { get; init; }

    public bool InGitRepository { get; init; }

    public string Folder => Path.GetDirectoryName(MainFile) ?? "";

    public int Total => Resources.Values.Sum();
}

/// <summary>
/// <c>ProjectPath</c> is the project folder or its main file. <c>BaseDataFile</c> is the data file the project was made for (the project is a change set over it).
/// <c>Destination</c> is a new or empty folder that becomes the G3MStudio project; the UTMT folder is never written to.
/// </summary>
public sealed record UtmtImportRequest(string ProjectPath, string BaseDataFile, string Destination, bool InitGit = true, bool RunScripts = false);

public enum UtmtGitState { None, Initialized, Existing }

/// <summary>A thing the conversion could not carry over or had to change. <paramref name="Key"/> picks the text, <paramref name="Detail"/> is shown after it.</summary>
public sealed record UtmtWarning(string Key, string Detail);

public sealed record UtmtImportReport(UtmtProjectInfo Info, string ProjectFolder, string ProjectFile, string DataFile, int Resources, UtmtGitState Git, IReadOnlyList<UtmtWarning> Warnings);
