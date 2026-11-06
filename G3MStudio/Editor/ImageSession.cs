using G3MStudio.Core.Workspace;

namespace G3MStudio.Editor;

/// <summary>Where the image editor gets its pictures and where an edit goes: the workspace history (a document tab) or a window that applies at the end.</summary>
public abstract class ImageSession
{
    public abstract int FrameCount { get; }

    public abstract PixelBuffer Read(int frame);

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

    public override void Commit(int frame, ImageEdit edit)
    {
        _own = true;
        try
        {
            _workspace.Edit(new ImageEditCommand(_sprite, frame, edit.Selection, edit.Ops, edit.After));
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
        if (!_own) OnChanged();
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
