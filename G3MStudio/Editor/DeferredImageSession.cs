using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

namespace G3MStudio.Editor;

/// <summary>
/// The image editor as a tab with Apply that means something: edits stay in a local list of their own (with their own undo and redo), shown over the sprite the workspace holds,
/// and the workspace, the sprite tab, the Explorer and autosave see them only when they are applied, all together as one undoable workspace step.
/// <see cref="WorkspaceImageSession"/> is the other way (every edit goes straight into the history), chosen by the setting "Apply image edits immediately".
/// </summary>
public sealed class DeferredImageSession : ImageSession, IDisposable
{
    // One local edit: its command (not run yet), the layers of its frame after it, and what the workspace held for that frame when the frame was first edited here.
    private sealed record Entry(int Frame, ImageEditCommand Command, LayerSet After, LayerSet Base);

    private readonly Workspace _workspace;
    private readonly string _sprite;
    private readonly WorkspaceImageSession _inner; // the structural operations (resize, frame list) and their dialogs' facts come from here
    private readonly List<Entry> _undo = [];
    private readonly Stack<Entry> _redo = [];
    private bool _own;
    private bool _behind;
    private string? _notice;

    public DeferredImageSession(Workspace workspace, string sprite)
    {
        (_workspace, _sprite) = (workspace, sprite);
        _inner = new WorkspaceImageSession(workspace, sprite);
        workspace.History.Changed += OnHistoryChanged;
    }

    public override int FrameCount => _inner.FrameCount;

    public override bool SupportsLayers => true;

    public override bool Behind => _behind;

    public override bool Pending => _undo.Count > 0;

    public override ResizeContext? ResizeContext => _inner.ResizeContext;

    public override IReadOnlyList<FitStep>? PlanResize(int frame, ResizeSpec spec, bool allFrames, FitOptions? fit = null) => _inner.PlanResize(frame, spec, allFrames, fit);

    public override string? TakeNotice()
    {
        var notice = _notice;
        _notice = null;
        return notice;
    }

    private LayerSet? Local(int frame) => _undo.LastOrDefault(e => e.Frame == frame)?.After;

    private LayerSet Stored(int frame)
    {
        using (_workspace.Lock.Read()) return LayerStore.Of(_workspace.Data).Read(_workspace.Data, _sprite, frame);
    }

    public override PixelBuffer Read(int frame)
    {
        if (Local(frame) is { } local) return local.Flat.Copy();
        using (_workspace.Lock.Read()) return FramePicture.Read(_workspace.Data, _sprite, frame);
    }

    public override LayerSet ReadLayers(int frame)
    {
        _behind = false; // the editor shows what the data and its own edits hold from here on
        return Local(frame) ?? Stored(frame);
    }

    public override void Commit(int frame, ImageEdit edit)
    {
        var before = Local(frame) ?? Stored(frame);
        Add(frame, new ImageEditCommand(_sprite, frame, edit.Selection, edit.Ops, edit.After, edit.Layer), ImageEditCommand.Draw(before, edit.Selection, edit.Ops, edit.After, edit.Layer), before);
    }

    public override bool CommitLayers(int frame, ImageOp op)
    {
        var before = Local(frame) ?? Stored(frame);
        Add(frame, new ImageEditCommand(_sprite, frame, null, [op]), before.Apply(op), before);
        return true;
    }

    private void Add(int frame, ImageEditCommand command, LayerSet after, LayerSet before)
    {
        // The first local edit of a frame remembers what the workspace held then; Apply compares it with what the workspace holds by then.
        var origin = _undo.FirstOrDefault(e => e.Frame == frame)?.Base ?? before;
        _undo.Add(new Entry(frame, command, after, origin));
        _redo.Clear();
        OnPendingChanged();
    }

    // With nothing local the buttons and keys fall through to the workspace history: after an Apply, undo takes that whole step back.
    public override bool CanUndo => _undo.Count > 0 || _workspace.History.CanUndo;

    public override bool CanRedo => _redo.Count > 0 || (_undo.Count == 0 && _workspace.History.CanRedo);

