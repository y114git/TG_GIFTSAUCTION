using G3MLib.DataFile;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Editor;

/// <summary>Where the image editor gets its pictures and where an edit goes: the workspace history (a tab) or a window that applies at the end.</summary>
public abstract class ImageSession
{
    public abstract int FrameCount { get; }

    public abstract PixelBuffer Read(int frame);

    /// <summary>True when the session keeps layers (a tab does; the painter window works on one flat picture).</summary>
    public virtual bool SupportsLayers => false;

    /// <summary>The layers of a frame; one layer holding the picture when the session has none.</summary>
    public virtual LayerSet ReadLayers(int frame) => LayerSet.OneLayer(Read(frame));

    /// <summary>Runs one layer operation as an undoable edit; false when the session has no layers.</summary>
    public virtual bool CommitLayers(int frame, ImageOp op) => false;

    /// <summary>Takes an edit the canvas already applied to its picture.</summary>
    public abstract void Commit(int frame, ImageEdit edit);

    /// <summary>True when the data changed behind the editor and the editor has not read it again: an edit made from its picture now would overwrite the newer data, so it is refused and the editor reloads.</summary>
    public virtual bool Behind => false;

    /// <summary>True while edits made in this editor wait for Apply or Discard.</summary>
    public virtual bool Pending => false;

    /// <summary>The language key of a one-time note the editor shows in its status line after a call (the session did something first); null when there is none.</summary>
    public virtual string? TakeNotice() => null;

    /// <summary>Accepts the edits made so far (they stay in the data).</summary>
    public virtual void Accept()
    {
    }

    /// <summary>Takes the pending edits back; false when other edits came in between, so undo has to do it.</summary>
    public virtual bool Discard() => false;

    /// <summary>The pictures of every frame as the editor shows them, for "Save as new": layers are flattened.</summary>
    public virtual IReadOnlyList<PixelBuffer> Pictures() => [.. Enumerable.Range(0, FrameCount).Select(Read)];

    /// <summary>Raised when <see cref="Pending"/> may have changed.</summary>
    public event Action? PendingChanged;

    protected void OnPendingChanged() => PendingChanged?.Invoke();

    public abstract bool CanUndo { get; }

    public abstract bool CanRedo { get; }

    public abstract void Undo();

    public abstract void Redo();

    /// <summary>Raised when the pictures changed behind the editor (an undo, a redo, a frame change), so it reads the frame again.</summary>
    public event Action? Changed;

    /// <summary>What the Resize dialog needs (frame count, texture groups); null when the session cannot resize a whole sprite.</summary>
    public virtual ResizeContext? ResizeContext => null;

    /// <summary>Resizes the frame or every frame as one undoable edit, and moves the sprite to a texture group; false when the session cannot.</summary>
    public virtual bool CommitResize(int frame, ResizeSpec spec, bool allFrames, string? group, FitOptions? fit = null) => false;

    /// <summary>Where the resized frames will go on the texture pages (<see cref="TextureFit"/>), without changing anything; null when the session has no pages.</summary>
    public virtual IReadOnlyList<FitStep>? PlanResize(int frame, ResizeSpec spec, bool allFrames, FitOptions? fit = null) => null;

    /// <summary>The <see cref="ChangeFrames"/> operation that adds an empty frame; the others are the <see cref="FrameListCommand"/> operations.</summary>
    public const string AddBlankFrame = "addBlank";
    /// <summary>Adds, copies or removes a frame; false when the session has no frame list.</summary>
    public virtual bool ChangeFrames(string operation, int frame) => false;

    protected void OnChanged() => Changed?.Invoke();
}

/// <summary>The image editor as a tab: every edit is one <see cref="ImageEditCommand"/> in the workspace history, so undo, redo, the journal and autosave all work.</summary>
public sealed class WorkspaceImageSession : ImageSession, IDisposable
{
    private readonly Workspace _workspace;
    private readonly string _sprite;
    private bool _own;
    private bool _behind;
    private int _checkpointAt;
    private EditCommand? _checkpointTop;
    private readonly HashSet<EditCommand> _mine = [];

    public WorkspaceImageSession(Workspace workspace, string sprite)
    {
        (_workspace, _sprite) = (workspace, sprite);
        workspace.History.Changed += OnHistoryChanged;
        Accept();
    }

    public override bool Behind => _behind;

    /// <summary>Marks the editor as up to date: a session that wrote to the workspace on its own account (the deferred session) calls this before it uses this one.</summary>
    internal void Resync() => _behind = false;

