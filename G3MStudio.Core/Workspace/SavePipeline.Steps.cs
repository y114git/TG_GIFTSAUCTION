using G3MLib.DataFile;
using G3MStudio.Core.Settings;

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
            if (!chunks.SequenceEqual(backChunks)) return Mismatch(data, path, $"chunks {string.Join(",", chunks)} written, {string.Join(",", backChunks)} read");
            var counts = ResourceTypes.Counts(data);
            var backCounts = ResourceTypes.Counts(back);
            foreach (var (key, count) in counts)
                if (backCounts.GetValueOrDefault(key) != count) return Mismatch(data, path, $"{key}: {count} written, {backCounts.GetValueOrDefault(key)} read");
            return null;
        }
        catch (Exception e) when (e is IOException or InvalidDataException or GameMakerSerializationException)
        {
            FailureReport.Report(e, "save-validate", FailureReport.FileTags(path, data));
            return e.Message;
        }
    }
    // The file read back, but not as what was written: a data file that would have been corrupt, caught before it replaced the target.
    private static string Mismatch(GameMakerData data, string path, string what)
    {
        FailureReport.Problem("save-validate", "The written data file did not read back the same: " + what, FailureReport.FileTags(path, data));
        return what;
    }

    /// <summary>The new file is about as large as the old one, so a drive with less free room than that (plus a margin) is refused before anything is written.</summary>
    private void CheckRoom(string target)
    {
        if (!File.Exists(target)) return;
        var free = FreeSpace?.Invoke(target);
        if (free is null)
        {
            try { free = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(target)) ?? "").AvailableFreeSpace; }
            catch (Exception e) when (e is IOException or ArgumentException or UnauthorizedAccessException) { return; } // a path without a drive (a share): no check
        }

        var needed = new FileInfo(target).Length + SpaceMargin;
        if (free < needed) throw new IOException($"Not enough free space to save: {needed / (1024 * 1024)} MB needed, {free / (1024 * 1024)} MB free.");
    }

    /// <summary>Copies the current target next to the backup slots; the slots stay as they are until <see cref="RotateBackups"/>.</summary>
    private string PrepareBackup(string target, string next)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(BackupSlot(target, 1)) ?? ".");
        var partial = BackupSlot(target, 1) + PartSuffix;
        File.Copy(target, partial, overwrite: true);
        Hook?.Invoke(SaveStep.BackupRotated, next);
        Hook?.Invoke(SaveStep.BackedUp, next);
        return partial;
    }

    /// <summary>A leftover that cannot be deleted now (a scanner holds it) must not hide the result of the save; the next save clears it.</summary>
    private static void TryDelete(string file)
    {
        try
        {
            File.Delete(file);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // left behind
        }
    }

    /// <summary>The target was replaced: shift the slots and put the old file in slot 1. A failure here costs a backup, never the save.</summary>
    private void RotateBackups(string target, string partial)
    {
        try
        {
            ShiftSlots(target);
            File.Move(partial, BackupSlot(target, 1), overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            FailureReport.Report(e, "save-backup", new Dictionary<string, string> { ["g3mlib.version"] = FailureReport.G3MLibVersion });
            TryDelete(partial);
        }
    }

    private void ShiftSlots(string target)
    {
        // Shift only the slots that exist (they are contiguous from 1), then drop leftovers above the count after it was lowered.
        var top = 0;
        while (top < BackupCount - 1 && File.Exists(BackupSlot(target, top + 1))) top++;
        for (var slot = top; slot >= 1; slot--)
            File.Move(BackupSlot(target, slot), BackupSlot(target, slot + 1), overwrite: true);
        // Every numbered slot above the count goes, also past a gap left by a slot deleted by hand.
        var slotName = Path.GetFileName(BackupSlot(target, 1))[..^1];
        foreach (var file in Directory.GetFiles(Path.GetDirectoryName(BackupSlot(target, 1)) ?? ".", slotName + "*"))
            if (int.TryParse(Path.GetFileName(file).AsSpan(slotName.Length), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var slot) && slot > BackupCount)
                File.Delete(file);
    }

    // A read only flag is not a lock: waiting does not help, and the user has to clear it.
    private static bool IsReadOnly(string target)
    {
        try
        {
            return File.GetAttributes(target).HasFlag(FileAttributes.ReadOnly);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
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
                if (IsReadOnly(target))
                {
                    TryDelete(next);
                    return new SaveResult(false, SaveFailure.ReadOnly, e.Message);
                }

                if (attempt < LockRetries)
                {
                    await Task.Delay(LockRetryDelay, cancellationToken);
                    continue;
                }

                // The verified file is dropped; the edits stay in memory and in the journal, and the next Save writes it again.
                FailureReport.Report(e, "save-replace", new Dictionary<string, string> { ["failure"] = "locked", ["g3mlib.version"] = FailureReport.G3MLibVersion });
                TryDelete(next);
                return new SaveResult(false, SaveFailure.Locked, e.Message, FileHolders.Find(target));
            }
        }
    }
}
