using G3MLib.DataFile;
using G3MStudio.Core.Settings;

namespace G3MStudio.Core.Workspace;

public enum SaveStep { Writing, Written, Validated, BackupRotated, BackedUp, Replaced }

public enum SaveFailure { None, Locked, ReadOnly, ValidationFailed, ChangedOnDisk }

/// <param name="Saved">True when the target now holds the new file.</param>
/// <param name="Failure">Why the save did not happen.</param>
/// <param name="Message">The error text for the user, when there is one.</param>
/// <param name="Holder">The program that holds the target open, when known.</param>
/// <param name="BackupFailure">Why the backup copy of the old file could not be made; the save went on without it.</param>
/// <param name="Warning">A problem after a save that worked (the saved file could not be read back for its checksum); the save stands.</param>
public sealed record SaveResult(bool Saved, SaveFailure Failure = SaveFailure.None, string? Message = null, string? Holder = null, Exception? BackupFailure = null, string? Warning = null);

/// <summary>Models a process kill: thrown by the test hook, it skips all cleanup like a dead process would.</summary>
public sealed class SaveInterruptedException : Exception;

/// <summary>
/// Save and Sync (design/shell.md 14.2): build the data file from memory next to the target, validate it by reading it back,
/// rotate the configured number of backups in the user data folder, then replace the target. The target is never opened for writing, so a kill at
/// any step leaves the old or the new valid file.
/// </summary>
public sealed partial class SavePipeline(UserData userData, int backupCount = 3)
{
    public const string NextSuffix = ".g3mnew";

    private const string PartSuffix = ".part";

    public int BackupCount { get; } = Math.Clamp(backupCount, 1, AppSettings.MaxBackups);

    public int LockRetries { get; init; } = 6; // 3 s in all: a virus scan or an indexer often holds a fresh file for a couple of seconds

    public TimeSpan LockRetryDelay { get; init; } = TimeSpan.FromMilliseconds(500);

    /// <summary>Test hook, called before each step with the temp file path.</summary>
    internal Action<SaveStep, string>? Hook { get; init; }

    /// <summary>Test hook: the free bytes of the drive that holds a path.</summary>
    internal Func<string, long>? FreeSpace { get; init; }

    private const long SpaceMargin = 16L * 1024 * 1024;

    // A scanner or sync client can hold a file for a moment right after it was written; a short wait ends most of those.
    private static async Task DeleteWithRetry(string path, CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                File.Delete(path);
                return;
            }
            catch (IOException) when (attempt < 4)
            {
                await Task.Delay(150 * (attempt + 1), cancellationToken);
            }
        }
    }

    public string BackupSlot(string target, int slot) =>
        userData.Combine("backups", UserData.KeyFor(target), $"{Path.GetFileName(target)}.{slot}");

    // One save at a time in the program: two saves share the temp file name of a target and the write state of the model, so a second one would find the temp file
    // held by the first or see the first one's write tables cleared halfway.
    private static readonly SemaphoreSlim OneSave = new(1, 1);

    public async Task<SaveResult> SaveAsync(GameMakerData data, string target, IProgress<GameMakerProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        await OneSave.WaitAsync(cancellationToken);
        try
        {
            return await SaveCoreAsync(data, target, progress, cancellationToken);
        }
        finally
        {
            OneSave.Release();
        }
    }

    private async Task<SaveResult> SaveCoreAsync(GameMakerData data, string target, IProgress<GameMakerProgress>? progress, CancellationToken cancellationToken)
    {
        var next = target + NextSuffix;
        try
        {
            CheckRoom(target);
            Hook?.Invoke(SaveStep.Writing, next);
            // A file or link left under this name (a shared folder can carry one) is removed, never written through; CreateNew refuses one that appears meanwhile.
            await DeleteWithRetry(next, cancellationToken);
            PagePixels.EncodeAll(data); // pages the editors keep as raw pixels go back to their stored format
            await using (var stream = new FileStream(next, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
            {
                await GameMakerIO.WriteAsync(stream, data, Hook is null ? progress ?? NoProgress.Instance : new HookedProgress(progress, () => Hook(SaveStep.Writing, next)), cancellationToken);
#pragma warning disable CA1849 // FlushAsync does not force the bytes to the disk, Flush(true) does, and this is the one place that needs it
                stream.Flush(true);
#pragma warning restore CA1849
            }

            Hook?.Invoke(SaveStep.Written, next);
            if (await Validate(data, next, cancellationToken) is { } problem)
            {
                TryDelete(next);
                return new SaveResult(false, SaveFailure.ValidationFailed, problem);
            }

            Hook?.Invoke(SaveStep.Validated, next);
            // The old file is copied aside first; the backup slots shift only after the replace worked, so failed saves never push good backups out.
            string? partial = null;
            Exception? backupFailure = null;
            if (File.Exists(target))
            {
                try
                {
                    partial = await Task.Run(() => PrepareBackup(target, next), CancellationToken.None); // a copy of a big file must not stop the UI thread
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    // A backup folder on a full or read-only drive costs the backup, never the save.
                    FailureReport.Report(e, "save-backup", new Dictionary<string, string> { ["g3mlib.version"] = FailureReport.G3MLibVersion });
                    TryDelete(BackupSlot(target, 1) + PartSuffix);
                    backupFailure = e;
                }
            }

            var result = await Replace(next, target, cancellationToken);
            if (partial is null) return backupFailure is null ? result : result with { BackupFailure = backupFailure };
            if (result.Saved) RotateBackups(target, partial);
            else TryDelete(partial);
            return result;
        }
        catch (Exception e) when (e is not SaveInterruptedException)
        {
            if (e is not OperationCanceledException) FailureReport.Report(e, "save", FailureReport.FileTags(next, data));
            TryDelete(next);
            TryDelete(BackupSlot(target, 1) + PartSuffix);
            throw;
        }
    }
}