    // The edits since the last Accept: the history grew past the checkpoint (undoing below it is a different state, not a pending edit).
    public override bool Pending
    {
        get
        {
            using (_workspace.Lock.Read()) return HasMine(_workspace.History);
        }
    }

    private bool AboveCheckpoint(EditHistory history) => history.Position > _checkpointAt && (_checkpointAt == 0 || ReferenceEquals(history.Items[_checkpointAt - 1], _checkpointTop));

    // Pending only when one of this editor's own commands is above the checkpoint: an unrelated workspace edit is not this editor's to keep or discard.
    private bool HasMine(EditHistory history) => AboveCheckpoint(history) && history.Items.Skip(_checkpointAt).Take(history.Position - _checkpointAt).Any(_mine.Contains);

    public override void Accept()
    {
        using (_workspace.Lock.Read())
        {
            var history = _workspace.History;
            (_checkpointAt, _checkpointTop) = (history.Position, history.Position > 0 ? history.Items[history.Position - 1] : null);
        }

        _mine.Clear();
        OnPendingChanged();
    }

    public override bool Discard()
    {
        int steps;
        using (_workspace.Lock.Read())
        {
            var history = _workspace.History;
            // Only the edits this editor made go: with another edit in between the undo would take that one too.
            if (!HasMine(history) || history.Items.Skip(_checkpointAt).Take(history.Position - _checkpointAt).Any(c => !_mine.Contains(c))) return false;
            steps = history.Position - _checkpointAt;
        }

        _own = true;
        try
        {
            for (var i = 0; i < steps; i++) _workspace.Undo();
        }
        finally
        {
            _own = false;
        }

        _mine.Clear();
        OnChanged();
        OnPendingChanged();
        return true;
    }

    public override int FrameCount => _workspace.Data.Sprites.FirstOrDefault(s => s.Name.Content == _sprite)?.Textures.Count ?? 0;

    public override PixelBuffer Read(int frame)
    {
        using (_workspace.Lock.Read()) return FramePicture.Read(_workspace.Data, _sprite, frame);
    }

    public override bool SupportsLayers => true;

    public override LayerSet ReadLayers(int frame)
    {
        using (_workspace.Lock.Read())
        {
            _behind = false; // the editor shows what the data holds from here on
            return LayerStore.Of(_workspace.Data).Read(_workspace.Data, _sprite, frame);
        }
    }

    public override bool CommitLayers(int frame, ImageOp op)
    {
        Commit(new ImageEditCommand(_sprite, frame, null, [op]));
        return true;
    }

    public override void Commit(int frame, ImageEdit edit) => Commit(new ImageEditCommand(_sprite, frame, edit.Selection, edit.Ops, edit.After, edit.Layer));

    // Only pages the file holds itself: an external page has no pixels to place on.
    public static IReadOnlyList<string> PagesOf(GameMakerData data) => [.. data.EmbeddedTextures.Where(t => !t.TextureExternal && t.TextureData?.Image is not null).Select(t => t.Name?.Content ?? "")];

    public override ResizeContext? ResizeContext
    {
        get
        {
            using (_workspace.Lock.Read())
                return new ResizeContext(FrameCount, [.. (_workspace.Data.TextureGroupInfo ?? []).Select(g => g.Name.Content ?? "")], SpriteResize.GroupOf(_workspace.Data, _sprite),
                    PagesOf(_workspace.Data), TextureFit.AutomaticMaxSide(_workspace.Data), Box: true,
                    FrameSizes: [.. _workspace.Data.Sprites.First(s => s.Name.Content == _sprite).Textures.Select(t => (t.Texture?.TargetWidth ?? 0, t.Texture?.TargetHeight ?? 0))]);
        }
    }

    public override IReadOnlyList<FitStep>? PlanResize(int frame, ResizeSpec spec, bool allFrames, FitOptions? fit = null)
    {
        using (_workspace.Lock.Read())
        {
            var data = _workspace.Data;
            var sprite = data.Sprites.FirstOrDefault(s => s.Name.Content == _sprite);
            if (sprite?.Textures.ElementAtOrDefault(frame)?.Texture is not { } item) return null;
            // the requests the edit makes: every frame together (each shared picture once), or the one frame
            return TextureFit.Plan(data, allFrames && sprite.Textures.Count > 1
                ? FramesFitCommand.Requests(data, _sprite, [.. Enumerable.Range(0, sprite.Textures.Count)], spec.Width, spec.Height).Requests
                : [TextureFitCommand.RequestFor(data, "sprites", item, spec.Width, spec.Height)], options: fit);
        }
    }

