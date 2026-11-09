using G3MLib.DataFile;

namespace G3MStudio.Core.Workspace;

public enum SaveStep { Writing, Written, Validated, BackupRotated, BackedUp, Replaced }

public enum SaveFailure { None, Locked, ValidationFailed }

/// <param name="Saved">True when the target now holds the new file.</param>
/// <param name="Failure">Why the save did not happen.</param>
/// <param name="Message">The error text for the user, when there is one.</param>
/// <param name="Holder">The program that holds the target open, when known.</param>
public sealed record SaveResult(bool Saved, SaveFailure Failure = SaveFailure.None, string? Message = null, string? Holder = null);

/// <summary>Models a process kill: thrown by the test hook, it skips all cleanup like a dead process would.</summary>
public sealed class SaveInterruptedException : Exception;

/// <summary>
/// Save and Sync (design/shell.md 14.2): build the data file from memory next to the target, validate it by reading it back,
/// rotate 1 to 3 backups in the user data folder, then replace the target. The target is never opened for writing, so a kill at
/// any step leaves the old or the new valid file.
/// </summary>
public sealed partial class SavePipeline(UserData userData, int backupCount = 3)
{
    public const string NextSuffix = ".g3mnew";

    public int BackupCount { get; } = Math.Clamp(backupCount, 1, 3);

    public int LockRetries { get; init; } = 3;

    public TimeSpan LockRetryDelay { get; init; } = TimeSpan.FromMilliseconds(500);

    /// <summary>Test hook, called before each step with the temp file path.</summary>
    internal Action<SaveStep, string>? Hook { get; init; }

    public string BackupSlot(string target, int slot) =>
        userData.Combine("backups", UserData.KeyFor(target), $"{Path.GetFileName(target)}.{slot}");

    public async Task<SaveResult> SaveAsync(GameMakerData data, string target, IProgress<GameMakerProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var next = target + NextSuffix;
        try
        {
            Hook?.Invoke(SaveStep.Writing, next);
            // A file or link left under this name (a shared folder can carry one) is removed, never written through; CreateNew refuses one that appears meanwhile.
            File.Delete(next);
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
                File.Delete(next);
                return new SaveResult(false, SaveFailure.ValidationFailed, problem);
            }

            Hook?.Invoke(SaveStep.Validated, next);
            if (File.Exists(target)) RotateBackups(target, next);
            return await Replace(next, target, cancellationToken);
        }
        catch (Exception e) when (e is not SaveInterruptedException)
        {
            File.Delete(next);
            throw;
        }
    }
}
