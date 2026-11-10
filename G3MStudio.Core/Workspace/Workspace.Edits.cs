using G3MLib.DataFile.Decompiler;

namespace G3MStudio.Core.Workspace;

public sealed partial class Workspace
{
    private readonly List<EditCommand> _queued = [];
    private bool _hold;

    /// <summary>A queued edit could not be applied when its turn came.</summary>
    public event Action<EditCommand, Exception>? QueuedEditFailed;

    /// <summary>Edits made from now on wait (Project export, save) and run in order when <see cref="ReleaseEdits"/> is called.</summary>
    internal void HoldEdits()
    {
        _hold = true;
        History.Frozen = true;
    }

    internal void ReleaseEdits()
    {
        _hold = false;
        History.Frozen = false;
        ApplyQueued();
    }

    private void ApplyQueued()
    {
        var queued = _queued.ToList();
        _queued.Clear();
        foreach (var command in queued)
        {
            try
            {
                using (Lock.Write()) History.Execute(command);
            }
            catch (Exception e) when (e is InvalidOperationException or ArgumentException)
            {
                QueuedEditFailed?.Invoke(command, e);
            }
        }
    }

    /// <summary>
    /// The way UI code makes an edit: under the model write lock, so background readers (decompiling) never see it half done.
    /// False when the edit was queued because the workspace is saving or exporting.
    /// </summary>
    public bool Edit(EditCommand command)
    {
        if (_hold || History.Frozen)
        {
            _queued.Add(command);
            return false;
        }

        using (Lock.Write())
        {
            _decompileContext = null;
            History.Execute(command);
        }

        return true;
    }

    /// <summary>
    /// Like <see cref="Edit"/> for a long edit: it applies on a pool thread under the write lock, and the history, the journal and the events follow
    /// on the thread that called this. While the workspace is saving or exporting it waits for that to end, then goes first (it is not put in the edit queue, which could not be cancelled).
    /// </summary>
    public async Task EditInBackground(EditCommand command)
    {
        while (_hold || History.Frozen) await Task.Delay(50);
        History.Frozen = true;
        try
        {
            try
            {
                await Task.Run(() =>
                {
                    using (Lock.Write())
                    {
                        _decompileContext = null;
                        command.Apply(Data);
                    }
                }, CancellationToken.None);
            }
            finally
            {
                EndBackground();
            }

            using (Lock.Write()) History.Commit(command);
        }
        finally
        {
            // Edits made while this one ran were queued; they follow it in the history.
            if (!_hold) ApplyQueued();
        }
    }

    // A hold that began meanwhile (save, export) keeps the history frozen until ReleaseEdits.
    private void EndBackground()
    {
        if (!_hold) History.Frozen = false;
    }

    /// <summary>
    /// Tries each edit alone and puts the model back after every one, in one pass under the write lock. Returns the edits that threw, with the exception,
    /// so a caller can leave those out before it writes the rest as one group. Nothing is added to the history.
    /// </summary>
    public async Task<IReadOnlyList<(EditCommand Command, InvalidOperationException Error)>> ProbeInBackground(IReadOnlyList<EditCommand> commands)
    {
        while (_hold || History.Frozen) await Task.Delay(50);
        History.Frozen = true;
        try
        {
            return await Task.Run(() =>
            {
                var failures = new List<(EditCommand, InvalidOperationException)>();
                using (Lock.Write())
                {
                    _decompileContext = null;
                    foreach (var command in commands)
                    {
                        // An apply that throws leaves the model as before (see EditHistory.Execute); only a revert that throws can leave it changed, and that is reported too.
                        try
                        {
                            command.Apply(Data);
                        }
                        catch (Exception e) when (e is not OutOfMemoryException)
                        {
                            failures.Add((command, Failure(e)));
                            continue;
                        }

                        try
                        {
                            command.Revert(Data);
                        }
                        catch (Exception e) when (e is not OutOfMemoryException)
                        {
                            failures.Add((command, Failure(e)));
                        }
                    }

                    _decompileContext = null;
                }

                return (IReadOnlyList<(EditCommand, InvalidOperationException)>)failures;
            }, CancellationToken.None);
        }
        finally
        {
            EndBackground();
            if (!_hold) ApplyQueued();
        }
    }

    private static InvalidOperationException Failure(Exception e) => e as InvalidOperationException ?? new InvalidOperationException(e.Message, e);

    /// <summary>Counts decompiler setting changes, which are not edits but alter the text of every code entry.</summary>
    public int DecompilerRevision { get; private set; }

    public void DecompilerChanged() => DecompilerRevision++;

    private GlobalDecompileContext? _decompileContext;
    private readonly Lock _contextLock = new();

    /// <summary>
    /// The context decompiling needs, made once and shared until the next edit: building it reads every function and variable, which costs
    /// far more than decompiling one entry. Call with <see cref="Lock"/> held for reading.
    /// </summary>
    public GlobalDecompileContext DecompileContext()
    {
        lock (_contextLock) return _decompileContext ??= new GlobalDecompileContext(Data);
    }

    /// <summary>Drops the shared decompile context to free memory; the next decompile builds it again.</summary>
    public void ReleaseDecompileContext()
    {
        lock (_contextLock) _decompileContext = null;
    }

    public void Undo()
    {
        using (Lock.Write())
        {
            _decompileContext = null;
            History.Undo();
        }
    }

    public void Redo()
    {
        using (Lock.Write())
        {
            _decompileContext = null;
            History.Redo();
        }
    }

    /// <summary>Undo or redo to any point of the history list.</summary>
    public void MoveTo(int position)
    {
        using (Lock.Write())
        {
            _decompileContext = null;
            History.MoveTo(position);
        }
    }
}
