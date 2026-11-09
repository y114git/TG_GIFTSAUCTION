using G3MLib.DataFile;

namespace G3MStudio.Core.Workspace;

public sealed partial class WorkspaceService
{
    /// <summary>
    /// Opens another data file next to the current one, for work that touches both (copying resources). It is not the current workspace, has no
    /// project and is not in the recent list; the caller saves or drops it and disposes it.
    /// </summary>
    public async Task<Workspace> OpenDetachedAsync(string path, IProgress<GameMakerProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var full = Path.GetFullPath(path);
        if (!File.Exists(full)) throw new FileNotFoundException("Data file not found.", full);
        if (Current is { } current && string.Equals(current.Path, full, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("That data file is open already.");
        var warnings = new List<string>();
        var read = GameMakerIO.ReadAsync(full, progress ?? NoProgress.Instance, cancellationToken, (warning, _) => { lock (warnings) warnings.Add(warning); });
        var identity = DataFileIdentity.ComputeAsync(full, cancellationToken);
        await Task.WhenAll(read, identity);
        var workspace = new Workspace(full, await identity, await read, warnings);
        try
        {
            workspace.StartJournal(UserData);
        }
        catch
        {
            workspace.Dispose();
            throw;
        }

        return workspace;
    }
}
