using G3MLib.DataFile;
using G3MStudio.Core.Settings;

namespace G3MStudio.Core.Workspace;

/// <summary>Size and write time of a file: what a save compares to find a file that another program changed.</summary>
internal readonly record struct FileStamp(long Length, DateTime WriteUtc)
{
    public static FileStamp? Of(string path)
    {
        try
        {
            var info = new FileInfo(path);
            return info.Exists ? new FileStamp(info.Length, info.LastWriteTimeUtc) : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}

public sealed partial class Workspace
{
    private FileStamp? _stamp;

    /// <summary>The file as it was when it was read or last written by this workspace; taken before a read, so a change during the read counts as a change.</summary>
    internal void RememberStamp(FileStamp? stamp = null) => _stamp = stamp ?? FileStamp.Of(Path);

    /// <summary>
    /// Asked before a save replaces a file that changed on disk since it was opened or last saved. True overwrites it. Core shows no dialog: the window sets this;
    /// without it such a save is refused.
    /// </summary>
    public Func<Task<bool>>? ConfirmOverwriteChanged { get; set; }

    private async Task<SaveResult?> RefuseIfChangedOnDiskAsync(bool interactive)
    {
        if (_stamp is not { } known || FileStamp.Of(Path) is not { } now || now == known) return null;
        // A save nobody asked for (autosync) never opens a dialog: it is refused and the caller remembers the change.
        if (interactive && ConfirmOverwriteChanged is { } ask && await ask()) return null;
        return new SaveResult(false, SaveFailure.ChangedOnDisk, "The file was changed outside G3MStudio since it was opened or saved.");
    }

    /// <summary>The checksum of the file just written. A scanner can hold it for a moment, so a failed read is tried again; null with the error when it keeps failing.</summary>
    private async Task<(DataFileIdentity? Identity, Exception? Error)> ReadIdentityAsync(SavePipeline pipeline)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return (await DataFileIdentity.ComputeAsync(Path, CancellationToken.None), null);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                if (attempt < pipeline.LockRetries)
                {
                    await Task.Delay(pipeline.LockRetryDelay, CancellationToken.None);
                    continue;
                }

                FailureReport.Report(e, "save-identity", new Dictionary<string, string> { ["g3mlib.version"] = FailureReport.G3MLibVersion });
                return (null, e);
            }
        }
    }

    /// <summary>A save ended: edits run again unless another save, export or background edit still holds them.</summary>
    private void EndSave()
    {
        if (Thaw()) ApplyQueued();
    }

    /// <summary>A background edit that is changing the model finishes first; edits that start later wait for the freeze.</summary>
    private async Task WaitForAppliedEdits()
    {
        await _applying.WaitAsync(CancellationToken.None);
        _applying.Release();
    }

    private readonly Lock _busyGate = new();
    private readonly CancellationTokenSource _closing = new();
    private int _busy;
    private bool _disposeWanted;

    /// <summary>A job that reads the model off the UI thread (save, project write) enters, so closing the file waits for it instead of disposing the model under it.</summary>
    private void Enter()
    {
        lock (_busyGate)
        {
            ObjectDisposedException.ThrowIf(_disposeWanted, this);
            _busy++;
        }
    }

    private void Leave()
    {
        lock (_busyGate)
        {
            if (--_busy > 0 || !_disposeWanted) return;
        }

        Release();
    }

    private CancellationTokenSource BeginSave(CancellationToken cancellationToken)
    {
        Enter();
        return CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _closing.Token);
    }

    /// <summary>Saves through the pipeline. Edits wait while it runs; on success the journal starts over from the saved file.</summary>
    public Task<SaveResult> SaveAsync(SavePipeline pipeline, IProgress<GameMakerProgress>? progress = null, CancellationToken cancellationToken = default) => SaveCoreAsync(pipeline, progress, true, cancellationToken);

    /// <summary>A save nobody asked for (autosync): a file changed on disk is refused without a dialog, with <see cref="SaveFailure.ChangedOnDisk"/>.</summary>
    internal Task<SaveResult> SaveUnattendedAsync(SavePipeline pipeline, CancellationToken cancellationToken = default) => SaveCoreAsync(pipeline, null, false, cancellationToken);

    private async Task<SaveResult> SaveCoreAsync(SavePipeline pipeline, IProgress<GameMakerProgress>? progress, bool interactive, CancellationToken cancellationToken)
    {
        // Asked before anything is frozen, so edits go on while the user decides.
        if (await RefuseIfChangedOnDiskAsync(interactive) is { } refused) return refused;
        using var linked = BeginSave(cancellationToken);
        Freeze();
        try
        {
            await WaitForAppliedEdits();
            var result = await pipeline.SaveAsync(Data, Path, progress, linked.Token);
            if (result.BackupFailure is { } backupFailure) ReportDataWriteFailure(backupFailure);
            if (!result.Saved) return result;

            // No cancellation from here: the file is already replaced, so the journal must follow.
            RememberStamp();
            var (identity, identityError) = await ReadIdentityAsync(pipeline);
            // The file is new all the same: its size follows and the hash is unknown (empty) until it is read again, so the journal never pairs it with the old file.
            Identity = identity ?? Identity with { Size = FileStamp.Of(Path)?.Length ?? Identity.Size, Md5 = "" };
            History.MarkSaved();
            JournalSuspended = false;
            // The file is saved whatever happens to the files in the data folder: a failure there is a warning, not a failed save.
            try
            {
                _journal?.Reset(Identity, []);
                if (identity is not null) IdentityChanged?.Invoke(identity);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                ReportDataWriteFailure(e);
            }

            return identityError is null ? result : result with { Warning = identityError.Message };
        }
        finally
        {
            try
            {
                EndSave();
            }
            finally
            {
                Leave();
            }
        }
    }

    /// <summary>Writes the model to another file (Save As, Save a Copy). Edits wait while it runs, as for a save, so the file never holds half an edit.</summary>
    public async Task<SaveResult> SaveCopyAsync(SavePipeline pipeline, string target, IProgress<GameMakerProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        // The open file itself is a normal save: identity, journal and the saved mark follow it.
        if (string.Equals(System.IO.Path.GetFullPath(target), Path, OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase))
            return await SaveAsync(pipeline, progress, cancellationToken);
        using var linked = BeginSave(cancellationToken);
        Freeze();
        try
        {
            await WaitForAppliedEdits();
            return await pipeline.SaveAsync(Data, target, progress, linked.Token);
        }
        finally
        {
            try
            {
                EndSave();
            }
            finally
            {
                Leave();
            }
        }
    }
}