    public override bool CommitResize(int frame, ResizeSpec spec, bool allFrames, string? group, FitOptions? fit = null)
    {
        EditCommand command;
        using (_workspace.Lock.Read()) command = SpriteResize.Build(_workspace.Data, _sprite, frame, spec with { Fit = fit }, allFrames, group);
        Commit(command);
        return true;
    }

    private void Commit(EditCommand command)
    {
        // The editor's picture is older than the data (the reload is still on its way): writing it would silently undo the newer change.
        if (_behind) throw new InvalidOperationException("The sprite changed after this editor read it.");
        _own = true;
        try
        {
            _mine.Add(command);
            _workspace.Edit(command);
        }
        finally
        {
            _own = false;
        }

        OnPendingChanged();
    }

    public override bool CanUndo => _workspace.History.CanUndo;

    public override bool CanRedo => _workspace.History.CanRedo;

    public override void Undo()
    {
        _workspace.Undo();
        OnPendingChanged();
    }

    public override void Redo()
    {
        _workspace.Redo();
        OnPendingChanged();
    }

    public override bool ChangeFrames(string operation, int frame)
    {
        EditCommand command;
        if (operation != AddBlankFrame) command = new FrameListCommand(_sprite, operation, frame);
        else using (_workspace.Lock.Read()) command = FrameListCommand.AddBlank(_workspace.Data.Sprites.First(s => s.Name.Content == _sprite), frame);
        _mine.Add(command);
        var done = _workspace.Edit(command);
        OnPendingChanged();
        return done;
    }

    private void OnHistoryChanged()
    {
        if (_own) return;
        // A change that is not this editor's own (another editor, the sprite editor, an undo): the picture the canvas holds is older than the data until the reload below runs.
        if (_workspace.History.Last is not null) _behind = true;
        // The history raises this while the workspace is locked for writing, and reading the frame needs the read lock, so the reload waits until the lock is free.
        Avalonia.Threading.Dispatcher.UIThread.Post(OnChanged);
        Avalonia.Threading.Dispatcher.UIThread.Post(OnPendingChanged);
    }

    public void Dispose() => _workspace.History.Changed -= OnHistoryChanged;
}

/// <summary>One picture edited in a window: edits stay local (with their own undo) until the window hands the picture back.</summary>
public class LocalImageSession(PixelBuffer picture) : ImageSession
{
    /// <summary>Every undo step keeps a whole picture, so the oldest steps go once there are this many, or once they hold more than <see cref="MaxUndoBytes"/> (always one step stays).</summary>
    public const int MaxUndoSteps = 100;
    public const long MaxUndoBytes = 256L * 1024 * 1024;

    private readonly List<PixelBuffer> _undo = [];
    private readonly List<PixelBuffer> _redo = [];

    private static void Push(List<PixelBuffer> stack, PixelBuffer picture)
    {
        stack.Add(picture);
        var bytes = stack.Sum(p => (long)p.Data.Length);
        while (stack.Count > MaxUndoSteps || (stack.Count > 1 && bytes > MaxUndoBytes))
        {
            bytes -= stack[0].Data.Length;
            stack.RemoveAt(0);
        }
    }

    private static PixelBuffer Pop(List<PixelBuffer> stack)
    {
        var top = stack[^1];
        stack.RemoveAt(stack.Count - 1);
        return top;
    }

    /// <summary>The last picture the editor committed; Apply hands this back.</summary>
    public PixelBuffer Current { get; private set; } = picture.Copy();

    public override int FrameCount => 1;

    public override PixelBuffer Read(int frame) => Current.Copy();

    public override void Commit(int frame, ImageEdit edit)
    {
        Push(_undo, Current);
        _redo.Clear();
        Current = edit.After;
    }

    /// <summary>Takes a whole picture as one undoable step and tells the editor to read it.</summary>
    protected void Replace(PixelBuffer picture)
    {
        Push(_undo, Current);
        _redo.Clear();
        Current = picture;
        OnChanged();
    }

    public override bool CanUndo => _undo.Count > 0;

    public override bool CanRedo => _redo.Count > 0;

    public override void Undo() => Step(_undo, _redo);

    public override void Redo() => Step(_redo, _undo);

    private void Step(List<PixelBuffer> from, List<PixelBuffer> to)
    {
        if (from.Count == 0) return;
        Push(to, Current);
        Current = Pop(from);
        OnChanged();
    }
}
