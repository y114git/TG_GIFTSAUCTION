using G3MLib.DataFile;

namespace G3MStudio.Core.Workspace;

/// <summary>One undo stack per workspace (design/shell.md 14.4). <see cref="Recorded"/> feeds the journal.</summary>
public sealed partial class EditHistory(GameMakerData data)
{
    private readonly List<EditCommand> _items = [];
    private int _saved;

    /// <summary>Applies the command; if it throws, nothing is recorded and the model is as before.</summary>
    public void Execute(EditCommand command)
    {
        ThrowIfFrozen();
        command.Apply(data);
        if (_saved > Position) _saved = -1;
        _items.RemoveRange(Position, _items.Count - Position);
        _items.Add(command);
        Position++;
        Notify("do", command);
    }

    public void Undo()
    {
        ThrowIfFrozen();
        if (Position == 0) throw new InvalidOperationException("Nothing to undo.");
        var command = _items[Position - 1];
        command.Revert(data);
        Position--;
        Notify("undo", command);
    }

    public void Redo()
    {
        ThrowIfFrozen();
        if (Position == _items.Count) throw new InvalidOperationException("Nothing to redo.");
        var command = _items[Position];
        command.Apply(data);
        Position++;
        Notify("redo", command);
    }

    /// <summary>
    /// The model was changed in a way that cannot be undone (an import): the undo stack is dropped, and the file counts as changed until it is
    /// saved. The model may be half changed after a cancelled import, so this is called whether the change finished or not.
    /// </summary>
    public void Barrier()
    {
        _items.Clear();
        Position = 0;
        _saved = -1;
        BulkChanges++;
        Version++;
        Last = null;
        ReferenceCache.Of(data).Invalidate();
        Changed?.Invoke();
    }

    public void MarkSaved()
    {
        BulkChanges = 0;
        _saved = Position;
        Version++;
        Last = null;
        Changed?.Invoke();
    }

    private void ThrowIfFrozen()
    {
        if (Frozen) throw new InvalidOperationException("The workspace is saving; edits wait until it is done.");
    }

    private void Notify(string op, EditCommand command)
    {
        Recorded?.Invoke(op, command);
        Version++;
        Last = command;
        if (!command.KeepsReferences) ReferenceCache.Of(data).Invalidate();
        Changed?.Invoke();
    }
}
