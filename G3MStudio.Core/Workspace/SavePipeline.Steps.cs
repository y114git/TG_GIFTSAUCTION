using G3MLib.DataFile;

namespace G3MStudio.Core.Workspace;

public sealed partial class SavePipeline
{
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
