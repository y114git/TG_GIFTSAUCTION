using G3MStudio.Core.Workspace;

namespace G3MStudio.Editor;

/// <summary>Where the image editor gets its pictures and where an edit goes: the workspace history (a document tab) or a window that applies at the end.</summary>
public abstract class ImageSession
{
    public abstract int FrameCount { get; }

    public abstract PixelBuffer Read(int frame);

    /// <summary>True when the session keeps layers (a document tab does; the painter window works on one flat picture).</summary>
    public virtual bool SupportsLayers => false;

    /// <summary>The layers of a frame; one layer holding the picture when the session has none.</summary>
    public virtual LayerSet ReadLayers(int frame) => LayerSet.OneLayer(Read(frame));

    /// <summary>Runs one layer operation as an undoable edit; false when the session has no layers.</summary>
    public virtual bool CommitLayers(int frame, ImageOp op) => false;

    /// <summary>Takes an edit the canvas already applied to its picture.</summary>
    public abstract void Commit(int frame, ImageEdit edit);

    public abstract bool CanUndo { get; }

    public abstract bool CanRedo { get; }

    public abstract void Undo();

    public abstract void Redo();

    /// <summary>Raised when the pictures changed behind the editor (an undo, a redo, a frame change), so it reads the frame again.</summary>
    public event Action? Changed;

    /// <summary>Adds a copy of the frame after it, or removes it; false when the session has no frame list.</summary>
    public virtual bool ChangeFrames(string operation, int frame) => false;

    protected void OnChanged() => Changed?.Invoke();
}

/// <summary>The image editor as a document tab: every edit is one <see cref="ImageEditCommand"/> in the workspace history, so undo, redo, the journal and autosave all work.</summary>
public sealed class WorkspaceImageSession : ImageSession, IDisposable
{
    private readonly Workspace _workspace;
    private readonly string _sprite;
    private bool _own;

    public WorkspaceImageSession(Workspace workspace, string sprite)
    {
        (_workspace, _sprite) = (workspace, sprite);
        workspace.History.Changed += OnHistoryChanged;
    }

    public override int FrameCount => _workspace.Data.Sprites.FirstOrDefault(s => s.Name.Content == _sprite)?.Textures.Count ?? 0;

    public override PixelBuffer Read(int frame)
    {
        using (_workspace.Lock.Read()) return FramePicture.Read(_workspace.Data, _sprite, frame);
    }

    public override bool SupportsLayers => true;

    public override LayerSet ReadLayers(int frame)
    {
        using (_workspace.Lock.Read()) return LayerStore.Of(_workspace.Data).Read(_workspace.Data, _sprite, frame);
    }

    public override bool CommitLayers(int frame, ImageOp op)
    {
        Commit(new ImageEditCommand(_sprite, frame, null, [op]));
        return true;
    }

    public override void Commit(int frame, ImageEdit edit) => Commit(new ImageEditCommand(_sprite, frame, edit.Selection, edit.Ops, edit.After, edit.Layer));

    private void Commit(ImageEditCommand command)
    {
        _own = true;
        try
        {
            _workspace.Edit(command);
        }
        finally
        {
            _own = false;
        }
    }

    public override bool CanUndo => _workspace.History.CanUndo;

    public override bool CanRedo => _workspace.History.CanRedo;

    public override void Undo() => _workspace.Undo();

    public override void Redo() => _workspace.Redo();

    public override bool ChangeFrames(string operation, int frame) => _workspace.Edit(new FrameListCommand(_sprite, operation, frame));

    private void OnHistoryChanged()
    {
        // The history raises this while the workspace is locked for writing, and reading the frame needs the read lock, so the reload waits until the lock is free.
        if (!_own) Avalonia.Threading.Dispatcher.UIThread.Post(OnChanged);
    }

    public void Dispose() => _workspace.History.Changed -= OnHistoryChanged;
}

/// <summary>One picture edited in a window: edits stay local (with their own undo) until the window hands the picture back.</summary>
public sealed class LocalImageSession(PixelBuffer picture) : ImageSession
{
    private readonly Stack<PixelBuffer> _undo = [];
    private readonly Stack<PixelBuffer> _redo = [];

    /// <summary>The last picture the editor committed; Apply hands this back.</summary>
    public PixelBuffer Current { get; private set; } = picture.Copy();

    public override int FrameCount => 1;

    public override PixelBuffer Read(int frame) => Current.Copy();

    public override void Commit(int frame, ImageEdit edit)
    {
        _undo.Push(Current);
        _redo.Clear();
        Current = edit.After;
    }

    public override bool CanUndo => _undo.Count > 0;

    public override bool CanRedo => _redo.Count > 0;

    public override void Undo() => Step(_undo, _redo);

    public override void Redo() => Step(_redo, _undo);

    private void Step(Stack<PixelBuffer> from, Stack<PixelBuffer> to)
    {
        if (from.Count == 0) return;
        to.Push(Current);
        Current = from.Pop();
        OnChanged();
    }
}
