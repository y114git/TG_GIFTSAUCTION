
namespace G3MStudio.Core.Workspace;

public sealed partial class EditHistory
{
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

    /// <summary>Counts every change of the data (an edit, an undo, a redo, a bulk change), so a cache can tell that the data moved; Position cannot, because undo then a new edit returns to the same place.</summary>
    public int Version { get; private set; }

    /// <summary>The command behind the last <see cref="Changed"/> (done, undone or redone); null after a save or a bulk change.</summary>
    public EditCommand? Last { get; private set; }

    public event Action? Changed;

    /// <summary>Op is "do", "undo" or "redo".</summary>
    public event Action<string, EditCommand>? Recorded;
    /// <summary>Undo or redo to any point of the history list.</summary>
    public void MoveTo(int position)
    {
        while (Position > position) Undo();
        while (Position < position) Redo();
    }
}
