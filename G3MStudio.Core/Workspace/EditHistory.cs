using G3MLib.DataFile;

namespace G3MStudio.Core.Workspace;

/// <summary>One undo stack per workspace (design/shell.md 14.4). <see cref="Recorded"/> feeds the journal.</summary>
public sealed class EditHistory(GameMakerData data)
{
    private readonly List<EditCommand> _items = [];
    private int _saved;

    /// <summary>Commands in order of execution; the first <see cref="Position"/> of them are applied.</summary>
    public IReadOnlyList<EditCommand> Items => _items;

    public int Position { get; private set; }

    /// <summary>True while a save reads the model: no edits then.</summary>
    public bool Frozen { get; set; }

    public bool CanUndo => Position > 0 && !Frozen;

    public bool CanRedo => Position < _items.Count && !Frozen;

    /// <summary>Differs from the last saved state. Undoing back to the save point is clean again.</summary>
    public bool IsDirty => Position != _saved || BulkChanges > 0;

    /// <summary>Changes made since the last save that cannot be undone (imports). They count as unsaved edits.</summary>
    public int BulkChanges { get; private set; }

    /// <summary>Edits between the save point and now (all applied edits when the save point was cut off).</summary>
    public int UnsavedCount => (_saved < 0 ? Position : Math.Abs(Position - _saved)) + BulkChanges;

    public event Action? Changed;

    /// <summary>Op is "do", "undo" or "redo".</summary>
    public event Action<string, EditCommand>? Recorded;

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

    /// <summary>Undo or redo to any point of the history list.</summary>
    public void MoveTo(int position)
    {
        while (Position > position) Undo();
        while (Position < position) Redo();
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
        Changed?.Invoke();
    }

    public void MarkSaved()
    {
        BulkChanges = 0;
        _saved = Position;
        Changed?.Invoke();
    }

    private void ThrowIfFrozen()
    {
        if (Frozen) throw new InvalidOperationException("The workspace is saving; edits wait until it is done.");
    }

    private void Notify(string op, EditCommand command)
    {
        Recorded?.Invoke(op, command);
        Changed?.Invoke();
    }
}