    public override void Undo()
    {
        if (_undo.Count == 0)
        {
            _redo.Clear(); // a local redo was made on a picture the workspace undo is about to change
            _workspace.Undo();
        }
        else
        {
            _redo.Push(_undo[^1]);
            _undo.RemoveAt(_undo.Count - 1);
            OnChanged();
        }

        OnPendingChanged();
    }

    public override void Redo()
    {
        if (_redo.Count == 0)
        {
            // Local edits are pending: a redo of the workspace would write a different step under them, so it waits until they are applied or dropped.
            if (_undo.Count == 0) _workspace.Redo();
        }
        else
        {
            _undo.Add(_redo.Pop());
            OnChanged();
        }

        OnPendingChanged();
    }

    /// <summary>True when a frame with local edits changed in the workspace after the first of these edits (another editor, the sprite tab, an undo): applying would overwrite that change.</summary>
    public bool Conflict
    {
        get
        {
            if (_undo.Count == 0) return false;
            if (FrameCount == 0) return true;
            return _undo.GroupBy(e => e.Frame).Any(g => g.Key >= FrameCount || !SameFrame(g.First().Base, Stored(g.Key)));
        }
    }

    private static bool SameFrame(LayerSet a, LayerSet b) => (a.Width, a.Height) == (b.Width, b.Height) && LayerSet.SameLook(a.Flat, b.Flat);

    /// <summary>
    /// Writes every local edit into the workspace as one undoable step. False, with nothing written, when a frame changed elsewhere and <paramref name="overwrite"/> is false:
    /// the caller asks first, then applies again with overwrite (the local picture of those frames wins).
    /// </summary>
    public bool Apply(bool overwrite = false)
    {
        if (_undo.Count == 0) return true;
        if (!overwrite && Conflict) return false;
        var commands = _undo.Select(e => e.Command).ToList();
        _own = true;
        try
        {
            _workspace.Edit(commands.Count == 1 ? commands[0] : new GroupCommand(commands));
        }
        finally
        {
            _own = false;
        }

        _undo.Clear();
        _redo.Clear();
        OnPendingChanged();
        return true;
    }

    public override void Accept() => Apply();

    /// <summary>Drops every local edit; the editor reads the workspace again. The workspace history is not touched.</summary>
    public override bool Discard()
    {
        _undo.Clear();
        _redo.Clear();
        OnChanged();
        OnPendingChanged();
        return true;
    }

    // A change of the sprite's frames or size writes to the workspace, so the pending edits go in first (one step) and the operation runs on the result.
    // The choice keeps the history in order (the edits, then the resize) and the frame numbers of the local edits valid; the status line says so.
    private void ApplyFirst()
    {
        if (_undo.Count == 0) return;
        if (!Apply()) throw new InvalidOperationException(Loc.Current["image.applyFirstBlocked"]);
        _notice = "image.appliedFirst";
    }

    public override bool CommitResize(int frame, ResizeSpec spec, bool allFrames, string? group, FitOptions? fit = null)
    {
        ApplyFirst();
        return Inner(() => _inner.CommitResize(frame, spec, allFrames, group, fit));
    }

    public override bool ChangeFrames(string operation, int frame)
    {
        ApplyFirst();
        return Inner(() => _inner.ChangeFrames(operation, frame));
    }

    private bool Inner(Func<bool> run)
    {
        _inner.Resync();
        _own = true;
        try
        {
            return run();
        }
        catch
        {
            _notice = null; // the editor shows the refusal; "applied first" would be stale at the next call
            throw;
        }
        finally
        {
            _own = false;
        }
    }

    private void OnHistoryChanged()
    {
        if (_own) return;
        // Not this editor's own write (another editor, the sprite tab, an undo): the canvas picture is older than the data until the reload below runs; local edits stay.
        if (_workspace.History.Last is not null) _behind = true;
        // The history raises this while the workspace is locked for writing, and reading a frame needs the read lock, so the reload waits until it is free.
        Avalonia.Threading.Dispatcher.UIThread.Post(OnChanged);
        Avalonia.Threading.Dispatcher.UIThread.Post(OnPendingChanged);
    }

    public void Dispose()
    {
        _workspace.History.Changed -= OnHistoryChanged;
        _inner.Dispose();
    }
}
