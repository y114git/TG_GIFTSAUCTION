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
public sealed class SavePipeline(UserData userData, int backupCount = 3)
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
            await using (var stream = new FileStream(next, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
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

    private sealed class HookedProgress(IProgress<GameMakerProgress>? inner, Action onFirst) : IProgress<GameMakerProgress>
    {
        private int _first = 1;

        public void Report(GameMakerProgress value)
        {
            if (Interlocked.Exchange(ref _first, 0) == 1) onFirst();
            inner?.Report(value);
        }
    }

    /// <summary>Null when the file reads back with the same chunks and entry counts as the model; otherwise what differs.</summary>
    private static async Task<string?> Validate(GameMakerData data, string path, CancellationToken cancellationToken)
    {
        try
        {
            using var back = await GameMakerIO.ReadAsync(path, NoProgress.Instance, cancellationToken);
            var chunks = data.FORM.Chunks.Keys.Order().ToList();
            var backChunks = back.FORM.Chunks.Keys.Order().ToList();
            if (!chunks.SequenceEqual(backChunks)) return $"chunks {string.Join(",", chunks)} written, {string.Join(",", backChunks)} read";
            var counts = ResourceTypes.Counts(data);
            var backCounts = ResourceTypes.Counts(back);
            foreach (var (key, count) in counts)
                if (backCounts.GetValueOrDefault(key) != count) return $"{key}: {count} written, {backCounts.GetValueOrDefault(key)} read";
            return null;
        }
        catch (Exception e) when (e is IOException or InvalidDataException or GameMakerSerializationException)
        {
            return e.Message;
        }
    }

    private void RotateBackups(string target, string next)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(BackupSlot(target, 1)) ?? ".");
        for (var slot = BackupCount - 1; slot >= 1; slot--)
            if (File.Exists(BackupSlot(target, slot))) File.Move(BackupSlot(target, slot), BackupSlot(target, slot + 1), overwrite: true);
        Hook?.Invoke(SaveStep.BackupRotated, next);
        var partial = BackupSlot(target, 1) + ".part";
        File.Copy(target, partial, overwrite: true);
        File.Move(partial, BackupSlot(target, 1), overwrite: true);
        Hook?.Invoke(SaveStep.BackedUp, next);
    }

    private async Task<SaveResult> Replace(string next, string target, CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                File.Move(next, target, overwrite: true);
                Hook?.Invoke(SaveStep.Replaced, next);
                return new SaveResult(true);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                if (attempt < LockRetries)
                {
                    await Task.Delay(LockRetryDelay, cancellationToken);
                    continue;
                }

                // The verified file is dropped; the edits stay in memory and in the journal, and the next Save writes it again.
                File.Delete(next);
                return new SaveResult(false, SaveFailure.Locked, e.Message, FileHolders.Find(target));
            }
        }
    }
}
