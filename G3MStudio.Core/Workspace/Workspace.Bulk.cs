namespace G3MStudio.Core.Workspace;

public sealed partial class Workspace
{
    /// <summary>
    /// Runs a change that cannot be undone (an import) under the write lock and then drops the undo stack, whether the change finished or not.
    /// Normally only on a workspace without unsaved edits, so the journal never has to mix them with a change it cannot replay. With allowUnsaved the owner chose to go on:
    /// the unsaved edits stay in memory but can no longer be undone, and crash recovery starts from this point.
    /// </summary>
    public void ApplyBulk(Action change, bool allowUnsaved = false)
    {
        if (History.IsDirty && !allowUnsaved) throw new InvalidOperationException("Save or revert the unsaved changes first.");
        using (Lock.Write())
        {
            _decompileContext = null;
            try
            {
                change();
            }
            finally
            {
                History.Barrier();
                _journal?.Reset(Identity, []);
                JournalSuspended = true;
            }
        }

        BulkApplied?.Invoke();
    }

    /// <summary>
    /// Like <see cref="ApplyBulk"/>, but the change runs on a pool thread and every event (the history changing, the project refreshing) is raised on the
    /// thread that called this, which a window needs because its controls belong to its own thread.
    /// </summary>
    public async Task ApplyBulkAsync(Action change, bool allowUnsaved = false)
    {
        if (History.IsDirty && !allowUnsaved) throw new InvalidOperationException("Save or revert the unsaved changes first.");
        try
        {
            await Task.Run(() =>
            {
                using (Lock.Write())
                {
                    _decompileContext = null;
                    try
                    {
                        change();
                    }
                    finally
                    {
                        _journal?.Reset(Identity, []);
                    }
                }
            }, CancellationToken.None);
        }
        finally
        {
            History.Barrier();
            JournalSuspended = true;
            BulkApplied?.Invoke();
        }
    }

    /// <summary>A bulk change finished: a project has to refresh every resource.</summary>
    public event Action? BulkApplied;
}
