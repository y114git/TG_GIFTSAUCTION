using G3MLib.DataFile;
using G3MStudio.Core.Settings;

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
        var stamp = FileStamp.Of(full);
        var read = GameMakerIO.ReadAsync(full, progress ?? NoProgress.Instance, cancellationToken, (warning, _) => { lock (warnings) warnings.Add(warning); });
        var identity = DataFileIdentity.ComputeAsync(full, cancellationToken);
        await WhenRead(read, identity, full);
        var workspace = new Workspace(full, await identity, await read, warnings);
        workspace.RememberStamp(stamp);
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

    // A file G3MLib cannot read is reported with its size range and the library version, never its name. A file another program holds open (a game that is running,
    // an archive tool still writing) is no fault of the reader: the open fails with the usual message and nothing is sent.
    private static async Task WhenRead(Task read, Task identity, string path)
    {
        try
        {
            await Task.WhenAll(read, identity);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            if (e is not IOException io || !Journal.IsHeld(io)) FailureReport.Report(e, "g3mlib.read", FailureReport.FileTags(path));
            throw;
        }
    }

    /// <summary>The run profile, recent list and last session of a file that was just opened; the exception when the data folder could not be written.</summary>
    private Exception? RememberOpened(Workspace workspace)
    {
        try
        {
            ApplyRunProfile(workspace);
            Recent.Add(workspace.Path, workspace.Name, workspace.Version, DateTimeOffset.Now, workspace.Identity);
            Settings.Update(x => x with { LastSession = workspace.Identity });
            return null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return e;
        }
    }

    private void RememberIdentity(DataFileIdentity identity)
    {
        try
        {
            Recent.Refresh(identity);
            Settings.Update(x => x with { LastSession = identity });
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Current?.ReportDataWriteFailure(e);
        }
    }

    /// <summary>A moved or replaced data file keeps the run profile of the old path, unless the new path has one of its own.</summary>
    public void MoveRunProfile(string from, string to)
    {
        from = Path.GetFullPath(from);
        to = Path.GetFullPath(to);
        if (Settings.Current.FileRunProfiles.TryGetValue(from, out var name) && !Settings.Current.FileRunProfiles.ContainsKey(to))
            Settings.Update(x => x with { FileRunProfiles = new Dictionary<string, string>(x.FileRunProfiles) { [to] = name } });
    }
}
