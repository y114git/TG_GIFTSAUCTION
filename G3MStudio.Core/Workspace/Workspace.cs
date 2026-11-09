using G3MLib.DataFile;
using G3MStudio.Core.Projects;
using G3MLib.Modding.Hashing;

namespace G3MStudio.Core.Workspace;

/// <summary>Size, MD5 and path of a data file at open time; the key that later finds its project (ADR-0010).</summary>
public sealed record DataFileIdentity(string Path, long Size, string Md5)
{
    public static async Task<DataFileIdentity> ComputeAsync(string path, CancellationToken cancellationToken = default)
    {
        var full = System.IO.Path.GetFullPath(path);
        return new DataFileIdentity(full, new FileInfo(full).Length, await FileHash.ComputeMd5Async(full, cancellationToken));
    }
}

/// <summary>G3MLib declares its progress parameters non-nullable although null is allowed; this stands in for "no progress".</summary>
internal sealed class NoProgress : IProgress<GameMakerProgress>
{
    public static readonly NoProgress Instance = new();

    public void Report(GameMakerProgress value)
    {
    }
}

/// <summary>One open data file: the G3MLib model is the live representation.</summary>
public sealed partial class Workspace(string path, DataFileIdentity identity, GameMakerData data, IReadOnlyList<string> warnings) : IDisposable
{
    public string Path { get; private set; } = path;

    public DataFileIdentity Identity { get; private set; } = identity;

    public GameMakerData Data { get; } = data;

    /// <summary>Guards the model for background jobs, as G3MLib describes for hosts.</summary>
    public ModelLock Lock { get; } = new();

    /// <summary>Load warnings from the reader (design/shell.md 14.1 item 4).</summary>
    public IReadOnlyList<string> Warnings { get; } = warnings;

    public string Name => Data.GeneralInfo?.DisplayName?.Content is { Length: > 0 } name ? name : System.IO.Path.GetFileName(Path);

    public string Version => Data.GeneralInfo is { } info ? $"{info.Major}.{info.Minor}.{info.Release}.{info.Build}" : "";

    public EditHistory History { get; } = new(data);

    public ProblemsModel Problems { get; } = new();

    /// <summary>The project of this data file when Project mode is on (ADR-0010), else null.</summary>
    public ProjectLink? Project { get; internal set; }

    public bool IsDirty => History.IsDirty;

    public void Dispose()
    {
        _journal?.Dispose();
        Lock.Dispose();
        Data.Dispose();
    }
}
